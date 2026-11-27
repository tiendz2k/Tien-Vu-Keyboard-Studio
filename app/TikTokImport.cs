using System;
using System.IO;
using System.Text.Json;

namespace TienKeyboardStudio3
{
    /// <summary>Ket qua phan giai link TikTok qua API tikwm (khong chinh thuc).</summary>
    public sealed class TikTokResolveResult
    {
        public string PlayUrl { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public long Size { get; set; }
    }

    /// <summary>
    /// Phan giai link TikTok — THUAN LOGIC (chi parse JSON), unit test duoc.
    /// LUU Y TRUNG THUC: TikTok khong co kenh tai chinh thuc cho ben thu ba;
    /// app dung dich vu phan giai cong khai tikwm.com (best-effort) — mot so
    /// video se khong lay duoc, khi do app bao loi ro rang thay vi treo.
    /// </summary>
    public static class TikTokResolver
    {
        public const string ApiBase = "https://www.tikwm.com/api/?url=";

        public static string BuildApiUrl(string videoUrl) => ApiBase + Uri.EscapeDataString(videoUrl ?? "");

        public static bool LooksLikeTikTokUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
            return u.Host.EndsWith("tiktok.com", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Parse JSON tra ve cua tikwm: { code:0, data:{ play, title, author:{nickname}, size } }.</summary>
        public static TikTokResolveResult ParseResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number && code.GetInt32() != 0)
                        return null;
                    if (!root.TryGetProperty("data", out var data)) return null;
                    string play = GetStr(data, "play");
                    if (string.IsNullOrEmpty(play)) return null;
                    string author = null;
                    if (data.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object)
                        author = GetStr(a, "nickname");
                    long size = 0;
                    if (data.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number)
                        s.TryGetInt64(out size);
                    return new TikTokResolveResult
                    {
                        PlayUrl = play,
                        Title = GetStr(data, "title"),
                        Author = author,
                        Size = size,
                    };
                }
            }
            catch { return null; }
        }

        private static string GetStr(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    public enum GifTarget { LedStrip, Tft }

    /// <summary>
    /// Cong cu ffmpeg: KHONG dong goi kem app (giu ban cai nhe). Thu tu tim:
    /// 1) thu muc tools/ canh exe, 2) %AppData%/TienKeyboardStudio/Tools,
    /// 3) PATH. Thieu thi nguoi dung bam tai ban essentials cua gyan.dev
    /// vao Tools (hanh dong chu dong, co ghi ro dung luong).
    /// Logic tim kiem + dung tham so la thuan, unit test duoc.
    /// </summary>
    public static class FfmpegTools
    {
        public const string EssentialsUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        public const string EssentialsNote = "Tải công cụ chuyển đổi (ffmpeg essentials, ~30 MB)";

        /// <summary>Tra ve duong dan ffmpeg.exe dau tien tim thay theo thu tu uu tien, hoac null.</summary>
        public static string Locate(string appDir, string toolsDir, string pathEnv, Func<string, bool> exists = null)
        {
            exists = exists ?? File.Exists;
            string exe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            if (!string.IsNullOrEmpty(appDir))
            {
                string p = Path.Combine(appDir, "tools", exe);
                if (exists(p)) return p;
            }
            if (!string.IsNullOrEmpty(toolsDir))
            {
                string p = Path.Combine(toolsDir, exe);
                if (exists(p)) return p;
            }
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    string p = Path.Combine(dir.Trim(), exe);
                    if (exists(p)) return p;
                }
            }
            return null;
        }

        /// <summary>
        /// Dung dong tham so ffmpeg: toi da 12 giay dau, 15 fps, palette 2 luot
        /// trong mot lenh (palettegen + paletteuse). LedStrip: rong 900px
        /// (dung cho GIF->LED). Tft: 128x128 (crop giua + scale).
        /// </summary>
        public static string BuildConvertArguments(string inputPath, string outputPath, GifTarget target)
        {
            string vf = target == GifTarget.Tft
                ? "fps=15,scale=128:128:force_original_aspect_ratio=increase,crop=128:128,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse"
                : "fps=15,scale=900:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse";
            return "-y -t 12 -i \"" + inputPath + "\" -vf \"" + vf + "\" -loop 0 \"" + outputPath + "\"";
        }
    }
}
