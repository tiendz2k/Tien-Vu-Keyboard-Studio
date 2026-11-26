using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TienKeyboardStudio3
{
    /// <summary>Mot muc trong catalog Thu vien Online (ten + ten file GIF dong goi san).</summary>
    public sealed class OnlineGifItem
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public string File { get; set; }
    }

    /// <summary>
    /// Thu vien Online DONG GOI SAN 100% (v3.2.2): 16 GIF cong dong dang dai LED
    /// rong nam san trong thu muc "OnlineLibrary" canh file exe; catalog nhung
    /// trong app chi chua ten + ten file local. Trang nay KHONG tai mang, khong
    /// chua bat ky URL ben thu ba nao — anh thumb va GIF goc deu doc thang tu dia.
    /// (Tinh nang "dan URL de tai GIF cua rieng ban" o trang Man hinh GIF la
    /// hanh dong chu dong cua nguoi dung — giu nguyen, nam o GifLibrary.)
    /// </summary>
    public static class OnlineCatalog
    {
        public static string OnlineLibraryDir => Path.Combine(AppContext.BaseDirectory, "OnlineLibrary");

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

        /// <summary>Duong dan file GIF local cua muc; null neu thieu file (nguoi dung da xoa thu muc OnlineLibrary).</summary>
        public static string ResolvePath(OnlineGifItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.File)) return null;
            string path = Path.Combine(OnlineLibraryDir, item.File);
            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// Anh thumb cua muc: UU TIEN file PNG dong goi san trong OnlineLibrary/thumbs
        /// (frame dai dien chon san luc dong goi, rong 320px) — doc rat nhe. Chi khi
        /// thieu file PNG moi giai ma frame dau cua GIF (du phong; GIF dai LED co the
        /// rat nhieu frame nen tranh dung duong nay hang loat).
        /// </summary>
        public static async Task<Bitmap> LoadThumbnailAsync(OnlineGifItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.File)) return null;
            string thumb = Path.Combine(OnlineLibraryDir, "thumbs",
                Path.GetFileNameWithoutExtension(item.File) + ".png");
            string path = File.Exists(thumb) ? thumb : ResolvePath(item);
            if (path == null) return null;
            return await Task.Run(() =>
            {
                using (var img = Image.Load<Rgba32>(path))
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
