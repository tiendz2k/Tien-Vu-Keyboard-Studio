using System;
using System.Collections.Generic;
using System.Threading;
using HidSharp;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Driver Pack 5 (v3.9) — lop thiet bi DareU. Dinh danh = VID:PID ->
    // muc registry (co che cua chinh hang: PID chon FwType/ho giao thuc).
    // PID la lạ (khong co trong 187 muc registry) -> CHI XEM, cam ghi.
    //  - TG / TG-RT / HS / TG-Dongle (FwType 0/1/6/7): feature report 64B,
    //    ACK = poll receiveFeatureReport 10 ms x 20, nibble trang thai == 2.
    //  - OHID khong day (FwType 4): feature report; OHID co day (5):
    //    output report + input queue (poll 20 ms x 20, co tre theo lenh).
    //  - JM / QF (FwType 2/3): output report 16B (ReportId 8), dap tra
    //    khop theo byte lenh dau, poll 20 ms x 5.
    // Moi lenh ghi deu qua cong ba lop: da dinh danh + cong tac thu
    // nghiem BAT + dry-run TAT; chua vuot cong thi chi ghi log goi.
    // Cac lenh doc/probe chay truc tiep o moi che do.
    // =====================================================================
    public sealed class DareUDevice : IDisposable
    {
        public Action<string> Log;
        public bool DryRun = true;
        public bool ExperimentalEnabled;
        public DareUEntry IdentifiedEntry;   // null = chua dinh danh / PID la
        public string ProductName = "";

        private HidStream _stream;
        private readonly object _ioLock = new object();

        public bool IsOpen => _stream != null;
        public bool Identified => IdentifiedEntry != null;
        public bool IsHe => IdentifiedEntry != null && IdentifiedEntry.IsHe;
        public string Family => IdentifiedEntry?.Family;

        private bool IsTg => IdentifiedEntry != null &&
            (IdentifiedEntry.FwType == 0 || IdentifiedEntry.FwType == 1 ||
             IdentifiedEntry.FwType == 6 || IdentifiedEntry.FwType == 7);
        private bool IsOhid => IdentifiedEntry != null &&
            (IdentifiedEntry.FwType == 4 || IdentifiedEntry.FwType == 5);
        private bool IsJm => IdentifiedEntry != null &&
            (IdentifiedEntry.FwType == 2 || IdentifiedEntry.FwType == 3);

        public void Cleanup() { Close(); }

        public void Close()
        {
            lock (_ioLock)
            {
                try { _stream?.Dispose(); } catch { }
                _stream = null;
            }
        }

        private void GateWriteOrThrow(string what)
        {
            if (!Identified)
                throw new InvalidOperationException(what + ": thiet bi DareU chua dinh danh duoc bang registry (PID la) — che do chi xem, cam ghi.");
            if (!ExperimentalEnabled)
                throw new InvalidOperationException(what + ": hay bat 'Che do thu nghiem' o trang Per-key truoc.");
            if (DryRun)
                throw new InvalidOperationException(what + ": Dry-run dang BAT — chi ghi log, khong ghi that.");
        }

        private byte ReportIdForCurrent()
        {
            if (IdentifiedEntry == null) return 0;
            if (IsJm) return JmProtocol.ReportId;
            if (IsTg && IdentifiedEntry.FwType == 7) return 7; // dongle TG-OHID
            return 0;
        }

        // Mo thiet bi DareU theo VID:PID (0x260D:*) — uu tien interface co
        // do dai report khop ho (64B TG/OHID, 16B JM).
        public bool OpenByVidPid(int vid, int pid)
        {
            Close();
            var entry = DareURegistryData.Match(vid, pid);
            int wantLen = entry != null && (entry.FwType == 2 || entry.FwType == 3) ? 16 : 64;
            foreach (var dev in DeviceList.Local.GetHidDevices(vid, pid))
            {
                try
                {
                    int outLen = 0, inLen = 0;
                    try { outLen = dev.GetMaxOutputReportLength(); inLen = dev.GetMaxInputReportLength(); } catch { }
                    if (outLen != wantLen && inLen != wantLen) continue;
                    if (!dev.TryOpen(out var s)) continue;
                    _stream = s;
                    _stream.ReadTimeout = 300;
                    _stream.WriteTimeout = 1000;
                    try { ProductName = dev.GetProductName() ?? ""; } catch { ProductName = ""; }
                    IdentifiedEntry = entry;
                    L($"[DareU] Da mo {ProductName} ({vid:X4}:{pid:X4})" +
                      (entry != null
                          ? $" — registry: {entry.DisplayName}, ho {entry.Family} (FwType {entry.FwType}), model noi bo {entry.InternalModel}."
                          : " — PID KHONG co trong registry 187 muc: CHE DO CHI XEM, cam ghi."));
                    return true;
                }
                catch { }
            }
            L($"[DareU] Khong mo duoc thiet bi {vid:X4}:{pid:X4}.");
            return false;
        }

        // ------------------------- Transport TG -------------------------
        private byte[] TgExchange(byte[] packet, string what)
        {
            lock (_ioLock)
            {
                if (_stream == null) throw new InvalidOperationException("Chua mo thiet bi DareU.");
                byte rid = ReportIdForCurrent();
                var send = new byte[packet.Length + 1];
                send[0] = rid;
                Array.Copy(packet, 0, send, 1, packet.Length);
                _stream.SetFeature(send);
                if (IdentifiedEntry != null && IdentifiedEntry.FwType == 7)
                    Thread.Sleep(TgProtocol.DongleExtraWaitMs);
                for (int i = 0; i < TgProtocol.AckPollMax; i++)
                {
                    Thread.Sleep(TgProtocol.AckPollIntervalMs);
                    var recv = new byte[TgProtocol.PacketLen + 1];
                    recv[0] = rid;
                    try { _stream.GetFeature(recv); }
                    catch { continue; }
                    var body = new byte[TgProtocol.PacketLen];
                    Array.Copy(recv, 1, body, 0, TgProtocol.PacketLen);
                    if (TgProtocol.IsAck(body)) return body;
                }
                throw new TimeoutException(what + ": khong nhan ACK TG (nibble trang thai == 2) sau " +
                    (TgProtocol.AckPollMax * TgProtocol.AckPollIntervalMs) + " ms.");
            }
        }

        // ------------------------- Transport OHID -------------------------
        // Tre rieng theo lenh cua hang (FwType 4 khong day): MACRO_DATA
        // 250 ms, ORDER/KEY_DATA_REPORT/KEY/MACRO 200 ms, RGB_MATRIX 100 ms,
        // con lai 25 ms.
        private static int OhidDelayMs(byte cmd)
        {
            if (cmd == OhidProtocol.CmdMacroData) return 250;
            if (cmd == OhidProtocol.CmdOrder || cmd == OhidProtocol.CmdKeyDataReport ||
                cmd == OhidProtocol.CmdKey || cmd == OhidProtocol.CmdMacro) return 200;
            if (cmd == OhidProtocol.CmdRgbMatrix) return 100;
            return 25;
        }

        private byte[] OhidExchange(byte[] packet, string what)
        {
            lock (_ioLock)
            {
                if (_stream == null) throw new InvalidOperationException("Chua mo thiet bi DareU.");
                bool wireless = IdentifiedEntry != null && IdentifiedEntry.FwType == 4;
                if (wireless)
                {
                    var send = new byte[packet.Length + 1];
                    Array.Copy(packet, 0, send, 1, packet.Length);
                    _stream.SetFeature(send);
                }
                else
                {
                    var send = new byte[packet.Length + 1];
                    Array.Copy(packet, 0, send, 1, packet.Length);
                    _stream.Write(send);
                }
                Thread.Sleep(OhidDelayMs((byte)(packet[2] & 0x7F)));
                for (int i = 0; i < OhidProtocol.ResponsePollMax; i++)
                {
                    try
                    {
                        byte[] raw;
                        if (wireless)
                        {
                            raw = new byte[OhidProtocol.PacketLen + 1];
                            _stream.GetFeature(raw);
                            var body = new byte[OhidProtocol.PacketLen];
                            Array.Copy(raw, 1, body, 0, OhidProtocol.PacketLen);
                            raw = body;
                        }
                        else
                        {
                            raw = _stream.Read();
                            if (raw.Length == OhidProtocol.PacketLen + 1 && raw[0] == 0)
                            {
                                var body = new byte[OhidProtocol.PacketLen];
                                Array.Copy(raw, 1, body, 0, OhidProtocol.PacketLen);
                                raw = body;
                            }
                        }
                        if (raw.Length >= 4 && raw[0] == OhidProtocol.Head) return raw;
                    }
                    catch { }
                    Thread.Sleep(OhidProtocol.ResponsePollIntervalMs);
                }
                throw new TimeoutException(what + ": khong nhan dap tra OHID sau " +
                    (OhidProtocol.ResponsePollMax * OhidProtocol.ResponsePollIntervalMs) + " ms.");
            }
        }

        // ------------------------- Transport JM/QF -------------------------
        private byte[] JmExchange(byte[] packet, string what)
        {
            lock (_ioLock)
            {
                if (_stream == null) throw new InvalidOperationException("Chua mo thiet bi DareU.");
                var send = new byte[packet.Length + 1];
                send[0] = JmProtocol.ReportId;
                Array.Copy(packet, 0, send, 1, packet.Length);
                _stream.Write(send);
                for (int i = 0; i < JmProtocol.ResponsePollMax; i++)
                {
                    Thread.Sleep(JmProtocol.ResponsePollIntervalMs);
                    try
                    {
                        var raw = _stream.Read();
                        int off = raw.Length > JmProtocol.PacketLen ? 1 : 0;
                        if (raw.Length - off >= 2 && raw[off + 1] == packet[1])
                        {
                            var body = new byte[JmProtocol.PacketLen];
                            Array.Copy(raw, off, body, 0, Math.Min(JmProtocol.PacketLen, raw.Length - off));
                            return body;
                        }
                    }
                    catch { }
                }
                throw new TimeoutException(what + ": khong nhan dap tra JM khop byte lenh sau " +
                    (JmProtocol.ResponsePollMax * JmProtocol.ResponsePollIntervalMs) + " ms.");
            }
        }

        // ------------------- Probe doc (chay moi che do) -------------------
        /// <summary>Probe doc theo ho: TG doc FW version + ten; OHID truy
        /// van version hex; JM doc GET_DEVICE_INFO. Chi doc — chay truc
        /// tiep ke ca khi dry-run.</summary>
        public string ProbeRead()
        {
            if (_stream == null) throw new InvalidOperationException("Chua mo thiet bi DareU.");
            if (!Identified) return "PID la — khong co probe doc an toan cho ho chua biet; chi xem registry.";
            if (IsTg)
            {
                var fw = TgExchange(TgProtocol.BuildGetFwVersion(), "DareU doc FW");
                var name = TgExchange(TgProtocol.BuildGetName(), "DareU doc ten");
                return $"TG doc OK: FW reply [{HexLog(Slice(fw, 0, 16))}...], ten reply [{HexLog(Slice(name, 0, 16))}...].";
            }
            if (IsOhid)
            {
                var r = OhidExchange(OhidProtocol.BuildOrder(OhidProtocol.OrderQueryVersionHex, null, false), "OHID doc version");
                var ver = OhidProtocol.ParseVersionHex(r);
                return ver.HasValue
                    ? $"OHID doc OK: FW version hex = 0x{ver.Value:X6}."
                    : $"OHID doc OK nhung dap tra ngan: [{HexLog(Slice(r, 0, 16))}].";
            }
            if (IsJm)
            {
                var rnd = new byte[4];
                new Random().NextBytes(rnd);
                var r = JmExchange(JmProtocol.BuildGetDeviceInfo(rnd), "JM doc thong tin");
                var info = JmProtocol.ParseDeviceInfo(r);
                return info.HasValue
                    ? $"JM doc OK: CID=0x{info.Value.Cid:X2} MID=0x{info.Value.Mid:X2} JmType=0x{info.Value.JmType:X2}."
                    : "JM doc OK nhung dap tra ngan.";
            }
            return "Ho chua ho tro probe.";
        }

        private static byte[] Slice(byte[] b, int off, int len)
        {
            var r = new byte[Math.Max(0, Math.Min(len, b.Length - off))];
            if (r.Length > 0) Array.Copy(b, off, r, 0, r.Length);
            return r;
        }

        // ------------------------- Ghi TG -------------------------
        private void TgWrite(byte[] packet, string what)
        {
            if (!DryRun && ExperimentalEnabled && Identified)
            {
                TgExchange(packet, what);
                L("[DareU] " + what + " — da gui + ACK.");
            }
            else
                L("[DareU] " + what + " (log): " + HexLog(packet));
        }

        public void SetLedEffect(byte effect, byte flag, byte speed, (byte R, byte G, byte B) color)
        {
            if (!IsTg) { L("[DareU] LED effect: ho nay dung khoi LED rieng (OHID) hoac chua co builder — chi log."); return; }
            TgWrite(TgProtocol.BuildLedEffect(0, effect, flag, speed,
                new List<(byte, byte, byte)> { color }), "TG dat hieu ung LED");
        }

        // ------------------- Magnetic Axis (chi TG-RT) -------------------
        private void RequireHe(string what)
        {
            if (!IsHe)
                throw new InvalidOperationException(what + ": chi danh cho model TG-RT (Magnetic Axis) — model hien tai khong phai TG-RT.");
        }

        private double Step() => IdentifiedEntry != null && IdentifiedEntry.TravelStepMm > 0
            ? IdentifiedEntry.TravelStepMm : 0.02;
        private int TVer() => IdentifiedEntry != null && IdentifiedEntry.TravelVersion >= 0
            ? IdentifiedEntry.TravelVersion : 1;

        public void SetGlobalTravel(double mm)
        {
            RequireHe("Dat hanh trinh toan cuc");
            var e = IdentifiedEntry;
            if (e.TravelMaxMm > 0 && (mm < e.TravelMinMm || mm > e.TravelMaxMm))
                throw new ArgumentOutOfRangeException(nameof(mm),
                    $"Hanh trinh {mm} mm ngoai dai cua {e.Name} ({e.TravelMinMm}–{e.TravelMaxMm} mm).");
            TgWrite(TgProtocol.BuildSetGlobalTravel(mm, Step(), TVer()),
                $"Magnetic Axis: hanh trinh toan cuc = {mm} mm");
        }

        public void SetSingleTrigger(byte keyId, double mm)
        {
            RequireHe("Dat Single Trigger");
            TgWrite(TgProtocol.BuildSetSingleTrigger(keyId, mm, Step(), TVer()),
                $"Magnetic Axis: Single Trigger phim {keyId} = {mm} mm");
        }

        public void SetRapidTrigger(byte keyId, double initMm, double pressMm, double releaseMm)
        {
            RequireHe("Dat Rapid Trigger");
            TgWrite(TgProtocol.BuildSetRapidTrigger(keyId, initMm, pressMm, releaseMm, Step(), TVer()),
                $"Magnetic Axis: Rapid Trigger phim {keyId} init {initMm} / nhan {pressMm} / nha {releaseMm} mm");
        }

        public void SetDeadZone(byte keyId, ushort rawValue)
        {
            RequireHe("Dat Dead Zone");
            TgWrite(TgProtocol.BuildSetDeadZone(keyId, rawValue),
                $"Magnetic Axis: Dead Zone phim {keyId} = 0x{rawValue:X4}");
        }

        public void SetGameMode(bool on)
        {
            RequireHe("Dat Game Mode");
            TgWrite(TgProtocol.BuildSetGameMode(on), "Magnetic Axis: Game Mode " + (on ? "BAT" : "TAT"));
        }

        /// <summary>Hieu chuan hanh trinh theo dung luong cua hang: gui
        /// [1] vao hieu chuan (nguoi dung nhan cac phim xuong day), sau do
        /// [0] thoat. Hai buoc deu qua cong ghi.</summary>
        public void TravelCalibrationEnter()
        {
            RequireHe("Hieu chuan hanh trinh");
            TgWrite(TgProtocol.BuildTravelCalibration(true),
                "Magnetic Axis: VAO hieu chuan hanh trinh (nhan cac phim xuong het co)");
        }

        public void TravelCalibrationExit()
        {
            RequireHe("Hieu chuan hanh trinh");
            TgWrite(TgProtocol.BuildTravelCalibration(false),
                "Magnetic Axis: THOAT hieu chuan hanh trinh");
        }

        /// <summary>Doc hanh trinh toan cuc (GET — chay moi che do).</summary>
        public double? ReadGlobalTravel()
        {
            RequireHe("Doc hanh trinh toan cuc");
            var reply = TgExchange(TgProtocol.BuildGetGlobalTravel(), "Doc hanh trinh toan cuc");
            return TgProtocol.DecodeTravel(reply, TgProtocol.HeaderLen, Step(), TVer());
        }

        // ------------------------- Ghi OHID -------------------------
        private void OhidWrite(byte[] packet, string what, bool saveAfter = true)
        {
            if (!DryRun && ExperimentalEnabled && Identified)
            {
                OhidExchange(packet, what);
                if (saveAfter) OhidExchange(OhidProtocol.BuildSaveUser(), what + " (SAVE_USER)");
                L("[DareU] " + what + " — da gui" + (saveAfter ? " + SAVE_USER." : "."));
            }
            else
                L("[DareU] " + what + " (log): " + HexLog(packet) +
                  (saveAfter ? " + SAVE_USER (log): " + HexLog(OhidProtocol.BuildSaveUser()) : ""));
        }

        public void OhidFactoryReset()
        {
            if (!IsOhid) throw new InvalidOperationException("Khoi phuc cai dat goc OHID: thiet bi hien tai khong phai ho OHID.");
            GateWriteOrThrow("Khoi phuc cai dat goc OHID");
            OhidWrite(OhidProtocol.BuildOrder(OhidProtocol.OrderFactory), "OHID khoi phuc cai dat goc (FACTORY)", saveAfter: false);
        }

        public void OhidSetSingleTrigger(IReadOnlyList<(byte KeyId, ushort TravelLe)> records)
        {
            if (!IsOhid) throw new InvalidOperationException("OHID Single Trigger: thiet bi hien tai khong phai ho OHID.");
            foreach (var p in OhidProtocol.BuildSingleRecords(records))
                OhidWrite(p, $"OHID Single Trigger ({records.Count} phim)");
        }

        public void OhidSetRt(IReadOnlyList<(byte KeyId, int ValueLe24)> records)
        {
            if (!IsOhid) throw new InvalidOperationException("OHID RT: thiet bi hien tai khong phai ho OHID.");
            foreach (var p in OhidProtocol.BuildRtRecords(records))
                OhidWrite(p, $"OHID Rapid Trigger ({records.Count} phim)");
        }

        public void OhidRgbNote()
        {
            // RGB_KEY / RGB_MATRIX: bang lenh co ma lenh nhung dinh dang ban
            // ghi khong co trong ho so web driver — chi ghi log, khong doan.
            L("[DareU] OHID RGB_KEY/RGB_MATRIX: ma lenh co trong bang lenh cua hang nhung dinh dang ban ghi khong co trong ho so — chi log, khong gui.");
        }

        // ------------------------- Ghi JM/QF -------------------------
        private void JmWrite(List<byte[]> packets, string what)
        {
            if (!DryRun && ExperimentalEnabled && Identified)
            {
                foreach (var p in packets) JmExchange(p, what);
                L("[DareU] " + what + $" — da gui {packets.Count} goi.");
            }
            else
                foreach (var p in packets) L("[DareU] " + what + " (log): " + HexLog(p));
        }

        /// <summary>Ghi buffer (keymap/macro) theo quy tac JM: 10 byte/goi,
        /// dia chi BE tu tang, count +128 cho goi chua cuoi.</summary>
        public void JmWriteBuffer(ushort startAddress, byte[] data)
        {
            if (!IsJm) throw new InvalidOperationException("Ghi buffer JM: thiet bi hien tai khong phai ho JM/QF.");
            JmWrite(JmProtocol.BuildWriteBuffer(startAddress, data),
                $"JM ghi buffer @0x{startAddress:X4} ({data?.Length ?? 0} byte)");
        }

        private static string HexLog(byte[] b) => b == null ? "" : F75Device.Hex(b, b.Length);

        private void L(string s) => Log?.Invoke(s);
        public void Dispose() => Close();
    }
}
