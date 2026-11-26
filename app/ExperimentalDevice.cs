using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HidSharp;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Transport rieng cho LOP THU NGHIEM (0xF5 / 0x23 / 0x20 / 0x78).
    // Tach khoi F75Device (duong verified, giu nguyen 100%) — lop nay mo
    // stream HID rieng toi cung kenh lenh 0xFF13 cua F75 Max khi can.
    //
    // QUY TAC AN TOAN (hard-coded, khong co ngoai le):
    // - Doc 0xF5 (ReadColors): CHI DOC — duoc phep chay live bat cu luc nao
    //   nguoi dung bam nut, ke ca khi Dry-run toan cuc dang bat.
    // - Ghi (SendSteps): CHI gui that khi ExperimentalEnabled == true VA
    //   DryRun == false. Thieu mot trong hai -> chi ghi log cac goi se gui.
    // - ACK bat buoc: byte[3] == 0x01 va byte[1] khop opcode; sai ACK la
    //   dung ngay lap tuc (abort) va bao loi ro rang.
    // =====================================================================
    public sealed class ExperimentalDevice : IDisposable
    {
        public bool DryRun { get; set; } = true;
        public bool ExperimentalEnabled { get; set; } = false;
        public Action<string> Log { get; set; } = _ => { };

        private readonly object _gate = new object();
        private HidStream _cmdStream;
        private bool _disposed;

        public bool WritesArmed => ExperimentalEnabled && !DryRun;

        private bool EnsureStream()
        {
            if (_cmdStream != null) return true;
            var cmd = F75Device.Enumerate()
                .FirstOrDefault(i => i.MaxFeature >= 64 &&
                                     i.Device.VendorID == F75Protocol.VendorId && i.Device.ProductID == F75Protocol.ProductId);
            if (cmd == null)
            {
                Log("[THU NGHIEM] Khong tim thay kenh lenh F75 Max (0xFF13). Kiem tra cap USB-C.");
                return false;
            }
            if (!cmd.Device.TryOpen(out _cmdStream))
            {
                Log("[THU NGHIEM] Khong mo duoc kenh lenh (thiet bi dang ban?).");
                return false;
            }
            _cmdStream.ReadTimeout = 300;
            _cmdStream.WriteTimeout = 1000;
            Log("[THU NGHIEM] Da mo kenh lenh rieng cho lop thu nghiem.");
            return true;
        }

        private void SendFeature(byte[] packet64)
        {
            var buf = new byte[65];
            buf[0] = 0;
            Array.Copy(packet64, 0, buf, 1, 64);
            _cmdStream.SetFeature(buf, 0, buf.Length);
        }

        private byte[] TryGetFeature()
        {
            var buf = new byte[65];
            buf[0] = 0;
            try
            {
                _cmdStream.GetFeature(buf, 0, buf.Length);
                var data = new byte[64];
                Array.Copy(buf, 1, data, 0, 64);
                return data;
            }
            catch { return null; }
        }

        private bool WaitAck(byte opcode, int tries = 10)
        {
            for (int i = 0; i < tries; i++)
            {
                Thread.Sleep(30);
                var reply = TryGetFeature();
                if (reply == null) continue;
                if (reply[0] == 0x04 && reply[1] == opcode && reply[3] == 0x01) return true;
                if (reply[0] == 0x04 && reply[1] == opcode)
                    Log($"[THU NGHIEM] ACK la: byte[3]=0x{reply[2]:X2}... (cho 0x01 o byte[3]) — doc lai: {F75Device.Hex(reply, 8)}");
            }
            return false;
        }

        // Gui mot chuoi Step. Tra ve true neu da gui that (live), false neu
        // chi log (dry-run / chua bat thu nghiem). Nem exception khi ACK sai.
        public bool SendSteps(string title, IList<F75Protocol.Step> steps)
        {
            lock (_gate)
            {
                if (!WritesArmed)
                {
                    string why = !ExperimentalEnabled
                        ? "cong tac Thu nghiem dang TAT"
                        : "Dry-run toan cuc dang BAT";
                    Log($"[THU NGHIEM - CHI LOG] {title}: se gui {steps.Count} goi (khong gui that — {why}).");
                    foreach (var s in steps)
                        Log($"    -> {s.Name}: {F75Device.Hex(s.Bytes, 24)}");
                    return false;
                }
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh lenh F75 Max.");
                Log($"[THU NGHIEM - GUI THAT] {title}: {steps.Count} goi.");
                foreach (var s in steps)
                {
                    SendFeature(s.Bytes);
                    if (s.ExpectAck)
                    {
                        if (!WaitAck(s.Bytes[1]))
                            throw new InvalidOperationException(
                                $"Thiet bi khong ACK buoc '{s.Name}' (opcode 0x{s.Bytes[1]:X2}). Dung lai de an toan.");
                    }
                    else Thread.Sleep(10); // PACKET_GAP cua Graham: >=2ms, dung 10ms
                }
                return true;
            }
        }

        // =====================================================================
        // 0xF5 — doc mau per-key (CHI DOC, chay live theo yeu cau nguoi dung).
        // Framing theo protocol.py: commit -> query -> 9 lan GET_FEATURE tho.
        // =====================================================================
        public Dictionary<byte, (byte R, byte G, byte B)> ReadColors()
        {
            lock (_gate)
            {
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh lenh F75 Max. Kiem tra cap USB-C va dong app chinh hang / app cua shop neu dang mo.");
                var steps = L99Protocol.BuildColorQuerySteps();
                Log("[0xF5] Bat dau doc mau phim (chi doc)...");
                foreach (var s in steps)
                {
                    SendFeature(s.Bytes);
                    Log($"[0xF5] -> {s.Name}: {F75Device.Hex(s.Bytes, 12)}");
                    if (!WaitAck(s.Bytes[1], tries: 14))
                        Log($"[0xF5] Canh bao: chua thay ACK chuan cho 0x{s.Bytes[1]:X2}, van tiep tuc doc khoi.");
                    Thread.Sleep(10);
                }
                var blocks = new List<byte[]>();
                int attempts = 0;
                while (blocks.Count < L99Protocol.ColorBlockCount && attempts < 60)
                {
                    attempts++;
                    var data = TryGetFeature();
                    if (data == null) { Thread.Sleep(25); continue; }
                    // Bo qua ban ACK echo cua chinh lenh query (04 F5 .. 01, con lai 0)
                    bool isAckEcho = data[0] == 0x04 && data[1] == L99Protocol.OpColorQuery && data[3] == 0x01
                                     && data.Skip(4).Take(56).All(b => b == 0);
                    if (isAckEcho) { Log("[0xF5] (bo qua 1 ban ACK echo)"); continue; }
                    blocks.Add(data);
                    Log($"[0xF5] <- khoi {blocks.Count}/9: {F75Device.Hex(data, 16)}");
                    Thread.Sleep(10);
                }
                if (blocks.Count < L99Protocol.ColorBlockCount)
                    throw new InvalidOperationException(
                        $"Chi doc duoc {blocks.Count}/9 khoi mau. Co the app chinh hang/shop dang mo va chiem kenh — hay dong chung roi thu lai.");
                var colors = L99Protocol.ParseColorBlocks(blocks);
                Log($"[0xF5] Doc xong: {colors.Count} id phim co mau.");
                return colors;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _cmdStream?.Dispose(); } catch { }
            _cmdStream = null;
        }
    }
}
