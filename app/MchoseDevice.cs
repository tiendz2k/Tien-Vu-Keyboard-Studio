using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HidSharp;

namespace TienKeyboardStudio3
{
    /// <summary>Ket qua dinh danh mot thiet bi MCHOSE ho CZ (GLW/QHW).</summary>
    public sealed class MchoseIdentification
    {
        public bool Known;                    // true = dinh danh khop registry + probe doc OK -> moi duoc ghi
        public MchoseCzEntry Entry;           // muc registry khop (neu co)
        public int Vid, Pid;
        public string Note;                   // giai thich (vd ly do chi-xem)
        public GlwProtocol.GlwInfo GlwInfo;   // thong tin doc duoc (stack GLW)
        public QhwProtocol.QhwBaseInfo QhwInfo; // thong tin doc duoc (stack QHW)

        public string ModelName => Entry != null ? Entry.DisplayName : null;
    }

    // =====================================================================
    // Transport HID cho MCHOSE ho CZ (Driver Pack 3, v3.6):
    //  - Stack GLW: kenh vendor usagePage 0xFF00, reportId 0, goi 64 byte
    //    qua output/input report, nhip mac dinh cua SDK hang: timeout
    //    1000 ms, retry 3, nghi 20 ms giua cac goi.
    //  - Stack QHW: kenh vendor usagePage 0xFF80, reportId 77, goi 31
    //    byte, sequence tang dan theo tung lenh.
    //
    // QUY TAC AN TOAN (hard-coded, giong Driver Pack 1 & 2):
    // - Dinh danh la lenh CHI DOC — chay live khi nguoi dung bam nut:
    //   GLW doc getInfo (cmd 3) va doi chieu dap tra 0xAA/cmd/offset;
    //   QHW doc getBaseInfo (0x0900) va doi chieu VID/PID ma chinh thiet
    //   bi tu khai voi registry. Khong khop -> Known=false (chi xem).
    // - Lenh ghi CHI gui that khi: (1) cong tac Thu nghiem BAT,
    //   (2) Dry-run toan cuc TAT, (3) da dinh danh Known. Thieu mot
    //   dieu kien -> chi log goi.
    // - Stack legacy (0x11/0x12) chi co builder — transport KHONG mo,
    //   moi thao tac deu chi ghi log (dry-run-only) va nhan ro nhu vay.
    // - Ho nay CHUA kiem chung tren phan cung that; rieng checksum GLW
    //   la gia thuyet sum-mod-256 (ham goc khong nam trong file da thu
    //   thap) — xem MchoseProtocol.cs va BUILD-INFO.
    // =====================================================================
    public sealed class MchoseDevice : IDisposable
    {
        public bool DryRun { get; set; } = true;
        public bool ExperimentalEnabled { get; set; } = false;
        public Action<string> Log { get; set; } = _ => { };

        public bool WritesArmed => ExperimentalEnabled && !DryRun;
        public MchoseIdentification Identification { get; private set; }

        private readonly object _gate = new object();
        private HidStream _stream;
        private int _outLen = GlwProtocol.PacketLen;
        private int _inLen = GlwProtocol.PacketLen;
        private byte _qhwSeq;
        private bool _disposed;

        private static readonly int[] KnownVids =
            MchoseRegistryData.CzEntries.Select(e => e.Vid).Distinct().ToArray();

        // ------------------------------------------------------------------
        private bool EnsureStream(MchoseCzEntry entry)
        {
            if (_stream != null) return true;
            HidDevice target = null;
            try
            {
                var candidates = new List<HidDevice>();
                foreach (var d in DeviceList.Local.GetHidDevices())
                {
                    if (entry != null && d.VendorID != entry.Vid) continue;
                    if (entry == null && !KnownVids.Contains(d.VendorID)) continue;
                    int maxOut = 0;
                    try { maxOut = d.GetMaxOutputReportLength(); } catch { }
                    int need = entry != null && entry.IsQhw ? QhwProtocol.PacketLen : GlwProtocol.PacketLen;
                    if (maxOut >= need) candidates.Add(d);
                }
                if (entry != null)
                {
                    target = candidates.FirstOrDefault(d => d.ProductID == entry.PidVendor)
                             ?? candidates.FirstOrDefault(d => d.ProductID == entry.PidMain)
                             ?? candidates.FirstOrDefault();
                }
                else target = candidates.FirstOrDefault();
                if (target != null)
                {
                    try { _outLen = Math.Max(GlwProtocol.PacketLen, target.GetMaxOutputReportLength()); } catch { }
                    try { _inLen = Math.Max(GlwProtocol.PacketLen, target.GetMaxInputReportLength()); } catch { }
                }
            }
            catch (Exception ex)
            {
                Log("[MCHOSE] Loi liet ke HID: " + ex.Message);
                return false;
            }
            if (target == null)
            {
                Log("[MCHOSE] Khong tim thay kenh vendor MCHOSE (usagePage 0xFF00 GLW / 0xFF80 QHW). Kiem tra cap USB.");
                return false;
            }
            if (!target.TryOpen(out _stream))
            {
                Log("[MCHOSE] Khong mo duoc kenh vendor (thiet bi dang ban?).");
                return false;
            }
            _stream.ReadTimeout = 1000;
            _stream.WriteTimeout = 1000;
            Log($"[MCHOSE] Da mo kenh vendor {target.VendorID:X4}:{target.ProductID:X4}.");
            return true;
        }

