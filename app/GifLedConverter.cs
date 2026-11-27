using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // GIF -> LED: doc tung frame GIF (bat ky kich thuoc, ke ca GIF dai
    // 900x281 cua cac thu vien online) va lay mau cho tung phim theo hinh
    // hoc XML cua F75 Max (MaxKeyData.StreamSlots). Ket qua moi frame:
    // - Slots: 80 o stream 0x20 (id + RGB) de gui/log qua L99Protocol.
    // - ViewColors: mau theo index phim cua KeyboardView de phat trong app.
    //
    // LAY MAU (v3.9.2 — port theo ham lay mau cua ung dung tham chieu
    // tren cung nen tang): MEDIAN theo tung kenh trong hop quanh tam LED
    // (±0.35 chieu rong phim / ±0.35 chieu cao hang), roi chon pixel that
    // trong hop gan mau trung vi nhat (hoa thi gan tam nhat); sau do ap
    // nguong den blackLevel (max(R,G,B) <= nguong -> den) va nhan he so
    // sang. Ban cu lay dung 1 pixel tai tam (point sampling) — mau nhieu
    // va de lech khi GIF co chi tiet nho.
    //
    // HE TOA DO (sua o v3.9.2): CX/CY trong MaxKeyData duoc chuan hoa
    // theo VUNG PHIM CON cua XML (left 90..707, top 28..267) trong khi
    // canvas layout day du la 900x341. Ban cu nhan thang CX/CY voi toan
    // khung GIF nen diem lay mau bi co/lech ve goc tren-trai. Ban nay
    // chuyen ve toa do toan canvas truoc khi lay mau:
    //   fx = (90 + CX * 617) / 900,  fy = (28 + CY * 239) / 341.
    // Kich thuoc phim dung cho hop median uoc luong tu khoang cach phim
    // trong chinh bang StreamSlots (buoc ngang nho nhat trong hang /
    // buoc hang nho nhat) — tham chieu dung rect rieng cua tung phim;
    // day la xap xi da tai lieu hoa.
    // =====================================================================
    public sealed class GifLedAnimation
    {
        public string Name;
        public int SourceWidth, SourceHeight, SourceFrames;
        public List<L99Protocol.StreamQuad[]> Frames = new List<L99Protocol.StreamQuad[]>();
        public List<Dictionary<int, (byte R, byte G, byte B)>> ViewFrames = new List<Dictionary<int, (byte, byte, byte)>>();
        public List<int> DelaysMs = new List<int>();
    }

    public static class GifLedConverter
    {
        public const int MaxLedFrames = 300;
        public const int DefaultBlackLevel = 24;

        // Hinh hoc canvas layout cua XML chinh hang (xem dau file).
        private const double LayoutWidth = 900.0;
        private const double LayoutHeight = 341.0;
        private const double RegionLeft = 90.0;
        private const double RegionTop = 28.0;
        private const double RegionWidth = 707.0 - 90.0;
        private const double RegionHeight = 267.0 - 28.0;

        // Hinh hoc lay mau theo tung slot (toa do toan canvas 0..1):
        // tam (Fx, Fy) + kich thuoc phim uoc luong (Kw, Kh).
        public struct SampleGeometry { public double Fx, Fy, Kw, Kh; }
        private static SampleGeometry[] _geometry;

        public static SampleGeometry[] Geometry => _geometry ??= BuildGeometry();

        private static SampleGeometry[] BuildGeometry()
        {
            var slots = MaxKeyData.StreamSlots;
            // Buoc hang (CY) nho nhat giua cac hang khac nhau -> chieu cao hang.
            var rowYs = slots.Select(s => s.CY).Distinct().OrderBy(v => v).ToList();
            double rowPitch = double.MaxValue;
            for (int i = 1; i < rowYs.Count; i++)
                rowPitch = Math.Min(rowPitch, rowYs[i] - rowYs[i - 1]);
            if (rowPitch == double.MaxValue) rowPitch = 0.16;

            // Buoc ngang nho nhat trong tung hang -> chieu rong 1 phim don vi.
            var unitByRow = new Dictionary<double, double>();
            foreach (var row in slots.GroupBy(s => s.CY))
            {
                var xs = row.Select(s => s.CX).OrderBy(v => v).ToList();
                double unit = double.MaxValue;
                for (int i = 1; i < xs.Count; i++)
                    unit = Math.Min(unit, xs[i] - xs[i - 1]);
                unitByRow[row.Key] = unit == double.MaxValue ? 0.06 : unit;
            }

            var geo = new SampleGeometry[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                geo[i] = new SampleGeometry
                {
                    Fx = (RegionLeft + s.CX * RegionWidth) / LayoutWidth,
                    Fy = (RegionTop + s.CY * RegionHeight) / LayoutHeight,
                    Kw = unitByRow[s.CY] * RegionWidth / LayoutWidth,
                    Kh = rowPitch * RegionHeight / LayoutHeight,
                };
            }
            return geo;
        }

        /// <summary>Lay mau cho mot phim tu mot frame: median theo kenh
        /// trong hop ±0.35×(Kw,Kh) quanh tam, chon pixel that gan mau
        /// trung vi nhat, ap blackLevel roi nhan brightness.
        /// Toa do la he toan canvas chuan hoa 0..1.</summary>
        public static (byte R, byte G, byte B) SampleKeyColor(
            ImageFrame<Rgba32> frame,
            double centerX, double centerY, double keyW, double keyH,
            int blackLevel = DefaultBlackLevel, double brightness = 1.0)
        {
            int w = frame.Width, h = frame.Height;
            double cx = centerX * (w - 1);
            double cy = centerY * (h - 1);
            double halfW = 0.35 * keyW * w;
            double halfH = 0.35 * keyH * h;
            int x0 = Math.Max(0, (int)Math.Floor(cx - halfW));
            int x1 = Math.Min(w - 1, (int)Math.Ceiling(cx + halfW));
            int y0 = Math.Max(0, (int)Math.Floor(cy - halfH));
            int y1 = Math.Min(h - 1, (int)Math.Ceiling(cy + halfH));

            // Median theo kenh bang histogram 256 muc (giong tham chieu).
            var histR = new int[256];
            var histG = new int[256];
            var histB = new int[256];
            int count = 0;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var px = frame[x, y];
                    histR[px.R]++; histG[px.G]++; histB[px.B]++;
                    count++;
                }
            int medR = HistogramMedian(histR, count);
            int medG = HistogramMedian(histG, count);
            int medB = HistogramMedian(histB, count);

            // Chon pixel that trong hop gan mau trung vi nhat (Euclid);
            // hoa thi lay pixel gan tam LED nhat.
            int bestR = medR, bestG = medG, bestB = medB;
            double bestDist = double.MaxValue, bestCenter = double.MaxValue;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var px = frame[x, y];
                    double d = (px.R - medR) * (px.R - medR) + (px.G - medG) * (px.G - medG) + (px.B - medB) * (px.B - medB);
                    double dc = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (d < bestDist || (d == bestDist && dc < bestCenter))
                    {
                        bestDist = d; bestCenter = dc;
                        bestR = px.R; bestG = px.G; bestB = px.B;
                    }
                }

            int r = bestR, g = bestG, b = bestB;
            if (Math.Max(r, Math.Max(g, b)) <= blackLevel)
                return (0, 0, 0);
            if (brightness != 1.0)
            {
                r = Math.Min(255, Math.Max(0, (int)Math.Round(r * brightness)));
                g = Math.Min(255, Math.Max(0, (int)Math.Round(g * brightness)));
                b = Math.Min(255, Math.Max(0, (int)Math.Round(b * brightness)));
            }
            return ((byte)r, (byte)g, (byte)b);
        }

        private static int HistogramMedian(int[] hist, int count)
        {
            int rank = count / 2; // phan tu thu rank (0-based) trong day da sap xep
            int acc = 0;
            for (int v = 0; v < 256; v++)
            {
                acc += hist[v];
                if (acc > rank) return v;
            }
            return 255;
        }

        public static GifLedAnimation Convert(string path, int blackLevel = DefaultBlackLevel, double brightness = 1.0)
        {
            var anim = new GifLedAnimation { Name = Path.GetFileNameWithoutExtension(path) };
            var geo = Geometry;
            using (var img = Image.Load<Rgba32>(path))
            {
                anim.SourceWidth = img.Width;
                anim.SourceHeight = img.Height;
                anim.SourceFrames = img.Frames.Count;
                int take = Math.Min(img.Frames.Count, MaxLedFrames);
                // Neu GIF qua dai: lay mau deu de khong qua MaxLedFrames frame.
                double step = img.Frames.Count > MaxLedFrames ? (double)img.Frames.Count / MaxLedFrames : 1.0;
                for (int f = 0; f < take; f++)
                {
                    int srcIdx = (int)(f * step);
                    var frame = img.Frames[srcIdx];
                    var quads = new L99Protocol.StreamQuad[MaxKeyData.StreamSlots.Length];
                    var view = new Dictionary<int, (byte, byte, byte)>();
                    for (int s = 0; s < MaxKeyData.StreamSlots.Length; s++)
                    {
                        var slot = MaxKeyData.StreamSlots[s];
                        var g = geo[s];
                        var c = SampleKeyColor(frame, g.Fx, g.Fy, g.Kw, g.Kh, blackLevel, brightness);
                        quads[s] = new L99Protocol.StreamQuad(slot.Id, c.R, c.G, c.B);
                        view[slot.ViewIndex] = (c.R, c.G, c.B);
                    }
                    anim.Frames.Add(quads);
                    anim.ViewFrames.Add(view);
                    int delay = 100;
                    try
                    {
                        var meta = frame.Metadata.GetGifMetadata();
                        if (meta != null && meta.FrameDelay > 0) delay = meta.FrameDelay * 10; // don vi 10ms
                    }
                    catch { }
                    anim.DelaysMs.Add(Math.Max(30, Math.Min(1000, delay)));
                }
            }
            return anim;
        }
    }
}
