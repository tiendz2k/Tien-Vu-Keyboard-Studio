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
        public static readonly string[] Names =
            { "Sóng lan", "Vệt sáng", "Mưa phím", "Đa sóng", "Bản đồ nhiệt", "Chữ thập" };
        public const int Ripple = 0;
        public const int Trail = 1;
        public const int KeyRain = 2;
        // v3.16: ba hieu ung moi, hanh vi tai hien tu tai lieu cong khai
        // cua QMK RGB Matrix (GPL — chi hoc co che, tu viet lai, khong
        // sao chep ma): MULTISPLASH (nhieu xung tron dong thoi), TYPING
        // HEATMAP (dem luot go + suy giam -> ramp hue lanh-nong),
        // SOLID_REACTIVE_CROSS (sang doc hang + cot cua phim vua go).
        public const int MultiSplash = 3;
        public const int Heatmap = 4;
        public const int Cross = 5;

        // Thoi gian song cua mot su kien theo tung hieu ung (giay).
        // Heatmap khong song bang su kien (song bang bo dem nhiet) —
        // gia tri nay chi de don dep danh sach su kien dung chung.
        public static double LifetimeSec(int effectId)
        {
            switch (effectId)
            {
                case Ripple: return 1.4;
                case Trail: return 1.1;
                case KeyRain: return 1.6;
                case MultiSplash: return 1.3;
                case Cross: return 1.1;
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

        // v3.16 — BAN DO NHIET: bo dem luot go theo phim (chi so dem,
        // khong noi dung) + suy giam mu theo thoi gian. Cung dieu khoan
        // rieng tu nhu su kien: chi trong RAM, khong log/khong file.
        private readonly double[] _heat = new double[80];
        private double _heatClock = double.NaN;
        public const double HeatTauSec = 5.0;    // hang so suy giam nhiet
        public const double HeatFullCount = 15;  // so luot go de "nong" toi da
        public const double HeatCap = 40;        // tran bo dem (chong tran so)

        public int PendingCount { get { lock (_gate) return _events.Count; } }

        /// <summary>Ghi mot su kien go: CHI vi tri phim + moc thoi gian.</summary>
        public void AddEvent(int viewIndex, double t)
        {
            if (viewIndex < 0 || viewIndex >= 80) return;
            lock (_gate)
            {
                _events.Add(new KeyEvent { View = viewIndex, T = t });
                if (_events.Count > MaxEvents) _events.RemoveRange(0, _events.Count - MaxEvents);
                DecayHeatTo(t);
                _heat[viewIndex] = Math.Min(HeatCap, _heat[viewIndex] + 1);
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _events.Clear();
                Array.Clear(_heat, 0, _heat.Length);
                _heatClock = double.NaN;
            }
        }

        // Suy giam toan bo bo dem nhiet toi moc thoi gian t (mu theo
        // HeatTauSec). Goi trong khoa _gate.
        private void DecayHeatTo(double t)
        {
            if (double.IsNaN(_heatClock)) { _heatClock = t; return; }
            double dt = t - _heatClock;
            if (dt <= 0) return;
            double f = Math.Exp(-dt / HeatTauSec);
            for (int i = 0; i < _heat.Length; i++) _heat[i] *= f;
            _heatClock = t;
        }

        /// <summary>Muc nhiet 0..1 cua mot phim tai moc t (cho test +
        /// chan doan): 0 = nguoi, 1 = nong toi da.</summary>
        public double HeatLevel(int viewIndex, double t)
        {
            if (viewIndex < 0 || viewIndex >= 80) return 0;
            lock (_gate)
            {
                DecayHeatTo(t);
                return Math.Min(1, _heat[viewIndex] / HeatFullCount);
            }
        }

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
            double[] heatSnap = null;
            lock (_gate)
            {
                _events.RemoveAll(e => tNow - e.T > life);
                evs = _events.ToArray();
                if (effectId == ReactiveEffects.Heatmap)
                {
                    DecayHeatTo(tNow);
                    heatSnap = (double[])_heat.Clone();
                }
            }

            // v3.16 — BAN DO NHIET: mau khong den tu su kien ma tu bo
            // dem nhiet cua tung phim: hue chay 240 (xanh lanh) -> 0
            // (do nong) theo muc nhiet, do sang theo ca muc nhiet.
            // Ramp hue LA noi dung cua hieu ung nay nen khong qua che
            // do mau dung chung (giong generator co hue rieng o
            // RealtimeEffects).
            if (heatSnap != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var s = slots[i];
                    double hv = s.ViewIndex >= 0 && s.ViewIndex < heatSnap.Length
                        ? heatSnap[s.ViewIndex] : 0;
                    (byte R, byte G, byte B) c;
                    if (hv <= 0.05)
                    {
                        c = (0, 0, 0);
                    }
                    else
                    {
                        double p = Math.Min(1, hv / HeatFullCount);
                        var hc = ColorMath.HsvToRgb(240.0 * (1 - p), 0.9, 1.0);
                        c = ColorMath.Scale(hc, b * (0.2 + 0.8 * p));
                    }
                    quads[i] = new L99Protocol.StreamQuad(s.Id, c.R, c.G, c.B);
                }
                return quads;
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
                        case ReactiveEffects.MultiSplash:
                            {
                                // Da song: xung TRON day dac lan ra tu
                                // phim vua go — do sang la ham cua khoang
                                // cach (trong ban kinh dang lon dan theo
                                // tuoi) va thoi gian ke tu luot go; nhieu
                                // xung CONG DON (o vong lap ngoai) nen go
                                // nhanh van thay tung dot song rieng.
                                double radius = 0.10 + age * 0.55;
                                double core = dist < radius ? 1 - dist / radius : 0;
                                contrib = core * Math.Max(0, 1 - age / life);
                                break;
                            }
                        case ReactiveEffects.Cross:
                            {
                                // Chu thap: sang doc HANG va COT cua
                                // phim vua go, mo theo khoang cach do
                                // doc canh va theo tuoi; phim cheo (khong
                                // cung hang lan cot) giu toi.
                                double dArm = double.PositiveInfinity;
                                if (Math.Abs(dy) <= 0.055) dArm = Math.Min(dArm, Math.Abs(dx));
                                if (Math.Abs(dx) <= 0.035) dArm = Math.Min(dArm, Math.Abs(dy));
                                if (!double.IsInfinity(dArm))
                                    contrib = Math.Exp(-dArm / 0.28) * Math.Max(0, 1 - age / life);
                                break;
                            }
                    }
                    // Da song cong don nhieu xung; cac hieu ung con lai
                    // lay max (tranh chay sang khi go nhanh).
                    if (effectId == ReactiveEffects.MultiSplash) p += contrib;
                    else if (contrib > p) p = contrib;
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
