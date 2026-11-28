using System;
using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.3 — Trang thai cua Cho plugin + GameSense, luu chung vao
    // settings.json (xem LibrarySettings trong GifLibrary.cs).
    // File nay THUAN LOGIC (khong UI/HID) de unit test duoc.
    // =====================================================================

    /// <summary>Trang thai mot plugin trong Cho plugin: bat/tat + cau hinh rieng.</summary>
    public sealed class PluginState
    {
        public bool Enabled { get; set; }
        public Dictionary<string, string> Config { get; set; } = new Dictionary<string, string>();

        public string GetCfg(string key, string fallback)
            => Config != null && Config.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;

        public bool GetCfgBool(string key, bool fallback)
            => Config != null && Config.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

        public int GetCfgInt(string key, int fallback)
            => Config != null && Config.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : fallback;
    }

    /// <summary>Cai dat GameSense (CS2 GSI) — luu trong settings.json.</summary>
    public sealed class GameSenseSettings
    {
        public bool Listening { get; set; } = false;
        public int Port { get; set; } = 13650;

        public bool DeathEnabled { get; set; } = true;
        public int DeathAction { get; set; } = 0; // 0 mo web, 1 chay chuong trinh, 2 nhay den phim
        public string DeathUrl { get; set; } = "https://www.youtube.com";
        public string DeathProgram { get; set; } = "";

        public bool FlashEnabled { get; set; } = true;
        public int FlashAction { get; set; } = 0; // 0 overlay chong loa, 1 lam toi den, 2 khong lam gi
        public string FlashImage { get; set; } = "black"; // black / gray / custom:<file>
        public double FlashOpacity { get; set; } = 0.85;

        public bool WinEnabled { get; set; } = true;
        public int WinAction { get; set; } = 0; // 0 den an mung (stream), 1 mo nhac (web), 2 khong lam gi
        public string WinUrl { get; set; } = "";

        public bool BombEnabled { get; set; } = false;
        public int BombAction { get; set; } = 0; // 0 nhay den phim, 1 khong lam gi

        // Phan ung den phim qua stream 0x20 (ExperimentalDevice) — chi la
        // cong tac chon tuy chon; viec gui dien ra truc tiep nhu moi lenh
        // 0x20 khac (v3.14: khong con cong tac thu nghiem / Dry-run).
        public bool LightingEnabled { get; set; } = false;
    }

    /// <summary>Bang mau chu de (4 mau nhu ban tham khao) — dang RGB thuan.</summary>
    public static class AccentTable
    {
        public sealed class Accent
        {
            public string Name;
            public (byte R, byte G, byte B) A;
            public (byte R, byte G, byte B) B;
        }

        public static readonly Accent[] All =
        {
            new Accent { Name = "Tím",   A = (0x7C, 0x3A, 0xED), B = (0x22, 0xD3, 0xEE) },
            new Accent { Name = "Dương", A = (0x25, 0x63, 0xEB), B = (0x38, 0xBD, 0xF8) },
            new Accent { Name = "Ngọc",  A = (0x05, 0x96, 0x69), B = (0x34, 0xD3, 0x99) },
            new Accent { Name = "Hồng",  A = (0xDB, 0x27, 0x77), B = (0xFB, 0x71, 0x85) },
        };

        public static Accent Get(int index)
            => index >= 0 && index < All.Length ? All[index] : All[0];
    }

    /// <summary>Toan hoc nen app: tinh kich thuoc dich khi nen anh lon.</summary>
    public static class AppearanceMath
    {
        /// <summary>
        /// Giu nguyen ti le, thu nho sao cho canh dai nhat <= maxDim.
        /// Anh nho hon thi giu nguyen (khong phong to).
        /// </summary>
        public static (int W, int H) ComputeTargetSize(int width, int height, int maxDim)
        {
            if (width <= 0 || height <= 0 || maxDim <= 0) return (Math.Max(1, width), Math.Max(1, height));
            int longest = Math.Max(width, height);
            if (longest <= maxDim) return (width, height);
            double scale = (double)maxDim / longest;
            return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
        }
    }

    /// <summary>
    /// Anh xa plugin khoi dong -> phong cach man hinh chao.
    /// Quy tac (theo brief): plugin boot dang BAT trong Cho plugin se quyet
    /// dinh man chao khoi dong. Uu tien plugin nguoi dung chon gan nhat
    /// (preferred); neu plugin do dang tat thi roi ve Tien Galaxy Boot neu
    /// no dang bat; khong plugin boot nao bat -> "none" (chi logo, khong
    /// hieu ung).
    /// </summary>
    public static class GreetingStyles
    {
        public const string Galaxy = "galaxy";
        public const string Terminal = "terminal";
        public const string Amber550 = "550c";
        public const string Moss = "moss";
        public const string Particles = "particles";
        public const string Custom = "custom"; // v3.11: plugin tu nhap (id dang "custom:<manifest-id>")
        public const string None = "none";

        /// <summary>Tien to id trang thai cua plugin tu nhap trong settings.</summary>
        public const string CustomPrefix = "custom:";

        public static bool IsCustomId(string pluginId)
            => !string.IsNullOrEmpty(pluginId) && pluginId.StartsWith(CustomPrefix, StringComparison.Ordinal);

        public static string ManifestIdOf(string pluginId)
            => IsCustomId(pluginId) ? pluginId.Substring(CustomPrefix.Length) : pluginId;

        public const string PluginGalaxy = "tien-galaxy-boot";
        public const string PluginDeepSeek = "deepseek-harness";
        public const string Plugin550C = "550c-boot";
        public const string PluginMoss = "moss-ignition";
        public const string PluginParticles = "hat-bui-tv";

        public static string StyleForPlugin(string pluginId)
        {
            if (IsCustomId(pluginId)) return Custom;
            switch (pluginId)
            {
                case PluginDeepSeek: return Terminal;
                case Plugin550C: return Amber550;
                case PluginMoss: return Moss;
                case PluginParticles: return Particles;
                case PluginGalaxy: return Galaxy;
                default: return None;
            }
        }

        public static string Resolve(IEnumerable<string> enabledBootPluginIds, string preferredPluginId)
        {
            var enabled = new HashSet<string>(enabledBootPluginIds ?? Array.Empty<string>());
            if (!string.IsNullOrEmpty(preferredPluginId) && enabled.Contains(preferredPluginId))
            {
                string s = StyleForPlugin(preferredPluginId);
                if (s != None) return s;
            }
            if (enabled.Contains(PluginGalaxy)) return Galaxy;
            // Bat ky plugin boot nao dang bat (deepseek/550c) deu dung duoc
            foreach (var id in enabled)
            {
                string s = StyleForPlugin(id);
                if (s != None) return s;
            }
            return None;
        }
    }
}
