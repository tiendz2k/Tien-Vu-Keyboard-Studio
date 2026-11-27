using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HidSharp;

namespace TienKeyboardStudio3
{
    /// <summary>Ket qua dinh danh mot thiet bi RongYuan bang lenh 0x8F.</summary>
    public sealed class RongYuanIdentification
    {
        public bool Known;                 // true = dev_id khop registry -> moi duoc ghi
        public RongYuanRegistryEntry Entry; // muc registry khop (neu Known)
        public uint? DevId;                // dev_id doc duoc (neu co)
        public int Vid, Pid;
        public string Note;                // giai thich (vd ly do chi-xem)

        public string DevIdHex => DevId.HasValue ? "0x" + DevId.Value.ToString("X8") : "(không đọc được)";
        public string ModelName => Entry != null ? Entry.DisplayName : null;
    }

    // =====================================================================
    // Transport HID cho nen tang RongYuan (Driver Pack 2, v3.5).
    //
    // QUY TAC AN TOAN (hard-coded, giong Driver Pack 1):
    // - Dinh danh (0x8F) la lenh CHI DOC — chay live khi nguoi dung bam nut.
    // - Lenh ghi CHI gui that khi: (1) cong tac Thu nghiem BAT,
    //   (2) Dry-run toan cuc TAT, va (3) thiet bi da dinh danh KHOP
    //   registry (Identify() tra Known voi dev_id thuoc 52 muc MonsGeek
    //   da kiem chung). Thieu mot dieu kien -> chi log goi.
    // - Pacing 10 ms giua cac goi; ACK = dap tra co resp[0] == opcode.
    // - Ho nay CHUA kiem chung tren phan cung that — moi trang cua ho
    //   deu gan nhan nay.
    // =====================================================================
    public sealed class RongYuanDevice : IDisposable
    {
        public bool DryRun { get; set; } = true;
        public bool ExperimentalEnabled { get; set; } = false;
        public Action<string> Log { get; set; } = _ => { };

        public bool WritesArmed => ExperimentalEnabled && !DryRun;
        public RongYuanIdentification Identification { get; private set; }

        private readonly object _gate = new object();
        private HidStream _stream;
        private int _bufferLength = RongYuanProtocol.ReportLength + 1; // +1 cho byte report ID (descriptor 65)
        private bool _disposed;

        private static readonly int[] KnownVids =
            RongYuanRegistryData.Entries.Select(e => e.Vid).Distinct().ToArray();

        private bool EnsureStream(int preferVid = 0, int preferPid = 0)
        {
            if (_stream != null) return true;
            HidDevice target = null;
            try
            {
                var candidates = new List<HidDevice>();
                foreach (var d in DeviceList.Local.GetHidDevices())
                {
                    if (!KnownVids.Contains(d.VendorID)) continue;
                    int maxFeature = 0;
                    try { maxFeature = d.GetMaxFeatureReportLength(); } catch { }
                    // Kenh lenh RongYuan: usage page 0xFFFF, feature report 64 byte (+1 report ID)
                    if (maxFeature >= RongYuanProtocol.ReportLength) candidates.Add(d);
                }
                target = candidates.FirstOrDefault(d => preferVid != 0 && d.VendorID == preferVid && (preferPid == 0 || d.ProductID == preferPid))
                         ?? candidates.FirstOrDefault(d => preferVid != 0 && d.VendorID == preferVid)
                         ?? candidates.FirstOrDefault();
                if (target != null)
                {
                    try { _bufferLength = Math.Max(RongYuanProtocol.ReportLength, target.GetMaxFeatureReportLength()); }
                    catch { _bufferLength = RongYuanProtocol.ReportLength + 1; }
                }
            }
            catch (Exception ex)
            {
                Log("[RONGYUAN] Loi liet ke HID: " + ex.Message);
                return false;
            }
            if (target == null)
            {
                Log("[RONGYUAN] Khong tim thay kenh lenh RongYuan (VID 3151..., feature report 64 byte). Kiem tra cap USB — hieu chuan va ghi chi chay khi cam day.");
                return false;
            }
            if (!target.TryOpen(out _stream))
            {
                Log("[RONGYUAN] Khong mo duoc kenh lenh (thiet bi dang ban?).");
                return false;
            }
            _stream.ReadTimeout = 400;
            _stream.WriteTimeout = 1000;
            Log($"[RONGYUAN] Da mo kenh lenh {target.VendorID:X4}:{target.ProductID:X4}.");
            return true;
        }

        private void SendFeature(byte[] packet64)
        {
            var buf = new byte[_bufferLength];
            // Payload 64 byte nam o cuoi buffer; byte dau la report ID (0) neu descriptor co.
            int offset = _bufferLength - RongYuanProtocol.ReportLength;
            Array.Copy(packet64, 0, buf, offset, RongYuanProtocol.ReportLength);
            _stream.SetFeature(buf, 0, buf.Length);
            Thread.Sleep(RongYuanProtocol.PacingMs);
        }

