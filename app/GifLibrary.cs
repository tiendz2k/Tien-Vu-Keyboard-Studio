using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TienKeyboardStudio3
{
    /// <summary>
    /// Cai dat thu vien GIF ca nhan cua Tien:
    ///  - Thu muc goc: %AppData%/TienKeyboardStudio/
    ///  - settings.json: danh sach thu muc nguon nguoi dung them vao.
    ///  - MyGIFs/: noi luu GIF nguoi dung tai tu URL / chuan hoa vao.
    /// Chi doc/ghi file local. Mang chi dung trong DownloadImageAsync -
    /// hanh dong tai anh do CHINH nguoi dung bam nut (khong telemetry).
    /// </summary>
    public sealed class LibrarySettings
    {
        public List<string> Folders { get; set; } = new List<string>();

        // --- v3.3: Giao dien + man hinh chao + plugin + GameSense ---
        // Moi truong deu co mac dinh an toan de file settings.json cu (v3.2)
        // nap len khong loi va giu nguyen hanh vi cu.
        public string Background { get; set; } = "default";   // default/aurora/sunset/forest/space/custom
        public string CustomBackgroundFile { get; set; } = ""; // ten file trong thu muc Backgrounds/
        public int AccentIndex { get; set; } = 0;              // 0..3 (bang mau trong AppSettings.cs)
        public bool SkipGreeting { get; set; } = false;
        public string GreetingPlugin { get; set; } = "tien-galaxy-boot"; // plugin boot dang chon lam man chao
        public Dictionary<string, PluginState> Plugins { get; set; } = new Dictionary<string, PluginState>();
        public GameSenseSettings GameSense { get; set; } = new GameSenseSettings();

        // --- v3.9.1: an/hien nhat ky thao tac. MAC DINH AN (false) — file
        // settings.json cu khong co truong nay se nap thanh false, dung y
        // nguoi dung: panel nhat ky khong con hien san tren giao dien.
        // Day CHI la thay doi hien thi: bo dem nhat ky trong app van thu
        // thap lien tuc, bat cong tac nay len la thay lich su gan day.
        public bool ShowOperationLog { get; set; } = false;

        // --- v3.11: chu de kieu AULA (o mau + do mo kinh + nen dong) va
        // thoi tiet theo vi tri. Mac dinh an toan cho settings.json cu:
        // ThemeTile rong -> ResolveId anh xa tu AccentIndex (giu dien mao
        // cu sau nang cap); WeatherMode "auto" la mac dinh moi theo y
        // nguoi dung (tu dong theo mang, chon tay la ghi de). ---
        public string ThemeTile { get; set; } = "";            // cuc-quang/may-nhat/la-phong/rung-sau/nguyen-ban
        public double GlassBlur { get; set; } = 7;             // 4..10 (thang AULA)
        public string LiveWallpaperFile { get; set; } = "";    // ten file GIF trong Backgrounds/ khi Background == "live"
        public bool LiveWallpaperPaused { get; set; } = false;
        public string WeatherMode { get; set; } = "auto";      // auto | manual
        public string WeatherCity { get; set; } = "";          // che do chon tay (rong = Ha Noi)
        public string GeoCity { get; set; } = "";              // cache do IP (tu dong theo mang)
        public string GeoCountry { get; set; } = "";
        public double GeoLat { get; set; }
        public double GeoLon { get; set; }
        public string GeoFetchedUtc { get; set; } = "";        // ISO 8601, cache 24h

        // --- v3.12: trang thai anh sang day du (hieu ung firmware + per-key
        // + nhac 0x78 + thoi gian thuc + GIF->LED) va mau gan day cua bo
        // chon mau. settings.json cu khong co -> null/empty, MainWindow
        // dung mac dinh trong LightingStateData. ---
        public LightingStateData Lighting { get; set; }
        public List<string> RecentColors { get; set; } = new List<string>();

        // --- v3.14: trang thai muc Cap nhat (panel Cai dat). settings.json
        // cu khong co cac truong nay -> mac dinh rong/-1, nap binh thuong.
        // (Ban nay khong con truong DryRun/Experimental nao trong settings;
        // file cu neu con thua hai truong do thi trinh doc JSON bo qua.) ---
        public string UpdateLastCheckUtc { get; set; } = "";   // ISO 8601 UTC
        public string UpdateLatestVersion { get; set; } = "";  // phien ban moi nhat da biet tu manifest
        public string UpdateLatestNotes { get; set; } = "";
        public string UpdateLightUrl { get; set; } = "";
        public string UpdateSha256 { get; set; } = "";
        public string RegistryLastCheckUtc { get; set; } = "";
        public string GifCatalogLastCheckUtc { get; set; } = "";
        public int GifCatalogCount { get; set; } = -1;         // -1 = chua tung lam moi
    }

    /// <summary>Thu muc luu anh nen nguoi dung tu chon (v3.3).</summary>
    public static class BackgroundStore
    {
        public static string BackgroundsDir => Path.Combine(GifLibraryStore.RootDir, "Backgrounds");

        /// <summary>Duong dan day du cua file nen custom da luu (null neu chua co).</summary>
        public static string ResolveCustomPath(LibrarySettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(settings.CustomBackgroundFile)) return null;
            string p = Path.Combine(BackgroundsDir, settings.CustomBackgroundFile);
            return File.Exists(p) ? p : null;
        }
    }

    public static class GifLibraryStore
    {
        public static string RootDir
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "TienKeyboardStudio");
            }
        }

        public static string MyGifsDir => Path.Combine(RootDir, "MyGIFs");
        public static string SettingsPath => Path.Combine(RootDir, "settings.json");

        public static LibrarySettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var s = JsonSerializer.Deserialize<LibrarySettings>(File.ReadAllText(SettingsPath));
                    if (s != null)
                    {
                        if (s.Folders == null) s.Folders = new List<string>();
                        if (s.RecentColors == null) s.RecentColors = new List<string>();
                        if (s.Plugins == null) s.Plugins = new Dictionary<string, PluginState>();
                        if (s.GameSense == null) s.GameSense = new GameSenseSettings();
                        if (s.Lighting != null && s.Lighting.ScheduleRules == null)
                            s.Lighting.ScheduleRules = new List<ScheduleRuleData>();
                        if (string.IsNullOrEmpty(s.Background)) s.Background = "default";
                        if (string.IsNullOrEmpty(s.GreetingPlugin)) s.GreetingPlugin = "tien-galaxy-boot";
                        return s;
                    }
                }
            }
            catch { }
            return new LibrarySettings();
        }

        public static void Save(LibrarySettings settings)
        {
            Directory.CreateDirectory(RootDir);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static void EnsureDirs()
        {
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(MyGifsDir);
        }

        private static readonly string[] ImageExts = { ".gif", ".png", ".jpg", ".jpeg", ".bmp", ".webp" };

        /// <summary>Quet mot thu muc (khong de quy sau) lay file anh/GIF.</summary>
        public static List<string> ScanFolder(string folder)
        {
            var result = new List<string>();
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return result;
                foreach (var f in Directory.GetFiles(folder))
                {
                    if (Array.IndexOf(ImageExts, Path.GetExtension(f).ToLowerInvariant()) >= 0)
                        result.Add(f);
                }
            }
            catch { }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>
        /// Tai anh tu URL do nguoi dung dan vao. Day la loi goi mang DUY NHAT
        /// cua app, chi chay khi nguoi dung bam nut "Tai tu link".
        /// Kiem tra: HTTP thanh cong, dung luong <= 25MB, giai ma duoc bang
        /// ImageSharp (tuc la anh that). Tra ve duong dan file tam da tai.
        /// </summary>
        public static async Task<string> DownloadImageAsync(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("Link khong hop le - chi chap nhan http/https.");

            EnsureDirs();
            string tempPath = Path.Combine(RootDir, "download-tmp.bin");
            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromSeconds(60);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TienKeyboardStudio/3.1");
                using (var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
                {
                    resp.EnsureSuccessStatusCode();
                    if (resp.Content.Headers.ContentLength > 25 * 1024 * 1024)
                        throw new InvalidOperationException("File qua lon (>25MB), khong tai.");
                    using (var fs = File.Create(tempPath))
                        await resp.Content.CopyToAsync(fs);
                }
            }
            // Xac thuc la anh that bang cach giai ma thu
            try
            {
                using (var img = Image.Load<Rgba32>(tempPath)) { var _ = img.Width; }
            }
            catch
            {
                try { File.Delete(tempPath); } catch { }
                throw new InvalidOperationException("File tai ve khong phai anh/GIF hop le.");
            }
            return tempPath;
        }

        /// <summary>
        /// Chuan hoa anh/GIF ve chuan man hinh F75 Max: 128x128, toi da 255 frame
        /// (cat bot), giu delay tung frame (kep 30..500 ms nhu encoder goc).
        /// Luu thanh GIF vao MyGIFs, tra ve (duong dan, thong tin truoc/sau).
        /// </summary>
        public static (string OutPath, string Before, string After) NormalizeToMyGifs(
            string sourcePath, FitMode fit, string desiredName)
        {
            EnsureDirs();
            using (var src = Image.Load<Rgba32>(sourcePath))
            {
                int srcFrames = src.Frames.Count;
                int frames = Math.Min(srcFrames, F75Protocol.MaxFrames);
                string before = src.Width + "×" + src.Height + ", " + srcFrames + " frame";

                var mode = fit == FitMode.Fill ? ResizeMode.Crop
                         : fit == FitMode.Fit ? ResizeMode.Pad
                         : ResizeMode.Stretch;

                Image<Rgba32> first = ConvertFrame(src, 0, mode);
                first.Metadata.GetGifMetadata().RepeatCount = 0;
                var output = first;
                try
                {
                    for (int i = 1; i < frames; i++)
                    {
                        using (var f = ConvertFrame(src, i, mode))
                        {
                            var root = f.Frames.RootFrame;
                            root.Metadata.GetGifMetadata().FrameDelay = (ushort)DelayHundredths(src, i);
                            output.Frames.AddFrame(root);
                        }
                    }
                    output.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = (ushort)DelayHundredths(src, 0);

                    string name = SanitizeName(desiredName);
                    if (string.IsNullOrEmpty(name))
                        name = SanitizeName(Path.GetFileNameWithoutExtension(sourcePath));
                    if (string.IsNullOrEmpty(name)) name = "gif-cua-tien";
                    string outPath = Path.Combine(MyGifsDir, name + ".gif");
                    int n = 2;
                    while (File.Exists(outPath))
                        outPath = Path.Combine(MyGifsDir, name + "-" + (n++) + ".gif");

                    output.SaveAsGif(outPath, new GifEncoder { ColorTableMode = GifColorTableMode.Global });
                    string after = "128×128, " + frames + " frame" +
                                   (srcFrames > frames ? " (da cat tu " + srcFrames + ")" : "");
                    return (outPath, before, after);
                }
                finally
                {
                    output.Dispose();
                }
            }
        }

        private static Image<Rgba32> ConvertFrame(Image<Rgba32> src, int index, ResizeMode mode)
        {
            var frame = src.Frames.CloneFrame(index);
            frame.Mutate(m => m.Resize(new ResizeOptions
            {
                Size = new Size(F75Protocol.ScreenWidth, F75Protocol.ScreenHeight),
                Mode = mode,
                PadColor = Color.Black,
                Sampler = KnownResamplers.Bicubic,
            }));
            return frame;
        }

        private static int DelayHundredths(Image<Rgba32> img, int index)
        {
            int ms = 100;
            try
            {
                var meta = img.Frames[index].Metadata.GetGifMetadata();
                if (meta != null && meta.FrameDelay > 0) ms = meta.FrameDelay * 10;
            }
            catch { }
            ms = Math.Min(500, Math.Max(30, ms));
            return ms / 10;
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '-');
            name = name.Trim().Trim('.');
            if (name.Length > 60) name = name.Substring(0, 60);
            return name;
        }
    }
}
