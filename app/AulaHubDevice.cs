using System;
using System.Collections.Generic;
using System.Threading;
using HidSharp;
using HidSharp.Reports;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Driver Pack 5 (v3.9) — lop thiet bi AULA Hub (HFD / SparkLink V1 /
    // SparkLink V2). Dinh danh = VID:PID + usage page khop registry
    // (mot cap VID:PID co the thuoc nhieu ho — usage page quyet dinh);
    // khong khop -> CHI XEM, cam ghi.
    //  - HFD: output report id 0, kich thuoc goi doc tu descriptor;
    //    ACK = dap tra [0x55, cmd khop], timeout 500 ms, toi da 3 lan.
    //    Giao dien phu 0xFF67 cho khoi LED/TFT lon. Dongle 2.4G dung bo
    //    lenh RIENG (khong forward giao thuc co day).
    //  - SparkLink V1/V2: hang doi ACK kieu FIFO (khong doi chieu
    //    opcode), timeout 1000 ms, toi da 3 lan, tuan tu nghiem ngat.
    // Moi lenh ghi qua cong ba lop: da dinh danh + thu nghiem BAT +
    // dry-run TAT; chua vuot cong thi chi ghi log goi. Doc/probe chay
    // truc tiep o moi che do.
    // =====================================================================
    public sealed class AulaHubDevice : IDisposable
    {
        public Action<string> Log;
        public bool DryRun = true;
        public bool ExperimentalEnabled;
        public AulaHubEntry IdentifiedEntry;   // null = chua dinh danh
        public string ProductName = "";
        public int ReportLength = 64;          // doc tu descriptor khi mo

        private HidStream _stream;
        private HidStream _subStream;          // giao dien phu 0xFF67 (HFD)
        private readonly object _ioLock = new object();

        public bool IsOpen => _stream != null;
        public bool Identified => IdentifiedEntry != null;
        public string Family => IdentifiedEntry?.Family;
        public bool IsHfd => IdentifiedEntry != null && IdentifiedEntry.Family == "HFD";
        public bool IsSlV1 => IdentifiedEntry != null && IdentifiedEntry.Family == "SparkLinkV1";
        public bool IsSlV2 => IdentifiedEntry != null && IdentifiedEntry.Family == "SparkLinkV2";
        public bool IsDongle => IdentifiedEntry != null && IdentifiedEntry.Connection == "2.4G dongle";

        public void Cleanup() { Close(); }

        public void Close()
        {
            lock (_ioLock)
            {
                try { _stream?.Dispose(); } catch { }
                try { _subStream?.Dispose(); } catch { }
                _stream = null; _subStream = null;
            }
        }

        private void GateWriteOrThrow(string what)
        {
            if (!Identified)
                throw new InvalidOperationException(what + ": thiet bi AULA Hub chua dinh danh duoc bang registry (VID:PID + usage page khong khop) — che do chi xem, cam ghi.");
            if (!ExperimentalEnabled)
                throw new InvalidOperationException(what + ": hay bat 'Che do thu nghiem' o trang Per-key truoc.");
            if (DryRun)
                throw new InvalidOperationException(what + ": Dry-run dang BAT — chi ghi log, khong ghi that.");
        }

        private bool Armed => Identified && ExperimentalEnabled && !DryRun;

        // Mo thiet bi theo VID:PID + usage page cua mot muc registry.
        public bool Open(AulaHubEntry entry)
        {
            Close();
            if (entry == null) return false;
            foreach (var dev in DeviceList.Local.GetHidDevices(entry.Vid, entry.Pid))
            {
                try
                {
                    var details = dev.GetReportDescriptor();
                    if (!details.TryGetReport(ReportType.Output, 0, out var rep)) continue;
                    // usage page cua collection chua report nay
                    if (!MatchesUsagePage(dev, entry)) continue;
                    if (!dev.TryOpen(out var s)) continue;
                    _stream = s;
                    _stream.ReadTimeout = 300;
                    _stream.WriteTimeout = 2000;
                    try { ProductName = dev.GetProductName() ?? ""; } catch { ProductName = ""; }
                    try { ReportLength = Math.Max(9, dev.GetMaxOutputReportLength() - 1); }
                    catch { ReportLength = entry.Family == "HFD" ? HfdProtocol.MainReportLen : 64; }
                    IdentifiedEntry = entry;
                    L($"[AULA Hub] Da mo {ProductName} ({entry.Vid:X4}:{entry.Pid:X4}) — registry: {entry.DisplayName}, ho {entry.Family}, usage page 0x{entry.UsagePage:X4}, report {ReportLength} byte.");
                    return true;
                }
                catch { }
            }
            L($"[AULA Hub] Khong mo duoc {entry.DisplayName} ({entry.Vid:X4}:{entry.Pid:X4}) tren usage page 0x{entry.UsagePage:X4}.");
            return false;
        }

        // Kiem tra usage page qua report descriptor: tim collection chua
        // output report id 0 va doi chieu usage page da biet cua ho.
        private static bool MatchesUsagePage(HidDevice dev, AulaHubEntry entry)
        {
            // HidSharp khong lo usage page cua tung collection qua API don
            // gian; dung do dai report lam bo loc phu va ghi nhan gioi han
            // nay trong log dinh danh (xem BUILD-INFO).
            try
            {
                int len = dev.GetMaxOutputReportLength();
                if (entry.Family == "HFD") return len >= 9; // 32B chinh hoac lon hon o nhanh man hinh
                return len >= 33; // SparkLink: 64B + report id
            }
            catch { return false; }
        }

        // Mo giao dien phu 0xFF67 cho HFD (khoi LED/TFT lon).
        private bool OpenSubInterface()
        {
            if (_subStream != null) return true;
            if (IdentifiedEntry == null) return false;
            foreach (var dev in DeviceList.Local.GetHidDevices(IdentifiedEntry.Vid, IdentifiedEntry.Pid))
            {
                try
                {
                    int len = 0;
                    try { len = dev.GetMaxOutputReportLength(); } catch { }
                    if (len < 512) continue; // nhanh man hinh: report rat lon (~4104)
                    if (!dev.TryOpen(out var s)) continue;
                    _subStream = s;
                    _subStream.ReadTimeout = 300;
                    _subStream.WriteTimeout = 5000;
                    L($"[AULA Hub] Da mo giao dien phu 0xFF67 (report {len} byte) cho khoi LED/TFT lon.");
                    return true;
                }
                catch { }
            }
            return false;
        }

        // ------------------------- Transport HFD -------------------------
        private byte[] HfdExchange(byte[] packet, byte expectedRespCmd, string what, bool useSub = false)
        {
            lock (_ioLock)
            {
                var stream = useSub ? _subStream : _stream;
                if (stream == null) throw new InvalidOperationException("Chua mo thiet bi AULA Hub" + (useSub ? " (thieu giao dien phu 0xFF67)." : "."));
                Exception last = null;
                for (int attempt = 0; attempt < HfdProtocol.MaxRetries; attempt++)
                {
                    var send = new byte[packet.Length + 1];
                    Array.Copy(packet, 0, send, 1, packet.Length);
                    stream.Write(send);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < HfdProtocol.TimeoutMs)
                    {
                        try
                        {
                            var raw = stream.Read();
                            int off = raw.Length > packet.Length && raw[0] == 0 ? 1 : 0;
                            var body = new byte[Math.Max(0, raw.Length - off)];
                            if (body.Length > 0) Array.Copy(raw, off, body, 0, body.Length);
                            if (HfdProtocol.IsResponseFor(body, expectedRespCmd)) return body;
                        }
                        catch { Thread.Sleep(5); }
                    }
                    last = new TimeoutException(what + $": het {HfdProtocol.TimeoutMs} ms cho dap tra cmd {expectedRespCmd}.");
                }
                throw last ?? new TimeoutException(what + ": khong nhan dap tra HFD.");
            }
        }

        private void HfdWrite(List<byte[]> packets, byte respCmd, string what, bool useSub = false)
        {
            if (Armed)
            {
                foreach (var p in packets) HfdExchange(p, respCmd, what, useSub);
                Thread.Sleep(HfdProtocol.LedSettleMs);
                L("[AULA Hub] " + what + $" — da gui {packets.Count} goi.");
            }
            else
                foreach (var p in packets) L("[AULA Hub] " + what + " (log): " + HexLog(p));
        }

        // ------------------- Transport SparkLink (FIFO ACK) -------------------
        private byte[] SlExchange(byte[] frame, string what)
        {
            lock (_ioLock)
            {
                if (_stream == null) throw new InvalidOperationException("Chua mo thiet bi AULA Hub.");
                Exception last = null;
                for (int attempt = 0; attempt < SparkLinkV1Protocol.MaxRetries; attempt++)
                {
                    var send = new byte[frame.Length + 1];
                    Array.Copy(frame, 0, send, 1, frame.Length);
                    _stream.Write(send);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < SparkLinkV1Protocol.TimeoutMs)
                    {
                        try
                        {
                            var raw = _stream.Read();
                            int off = raw.Length > frame.Length && raw[0] == 0 ? 1 : 0;
                            var body = new byte[Math.Max(0, raw.Length - off)];
                            if (body.Length > 0) Array.Copy(raw, off, body, 0, body.Length);
                            if (body.Length >= 2 && !(IsSlV1 && body.Length >= 3 && body[2] == SparkLinkV1Protocol.CmdFail))
                                return body; // FIFO: dap tra dau tien la ACK cua lenh vua gui
                        }
                        catch { Thread.Sleep(5); }
                    }
                    last = new TimeoutException(what + ": het 1000 ms cho ACK SparkLink (FIFO).");
                }
                throw last ?? new TimeoutException(what + ": khong nhan ACK SparkLink.");
            }
        }

        private void SlWrite(byte[] frame, string what, bool fireAndForget = false)
        {
            if (Armed)
            {
                if (fireAndForget)
                {
                    lock (_ioLock)
                    {
                        var send = new byte[frame.Length + 1];
                        Array.Copy(frame, 0, send, 1, frame.Length);
                        _stream.Write(send);
                    }
                }
                else SlExchange(frame, what);
                L("[AULA Hub] " + what + " — da gui.");
            }
            else
                L("[AULA Hub] " + what + " (log): " + HexLog(frame));
        }

        // ------------------- Probe doc (chay moi che do) -------------------
        public string ProbeRead()
        {
            if (_stream == null) throw new InvalidOperationException("Chua mo thiet bi AULA Hub.");
            if (!Identified) return "Chua dinh danh — mo bang mot muc registry truoc khi probe.";
            if (IsHfd && !IsDongle)
            {
                var reply = HfdExchange(
                    HfdProtocol.BuildRequest(HfdProtocol.CmdGetDeviceInfo, 0, null, ReportLength),
                    HfdProtocol.CmdGetDeviceInfo, "HFD doc thong tin thiet bi");
                var info = HfdProtocol.ParseDeviceInfo(HfdProtocol.ResponsePayload(reply));
                if (info == null) return "HFD doc OK nhung khoi thong tin ngan hon 56 byte.";
                return $"HFD doc OK: VID:PID trong khoi = {info.Vid:X4}:{info.Pid:X4}, so ban ghi LED {info.LedRecordCount}, " +
                       $"pin {info.Battery}, TFT toi da {info.TftMaxFrames} khung, do chinh xac RT {info.RtPrecision}, " +
                       $"nhac {(info.MusicFlag1 || info.MusicFlag2 ? "co" : "khong")}.";
            }
            if (IsHfd && IsDongle)
            {
                var reply = HfdExchange(HfdProtocol.BuildDonglePacket(HfdProtocol.CmdDongleInfo, null),
                    HfdProtocol.CmdDongleInfo, "HFD dongle doc thong tin");
                var bat = HfdProtocol.DongleBatteryOf(reply);
                return bat.HasValue ? $"Dongle HFD doc OK: pin thiet bi sau dongle = {bat.Value}." : "Dongle HFD doc OK (khong co byte pin).";
            }
            if (IsSlV1)
            {
                var reply = SlExchange(SparkLinkV1Protocol.BuildFrame(SparkLinkV1Protocol.CmdSync,
                    SparkLinkV1Protocol.WithRwFlag(false)), "SparkLink V1 dong bo dinh danh");
                var payload = SparkLinkV1Protocol.FramePayload(reply);
                return payload.Length >= 4
                    ? $"SparkLink V1 doc OK: BoardID = 0x{(payload[3] << 24) | (payload[2] << 16) | (payload[1] << 8) | payload[0]:X8}."
                    : "SparkLink V1 doc OK nhung payload SYNC ngan.";
            }
            if (IsSlV2)
            {
                var reply = SlExchange(SparkLinkV2Protocol.BuildFrame(SparkLinkV2Protocol.CatDevice, 1, null),
                    "SparkLink V2 doc phien ban giao thuc");
                return $"SparkLink V2 doc OK: dap tra [{HexLog(Slice(reply, 0, Math.Min(16, reply.Length)))}...].";
            }
            return "Ho chua ho tro probe.";
        }

        private static byte[] Slice(byte[] b, int off, int len)
        {
            var r = new byte[Math.Max(0, Math.Min(len, b.Length - off))];
            if (r.Length > 0) Array.Copy(b, off, r, 0, r.Length);
            return r;
        }

        // ------------------------- HFD: ghi -------------------------
        public void HfdSetLedEffect(byte mode, byte brightness, byte speed, (byte R, byte G, byte B) color)
        {
            if (!IsHfd) throw new InvalidOperationException("HFD LED: thiet bi hien tai khong phai ho HFD.");
            var blk = HfdProtocol.BuildLedEffectBlock(mode, brightness, speed, color.R, color.G, color.B);
            HfdWrite(HfdProtocol.BuildBlockRequests(HfdProtocol.CmdWriteLedEffect, blk, ReportLength),
                HfdProtocol.CmdWriteLedEffect, "HFD dat hieu ung LED");
        }

        public void HfdSetPerKey(IReadOnlyList<(byte R, byte G, byte B)> colors128)
        {
            if (!IsHfd) throw new InvalidOperationException("HFD per-key: thiet bi hien tai khong phai ho HFD.");
            var blk = HfdProtocol.BuildPerKeyBlock(colors128);
            HfdWrite(HfdProtocol.BuildBlockRequests(HfdProtocol.CmdWritePerKey, blk, ReportLength),
                HfdProtocol.CmdWritePerKey, "HFD ghi mau tung phim (512 byte)");
        }

        public void HfdSetRtRecord(int slot, bool rtSwitch, double travelMm, double pressMm, double releaseMm)
        {
            if (!IsHfd) throw new InvalidOperationException("HFD RT: thiet bi hien tai khong phai ho HFD.");
            if (slot < 0 || slot >= 128) throw new ArgumentOutOfRangeException(nameof(slot));
            var rec = HfdProtocol.BuildRtRecord(rtSwitch, travelMm, pressMm, releaseMm);
            var packets = new List<byte[]>
            {
                HfdProtocol.BuildRequest(HfdProtocol.CmdWriteRt, (ushort)(slot * 8), rec, ReportLength),
            };
            HfdWrite(packets, HfdProtocol.CmdWriteRt, $"HFD ghi ban ghi RT o {slot}");
        }

        /// <summary>Upload animation TFT NOVA98 (240x135 RGB565 LE) qua
        /// giao dien phu 0xFF67: bang tre 256 byte + cac khung; header
        /// rieng cua lenh 80 va dap tra cho o responseCmd 65.</summary>
        public void HfdUploadNova98Tft(IReadOnlyList<int> frameDelaysMs, byte[] rgb565Frames)
        {
            if (!IsHfd) throw new InvalidOperationException("HFD TFT: thiet bi hien tai khong phai ho HFD.");
            GateWriteOrThrow("Upload TFT NOVA98");
            if (!OpenSubInterface())
                throw new InvalidOperationException("Khong mo duoc giao dien phu 0xFF67 — upload TFT can giao dien nay.");
            var delay = HfdProtocol.BuildTftDelayTable(frameDelaysMs);
            HfdWrite(new List<byte[]> { HfdProtocol.BuildRequest(HfdProtocol.CmdTftAnimUpload, 0, delay, 4104) },
                HfdProtocol.Nova98TftExpectedResponseCmd, "HFD TFT: bang tre 256 byte", useSub: true);
            int chunk = 4096;
            int total = Math.Max(1, ((rgb565Frames?.Length ?? 0) + chunk - 1) / chunk);
            for (int i = 0; i < total; i++)
            {
                int len = Math.Min(chunk, (rgb565Frames?.Length ?? 0) - i * chunk);
                var part = new byte[len];
                if (len > 0) Array.Copy(rgb565Frames, i * chunk, part, 0, len);
                var hdr = HfdProtocol.BuildNova98TftHeader((ushort)i, (ushort)total);
                var payload = new List<byte>(hdr);
                payload.AddRange(part);
                HfdWrite(new List<byte[]> { HfdProtocol.BuildRequest(HfdProtocol.CmdTftAnimUpload, 0, payload.ToArray(), 4104) },
                    HfdProtocol.Nova98TftExpectedResponseCmd, $"HFD TFT: khung {i + 1}/{total}", useSub: true);
            }
        }

        public void HfdDongleSetLight(byte[] rgb565Payload)
        {
            if (!(IsHfd && IsDongle)) throw new InvalidOperationException("Den qua dongle HFD chi danh cho ket noi dongle.");
            HfdWrite(new List<byte[]> { HfdProtocol.BuildDonglePacket(HfdProtocol.CmdDongleLight, rgb565Payload) },
                HfdProtocol.CmdDongleLight, "HFD dongle: dat den (RGB565)");
        }

        // ---------------------- SparkLink V1: ghi ----------------------
        public void SlV1SetPrgb(IReadOnlyList<(byte R, byte G, byte B)> colors7, int brightnessUi, byte mode, int speedUi)
        {
            if (!IsSlV1) throw new InvalidOperationException("SparkLink V1 PRGB: thiet bi hien tai khong phai SparkLink V1.");
            SlWrite(SparkLinkV1Protocol.BuildPrgb(colors7, 1, brightnessUi, mode, speedUi, 0, 0),
                "SparkLink V1 dat khoi den PRGB");
        }

        public void SlV1SetPerKey(IReadOnlyList<(byte Key, byte R, byte G, byte B)> colors)
        {
            if (!IsSlV1) throw new InvalidOperationException("SparkLink V1 per-key: thiet bi hien tai khong phai SparkLink V1.");
            if (colors != null)
                foreach (var c in colors)
                    SlWrite(SparkLinkV1Protocol.BuildKrgb(c.Key, c.R, c.G, c.B),
                        $"SparkLink V1 per-key phim {c.Key}");
            SlWrite(SparkLinkV1Protocol.BuildKrgbSave(), "SparkLink V1 per-key: goi LUU (key 254)");
        }

        public void SlV1SetGlobalTravel(double travelMm, double deadPressMm, double deadReleaseMm)
        {
            if (!IsSlV1) throw new InvalidOperationException("SparkLink V1 DB: thiet bi hien tai khong phai SparkLink V1.");
            SlWrite(SparkLinkV1Protocol.BuildDb(travelMm, deadPressMm, deadReleaseMm),
                $"SparkLink V1 DB: hanh trinh {travelMm} mm, dead-zone nhan {deadPressMm} / nha {deadReleaseMm} mm");
        }

        public void SlV1SaveParams()
        {
            if (!IsSlV1) throw new InvalidOperationException("SparkLink V1 luu tham so: thiet bi hien tai khong phai SparkLink V1.");
            SlWrite(SparkLinkV1Protocol.BuildOrder(SparkLinkV1Protocol.OrderSaveParams),
                "SparkLink V1 luu tham so (ORDER 2)");
        }

        // ---------------------- SparkLink V2: ghi ----------------------
        public void SlV2SetLightingBase(byte area, byte open, byte mode, int luminanceUi, int speedUi)
        {
            if (!IsSlV2) throw new InvalidOperationException("SparkLink V2 lighting: thiet bi hien tai khong phai SparkLink V2.");
            SlWrite(SparkLinkV2Protocol.BuildLightingBase(true, area, open, mode, luminanceUi, speedUi, 0, 0),
                "SparkLink V2 dat den nen");
        }

        public void SlV2SetPerKeyChunk(byte area, byte chunkIndex,
            IReadOnlyList<(byte R, byte G, byte B, bool Customised)> cells15)
        {
            if (!IsSlV2) throw new InvalidOperationException("SparkLink V2 per-key: thiet bi hien tai khong phai SparkLink V2.");
            SlWrite(SparkLinkV2Protocol.BuildLightingCustomChunk(area, chunkIndex, cells15),
                $"SparkLink V2 per-key ben vung chunk {chunkIndex}");
        }

        /// <summary>Stream DirectDrive (byte[1]=5) — fire-and-forget,
        /// khong cho ACK; van qua cong ba lop nhu moi lenh ghi.</summary>
        public void SlV2StreamChunk(byte area, byte chunkIndex,
            IReadOnlyList<(byte R, byte G, byte B, bool Customised)> cells15)
        {
            if (!IsSlV2) throw new InvalidOperationException("SparkLink V2 stream: thiet bi hien tai khong phai SparkLink V2.");
            SlWrite(SparkLinkV2Protocol.BuildDirectDriveChunk(area, chunkIndex, cells15),
                $"SparkLink V2 DirectDrive chunk {chunkIndex}", fireAndForget: true);
        }

        public void SlV2SetTrigger(byte row, byte col, byte mode, double pressMm, double releaseMm,
            double rtPressMm, double rtReleaseMm)
        {
            if (!IsSlV2) throw new InvalidOperationException("SparkLink V2 trigger: thiet bi hien tai khong phai SparkLink V2.");
            SlWrite(SparkLinkV2Protocol.BuildTriggerFrame(row, col, mode,
                    pressMm, releaseMm, pressMm, rtPressMm, rtReleaseMm, 0, 0, 0, 0, 0, 0, 0),
                $"SparkLink V2 trigger o ({row},{col})");
        }

        public void SlV2SaveParams()
        {
            if (!IsSlV2) throw new InvalidOperationException("SparkLink V2 luu tham so: thiet bi hien tai khong phai SparkLink V2.");
            SlWrite(SparkLinkV2Protocol.BuildGlobalSave(), "SparkLink V2 luu tham so (Global 2)");
        }

        private static string HexLog(byte[] b) => b == null ? "" : F75Device.Hex(b, b.Length);

        private void L(string s) => Log?.Invoke(s);
        public void Dispose() => Close();
    }
}
