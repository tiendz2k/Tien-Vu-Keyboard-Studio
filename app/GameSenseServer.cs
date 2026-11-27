using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.3 — GameSense: HTTP server cuc bo nhan payload CS2 GSI.
    //
    // - Chi nghe 127.0.0.1 (localhost) — khong mo ra LAN/internet.
    // - Dung TcpListener tho nhu PhoneRemote (tranh quyen urlacl cua
    //   HttpListener tren Windows).
    // - Moi payload JSON duoc dua qua GsiEventDetector; su kien phat ra
    //   qua event Received de UI xu ly (mo web / overlay / den phim).
    // =====================================================================
    public sealed class GameSenseServer : IDisposable
    {
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private readonly GsiEventDetector _detector = new GsiEventDetector();

        public bool Running { get; private set; }
        public int Port { get; private set; }
        public string LastError { get; private set; } = "";
        public DateTime LastPayloadAt { get; private set; } = DateTime.MinValue;

        /// <summary>(su kien, payload tho) — goi tren thread nen.</summary>
        public event Action<GsiEventKind, string> EventReceived;
        /// <summary>Goi moi khi nhan duoc payload hop le (ke ca khong co su kien).</summary>
        public event Action PayloadReceived;
        public Action<string> Log { get; set; }

        public void Start(int port)
        {
            Stop();
            if (port <= 0 || port > 65535) port = 13650;
            Port = port;
            _detector.Reset();
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            Running = true;
            LastError = "";
            var ct = _cts.Token;
            _ = Task.Run(() => AcceptLoop(ct), ct);
            Log?.Invoke($"GameSense: dang nghe GSI tai 127.0.0.1:{port} (chi localhost).");
        }

        public void Stop()
        {
            Running = false;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }

        public void Dispose() => Stop();

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested) { LastError = ex.Message; Log?.Invoke("GameSense loi accept: " + ex.Message); }
                    break;
                }
                _ = Task.Run(() => HandleClient(client), ct);
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = 5000;
                    var stream = client.GetStream();
                    string body = ReadHttpBody(stream);
                    // Tra loi 200 ngay de game khong bi treo
                    var resp = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    stream.Write(resp, 0, resp.Length);
                    if (string.IsNullOrEmpty(body)) return;
                    LastPayloadAt = DateTime.Now;
                    PayloadReceived?.Invoke();
                    foreach (var ev in _detector.Feed(body))
                        EventReceived?.Invoke(ev, body);
                }
            }
            catch { /* mot payload loi khong duoc lam sap server */ }
        }

        // Doc HTTP request: header ket thuc bang \r\n\r\n, body theo Content-Length.
        private static string ReadHttpBody(NetworkStream stream)
        {
            var headerBuf = new List<byte>();
            var one = new byte[1];
            int matched = 0; // tien trinh khop "\r\n\r\n"
            while (headerBuf.Count < 64 * 1024)
            {
                int n = stream.Read(one, 0, 1);
                if (n <= 0) return null;
                headerBuf.Add(one[0]);
                byte b = one[0];
                if ((matched == 0 && b == (byte)'\r') || (matched == 1 && b == (byte)'\n') ||
                    (matched == 2 && b == (byte)'\r') || (matched == 3 && b == (byte)'\n'))
                {
                    matched++;
                    if (matched == 4) break;
                }
                else matched = b == (byte)'\r' ? 1 : 0;
            }
            string headers = Encoding.ASCII.GetString(headerBuf.ToArray());
            int contentLength = 0;
            foreach (var line in headers.Split(new[] { "\r\n" }, StringSplitOptions.None))
            {
                var idx = line.IndexOf(':');
                if (idx > 0 && line.Substring(0, idx).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line.Substring(idx + 1).Trim(), out contentLength);
            }
            if (contentLength <= 0 || contentLength > 1024 * 1024) return null;
            var body = new byte[contentLength];
            int read = 0;
            while (read < contentLength)
            {
                int n = stream.Read(body, read, contentLength - read);
                if (n <= 0) break;
                read += n;
            }
            return Encoding.UTF8.GetString(body, 0, read);
        }
    }

    /// <summary>Tim thu muc cfg cua CS2 tren may (qua Steam) de ghi file GSI.</summary>
    public static class Cs2Locator
    {
        /// <summary>Tra ve duong dan ...\game\csgo\cfg neu tim thay, nguoc lai null.</summary>
        public static string FindCfgDir()
        {
            try
            {
                var steamPaths = new List<string>();
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", false))
                {
                    var p = key?.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(p)) steamPaths.Add(p.Replace('/', '\\'));
                }
                foreach (var steam in steamPaths)
                {
                    foreach (var lib in EnumerateLibraries(steam))
                    {
                        string cfg = Path.Combine(lib, "steamapps", "common",
                            "Counter-Strike Global Offensive", "game", "csgo", "cfg");
                        if (Directory.Exists(cfg)) return cfg;
                    }
                }
            }
            catch { }
            return null;
        }

        private static IEnumerable<string> EnumerateLibraries(string steamRoot)
        {
            var libs = new List<string> { steamRoot };
            try
            {
                string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    foreach (var line in File.ReadAllLines(vdf))
                    {
                        var t = line.Trim();
                        if (t.StartsWith("\"path\""))
                        {
                            var parts = t.Split('"');
                            if (parts.Length >= 4)
                                libs.Add(parts[3].Replace("\\\\", "\\").Replace('/', '\\'));
                        }
                    }
                }
            }
            catch { }
            return libs;
        }
    }
}
