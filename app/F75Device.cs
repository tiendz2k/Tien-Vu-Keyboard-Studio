using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HidSharp;

namespace TienKeyboardStudio3
{
    public sealed class HidInterfaceInfo
    {
        public HidDevice Device;
        public string Path;
        public int MaxInput;
        public int MaxOutput;
        public int MaxFeature;
        public string Role;

        public override string ToString()
        {
            return string.Format("{0} | in={1} out={2} feature={3} | {4}",
                Role, MaxInput, MaxOutput, MaxFeature, Path);
        }
    }

    /// <summary>
    /// Lop van chuyen HID (HidSharp). Moi thao tac ghi deu di qua day va deu
    /// ton trong co DryRun: khi DryRun = true, khong mo stream ghi, khong gui
    /// bat ky byte nao xuong phim - chi ghi log goi "se gui".
    ///
    /// Chon giao dien (khop heuristic cua ca hai du an nguon mo):
    ///  - Kenh lenh  (usage page 0xFF13): thiet bi co MaxFeatureReport >= 64.
    ///  - Kenh man hinh (usage page 0xFF68): thiet bi co MaxOutputReport >= 4096.
    /// Tren Windows, buffer HID tinh ca byte report ID o dau (report ID = 0),
    /// nen goi feature gui di la 65 byte: [0]=0x00 + 64 byte goi lenh.
    /// </summary>
    public sealed class F75Device : IDisposable
    {
        public bool DryRun = true;
        public Action<string> Log = delegate { };

        private HidStream _cmdStream;
        private HidDevice _cmdDevice;
        private HidStream _dispStream;
        private HidDevice _dispDevice;

        private Thread _ackThread;
        private volatile bool _ackRunning;
        private volatile int _ackCount;

        // ---------------------------------------------------------------
        public static List<HidInterfaceInfo> Enumerate()
        {
            var list = new List<HidInterfaceInfo>();
            IEnumerable<HidDevice> devices;
            try
            {
                devices = DeviceList.Local.GetHidDevices(F75Protocol.VendorId, F75Protocol.ProductId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Khong liet ke duoc thiet bi HID: " + ex.Message, ex);
            }

            foreach (var d in devices)
            {
                var info = new HidInterfaceInfo
                {
                    Device = d,
                    Path = d.DevicePath,
                    MaxInput = Safe(() => d.GetMaxInputReportLength()),
                    MaxOutput = Safe(() => d.GetMaxOutputReportLength()),
                    MaxFeature = Safe(() => d.GetMaxFeatureReportLength()),
                };
                if (info.MaxFeature >= F75Protocol.PacketLength) info.Role = "Kenh lenh (0xFF13?)";
                else if (info.MaxOutput >= F75Protocol.ChunkLength) info.Role = "Kenh man hinh (0xFF68?)";
                else if (info.MaxInput >= 8) info.Role = "Kenh phim/media (chi doc)";
                else info.Role = "Giao dien HID khac";
                list.Add(info);
            }
            return list;
        }

        private static int Safe(Func<int> f)
        {
            try { return f(); } catch { return -1; }
        }

        // ---------------------------------------------------------------
        private void OpenStreamsIfNeeded()
        {
            if (_cmdStream != null) return;
            var all = Enumerate();
            var cmd = all.FirstOrDefault(i => i.MaxFeature >= F75Protocol.PacketLength);
            var disp = all.FirstOrDefault(i => i.MaxOutput >= F75Protocol.ChunkLength);
            if (cmd == null)
                throw new InvalidOperationException(
                    "Khong thay kenh lenh cua F75 Max (can giao dien co feature report >= 64 byte). " +
                    "Hay cam day USB-C va de phim o che do co day.");
            _cmdDevice = cmd.Device;
            if (!_cmdDevice.TryOpen(out _cmdStream))
                throw new InvalidOperationException("Khong mo duoc kenh lenh HID.");
            _cmdStream.ReadTimeout = 300;
            _cmdStream.WriteTimeout = 2000;
            Log("Da mo kenh lenh: " + cmd.Path);

            if (disp != null)
            {
                _dispDevice = disp.Device;
                if (_dispDevice.TryOpen(out _dispStream))
                {
                    _dispStream.ReadTimeout = Timeout.Infinite;
                    _dispStream.WriteTimeout = 5000;
                    Log("Da mo kenh man hinh: " + disp.Path);
                    StartAckReader();
                }
                else
                {
                    _dispStream = null;
                    Log("CANH BAO: khong mo duoc kenh man hinh - tai GIF se khong chay.");
                }
            }
        }

        private void StartAckReader()
        {
            if (_ackRunning) return;
            _ackRunning = true;
            _ackCount = 0;
            int len = Math.Max(64, Safe(() => _dispDevice.GetMaxInputReportLength()));
            _ackThread = new Thread(() =>
            {
                var buf = new byte[len];
                while (_ackRunning)
                {
                    try
                    {
                        int n = _dispStream.Read(buf, 0, buf.Length);
                        if (n > 0) Interlocked.Increment(ref _ackCount);
                    }
                    catch
                    {
                        if (!_ackRunning) break;
                        Thread.Sleep(20);
                    }
                }
            });
            _ackThread.IsBackground = true;
            _ackThread.Start();
        }

        private bool WaitAck(int baseline, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (_ackCount > baseline) return true;
                Thread.Sleep(10);
            }
            return _ackCount > baseline;
        }

