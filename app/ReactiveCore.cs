using System;
using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.15 — HIEU UNG GO PHIM PHAN UNG (stream 0x20): logic THUAN.
    //
    // DIEU KHOAN RIENG TU (bat buoc, theo thiet ke): su kien dau vao CHI
    // la (vi tri phim tren ban phim ao, moc thoi gian) — khong ma phim
    // ao tho (VK), khong noi dung, khong chuoi. Su kien chi song trong
    // RAM cua engine nay (gioi han 512 su kien, cu thi tu rot), khong
    // ghi log, khong ghi file, khong luu settings. Nguon su kien la hook
    // vi tri phim DUNG CHUNG cong tac opt-in voi Thong ke go phim
    // (TypingStatsService) — tat cong tac do la khong con su kien nao
    // duoc tao ra.
    //
    // Ba generator dung chung mot engine "su kien + suy giam theo thoi
    // gian": moi frame, do sang cua tung slot = tong dong gop cua cac
    // su kien chua het han, tinh tu khoang cach hinh hoc tren luoi phim
    // (CX/CY cua StreamSlots). Che do mau dung lai quy tac v3.13
    // (Don sac / Cau vong / Bang mau) nhu RealtimeEffects.
    // =====================================================================
    public static class ReactiveEffects
    {
        public static readonly string[] Names = { "Sóng lan", "Vệt sáng", "Mưa phím" };
        public const int Ripple = 0;
        public const int Trail = 1;
        public const int KeyRain = 2;

        // Thoi gian song cua mot su kien theo tung hieu ung (giay).
        public static double LifetimeSec(int effectId)
        {
            switch (effectId)
            {
                case Ripple: return 1.4;
                case Trail: return 1.1;
                case KeyRain: return 1.6;
                default: return 1.0;
            }
        }
    }

    /// <summary>Engine su kien go phim: nhan (viewIndex, t) va sinh
    /// frame stream theo hieu ung dang chon. Thread-safety: khoa noi bo
    /// (hook goi tu thread rieng, generator goi tu timer UI).</summary>
    public sealed class ReactiveEngine
    {
        private struct KeyEvent { public int View; public double T; }
        private readonly List<KeyEvent> _events = new List<KeyEvent>();
        private readonly object _gate = new object();
        public const int MaxEvents = 512;

        public int PendingCount { get { lock (_gate) return _events.Count; } }

        /// <summary>Ghi mot su kien go: CHI vi tri phim + moc thoi gian.</summary>
        public void AddEvent(int viewIndex, double t)
        {
            if (viewIndex < 0 || viewIndex >= 80) return;
            lock (_gate)
            {
                _events.Add(new KeyEvent { View = viewIndex, T = t });
                if (_events.Count > MaxEvents) _events.RemoveRange(0, _events.Count - MaxEvents);
            }
        }

        public void Clear() { lock (_gate) _events.Clear(); }

        // Hash xac dinh theo id phim (giong RealtimeEffects — dung cho
        // che do Bang mau nhat quan giua cac generator stream).
        private static double Hash01(int n)
        {
            uint x = (uint)(n * 2654435761u);
            x ^= x >> 15; x *= 2246822519u; x ^= x >> 13;
            return (x & 0xFFFFFF) / (double)0x1000000;
        }

        /// <summary>
        /// Sinh frame tai thoi diem tNow. Su kien het han bi loai trong
        /// luc sinh frame. Khong co su kien -> frame den (moi o van mang
        /// id cua chinh no, dung quy tac stream).
        /// </summary>
        public L99Protocol.StreamQuad[] Generate(int effectId, double tNow,
            (byte R, byte G, byte B) baseColor, double bright01, int colorMode)
        {
            var slots = MaxKeyData.StreamSlots;
            var quads = new L99Protocol.StreamQuad[slots.Length];
            double b = Math.Max(0, Math.Min(1, bright01));
            int mode = Math.Max(0, Math.Min(2, colorMode));
            double life = ReactiveEffects.LifetimeSec(effectId);

            KeyEvent[] evs;
            lock (_gate)
            {
                _events.RemoveAll(e => tNow - e.T > life);
                evs = _events.ToArray();
            }
            // Tam cua tung su kien (theo ViewIndex -> slot).
            var centers = new (double X, double Y)[evs.Length];
            for (int i = 0; i < evs.Length; i++)
            {
                centers[i] = (-1, -1);
                foreach (var s in slots)
                    if (s.ViewIndex == evs[i].View) { centers[i] = (s.CX, s.CY); break; }
            }

            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                double p = 0;
                for (int e = 0; e < evs.Length; e++)
                {
                    if (centers[e].X < 0) continue;
                    double age = tNow - evs[e].T;
                    if (age < 0) continue;
                    double dx = s.CX - centers[e].X;
                    double dy = s.CY - centers[e].Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double contrib = 0;
                    switch (effectId)
                    {
                        case ReactiveEffects.Ripple:
                            {
                                // Vong song lan ra tu phim vua go: sang
                                // nhat o ban kinh r = age * toc do, mo dan
                                // theo tuoi su kien.
                                double r = age * 0.55;
                                double ring = Math.Exp(-Math.Pow((dist - r) / 0.09, 2));
                                contrib = ring * Math.Max(0, 1 - age / life);
                                break;
                            }
                        case ReactiveEffects.Trail:
                            {
                                // Vet sang: phim vua go sang nhat roi mo
                                // dan theo ham mu; phim ke ben an theo
                                // khoang cach (ban kinh nho).
                                double glow = Math.Exp(-dist / 0.055);
                                contrib = glow * Math.Exp(-age / (life * 0.45));
                                break;
                            }
                        case ReactiveEffects.KeyRain:
                            {
                                // Mua phim: mot vet sang roi XUONG duoi
                                // cot cua phim vua go theo thoi gian.
                                double fall = age * 0.85; // do roi (don vi CY)
                                double dyFall = (s.CY - centers[e].Y) - fall;
                                double col = Math.Exp(-Math.Pow(dx / 0.045, 2));
                                double head = Math.Exp(-Math.Pow(dyFall / 0.10, 2));
                                // Vet phia tren dau roi mo dan (duoi mua)
                                double tail = dyFall < 0 ? Math.Exp(dyFall / 0.22) * 0.5 : 0;
                                contrib = col * Math.Max(head, tail) * Math.Max(0, 1 - age / life);
                                break;
                            }
                    }
                    if (contrib > p) p = contrib; // max, khong cong don (tranh chay sang khi go nhanh)
                }
                p = Math.Min(1, p);
                (byte R, byte G, byte B) c;
                if (p <= 0.003)
                {
                    c = (0, 0, 0);
                }
                else if (mode == ColorModes.Rainbow)
                {
                    var hc = ColorMath.HsvToRgb(s.CX * 360 - tNow * 60, 0.85, 1.0);
                    c = ColorMath.Scale(hc, b * p);
                }
                else if (mode == ColorModes.Palette)
                {
                    int idx = (int)(Hash01(s.Id * 31 + 7) * RealtimeEffects.StreamPalette.Length)
                              % RealtimeEffects.StreamPalette.Length;
                    c = ColorMath.Scale(RealtimeEffects.StreamPalette[idx], b * p);
                }
                else
                {
                    c = ColorMath.Scale(baseColor, b * p);
                }
                quads[i] = new L99Protocol.StreamQuad(s.Id, c.R, c.G, c.B);
            }
            return quads;
        }
    }
}
