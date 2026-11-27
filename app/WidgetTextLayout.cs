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
    }
}
