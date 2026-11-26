using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TienKeyboardStudioPreview
{
    /// <summary>
    /// Ve widget 128×128 phia may tinh (host-side) roi dua vao man hinh TFT cua
    /// ban phim ve — dung co che ban that se dung: render bitmap tren PC,
    /// gui xuong man hinh phim. Phong cach sach, hien dai, trung tinh.
    /// </summary>
    public static class WidgetRenderer
    {
        private static readonly FontFamily Fam =
            new FontFamily("avares://TienKeyboardStudioPreview/Assets/Fonts#Be Vietnam Pro");

        private static readonly Color BgTop = Color.FromRgb(0x0B, 0x0E, 0x1A);
        private static readonly Color BgBottom = Color.FromRgb(0x16, 0x1B, 0x30);
        private static readonly Color Ink = Color.FromRgb(0xED, 0xF0, 0xFF);
        private static readonly Color Sub = Color.FromRgb(0x8B, 0x93, 0xB8);
        private static readonly Color Violet = Color.FromRgb(0x7C, 0x3A, 0xED);
        private static readonly Color Cyan = Color.FromRgb(0x22, 0xD3, 0xEE);
        private static readonly Color Pink = Color.FromRgb(0xF4, 0x72, 0xB6);
        private static readonly Color Green = Color.FromRgb(0x34, 0xD3, 0x99);

        private static Typeface Tf(double w) => new Typeface(Fam, FontStyle.Normal,
            w >= 700 ? FontWeight.Bold : w >= 600 ? FontWeight.SemiBold : FontWeight.Normal, FontStretch.Normal);

        private static FormattedText Text(string s, double size, Color color, double w = 400)
            => new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                Tf(w), size, new SolidColorBrush(color));

        private static void Bg(DrawingContext ctx)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(BgTop, 0), new GradientStop(BgBottom, 1) },
            };
            ctx.FillRectangle(brush, new Rect(0, 0, 128, 128));
        }

        private static void Bar(DrawingContext ctx, double x, double y, double w, double h, double frac, Color color)
        {
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF)), null,
                new Rect(x, y, w, h), (float)(h / 2), (float)(h / 2));
            if (frac > 0.01)
                ctx.DrawRectangle(new SolidColorBrush(color), null,
                    new Rect(x, y, Math.Max(h, w * Math.Min(1, frac)), h), (float)(h / 2), (float)(h / 2));
        }

        private static RenderTargetBitmap NewBitmap()
            => new RenderTargetBitmap(new PixelSize(128, 128), new Vector(96, 96));

        // ---------- Widget 1: Thong so may (gia tri gia lap dao dong nhe) ----------
        public static Bitmap RenderStats(double cpu, double gpu, double ram, int gpuTemp)
        {
            var rtb = NewBitmap();
            using (var ctx = rtb.CreateDrawingContext())
            {
                Bg(ctx);
                ctx.DrawEllipse(new SolidColorBrush(Green), null, new Point(12, 13), 3, 3);
                var title = Text("THÔNG SỐ MÁY", 8.5, Sub, 600);
                ctx.DrawText(title, new Point(20, 13 - title.Height / 2));
                Row(ctx, 30, "CPU", cpu, Violet);
                Row(ctx, 56, "GPU", gpu, Cyan);
                Row(ctx, 82, "RAM", ram, Pink);
                var foot = Text("GPU " + gpuTemp + "°C · Mô phỏng", 8.5, Sub);
                ctx.DrawText(foot, new Point(10, 128 - 10 - foot.Height));
            }
            return rtb;
        }

        private static void Row(DrawingContext ctx, double y, string label, double frac, Color color)
        {
            var lab = Text(label, 10, Ink, 600);
            ctx.DrawText(lab, new Point(10, y));
            var val = Text(Math.Round(frac * 100) + "%", 10, Ink, 700);
            ctx.DrawText(val, new Point(128 - 10 - val.Width, y));
            Bar(ctx, 10, y + 15, 108, 5, frac, color);
        }

        // ---------- Widget 2: Dang phat (mo phong) ----------
        public const string SongTitle = "Midnight Drive";
        public const string SongArtist = "TienMix · Mô phỏng";
        public const int SongDuration = 204; // 3:24

        public static Bitmap RenderNowPlaying(double progressSec, double phase)
        {
            var rtb = NewBitmap();
            using (var ctx = rtb.CreateDrawingContext())
            {
                Bg(ctx);
                // bia album: dia nhua gradient + vong tron dong tam
                var art = new Rect(10, 12, 46, 46);
                var artBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Violet, 0), new GradientStop(Cyan, 1) },
                };
                using (ctx.PushClip(new RoundedRect(art, 8, 8)))
                {
                    ctx.DrawRectangle(artBrush, null, art);
                    var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)), 1);
                    foreach (double rr in new[] { 16.0, 11.0, 6.0 })
                        ctx.DrawEllipse(null, ringPen, art.Center, rr, rr);
                    ctx.DrawEllipse(Brushes.White, null, art.Center, 2.2, 2.2);
                }
                var t1 = Text(SongTitle, 11.5, Ink, 700);
                ctx.DrawText(t1, new Point(64, 14));
                var t2 = Text(SongArtist, 8.5, Sub);
                ctx.DrawText(t2, new Point(64, 32));
                // 3 cot EQ nho nhay theo pha
                for (int i = 0; i < 3; i++)
                {
                    double hgt = 5 + 6 * Math.Abs(Math.Sin(phase * 2.2 + i * 1.3));
                    ctx.DrawRectangle(new SolidColorBrush(Cyan), null,
                        new Rect(64 + i * 7, 58 - hgt, 4, hgt), 1.5f, 1.5f);
                }
                var np = Text("ĐANG PHÁT", 7.5, Sub, 600);
                ctx.DrawText(np, new Point(88, 47));
                // tien trinh
                double frac = Math.Min(1, progressSec / SongDuration);
                Bar(ctx, 10, 96, 108, 5, frac, Violet);
                var cur = Text(Fmt(progressSec), 8.5, Sub);
                ctx.DrawText(cur, new Point(10, 106));
                var tot = Text(Fmt(SongDuration), 8.5, Sub);
                ctx.DrawText(tot, new Point(128 - 10 - tot.Width, 106));
            }
            return rtb;
        }

        private static string Fmt(double sec)
        {
            int s = (int)sec;
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        // ---------- Widget 3: Dong ho that + thoi tiet mo phong ----------
        public static Bitmap RenderClock(DateTime now)
        {
            var rtb = NewBitmap();
            using (var ctx = rtb.CreateDrawingContext())
            {
                Bg(ctx);
                string time = now.ToString("HH:mm");
                var ft = Text(time, 40, Ink, 700);
                ctx.DrawText(ft, new Point((128 - ft.Width) / 2, 16));
                // duong accent gradient duoi gio
                var line = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Violet, 0), new GradientStop(Cyan, 1) },
                };
                ctx.DrawRectangle(line, null, new Rect(34, 66, 60, 3), 1.5f, 1.5f);
                string date = WeekdayVi(now.DayOfWeek) + " · " + now.ToString("dd/MM/yyyy");
                var fd = Text(date, 10, Sub, 600);
                ctx.DrawText(fd, new Point((128 - fd.Width) / 2, 78));
                var fw = Text("Hà Nội 24°C · Có mây", 9.5, Ink);
                ctx.DrawText(fw, new Point((128 - fw.Width) / 2, 96));
                var fp = Text("Pin phím 87%", 8.5, Sub);
                ctx.DrawText(fp, new Point((128 - fp.Width) / 2, 112));
            }
            return rtb;
        }

        private static string WeekdayVi(DayOfWeek d)
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
    }
}