        // ---------------------------------------------------------------
        /// <summary>Gui mot chuoi lenh len kenh lenh (feature report).</summary>
        public void RunCommandSequence(string title, List<F75Protocol.Step> steps)
        {
            Log("=== " + title + " ===");
            if (DryRun)
            {
                foreach (var s in steps)
                    Log("[DRY-RUN] Se gui SET_FEATURE(64B) " + s.Name + ": " + Hex(s.Bytes, 20) + " ...");
                Log("[DRY-RUN] Xong (khong gui byte nao xuong phim).");
                return;
            }

            OpenStreamsIfNeeded();
            foreach (var s in steps)
            {
                SendFeature(s.Bytes);
                Log("[WRITE] SET_FEATURE " + s.Name + ": " + Hex(s.Bytes, 20) + " ...");
                if (s.ExpectAck)
                {
                    var reply = PollAck(s.Bytes);
                    if (reply != null)
                        Log("  <- ACK: " + Hex(reply, 8) + " ... (byte[3]=0x" + reply[3].ToString("X2") + ")");
                    else
                        Log("  <- CANH BAO: het thoi gian cho ma khong thay ACK (byte[3]=0x01).");
                }
                else if (s.ReadEcho)
                {
                    Thread.Sleep(50);
                    var reply = TryGetFeature();
                    if (reply != null) Log("  <- phan hoi: " + Hex(reply, 8) + " ...");
                }
                Thread.Sleep(40); // firmware can thoi gian xu ly moi goi (theo ma nguon goc)
            }
            Log("=== Xong: " + title + " ===");
        }

        private void SendFeature(byte[] packet64)
        {
            int len = Math.Max(65, Safe(() => _cmdDevice.GetMaxFeatureReportLength()));
            var buf = new byte[len];
            buf[0] = 0x00; // report ID
            Array.Copy(packet64, 0, buf, 1, Math.Min(64, len - 1));
            _cmdStream.SetFeature(buf);
        }

        private byte[] TryGetFeature()
        {
            try
            {
                int len = Math.Max(65, Safe(() => _cmdDevice.GetMaxFeatureReportLength()));
                var buf = new byte[len];
                buf[0] = 0x00;
                _cmdStream.GetFeature(buf);
                var pkt = new byte[64];
                Array.Copy(buf, 1, pkt, 0, 64);
                return pkt;
            }
            catch
            {
                return null;
            }
        }

        private byte[] PollAck(byte[] sentPacket)
        {
            for (int i = 0; i < 6; i++)
            {
                Thread.Sleep(50);
                var reply = TryGetFeature();
                if (reply == null) continue;
                if (reply.Length > 3 && reply[0] == sentPacket[0] && reply[1] == sentPacket[1] && reply[3] == 0x01)
                    return reply;
            }
            return null;
        }

