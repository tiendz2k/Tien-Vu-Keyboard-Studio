using System;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.11 — Chu de giao dien kieu AULA Hub (port tu Preview v0.11.3 da
    // duyet): 5 o "Mau chu de" lon. File nay THUAN LOGIC (khong UI) de
    // unit test duoc: bang o, anh xa tuong thich nguoc tu AccentIndex cu
    // (v3.3–v3.10) va toan hoc lop phu theo do mo kinh.
    // =====================================================================

    /// <summary>Mot o mau chu de trong muc "Giao dien chu de".</summary>
    public sealed class ThemeTileDef
    {
        public string Id;
        public string Name;
        public string Sub;                 // phu de duoi ten (chi "Nguyen ban" co)
        public (byte R, byte G, byte B) A; // dau gradient
        public (byte R, byte G, byte B) B; // cuoi gradient
        public bool Legacy;                // true = chrome gradient ban truoc (v3.9.x)
        public int AccentIndex;            // chi so tuong ung trong AccentTable cu (dong bo settings)
    }

    public static class ThemeTiles
    {
        public const string CucQuang = "cuc-quang";
        public const string MayNhat = "may-nhat";
        public const string LaPhong = "la-phong";
        public const string RungSau = "rung-sau";
        public const string NguyenBan = "nguyen-ban";

        public static readonly ThemeTileDef[] All =
        {
            new ThemeTileDef { Id = CucQuang, Name = "Cực quang", A = (0x7C, 0x3A, 0xED), B = (0x22, 0xD3, 0xEE), AccentIndex = 0 },
            new ThemeTileDef { Id = MayNhat, Name = "Mây nhạt", A = (0x60, 0xA5, 0xFA), B = (0xA5, 0xF3, 0xFC), AccentIndex = 1 },
            new ThemeTileDef { Id = LaPhong, Name = "Lá phong", A = (0xDC, 0x26, 0x26), B = (0xF5, 0x9E, 0x0B), AccentIndex = 3 },
            new ThemeTileDef { Id = RungSau, Name = "Rừng sâu", A = (0x05, 0x96, 0x69), B = (0x34, 0xD3, 0x99), AccentIndex = 2 },
            new ThemeTileDef { Id = NguyenBan, Name = "Nguyên bản", Sub = "Mặc định bản trước", A = (0x7C, 0x3A, 0xED), B = (0x22, 0xD3, 0xEE), Legacy = true, AccentIndex = 0 },
        };

        public static ThemeTileDef Find(string id)
        {
            if (!string.IsNullOrEmpty(id))
                foreach (var t in All)
                    if (t.Id == id) return t;
            return null;
        }

        public static bool IsLegacy(string id) => Find(id)?.Legacy == true;

        /// <summary>
        /// Id o chu de hieu luc tu settings: uu tien ThemeTile da luu (v3.11+);
        /// settings cu (chi co AccentIndex cua v3.3–v3.10) thi anh xa ve o
        /// CUNG MAU trong bo moi (AccentIndex 0 = tim→cyan -> "Cuc quang",
        /// cung cap mau voi "Nguyen ban" nhung la chrome AULA trang — dung
        /// mac dinh cua ban xem truoc da duyet; nguoi thich chrome gradient
        /// cu chi can chon o "Nguyen ban").
        /// </summary>
        public static string ResolveId(LibrarySettings settings)
        {
            var t = Find(settings?.ThemeTile);
            if (t != null) return t.Id;
            int idx = settings?.AccentIndex ?? 0;
            switch (idx)
            {
                case 1: return MayNhat;
                case 2: return RungSau;
                case 3: return LaPhong;
                default: return CucQuang;
            }
        }
    }

    /// <summary>Toan hoc do mo kinh (thang 4–10 nhu AULA Hub).</summary>
    public static class GlassMath
    {
        public const double MinLevel = 4;
        public const double MaxLevel = 10;

        public static double ClampLevel(double level)
            => Math.Max(MinLevel, Math.Min(MaxLevel, level));

        /// <summary>
        /// Alpha hai dau gradient cua lop phu toi tren hinh nen theo muc mo:
        /// muc 4 → (0x33, 0x73) trong va net; muc 10 → (0x59, 0x99) diu hon.
        /// (Cong thuc cua Preview v0.11.3 da duyet.)
        /// </summary>
        public static (byte Top, byte Bottom) ShadeAlphas(double level)
        {
            double t = (ClampLevel(level) - MinLevel) / (MaxLevel - MinLevel);
            byte top = (byte)Math.Round(0x33 + t * (0x59 - 0x33));
            byte bottom = (byte)Math.Round(0x73 + t * (0x99 - 0x73));
            return (top, bottom);
        }
    }
}
