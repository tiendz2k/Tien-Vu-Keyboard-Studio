using System;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.11 — Nen dong (live wallpaper) tu VIDEO NGAN: nguoi dung chon
    // .mp4/.gif; MP4 duoc chuyen bang ffmpeg (tai khi can, cung co che voi
    // tinh nang TikTok) lay toi da 12 giay dau -> GIF vong lap 15 fps,
    // rong toi da 1280px, luu trong thu muc Backgrounds. GIF nho duoc
    // dung thang. File nay THUAN LOGIC (dinh tuyen + dung lenh + gioi
    // han) de unit test duoc; phan phat khung hinh nam o MainWindow.
    // =====================================================================

    public enum LiveWallpaperRoute
    {
        UseGifDirect,       // GIF du nho: dung thang, khong can ffmpeg
        ConvertWithFfmpeg,  // MP4 (hoac GIF qua lon): cat + chuyen bang ffmpeg
        Reject,             // dinh dang khong ho tro
    }

    public static class LiveWallpaper
    {
        public const int MaxSeconds = 12;
        public const int Fps = 15;
        public const int MaxWidth = 1280;
        public const long GifDirectMaxBytes = 20L * 1024 * 1024;

        private static readonly string[] VideoExts = { ".mp4", ".mov", ".webm", ".mkv", ".avi" };

        /// <summary>Chon duong xu ly theo dinh dang + dung luong file nguon.</summary>
        public static LiveWallpaperRoute DecideRoute(string extension, long sizeBytes)
        {
            string ext = (extension ?? "").ToLowerInvariant();
            if (ext == ".gif")
                return sizeBytes > 0 && sizeBytes <= GifDirectMaxBytes
                    ? LiveWallpaperRoute.UseGifDirect
                    : LiveWallpaperRoute.ConvertWithFfmpeg;
            foreach (var v in VideoExts)
                if (ext == v) return LiveWallpaperRoute.ConvertWithFfmpeg;
            return LiveWallpaperRoute.Reject;
        }

        /// <summary>Gioi han thoi luong doan lap: kep ve 1..12 giay.</summary>
        public static int ClampSeconds(double seconds)
        {
            int s = (int)Math.Round(seconds);
            return Math.Max(1, Math.Min(MaxSeconds, s));
        }

        /// <summary>
        /// Dong tham so ffmpeg: -t kep thoi luong, fps=15, scale khong vuot
        /// 1280px chieu rong (giu nguyen neu nho hon), palette 2 luot trong
        /// mot lenh nhu FfmpegTools.BuildConvertArguments, lap vo han.
        /// </summary>
        public static string BuildConvertArguments(string inputPath, string outputPath, double seconds = MaxSeconds)
        {
            int sec = ClampSeconds(seconds);
            string vf = "fps=" + Fps + ",scale=w=min(" + MaxWidth + "\\,iw):h=-1:flags=lanczos,"
                      + "split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse";
            return "-y -t " + sec + " -i \"" + inputPath + "\" -vf \"" + vf + "\" -loop 0 \"" + outputPath + "\"";
        }
    }
}
