using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HidSharp;

namespace TienKeyboardStudio3
{
    /// <summary>Ket qua dinh danh mot thiet bi kb_by_v2 bang lenh 0x82.</summary>
    public sealed class KbByV2Identification
    {
        public bool Known;                 // true = khop registry -> moi duoc ghi
        public string ModelName;           // ten model da dinh danh (neu Known)
        public string Method;              // cach dinh danh (hien len UI)
        public byte[] IdBytes;             // 6 byte dinh danh doc duoc (neu co)
        public int Vid, Pid;
        public string Note;                // giai thich (vd ly do chi-xem)

        public string IdHex => IdBytes == null ? "(không đọc được)"
            : string.Join(" ", IdBytes.Select(b => b.ToString("X2")));
        public string UuidHex => IdBytes == null ? ""
            : "0x" + KbByV2Protocol.IdentityToUuid(IdBytes).ToString("X12");
    }

    // =====================================================================
    // Transport HID cho ho kb_by_v2 (Driver Pack 1, v3.4).
    //
    // QUY TAC AN TOAN (hard-coded):
    // - Dinh danh (0x82) la lenh CHI DOC — chay live khi nguoi dung bam nut.
    // - Lenh ghi CHI gui that khi: (1) cong tac Thu nghiem BAT,
    //   (2) Dry-run toan cuc TAT, va (3) thiet bi da dinh danh KHOP
    //   registry (Identify() tra Known). Thieu mot dieu kien -> chi log.
    // - Sau set_keys cho 100 ms (tai lieu goc). ACK = echo 7 byte header;
    //   echo sai -> dung va bao loi.
    // - Duong dongle 2.4G: ban nay CHI log goi (dry-run-only) — chua mo
    //   transport that len dongle. Ghi ro trong BUILD-INFO.
    // - Ho nay CHUA kiem chung tren phan cung that (khong ai trong doi
    //   so huu cac mau phim nay) — moi trang cua ho deu gan nhan nay.
    // =====================================================================
    public sealed class KbByV2Device : IDisposable
    {
        public bool DryRun { get; set; } = true;
        public bool ExperimentalEnabled { get; set; } = false;
        public Action<string> Log { get; set; } = _ => { };

        public bool WritesArmed => ExperimentalEnabled && !DryRun;
        public KbByV2Identification Identification { get; private set; }

        private readonly object _gate = new object();
        private HidStream _stream;
        private bool _disposed;

        private static readonly int[] KnownPids =
        {
            KbByV2Protocol.PidShared, KbByV2Protocol.PidK1Alt,
            KbByV2Protocol.PidC98IsoDe, KbByV2Protocol.PidC98IsoFr,
        };

        private bool EnsureStream()
        {
            if (_stream != null) return true;
            HidDevice target = null;
            try
            {
                foreach (var d in DeviceList.Local.GetHidDevices(KbByV2Protocol.VendorId))
                {
                    if (!KnownPids.Contains(d.ProductID)) continue;
                    int maxFeature = 0;
                    try { maxFeature = d.GetMaxFeatureReportLength(); } catch { }
                    // Kenh kb_by_v2: feature report 519 byte (+1 byte report ID)
                    if (maxFeature >= KbByV2Protocol.PayloadLength) { target = d; break; }
                }
            }
            catch (Exception ex)
            {
                Log("[KB_BY_V2] Loi liet ke HID: " + ex.Message);
                return false;
            }
            if (target == null)
            {
                Log("[KB_BY_V2] Khong tim thay kenh kb_by_v2 (VID 258A, feature report 519 byte). Kiem tra cap USB — dongle 2.4G chua ho tro ghi.");
                return false;
            }
            if (!target.TryOpen(out _stream))
            {
                Log("[KB_BY_V2] Khong mo duoc kenh lenh (thiet bi dang ban?).");
                return false;
            }
            _stream.ReadTimeout = 400;
            _stream.WriteTimeout = 1000;
            Log($"[KB_BY_V2] Da mo kenh lenh {target.VendorID:X4}:{target.ProductID:X4}.");
            return true;
        }

        private void SendFeature(byte[] payload519)
        {
            var buf = new byte[KbByV2Protocol.PayloadLength + 1];
            buf[0] = KbByV2Protocol.ReportId;
            Array.Copy(payload519, 0, buf, 1, KbByV2Protocol.PayloadLength);
            _stream.SetFeature(buf, 0, buf.Length);
        }

        private byte[] TryGetFeature()
        {
            var buf = new byte[KbByV2Protocol.PayloadLength + 1];
            buf[0] = KbByV2Protocol.ReportId;
            try
            {
                _stream.GetFeature(buf, 0, buf.Length);
                var data = new byte[KbByV2Protocol.PayloadLength];
                Array.Copy(buf, 1, data, 0, KbByV2Protocol.PayloadLength);
                return data;
            }
            catch { return null; }
        }

