using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.14 — Muc "Cap nhat" trong panel Cai dat: kiem tra ban app moi
    // (docs/update.json tren repo), cap nhat bang thiet bi (co che san co
    // trong DeviceRegistry) va lam moi catalog GIF online
    // (docs/online-library-drive.json). File nay THUAN LOGIC (parse /
    // so sanh phien ban / tai + kiem bam) de unit test duoc; phan UI nam
    // o MainWindowUpdate.cs. Moi loi mang deu duoc goi thanh ket qua
    // (Ok, Error) — UI chi hien mot dong trang thai, khong nem exception.
    // =====================================================================

    /// <summary>Manifest ban app moi — dung dang docs/update.json tren repo.</summary>
    public sealed class AppUpdateManifest
    {
        public string Version { get; set; } = "";
        public string Notes { get; set; } = "";
        public string LightUrl { get; set; } = "";
        public string Sha256 { get; set; } = "";
    }

    public static class UpdateCore
    {
        // Cung host pattern voi DeviceRegistry.RawUrl (repo nguon cua du an).
        public const string ManifestUrl =
            "https://raw.githubusercontent.com/tiendz2k/T-Keyboard-Studio/main/docs/update.json";
        public const string GifCatalogUrl =
            "https://raw.githubusercontent.com/tiendz2k/T-Keyboard-Studio/main/docs/online-library-drive.json";

        /// <summary>Thu muc chua ban moi da tai (khong bao gio tu thay exe dang chay).</summary>
        public static string UpdatesDir => Path.Combine(GifLibraryStore.RootDir, "Updates");

        // ---------------- Manifest + so sanh phien ban ----------------

        /// <summary>Parse manifest; tra null neu JSON hong / thieu Version hop le.</summary>
        public static AppUpdateManifest ParseManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var m = JsonSerializer.Deserialize<AppUpdateManifest>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (m == null || CompareVersions(m.Version, "0") == null) return null;
                m.Version = m.Version.Trim();
                m.Notes = m.Notes ?? "";
                m.LightUrl = m.LightUrl ?? "";
                m.Sha256 = m.Sha256 ?? "";
                return m;
            }
            catch { return null; }
        }

        /// <summary>
        /// So sanh hai chuoi phien ban dang so cham (3.14.0). Tra -1/0/1;
        /// null neu MOT TRONG HAI khong parse duoc (rong, chu, dinh dang la).
        /// </summary>
        public static int? CompareVersions(string a, string b)
        {
            if (!TryParts(a, out var pa) || !TryParts(b, out var pb)) return null;
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int x = i < pa.Length ? pa[i] : 0;
                int y = i < pb.Length ? pb[i] : 0;
                if (x != y) return x < y ? -1 : 1;
            }
            return 0;
        }

        private static bool TryParts(string v, out int[] parts)
        {
            parts = null;
            if (string.IsNullOrWhiteSpace(v)) return false;
            var segs = v.Trim().TrimStart('v', 'V').Split('.');
            if (segs.Length == 0 || segs.Length > 4) return false;
            parts = new int[segs.Length];
            for (int i = 0; i < segs.Length; i++)
                if (!int.TryParse(segs[i], out parts[i]) || parts[i] < 0) return false;
            return true;
        }

        /// <summary>remote moi hon current (ca hai deu phai parse duoc).</summary>
        public static bool IsNewer(string remote, string current)
            => CompareVersions(remote, current) is int c && c > 0;

        /// <summary>
        /// Logic chi bao "Co ban moi" o muc Cap nhat: chi hien khi phien ban
        /// da biet (luu trong settings / vua kiem tra) moi hon ban dang chay.
        /// </summary>
        public static bool ShouldShowBadge(string latestKnownVersion, string currentVersion)
            => !string.IsNullOrEmpty(latestKnownVersion) && IsNewer(latestKnownVersion, currentVersion);

        // ---------------- Tai file + kiem bam SHA-256 ----------------

        /// <summary>(url, tempPath, progress01, ct) -> tai file ve tempPath; progress01 nhan 0..1 (hoac -1 neu chua biet tong). Tiem vao de test bang stub.</summary>
        public delegate Task Downloader(string url, string tempPath, IProgress<double> progress01, CancellationToken ct);

        public static string Sha256Hex(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Kiem bam cua file so voi gia tri ky vong (khong phan biet hoa thuong).</summary>
        public static bool VerifySha256(string path, string expectedHex)
        {
            if (string.IsNullOrWhiteSpace(expectedHex) || !File.Exists(path)) return false;
            try { return string.Equals(Sha256Hex(path), expectedHex.Trim(), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <summary>
        /// Tai mot file ve destPath (qua file .part) roi kiem SHA-256 neu
        /// manifest co ghi bam. Loi mang / sai bam -> xoa file tam, tra
        /// (false, ly do). Khong bao gio cham vao exe dang chay.
        /// progress01: 0..1 theo byte da tai (neu server cho biet tong).
        /// requireZipMagic (v3.17.1): payload PHAI bat dau bang chu ky ZIP
        /// "PK\x03\x04" — kiem TRUOC khi bam, de trang HTML lac duong (vd
        /// trang trung gian/dang nhap cua Drive) bi loai ngay ca khi bam
        /// tinh tren chinh trang do lai khop. Chi dung cho goi cap nhat app.
        /// </summary>
        public static async Task<(bool Ok, string Error)> DownloadAndVerifyAsync(
            string url, string destPath, string expectedSha256,
            IProgress<double> progress01 = null, Downloader downloader = null, CancellationToken ct = default,
            bool requireZipMagic = false)
        {
            if (string.IsNullOrWhiteSpace(url)) return (false, "Manifest thiếu link tải.");
            string tempPath = destPath + ".part";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                await (downloader ?? HttpDownloadAsync)(url, tempPath, progress01, ct);
                if (!File.Exists(tempPath)) return (false, "Tải xong nhưng không thấy file.");
                if (requireZipMagic && !HasZipMagic(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                    return (false, "File tải về không phải file zip hợp lệ — đã xoá, hãy thử lại.");
                }
                if (!string.IsNullOrWhiteSpace(expectedSha256) && !VerifySha256(tempPath, expectedSha256))
                {
                    try { File.Delete(tempPath); } catch { }
                    return (false, "File tải về sai mã băm SHA-256 — đã xoá, hãy thử lại.");
                }
                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(tempPath, destPath);
                return (true, null);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                return (false, ex.Message);
            }
        }

        /// <summary>File co bat dau bang chu ky ZIP "PK\x03\x04" khong (file ngan hon 4 byte = khong).</summary>
        public static bool HasZipMagic(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var head = new byte[4];
                    int read = 0, n;
                    while (read < 4 && (n = fs.Read(head, read, 4 - read)) > 0) read += n;
                    return read == 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04;
                }
            }
            catch { return false; }
        }

        // ---------------- v3.17.1: tai qua trang trung gian cua Google Drive ----------------
        // File zip >~25 MB tren Drive: GET truc tiep tra ve mot trang HTML
        // trung gian ("can't scan this file for viruses") chua <form> tro
        // toi drive.usercontent.google.com kem cac truong an (id, export,
        // confirm, uuid thay doi theo tung lan). Ban 3.17.0 tai nguyen
        // trang HTML do ve -> sai bam SHA-256 -> bao "tai that bai".
        // Ban nay: thay HTML thi boc tach form Drive (neu co) roi GET tiep
        // theo action + toan bo truong an lam query — payload that nam o
        // phan hoi cua request THU HAI. HTML khong phai form Drive (vd
        // trang dang nhap khi file chua chia se) -> loi sach, khong giu file.

        /// <summary>
        /// Chi danh cho unit test: tiem HttpMessageHandler (stub HTTP
        /// server cuc bo). Production luon null — dung handler mac dinh.
        /// </summary>
        internal static Func<HttpMessageHandler> HttpHandlerFactory;

        private static HttpClient CreateHttpClient()
        {
            var handler = HttpHandlerFactory?.Invoke();
            var http = handler != null ? new HttpClient(handler) : new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(10);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TienVuKeyboardStudio/" + AboutInfo.Version);
            return http;
        }

        private static bool IsHtml(HttpResponseMessage resp)
            => string.Equals(resp.Content.Headers.ContentType?.MediaType, "text/html",
                StringComparison.OrdinalIgnoreCase);

        private const string HtmlNotFileError =
            "Máy chủ trả về trang HTML thay vì file — file có thể chưa được chia sẻ công khai, hãy thử lại sau.";

        private static async Task HttpDownloadAsync(string url, string tempPath, IProgress<double> progress01, CancellationToken ct)
        {
            using (var http = CreateHttpClient())
            using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                if (IsHtml(resp))
                {
                    string html = await ReadHtmlCappedAsync(resp.Content, ct);
                    Uri pageUri = resp.RequestMessage?.RequestUri ?? new Uri(url);
                    if (TryParseDriveConfirmUrl(html, pageUri, out string confirmUrl))
                    {
                        // Request thu hai theo form Drive — day moi la payload.
                        using (var resp2 = await http.GetAsync(confirmUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                        {
                            resp2.EnsureSuccessStatusCode();
                            if (IsHtml(resp2)) throw new InvalidOperationException(HtmlNotFileError);
                            await CopyPayloadAsync(resp2.Content, tempPath, progress01, ct);
                        }
                        return;
                    }
                    throw new InvalidOperationException(HtmlNotFileError);
                }
                await CopyPayloadAsync(resp.Content, tempPath, progress01, ct);
            }
        }

        /// <summary>Chep payload ve tempPath kem bao tien do (nguyen trang hanh vi cu).</summary>
        private static async Task CopyPayloadAsync(HttpContent content, string tempPath, IProgress<double> progress01, CancellationToken ct)
        {
            long totalLen = content.Headers.ContentLength ?? -1;
            using (var src = await content.ReadAsStreamAsync(ct))
            using (var fs = File.Create(tempPath))
            {
                var buf = new byte[81920];
                long total = 0;
                int n;
                while ((n = await src.ReadAsync(buf, 0, buf.Length, ct)) > 0)
                {
                    await fs.WriteAsync(buf, 0, n, ct);
                    total += n;
                    progress01?.Report(totalLen > 0 ? (double)total / totalLen : -1);
                }
            }
        }

        /// <summary>
        /// Doc than HTML nhung gioi han 1 MB — trang trung gian cua Drive
        /// chi vai KB; vuot nguong thi khong the la form xac nhan hop le.
        /// </summary>
        private static async Task<string> ReadHtmlCappedAsync(HttpContent content, CancellationToken ct)
        {
            const int cap = 1024 * 1024;
            using (var src = await content.ReadAsStreamAsync(ct))
            using (var ms = new MemoryStream())
            {
                var buf = new byte[16384];
                int n;
                while (ms.Length <= cap && (n = await src.ReadAsync(buf, 0, buf.Length, ct)) > 0)
                    ms.Write(buf, 0, n);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>
        /// Tim trong HTML mot &lt;form&gt; ma action (sau khi giai quyet
        /// theo pageUri) tro toi host cua Drive (drive.usercontent.google.com
        /// hoac drive.google.com), thu thap MOI truong an (input
        /// type="hidden") ben trong form do — doc ten/gia tri tong quat,
        /// khong hard-code ten truong — roi dung URL GET: action + "?"/"&amp;"
        /// + cac truong da ma hoa query. Khong thay form Drive -> false.
        /// </summary>
        public static bool TryParseDriveConfirmUrl(string html, Uri pageUri, out string confirmUrl)
        {
            confirmUrl = null;
            if (string.IsNullOrEmpty(html)) return false;
            foreach (Match form in Regex.Matches(html, @"<form\b[^>]*>", RegexOptions.IgnoreCase))
            {
                string action = GetAttr(form.Value, "action");
                if (string.IsNullOrWhiteSpace(action)) continue;
                action = WebUtility.HtmlDecode(action).Trim();
                Uri actionUri;
                try
                {
                    actionUri = pageUri != null ? new Uri(pageUri, action) : new Uri(action);
                }
                catch { continue; }
                if (!IsDriveHost(actionUri.Host)) continue;

                int bodyStart = form.Index + form.Length;
                int bodyEnd = html.IndexOf("</form>", bodyStart, StringComparison.OrdinalIgnoreCase);
                if (bodyEnd < 0) bodyEnd = html.Length;
                string body = html.Substring(bodyStart, bodyEnd - bodyStart);

                var fields = new List<KeyValuePair<string, string>>();
                foreach (Match input in Regex.Matches(body, @"<input\b[^>]*>", RegexOptions.IgnoreCase))
                {
                    string type = GetAttr(input.Value, "type");
                    if (!string.Equals(type, "hidden", StringComparison.OrdinalIgnoreCase)) continue;
                    string name = GetAttr(input.Value, "name");
                    if (string.IsNullOrEmpty(name)) continue;
                    string value = GetAttr(input.Value, "value") ?? "";
                    fields.Add(new KeyValuePair<string, string>(
                        WebUtility.HtmlDecode(name), WebUtility.HtmlDecode(value)));
                }
                if (fields.Count == 0) continue;

                var sb = new StringBuilder(actionUri.GetLeftPart(UriPartial.Path));
                // Giu query san co cua action (neu co) roi noi cac truong form.
                sb.Append(string.IsNullOrEmpty(actionUri.Query) ? "?" : actionUri.Query + "&");
                for (int i = 0; i < fields.Count; i++)
                {
                    if (i > 0) sb.Append('&');
                    sb.Append(Uri.EscapeDataString(fields[i].Key));
                    sb.Append('=');
                    sb.Append(Uri.EscapeDataString(fields[i].Value));
                }
                confirmUrl = sb.ToString();
                return true;
            }
            return false;
        }

        private static bool IsDriveHost(string host)
            => string.Equals(host, "drive.usercontent.google.com", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "drive.google.com", StringComparison.OrdinalIgnoreCase);

        /// <summary>Lay gia tri mot thuoc tinh trong the HTML (nhay doi/don/khong nhay); null neu khong co.</summary>
        private static string GetAttr(string tag, string name)
        {
            var m = Regex.Match(tag,
                @"\b" + Regex.Escape(name) + @"\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))",
                RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            for (int i = 1; i <= 3; i++)
                if (m.Groups[i].Success) return m.Groups[i].Value;
            return "";
        }

        // ---------------- Lam moi du lieu (registry / GIF) ----------------

        /// <summary>
        /// Kiem tra JSON catalog GIF tai ve: hop le khi parse ra it nhat
        /// 1 muc. Tra (Ok, so muc, loi) de UI hien dung mot dong trang thai.
        /// </summary>
        public static (bool Ok, int Count, string Error) ValidateCatalogJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return (false, 0, "Dữ liệu tải về trống.");
            try
            {
                var items = OnlineCatalogCore.ParseCatalog(json);
                return items.Count > 0 ? (true, items.Count, null) : (false, 0, "Catalog không có mục hợp lệ nào.");
            }
            catch (Exception ex) { return (false, 0, ex.Message); }
        }

        /// <summary>Ket qua kiem tra registry: dung lai TryParseValid san co cua DeviceRegistry.</summary>
        public static (bool Ok, int Count, string Error) ValidateRegistryJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return (false, 0, "Dữ liệu tải về trống.");
            return DeviceRegistry.TryParseValid(json, out var reg, out string error)
                ? (true, reg.Devices.Count, null)
                : (false, 0, error);
        }

        // ---------------- Trang thai / thoi gian ----------------

        public static string NowIso() => DateTime.UtcNow.ToString("o");

        /// <summary>Dong "lan kiem tra gan nhat" cho UI; rong -> chua kiem tra.</summary>
        public static string FormatLastCheck(string isoUtc)
        {
            if (string.IsNullOrWhiteSpace(isoUtc)) return "Chưa kiểm tra lần nào.";
            if (DateTime.TryParse(isoUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc))
                return "Lần kiểm tra gần nhất: " + utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + ".";
            return "Chưa kiểm tra lần nào.";
        }
    }
}
