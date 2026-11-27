using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HidSharp;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Driver Pack 4 (v3.7) — transport HID cho cac ho "anh em SONiX"
    // (docs/PROTOCOLS-REFERENCE-APP.md). Nguyen tac nhu cac pack truoc:
    //  - Doc (probe/info/trang thai) chay THẬT o moi che do.
    //  - Ghi/stream chi chay that khi: da nhan dien + cong tac thu nghiem
    //    BAT + dry-run TAT. Ngoai ra chi ghi log goi tin (packet-logged).
    //  - Nova75 va WIN60 SE chi o muc builder + log (Nova75: trinh ghi
    //    frame native chua ro; WIN60 SE: hang tu danh dau thu nghiem).
    //  - CANH BAO TRUNG NEN TANG: S75 Pro/Nova75 dung CHUNG VID:PID
    //    0C45:800A voi F75 Max va probe 0xF5 KHONG phan biet duoc chung
    //    voi F75 Max — chi co productName (hint) + lua chon mau cua nguoi
    //    dung. Trang ho B hien canh bao nay ro rang.
    // =====================================================================
    public sealed class SonixSiblingsDevice : IDisposable
    {
        public bool DryRun = true;
        public bool ExperimentalEnabled;
        public Action<string> Log;

        public sealed class Identification
        {
            public bool Known;              // da qua probe cua stack dang chon
            public string Stack = "";       // "B" | "C" | "D" | "E" | "F2"
            public string ModelName = "";
            public int Vid, Pid;
            public string Note = "";
        }

        public Identification LastId { get; private set; }
        public SonixBProbeResult LastProbeB { get; private set; }
        public string LastIdSummary = ""; // tom tat probe cho nut "Sao chep thong tin thiet bi"

        private bool WritesArmed => ExperimentalEnabled && !DryRun;

        private void GateWriteOrThrow()
        {
            if (!WritesArmed)
                throw new InvalidOperationException(
                    "Ghi/stream đang bị khóa: cần BẬT công tắc thử nghiệm VÀ TẮT dry-run. Ở chế độ hiện tại app chỉ ghi log gói tin.");
        }

        private void GateLogOnly(string title, string hexPreview)
        {
            Log?.Invoke($"[SONiX-Sib] {title}: chỉ log (chưa đủ điều kiện ghi thật hoặc stack chỉ hỗ trợ log). {hexPreview}");
        }

        // ============================ HO B ============================
        public sealed class SonixBProbeResult
        {
            public bool AckReady;
            public bool EndReady;
            public int ColorCount;
            public Dictionary<byte, (byte R, byte G, byte B)> Colors = new();
            public string ProductName = "";
            public string Note = "";
        }

        private List<HidStream> OpenSonixBStreams()
        {
            var list = new List<HidStream>();
            foreach (var dev in DeviceList.Local.GetHidDevices(SonixBProtocol.VendorId, SonixBProtocol.ProductId))
            {
                HidStream s = null;
                try
                {
                    if (dev.GetMaxFeatureReportLength() < SonixBProtocol.PacketLen + 1) continue;
                    s = dev.Open();
                    if (s != null) list.Add(s);
                }
                catch { try { s?.Dispose(); } catch { } }
            }
            return list;
        }

        private static byte[] FeatureRoundTrip(HidStream s, byte[] packet)
        {
            var send = new byte[packet.Length + 1];
            Array.Copy(packet, 0, send, 1, packet.Length);
            s.SetFeature(send);
            var recv = new byte[packet.Length + 1];
            s.GetFeature(recv);
            var data = new byte[packet.Length];
            Array.Copy(recv, 1, data, 0, packet.Length);
            return data;
        }

        /// <summary>Probe doc 0xF5: gui lenh 0xF5 (byte[8]=9), kiem ACK,
        /// doc 9 khoi trang thai, roi lenh 0x02. DOC THUAN — chay that o
        /// moi che do. Day cung la buoc nhan dien/xac minh ho B.</summary>
        public SonixBProbeResult ProbeSonixB()
        {
            var result = new SonixBProbeResult();
            foreach (var s in OpenSonixBStreams())
            {
                using (s)
                {
                    try { result.ProductName = s.Device.GetProductName() ?? ""; } catch { }
                    var probe = SonixBProtocol.BuildCommand(SonixBProtocol.OpProbe);
                    var reply = FeatureRoundTrip(s, probe);
                    var ack = SonixBProtocol.CheckAck(reply, SonixBProtocol.OpProbe);
                    if (ack == SonixBProtocol.Ack.Invalid)
                    {
                        result.Note = "Thiết bị 0C45:800A không trả ACK hợp lệ cho probe 0xF5.";
                        Log?.Invoke("[SONiX-B] Probe 0xF5: ACK không hợp lệ.");
                        continue;
                    }
                    result.AckReady = ack == SonixBProtocol.Ack.Ready;
                    var blocks = new List<byte[]>();
                    for (int i = 0; i < SonixBProtocol.StateBlockCount; i++)
                    {
                        var buf = new byte[SonixBProtocol.PacketLen + 1];
                        try { s.GetFeature(buf); } catch { break; }
                        var data = new byte[SonixBProtocol.PacketLen];
                        Array.Copy(buf, 1, data, 0, SonixBProtocol.PacketLen);
                        // Bo qua khoi echo ACK (04 F5 .. 01 .. toan 0) giong lop L99.
                        if (data[0] == 0x04 && data[1] == SonixBProtocol.OpProbe && data[3] == 0x01
                            && data.Skip(4).All(b => b == 0))
                            { i--; continue; }
                        blocks.Add(data);
                    }
                    result.Colors = SonixBProtocol.ParseStateBlocks(blocks);
                    result.ColorCount = result.Colors.Count;
                    var end = FeatureRoundTrip(s, SonixBProtocol.BuildCommand(SonixBProtocol.OpEnd));
                    result.EndReady = SonixBProtocol.CheckAck(end, SonixBProtocol.OpEnd) == SonixBProtocol.Ack.Ready;
                    result.Note = result.AckReady
                        ? $"Probe 0xF5 OK: đọc được {result.ColorCount} màu phím từ {blocks.Count} khối trạng thái; END {(result.EndReady ? "sẵn sàng" : "chưa sẵn sàng")}."
                        : "Probe 0xF5 trả ACK bận (busy) — thử lại khi bàn phím rảnh.";
                    LastProbeB = result;
                    LastIdSummary = $"SONiX-B 0xF5: ACK {(result.AckReady ? "ready" : "busy")}, {result.ColorCount} màu phím, END {(result.EndReady ? "ready" : "?")}; productName=\"{result.ProductName}\"";
                    Log?.Invoke("[SONiX-B] " + result.Note);
                    if (result.AckReady)
                    {
                        string name = (result.ProductName ?? "").ToLowerInvariant();
                        string model =
                            name == "aula-nova75" ? "AULA Nova75" :
                            name.Contains("s75 pro") && !name.Contains("max") && !name.Contains("he") && !name.Contains("ultra")
                                ? "AULA S75 Pro" :
                                "Thiết bị nền SONiX 0C45:800A (chưa phân biệt được S75 Pro / Nova75 / F75 Max bằng probe)";
                        LastId = new Identification
                        {
                            Known = true,
                            Stack = "B",
                            ModelName = model,
                            Vid = SonixBProtocol.VendorId,
                            Pid = SonixBProtocol.ProductId,
                            Note = "Probe 0xF5 KHÔNG phân biệt được S75 Pro/Nova75 với F75 Max (chung VID:PID + chung giao thức). " +
                                   "Nhận diện mẫu dựa vào productName ở trên + mẫu bạn đang chọn; ghi/stream vẫn cần công tắc thử nghiệm + tắt dry-run.",
                        };
                    }
                    return result;
                }
            }
            result.Note = "Không tìm thấy thiết bị 0C45:800A có kênh feature report 0xFF13 (hãy cắm USB-C).";
            Log?.Invoke("[SONiX-B] " + result.Note);
            return result;
        }

        /// <summary>Stream 1 frame len S75 Pro (duong gui JS: 0x20 -> cho
        /// ACK -> 8 khoi -> 0x02). Nova75 chi log (trinh ghi native chua ro).</summary>
        public bool StreamFrameSonixB(string modelName, Dictionary<byte, (byte R, byte G, byte B)> colors)
        {
            bool isNova = (modelName ?? "").Contains("Nova75");
            var blocks = SonixBProtocol.BuildStreamBlocks(SonixBProtocol.S75ProKeyIds, colors);
            if (isNova)
            {
                var frame = SonixBProtocol.BuildFrame512(SonixBProtocol.S75ProKeyIds, colors);
                GateLogOnly("Nova75 stream (frame 512 byte, trình ghi native chưa rõ — chỉ log)",
                    F75Device.Hex(frame, 32) + " …");
                return false;
            }
            if (!WritesArmed)
            {
                GateLogOnly("S75 Pro stream frame", F75Device.Hex(SonixBProtocol.BuildCommand(SonixBProtocol.OpStream), 16)
                    + " + " + F75Device.Hex(blocks[0], 24) + " …");
                return false;
            }
            GateWriteOrThrow();
            foreach (var s in OpenSonixBStreams())
            {
                using (s)
                {
                    FeatureRoundTrip(s, SonixBProtocol.BuildCommand(SonixBProtocol.OpStream));
                    foreach (var ladder in SonixBProtocol.ZeroAckPollLadderMs)
                    {
                        Thread.Sleep(ladder);
                        var buf = new byte[SonixBProtocol.PacketLen + 1];
                        try { s.GetFeature(buf); } catch { }
                        bool allZero = buf.Skip(1).All(b => b == 0);
                        if (!allZero) break;
                    }
                    foreach (var b in blocks)
                    {
                        Thread.Sleep(SonixBProtocol.PacerMs);
                        var send = new byte[b.Length + 1];
                        Array.Copy(b, 0, send, 1, b.Length);
                        s.SetFeature(send);
                    }
                    for (int attempt = 0; attempt <= SonixBProtocol.EndRetries; attempt++)
                    {
                        var endReply = FeatureRoundTrip(s, SonixBProtocol.BuildCommand(SonixBProtocol.OpEnd));
                        if (SonixBProtocol.CheckAck(endReply, SonixBProtocol.OpEnd) != SonixBProtocol.Ack.Busy) break;
                        Thread.Sleep(SonixBProtocol.EndRetryDelayMs);
                    }
                    Log?.Invoke("[SONiX-B] Đã stream 1 frame THẬT lên S75 Pro (0x20 + 8 khối + 0x02).");
                    return true;
                }
            }
            Log?.Invoke("[SONiX-B] Không mở được stream thiết bị để stream frame.");
            return false;
        }

        // ============================ HO C: MAD60 ============================
        private HidStream OpenByVidPid(int vid, int[] pids, int usagePage, int minOut)
        {
            foreach (var pid in pids)
                foreach (var dev in DeviceList.Local.GetHidDevices(vid, pid))
                {
                    HidStream s = null;
                    try
                    {
                        if (dev.GetMaxOutputReportLength() < minOut) continue;
                        s = dev.Open();
                        if (s != null) return s;
                    }
                    catch { try { s?.Dispose(); } catch { } }
                }
            return null;
        }

        private static byte[] ExchangeOutput(HidStream s, byte[] packet, int replyLen, int timeoutMs = 500)
        {
            s.WriteTimeout = timeoutMs;
            s.ReadTimeout = timeoutMs;
            s.Write(packet);
            var reply = new byte[replyLen];
            try
            {
                int n = s.Read(reply, 0, replyLen);
                if (n < replyLen)
                {
                    var full = new byte[replyLen];
                    Array.Copy(reply, full, Math.Max(0, n));
                    return full;
                }
            }
            catch { }
            return reply;
        }

        public sealed class Mad60Info
        {
            public bool VersionOk;
            public int Pid;
            public byte[] InfoPacket;
            public byte[] ColorsBackup;
            public string Note = "";
        }

        /// <summary>Kiem tra MAD60: probe [1] phai tra ve fw protocol v9,
        /// sau do doc goi info [8,65]. DOC — chay that.</summary>
        public Mad60Info ProbeMad60()
        {
            var info = new Mad60Info();
            using var s = OpenByVidPid(Mad60Protocol.VendorId,
                new[] { Mad60Protocol.PidV2, Mad60Protocol.PidOriginal, Mad60Protocol.PidRgb },
                Mad60Protocol.UsagePage, Mad60Protocol.PacketLen);
            if (s == null)
            {
                info.Note = "Không tìm thấy thiết bị 373B:1120/105D/1054 (MAD60) trên kênh output report.";
                Log?.Invoke("[MAD60] " + info.Note);
                return info;
            }
            try { info.Pid = s.Device.ProductID; } catch { }
            var probeReply = ExchangeOutput(s, Mad60Protocol.BuildVersionProbe(), Mad60Protocol.PacketLen);
            info.VersionOk = Mad60Protocol.CheckVersionReply(probeReply);
            if (!info.VersionOk)
            {
                info.Note = $"Probe [1] trả về [{probeReply[1]}, {probeReply[2]}] — không phải firmware protocol v9; app gốc từ chối kết nối, app này cũng vậy.";
                Log?.Invoke("[MAD60] " + info.Note);
                LastIdSummary = $"MAD60 probe: fw protocol KHÔNG phải v9 (reply {probeReply[1]},{probeReply[2]})";
                return info;
            }
            info.InfoPacket = ExchangeOutput(s, Mad60Protocol.BuildGetLightingInfo(), Mad60Protocol.PacketLen);
            info.Note = "MAD60 nhận diện OK: firmware protocol v9, đã đọc gói info [8,65].";
            LastId = new Identification
            {
                Known = true, Stack = "C", Vid = Mad60Protocol.VendorId, Pid = info.Pid,
                ModelName = info.Pid == Mad60Protocol.PidV2 ? "MADLIONS MAD60 HE V2"
                    : info.Pid == Mad60Protocol.PidRgb ? "MADLIONS MAD60 HE (RGB)" : "MADLIONS MAD60 HE",
                Note = "Nhận diện bằng probe bắt tay [1] → fw protocol v9 (probe quyết định của họ C).",
            };
            LastIdSummary = $"MAD60: PID {info.Pid:X4}, fw protocol v9 OK";
            Log?.Invoke("[MAD60] " + info.Note);
            return info;
        }

        /// <summary>Stream 1 frame mau (210 byte) len MAD60: sao luu mau
        /// goc -> vao custom -> 10 goi frame. GHI THAT can cong 3 lop.</summary>
        public bool StreamFrameMad60(byte[] colors210)
        {
            var packets = Mad60Protocol.BuildFramePackets(colors210);
            if (!WritesArmed)
            {
                GateLogOnly("MAD60 stream frame", F75Device.Hex(packets[0], 24) + " … (10 gói)");
                return false;
            }
            GateWriteOrThrow();
            var info = ProbeMad60();
            if (!info.VersionOk || info.InfoPacket == null) return false;
            using var s = OpenByVidPid(Mad60Protocol.VendorId,
                new[] { Mad60Protocol.PidV2, Mad60Protocol.PidOriginal, Mad60Protocol.PidRgb },
                Mad60Protocol.UsagePage, Mad60Protocol.PacketLen);
            if (s == null) return false;
            // Sao luu mau goc (10 header [8,69,...])
            var replies = new List<byte[]>();
            for (int page = 0; page < 5; page++)
                for (int half = 0; half < 2; half++)
                {
                    var r = ExchangeOutput(s, Mad60Protocol.BuildReadColorsHeader(page, half), Mad60Protocol.PacketLen);
                    replies.Add(Mad60Protocol.ParseColorsReply(r));
                }
            var backup = Mad60Protocol.AssembleColors(replies);
            ExchangeOutput(s, Mad60Protocol.BuildEnterCustom(info.InfoPacket, true), Mad60Protocol.PacketLen);
            foreach (var p in packets) ExchangeOutput(s, p, Mad60Protocol.PacketLen, 200);
            // Khoi phuc mau goc roi thoat custom
            foreach (var p in Mad60Protocol.BuildFramePackets(backup)) ExchangeOutput(s, p, Mad60Protocol.PacketLen, 200);
            ExchangeOutput(s, Mad60Protocol.BuildEnterCustom(info.InfoPacket, false), Mad60Protocol.PacketLen);
            Log?.Invoke("[MAD60] Đã stream 1 frame THẬT và khôi phục màu gốc.");
            return true;
        }

        // ============================ HO D: HERO68 HE ============================
        public sealed class Hero68Info
        {
            public bool IdentityOk;
            public byte[] LightingState;
            public string Note = "";
        }

        private static byte[] HeroExchange(HidStream s, byte[] packet)
        {
            // Report: [reportId 9] + 63 byte data
            var send = new byte[Hero68Protocol.PacketLen + 1];
            send[0] = Hero68Protocol.ReportId;
            Array.Copy(packet, 0, send, 1, Hero68Protocol.PacketLen);
            s.WriteTimeout = 500;
            s.ReadTimeout = 500;
            s.Write(send);
            var recv = new byte[Hero68Protocol.PacketLen + 1];
            try { s.Read(recv, 0, recv.Length); } catch { }
            var data = new byte[Hero68Protocol.PacketLen];
            Array.Copy(recv, 1, data, 0, Hero68Protocol.PacketLen);
            return data;
        }

        /// <summary>Nhan dien HERO68 HE: cmd 130 phai tra dung chu ky
        /// [17,0,0,0,0,3]; doc them trang thai den cmd 132. DOC — chay that.</summary>
        public Hero68Info ProbeHero68()
        {
            var info = new Hero68Info();
            using var s = OpenByVidPid(Hero68Protocol.VendorId, new[] { Hero68Protocol.ProductId },
                Hero68Protocol.UsagePage, Hero68Protocol.PacketLen + 1);
            if (s == null)
            {
                info.Note = "Không tìm thấy thiết bị 372E:103E (HERO68 HE).";
                Log?.Invoke("[HERO68] " + info.Note);
                return info;
            }
            var reply = HeroExchange(s, Hero68Protocol.BuildIdentity());
            info.IdentityOk = Hero68Protocol.CheckIdentityReply(reply)
                              && Hero68Protocol.VerifyChecksum(reply);
            if (!info.IdentityOk)
            {
                info.Note = "Đáp trả cmd 130 không khớp chữ ký HERO68 HE ([17,0,0,0,0,3]) — không nhận diện (có thể là bản Air/Mini/Pro/Ultra, app gốc cũng không hỗ trợ).";
                Log?.Invoke("[HERO68] " + info.Note);
                LastIdSummary = "HERO68: chữ ký cmd 130 KHÔNG khớp";
                return info;
            }
            var lightReply = HeroExchange(s, Hero68Protocol.BuildReadLighting());
            info.LightingState = Hero68Protocol.ParseLightingState(lightReply);
            info.Note = $"HERO68 HE nhận diện OK (chữ ký cmd 130 khớp, checksum đúng). Trạng thái đèn: {(info.LightingState != null ? F75Device.Hex(info.LightingState, info.LightingState.Length) : "không đọc được")}.";
            LastId = new Identification
            {
                Known = true, Stack = "D", Vid = Hero68Protocol.VendorId, Pid = Hero68Protocol.ProductId,
                ModelName = "AULA HERO68 HE",
                Note = "Nhận diện bằng chữ ký danh tính cmd 130 + checksum byte 62.",
            };
            LastIdSummary = $"HERO68: chữ ký 130 OK, lighting={(info.LightingState != null ? F75Device.Hex(info.LightingState, info.LightingState.Length) : "?")}";
            Log?.Invoke("[HERO68] " + info.Note);
            return info;
        }

        /// <summary>Stream 1 frame len HERO68 HE theo dung trinh tu ho so:
        /// doc mau goc -> vao stream (1,128)/(4,2)/(4,4) -> goi gom mau +
        /// terminator -> khoi phuc. Cach nhau toi thieu 34 ms/frame.</summary>
        public bool StreamFrameHero68(List<(byte Id, byte R, byte G, byte B)> keys)
        {
            var packets = Hero68Protocol.BuildFramePackets(keys);
            if (!WritesArmed)
            {
                GateLogOnly("HERO68 stream frame", F75Device.Hex(Hero68Protocol.BuildEnterStream1(), 16)
                    + " + " + (packets.Count > 0 ? F75Device.Hex(packets[0], 24) : "") + " …");
                return false;
            }
            GateWriteOrThrow();
            using var s = OpenByVidPid(Hero68Protocol.VendorId, new[] { Hero68Protocol.ProductId },
                Hero68Protocol.UsagePage, Hero68Protocol.PacketLen + 1);
            if (s == null) return false;
            // Sao luu mau goc
            var backupMap = new Dictionary<byte, (byte R, byte G, byte B)>();
            foreach (var rp in Hero68Protocol.BuildReadColorPackets(Hero68Protocol.KeyIds))
            {
                var r = HeroExchange(s, rp);
                foreach (var kv in Hero68Protocol.ParseColorReply(r)) backupMap[kv.Key] = kv.Value;
            }
            var lightReply = HeroExchange(s, Hero68Protocol.BuildReadLighting());
            var lightState = Hero68Protocol.ParseLightingState(lightReply) ?? new byte[7];
            HeroExchange(s, Hero68Protocol.BuildEnterStream1());
            Thread.Sleep(20);
            HeroExchange(s, Hero68Protocol.BuildEnterStream2());
            HeroExchange(s, Hero68Protocol.BuildEnterStream3());
            foreach (var p in packets) HeroExchange(s, p);
            HeroExchange(s, Hero68Protocol.BuildFrameTerminator());
            var backupKeys = Hero68Protocol.KeyIds
                .Select(id => (id, backupMap.TryGetValue(id, out var c) ? c.R : (byte)0,
                                   backupMap.TryGetValue(id, out var c2) ? c2.G : (byte)0,
                                   backupMap.TryGetValue(id, out var c3) ? c3.B : (byte)0)).ToList();
            foreach (var p in Hero68Protocol.BuildRestorePackets(backupKeys, lightState)) HeroExchange(s, p);
            Log?.Invoke("[HERO68] Đã stream 1 frame THẬT và khôi phục màu gốc.");
            return true;
        }

        // ============================ HO E: WIN60 HE ============================
        public sealed class Win60HeInfo
        {
            public bool Ok;
            public byte[] LightingState;
            public string Note = "";
        }

        private static void Win60Send(HidStream s, byte[] packet)
        {
            var send = new byte[Win60HeProtocol.PacketLen + 1];
            send[0] = Win60HeProtocol.ReportId;
            Array.Copy(packet, 0, send, 1, Win60HeProtocol.PacketLen);
            s.Write(send);
        }

        /// <summary>Doc trang thai den WIN60 HE (lenh 7). DOC — chay that.</summary>
        public Win60HeInfo ProbeWin60He()
        {
            var info = new Win60HeInfo();
            using var s = OpenByVidPid(Win60HeProtocol.VendorId, new[] { Win60HeProtocol.ProductId },
                Win60HeProtocol.UsagePage, Win60HeProtocol.PacketLen + 1);
            if (s == null)
            {
                info.Note = "Không tìm thấy thiết bị 2E3C:C365 trên usage page 0xFF33 (WIN60 HE).";
                Log?.Invoke("[WIN60HE] " + info.Note);
                return info;
            }
            Win60Send(s, Win60HeProtocol.BuildReadLighting());
            s.ReadTimeout = 500;
            var recv = new byte[Win60HeProtocol.PacketLen + 1];
            try { s.Read(recv, 0, recv.Length); } catch { }
            var data = new byte[Win60HeProtocol.PacketLen];
            Array.Copy(recv, 1, data, 0, Win60HeProtocol.PacketLen);
            info.LightingState = Win60HeProtocol.ParseLightingState(data);
            info.Ok = info.LightingState != null;
            info.Note = info.Ok
                ? $"WIN60 HE trả trạng thái đèn hợp lệ (12 byte tại offset 5, ngữ nghĩa chưa giải mã): {F75Device.Hex(info.LightingState, info.LightingState.Length)}."
                : "Đáp trả lệnh 7 không hợp lệ ([0]!=7 hoặc [4]<12).";
            if (info.Ok)
            {
                LastId = new Identification
                {
                    Known = true, Stack = "E", Vid = Win60HeProtocol.VendorId, Pid = Win60HeProtocol.ProductId,
                    ModelName = "AULA WIN60 HE",
                    Note = "Nhận diện bằng đáp trả hợp lệ của lệnh đọc đèn 7 trên usage page 0xFF33.",
                };
                LastIdSummary = $"WIN60 HE: lệnh 7 OK, state={F75Device.Hex(info.LightingState, info.LightingState.Length)}";
            }
            Log?.Invoke("[WIN60HE] " + info.Note);
            return info;
        }

        /// <summary>Stream 1 frame luoi 132 o len WIN60 HE: enable (23) ->
        /// 8 goi du lieu (15) voi pacer 2 ms -> disable.</summary>
        public bool StreamFrameWin60He(byte[] rgb396)
        {
            var packets = Win60HeProtocol.BuildFramePackets(rgb396);
            if (!WritesArmed)
            {
                GateLogOnly("WIN60 HE stream frame", F75Device.Hex(Win60HeProtocol.BuildStreamEnable(true), 16)
                    + " + " + F75Device.Hex(packets[0], 24) + " … (8 gói)");
                return false;
            }
            GateWriteOrThrow();
            using var s = OpenByVidPid(Win60HeProtocol.VendorId, new[] { Win60HeProtocol.ProductId },
                Win60HeProtocol.UsagePage, Win60HeProtocol.PacketLen + 1);
            if (s == null) return false;
            Win60Send(s, Win60HeProtocol.BuildStreamEnable(true));
            Thread.Sleep(50);
            foreach (var p in packets)
            {
                Win60Send(s, p);
                Thread.Sleep(Win60HeProtocol.PacerMs);
            }
            Win60Send(s, Win60HeProtocol.BuildStreamEnable(false));
            Log?.Invoke("[WIN60HE] Đã stream 1 frame THẬT (lưới 132 ô).");
            return true;
        }

        // ============================ HO F2: WIN60 SE (log-only) ============================
        /// <summary>WIN60 SE: chi dung goi va ghi log. Hang tu danh dau mau
        /// nay "thu nghiem/chua kiem chung" trong UI cua chinh ho; app nay
        /// khong ghi that len no.</summary>
        public void LogWin60SeDemo()
        {
            byte session = Win60SeProtocol.NewSessionByte(new Random());
            var info = Win60SeProtocol.BuildPacket(session, 1, 1);
            var frame = new byte[Win60SeProtocol.StreamEntries * 3];
            for (int i = 0; i < Win60SeProtocol.StreamEntries; i++)
            {
                frame[i * 3] = 0x7C; frame[i * 3 + 1] = 0x3A; frame[i * 3 + 2] = 0xED;
            }
            var packets = Win60SeProtocol.BuildFramePackets(session, frame);
            GateLogOnly($"WIN60 SE (session 0x{session:X2}) exchange(1,1)", F75Device.Hex(info, info.Length));
            GateLogOnly("WIN60 SE stream frame (4 gói)", F75Device.Hex(packets[0], 24) + " …");
            LastIdSummary = $"WIN60 SE: chỉ log (session 0x{session:X2}); nhà sản xuất tự đánh dấu thử nghiệm";
        }

        public void Dispose() { }
    }
}
