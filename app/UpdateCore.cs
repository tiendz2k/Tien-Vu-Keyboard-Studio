using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
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
        /// </summary>
        public static async Task<(bool Ok, string Error)> DownloadAndVerifyAsync(
            string url, string destPath, string expectedSha256,
            IProgress<double> progress01 = null, Downloader downloader = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url)) return (false, "Manifest thiếu link tải.");
            string tempPath = destPath + ".part";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                await (downloader ?? HttpDownloadAsync)(url, tempPath, progress01, ct);
                if (!File.Exists(tempPath)) return (false, "Tải xong nhưng không thấy file.");
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

        private static async Task HttpDownloadAsync(string url, string tempPath, IProgress<double> progress01, CancellationToken ct)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TienVuKeyboardStudio/" + AboutInfo.Version);
                using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    resp.EnsureSuccessStatusCode();
                    long totalLen = resp.Content.Headers.ContentLength ?? -1;
                    using (var src = await resp.Content.ReadAsStreamAsync(ct))
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
            }
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
