using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TienKeyboardStudio3
{
    /// <summary>
    /// Cache anh thu nho PNG cho luoi GIF (v3.8 — sua RAM): luoi chi doc file
    /// PNG nho da cache, KHONG giai ma GIF day du de hien thi. Lan dau gap mot
    /// GIF, app tao thumb (frame 0, thu nho) va luu vao
    /// %AppData%/TienKeyboardStudio/ThumbCache/ voi khoa = hash(duong dan +
    /// kich thuoc + thoi gian sua file) — GIF thay doi thi khoa doi theo.
    /// Giai ma day du chi xay ra khi nguoi dung mo/phat GIF that.
    /// </summary>
    public static class GifThumbCache
    {
        public static string CacheKey(string gifPath)
        {
            var fi = new FileInfo(gifPath);
            string seed = gifPath + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
                return BitConverter.ToString(h).Replace("-", "").Substring(0, 24).ToLowerInvariant();
            }
        }

        public static string ThumbPathFor(string cacheDir, string gifPath)
            => Path.Combine(cacheDir, CacheKey(gifPath) + ".png");

        /// <summary>
        /// Tra ve duong dan PNG thumb: co san thi tra ngay (chi doc file nho),
        /// chua co thi tao tu frame 0 cua GIF roi luu cache. Loi -> null.
        /// </summary>
        public static string EnsureThumbnail(string gifPath, string cacheDir, int maxDim = 96)
        {
            if (string.IsNullOrEmpty(gifPath) || !File.Exists(gifPath)) return null;
            string thumb = ThumbPathFor(cacheDir, gifPath);
            if (File.Exists(thumb)) return thumb;
            try
            {
                Directory.CreateDirectory(cacheDir);
                using (var img = Image.Load<Rgba32>(gifPath))
                using (var frame = img.Frames.CloneFrame(0))
                {
                    if (frame.Width > maxDim || frame.Height > maxDim)
                    {
                        double s = (double)maxDim / Math.Max(frame.Width, frame.Height);
                        frame.Mutate(m => m.Resize(
                            Math.Max(1, (int)Math.Round(frame.Width * s)),
                            Math.Max(1, (int)Math.Round(frame.Height * s))));
                    }
                    frame.SaveAsPng(thumb);
                }
                return thumb;
            }
            catch { return null; }
        }
    }
}
