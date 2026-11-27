using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TienKeyboardStudio3
{
    /// <summary>Mot muc trong catalog Thu vien Online tren Google Drive (v3.8).</summary>
    public sealed class OnlineGifItem
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public string File { get; set; }
        public string DriveId { get; set; }
        public long Size { get; set; }
        public string DownloadUrl { get; set; }
    }

    /// <summary>File catalog dang { version, host, folder, items: [...] } nhu repo cong bo.</summary>
    public sealed class OnlineCatalogDoc
    {
        public int Version { get; set; }
        public string Host { get; set; }
        public string Folder { get; set; }
        public List<OnlineGifItem> Items { get; set; } = new List<OnlineGifItem>();
    }

    /// <summary>Parse catalog Thu vien Online — thuan logic, unit test duoc.</summary>
    public static class OnlineCatalogCore
    {
        public static List<OnlineGifItem> ParseCatalog(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<OnlineGifItem>();
            var doc = JsonSerializer.Deserialize<OnlineCatalogDoc>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var items = doc?.Items ?? new List<OnlineGifItem>();
            items.RemoveAll(i => i == null || string.IsNullOrEmpty(i.File) || string.IsNullOrEmpty(i.DownloadUrl));
            return items;
        }

        /// <summary>Ten file thumb PNG dong goi san tuong ung (cong-dong-01.gif -> cong-dong-01.png).</summary>
        public static string ThumbFileName(OnlineGifItem item)
            => item == null || string.IsNullOrEmpty(item.File)
                ? null
                : Path.GetFileNameWithoutExtension(item.File) + ".png";

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "?";
            if (bytes >= 1024 * 1024) return (bytes / 1048576.0).ToString("0.0") + " MB";
            if (bytes >= 1024) return (bytes / 1024.0).ToString("0") + " KB";
            return bytes + " B";
        }
    }

    public enum OnlineCacheState { NotDownloaded, Downloading, Downloaded }

    /// <summary>
    /// Bo nho dem cua Thu vien Online (v3.8): GIF chi tai ve KHI nguoi dung bam
    /// "Tai ve", luu o %AppData%/TienKeyboardStudio/OnlineLibrary/, kiem tra
    /// dung luong byte khop catalog sau khi tai; tai loi thi xoa file do dang.
    /// Trang thai doc thang tu dia nen song sot qua cac lan mo app.
    /// Downloader duoc tiem vao (delegate) de unit test khong cham mang that.
    /// </summary>
    public sealed class OnlineCacheStore
    {
        /// <summary>(url, tempPath, progress bytes, ct) -> tai file ve tempPath.</summary>
        public delegate Task Downloader(string url, string tempPath, IProgress<long> progress, CancellationToken ct);

        private readonly string _cacheDir;
        private readonly Downloader _downloader;
        private readonly HashSet<string> _inFlight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        public OnlineCacheStore(string cacheDir, Downloader downloader = null)
        {
            _cacheDir = cacheDir ?? throw new ArgumentNullException(nameof(cacheDir));
            _downloader = downloader ?? HttpDownloadAsync;
        }

        public string CacheDir => _cacheDir;

        public string CachedPath(OnlineGifItem item)
            => item == null || string.IsNullOrEmpty(item.File) ? null : Path.Combine(_cacheDir, item.File);

        /// <summary>File da tai duoc coi la hop le chi khi ton tai VA dung kich thuoc catalog.</summary>
        public bool IsDownloaded(OnlineGifItem item)
        {
            string p = CachedPath(item);
            if (p == null || !File.Exists(p)) return false;
            try { return item.Size <= 0 || new FileInfo(p).Length == item.Size; }
            catch { return false; }
        }

        public OnlineCacheState StateOf(OnlineGifItem item)
        {
            if (item != null)
            {
                lock (_lock) { if (_inFlight.Contains(item.File)) return OnlineCacheState.Downloading; }
            }
            return IsDownloaded(item) ? OnlineCacheState.Downloaded : OnlineCacheState.NotDownloaded;
        }

        /// <summary>
        /// Tai GIF ve cache. Thanh cong: tra ve duong dan file cache.
        /// That bai (mang loi / sai dung luong): xoa file tam, nem loi voi
        /// thong diep ro rang — khong bao gio de lai file do dang trong cache.
        /// </summary>
        public async Task<string> DownloadAsync(OnlineGifItem item, IProgress<long> progress = null, CancellationToken ct = default)
        {
            if (item == null || string.IsNullOrEmpty(item.DownloadUrl))
                throw new InvalidOperationException("Mục này thiếu link tải trong catalog.");
            string finalPath = CachedPath(item);
            string tempPath = finalPath + ".part";
            lock (_lock)
            {
                if (_inFlight.Contains(item.File))
                    throw new InvalidOperationException("Đang tải mục này rồi.");
                _inFlight.Add(item.File);
            }
            try
            {
                Directory.CreateDirectory(_cacheDir);
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                await _downloader(item.DownloadUrl, tempPath, progress, ct);
                long len = File.Exists(tempPath) ? new FileInfo(tempPath).Length : -1;
                if (len < 0)
                    throw new InvalidOperationException("Tải xong nhưng không thấy file — thử lại nhé.");
                if (item.Size > 0 && len != item.Size)
                    throw new InvalidOperationException(
                        $"File tải về sai dung lượng ({len} byte, đúng phải là {item.Size} byte) — đã xoá, hãy thử lại.");
                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(tempPath, finalPath);
                return finalPath;
            }
            catch
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                throw;
            }
            finally
            {
                lock (_lock) { _inFlight.Remove(item.File); }
            }
        }

        public bool DeleteDownloaded(OnlineGifItem item)
        {
            string p = CachedPath(item);
            if (p == null || !File.Exists(p)) return false;
            try { File.Delete(p); return true; } catch { return false; }
        }

        private static async Task HttpDownloadAsync(string url, string tempPath, IProgress<long> progress, CancellationToken ct)
        {
            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromMinutes(5);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TienVuKeyboardStudio/3.8");
                using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    resp.EnsureSuccessStatusCode();
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
                            progress?.Report(total);
                        }
                    }
                }
            }
        }
    }
}
