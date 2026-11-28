using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TienKeyboardStudio3
{
    /// <summary>
    /// Thu vien Online v3.8 — BAN GON: GIF goc KHONG con dong goi kem app nua.
    /// Catalog (ten + link Google Drive cong khai cua chu du an + dung luong)
    /// nhung san trong app; anh thumb PNG nho van dong goi de hien ngay.
    /// GIF chi tai ve khi nguoi dung bam "Tai ve", luu vao
    /// %AppData%/TienKeyboardStudio/OnlineLibrary/ (OnlineCacheStore quan ly
    /// trang thai + kiem tra dung luong). Logic thuan nam o OnlineLibraryCore.cs.
    /// </summary>
    public static class OnlineCatalog
    {
        /// <summary>Thu muc chua thumb PNG dong goi san canh exe (chi anh nho, khong con GIF).</summary>
        public static string BundledDir => Path.Combine(AppContext.BaseDirectory, "OnlineLibrary");

        /// <summary>Thu muc cache GIF da tai (trong %AppData% cua app).</summary>
        public static string CacheDir => Path.Combine(GifLibraryStore.RootDir, "OnlineLibrary");

        private static OnlineCacheStore _store;
        public static OnlineCacheStore Store => _store ??= new OnlineCacheStore(CacheDir);

        /// <summary>
        /// File cache catalog da lam moi tu repo (muc Cap nhat, v3.14) —
        /// nam trong %AppData% cua app; co cache hop le thi uu tien cache
        /// (chua GIF moi), khong thi dung catalog nhung san trong app.
        /// </summary>
        public static string CatalogCachePath => Path.Combine(GifLibraryStore.RootDir, "online-library-drive.json");

        public static List<OnlineGifItem> LoadCatalog()
        {
            try
            {
                if (File.Exists(CatalogCachePath))
                {
                    var cached = OnlineCatalogCore.ParseCatalog(File.ReadAllText(CatalogCachePath));
                    if (cached.Count > 0) return cached;
                }
            }
            catch { }
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream("TienKeyboardStudio32.online-catalog.json"))
                {
                    if (stream == null) return new List<OnlineGifItem>();
                    using (var r = new StreamReader(stream))
                        return OnlineCatalogCore.ParseCatalog(r.ReadToEnd());
                }
            }
            catch { return new List<OnlineGifItem>(); }
        }

        /// <summary>Luu JSON catalog vua tai ve lam cache (muc Cap nhat goi sau khi da kiem hop le).</summary>
        public static void SaveCatalogCache(string json)
        {
            Directory.CreateDirectory(GifLibraryStore.RootDir);
            File.WriteAllText(CatalogCachePath, json);
        }

        /// <summary>Duong dan file GIF trong cache; null neu chua tai.</summary>
        public static string ResolvePath(OnlineGifItem item)
            => Store.IsDownloaded(item) ? Store.CachedPath(item) : null;

        /// <summary>Anh thumb dong goi san (PNG nho canh exe) — doc rat nhe, khong giai ma GIF.</summary>
        public static async Task<Bitmap> LoadThumbnailAsync(OnlineGifItem item)
        {
            string name = OnlineCatalogCore.ThumbFileName(item);
            if (name == null) return null;
            string thumb = Path.Combine(BundledDir, "thumbs", name);
            if (!File.Exists(thumb)) return null;
            return await Task.Run(() =>
            {
                using (var img = Image.Load<Rgba32>(thumb))
                using (var frame = img.Frames.CloneFrame(0))
                {
                    if (frame.Width > 320)
                        frame.Mutate(m => m.Resize(320, (int)(frame.Height * 320.0 / frame.Width)));
                    return BitmapConvert.ToBitmap(frame);
                }
            });
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
