using System;
using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.10 — Theme man hinh chao (Cho plugin): dang ky metadata + logic
    // thuan (builder payload QR, lay mau hat logo, may trang thai trinh tu
    // chao). File nay KHONG dung UI/HID de unit test duoc (xem tests/).
    //
    // Nguon goc cac theme ben thu ba (nghien cuu noi bo
    // plugin-store-research/REPORT.md): he sinh thai DeepSeek Harness (DSH)
    // tren GitHub, phan lon MIT. Vi app la Avalonia thuan (khong webview),
    // moi theme deu la VIET LAI chuyen dong bang Avalonia/Skia — rui ro ban
    // quyen nam o asset (video/anh/giong/ten thuong hieu phim-game-anime),
    // khong o code. Quy tac da ap dung: cau truc/tiet tau hoc tu do; hinh,
    // tieng, chu thuong hieu cua phim/game/anime tu lam lai hoac bo.
    // =====================================================================

    /// <summary>Metadata mot theme khoi dong trong Cho plugin.</summary>
    public sealed class GreetingThemeInfo
    {
        public string PluginId;     // id trong settings.json (Plugins dict)
        public string Style;        // khoa phong cach cua GreetingStyles
        public string Name;         // ten hien thi
        public string Author;       // tac gia hien thi tren the
        public string RepoUrl;      // repo nguon ("" neu la theme cua app)
        public string License;      // "MIT" / "Cua app"
        public bool ThirdParty;     // true = theme ben thu ba, can ghi cong
        public string Attribution;  // dong ghi cong day du (README/NOTICE di kem ban phat hanh)
    }

    public static class GreetingThemeRegistry
    {
        public static readonly GreetingThemeInfo[] All =
        {
            new GreetingThemeInfo
            {
                PluginId = "deepseek-harness", Style = "terminal",
                Name = "DeepSeek Harness — Rhine Lab (bản đầy đủ)",
                Author = "6shenhonghong9",
                RepoUrl = "https://github.com/6shenhonghong9/dsh-startup-screen",
                License = "MIT", ThirdParty = true,
                Attribution = "“DeepSeek Harness — Rhine Lab” (six-act startup) © 6shenhonghong9 — MIT License — https://github.com/6shenhonghong9/dsh-startup-screen . Cấu trúc 6 hồi được tái hiện bằng Avalonia; toàn bộ chữ/thương hiệu fan-work trong bản gốc đã được thay bằng nội dung của app.",
            },
            new GreetingThemeInfo
            {
                PluginId = "550c-boot", Style = "550c",
                Name = "550C Boot + Thẻ ID",
                Author = "yannicksong0106 (Ziyang Song) · intro gốc: Voidpocket",
                RepoUrl = "https://github.com/yannicksong0106/dsh-550c-boot",
                License = "MIT", ThirdParty = true,
                Attribution = "“550C Boot + UEG Login” © Ziyang Song (yannicksong0106) — MIT License — https://github.com/yannicksong0106/dsh-550c-boot ; phần intro 550C gốc của Voidpocket (được ghi credit trong repo). Tái hiện bằng Avalonia; thẻ ID là “TV Access Card” của app.",
            },
            new GreetingThemeInfo
            {
                PluginId = "moss-ignition", Style = "moss",
                Name = "MOSS · Ignition",
                Author = "Tái hiện phong cách — cảm hứng: QIFANAI",
                RepoUrl = "https://github.com/QIFANAI/dsh-startup-moss2",
                License = "MIT (cấu trúc)", ThirdParty = true,
                Attribution = "“MOSS · Ignition” là bản TÁI HIỆN PHONG CÁCH, vẽ thủ tục 100% bằng Avalonia: tiết tấu 4 hồi học từ “MOSS 550W” của QIFANAI — MIT License — https://github.com/QIFANAI/dsh-startup-moss2 . KHÔNG dùng video Trái Đất/MOSS của bản gốc (tư liệu Wallpaper Engine, quyền chưa rõ), không dùng footage, trích dẫn hay logo phim.",
            },
            new GreetingThemeInfo
            {
                PluginId = "hat-bui-tv", Style = "particles",
                Name = "Hạt Bụi TV",
                Author = "Ý tưởng hạt biến hình: inju0524 (INJU Particle Boot)",
                RepoUrl = "https://github.com/inju0524/dsh-inju-boot",
                License = "MIT", ThirdParty = true,
                Attribution = "“Hạt Bụi TV” lấy ý tưởng hạt biến hình (particle morph) từ “INJU Particle Boot” © inju0524 — MIT License — https://github.com/inju0524/dsh-inju-boot . Hình đích là logo Mẫu F của chính app; toàn bộ hình ảnh sinh bằng code.",
            },
            new GreetingThemeInfo
            {
                PluginId = "tien-galaxy-boot", Style = "galaxy",
                Name = "Tien Galaxy Boot",
                Author = "tiendz2k",
                RepoUrl = "", License = "Của app", ThirdParty = false,
                Attribution = "",
            },
        };

        public static GreetingThemeInfo Find(string pluginId)
        {
            foreach (var t in All)
                if (t.PluginId == pluginId) return t;
            return null;
        }

        public static GreetingThemeInfo FindByStyle(string style)
        {
            foreach (var t in All)
                if (t.Style == style) return t;
            return null;
        }

        /// <summary>Cac dong ghi cong DAY DU cua theme ben thu ba — dung
        /// cho README/NOTICE di kem ban phat hanh (trong UI khong con hien
        /// ban day du nay nua tu v3.11.1).</summary>
        public static List<string> CreditLines()
        {
            var lines = new List<string>();
            foreach (var t in All)
                if (t.ThirdParty) lines.Add(t.Attribution);
            return lines;
        }

        /// <summary>
        /// v3.11.1: dong ghi cong GON cho muc "Nguon theme & giay phep" —
        /// NOI DUY NHAT trong UI con hien ghi cong (Cai dat -> Gioi thieu):
        /// "Ten theme — tac gia — giay phep", moi theme mot dong.
        /// </summary>
        public static List<string> AboutCreditLines()
        {
            var lines = new List<string>();
            foreach (var t in All)
                if (t.ThirdParty) lines.Add(t.Name + " — " + t.Author + " — " + t.License);
            return lines;
        }
    }

    /// <summary>
    /// Payload ma QR tren the ID cua theme 550C. Uu tien noi dung nguoi dung
    /// tu nhap (qrtext); de trong thi dung the dinh danh gom ten + ma.
    /// Thuan chuoi, xac dinh (deterministic) de test.
    /// </summary>
    public static class QrPayloadBuilder
    {
        public const string DefaultNote = "TIEN VU KEYBOARD STUDIO — TV ACCESS CARD";

        public static string Build(string name, string code, string qrText)
        {
            string custom = (qrText ?? "").Trim();
            if (custom.Length > 0) return custom;
            string n = string.IsNullOrWhiteSpace(name) ? "T OPERATOR" : name.Trim();
            string c = string.IsNullOrWhiteSpace(code) ? "TKB-001" : code.Trim();
            return DefaultNote + "\nID: " + n + "\nCODE: " + c;
        }
    }

    /// <summary>
    /// Serial tren the ID: suy ra xac dinh tu ten + ma (FNV-1a 32-bit ->
    /// 8 ky tu hex), dinh dang TV-XXXX-XXXX. Cung mot nguoi dung luon ra
    /// cung mot serial giua cac lan mo app.
    /// </summary>
    public static class IdCardSerial
    {
        public static string Build(string name, string code)
        {
            string seed = (name ?? "") + "|" + (code ?? "");
            uint h = 2166136261u;
            foreach (char ch in seed)
            {
                h ^= ch;
                h *= 16777619u;
            }
            return "TV-" + h.ToString("X8").Insert(4, "-");
        }
    }

    /// <summary>
    /// Lay mau diem dich cho hat logo (theme Hat Bui TV): doc pixel RGBA cua
    /// anh logo, tim vung "tien canh" (logo) roi tra ve cac diem chuan hoa
    /// 0..1 THEO HOP BAO cua tien canh (logo dien day khung 0..1), kem mau
    /// RGB cua pixel nguon.
    ///
    /// Cach nhan dien tien canh: logo ban phat hanh la PNG RGB mo (khong co
    /// kenh alpha thay doi) nen quy tac chinh la: neu anh CO alpha thay doi
    /// dang ke thi dung alpha >= 140; neu khong, dung khoang cach mau so voi
    /// mau nen lay o pixel goc (0,0) — pixel nao lech > 60 (Euclid RGB) la
    /// tien canh. Ca hai nhanh deu xac dinh.
    ///
    /// Chon diem: xao tron Fisher-Yates bang LCG co hat giong co dinh roi
    /// lay N diem dau — cung dau vao + cung seed => cung ket qua (test duoc).
    /// </summary>
    public static class LogoParticleSampler
    {
        public struct TargetPoint
        {
            public double X, Y;      // 0..1 trong hop bao tien canh
            public byte R, G, B;
        }

        public static List<TargetPoint> Sample(byte[] rgba, int width, int height, int count, int seed)
        {
            var result = new List<TargetPoint>();
            if (rgba == null || width <= 0 || height <= 0 || count <= 0) return result;
            if (rgba.Length < width * height * 4) return result;

            // Alpha co thay doi dang ke khong?
            int minA = 255, maxA = 0;
            for (int i = 3; i < rgba.Length; i += 4)
            {
                int a = rgba[i];
                if (a < minA) minA = a;
                if (a > maxA) maxA = a;
            }
            bool useAlpha = (maxA - minA) > 32;

            byte bgR = rgba[0], bgG = rgba[1], bgB = rgba[2];
            var fg = new List<int>(); // chi so pixel tien canh
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int p = (y * width + x) * 4;
                    bool isFg;
                    if (useAlpha) isFg = rgba[p + 3] >= 140;
                    else
                    {
                        int dr = rgba[p] - bgR, dg = rgba[p + 1] - bgG, db = rgba[p + 2] - bgB;
                        isFg = dr * dr + dg * dg + db * db > 60 * 60;
                    }
                    if (isFg)
                    {
                        fg.Add(y * width + x);
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (fg.Count == 0 || maxX < minX || maxY < minY) return result;

            // Fisher-Yates voi LCG xac dinh (Numerical Recipes).
            // Tron seed bang hang so Knuth: seed lien ke (42/43) khong duoc
            // trung trang thai khoi tao (loi cu: seed|1 lam 42 va 43 giong nhau).
            uint state = unchecked((uint)seed * 2654435761u + 2246822519u);
            if (state == 0) state = 1;
            for (int i = fg.Count - 1; i > 0; i--)
            {
                state = state * 1664525u + 1013904223u;
                int j = (int)(state % (uint)(i + 1));
                int tmp = fg[i]; fg[i] = fg[j]; fg[j] = tmp;
            }

            double bw = Math.Max(1, maxX - minX);
            double bh = Math.Max(1, maxY - minY);
            int take = Math.Min(count, fg.Count);
            for (int i = 0; i < take; i++)
            {
                int idx = fg[i];
                int px = idx % width, py = idx / width;
                int o = idx * 4;
                result.Add(new TargetPoint
                {
                    X = (px - minX) / bw,
                    Y = (py - minY) / bh,
                    R = rgba[o], G = rgba[o + 1], B = rgba[o + 2],
                });
            }
            return result;
        }
    }

    /// <summary>
    /// May trang thai trinh tu man chao theo phong cach: danh sach hoi
    /// (phase) + thoi luong; hoi hien tai theo thoi gian da troi; trang
    /// thai bo qua (Skip) va hoan tat. Thuan logic de test — UI chi doc
    /// trang thai nay de ve.
    /// </summary>
    public sealed class GreetingSequence
    {
        public sealed class Phase
        {
            public string Id;
            public int DurationMs;
        }

        private readonly Phase[] _phases;
        public string Style { get; }
        public bool Skipped { get; private set; }

        private GreetingSequence(string style, Phase[] phases)
        {
            Style = style;
            _phases = phases;
        }

        public IReadOnlyList<Phase> Phases => _phases;

        public int TotalMs
        {
            get { int t = 0; foreach (var p in _phases) t += p.DurationMs; return t; }
        }

        public static GreetingSequence For(string style)
        {
            Phase P(string id, int ms) => new Phase { Id = id, DurationMs = ms };
            switch (style)
            {
                case "terminal": // Rhine Lab — 6 hoi
                    return new GreetingSequence(style, new[]
                    {
                        P("title-slate", 700), P("whiteout", 350), P("dossier", 1300),
                        P("authorization", 2300), P("chromatic-reveal", 700), P("ready", 500),
                    });
                case "550c": // 550C Boot + The ID
                    return new GreetingSequence(style, new[]
                    {
                        P("crt-boot", 2500), P("ueg-login", 900), P("id-card", 2300), P("granted", 500),
                    });
                case "moss": // MOSS · Ignition — 4 hoi + lo logo
                    return new GreetingSequence(style, new[]
                    {
                        P("neural-link", 1400), P("red-eye", 1400), P("split", 700),
                        P("ignition", 2600), P("logo-reveal", 900),
                    });
                case "particles": // Hat Bui TV
                    return new GreetingSequence(style, new[]
                    {
                        P("swirl", 1600), P("morph", 2100), P("settle", 1200), P("reveal", 700),
                    });
                case "galaxy":
                    return new GreetingSequence(style, new[] { P("sweep", 1600), P("ready", 400) });
                default:
                    return new GreetingSequence(style ?? "none", new[] { P("static", 2000) });
            }
        }

        /// <summary>Chi so hoi tai thoi diem elapsedMs (clamp ve hoi cuoi).</summary>
        public int PhaseIndexAt(int elapsedMs)
        {
            if (elapsedMs < 0) elapsedMs = 0;
            int acc = 0;
            for (int i = 0; i < _phases.Length; i++)
            {
                acc += _phases[i].DurationMs;
                if (elapsedMs < acc) return i;
            }
            return _phases.Length - 1;
        }

        public string PhaseIdAt(int elapsedMs) => _phases[PhaseIndexAt(elapsedMs)].Id;

        /// <summary>Tien trinh 0..1 trong hoi hien tai.</summary>
        public double PhaseProgressAt(int elapsedMs)
        {
            if (elapsedMs < 0) elapsedMs = 0;
            int acc = 0;
            for (int i = 0; i < _phases.Length; i++)
            {
                int d = _phases[i].DurationMs;
                if (elapsedMs < acc + d)
                    return d <= 0 ? 1 : (double)(elapsedMs - acc) / d;
                acc += d;
            }
            return 1;
        }

        public bool IsCompleteAt(int elapsedMs) => elapsedMs >= TotalMs;

        public void Skip() => Skipped = true;

        public bool IsDoneAt(int elapsedMs) => Skipped || IsCompleteAt(elapsedMs);
    }
}