        // ---------------- GLW transport ----------------
        private void GlwSend(byte[] packet64)
        {
            var buf = new byte[_outLen];
            int offset = _outLen - GlwProtocol.PacketLen;
            Array.Copy(packet64, 0, buf, offset, GlwProtocol.PacketLen);
            _stream.Write(buf, 0, buf.Length);
            Thread.Sleep(20);
        }

        private byte[] GlwTryRead()
        {
            var buf = new byte[_inLen];
            try
            {
                int n = _stream.Read(buf, 0, buf.Length);
                if (n <= 0) return null;
                var data = new byte[GlwProtocol.PacketLen];
                int take = Math.Min(GlwProtocol.PacketLen, n);
                // Goi dap tra nam o cuoi buffer (cung quy uoc voi cac device khac cua app)
                Array.Copy(buf, n - take, data, GlwProtocol.PacketLen - take, take);
                return data;
            }
            catch { return null; }
        }

        /// <summary>Gui mot goi GLW va doi dap tra khop (0xAA/cmd/offset). Nem loi neu khong khop.</summary>
        public byte[] GlwTransact(string title, byte[] packet)
        {
            lock (_gate)
            {
                if (Identification == null || Identification.Entry == null || !Identification.Entry.IsGlw)
                {
                    // Doc dinh danh duoc phep truoc khi co Identification (Identify dung duong rieng)
                    throw new InvalidOperationException("Kenh GLW chua san sang — hay chay Nhan dien truoc.");
                }
                return GlwTransactLocked(title, packet);
            }
        }

        private byte[] GlwTransactLocked(string title, byte[] packet)
        {
            if (!EnsureStream(Identification?.Entry))
                throw new InvalidOperationException("Khong mo duoc kenh vendor GLW.");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                GlwSend(packet);
                var reply = GlwTryRead();
                if (reply != null && GlwProtocol.MatchReply(packet, reply))
                {
                    Log($"[MCHOSE-GLW] {title}: <- {F75Device.Hex(reply, 16)}");
                    return reply;
                }
            }
            throw new InvalidOperationException($"GLW '{title}': khong nhan duoc dap tra khop (0xAA/cmd/offset) sau 3 lan — dung lai de an toan.");
        }

        /// <summary>Doc mot vung GLW theo manh 56 byte, ghep du lieu huu ich.</summary>
        public byte[] GlwReadRegion(string title, byte cmd, int baseOffset, int total)
        {
            lock (_gate)
            {
                var packets = GlwProtocol.ChunkRead(cmd, baseOffset, total);
                var data = new List<byte>();
                foreach (var p in packets)
                {
                    var reply = GlwTransactLocked(title, p);
                    data.AddRange(GlwProtocol.ReplyData(reply));
                }
                Log($"[MCHOSE-GLW] {title}: doc duoc {data.Count} byte.");
                return data.ToArray();
            }
        }

        // ---------------- QHW transport ----------------
        private void QhwSend(byte[] packet31)
        {
            int total = _outLen >= QhwProtocol.PacketLen + 1 ? _outLen : QhwProtocol.PacketLen + 1;
            var buf = new byte[total];
            int offset = total - (QhwProtocol.PacketLen + 1);
            buf[offset] = QhwProtocol.ReportId;
            Array.Copy(packet31, 0, buf, offset + 1, QhwProtocol.PacketLen);
            _stream.Write(buf, 0, buf.Length);
            Thread.Sleep(20);
        }

        private byte[] QhwTryRead()
        {
            int total = _inLen >= QhwProtocol.PacketLen + 1 ? _inLen : QhwProtocol.PacketLen + 1;
            var buf = new byte[total];
            try
            {
                int n = _stream.Read(buf, 0, buf.Length);
                if (n <= 0) return null;
                var data = new byte[QhwProtocol.PacketLen];
                int take = Math.Min(QhwProtocol.PacketLen, n);
                Array.Copy(buf, n - take, data, QhwProtocol.PacketLen - take, take);
                return data;
            }
            catch { return null; }
        }