        private byte[] TryGetFeature()
        {
            var buf = new byte[_bufferLength];
            try
            {
                _stream.GetFeature(buf, 0, buf.Length);
                var data = new byte[RongYuanProtocol.ReportLength];
                Array.Copy(buf, _bufferLength - RongYuanProtocol.ReportLength, data, 0, RongYuanProtocol.ReportLength);
                return data;
            }
            catch { return null; }
        }

        /// <summary>
        /// Dinh danh thiet bi bang lenh 0x8F (CHI DOC — chay live).
        /// dev_id doc o byte 1..4 (LE), thu toi da 3 lan nhu driver goc.
        /// Khong khop registry -> Known=false (chi xem).
        /// </summary>
        public RongYuanIdentification Identify(int vid, int pid)
        {
            lock (_gate)
            {
                var result = new RongYuanIdentification { Vid = vid, Pid = pid, Known = false };
                if (!EnsureStream(vid, pid))
                {
                    result.Note = "Không mở được kênh RongYuan — không đọc được dev_id. Thiết bị ở chế độ chỉ xem.";
                    Identification = result;
                    return result;
                }
                var probe = RongYuanProtocol.BuildGetInfor();
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    SendFeature(probe);
                    var reply = TryGetFeature();
                    var devId = RongYuanProtocol.ParseDevId(reply);
                    if (devId == null) continue;
                    result.DevId = devId;
                    var entry = RongYuanProtocol.MatchDevId(devId.Value);
                    if (entry != null)
                    {
                        result.Known = true;
                        result.Entry = entry;
                        result.Note = "Đã định danh khớp registry (dev_id thuộc tập đã kiểm chứng trong driver chính hãng) — lệnh ghi được phép mở khoá (vẫn cần công tắc Thử nghiệm + tắt Dry-run).";
                        Log($"[RONGYUAN] Dinh danh: {entry.DisplayName} — dev_id {result.DevIdHex} (chip {entry.ChipFamily}, VID {entry.Vid:X4}).");
                    }
                    else
                    {
                        result.Note = $"Đọc được dev_id {result.DevIdHex} nhưng dev_id này KHÔNG có trong registry đã trích (hoặc thuộc mục chưa có dev_id đã kiểm chứng) — chế độ CHỈ XEM: app không cho ghi.";
                        Log($"[RONGYUAN] dev_id {result.DevIdHex} khong khop muc registry nao co dev_id.");
                    }
                    Identification = result;
                    return result;
                }
                result.Note = "Không đọc được dev_id (lệnh 0x8F không trả lời hợp lệ) — thiết bị ở chế độ CHỈ XEM.";
                Identification = result;
                return result;
            }
        }

        /// <summary>Gui lenh DOC — chay live, tra goi dap tra 64 byte dau tien (hoac null).</summary>
        public byte[] SendRead(string title, byte[] packet)
        {
            lock (_gate)
            {
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh RongYuan.");
                SendFeature(packet);
                var reply = TryGetFeature();
                Log(reply == null
                    ? $"[RONGYUAN] {title}: khong co dap tra."
                    : $"[RONGYUAN] {title}: <- {F75Device.Hex(reply, 24)}");
                return reply;
            }
        }

        /// <summary>
        /// Doc nhieu manh lien tiep (moi manh mot yeu cau rieng nhu driver
        /// goc): tra du lieu huu ich sau header 8 byte cua moi dap tra.
        /// </summary>
        public byte[] SendReadChunks(string title, Func<int, byte[]> requestForChunk, int chunkCount)
        {
            lock (_gate)
            {
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh RongYuan.");
                var data = new List<byte>();
                for (int i = 0; i < chunkCount; i++)
                {
                    var req = requestForChunk(i);
                    SendFeature(req);
                    var reply = TryGetFeature();
                    if (reply == null)
                    {
                        Log($"[RONGYUAN] {title}: manh {i} khong co dap tra — dung doc.");
                        break;
                    }
                    for (int j = RongYuanProtocol.HeaderLength; j < reply.Length; j++) data.Add(reply[j]);
                }
                Log($"[RONGYUAN] {title}: doc duoc {data.Count} byte du lieu ({chunkCount} manh yeu cau).");
                return data.ToArray();
            }
        }

        private void GateWriteOrThrow()
        {
            if (Identification == null || !Identification.Known)
                throw new InvalidOperationException(
                    "Chua dinh danh khop registry (dev_id) — khong cho ghi. Hay chay 'Nhan dien thiet bi (0x8F)' truoc; cac model chua co dev_id da kiem chung luon o che do chi xem.");
        }

