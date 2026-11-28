using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TienKeyboardStudio3
{
    /// <summary>
    /// Ve widget 128×128 phia may tinh (host-side) roi dua vao man hinh TFT cua
    /// ban phim ve — dung co che ban that se dung: render bitmap tren PC,
    /// gui xuong man hinh phim. Phong cach sach, hien dai, trung tinh.
    /// </summary>
    public static class WidgetRenderer
    {
        private static readonly FontFamily Fam =
            new FontFamily("avares://TienKeyboardStudio/Assets/Fonts#Be Vietnam Pro");

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

        // ---------- Widget 1b: Thong so may THAT (v3.2, SystemStats) ----------
        public static Bitmap RenderStatsReal(SystemStats s)
        {
            var rtb = NewBitmap();
            using (var ctx = rtb.CreateDrawingContext())
            {
                Bg(ctx);
                ctx.DrawEllipse(new SolidColorBrush(Green), null, new Point(12, 13), 3, 3);
                var title = Text("THÔNG SỐ MÁY", 8.5, Sub, 600);
                ctx.DrawText(title, new Point(20, 13 - title.Height / 2));
                Row(ctx, 30, "CPU", s.CpuPercent / 100.0, Violet);
                if (s.HasGpu)
                {
                    Row(ctx, 56, "GPU", (s.GpuPercent ?? 0) / 100.0, Cyan);
                    Row(ctx, 82, "RAM", s.RamPercent / 100.0, Pink);
                    var foot = Text("GPU " + (s.GpuTemp?.ToString() ?? "—") + "°C · VRAM " + (s.GpuMemPercent?.ToString() ?? "—") + "%", 8.5, Sub);
                    ctx.DrawText(foot, new Point(10, 128 - 10 - foot.Height));
                }
                else
                {
                    Row(ctx, 56, "RAM", s.RamPercent / 100.0, Pink);
                    var foot = Text("Không thấy GPU NVIDIA", 8.5, Sub);
                    ctx.DrawText(foot, new Point(10, 128 - 10 - foot.Height));
                }
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

        // ---------- Widget 2: Dang phat THAT (v3.15, GSMTC) ----------
        // Ban mo phong cu (SongTitle/SongArtist/SongDuration) DA XOA —
        // du lieu that den tu NowPlayingService (Windows.Media.Control),
        // MainWindow dung thanh MediaViewData roi goi renderer nay.
        // Khong co session -> RenderNowPlayingIdle (khong giu bai cu
        // dong bang).
        public struct MediaViewData
        {
            public string Title;
            public string Artist;
            public double PositionSec;
            public double DurationSec;
            public bool IsPlaying;
            public byte[] ArtPngBytes; // PNG thu nho hoac null
            // v3.17: offset ky tu cua marquee tieu de — caller giu state
            // (reset khi doi bai), renderer tra offset ke tiep qua
            // tham so out cua RenderNowPlayingReal.
            public int MarqueeOffset;
        }

        // v3.17 (scope D): ve theo bo cuc WidgetTextLayout.NowPlayingLines
        // — anh bia 44×44 goc tren (neu co), tieu de toan chieu ngang
        // toi da 2 dong co dan xuong (khong du thi marquee truot theo
        // MarqueeOffset), nghe si 1 dong co dan, thanh tien trinh +
        // thoi gian o day khung, glyph phat/tam dung canh dong trang
        // thai. Ban cu ep tieu de vao cot 56px nen cat gan het bai hat.
        public static Bitmap RenderNowPlayingReal(MediaViewData d, double phase, out int marqueeNextOffset)
        {
            bool hasArt = d.ArtPngBytes != null && d.ArtPngBytes.Length > 0;
            var lay = WidgetTextLayout.NowPlayingLines(d.Title ?? "", d.Artist ?? "", hasArt, d.IsPlaying,
                NowPlayingLogic.FormatTime(d.PositionSec), NowPlayingLogic.FormatTime(d.DurationSec),
                d.MarqueeOffset, (s, size, w) => Text(s, size, Ink, w).Width);
            marqueeNextOffset = lay.MarqueeNextOffset;
            var rtb = NewBitmap();
            using (var ctx = rtb.CreateDrawingContext())
            {
                Bg(ctx);
                if (hasArt)
                {
                    var art = new Rect(lay.ArtX, lay.ArtY, lay.ArtSize, lay.ArtSize);
                    Bitmap artBmp = null;
                    try { artBmp = new Bitmap(new System.IO.MemoryStream(d.ArtPngBytes)); }
                    catch { artBmp = null; }
                    using (ctx.PushClip(new RoundedRect(art, 8, 8)))
                    {
                        if (artBmp != null)
                        {
                            ctx.DrawImage(artBmp, art);
                        }
                        else
                        {
                            // Co bytes nhung giai ma loi: dia gradient nhu khung cu.
                            var artBrush = new LinearGradientBrush
                            {
                                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                                GradientStops = { new GradientStop(Violet, 0), new GradientStop(Cyan, 1) },
                            };
                            ctx.DrawRectangle(artBrush, null, art);
                            var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)), 1);
                            foreach (double rr in new[] { 15.0, 10.0, 5.5 })
                                ctx.DrawEllipse(null, ringPen, art.Center, rr, rr);
                            ctx.DrawEllipse(Brushes.White, null, art.Center, 2.2, 2.2);
                        }
                    }
                    artBmp?.Dispose();
                }
                DrawStateGlyph(ctx, lay);
                foreach (var ln in lay.Lines)
                {
                    var color = ln.Role == "title" ? Ink : Sub;
                    var ft = Text(ln.Text, ln.Size, color, ln.Weight);
                    ctx.DrawText(ft, new Point(ln.X, ln.Y));
                }
                // 3 cot EQ: chi nhay khi dang phat that. Co anh: canh duoi
                // khoi anh; khong anh: giua khung, duoi dong nghe si.
                double eqBase = hasArt ? 50 : 100;
                double eqX = hasArt ? 58 : 55;
                for (int i = 0; i < 3; i++)
                {
                    double hgt = d.IsPlaying ? 5 + 6 * Math.Abs(Math.Sin(phase * 2.2 + i * 1.3)) : 5;
                    ctx.DrawRectangle(new SolidColorBrush(Cyan), null,
                        new Rect(eqX + i * 7, eqBase - hgt, 4, hgt), 1.5f, 1.5f);
                }
                double frac = d.DurationSec > 0 ? Math.Min(1, d.PositionSec / d.DurationSec) : 0;
                Bar(ctx, lay.BarX, lay.BarY, lay.BarW, lay.BarH, frac, Violet);
            }
            return rtb;
        }

        // Glyph phat (tam giac) / tam dung (hai vach) tai tam layout chi dinh.
        private static void DrawStateGlyph(DrawingContext ctx, WidgetTextLayout.NowPlayingLayout lay)
        {
            var brush = new SolidColorBrush(Cyan);
            double cx = lay.GlyphCX, cy = lay.GlyphCY;
            if (lay.IsPlaying)
            {
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(new Point(cx - 3.5, cy - 4.5), true);
                    g.LineTo(new Point(cx - 3.5, cy + 4.5));
                    g.LineTo(new Point(cx + 4.5, cy));
                    g.EndFigure(true);
                }
                ctx.DrawGeometry(brush, null, geo);
            }
            else
            {
                ctx.DrawRectangle(brush, null, new Rect(cx - 4.5, cy - 4.5, 3.2, 9));
                ctx.DrawRectangle(brush, null, new Rect(cx + 1.3, cy - 4.5, 3.2, 9));
            }
        }

        /// <summary>Khung khi khong co nhac nao dang phat (graceful absence).</summary>
        public static Bitmap RenderNowPlayingIdle()
        {
            var rtb = NewBitmap();
            using (var ctx = rtb.CreateDrawingContext())
            {
                Bg(ctx);
                var art = new Rect(41, 16, 46, 46);
                var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(0x60, 0x8B, 0x93, 0xB8)), 1.5);
                ctx.DrawEllipse(null, ringPen, art.Center, 20, 20);
                ctx.DrawEllipse(new SolidColorBrush(Sub), null, art.Center, 3, 3);
                var t1 = Text("Chưa có nhạc", 11.5, Ink, 700);
                ctx.DrawText(t1, new Point((128 - t1.Width) / 2, 74));
                var t2 = Text("đang phát", 11.5, Ink, 700);
                ctx.DrawText(t2, new Point((128 - t2.Width) / 2, 90));
                var t3 = Text("Mở Spotify hoặc trình duyệt", 7.5, Sub);
                ctx.DrawText(t3, new Point((128 - t3.Width) / 2, 110));
            }
            return rtb;
        }

        // ---------- Widget 3: Dong ho that + thoi tiet ----------
        // v3.11.2: bo cuc chu nam o WidgetTextLayout (logic thuan, co test).
        // placeLine/detailLine = null -> chu mo phong cua ban Preview.
        // Ban that truyen noi chon + nhiet do/trang thai that (Open-Meteo).
        // Dong chan trang ten app DA GO theo yeu cau cua Tien: man hinh
        // 128px nho, dong thoi tiet tach lam hai dong (noi chon / nhiet do
        // + trang thai) de khong tran hai ben nhu ban cu.
        public static Bitmap RenderClock(DateTime now, string placeLine = null, string detailLine = null)
        {
            var rtb = NewBitmap();
            using (var ctx = rtb.CreateDrawingContext())
            {
                Bg(ctx);
                var lines = WidgetTextLayout.ClockLines(now,
                    placeLine ?? "Hà Nội", detailLine ?? "24°C · Có mây",
                    (s, size, w) => Text(s, size, Ink, w).Width);
                foreach (var ln in lines)
                {
                    var color = ln.Role == "date" ? Sub : Ink;
                    var ft = Text(ln.Text, ln.Size, color, ln.Weight);
                    ctx.DrawText(ft, new Point((128 - ft.Width) / 2, ln.Y));
                    if (ln.Role == "time")
                    {
                        // duong accent gradient duoi gio
                        var line = new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                            GradientStops = { new GradientStop(Violet, 0), new GradientStop(Cyan, 1) },
                        };
                        ctx.DrawRectangle(line, null, new Rect(34, 65, 60, 3), 1.5f, 1.5f);
                    }
                }
            }
            return rtb;
        }
    }
}
