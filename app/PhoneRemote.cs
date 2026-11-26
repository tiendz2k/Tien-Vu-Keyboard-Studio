using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Dieu khien tu dien thoai: HTTP server cuc bo (TcpListener, KHONG dung
    // HttpListener de tranh quyen urlacl cua Windows). Chi nghe trong LAN,
    // mac dinh TAT, bat/tat tu trang Dien thoai. Bao ve bang PIN 4 so (ma QR
    // chua san PIN trong URL). Moi thao tac deu goi lai dung lop thiet bi
    // cua app nen ton trong Dry-run toan cuc nhu thao tac tren PC.
    // =====================================================================
    public sealed class RemoteState
    {
        public bool DryRun;
        public bool Experimental;
        public string Model = "";
        public List<(int Id, string Name)> Effects = new List<(int, string)>();
        public List<string> Gifs = new List<string>();
    }

    public sealed class PhoneRemoteServer : IDisposable
    {
        public int Port { get; private set; }
        public string Pin { get; private set; } = "";
        public bool Running { get; private set; }
        public string LocalIp { get; private set; } = "127.0.0.1";
        public string Url => $"http://{LocalIp}:{Port}/?k={Pin}";

        public Func<RemoteState> GetState;
        public Action<int, byte, byte, byte, int, int, int> ApplyEffect; // id, R,G,B, sang 0..4, toc do 0..4, huong
        public Action<string> ApplyGif;
        public Action<int> ApplyWidget; // 0 = thong so, 2 = dong ho

        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public void Start()
        {
            if (Running) return;
            Pin = new Random().Next(0, 10000).ToString("D4");
            LocalIp = FindLanIp();
            Exception last = null;
            for (int attempt = 0; attempt < 25 && _listener == null; attempt++)
            {
                int port = new Random().Next(8100, 8900);
                try
                {
                    _listener = new TcpListener(IPAddress.Any, port);
                    _listener.Start();
                    Port = port;
                }
                catch (Exception ex) { last = ex; _listener = null; }
            }
            if (_listener == null) throw new InvalidOperationException("Không mở được cổng LAN: " + last?.Message);
            _running = true;
            Running = true;
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "PhoneRemote" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            Running = false;
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }

        public void Dispose() => Stop();

        private static string FindLanIp()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                            return addr.Address.ToString();
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { break; }
                new Thread(() => Handle(client)) { IsBackground = true }.Start();
            }
        }

        private void Handle(TcpClient client)
        {
            try
            {
                client.ReceiveTimeout = 8000;
                client.SendTimeout = 8000;
                using (client)
                using (var ns = client.GetStream())
                {
                    string head = ReadHead(ns, out string rest);
                    if (head == null) return;
                    var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    var parts = lines[0].Split(' ');
                    if (parts.Length < 2) return;
                    string method = parts[0];
                    string rawPath = parts[1];
                    string path = rawPath, query = "";
                    int qi = rawPath.IndexOf('?');
                    if (qi >= 0) { path = rawPath.Substring(0, qi); query = rawPath.Substring(qi + 1); }
                    var q = ParseQuery(query);
                    string pin = q.TryGetValue("k", out string kv) ? kv : null;
                    string body = rest ?? "";
                    int contentLength = 0;
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            int.TryParse(line.Substring(15).Trim(), out contentLength);
                        if (line.StartsWith("X-Pin:", StringComparison.OrdinalIgnoreCase))
                            pin = line.Substring(6).Trim();
                    }
                    while (body.Length < contentLength)
                    {
                        var buf = new byte[4096];
                        int n = ns.Read(buf, 0, Math.Min(buf.Length, contentLength - body.Length));
                        if (n <= 0) break;
                        body += Encoding.UTF8.GetString(buf, 0, n);
                    }
                    bool authed = pin == Pin;
                    string response;
                    if (path == "/" && method == "GET")
                        response = Page(authed);
                    else if (path == "/api/state" && method == "GET")
                        response = authed ? Json(StateJson()) : Json("{\"error\":\"PIN sai\"}", 403);
                    else if (path == "/api/effect" && method == "POST")
                        response = authed ? DoEffect(body) : Json("{\"error\":\"PIN sai\"}", 403);
                    else if (path == "/api/gif" && method == "POST")
                        response = authed ? DoGif(body) : Json("{\"error\":\"PIN sai\"}", 403);
                    else if (path == "/api/widget" && method == "POST")
                        response = authed ? DoWidget(body) : Json("{\"error\":\"PIN sai\"}", 403);
                    else
                        response = Json("{\"error\":\"not found\"}", 404);
                    var bytes = Encoding.UTF8.GetBytes(response);
                    ns.Write(bytes, 0, bytes.Length);
                    ns.Flush();
                }
            }
            catch { /* mot ket noi loi khong duoc lam sap server */ }
        }

        private static string ReadHead(NetworkStream ns, out string rest)
        {
            rest = "";
            var sb = new StringBuilder();
            var buf = new byte[1];
            int matched = 0;
            var terminator = new[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
            while (sb.Length < 65536)
            {
                int n = ns.Read(buf, 0, 1);
                if (n <= 0) return null;
                sb.Append((char)buf[0]);
                matched = buf[0] == terminator[matched] ? matched + 1 : (buf[0] == terminator[0] ? 1 : 0);
                if (matched == 4) break;
            }
            return sb.ToString();
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var d = new Dictionary<string, string>();
            foreach (var pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0) d[Uri.UnescapeDataString(pair.Substring(0, eq))] = Uri.UnescapeDataString(pair.Substring(eq + 1));
            }
            return d;
        }

        private string StateJson()
        {
            var s = GetState?.Invoke() ?? new RemoteState();
            using (var ms = new MemoryStream())
            {
                using (var w = new Utf8JsonWriter(ms))
                {
                    w.WriteStartObject();
                    w.WriteBoolean("dryRun", s.DryRun);
                    w.WriteBoolean("experimental", s.Experimental);
                    w.WriteString("model", s.Model);
                    w.WriteStartArray("effects");
                    foreach (var (id, name) in s.Effects) { w.WriteStartObject(); w.WriteNumber("id", id); w.WriteString("name", name); w.WriteEndObject(); }
                    w.WriteEndArray();
                    w.WriteStartArray("gifs");
                    foreach (var g in s.Gifs) w.WriteStringValue(g);
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private string DoEffect(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    var r = doc.RootElement;
                    int id = r.GetProperty("id").GetInt32();
                    byte cr = (byte)r.GetProperty("r").GetInt32();
                    byte cg = (byte)r.GetProperty("g").GetInt32();
                    byte cb = (byte)r.GetProperty("b").GetInt32();
                    int br = r.TryGetProperty("brightness", out var b) ? b.GetInt32() : 3;
                    int sp = r.TryGetProperty("speed", out var s) ? s.GetInt32() : 2;
                    int dir = r.TryGetProperty("direction", out var d) ? d.GetInt32() : 0;
                    ApplyEffect?.Invoke(id, cr, cg, cb, br, sp, dir);
                }
                return Json("{\"ok\":true}");
            }
            catch (Exception ex) { return Json("{\"error\":\"" + Escape(ex.Message) + "\"}", 400); }
        }

        private string DoGif(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    string name = doc.RootElement.GetProperty("name").GetString();
                    ApplyGif?.Invoke(name);
                }
                return Json("{\"ok\":true}");
            }
            catch (Exception ex) { return Json("{\"error\":\"" + Escape(ex.Message) + "\"}", 400); }
        }

        private string DoWidget(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    int kind = doc.RootElement.GetProperty("kind").GetInt32();
                    ApplyWidget?.Invoke(kind);
                }
                return Json("{\"ok\":true}");
            }
            catch (Exception ex) { return Json("{\"error\":\"" + Escape(ex.Message) + "\"}", 400); }
        }

        private static string Escape(string s) => s.Replace("\\", " ").Replace("\"", " ").Replace("\n", " ");

        private static string Json(string body, int code = 200)
        {
            string status = code == 200 ? "200 OK" : code == 403 ? "403 Forbidden" : code == 404 ? "404 Not Found" : "400 Bad Request";
            return $"HTTP/1.1 {status}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
        }

        private string Page(bool authed)
        {
            string html = authed ? RemoteHtml : PinHtml;
            return $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(html)}\r\nConnection: close\r\n\r\n{html}";
        }

        private const string PinHtml = @"<!DOCTYPE html><html lang='vi'><head><meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'><title>T Keyboard — PIN</title>