        /// <summary>
        /// Dinh danh thiet bi bang lenh 0x82 (CHI DOC — chay live).
        /// Thu probe UUID (len 6) truoc, roi probe chu ky F75/K1 (len 10);
        /// doi chieu registry. Khong khop -> Known=false (chi xem).
        /// </summary>
        public KbByV2Identification Identify(int vid, int pid)
        {
            lock (_gate)
            {
                var result = new KbByV2Identification { Vid = vid, Pid = pid, Known = false };
                if (!EnsureStream())
                {
                    result.Note = "Không mở được kênh kb_by_v2 — không đọc được định danh. Thiết bị ở chế độ chỉ xem.";
                    Identification = result;
                    return result;
                }
                foreach (var probe in new[] { KbByV2Protocol.BuildUuidProbe(), KbByV2Protocol.BuildIdentityProbe() })
                {
                    SendFeature(probe);
                    Thread.Sleep(30);
                    var reply = TryGetFeature();
                    var id = KbByV2Protocol.ParseIdentityBytes(reply);
                    if (id == null) continue;
                    result.IdBytes = id;
                    var entry = KbByV2Protocol.MatchIdentity(id);
                    if (entry != null)
                    {
                        result.Known = true;
                        result.ModelName = entry.ModelName;
                        result.Method = entry.Method;
                        result.Note = "Đã định danh khớp registry — lệnh ghi được phép mở khoá (vẫn cần công tắc Thử nghiệm + tắt Dry-run).";
                        Log($"[KB_BY_V2] Dinh danh: {entry.ModelName} — {entry.Method}; UUID {result.UuidHex}.");
                        Identification = result;
                        return result;
                    }
                    Log($"[KB_BY_V2] Doc duoc dinh danh {result.IdHex} nhung KHONG khop registry.");
                    break;
                }
                result.Note = "Định danh không khớp registry (hoặc không đọc được) — thiết bị lạ trên VID:PID dùng chung. Chế độ CHỈ XEM: app không cho ghi.";
                Identification = result;
                return result;
            }
        }

        /// <summary>Gui lenh DOC (0x80+) — chay live, tra payload dap tra (519 byte) hoac null.</summary>
        public byte[] SendRead(string title, byte[] packet)
        {
            lock (_gate)
            {
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh kb_by_v2.");
                SendFeature(packet);
                Thread.Sleep(30);
                var reply = TryGetFeature();
                Log(reply == null
                    ? $"[KB_BY_V2] {title}: khong co dap tra."
                    : $"[KB_BY_V2] {title}: <- {F75Device.Hex(reply, 24)}");
                return reply;
            }
        }

        /// <summary>
        /// Gui lenh GHI. Tra ve true neu da gui that; false neu chi log.
        /// Nem exception khi chua dinh danh khop registry, hoac ACK echo sai.
        /// </summary>
        public bool SendWrite(string title, byte[] packet, bool verifyEcho = true, int settleMs = 0)
        {
            lock (_gate)
            {
                if (Identification == null || !Identification.Known)
                    throw new InvalidOperationException(
                        "Chua dinh danh khop registry — khong cho ghi. Hay chay 'Nhan dien thiet bi (0x82)' truoc.");
                if (!WritesArmed)
                {
                    string why = !ExperimentalEnabled ? "cong tac Thu nghiem dang TAT" : "Dry-run toan cuc dang BAT";
                    Log($"[KB_BY_V2 - CHI LOG] {title}: se gui goi (khong gui that — {why}).");
                    Log($"    -> {F75Device.Hex(packet, 24)}");
                    return false;
                }
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh kb_by_v2.");
                SendFeature(packet);
                if (verifyEcho)
                {
                    Thread.Sleep(30);
                    var reply = TryGetFeature();
                    if (!KbByV2Protocol.CheckHeaderEcho(packet, reply))
                        throw new InvalidOperationException(
                            $"ACK echo khong khop cho '{title}' — dung lai de an toan.");
                    Log($"[KB_BY_V2] {title}: da gui that, ACK echo OK.");
                }
                else Log($"[KB_BY_V2] {title}: da gui that.");
                if (settleMs > 0) Thread.Sleep(settleMs);
                return true;
            }
        }

        /// <summary>Ghi keymap mot lop: gui set_keys roi cho 100 ms theo tai lieu goc.</summary>
        public bool SendSetKeys(byte layer, byte[] keymap504)
            => SendWrite($"Ghi keymap lop {layer} (0x03)", KbByV2Protocol.BuildSetKeys(layer, keymap504),
                         verifyEcho: true, settleMs: KbByV2Protocol.SetKeysSettleMs);

        /// <summary>
        /// Duong dongle 2.4G: ban v3.4 CHI LOG cac goi send_batch — khong mo
        /// transport that len dongle (dry-run-only, ghi ro trong BUILD-INFO).
        /// </summary>
        public bool LogWirelessBatch(string title, List<byte[]> packets)
        {
            Log($"[KB_BY_V2 2.4G - CHI LOG] {title}: {packets.Count} goi 19 byte (dongle chua mo transport that).");
            foreach (var p in packets) Log($"    -> {F75Device.Hex(p, 19)}");
            return false;
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
