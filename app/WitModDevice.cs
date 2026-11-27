using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using HidSharp;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Driver Pack 4 (v3.7) — transport HID cho nen tang WitMod
    // (docs/PROTOCOLS-WITMOD-FULL.md). Nhan dien: quet theo bang filter
    // 36 cap VID:PID + usagePage 0xFF1B, sau do lenh 0x0D (chuoi san
    // pham) + 0x01 (khoi support) la probe QUYET DINH. Thiet bi khong
    // qua duoc probe -> chi xem (view-only), khong ghi.
    // Quy tac phien (tu ho so): timeout 300 ms, thu lai 2 lan, writeDelay
    // 5 ms sau moi tac vu da ACK, ACK ghi = echo opcode+sub (goi dau
    // tien cho ACK nghiem ngat toi 100 ms), sau ghi keymap phai poll
    // 0x01 den khi bit isStoringData tat (moi 60 ms, toi da 2000 ms).
    // =====================================================================
    public sealed class WitModDevice : IDisposable
    {
        public bool DryRun = true;
        public bool ExperimentalEnabled;
        public Action<string> Log;

        public sealed class Identification
        {
            public bool Known;
            public int Vid, Pid;
            public string ProductId = "";     // truong CSV index 4 cua lenh 0x0D
            public string ProductName = "";   // HID productName
            public string Serial = "";
            public WitModProtocol.StatusInfo Status;
            public string ModelName = "";
            public string Note = "";
        }

        public Identification LastId { get; private set; }
        public string LastIdSummary = "";

        private bool WritesArmed => ExperimentalEnabled && !DryRun;

        private void GateWriteOrThrow()
        {
            if (!WritesArmed)
                throw new InvalidOperationException(
                    "Ghi WitMod đang bị khóa: cần thiết bị đã qua probe 0x0D/0x01 + BẬT công tắc thử nghiệm + TẮT dry-run.");
        }

        // Tim thiet bi theo bang filter 36 cap VID:PID. Kenh dung la
        // usagePage 0xFF1B (theo ho so); HidSharp ban nay khong lo usage
        // page nen buoc loc quyet dinh la probe 0x0D + 0x01 o Identify().
        private List<HidDevice> FindCandidates()
        {
            var list = new List<HidDevice>();
            foreach (var dev in DeviceList.Local.GetHidDevices())
            {
                try
                {
                    if (WitModProtocol.IsFilterPair(dev.VendorID, dev.ProductID))
                        list.Add(dev);
                }
                catch { }
            }
            return list;
        }

        private HidStream OpenStream(HidDevice dev)
        {
            try
            {
                if (dev.GetMaxOutputReportLength() < WitModProtocol.PacketLen + 1) return null;
                var s = dev.Open();
                if (s != null)
                {
                    s.ReadTimeout = WitModSession.DefaultTimeoutMs;
                    s.WriteTimeout = WitModSession.DefaultTimeoutMs;
                }
                return s;
            }
            catch { return null; }
        }

        // Gui 1 goi (reportId 1 + 63 byte data) va cho dap tra khop matcher.
        // Tra ve goi data 63 byte cua dap tra, hoac null neu het timeout.
        private byte[] SendAndWait(HidStream s, byte[] packet, Func<byte[], bool> matcher, int timeoutMs = WitModSession.DefaultTimeoutMs)
        {
            var send = new byte[WitModProtocol.PacketLen + 1];
            send[0] = WitModProtocol.ReportId;
            Array.Copy(packet, 0, send, 1, WitModProtocol.PacketLen);
            s.Write(send);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var recv = new byte[WitModProtocol.PacketLen + 1];
                int n;
                try { n = s.Read(recv, 0, recv.Length); }
                catch (TimeoutException) { break; }
                catch { break; }
                if (n <= 1) continue;
                var data = new byte[WitModProtocol.PacketLen];
                Array.Copy(recv, 1, data, 0, Math.Min(WitModProtocol.PacketLen, n - 1));
                if (matcher(data)) return data;
            }
            return null;
        }

        private byte[] RequestWithRetries(HidStream s, byte[] packet, Func<byte[], bool> matcher)
        {
            for (int attempt = 0; attempt <= WitModSession.ReadRetries; attempt++)
            {
                var r = SendAndWait(s, packet, matcher);
                if (r != null) return r;
                if (attempt < WitModSession.ReadRetries) Thread.Sleep(WitModSession.RetryBackoffMs);
            }
            return null;
        }

        /// <summary>Probe nhan dien day du tren moi ung vien filter:
        /// 0x0D (toi 3 lan) -> product id; 0x01 -> khoi support; 0x15 -> serial.
        /// DOC — chay that o moi che do.</summary>
        public Identification Identify()
        {
            var candidates = FindCandidates();
            Log?.Invoke($"[WitMod] Quét bảng filter: {candidates.Count} ứng viên VID:PID (kênh WitMod là usagePage 0xFF1B; probe 0x0D/0x01 sẽ quyết định).");
            foreach (var dev in candidates)
            {
                using var s = OpenStream(dev);
                if (s == null) continue;
                string productName = "";
                try { productName = dev.GetProductName() ?? ""; } catch { }

                byte[] productReply = null;
                for (int t = 0; t < 3 && productReply == null; t++)
                    productReply = SendAndWait(s, WitModProtocol.BuildGetProductInfo(),
                        r => WitModProtocol.MatchesOpcode(r, WitModProtocol.OpProductInfo));
                var productId = productReply != null ? WitModProtocol.ParseProductId(productReply) : null;
                var statusReply = RequestWithRetries(s, WitModProtocol.BuildGetStatus(),
                    r => WitModProtocol.MatchesOpcode(r, WitModProtocol.OpStatus));
                var status = statusReply != null ? WitModProtocol.ParseStatus(statusReply) : null;
                if (productId == null || status == null)
                {
                    Log?.Invoke($"[WitMod] Ứng viên {dev.VendorID:X4}:{dev.ProductID:X4} \"{productName}\" không qua được probe 0x0D/0x01 — bỏ qua (view-only).");
                    continue;
                }
                var serialReply = SendAndWait(s, WitModProtocol.BuildGetSerial(),
                    r => WitModProtocol.MatchesOpcode(r, WitModProtocol.OpSerial));
                var id = new Identification
                {
                    Known = true,
                    Vid = dev.VendorID,
                    Pid = dev.ProductID,
                    ProductId = productId,
                    ProductName = productName,
                    Serial = serialReply != null ? WitModProtocol.ParseSerial(serialReply) ?? "" : "",
                    Status = status,
                    ModelName = MapModel(productName, productId),
                };
                id.Note = $"Probe 0x0D: product \"{productId}\"; support: calibration={status.SupportCalibration}, " +
                          $"customLight={status.SupportCustomLight}, deadBand={status.SupportDeadBand}, " +
                          $"pollRate={status.SupportPollRate}, RS={status.SupportRS}, MDS={status.SupportMDS}; " +
                          $"pin {status.BatteryPercent}% (tri-mode {status.TriMode}).";
                LastId = id;
                LastIdSummary = $"WitMod 0x0D product=\"{productId}\", productName=\"{productName}\", serial=\"{id.Serial}\", " +
                                $"support[cal={B(status.SupportCalibration)},light={B(status.SupportCustomLight)},deadband={B(status.SupportDeadBand)},poll={B(status.SupportPollRate)},RS={B(status.SupportRS)},MDS={B(status.SupportMDS)}], pin {status.BatteryPercent}%";
                Log?.Invoke($"[WitMod] Nhận diện: {id.ModelName} ({id.Vid:X4}:{id.Pid:X4}). " + id.Note);
                return id;
            }
            LastIdSummary = "WitMod: không ứng viên nào qua được probe 0x0D/0x01";
            Log?.Invoke("[WitMod] Không thiết bị nào qua được probe quyết định 0x0D + 0x01 — mọi thứ ở chế độ chỉ xem.");
            return new Identification { Known = false, Note = "Không qua được probe 0x0D/0x01." };
        }

        private static string B(bool b) => b ? "1" : "0";

        /// <summary>Map sang mau da biet: app desktop nhan dien K617 HE
        /// bang tien to productName "7153" (8 bien the layout).</summary>
        public static string MapModel(string productName, string productId)
        {
            string name = productName ?? "";
            if (name.StartsWith("7153", StringComparison.OrdinalIgnoreCase))
                return "Redragon K617 HE";
            return "Thiết bị WitMod (chưa có trong bảng mẫu)";
        }

        // Mo stream cua thiet bi da nhan dien (theo VID:PID cua LastId).
        private HidStream OpenIdentified()
        {
            if (LastId == null || !LastId.Known) return null;
            foreach (var dev in FindCandidates())
                if (dev.VendorID == LastId.Vid && dev.ProductID == LastId.Pid)
                {
                    var s = OpenStream(dev);
                    if (s != null) return s;
                }
            return null;
        }

        /// <summary>Doc cau hinh den chinh (0x07 sub=1). DOC — chay that.</summary>
        public WitModProtocol.LightConfig ReadMainLightConfig()
        {
            using var s = OpenIdentified();
            if (s == null) return null;
            var reply = RequestWithRetries(s, WitModProtocol.BuildGetLightConfig(false),
                r => WitModProtocol.MatchesOpcode(r, WitModProtocol.OpMainConfig));
            if (reply == null) return null;
            var cfg = WitModProtocol.LightConfig.FromBytes(WitModProtocol.PayloadOf(reply));
            if (cfg != null)
                Log?.Invoke($"[WitMod] Đèn chính: mode={cfg.Mode}, sáng={cfg.Brightness}, tốc độ={cfg.Speed}, FG=#{cfg.FgR:X2}{cfg.FgG:X2}{cfg.FgB:X2}.");
            return cfg;
        }

        /// <summary>Doc trang keymap 0 (lop mac dinh, 0x18 sub=0x80). DOC — chay that.</summary>
        public string ReadKeymapPageSummary()
        {
            using var s = OpenIdentified();
            if (s == null) return null;
            var reply = RequestWithRetries(s, WitModProtocol.BuildKeymapReadPage(false, 0),
                r => WitModProtocol.MatchesOpcode(r, WitModProtocol.OpKeymap));
            if (reply == null) return "Không đọc được trang keymap 0.";
            var payload = WitModProtocol.PayloadOf(reply);
            int nonZero = payload.Count(b => b != 0);
            string msg = $"Trang keymap 0: {payload.Length} byte, {nonZero} byte khác 0 (14 bản ghi phím × 4 byte).";
            Log?.Invoke("[WitMod] " + msg);
            return msg;
        }

        /// <summary>Doc poll rate + cau hinh trigger (0x21). DOC — chay that.</summary>
        public string ReadTriggerSummary()
        {
            using var s = OpenIdentified();
            if (s == null) return null;
            var rateReply = RequestWithRetries(s, WitModProtocol.BuildGetPollRate(),
                r => WitModProtocol.MatchesOpcode(r, WitModProtocol.OpTrigger));
            int rate = rateReply != null && rateReply.Length > 6 ? rateReply[6] : 0;
            var cfgReply = RequestWithRetries(s, WitModProtocol.BuildGetTriggerConfig(),
                r => WitModProtocol.MatchesOpcode(r, WitModProtocol.OpTrigger));
            var cfg = cfgReply != null ? WitModProtocol.ParseTriggerConfig(cfgReply) : (0, 10, 0);
            string msg = $"Poll rate: {(rate > 0 ? WitModProtocol.PollRateHz(rate) + " Hz (raw " + rate + ")" : "không rõ")}; " +
                         $"trigger: maxTravel raw={cfg.Item1}, unit raw={cfg.Item2}, minTravel raw={cfg.Item3}.";
            Log?.Invoke("[WitMod] " + msg);
            return msg;
        }

        /// <summary>Ghi cau hinh den chinh (0x07 sub=0). Can cong 3 lop;
        /// neu khong chi ghi log goi tin.</summary>
        public bool WriteMainLightConfig(WitModProtocol.LightConfig cfg)
        {
            var packet = WitModProtocol.BuildSetLightConfig(false, cfg);
            if (!WritesArmed || LastId?.Known != true)
            {
                Log?.Invoke("[WitMod] Ghi cấu hình đèn: chỉ log gói tin: " + F75Device.Hex(packet, 24) + " …");
                return false;
            }
            GateWriteOrThrow();
            using var s = OpenIdentified();
            if (s == null) return false;
            var ack = SendAndWait(s, packet,
                r => WitModProtocol.IsAckEcho(r, WitModProtocol.OpMainConfig, 0),
                WitModSession.StrictAckTimeoutMs);
            Thread.Sleep(WitModSession.WriteDelayMs);
            Log?.Invoke(ack != null
                ? "[WitMod] Đã ghi cấu hình đèn THẬT (ACK echo 0x07/0)."
                : "[WitMod] Đã ghi cấu hình đèn (không nhận ACK trong 100 ms — chế độ fire-and-forget như app gốc).");
            return true;
        }

        /// <summary>Poll 0x01 den khi bit isStoringData tat (sau ghi keymap).</summary>
        public bool WaitStoreComplete(HidStream s)
        {
            Thread.Sleep(WitModSession.StoreSettleMs);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < WitModSession.StoreTimeoutMs)
            {
                var reply = SendAndWait(s, WitModProtocol.BuildGetStatus(),
                    r => WitModProtocol.MatchesOpcode(r, WitModProtocol.OpStatus), 200);
                var st = reply != null ? WitModProtocol.ParseStatus(reply) : null;
                if (st != null && !st.IsStoringData) return true;
                Thread.Sleep(WitModSession.StorePollIntervalMs);
            }
            return false;
        }

        /// <summary>Ghi keymap (0x18): tung goi cho ACK echo, goi dau cho
        /// nghiem ngat 100 ms; xong poll isStoringData. Can cong 3 lop.</summary>
        public bool WriteKeymap(bool fnLayer, byte[] records)
        {
            var packets = WitModProtocol.BuildKeymapWrite(fnLayer, records);
            if (!WritesArmed || LastId?.Known != true)
            {
                Log?.Invoke($"[WitMod] Ghi keymap ({packets.Count} gói): chỉ log gói đầu: " +
                            (packets.Count > 0 ? F75Device.Hex(packets[0], 24) : "") + " …");
                return false;
            }
            GateWriteOrThrow();
            using var s = OpenIdentified();
            if (s == null) return false;
            byte sub = fnLayer ? (byte)2 : (byte)0;
            for (int i = 0; i < packets.Count; i++)
            {
                int timeout = i == 0 ? WitModSession.StrictAckTimeoutMs : WitModSession.DefaultTimeoutMs;
                SendAndWait(s, packets[i], r => WitModProtocol.IsAckEcho(r, WitModProtocol.OpKeymap, sub), timeout);
                Thread.Sleep(WitModSession.WriteDelayMs);
            }
            bool done = WaitStoreComplete(s);
            Log?.Invoke(done
                ? "[WitMod] Đã ghi keymap THẬT và thiết bị báo lưu xong (isStoringData=0)."
                : "[WitMod] Đã ghi keymap nhưng poll isStoringData quá 2000 ms — kiểm tra lại thiết bị.");
            return true;
        }

        /// <summary>Ghi deadband (0x26): anh 264 byte, ACK echo tung goi. Can cong 3 lop.</summary>
        public bool WriteDeadband(byte[] image264)
        {
            var packets = WitModProtocol.BuildDeadbandWrite(image264);
            if (!WritesArmed || LastId?.Known != true)
            {
                Log?.Invoke($"[WitMod] Ghi deadband ({packets.Count} gói): chỉ log gói đầu: " +
                            (packets.Count > 0 ? F75Device.Hex(packets[0], 24) : "") + " …");
                return false;
            }
            GateWriteOrThrow();
            using var s = OpenIdentified();
            if (s == null) return false;
            foreach (var p in packets)
            {
                SendAndWait(s, p, r => WitModProtocol.IsAckEcho(r, WitModProtocol.OpDeadband, 1),
                    WitModSession.DefaultTimeoutMs);
                Thread.Sleep(WitModSession.WriteDelayMs);
            }
            Log?.Invoke("[WitMod] Đã ghi deadband THẬT (0x26).");
            return true;
        }

        public void Dispose() { }
    }
}