        // ---------------------------------------------------------------
        /// <summary>Tai stream man hinh (da ma hoa) len phim, theo chunk + ACK.</summary>
        public void UploadDisplay(byte[] stream, int slot, Action<int, int> progress)
        {
            int chunkCount = stream.Length / F75Protocol.ChunkLength;

            if (DryRun)
            {
                Log("=== Tai GIF len man hinh (slot " + slot + ") ===");
                Log("[DRY-RUN] Stream: " + stream.Length + " byte = " + chunkCount + " chunk x 4096 byte.");
                Log("[DRY-RUN] Header: so frame=" + stream[0] + ", delay[frame0]=" + stream[1] + " (don vi 2ms)");
                foreach (var s in F75Protocol.BuildDisplayBegin(slot, chunkCount))
                    Log("[DRY-RUN] Se gui SET_FEATURE " + s.Name + ": " + Hex(s.Bytes, 20) + " ...");
                for (int i = 0; i < chunkCount; i++)
                {
                    if (i < 2 || i >= chunkCount - 1 || (i + 1) % 50 == 0)
                        Log("[DRY-RUN] Se gui OUTPUT chunk " + (i + 1) + "/" + chunkCount + " (4096 byte) len kenh 0xFF68 va cho ACK.");
                    if (progress != null) progress(i + 1, chunkCount);
                }
                var fin = F75Protocol.BuildDisplayFinish();
                Log("[DRY-RUN] Se gui SET_FEATURE " + fin.Name + ": " + Hex(fin.Bytes, 20) + " ...");
                Log("[DRY-RUN] Xong (khong gui byte nao xuong phim).");
                return;
            }

            OpenStreamsIfNeeded();
            if (_dispStream == null)
                throw new InvalidOperationException("Khong co kenh man hinh (0xFF68) - khong tai GIF duoc.");

            Log("=== Tai GIF len man hinh (slot " + slot + ", " + chunkCount + " chunk) ===");
            RunCommandSequenceSilent(F75Protocol.BuildDisplayBegin(slot, chunkCount));

            // Cho ACK mo dau ~150 ms nhu ma nguon goc
            int base0 = _ackCount;
            WaitAck(base0, 150);

            int acked = 0;
            int outLen = Math.Max(F75Protocol.ChunkLength + 1, Safe(() => _dispDevice.GetMaxOutputReportLength()));
            for (int i = 0; i < chunkCount; i++)
            {
                var buf = new byte[outLen];
                buf[0] = 0x00; // report ID
                Array.Copy(stream, i * F75Protocol.ChunkLength, buf, 1, F75Protocol.ChunkLength);
                int before = _ackCount;
                _dispStream.Write(buf, 0, buf.Length);
                if (WaitAck(before, 400)) acked++;
                if (progress != null) progress(i + 1, chunkCount);
                if ((i + 1) % 50 == 0 || i == chunkCount - 1)
                    Log("  chunk " + (i + 1) + "/" + chunkCount + " - ACK nhan duoc: " + acked);
            }
            Log("Tong ket chunk: phim da ACK " + acked + "/" + chunkCount + " chunk.");

            RunCommandSequenceSilent(new List<F75Protocol.Step> { F75Protocol.BuildDisplayFinish() });
            Log("=== Xong tai GIF ===");
        }

        /// <summary>Gui chuoi lenh khong log tieu de (dung ben trong UploadDisplay khi ghi that).</summary>
        private void RunCommandSequenceSilent(List<F75Protocol.Step> steps)
        {
            foreach (var s in steps)
            {
                SendFeature(s.Bytes);
                Log("[WRITE] SET_FEATURE " + s.Name + ": " + Hex(s.Bytes, 20) + " ...");
                if (s.ExpectAck)
                {
                    var reply = PollAck(s.Bytes);
                    Log(reply != null ? "  <- ACK OK" : "  <- CANH BAO: khong thay ACK.");
                }
                Thread.Sleep(40);
            }
        }

        public static string Hex(byte[] data, int max)
        {
            int n = Math.Min(data.Length, max);
            var parts = new string[n];
            for (int i = 0; i < n; i++) parts[i] = data[i].ToString("X2");
            return string.Join(" ", parts);
        }

        public void Dispose()
        {
            _ackRunning = false;
            try { _dispStream?.Dispose(); } catch { }
            try { _cmdStream?.Dispose(); } catch { }
            _dispStream = null;
            _cmdStream = null;
        }
    }
}
