using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TienKeyboardStudio3
{
    /// <summary>Mot muc trong catalog thu vien GIF online (chi ten + URL cong khai).</summary>
    public sealed class OnlineGifItem
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public string Gif { get; set; }
        public string Thumb { get; set; }
    }

    /// <summary>
    /// Thu vien GIF online kieu hoangphuckeyboard.shop/thu-vien-gif:
    /// catalog 16 GIF cong dong duoc NHUNG san trong app (JSON chi chua ten va
    /// URL cong khai tren CDN cua shop - KHONG dong goi file GIF). Anh thumb
    /// (webp) va GIF goc chi tai ve KHI nguoi dung duyet/bam chon, toi da 25MB,
    /// xac thuc giai ma duoc bang ImageSharp. Khong telemetry, khong endpoint nao khac.
    /// </summary>
    public static class OnlineCatalog
    {
        public const int MaxBytes = 25 * 1024 * 1024;

        private static readonly HttpClient Http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var h = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("TienKeyboardStudio/3.2");
            return h;
        }

        public static string CacheDir => Path.Combine(GifLibraryStore.RootDir, "OnlineCache");

        public static List<OnlineGifItem> LoadCatalog()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream("TienKeyboardStudio32.online-catalog.json"))
                {
                    if (stream == null) return new List<OnlineGifItem>();
                    var items = JsonSerializer.Deserialize<List<OnlineGifItem>>(stream,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return items ?? new List<OnlineGifItem>();
                }
            }
            catch { return new List<OnlineGifItem>(); }
        }

        private static async Task<byte[]> DownloadBytesAsync(string url, int maxBytes)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("Link khong hop le - chi chap nhan http/https.");
            using (var resp = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                if (resp.Content.Headers.ContentLength > maxBytes)
                    throw new InvalidOperationException("File qua lon (>25MB).");
                using (var ms = new MemoryStream())
                {
                    var buffer = new byte[81920];
                    using (var s = await resp.Content.ReadAsStreamAsync())
                    {
                        int read;
                        while ((read = await s.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            if (ms.Length + read > maxBytes)
                                throw new InvalidOperationException("File qua lon (>25MB).");
                            ms.Write(buffer, 0, read);
                        }
                    }
                    return ms.ToArray();
                }
            }
        }

        /// <summary>Tai anh thumb webp va giai ma thanh Bitmap (thu nho toi da 320px rong).</summary>
        public static async Task<Bitmap> LoadThumbnailAsync(OnlineGifItem item)
        {
            var bytes = await DownloadBytesAsync(item.Thumb, 8 * 1024 * 1024);
            return await Task.Run(() =>
            {
                using (var img = Image.Load<Rgba32>(bytes))
                {
                    var frame = img.Frames.CloneFrame(0);
                    if (frame.Width > 320)
                        frame.Mutate(m => m.Resize(320, (int)(frame.Height * 320.0 / frame.Width)));
                    return BitmapConvert.ToBitmap(frame);
                }
            });
        }

        /// <summary>
        /// Tai GIF goc ve cache tam (theo tung muc), xac thuc giai ma duoc.
        /// Tra ve duong dan file tam. Goi lai lan sau se dung ban cache.
        /// </summary>
        public static async Task<string> DownloadOriginalAsync(OnlineGifItem item)
        {
            Directory.CreateDirectory(CacheDir);
            string key = StableKey(item.Gif);
            string path = Path.Combine(CacheDir, key + ".gif");
            if (File.Exists(path) && new FileInfo(path).Length > 0) return path;

            var bytes = await DownloadBytesAsync(item.Gif, MaxBytes);
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            try
            {
                using (var img = Image.Load<Rgba32>(tmp)) { var _ = img.Width; }
            }
            catch
            {
                try { File.Delete(tmp); } catch { }
                throw new InvalidOperationException("File tai ve khong phai anh/GIF hop le.");
            }
            File.Move(tmp, path, true);
            return path;
        }

        private static string StableKey(string url)
        {
            // dung doan GUID trong URL lam ten file cache (on dinh, khong trung)
            var parts = url.Split('/');
            foreach (var p in parts)
                if (p.Length == 36 && p.Contains("-")) return p;
            unchecked
            {
                int h = 17;
                foreach (char c in url) h = h * 31 + c;
                return "gif-" + (h & 0x7fffffff).ToString("x8");
            }
        }

        /// <summary>
        /// Tao frame xem truoc 128x128 DUNG nhu ket qua sau chuan hoa (Fill/Fit/Stretch),
        /// toi da 40 frame dau de phat trong man hinh TFT cua ban phim ve.
        /// </summary>
        public static List<Bitmap> LoadPreviewFrames(string path, FitMode fit, int maxFrames = 40)
        {
            var list = new List<Bitmap>();
            var mode = fit == FitMode.Fill ? ResizeMode.Crop
                     : fit == FitMode.Fit ? ResizeMode.Pad
                     : ResizeMode.Stretch;
            using (var img = Image.Load<Rgba32>(path))
            {
                int n = Math.Min(img.Frames.Count, maxFrames);
                for (int i = 0; i < n; i++)
                {
                    using (var f = img.Frames.CloneFrame(i))
                    {
                        f.Mutate(m => m.Resize(new ResizeOptions
                        {
                            Size = new Size(F75Protocol.ScreenWidth, F75Protocol.ScreenHeight),
                            Mode = mode,
                            PadColor = Color.Black,
                            Sampler = KnownResamplers.Bicubic,
                        }));
                        list.Add(BitmapConvert.ToBitmap(f));
                    }
                }
            }
            return list;
        }
    }

    /// <summary>Chuyen frame ImageSharp (128x128 hoac nho hon) sang Avalonia Bitmap BGRA.</summary>
    public static class BitmapConvert
    {
        public static Bitmap ToBitmap(Image<Rgba32> frame)
        {
            int w = frame.Width, h = frame.Height;
            var wb = new WriteableBitmap(new Avalonia.PixelSize(w, h), new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque);
            using (var fb = wb.Lock())
            {
                int stride = fb.RowBytes;
                var buffer = new byte[stride * h];
                frame.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            var p = row[x];
                            int o = y * stride + x * 4;
                            buffer[o] = p.B; buffer[o + 1] = p.G; buffer[o + 2] = p.R; buffer[o + 3] = 255;
                        }
                    }
                });
                System.Runtime.InteropServices.Marshal.Copy(buffer, 0, fb.Address, buffer.Length);
            }
            return wb;
        }
    }
}