        /// <summary>Gui mot lenh QHW, tra payload dap tra (checksum XOR phai hop le).</summary>
        public byte[] QhwTransact(string title, ushort cmd, byte[] payload)
        {
            lock (_gate)
            {
                if (!EnsureStream(Identification?.Entry))
                    throw new InvalidOperationException("Khong mo duoc kenh vendor QHW.");
                var packet = QhwProtocol.Build(cmd, payload, _qhwSeq++);
                _stream.ReadTimeout = 1000;
                QhwSend(packet);
                var reply = QhwTryRead();
                if (reply == null)
                    throw new InvalidOperationException($"QHW '{title}': khong co dap tra.");
                if (!QhwProtocol.VerifyChecksum(reply))
                    throw new InvalidOperationException($"QHW '{title}': checksum XOR cua dap tra khong hop le — dung lai de an toan.");
                var data = QhwProtocol.PayloadOf(reply);
                Log($"[MCHOSE-QHW] {title}: <- {F75Device.Hex(data, 20)}");
                return data;
            }
        }

        // ---------------- Dinh danh (chi doc, chay live) ----------------
        /// <summary>
        /// Dinh danh thiet bi MCHOSE CZ dang ket noi so voi model dang chon
        /// (vid/pid cua model). GLW: probe getInfo (cmd 3). QHW: probe
        /// getBaseInfo (0x0900) doi chieu VID/PID thiet bi tu khai.
        /// Khong khop -> Known=false (chi xem).
        /// </summary>
        public MchoseIdentification Identify(int vid, int pid)
        {
            lock (_gate)
            {
                var result = new MchoseIdentification { Vid = vid, Pid = pid, Known = false };
                var entry = MchoseRegistryData.MatchCz(vid, pid);
                result.Entry = entry;
                if (entry == null)
                {
                    result.Note = "VID:PID của model đang chọn không có trong registry MCHOSE CZ — chế độ CHỈ XEM.";
                    Identification = result;
                    return result;
                }
                if (!EnsureStream(entry))
                {
                    result.Note = "Không mở được kênh vendor MCHOSE — không probe được danh tính. Thiết bị ở chế độ chỉ xem.";
                    Identification = result;
                    return result;
                }
                Identification = result; // tam thoi de cac transact dinh danh dung duoc entry
                try
                {
                    if (entry.IsGlw)
                    {
                        var req = GlwProtocol.BuildRead(GlwProtocol.CmdGetInfo, 0, 56);
                        var reply = GlwTransactLocked("getInfo (định danh GLW)", req);
                        var info = GlwProtocol.ParseInfo(GlwProtocol.ReplyData(reply));
                        result.GlwInfo = info;
                        result.Known = true;
                        result.Note = "Đã định danh: kênh vendor GLW trả lời getInfo khớp mẫu đang chọn (PID trong registry + probe đọc OK) — lệnh ghi được phép mở khoá (vẫn cần công tắc Thử nghiệm + tắt Dry-run). LƯU Ý: checksum GLW là giả thuyết sum-mod-256 chưa kiểm chứng.";
                        Log($"[MCHOSE] Dinh danh GLW: {entry.DisplayName} — FW {info.FwVer}, RF FW {info.RfFwVer}.");
                    }
                    else
                    {
                        var data = QhwTransact("getBaseInfo (định danh QHW)", QhwProtocol.CmdGetBaseInfo, null);
                        var info = QhwProtocol.ParseBaseInfo(data);
                        result.QhwInfo = info;
                        if (info.Vid == entry.Vid && (info.Pid == entry.PidMain || info.Pid == entry.PidVendor))
                        {
                            result.Known = true;
                            result.Note = "Đã định danh: getBaseInfo (0x0900) tự khai VID/PID khớp registry — lệnh ghi được phép mở khoá (vẫn cần công tắc Thử nghiệm + tắt Dry-run).";
                            Log($"[MCHOSE] Dinh danh QHW: {entry.DisplayName} — thiet bi tu khai {info.Vid:X4}:{info.Pid:X4}, pin {info.BatteryLevel}%.");
                        }
                        else
                        {
                            result.Note = $"getBaseInfo tự khai {info.Vid:X4}:{info.Pid:X4} KHÔNG khớp mẫu đang chọn ({entry.Vid:X4}:{entry.PidMain:X4}) — chế độ CHỈ XEM.";
                            Log($"[MCHOSE] QHW tu khai {info.Vid:X4}:{info.Pid:X4} != registry {entry.Vid:X4}:{entry.PidMain:X4}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Known = false;
                    result.Note = "Probe định danh thất bại: " + ex.Message + " — thiết bị ở chế độ CHỈ XEM.";
                    Log("[MCHOSE] Dinh danh that bai: " + ex.Message);
                }
                Identification = result;
                return result;
            }
        }

        // ---------------- Khoa ghi ----------------
        private void GateWriteOrThrow()
        {
            if (Identification == null || !Identification.Known)
                throw new InvalidOperationException(
                    "Chua dinh danh khop registry MCHOSE — khong cho ghi. Hay chay 'Nhan dien thiet bi' truoc; dinh danh khong khop thi luon o che do chi xem.");
        }

        private bool GateLogOnly(string title, string hexPreview)
        {
            string why = !ExperimentalEnabled ? "cong tac Thu nghiem dang TAT" : "Dry-run toan cuc dang BAT";
            Log($"[MCHOSE - CHI LOG] {title}: se gui goi (khong gui that — {why}).");
            if (hexPreview != null) Log($"    -> {hexPreview}");
            return false;
        }

        /// <summary>Ghi mot vung GLW theo manh (trigger/DKS/MT/TGL/keymap/macro...). Cung khoa 3 lop.</summary>
        public bool GlwWriteRegion(string title, byte setCmd, int baseOffset, byte[] data)
        {
            lock (_gate)
            {
                GateWriteOrThrow();
                var packets = GlwProtocol.ChunkWrite(setCmd, baseOffset, data);
                if (!WritesArmed)
                    return GateLogOnly($"{title} ({packets.Count} goi)", F75Device.Hex(packets[0], 24));
                foreach (var p in packets) GlwTransactLocked(title, p);
                Log($"[MCHOSE-GLW] {title}: da ghi that {packets.Count} goi ({data.Length} byte).");
                return true;
            }
        }

        /// <summary>Ghi mot lenh GLW don (hieu chuan, khoi phuc goc...). Cung khoa 3 lop.</summary>
        public bool GlwWriteSingle(string title, byte[] packet)
        {
            lock (_gate)
            {
                GateWriteOrThrow();
                if (!WritesArmed) return GateLogOnly(title, F75Device.Hex(packet, 24));
                GlwTransactLocked(title, packet);
                Log($"[MCHOSE-GLW] {title}: da gui that.");
                return true;
            }
        }

        /// <summary>Ghi mot lenh QHW. Cung khoa 3 lop; timeout rieng cho khoi phuc (8 giay, khong retry).</summary>
        public bool QhwWrite(string title, ushort cmd, byte[] payload, int readTimeoutMs = 1000)
        {
            lock (_gate)
            {
                GateWriteOrThrow();
                if (!WritesArmed)
                {
                    var preview = QhwProtocol.Build(cmd, payload, _qhwSeq);
                    return GateLogOnly(title, F75Device.Hex(preview, 24));
                }
                if (!EnsureStream(Identification?.Entry))
                    throw new InvalidOperationException("Khong mo duoc kenh vendor QHW.");
                var packet = QhwProtocol.Build(cmd, payload, _qhwSeq++);
                _stream.ReadTimeout = readTimeoutMs;
                QhwSend(packet);
                var reply = QhwTryRead();
                if (reply != null && !QhwProtocol.VerifyChecksum(reply))
                    throw new InvalidOperationException($"QHW '{title}': checksum dap tra khong hop le — dung lai de an toan.");
                Log(reply == null
                    ? $"[MCHOSE-QHW] {title}: da gui that (khong co dap tra)."
                    : $"[MCHOSE-QHW] {title}: da gui that, dap tra OK.");
                return true;
            }
        }

        /// <summary>
        /// Stack legacy (0x11/0x12): CHI LOG — transport that khong mo o
        /// ban nay. Tra ve false luon; goi logic + dang day duoc ghi log.
        /// </summary>
        public bool LegacyLogOnly(string title, byte[] logicalPacket)
        {
            lock (_gate)
            {
                var wire = MchoseLegacyProtocol.ToWire(logicalPacket);
                Log($"[MCHOSE-LEGACY - CHI LOG] {title}: transport legacy chua mo (dry-run-only) — goi logic se la:");
                Log($"    logic -> {F75Device.Hex(logicalPacket, 32)}");
                Log($"    wire  -> {F75Device.Hex(wire, 32)}  (sau reportId, XOR 0xFF)");
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _stream?.Dispose(); } catch { }
            _stream = null;
        }
    }
}
