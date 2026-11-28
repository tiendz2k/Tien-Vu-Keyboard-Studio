using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TienKeyboardStudio3
{
    /// <summary>Trang thai RGB dung chung cho moi ban phim xem truoc.</summary>
    public sealed class RgbAnimState
    {
        public int Effect = 10;                 // 0..19, khop danh sach 20 hieu ung firmware
        public Color Brush = Color.FromRgb(0x7C, 0x3A, 0xED);
        public bool Multi;
        public double Brightness = 3;           // 0..4 (UI)
        public double Speed = 2;                // 0..4 (UI)
        public int Direction = 0;               // 0 Phai, 1 Xuong, 2 Trai, 3 Len
        public bool PaintMode;
        // Trang "Theo nhac" (mo phong): Effect = 100
        public double MusicGain = 1.0;          // do nhay 0.4..1.8
        public int MusicBg = 1;                 // 0 Tat, 1 Mo, 2 Day du
        // v3.2 — cac che do du lieu that:
        // Effect = 101: pho nhac THAT tu WASAPI (ExtBands: 23 dai, 0..1).
        public float[] ExtBands;
        // FrameColors != null: hien dung mau tung phim (GIF->LED, stream,
        // doc 0xF5) — khoa theo id phim cua KeyboardView ("k0".."k79").
        public System.Collections.Generic.IDictionary<string, Color> FrameColors;
        // Effect = 103: heatmap thong ke go phim (Heat: 0..1 theo id phim).
        public System.Collections.Generic.IDictionary<string, double> Heat;
    }

    public enum KeyboardMode { Animated, Screen }

    /// <summary>
    /// Ban phim F75 Max ve bang vector (khong dung tai san cua AULA):
    /// 80 phim dung layout that (do tu anh render chinh hang: hang so ket thuc
    /// bang Delete, hang F ket thuc bang man hinh TFT + num xoay, hang duoi
    /// Ctrl/Win/Alt 1.25u, Space 6.25u, Alt/Fn 1.25u, cum mui ten lech 0.5u).
    /// Che do Animated: chay hieu ung RGB truc tiep tren tung phim.
    /// Che do Screen: phim mo tinh, man hinh TFT phat GIF da chon (frame 128x128
    /// sau chuan hoa) - xem truoc dung cai se len man hinh that.
    /// </summary>
    public sealed class KeyboardView : Control
    {
        public sealed class KeyDef
        {
            public string Id;
            public string Label;
            public double X;      // don vi phim, tu trai vung phim
            public double W;      // don vi phim
            public int Row;
            public double CX => X + W / 2.0;
            public double CY => Row + 0.5;
        }

        // ---- layout (don vi phim), tong rong vung phim = 16u ----
        private static readonly List<KeyDef> Keys = BuildLayout();
        public static IReadOnlyList<KeyDef> AllKeys => Keys;

        private const double PadX = 0.35, PadY = 0.32, RowH = 0.92, RowGap = 0.11;
        private const double InnerW = 16.0;
        private const double TotalW = InnerW + PadX * 2;                       // 16.7
        private const double TotalH = PadY * 2 + 6 * RowH + 5 * RowGap;        // 6.71
        private const double ScreenX = PadX + 13.62, ScreenW = 1.34;
        private const double KnobCX = PadX + 15.52, KnobR = 0.40;

        private static List<KeyDef> BuildLayout()
        {
            var list = new List<KeyDef>();
            int seq = 0;
            void Row(int r, params (string Label, double W, double GapBefore)[] items)
            {
                double x = 0;
                foreach (var (label, w, gap) in items)
                {
                    x += gap;
                    list.Add(new KeyDef { Id = "k" + (seq++), Label = label, X = x, W = w, Row = r });
                    x += w;
                }
            }
            Row(0, ("Esc", 1, 0), ("F1", 1, 0), ("F2", 1, 0), ("F3", 1, 0), ("F4", 1, 0),
                   ("F5", 1, 0.25), ("F6", 1, 0), ("F7", 1, 0), ("F8", 1, 0),
                   ("F9", 1, 0.25), ("F10", 1, 0), ("F11", 1, 0), ("F12", 1, 0));
            Row(1, ("`", 1, 0), ("1", 1, 0), ("2", 1, 0), ("3", 1, 0), ("4", 1, 0), ("5", 1, 0),
                   ("6", 1, 0), ("7", 1, 0), ("8", 1, 0), ("9", 1, 0), ("0", 1, 0), ("-", 1, 0),
                   ("=", 1, 0), ("Backspace", 2, 0), ("Delete", 1, 0));
            Row(2, ("Tab", 1.5, 0), ("Q", 1, 0), ("W", 1, 0), ("E", 1, 0), ("R", 1, 0), ("T", 1, 0),
                   ("Y", 1, 0), ("U", 1, 0), ("I", 1, 0), ("O", 1, 0), ("P", 1, 0), ("[", 1, 0),
                   ("]", 1, 0), ("\\", 1.5, 0), ("PgUp", 1, 0));
            Row(3, ("Caps Lock", 1.75, 0), ("A", 1, 0), ("S", 1, 0), ("D", 1, 0), ("F", 1, 0),
                   ("G", 1, 0), ("H", 1, 0), ("J", 1, 0), ("K", 1, 0), ("L", 1, 0), (";", 1, 0),
                   ("'", 1, 0), ("Enter", 2.25, 0), ("PgDn", 1, 0));
            Row(4, ("Shift", 2.25, 0), ("Z", 1, 0), ("X", 1, 0), ("C", 1, 0), ("V", 1, 0),
                   ("B", 1, 0), ("N", 1, 0), ("M", 1, 0), (",", 1, 0), (".", 1, 0), ("/", 1, 0),
                   ("Shift", 1.75, 0), ("↑", 1, 0), ("End", 1, 0));
            Row(5, ("Ctrl", 1.25, 0), ("Win", 1.25, 0), ("Alt", 1.25, 0), ("", 6.25, 0),
                   ("Alt", 1.25, 0), ("Fn", 1.25, 0), ("←", 1, 0.5), ("↓", 1, 0), ("→", 1, 0));
            return list;
        }

        // ---- public API ----
        public KeyboardMode Mode { get; set; } = KeyboardMode.Animated;
        public RgbAnimState State { get; set; } = new RgbAnimState();
        public IDictionary<string, Color> Painted { get; set; }
        public string SelectedKeyId { get; set; }
        public bool Interactive { get; set; }
        public static bool AnimationsEnabled = true;

        public event Action<string, string> KeyClicked;
        public event Action<Bitmap> ScreenFrameChanged;

        private IList<Bitmap> _screenFrames;
        public IList<Bitmap> ScreenFrames
        {
            get => _screenFrames;
            set
            {
                _screenFrames = value;
                _screenIdx = 0;
                _screenAcc = 0;
                InvalidateVisual();
                if (value != null && value.Count > 0) ScreenFrameChanged?.Invoke(value[0]);
            }
        }
        public Bitmap CurrentScreenFrame =>
            _screenFrames != null && _screenFrames.Count > 0 ? _screenFrames[_screenIdx % _screenFrames.Count] : null;

        private Bitmap _screenImage;
        /// <summary>Bitmap tinh ghi de len man hinh TFT (dung cho widget ve phia host).</summary>
        public Bitmap ScreenImage
        {
            get => _screenImage;
            set { _screenImage = value; InvalidateVisual(); }
        }

        private readonly DispatcherTimer _timer;
        private double _t;                 // thoi gian hieu ung (giay, da nhan toc do)
        private double _screenAcc;         // tich luy de doi frame man hinh (100ms/frame)
        private int _screenIdx;
        private int _lastClockSecond = -1;
        private readonly Random _rng = new Random(1234);
        private readonly List<(string Id, double Start, Color Col)> _flashes = new List<(string, double, Color)>();

        public KeyboardView()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!AnimationsEnabled || !IsEffectivelyVisible) return;
            double speedScale = 0.35 + (State?.Speed ?? 2) * 0.45;
            bool dirty = false;

            if (Mode == KeyboardMode.Animated && State != null && !State.PaintMode && State.Effect != 0)
            {
                _t += 0.033 * speedScale;
                SpawnFlashes();
                dirty = true;
            }
            if (_screenFrames != null && _screenFrames.Count > 1)
            {
                _screenAcc += 33;
                if (_screenAcc >= 100)
                {
                    _screenAcc = 0;
                    _screenIdx = (_screenIdx + 1) % _screenFrames.Count;
                    ScreenFrameChanged?.Invoke(_screenFrames[_screenIdx]);
                    dirty = true;
                }
            }
            if (_screenFrames == null || _screenFrames.Count == 0)
            {
                int sec = DateTime.Now.Second;
                if (sec != _lastClockSecond) { _lastClockSecond = sec; dirty = true; }
            }
            if (dirty) InvalidateVisual();
        }

        /// <summary>Pho nhac tong hop cho che do Theo nhac mo phong: song pho + nhip bass dap ben trai.</summary>
        private double MusicSpectrum(double x, double t)
        {
            double v = 0.42
                + 0.30 * Math.Sin(t * 2.3 + x * 0.55)
                + 0.22 * Math.Sin(t * 3.9 - x * 1.25 + 1.7)
                + 0.18 * Math.Sin(t * 6.1 + x * 2.6);
            double beat = Math.Pow(Math.Max(0, Math.Sin(t * 4.6)), 3.0);
            v += beat * 0.38 * Math.Exp(-x / 4.5);
            v += beat * 0.15 * Math.Exp(-Math.Abs(x - 8) / 3.0);
            return v;
        }

        private void SpawnFlashes()
        {
            int eff = State.Effect;
            if (eff != 2 && eff != 3 && eff != 4 && eff != 13) { _flashes.Clear(); return; }
            _flashes.RemoveAll(f => _t - f.Start > 0.8);
            if (_flashes.Count < 7 && _rng.NextDouble() < 0.35)
            {
                var k = Keys[_rng.Next(Keys.Count)];
                Color c = eff == 4 || State.Multi
                    ? Hsv((_rng.NextDouble()), 0.8, 1.0)
                    : State.Brush;
                _flashes.Add((k.Id, _t, c));
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = availableSize.Width;
            if (double.IsInfinity(w) || w <= 0) w = 640;
            return new Size(w, w * TotalH / TotalW);
        }

        // ---- geometry ----
        private Rect KeyRect(KeyDef k, double unit)
        {
            double x = (PadX + k.X) * unit + unit * 0.035;
            double y = (PadY + k.Row * (RowH + RowGap)) * unit + unit * 0.03;
            double w = k.W * unit - unit * 0.07;
            double h = RowH * unit - unit * 0.06;
            return new Rect(x, y, w, h);
        }

        private Rect ScreenRect(double unit) =>
            new Rect(ScreenX * unit, PadY * unit - unit * 0.02, ScreenW * unit, (RowH + 0.04) * unit);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!Interactive || Bounds.Width <= 0) return;
            double unit = Bounds.Width / TotalW;
            var p = e.GetPosition(this);
            foreach (var k in Keys)
            {
                if (KeyRect(k, unit).Contains(p))
                {
                    KeyClicked?.Invoke(k.Id, k.Label);
                    e.Handled = true;
                    return;
                }
            }
        }

        // ---- mau hieu ung ----
        private Color EffectColor(KeyDef k)
        {
            var st = State;
            if (st == null) return Color.FromRgb(0x1B, 0x16, 0x33);
            if (st.PaintMode)
            {
                if (Painted != null && Painted.TryGetValue(k.Id, out var pc)) return pc;
                return Color.FromRgb(0x1B, 0x16, 0x33);
            }
            if (st.FrameColors != null)
            {
                if (st.FrameColors.TryGetValue(k.Id, out var fc)) return fc;
                return Color.FromRgb(0x10, 0x10, 0x18);
            }
            double bscale = 0.2 + 0.8 * ((st.Brightness + 1) / 5.0);
            double t = _t;
            double x = k.CX, y = k.CY;
            // v3.13: "muc" cua phim — Da sac thi moi phim mot hue cau
            // vong theo vi tri ngang (khop LightingCore.ColorModes.
            // PreviewInk), Don sac thi mau da chon. Cac hieu ung truoc
            // day chi nhan do sang tren mau don (5,7,13,15,17,18...)
            // nay dung ink nen Da sac thay ro tren xem truoc.
            Color ink = st.Multi ? Hsv(Frac(x / 16.0), 0.85, 1.0) : st.Brush;
            double dirCoord = st.Direction == 1 ? y : st.Direction == 2 ? -x : st.Direction == 3 ? -y : x;
            Color c;
            switch (st.Effect)
            {
                case 101: // Theo nhac THAT (WASAPI): cot theo 23 dai pho ngoai
                    {
                        float[] bands = st.ExtBands;
                        double spec = 0.03;
                        if (bands != null && bands.Length > 0)
                        {
                            int bi = (int)(x / 16.0 * bands.Length);
                            if (bi < 0) bi = 0; if (bi >= bands.Length) bi = bands.Length - 1;
                            spec = Math.Max(0.03, Math.Min(1.0, bands[bi] * st.MusicGain));
                        }
                        double colH = spec * 6.0;
                        double fromBottom = 5 - k.Row;
                        double frac = fromBottom / 5.0;
                        Color mc = Hsv(0.54 - 0.54 * frac, 0.85, 1.0);
                        if (fromBottom < colH)
                        {
                            double peak = 1.0 - Math.Min(1.0, colH - fromBottom);
                            return Scale(mc, (0.55 + 0.45 * peak) * bscale);
                        }
                        if (st.MusicBg == 0) return Color.FromRgb(0x10, 0x10, 0x18);
                        return Scale(mc, (st.MusicBg == 2 ? 0.30 : 0.13) * bscale);
                    }
                case 103: // Heatmap thong ke go phim: cang go nhieu cang nong
                    {
                        double h = 0;
                        if (st.Heat != null) st.Heat.TryGetValue(k.Id, out h);
                        if (h <= 0.001) return Color.FromRgb(0x14, 0x13, 0x22);
                        Color hot = h < 0.5 ? Mix(Color.FromRgb(0x7C, 0x3A, 0xED), Color.FromRgb(0xEF, 0x44, 0x44), h * 2)
                                            : Mix(Color.FromRgb(0xEF, 0x44, 0x44), Color.FromRgb(0xF5, 0xC5, 0x42), (h - 0.5) * 2);
                        return Scale(hot, (0.35 + 0.65 * h) * bscale);
                    }
                case 100: // Theo nhac (MO PHONG): cot equalizer nhay theo pho tong hop
                    {
                        double spec = Math.Max(0.04, Math.Min(1.0, MusicSpectrum(x, t) * st.MusicGain));
                        double colH = spec * 6.0;              // chieu cao cot tinh bang hang
                        double fromBottom = 5 - k.Row;         // 0 = hang duoi cung
                        double frac = fromBottom / 5.0;
                        Color mc = Hsv(0.54 - 0.54 * frac, 0.85, 1.0); // cyan -> tim -> hong/do
                        if (fromBottom < colH)
                        {
                            double peak = 1.0 - Math.Min(1.0, colH - fromBottom);
                            return Scale(mc, (0.55 + 0.45 * peak) * bscale);
                        }
                        if (st.MusicBg == 0) return Color.FromRgb(0x10, 0x10, 0x18);
                        return Scale(mc, (st.MusicBg == 2 ? 0.30 : 0.13) * bscale);
                    }
                case 0: return Color.FromRgb(0x10, 0x10, 0x18);
                case 1: c = st.Brush; break;
                case 2: // Single On: nen tat, phim ngau nhien sang roi tat dan (mo phong go phim)
                    c = Color.FromRgb(0x10, 0x10, 0x18);
                    foreach (var f in _flashes)
                        if (f.Id == k.Id) c = Mix(c, f.Col, Math.Max(0, 1 - (t - f.Start) / 0.8));
                    return Scale(c, bscale);
                case 3: // Single Off: nen sang, phim ngau nhien tat dan
                    c = ink;
                    foreach (var f in _flashes)
                        if (f.Id == k.Id) c = Mix(c, Color.FromRgb(0x10, 0x10, 0x18), Math.Max(0, 1 - (t - f.Start) / 0.8));
                    return Scale(c, bscale);
                case 4: // Glittering: lap lanh ngau nhien nhieu mau
                    c = Color.FromRgb(0x14, 0x12, 0x24);
                    foreach (var f in _flashes)
                        if (f.Id == k.Id) c = Mix(c, f.Col, Math.Max(0, 1 - (t - f.Start) / 0.8));
                    return Scale(c, bscale);
                case 5: // Falling: mua roi xuong theo cot
                    {
                        double ph = Frac(y * 0.22 - t * 0.55 + Hash(k.Id) * 0.35);
                        double v = Math.Pow(Math.Max(0, 1 - ph), 2.2);
                        c = Scale(ink, 0.12 + v);
                        return Scale(c, bscale);
                    }
                case 6: // Colourful: cau vong theo cot
                    c = Hsv(Frac(x / 16.0), 0.85, 1.0); return Scale(c, bscale);
                case 7: // Breathing
                    c = Scale(ink, 0.15 + 0.85 * (0.5 + 0.5 * Math.Sin(t * 2.2))); return Scale(c, bscale);
                case 8: // Spectrum: doi mau toan ban
                    c = Hsv(Frac(t * 0.06), 0.85, 1.0); return Scale(c, bscale);
                case 9: // Outward: song lan tu tam ra
                    {
                        double d = Dist(x, y, 8, 3);
                        double v = 0.5 + 0.5 * Math.Sin(d * 1.6 - t * 4.0);
                        c = Scale(st.Multi ? Hsv(Frac(d / 9.0 + t * 0.03), 0.85, 1) : st.Brush, 0.25 + 0.75 * v);
                        return Scale(c, bscale);
                    }
                case 10: // Wave: cau vong chay cheo
                    c = Hsv(Frac((x + y * 1.6) / 18.0 - t * 0.12), 0.85, 1.0); return Scale(c, bscale);
                case 11: // Rolling: dai mau chay ngang
                    c = Hsv(Frac(dirCoord / 7.0 - t * 0.15), 0.9, 1.0); return Scale(c, bscale);
                case 12: // Rotating: cau vong xoay quanh tam
                    {
                        double ang = Math.Atan2(y - 3, x - 8) / (2 * Math.PI);
                        c = Hsv(Frac(ang + t * 0.08), 0.85, 1.0); return Scale(c, bscale);
                    }
                case 13: // Explode: bung sang roi tan (mo phong bang flash mau co)
                    c = Scale(ink, 0.25);
                    foreach (var f in _flashes)
                        if (f.Id == k.Id) c = Mix(c, Colors.White, Math.Max(0, 1 - (t - f.Start) / 0.8) * 0.7);
                    return Scale(c, bscale);
                case 14: // Launch: song chay len tren
                    {
                        double v = 0.5 + 0.5 * Math.Sin((6 - y) * 1.8 - t * 5.0);
                        c = Scale(st.Multi ? Hsv(Frac(y / 7.0 - t * 0.05), 0.85, 1) : st.Brush, 0.25 + 0.75 * v);
                        return Scale(c, bscale);
                    }
                case 15: // Ripples: gon dong tam
                    {
                        double d = Dist(x, y, 8, 3);
                        double v = Math.Pow(0.5 + 0.5 * Math.Sin(d * 2.4 - t * 4.5), 2.0);
                        c = Scale(ink, 0.15 + 0.85 * v); return Scale(c, bscale);
                    }
                case 16: // Flowing: sang chay theo huong
                    {
                        double v = 0.5 + 0.5 * Math.Sin(dirCoord * 1.1 - t * 3.4);
                        c = Scale(st.Multi ? Hsv(Frac(dirCoord / 12.0 - t * 0.07), 0.85, 1) : st.Brush, 0.3 + 0.7 * v);
                        return Scale(c, bscale);
                    }
                case 17: // Pulsating: nhip manh
                    {
                        double s = Math.Sin(t * 3.2);
                        c = Scale(ink, 0.2 + 0.8 * Math.Pow(Math.Max(0, s), 1.6)); return Scale(c, bscale);
                    }
                case 18: // Tilt: dai sang quet cheo
                    {
                        double v = Math.Pow(0.5 + 0.5 * Math.Sin((x * 0.8 + y) * 0.9 - t * 2.6), 1.5);
                        c = Scale(ink, 0.2 + 0.8 * v); return Scale(c, bscale);
                    }
                case 19: // Shuttle: dai sang chay qua lai
                    {
                        double pos = 8 + 7.2 * Math.Sin(t * 1.5);
                        double v = Math.Max(0, 1 - Math.Abs(x - pos) / 2.6);
                        c = Scale(st.Multi ? Hsv(Frac(pos / 16.0), 0.85, 1) : st.Brush, 0.15 + 0.85 * v * v);
                        return Scale(c, bscale);
                    }
                default:
                    c = st.Multi ? Hsv(Frac(x / 16.0), 0.85, 1.0) : st.Brush;
                    return Scale(c, bscale);
            }
            if (st.Multi && st.Effect == 1)
                c = Hsv(Frac(x / 16.0), 0.85, 1.0);
            return Scale(c, bscale);
        }

        // ---- ve ----
        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 10) return;
            double unit = w / TotalW;

            // Bong do xuong "mat ban" + quang sang LED phan chieu quanh ban phim
            var shadowRect = new Rect(unit * 0.02, unit * 0.34, w - unit * 0.04, TotalH * unit - unit * 0.12);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x73, 0x00, 0x00, 0x00)), null,
                shadowRect, (float)(unit * 0.5), (float)(unit * 0.5));
            if (Mode == KeyboardMode.Animated && State != null && State.Effect != 0)
            {
                Color halo = (State.Effect == 6 || State.Effect == 8 || State.Effect == 10 ||
                              State.Effect == 11 || State.Effect == 12 || State.Effect == 100)
                    ? Hsv(Frac(_t * 0.05), 0.65, 1.0)
                    : State.Brush;
                var haloBrush = new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0x30, halo.R, halo.G, halo.B), 0),
                        new GradientStop(Color.FromArgb(0x00, halo.R, halo.G, halo.B), 1),
                    },
                };
                ctx.DrawEllipse(haloBrush, null,
                    new Point(w / 2, TotalH * unit * 0.55), w * 0.56, TotalH * unit * 0.62);
            }

            // Vo may (case): nhom phay xuoc - gradient kim loai bac xam trung tinh
            var caseRect = new Rect(unit * 0.05, unit * 0.05, w - unit * 0.10, TotalH * unit - unit * 0.10);
            var caseBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.35, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0x8B, 0x88, 0x97), 0),
                    new GradientStop(Color.FromRgb(0x59, 0x56, 0x66), 0.45),
                    new GradientStop(Color.FromRgb(0x35, 0x33, 0x3E), 1),
                },
            };
            ctx.DrawRectangle(caseBrush, new Pen(new SolidColorBrush(Color.FromRgb(0xA8, 0xA3, 0xB8)), 1.2),
                caseRect, (float)(unit * 0.42), (float)(unit * 0.42));
            // van phay xuoc: cac duong ngang rat mo tren be mat vo
            using (ctx.PushClip(new RoundedRect(caseRect, (float)(unit * 0.42), (float)(unit * 0.42))))
            {
                var streakLight = new Pen(new SolidColorBrush(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)), 1);
                var streakDark = new Pen(new SolidColorBrush(Color.FromArgb(0x0B, 0x00, 0x00, 0x00)), 1);
                for (double yy = caseRect.Y + 3; yy < caseRect.Bottom; yy += 3.2)
                {
                    var pen = ((int)(yy / 3.2) % 2 == 0) ? streakLight : streakDark;
                    ctx.DrawLine(pen, new Point(caseRect.X, yy), new Point(caseRect.Right, yy));
                }
            }
            // tam nen trong
            var plateRect = new Rect(PadX * unit * 0.55, PadY * unit * 0.55,
                w - PadX * unit * 1.1, TotalH * unit - PadY * unit * 1.1);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x38, 0x00, 0x00, 0x00)), null,
                plateRect, (float)(unit * 0.34), (float)(unit * 0.34));

            var typeface = LegendTypeface();
            foreach (var k in Keys)
            {
                var rect = KeyRect(k, unit);
                Color c = Mode == KeyboardMode.Screen && State != null && !State.PaintMode
                    ? Scale(State.Brush, 0.30)
                    : EffectColor(k);
                DrawKey(ctx, rect, unit, c, k.Label, typeface,
                    SelectedKeyId != null && SelectedKeyId == k.Id);
            }

            DrawScreen(ctx, unit, typeface);
            DrawKnob(ctx, unit);
        }

        private static readonly Typeface LegendTf = new Typeface(
            new FontFamily("avares://TienKeyboardStudio/Assets/Fonts#Be Vietnam Pro"),
            FontStyle.Normal, FontWeight.SemiBold, FontStretch.Normal);

        private Typeface LegendTypeface() => LegendTf;

        // Mau keycap trang nga (ivory) - hang so dung chung cho moi phim
        private static readonly Color IvoryTop = Color.FromRgb(0xFF, 0xFF, 0xFE);
        private static readonly Color IvoryFace = Color.FromRgb(0xF1, 0xEF, 0xE9);
        private static readonly Color IvorySkirtTop = Color.FromRgb(0xDC, 0xD8, 0xCF);
        private static readonly Color IvorySkirtBottom = Color.FromRgb(0xA3, 0x9E, 0x93);
        private static readonly Color LegendDark = Color.FromRgb(0x2B, 0x24, 0x40);

        private void DrawKey(DrawingContext ctx, Rect rect, double unit, Color c, string label, Typeface tf, bool selected)
        {
            float rad = (float)(unit * 0.16);
            double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

            // Quang LED bloom mem lan ra quanh/duoi phim theo mau hieu ung
            if (lum > 0.07)
            {
                ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(lum * 44), c.R, c.G, c.B)), null,
                    rect.Inflate(unit * 0.17), rad + 3, rad + 3);
                ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(lum * 92), c.R, c.G, c.B)), null,
                    rect.Inflate(unit * 0.075), rad + 2, rad + 2);
            }

            // Hong phim (skirt): nga xam 3D, day nhuom mau LED theo do sang
            double skirtTint = 0.04 + 0.34 * lum;
            var skirt = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Mix(IvorySkirtTop, c, skirtTint * 0.55), 0),
                    new GradientStop(Mix(IvorySkirtBottom, c, skirtTint), 1),
                },
            };
            ctx.DrawRectangle(skirt, new Pen(new SolidColorBrush(Color.FromArgb(0x38, 0x00, 0x00, 0x00)), 0.75), rect, rad, rad);
            // bong trong o day phim
            var innerShade = new Rect(rect.X + 1, rect.Bottom - rect.Height * 0.16, rect.Width - 2, rect.Height * 0.13);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x2A, 0x00, 0x00, 0x00)), null,
                innerShade, rad * 0.6f, rad * 0.6f);

            // Mat tren phim: trang nga sang, nhuom nhe mau LED dan xuong day
            var face = new Rect(rect.X + rect.Width * 0.055, rect.Y + rect.Height * 0.05,
                rect.Width * 0.89, rect.Height * 0.76);
            double faceTint = 0.03 + 0.40 * lum;
            var faceBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Mix(IvoryTop, c, faceTint * 0.45), 0),
                    new GradientStop(Mix(IvoryFace, c, faceTint), 1),
                },
            };
            ctx.DrawRectangle(faceBrush, null, face, rad * 0.8f, rad * 0.8f);
            // duong sang tren cung cua mat phim
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF)), 1),
                new Point(face.X + 2, face.Y + 0.7), new Point(face.Right - 2, face.Y + 0.7));

            if (selected)
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)), 2),
                    rect, rad, rad);

            if (!string.IsNullOrEmpty(label))
            {
                double fs = Math.Min(unit * 0.30, face.Height * 0.52);
                if (label.Length > 4) fs *= 4.0 / Math.Min(label.Length, 9) * 1.55;
                if (label == "Backspace") fs = unit * 0.20;
                var ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, tf, fs, new SolidColorBrush(LegendDark));
                while (ft.Width > face.Width * 0.94 && fs > 5)
                {
                    fs -= 0.7;
                    ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, tf, fs, new SolidColorBrush(LegendDark));
                }
                ctx.DrawText(ft, new Point(face.X + (face.Width - ft.Width) / 2, face.Y + (face.Height - ft.Height) / 2));
            }
        }

        private void DrawScreen(DrawingContext ctx, double unit, Typeface tf)
        {
            var bezel = ScreenRect(unit);
            float rad = (float)(unit * 0.10);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x0A)),
                new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x5E)), 1), bezel, rad, rad);
            var inner = bezel.Deflate(unit * 0.045);
            var frame = _screenImage ?? CurrentScreenFrame;
            if (frame != null)
            {
                using (ctx.PushClip(new RoundedRect(inner, rad * 0.7f, rad * 0.7f)))
                    ctx.DrawImage(frame, inner);
            }
            else
            {
                var bg = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromRgb(0x0B, 0x0E, 0x1A), 0),
                        new GradientStop(Color.FromRgb(0x15, 0x19, 0x2B), 1),
                    },
                };
                using (ctx.PushClip(new RoundedRect(inner, rad * 0.7f, rad * 0.7f)))
                {
                    ctx.DrawRectangle(bg, null, inner);
                    string time = DateTime.Now.ToString("HH:mm");
                    double fs = unit * 0.34;
                    var ft = new FormattedText(time, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, tf, fs, new SolidColorBrush(Color.FromRgb(0xED, 0xF0, 0xFF)));
                    ctx.DrawText(ft, new Point(inner.X + (inner.Width - ft.Width) / 2, inner.Y + inner.Height * 0.20));
                    var ft2 = new FormattedText("F75 MAX · 128×128", System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, tf, unit * 0.115, new SolidColorBrush(Color.FromRgb(0x8B, 0x93, 0xB8)));
                    ctx.DrawText(ft2, new Point(inner.X + (inner.Width - ft2.Width) / 2, inner.Y + inner.Height * 0.68));
                }
            }
        }

        private void DrawKnob(DrawingContext ctx, double unit)
        {
            var center = new Point(KnobCX * unit, (PadY + RowH / 2) * unit);
            double r = KnobR * unit;
            // vong tick kim loai mo quanh num
            var tickPen = new Pen(new SolidColorBrush(Color.FromRgb(0xC9, 0xC4, 0xD6)), 1);
            for (int i = 0; i < 12; i++)
            {
                double a = i * Math.PI / 6;
                var p1 = new Point(center.X + Math.Cos(a) * r * 1.10, center.Y + Math.Sin(a) * r * 1.10);
                var p2 = new Point(center.X + Math.Cos(a) * r * 1.20, center.Y + Math.Sin(a) * r * 1.20);
                ctx.DrawLine(tickPen, p1, p2);
            }
            // than num: kim loai bac xoay
            var outer = new RadialGradientBrush
            {
                Center = new RelativePoint(0.38, 0.32, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.38, 0.32, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.78, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.78, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xEC, 0xEB, 0xF1), 0),
                    new GradientStop(Color.FromRgb(0xA3, 0xA0, 0xB0), 0.55),
                    new GradientStop(Color.FromRgb(0x5E, 0x5B, 0x6E), 0.86),
                    new GradientStop(Color.FromRgb(0x3A, 0x37, 0x47), 1),
                },
            };
            ctx.DrawEllipse(outer, new Pen(new SolidColorBrush(Color.FromRgb(0x1E, 0x1C, 0x28)), 1.1),
                center, r, r);
            // van tron dong tam tren be mat num
            var grooveDark = new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x00, 0x00)), 1);
            var grooveLight = new Pen(new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)), 1);
            foreach (double gr in new[] { 0.86, 0.72, 0.58 })
            {
                ctx.DrawEllipse(null, grooveDark, center, r * gr, r * gr);
                ctx.DrawEllipse(null, grooveLight, new Point(center.X, center.Y + 0.8), r * gr, r * gr);
            }
            // nap giua
            var cap = new RadialGradientBrush
            {
                Center = new RelativePoint(0.4, 0.35, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.4, 0.35, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.8, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.8, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xD6, 0xD3, 0xDE), 0),
                    new GradientStop(Color.FromRgb(0x7E, 0x7B, 0x8C), 1),
                },
            };
            ctx.DrawEllipse(cap, null, center, r * 0.46, r * 0.46);
            double ia = -0.7;
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x3B, 0x2A, 0x6E)), 2),
                center, new Point(center.X + Math.Cos(ia) * r * 0.40, center.Y + Math.Sin(ia) * r * 0.40));
        }

        // ---- helpers ----
        private static double Frac(double v) => v - Math.Floor(v);
        private static double Dist(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2, dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        private static double Hash(string s)
        {
            int h = 0;
            foreach (char ch in s) h = (h * 31 + ch) % 997;
            return h / 997.0;
        }
        public static Color Scale(Color c, double f) => Color.FromRgb(
            (byte)Math.Min(255, c.R * f), (byte)Math.Min(255, c.G * f), (byte)Math.Min(255, c.B * f));
        public static Color Mix(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }
        public static Color Hsv(double h, double s, double v)
        {
            double hh = Frac(h) * 6.0;
            int i = (int)hh;
            double f = hh - i;
            double p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            double r, g, b;
            switch (i % 6)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
    }
}