        /// <summary>
        /// Gui mot lenh GHI. Tra ve true neu da gui that; false neu chi log.
        /// Nem exception khi chua dinh danh khop registry.
        /// </summary>
        public bool SendWrite(string title, byte[] packet, bool verifyAck = true)
        {
            lock (_gate)
            {
                GateWriteOrThrow();
                if (!WritesArmed)
                {
                    string why = !ExperimentalEnabled ? "cong tac Thu nghiem dang TAT" : "Dry-run toan cuc dang BAT";
                    Log($"[RONGYUAN - CHI LOG] {title}: se gui goi (khong gui that — {why}).");
                    Log($"    -> {F75Device.Hex(packet, 24)}");
                    return false;
                }
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh RongYuan.");
                SendFeature(packet);
                if (verifyAck)
                {
                    var reply = TryGetFeature();
                    if (reply != null && !RongYuanProtocol.CheckAck(packet, reply))
                        throw new InvalidOperationException(
                            $"ACK khong khop cho '{title}' (resp[0]=0x{reply[0]:X2}, mong doi 0x{packet[0]:X2}) — dung lai de an toan.");
                    Log(reply == null
                        ? $"[RONGYUAN] {title}: da gui that (khong co dap tra ACK)."
                        : $"[RONGYUAN] {title}: da gui that, ACK OK.");
                }
                else Log($"[RONGYUAN] {title}: da gui that.");
                return true;
            }
        }

        /// <summary>Gui chuoi goi GHI (keymap/userpic/HE bulk/man hinh). Cung khoa an toan nhu SendWrite.</summary>
        public bool SendChunkedWrite(string title, List<byte[]> packets, int pacingMs = RongYuanProtocol.PacingMs)
        {
            lock (_gate)
            {
                GateWriteOrThrow();
                if (packets == null || packets.Count == 0) return false;
                if (!WritesArmed)
                {
                    string why = !ExperimentalEnabled ? "cong tac Thu nghiem dang TAT" : "Dry-run toan cuc dang BAT";
                    Log($"[RONGYUAN - CHI LOG] {title}: {packets.Count} goi se gui (khong gui that — {why}). Goi dau:");
                    Log($"    -> {F75Device.Hex(packets[0], 24)}");
                    return false;
                }
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh RongYuan.");
                for (int i = 0; i < packets.Count; i++)
                {
                    SendFeature(packets[i]);
                    if (pacingMs != RongYuanProtocol.PacingMs && pacingMs > 0) Thread.Sleep(pacingMs);
                    var reply = TryGetFeature();
                    if (reply != null && !RongYuanProtocol.CheckAck(packets[i], reply))
                        throw new InvalidOperationException(
                            $"ACK khong khop cho '{title}' o goi {i} — dung lai de an toan.");
                }
                Log($"[RONGYUAN] {title}: da gui that {packets.Count} goi.");
                return true;
            }
        }

        /// <summary>
        /// Nap mot khung hinh len man hinh TFT: (tuy chon) xoa flash ->
        /// goi chuan bi 165/169 (poll ACK resp[1]==1, toi da 10 lan x
        /// 100 ms) -> du lieu 37/41 theo manh 56 byte, nhip ~3 ms.
        /// Cung khoa an toan 3 lop; chua mo khoa thi chi log.
        /// </summary>
        public bool UploadScreenFrame(string title, bool rgb888, byte[] imageData, int width, int height, bool eraseFirst)
        {
            lock (_gate)
            {
                GateWriteOrThrow();
                var prepare = RongYuanScreen.BuildPrepare(rgb888, 0, 1, 0, 0, 0, width, height, imageData.Length);
                var chunks = RongYuanScreen.BuildDataChunks(rgb888, 0, 1, 0, imageData);
                if (!WritesArmed)
                {
                    string why = !ExperimentalEnabled ? "cong tac Thu nghiem dang TAT" : "Dry-run toan cuc dang BAT";
                    Log($"[RONGYUAN - CHI LOG] {title}: chuan bi {imageData.Length} byte anh {width}x{height} ({(rgb888 ? "RGB888" : "RGB565")}) — {chunks.Count} goi du lieu (khong gui that — {why}). Goi chuan bi:");
                    Log($"    -> {F75Device.Hex(prepare, 24)}");
                    return false;
                }
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh RongYuan.");
                if (eraseFirst)
                {
                    var erase = RongYuanProtocol.BuildFlashErase();
                    SendFeature(erase);
                    var eraseReply = TryGetFeature();
                    Log(RongYuanProtocol.CheckFlashEraseAck(eraseReply)
                        ? "[RONGYUAN] Xoa flash: ACK magic AA AA 55 55 OK."
                        : "[RONGYUAN] Xoa flash: khong thay ACK magic — van tiep tuc can trong.");
                }
                bool ready = false;
                for (int attempt = 0; attempt < 10 && !ready; attempt++)
                {
                    SendFeature(prepare);
                    var reply = TryGetFeature();
                    ready = RongYuanScreen.CheckPrepareAck(reply);
                    if (!ready) Thread.Sleep(100);
                }
                if (!ready)
                    throw new InvalidOperationException("Man hinh khong san sang (goi chuan bi khong duoc ACK resp[1]==1 sau 10 lan) — dung lai.");
                for (int i = 0; i < chunks.Count; i++)
                {
                    SendFeature(chunks[i]);
                    Thread.Sleep(RongYuanProtocol.ScreenChunkPacingMs);
                }
                Log($"[RONGYUAN] {title}: da nap that {chunks.Count} goi du lieu man hinh.");
                return true;
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
