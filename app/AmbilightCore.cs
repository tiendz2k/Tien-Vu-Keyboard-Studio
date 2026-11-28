using System;
using System.Collections.Generic;
using System.Linq;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.15 — AMBILIGHT: lay mau man hinh PC -> stream 0x20. File nay
    // THUAN LOGIC (test duoc tren moi nen tang): nhan buffer BGRA cua
    // khung hinh da downscale (ScreenCapture.cs chup bang GDI tren
    // Windows) roi lay mau MEDIAN theo vung tung phim — cung thuat toan
    // histogram 256 muc + chon pixel that gan trung vi nhat nhu
    // GifLedConverter.SampleKeyColor, nhung chay tren buffer tho thay vi
    // ImageSharp (ban sao co chu dich, ghi ro o day: converter GIF da
    // kiem chung KHONG bi sua). Hinh hoc o day tinh trong khong gian
    // chuan hoa cua StreamSlots (CX/CY 0..1) thay vi canvas layout
    // 900x341 cua GIF, vi "canvas" cua Ambilight la chinh man hinh.
    // =====================================================================
    public static class ScreenSampler
    {
        public const int ModeFull = 0;  // Toan man hinh
        public const int ModeEdge = 1;  // Vien man hinh
        public const int DefaultBlackLevel = 24;

        // Hinh hoc lay mau theo slot trong khong gian chuan hoa 0..1:
        // tam (Fx, Fy) + kich thuoc hop (Kw, Kh) — tinh tu buoc hang /
        // buoc cot nho nhat trong bang StreamSlots, nhu converter GIF.
        public struct SlotGeometry { public double Fx, Fy, Kw, Kh; }
        private static SlotGeometry[] _geometry;

        public static SlotGeometry[] Geometry => _geometry ??= BuildGeometry();

        private static SlotGeometry[] BuildGeometry()
        {
            var slots = MaxKeyData.StreamSlots;
            var rowYs = slots.Select(s => s.CY).Distinct().OrderBy(v => v).ToList();
            double rowPitch = double.MaxValue;
            for (int i = 1; i < rowYs.Count; i++)
                rowPitch = Math.Min(rowPitch, rowYs[i] - rowYs[i - 1]);
            if (rowPitch == double.MaxValue) rowPitch = 0.16;

            var unitByRow = new Dictionary<double, double>();
            foreach (var row in slots.GroupBy(s => s.CY))
            {
                var xs = row.Select(s => s.CX).OrderBy(v => v).ToList();
                double unit = double.MaxValue;
                for (int i = 1; i < xs.Count; i++)
                    unit = Math.Min(unit, xs[i] - xs[i - 1]);
                unitByRow[row.Key] = unit == double.MaxValue ? 0.06 : unit;
            }

            var geo = new SlotGeometry[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                geo[i] = new SlotGeometry { Fx = s.CX, Fy = s.CY, Kw = unitByRow[s.CY], Kh = rowPitch };
            }
            return geo;
        }

        /// <summary>
        /// Che do Vien: chieu tam phim (cx, cy chuan hoa) len dai vien
        /// cua man hinh — nua tren cua luoi phim -> vien tren, nua duoi
        /// -> vien duoi, cot cuc trai/phai -> vien trai/phai, o ruot noi
        /// suy ve canh gan nhat. Dai vien day 18% moi canh (tam vien o
        /// 9% tu mep). Xap xi kieu Ambilight TV: den phan ung voi mep
        /// hinh — noi mat nhin khi xem phim/choi game.
        /// </summary>
        public static (double X, double Y) EdgeProject(double cx, double cy)
        {
            const double band = 0.09; // tam dai vien (18% / 2)
            double x = Math.Max(0, Math.Min(1, cx));
            double y = Math.Max(0, Math.Min(1, cy));
            // Khoang cach toi 4 canh trong khong gian luoi phim.
            double dTop = y, dBottom = 1 - y, dLeft = x, dRight = 1 - x;
            double m = Math.Min(Math.Min(dTop, dBottom), Math.Min(dLeft, dRight));
            if (m == dTop) return (x, band);
            if (m == dBottom) return (x, 1 - band);
            if (m == dLeft) return (band, y);
            return (1 - band, y);
        }

        /// <summary>
        /// Lay mau toan bo ban phim tu mot khung BGRA (4 byte/pixel,
        /// thu tu B,G,R,A — dung dang GetDIBits tra ve). Tra ve dung
        /// thu tu MaxKeyData.StreamSlots de dua thang vao
        /// L99Protocol.BuildStreamFrameSteps.
        /// </summary>
        public static L99Protocol.StreamQuad[] SampleAll(
            ReadOnlySpan<byte> bgra, int w, int h,
            int mode = ModeFull, int blackLevel = DefaultBlackLevel, double brightness = 1.0)
        {
            var slots = MaxKeyData.StreamSlots;
            var geo = Geometry;
            var quads = new L99Protocol.StreamQuad[slots.Length];
            if (w <= 0 || h <= 0 || bgra.Length < w * h * 4)
            {
                for (int i = 0; i < slots.Length; i++)
                    quads[i] = new L99Protocol.StreamQuad(slots[i].Id, 0, 0, 0);
                return quads;
            }
            for (int i = 0; i < slots.Length; i++)
            {
                var g = geo[i];
                double fx = g.Fx, fy = g.Fy;
                if (mode == ModeEdge) (fx, fy) = EdgeProject(g.Fx, g.Fy);
                var c = SampleKeyColor(bgra, w, h, fx, fy, g.Kw, g.Kh, blackLevel, brightness);
                quads[i] = new L99Protocol.StreamQuad(slots[i].Id, c.R, c.G, c.B);
            }
            return quads;
        }

        /// <summary>Median theo kenh trong hop ±0.35×(Kw,Kh) quanh tam,
        /// chon pixel that gan mau trung vi nhat (hoa: gan tam nhat),
        /// ap blackLevel roi nhan brightness — cung cong thuc voi
        /// GifLedConverter.SampleKeyColor, doi tuong la buffer BGRA tho.</summary>
        public static (byte R, byte G, byte B) SampleKeyColor(
            ReadOnlySpan<byte> bgra, int w, int h,
            double centerX, double centerY, double keyW, double keyH,
            int blackLevel = DefaultBlackLevel, double brightness = 1.0)
        {
            double cx = centerX * (w - 1);
            double cy = centerY * (h - 1);
            double halfW = 0.35 * keyW * w;
            double halfH = 0.35 * keyH * h;
            int x0 = Math.Max(0, (int)Math.Floor(cx - halfW));
            int x1 = Math.Min(w - 1, (int)Math.Ceiling(cx + halfW));
            int y0 = Math.Max(0, (int)Math.Floor(cy - halfH));
            int y1 = Math.Min(h - 1, (int)Math.Ceiling(cy + halfH));

            var histR = new int[256];
            var histG = new int[256];
            var histB = new int[256];
            int count = 0;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int o = (y * w + x) * 4;
                    histB[bgra[o]]++; histG[bgra[o + 1]]++; histR[bgra[o + 2]]++;
                    count++;
                }
            int medR = HistogramMedian(histR, count);
            int medG = HistogramMedian(histG, count);
            int medB = HistogramMedian(histB, count);

            int bestR = medR, bestG = medG, bestB = medB;
            double bestDist = double.MaxValue, bestCenter = double.MaxValue;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int o = (y * w + x) * 4;
                    int r = bgra[o + 2], g = bgra[o + 1], b = bgra[o];
                    double d = (r - medR) * (r - medR) + (g - medG) * (g - medG) + (b - medB) * (b - medB);
                    double dc = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (d < bestDist || (d == bestDist && dc < bestCenter))
                    {
                        bestDist = d; bestCenter = dc;
                        bestR = r; bestG = g; bestB = b;
                    }
                }

            int rr = bestR, gg = bestG, bb = bestB;
            if (Math.Max(rr, Math.Max(gg, bb)) <= blackLevel)
                return (0, 0, 0);
            if (brightness != 1.0)
            {
                rr = Math.Min(255, Math.Max(0, (int)Math.Round(rr * brightness)));
                gg = Math.Min(255, Math.Max(0, (int)Math.Round(gg * brightness)));
                bb = Math.Min(255, Math.Max(0, (int)Math.Round(bb * brightness)));
            }
            return ((byte)rr, (byte)gg, (byte)bb);
        }

        private static int HistogramMedian(int[] hist, int count)
        {
            int rank = count / 2;
            int acc = 0;
            for (int v = 0; v < 256; v++)
            {
                acc += hist[v];
                if (acc > rank) return v;
            }
            return 255;
        }
    }

    // =====================================================================
    // v3.16 — BO CHINH MAU Ambilight: co che ColorAdjustment cua
    // Hyperion.ng (MIT, © Hyperion Project) — cong thuc duoc port lai
    // theo mo ta trong bao cao nghien cuu ma nguon mo: gamma tung kenh,
    // saturationGain, luminanceGain va luminanceMinimum (san do sang —
    // canh gan den nhung chua den han van phat sang nhe thay vi "chet"
    // den; den thuan 0 giu nguyen 0). Ap DUNG SAU lay mau, TRUOC lam
    // muot (dung thu tu pipeline cua Hyperion).
    // =====================================================================
    public sealed class ColorCorrectionSettings
    {
        public double GammaR = 1.0;
        public double GammaG = 1.0;
        public double GammaB = 1.0;
        public double SaturationGain = 1.15;  // LED thuong nhat hon man hinh — tang nhe mac dinh
        public double LuminanceGain = 1.0;
        public double LuminanceMinimum = 0.05; // 0..0.3 (HSL lightness)

        public static double ClampGamma(double g) => Math.Max(0.4, Math.Min(2.8, g));
        public static double ClampGain(double g) => Math.Max(0, Math.Min(2, g));
        public static double ClampLumGain(double g) => Math.Max(0.4, Math.Min(1.6, g));
        public static double ClampLumMin(double v) => Math.Max(0, Math.Min(0.3, v));

        public ColorCorrectionSettings Sanitized() => new ColorCorrectionSettings
        {
            GammaR = ClampGamma(GammaR),
            GammaG = ClampGamma(GammaG),
            GammaB = ClampGamma(GammaB),
            SaturationGain = ClampGain(SaturationGain),
            LuminanceGain = ClampLumGain(LuminanceGain),
            LuminanceMinimum = ClampLumMin(LuminanceMinimum),
        };
    }

    public static class ColorCorrection
    {
        /// <summary>Ap bo chinh mau len mot mau RGB: gamma tung kenh
        /// (tren thang 0..1) -> HSL: nhan saturation/luminance -> san
        /// luminanceMinimum (chi khi mau khong phai den thuan) -> RGB.</summary>
        public static (byte R, byte G, byte B) Apply((byte R, byte G, byte B) c, ColorCorrectionSettings s)
        {
            if (s == null) s = new ColorCorrectionSettings();
            double r = Math.Pow(c.R / 255.0, ColorCorrectionSettings.ClampGamma(s.GammaR));
            double g = Math.Pow(c.G / 255.0, ColorCorrectionSettings.ClampGamma(s.GammaG));
            double b = Math.Pow(c.B / 255.0, ColorCorrectionSettings.ClampGamma(s.GammaB));

            RgbToHsl(r, g, b, out double h, out double sat, out double lig);
            sat = Math.Max(0, Math.Min(1, sat * ColorCorrectionSettings.ClampGain(s.SaturationGain)));
            lig = Math.Max(0, Math.Min(1, lig * ColorCorrectionSettings.ClampLumGain(s.LuminanceGain)));
            double lumMin = ColorCorrectionSettings.ClampLumMin(s.LuminanceMinimum);
            if (lig > 0 && lig < lumMin) lig = lumMin;
            var rgb = HslToRgb(h, sat, lig);
            return (ColorMath.ToByte(rgb.R * 255), ColorMath.ToByte(rgb.G * 255), ColorMath.ToByte(rgb.B * 255));
        }

        /// <summary>Ap len ca khung stream (giu nguyen id tung o).</summary>
        public static L99Protocol.StreamQuad[] ApplyToFrame(
            IReadOnlyList<L99Protocol.StreamQuad> quads, ColorCorrectionSettings s)
        {
            var outp = new L99Protocol.StreamQuad[quads.Count];
            for (int i = 0; i < quads.Count; i++)
            {
                var q = quads[i];
                var c = Apply((q.R, q.G, q.B), s);
                outp[i] = new L99Protocol.StreamQuad(q.Id, c.R, c.G, c.B);
            }
            return outp;
        }

        private static void RgbToHsl(double r, double g, double b,
            out double h, out double s, out double l)
        {
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            double d = max - min;
            if (d < 1e-9) { h = 0; s = 0; return; }
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
        }

        private static (double R, double G, double B) HslToRgb(double h, double s, double l)
        {
            h = ((h % 360) + 360) % 360;
            double chroma = (1 - Math.Abs(2 * l - 1)) * s;
            double x = chroma * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - chroma / 2;
            double r, g, b;
            if (h < 60) { r = chroma; g = x; b = 0; }
            else if (h < 120) { r = x; g = chroma; b = 0; }
            else if (h < 180) { r = 0; g = chroma; b = x; }
            else if (h < 240) { r = 0; g = x; b = chroma; }
            else if (h < 300) { r = x; g = 0; b = chroma; }
            else { r = chroma; g = 0; b = x; }
            return (r + m, g + m, b + m);
        }
    }

    // =====================================================================
    // v3.16 — LAM MUOT MAU THEO THOI GIAN: co che LinearColorSmoothing
    // cua Hyperion.ng (MIT, © Hyperion Project) — moi tick xuat, tung
    // phim noi suy TUYEN TINH ve phia mau dich theo ti le dt/thoi-gian-
    // lam-muot, tach roi khoi fps chup hinh: chup 10fps van cho stream
    // 20fps muot. Khong bao gio vuot qua dich (k kep 0..1), khong dao
    // dao. smoothingMs = 0 -> nhay thang toi dich.
    // =====================================================================
    public sealed class ColorSmoother
    {
        private float[] _cur; // 3 kenh x so o, thang 0..255
        private bool _hasSize;

        public void Reset() { _hasSize = false; _cur = null; }

        /// <summary>Mot buoc noi suy: targets la khung dich moi nhat;
        /// dtSec la thoi gian that tu buoc truoc. Tra khung da lam muot
        /// (id giu nguyen theo targets).</summary>
        public L99Protocol.StreamQuad[] Step(
            IReadOnlyList<L99Protocol.StreamQuad> targets, double dtSec, int smoothingMs)
        {
            int n = targets.Count;
            if (!_hasSize || _cur == null || _cur.Length != n * 3)
            {
                _cur = new float[n * 3]; // bat dau tu den — fade-in mem nhu Hyperion
                _hasSize = true;
            }
            double k = smoothingMs <= 0 ? 1.0
                : Math.Max(0, Math.Min(1, dtSec * 1000.0 / smoothingMs));
            var outp = new L99Protocol.StreamQuad[n];
            for (int i = 0; i < n; i++)
            {
                var t = targets[i];
                int o = i * 3;
                _cur[o] += (float)((t.R - _cur[o]) * k);
                _cur[o + 1] += (float)((t.G - _cur[o + 1]) * k);
                _cur[o + 2] += (float)((t.B - _cur[o + 2]) * k);
                outp[i] = new L99Protocol.StreamQuad(t.Id,
                    ColorMath.ToByte(_cur[o]), ColorMath.ToByte(_cur[o + 1]), ColorMath.ToByte(_cur[o + 2]));
            }
            return outp;
        }
    }
}