<style>body{background:#0B0E14;color:#E7EAF2;font-family:system-ui,sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0}
.box{text-align:center;padding:24px}input{font-size:28px;letter-spacing:8px;text-align:center;width:180px;padding:12px;border-radius:12px;border:1px solid #333;background:#151A24;color:#fff}
button{margin-top:16px;font-size:17px;padding:12px 28px;border:0;border-radius:12px;background:linear-gradient(90deg,#7C3AED,#22D3EE);color:#fff}</style></head>
<body><div class='box'><h2>T Keyboard</h2><p>Nhập PIN hiển thị trên máy tính:</p>
<input id='p' inputmode='numeric' maxlength='4' placeholder='••••'><br><button onclick='go()'>Vào điều khiển</button></div>
<script>function go(){var v=document.getElementById('p').value;if(v.length==4)location.href='/?k='+v;}</script></body></html>";

        private const string RemoteHtml = @"<!DOCTYPE html><html lang='vi'><head><meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'><title>T Keyboard — Điều khiển</title>
<style>body{background:#0B0E14;color:#E7EAF2;font-family:system-ui,sans-serif;margin:0;padding:16px}
h1{font-size:19px}h2{font-size:15px;color:#9AA3B2;margin:18px 0 8px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:8px}
button{padding:12px 8px;border-radius:12px;border:1px solid #262C3A;background:#151A24;color:#E7EAF2;font-size:14px}
button:active{background:#7C3AED}.wide{grid-column:1/-1}input[type=color]{width:100%;height:44px;border:0;background:none}
#st{font-size:13px;color:#9AA3B2;white-space:pre-line}.tag{display:inline-block;padding:2px 8px;border-radius:99px;background:#2A2140;color:#C4B5FD;font-size:12px}</style></head>
<body><h1>T Keyboard <span class='tag' id='model'></span></h1><div id='st'>Đang tải…</div>
<h2>Hiệu ứng đèn</h2><input type='color' id='col' value='#7C3AED'><div class='grid' id='fx'></div>
<h2>GIF màn hình TFT</h2><div class='grid' id='gifs'></div>
<h2>Widget TFT</h2><div class='grid'><button onclick='widget(0)'>Thông số máy</button><button onclick='widget(2)'>Đồng hồ</button></div>
<script>var K=new URLSearchParams(location.search).get('k')||'';
async function api(p,b){var o=b?{method:'POST',headers:{'Content-Type':'application/json','X-Pin':K},body:JSON.stringify(b)}:{headers:{'X-Pin':K}};
var r=await fetch(p,o);return r.json();}
function hex(c){var n=parseInt(c.slice(1),16);return{r:n>>16&255,g:n>>8&255,b:n&255};}
async function load(){var s=await api('/api/state');document.getElementById('model').textContent=s.model||'';
document.getElementById('st').textContent=(s.dryRun?'Đang ở chế độ Dry-run: thao tác chỉ ghi log, không ghi xuống phím thật.':'Đang ở chế độ GHI THẬT xuống bàn phím.')+(s.experimental?'\nLớp thử nghiệm per-key: BẬT':'');
var fx=document.getElementById('fx');fx.innerHTML='';s.effects.forEach(function(e){var b=document.createElement('button');b.textContent=e.name;
b.onclick=async function(){var c=hex(document.getElementById('col').value);await api('/api/effect',{id:e.id,r:c.r,g:c.g,b:c.b,brightness:3,speed:2,direction:0});toast('Đã áp dụng: '+e.name);};fx.appendChild(b);});
var g=document.getElementById('gifs');g.innerHTML='';s.gifs.forEach(function(n){var b=document.createElement('button');b.textContent=n;
b.onclick=async function(){await api('/api/gif',{name:n});toast('Đã nạp GIF: '+n);};g.appendChild(b);});}
async function widget(k){await api('/api/widget',{kind:k});toast('Đã đẩy widget lên TFT');}
function toast(t){var s=document.getElementById('st');s.textContent=t;}
load();</script></body></html>";
    }
}
