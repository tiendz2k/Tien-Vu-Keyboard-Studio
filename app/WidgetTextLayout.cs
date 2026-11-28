using System;
using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    /// <summary>
    /// v3.11.2 — Logic THUAN cho bo cuc chu cua widget dong ho + thoi tiet
    /// 128×128 tren man hinh TFT. Tach ra khoi WidgetRenderer de test duoc
    /// khong can Avalonia: moi so do chieu rong di qua delegate Measurer —
    /// ban that truyen ham do bang FormattedText voi font Be Vietnam Pro
    /// dong goi trong app (dung metric font THAT cua renderer), test truyen
    /// ham do gia lap xac dinh.
    ///
    /// Hai thay doi theo yeu cau cua Tien sau khi dung that tren F75 Max:
    ///  1. Dong chan trang "Tien Vu Keyboard Studio" o day khung BI GO —
    ///     man hinh nho, dong ten app khong mang thong tin gi.
    ///  2. Dong thoi tiet dai ("Kuwana, Nhat Ban 12°C · Mua phun") bi tran
    ///     ca hai ben man hinh 128px -> tach lam HAI dong: dong Place
    ///     (noi chon) va dong Detail (nhiet do + trang thai). Dong nao van
    ///     qua dai (ten noi chon dai) thi cat gon bang dau "…" theo dung
    ///     chieu rong do duoc, khong de tran.
    /// </summary>
    public static class WidgetTextLayout
    {
        /// <summary>Chieu rong khung TFT (px).</summary>
        public const double FrameSize = 128;

        /// <summary>Chieu rong chu toi da cho phep: khung 128 tru le 4px moi ben.</summary>
        public const double MaxLineWidth = 120;

        /// <summary>Do chieu rong (px) cua mot chuoi o co chu + do dam cho truoc.</summary>
        public delegate double Measurer(string text, double size, double weight);

        /// <summary>Mot dong chu trong khung dong ho.</summary>
        public sealed class Line
        {
            public string Role;   // "time" | "date" | "place" | "detail"
            public string Text;
            public double Size;
            public double Weight;
            public double Y;
        }

        /// <summary>
        /// Cat gon chuoi cho vua maxWidth: giu nguyen neu da vua; neu khong,
        /// cat ve tien to dai nhat con vua khi them dau "…" (tim nhi phan
        /// theo so ky tu, dua tren ham do caller truyen vao).
        /// </summary>
        public static string FitToWidth(string text, double maxWidth, Measurer measure, double size, double weight)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (measure(text, size, weight) <= maxWidth) return text;
            const string ell = "…";
            if (measure(ell, size, weight) > maxWidth) return "";
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (measure(text.Substring(0, mid) + ell, size, weight) <= maxWidth) lo = mid;
                else hi = mid - 1;
            }
            return text.Substring(0, lo) + ell;
        }

        public static string WeekdayVi(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Sunday: return "Chủ Nhật";
                case DayOfWeek.Monday: return "Thứ Hai";
                case DayOfWeek.Tuesday: return "Thứ Ba";
                case DayOfWeek.Wednesday: return "Thứ Tư";
                case DayOfWeek.Thursday: return "Thứ Năm";
                case DayOfWeek.Friday: return "Thứ Sáu";
                default: return "Thứ Bảy";
            }
        }

        /// <summary>
        /// Mo hinh chu cua khung dong ho: gio lon, ngay, noi chon, nhiet do +
        /// trang thai. KHONG con dong chan trang ten app. Dong place/detail
        /// rong thi bo qua (khung chi con gio + ngay).
        /// </summary>
        public static List<Line> ClockLines(DateTime now, string place, string detail, Measurer measure)
        {
            var lines = new List<Line>
            {
                new Line { Role = "time", Text = now.ToString("HH:mm"), Size = 40, Weight = 700, Y = 13 },
            };
            string date = WeekdayVi(now.DayOfWeek) + " · " + now.ToString("dd/MM/yyyy");
            lines.Add(new Line
            {
                Role = "date",
                Text = FitToWidth(date, MaxLineWidth, measure, 10, 600),
                Size = 10, Weight = 600, Y = 75,
            });
            string p = (place ?? "").Trim();
            if (p.Length > 0)
                lines.Add(new Line
                {
                    Role = "place",
                    Text = FitToWidth(p, MaxLineWidth, measure, 9.5, 400),
                    Size = 9.5, Weight = 400, Y = 93,
                });
            string dt = (detail ?? "").Trim();
            if (dt.Length > 0)
                lines.Add(new Line
                {
                    Role = "detail",
                    Text = FitToWidth(dt, MaxLineWidth, measure, 9.5, 400),
                    Size = 9.5, Weight = 400, Y = 108,
                });
            return lines;
        }

        // =================================================================
        // v3.17 (scope D) — Bo cuc widget DANG PHAT tren TFT 128×128.
        //
        // Van de cua ban cu (v3.15/v3.16): anh bia 46×46 nam ben trai va
        // tieu de nghe si bi ep vao cot 56px ben phai, mot dong, co co
        // dinh — hau het tieu de bai hat that deu bi cat dut giua chung
        // (Tien thay tren F75 Max that: man hinh nho ma noi dung khong
        // hien du).
        //
        // Bo cuc moi (van logic thuan, do bang Measurer nhu ClockLines):
        //  - Anh bia (neu session co) thu ve 44×44 o goc tren trai; canh
        //    no la glyph phat/tam dung + dong trang thai nho.
        //  - Tieu de chiem TOAN CHIEU NGANG (112px): toi da 2 dong, ngat
        //    dong theo tu (tu qua dai thi be theo ky tu), co chu TU CO
        //    XUONG tu co bat dau toi co toi thieu truoc khi nghi den cat.
        //  - Neu o co toi thieu ma 2 dong van khong du: MARQUEE — cua so
        //    ky tu truot, moi tick day (auto-push) tien them
        //    MarqueeStepChars ky tu, chay vong qua dau noi "  ·  ".
        //    Trang thai marquee la tham so offset truyen vao (renderer
        //    khong tu giu state) — NowPlayingLines tra ve
        //    MarqueeNextOffset de caller luu cho tick sau.
        //  - Nghe si: 1 dong, co xuong toi co toi thieu roi moi cat "…".
        //  - Thanh tien trinh + thoi gian hien tai/tong o day khung,
        //    thoi gian tong can phai — LUON tron ven, khong bao gio cat.
        //  - Khong co anh bia: khong ve o anh thay the — tieu de duoc
        //    dung co bat dau lon hon va khoi chu nhich len tren.
        // Moi dong chu deu dam bao: X + do-rong-do-duoc <= 120 (le phai
        // 8px) va Y + Height <= 128.
        // =================================================================

        /// <summary>Le trai/phai cua khoi noi dung Now Playing.</summary>
        public const double NpMargin = 8;
        /// <summary>Chieu rong khoi noi dung: 128 − 2×8 = 112.</summary>
        public const double NpContentWidth = FrameSize - 2 * NpMargin;
        /// <summary>Me phai cua khoi noi dung (moi dong chu phai ket thuc truoc me nay).</summary>
        public const double NpRightEdge = FrameSize - NpMargin;
        /// <summary>Kich thuoc anh bia (vuong) khi session co thumbnail.</summary>
        public const double NpArtSize = 44;
        /// <summary>Co tieu de nho nhat truoc khi chuyen sang marquee.</summary>
        public const double NpTitleMinSize = 9;
        /// <summary>Co nghe si nho nhat truoc khi cat "…".</summary>
        public const double NpArtistMinSize = 7.5;
        /// <summary>So ky tu marquee tien them moi tick day.</summary>
        public const int MarqueeStepChars = 3;
        /// <summary>Chuoi noi khi marquee chay vong qua cuoi tieu de.</summary>
        public const string MarqueeSeparator = "  ·  ";

        /// <summary>Mot dong chu trong khung Dang phat.</summary>
        public sealed class NpLine
        {
            public string Role;   // "state" | "title" | "artist" | "time-cur" | "time-tot"
            public string Text;
            public double Size;
            public double Weight;
            public double X;
            public double Y;
            public double Height; // uoc luong Size × 1.22 de test chan day khung
        }

        /// <summary>Ket qua bo cuc Dang phat: renderer chi viec ve theo.</summary>
        public sealed class NowPlayingLayout
        {
            public bool HasArt;
            public double ArtX, ArtY, ArtSize;   // chi co nghia khi HasArt
            public double GlyphCX, GlyphCY;      // tam glyph phat/tam dung
            public bool IsPlaying;
            public readonly List<NpLine> Lines = new List<NpLine>();
            public bool Marquee;                 // tieu de dang o che do truot
            public int MarqueeNextOffset;        // offset (ky tu) cho tick sau; 0 khi khong marquee
            public double BarX, BarY, BarW, BarH;
        }

        private static string NormalizeSpaces(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var parts = s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        // Tien to dai nhat cua s do vua maxWidth (tim nhi phan theo ky tu).
        private static int LongestFittingPrefix(string s, double maxWidth, Measurer measure, double size, double weight)
        {
            if (s.Length == 0) return 0;
            if (measure(s, size, weight) <= maxWidth) return s.Length;
            int lo = 0, hi = s.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (measure(s.Substring(0, mid), size, weight) <= maxWidth) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>
        /// Ngat tieu de thanh toi da maxLines dong vua maxWidth o co cho
        /// truoc: ngat theo tu; tu nao dai hon ca mot dong thi be theo ky
        /// tu. Tra null neu khong xep duoc trong maxLines dong.
        /// </summary>
        public static List<string> WrapTitle(string title, double maxWidth, Measurer measure,
            double size, double weight, int maxLines)
        {
            string t = NormalizeSpaces(title);
            if (t.Length == 0) return new List<string> { "" };
            var lines = new List<string>();
            string cur = "";
            foreach (var word0 in t.Split(' '))
            {
                string word = word0;
                while (measure(word, size, weight) > maxWidth)
                {
                    if (cur.Length > 0)
                    {
                        lines.Add(cur); cur = "";
                        if (lines.Count >= maxLines) return null;
                    }
                    int n = LongestFittingPrefix(word, maxWidth, measure, size, weight);
                    if (n <= 0) return null;
                    lines.Add(word.Substring(0, n));
                    if (lines.Count >= maxLines) return null;
                    word = word.Substring(n);
                }
                if (cur.Length == 0) cur = word;
                else if (measure(cur + " " + word, size, weight) <= maxWidth) cur = cur + " " + word;
                else
                {
                    lines.Add(cur);
                    if (lines.Count >= maxLines) return null;
                    cur = word;
                }
            }
            if (cur.Length > 0) lines.Add(cur);
            if (lines.Count > maxLines) return null;
            foreach (var ln in lines)
                if (measure(ln, size, weight) > maxWidth) return null;
            return lines;
        }

        /// <summary>Ket qua do tieu de: cac dong + co da chon + trang thai marquee.</summary>
        public sealed class TitleFit
        {
            public List<string> Lines;
            public double Size;
            public bool Marquee;
            public int NextOffset;
        }

        /// <summary>
        /// Do tieu de vao khoi ngang maxWidth: thu co tu startSize xuong
        /// NpTitleMinSize (buoc 0.5) cho ban 2 dong; khong co nao xep duoc
        /// thi chuyen MARQUEE o co toi thieu — cua so ky tu bat dau tai
        /// marqueeOffset (chuan hoa theo chu ky title + dau noi), tra ve
        /// NextOffset = offset + MarqueeStepChars cho tick sau.
        /// </summary>
        public static TitleFit FitTitleBlock(string title, double maxWidth, Measurer measure,
            double startSize, int marqueeOffset)
        {
            string t = NormalizeSpaces(title);
            if (t.Length == 0)
                return new TitleFit { Lines = new List<string> { "" }, Size = startSize, Marquee = false, NextOffset = 0 };
            for (double size = startSize; size >= NpTitleMinSize - 1e-9; size -= 0.5)
            {
                var lines = WrapTitle(t, maxWidth, measure, size, 700, 2);
                if (lines != null)
                    return new TitleFit { Lines = lines, Size = size, Marquee = false, NextOffset = 0 };
            }
            string cycle = t + MarqueeSeparator;
            int cycleLen = cycle.Length;
            int off = ((marqueeOffset % cycleLen) + cycleLen) % cycleLen;
            string rest = (cycle + cycle).Substring(off);
            int n = LongestFittingPrefix(rest, maxWidth, measure, NpTitleMinSize, 700);
            if (n <= 0) n = 1;
            return new TitleFit
            {
                Lines = new List<string> { rest.Substring(0, n) },
                Size = NpTitleMinSize,
                Marquee = true,
                NextOffset = (off + MarqueeStepChars) % cycleLen,
            };
        }

        /// <summary>
        /// Do dong nghe si: giu nguyen van, co xuong tu startSize toi
        /// NpArtistMinSize; van khong vua thi cat "…" o co toi thieu.
        /// </summary>
        public static (string Text, double Size) FitArtistLine(string artist, double maxWidth,
            Measurer measure, double startSize)
        {
            string a = NormalizeSpaces(artist);
            if (a.Length == 0) return ("", startSize);
            for (double size = startSize; size >= NpArtistMinSize - 1e-9; size -= 0.5)
                if (measure(a, size, 400) <= maxWidth) return (a, size);
            return (FitToWidth(a, maxWidth, measure, NpArtistMinSize, 400), NpArtistMinSize);
        }

        /// <summary>
        /// Mo hinh bo cuc day du cua khung Dang phat. curText/totText la
        /// thoi gian DA DINH DANG ("m:ss") do caller truyen (renderer dung
        /// NowPlayingLogic.FormatTime) — layout chi dat vi tri: hien tai
        /// can trai, tong can phai cham me 120, khong bao gio bi cat.
        /// </summary>
        public static NowPlayingLayout NowPlayingLines(string title, string artist, bool hasArt,
            bool isPlaying, string curText, string totText, int marqueeOffset, Measurer measure)
        {
            var lay = new NowPlayingLayout { HasArt = hasArt, IsPlaying = isPlaying };
            double titleStart = hasArt ? 12.5 : 14;
            double artistStart = hasArt ? 9.5 : 10.5;

            // Dong trang thai + glyph phat/tam dung (goc tren).
            lay.Lines.Add(new NpLine
            {
                Role = "state",
                Text = isPlaying ? "ĐANG PHÁT" : "TẠM DỪNG",
                Size = 7.5, Weight = 600,
                X = hasArt ? 66 : 20, Y = hasArt ? 11 : 8, Height = 7.5 * 1.22,
            });
            lay.GlyphCX = hasArt ? 60 : 12;
            lay.GlyphCY = hasArt ? 15.5 : 12.5;
            if (hasArt) { lay.ArtX = NpMargin; lay.ArtY = NpMargin; lay.ArtSize = NpArtSize; }

            // Tieu de: toan chieu ngang, toi da 2 dong co dan xuong,
            // khong du thi marquee.
            var tf = FitTitleBlock(title, NpContentWidth, measure, titleStart, marqueeOffset);
            double ty = hasArt ? 58 : 24;
            double lh = tf.Size * 1.3;
            for (int i = 0; i < tf.Lines.Count; i++)
                lay.Lines.Add(new NpLine
                {
                    Role = "title", Text = tf.Lines[i], Size = tf.Size, Weight = 700,
                    X = NpMargin, Y = ty + i * lh, Height = tf.Size * 1.22,
                });
            lay.Marquee = tf.Marquee;
            lay.MarqueeNextOffset = tf.NextOffset;

            // Nghe si: 1 dong, co xuong roi moi cat.
            var (aText, aSize) = FitArtistLine(artist, NpContentWidth, measure, artistStart);
            lay.Lines.Add(new NpLine
            {
                Role = "artist", Text = aText, Size = aSize, Weight = 400,
                X = NpMargin, Y = hasArt ? 93 : 70, Height = aSize * 1.22,
            });

            // Thanh tien trinh + thoi gian o day khung — luon tron ven.
            lay.BarX = NpMargin; lay.BarY = 107; lay.BarW = NpContentWidth; lay.BarH = 5;
            lay.Lines.Add(new NpLine
            {
                Role = "time-cur", Text = curText ?? "", Size = 8.5, Weight = 400,
                X = NpMargin, Y = 116, Height = 8.5 * 1.22,
            });
            double totW = measure(totText ?? "", 8.5, 400);
            lay.Lines.Add(new NpLine
            {
                Role = "time-tot", Text = totText ?? "", Size = 8.5, Weight = 400,
                X = NpRightEdge - totW, Y = 116, Height = 8.5 * 1.22,
            });
            return lay;
        }
    }
}
