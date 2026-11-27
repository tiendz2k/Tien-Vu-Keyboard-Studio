using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TienKeyboardStudio3
{
    public static class UiBrushes
    {
        // Cap mau chu de hien tai (v3.3): ApplyAccent trong MainWindow cap nhat
        // hai bien nay + brush tai nguyen dung chung; brush tao tu code sau do
        // se dung cap mau moi.
        public static Color AccentA = Color.FromRgb(0x7C, 0x3A, 0xED);
        public static Color AccentB = Color.FromRgb(0x22, 0xD3, 0xEE);

        public static LinearGradientBrush AccentGradient() => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(AccentA, 0),
                new GradientStop(AccentB, 1),
            },
        };
        public static readonly SolidColorBrush GlassBorder = new SolidColorBrush(Color.FromArgb(0x17, 0xFF, 0xFF, 0xFF));
        public static readonly SolidColorBrush CardFill = new SolidColorBrush(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
        public static readonly SolidColorBrush TextSecondary = new SolidColorBrush(Color.FromRgb(0xA6, 0xA3, 0xB8));
    }

    /// <summary>Cong tac pill truot muot (ve tay, co animation num truot).</summary>
    public sealed class PillSwitch : Control
    {
        private bool _checked;
        private double _pos; // 0..1 vi tri num
        private readonly DispatcherTimer _anim;

        public bool IsChecked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                _anim.Start();
                Changed?.Invoke(value);
            }
        }
        public event Action<bool> Changed;

        public PillSwitch()
        {
            Width = 48; Height = 27;
            Cursor = new Cursor(StandardCursorType.Hand);
            _anim = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _anim.Tick += (s, e) =>
            {
                double target = _checked ? 1 : 0;
                _pos += (target - _pos) * 0.30;
                if (Math.Abs(target - _pos) < 0.01) { _pos = target; _anim.Stop(); }
                InvalidateVisual();
            };
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            IsChecked = !IsChecked;
            e.Handled = true;
        }

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
            IBrush track = _pos > 0.03
                ? (IBrush)UiBrushes.AccentGradient()
                : new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x38));
            ctx.DrawRectangle(track, null, rect, (float)(rect.Height / 2), (float)(rect.Height / 2));
            double kx = 3.5 + _pos * (rect.Width - rect.Height);
            var knobRect = new Rect(kx, 3.5, rect.Height - 7, rect.Height - 7);
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)), null,
                new Point(knobRect.Center.X, knobRect.Center.Y + 1.2), knobRect.Width / 2, knobRect.Height / 2);
            ctx.DrawEllipse(Brushes.White, null, knobRect.Center, knobRect.Width / 2, knobRect.Height / 2);
        }
    }

    /// <summary>
    /// The GIF trong luoi: hien frame dau; hover hoac dang chon thi phat dong
    /// (tai frame luoi bien, toi da 24 frame 96px). Bam de chon.
    /// </summary>
    public sealed class GifCard : Border
    {
        private readonly Avalonia.Controls.Image _img;
        private readonly TextBlock _label;
        private List<Bitmap> _frames;
        private int _idx;
        private bool _hover;
        private readonly DispatcherTimer _timer;

        public GifEntry Entry { get; }
        public int FrameSize { get; set; } = 96;
        public int MaxFrames { get; set; } = 24;
        public Func<GifEntry, Task<List<Bitmap>>> FramesLoader { get; set; }
        public Action<GifCard> Clicked { get; set; }

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set { _selected = value; UpdateVisual(); if (value) StartAnim(); else if (!_hover) StopAnim(); }
        }

        public GifCard(GifEntry entry, string subLabel = null)
        {
            Entry = entry;
            Classes.Add("pickcard");
            Cursor = new Cursor(StandardCursorType.Hand);
            _img = new Avalonia.Controls.Image
            {
                Width = FrameSize, Height = FrameSize,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            };
            _label = new TextBlock
            {
                Text = entry.Name,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xD5, 0xE6)),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = FrameSize + 14,
            };
            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(_img);
            stack.Children.Add(_label);
            if (!string.IsNullOrEmpty(subLabel))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = subLabel,
                    FontSize = 9.5,
                    Foreground = UiBrushes.TextSecondary,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = FrameSize + 14,
                });
            }
            Child = stack;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (s, e) =>
            {
                if (_frames == null || _frames.Count <= 1) return;
                _idx = (_idx + 1) % _frames.Count;
                _img.Source = _frames[_idx];
            };

            PointerEntered += (s, e) => { _hover = true; StartAnim(); };
            PointerExited += (s, e) => { _hover = false; if (!_selected) StopAnim(); };
            PointerPressed += (s, e) => { Clicked?.Invoke(this); };
            UpdateVisual();
        }

        public void SetFirstFrame(Bitmap bmp) { _img.Source = bmp; }

        private void UpdateVisual()
        {
            if (_selected)
            {
                BorderBrush = UiBrushes.AccentGradient();
                BorderThickness = new Thickness(2);
                Background = new SolidColorBrush(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF));
            }
            else
            {
                BorderBrush = UiBrushes.GlassBorder;
                BorderThickness = new Thickness(1);
                Background = UiBrushes.CardFill;
            }
        }

        private async void StartAnim()
        {
            if (_frames == null && FramesLoader != null)
            {
                try { _frames = await FramesLoader(Entry); } catch { _frames = null; }
                if (_frames != null && _frames.Count > 0) _img.Source = _frames[0];
            }
            if (!_timer.IsEnabled) _timer.Start();
        }

        private void StopAnim()
        {
            _timer.Stop();
            if (_frames != null && _frames.Count > 0) { _idx = 0; _img.Source = _frames[0]; }
        }
    }

    /// <summary>
    /// O xem truoc hieu ung RGB dang dai gradient; chi chay dong khi dang chon
    /// hoac hover de tiet kiem CPU.
    /// </summary>
    public sealed class EffectPreview : Control
    {
        public new int Effect;
        public Color BaseColor = Color.FromRgb(0x7C, 0x3A, 0xED);
        private bool _active;
        public bool Active
        {
            get => _active;
            set { _active = value; if (value) _timer.Start(); else if (!_hover) _timer.Stop(); InvalidateVisual(); }
        }
        private bool _hover;
        private double _t;
        private readonly DispatcherTimer _timer;
        private readonly Random _rng = new Random(77);

        public EffectPreview()
        {
            Height = 34;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _timer.Tick += (s, e) => { _t += 0.05; InvalidateVisual(); };
            PointerEntered += (s, e) => { _hover = true; _timer.Start(); };
            PointerExited += (s, e) => { _hover = false; if (!_active) _timer.Stop(); };
        }

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            var rect = new Rect(Bounds.Size);
            using (ctx.PushClip(new RoundedRect(rect, 8, 8)))
            {
                ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x1A)), rect);
                const int bars = 14;
                double bw = rect.Width / bars;
                for (int i = 0; i < bars; i++)
                {
                    Color c = BarColor(i, bars);
                    var br = new Rect(i * bw + 1, 3, bw - 2, rect.Height - 6);
                    ctx.DrawRectangle(new SolidColorBrush(c), null, br, 3, 3);
                }
            }
        }

        private Color BarColor(int i, int n)
        {
            double x = (double)i / n;
            switch (Effect)
            {
                case 0: return Color.FromRgb(0x24, 0x24, 0x2E);
                case 1: return BaseColor;
                case 6: return KeyboardView.Hsv(x, 0.85, 1);
                case 8: return KeyboardView.Hsv(Frac(_t * 0.08), 0.85, 1);
                case 10: return KeyboardView.Hsv(Frac(x - _t * 0.13), 0.85, 1);
                case 11: return KeyboardView.Hsv(Frac(x * 1.6 - _t * 0.16), 0.9, 1);
                case 12: return KeyboardView.Hsv(Frac(x + _t * 0.09), 0.85, 1);
                case 7: return KeyboardView.Scale(BaseColor, 0.25 + 0.75 * (0.5 + 0.5 * Math.Sin(_t * 2.4)));
                case 17: return KeyboardView.Scale(BaseColor, 0.2 + 0.8 * Math.Pow(Math.Max(0, Math.Sin(_t * 3.4)), 1.6));
                case 4:
                    return _rng.NextDouble() < 0.16
                        ? KeyboardView.Hsv(_rng.NextDouble(), 0.8, 1)
                        : Color.FromRgb(0x1B, 0x19, 0x28);
                case 2: return _rng.NextDouble() < 0.10 ? BaseColor : Color.FromRgb(0x1B, 0x19, 0x28);
                case 3: return _rng.NextDouble() < 0.10 ? Color.FromRgb(0x1B, 0x19, 0x28) : BaseColor;
                default:
                    double v = 0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(x * 9 - _t * 3.2));
                    return KeyboardView.Scale(BaseColor, v);
            }
        }

        private static double Frac(double v) => v - Math.Floor(v);
    }

    /// <summary>Ma QR mo phong (trang tri): luoi 25×25 o vuong + 3 o dinh vi goc.</summary>
    public sealed class QrMock : Control
    {
        private readonly bool[,] _cells = new bool[25, 25];

        public QrMock()
        {
            var rng = new Random(4826);
            for (int y = 0; y < 25; y++)
                for (int x = 0; x < 25; x++)
                    _cells[x, y] = rng.NextDouble() < 0.44;
            // xoa vung 3 goc cho o dinh vi
            ClearCorner(0, 0); ClearCorner(17, 0); ClearCorner(0, 17);
        }

        private void ClearCorner(int cx, int cy)
        {
            for (int y = cy; y < cy + 8 && y < 25; y++)
                for (int x = cx; x < cx + 8 && x < 25; x++)
                    _cells[x, y] = false;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double s = Math.Min(
                double.IsInfinity(availableSize.Width) ? 168 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 168 : availableSize.Height);
            return new Size(s, s);
        }

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            double s = Math.Min(Bounds.Width, Bounds.Height);
            var bg = new Rect(0, 0, s, s);
            ctx.DrawRectangle(Brushes.White, null, bg, 10, 10);
            double cell = s / 25.0;
            var ink = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x20));
            for (int y = 0; y < 25; y++)
                for (int x = 0; x < 25; x++)
                    if (_cells[x, y])
                        ctx.DrawRectangle(ink, null, new Rect(x * cell + 0.4, y * cell + 0.4, cell - 0.8, cell - 0.8), 1, 1);
            DrawFinder(ctx, 0, 0, cell, ink);
            DrawFinder(ctx, 17 * cell, 0, cell, ink);
            DrawFinder(ctx, 0, 17 * cell, cell, ink);
        }

        private static void DrawFinder(DrawingContext ctx, double ox, double oy, double cell, IBrush ink)
        {
            ctx.DrawRectangle(ink, null, new Rect(ox, oy, cell * 7, cell * 7), 2, 2);
            ctx.DrawRectangle(Brushes.White, null, new Rect(ox + cell, oy + cell, cell * 5, cell * 5), 1, 1);
            ctx.DrawRectangle(ink, null, new Rect(ox + cell * 2, oy + cell * 2, cell * 3, cell * 3), 1, 1);
        }
    }

    /// <summary>
    /// Vet sang quet ngang mot hang "phim" (24 o) dung cho man hinh chao
    /// (port tu Preview v0.10): dai sang chay trai -> phai trong ~1.6 giay roi
    /// mo dan. AmberMode = phong cach 550C: cung vet quet nhung tong ho phach.
    /// </summary>
    public sealed class GreetingSweep : Control
    {
        private readonly DispatcherTimer _timer;
        private double _t; // tien trinh 0..1
        public bool Running { get; private set; }
        public bool AmberMode { get; set; }

        public GreetingSweep()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _timer.Tick += (s, e) =>
            {
                _t += 0.030 / 1.6;
                if (_t >= 1.15) { _t = 1.15; Stop(); }
                InvalidateVisual();
            };
        }

        public void Play() { _t = 0; Running = true; _timer.Start(); InvalidateVisual(); }
        public void Stop() { Running = false; _timer.Stop(); }

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            var rect = new Rect(Bounds.Size);
            const int cells = 24;
            double gap = 3;
            double cw = (rect.Width - gap * (cells - 1)) / cells;
            double head = _t * 1.3 - 0.15; // vi tri dai sang (don vi: ti le chieu rong)
            for (int i = 0; i < cells; i++)
            {
                double x = i * (cw + gap);
                double cx = (x + cw / 2) / rect.Width;
                double d = Math.Abs(cx - head);
                double glow = Math.Max(0, 1 - d / 0.16);
                glow = glow * glow;
                Color c;
                if (glow <= 0.02)
                    c = AmberMode ? Color.FromRgb(0x2A, 0x1E, 0x0C) : Color.FromRgb(0x1D, 0x1B, 0x2A);
                else if (AmberMode)
                {
                    // Ho phach CRT: sang dan tu nau sam -> vang ho phach
                    double v = 0.25 + 0.75 * glow;
                    c = Color.FromRgb(
                        (byte)Math.Min(255, 255 * v),
                        (byte)Math.Min(255, 176 * v),
                        (byte)Math.Min(255, 32 * v));
                }
                else c = KeyboardView.Hsv(cx, 0.85, 0.25 + 0.75 * glow);
                var cell = new Rect(x, 2, cw, rect.Height - 4);
                ctx.DrawRectangle(new SolidColorBrush(c), null, cell, 5, 5);
                if (glow > 0.05)
                {
                    var halo = new Rect(x - 3, -1, cw + 6, rect.Height + 2);
                    ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(70 * glow), c.R, c.G, c.B)), null, halo, 8, 8);
                }
            }
        }
    }

    /// <summary>
    /// Vong reticle xoay lien tuc (theme Rhine Lab): vong tron + vach chia +
    /// hai cung xoay nguoc chieu. Play/Stop nhu GreetingSweep.
    /// </summary>
    public sealed class ReticleControl : Control
    {
        private readonly DispatcherTimer _timer;
        private double _angle;
        public bool Running { get; private set; }
        public Color RingColor = Color.FromRgb(0xE8, 0xB4, 0x6A);

        public ReticleControl()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _timer.Tick += (s, e) => { _angle += 0.045; InvalidateVisual(); };
        }

        public void Play() { Running = true; _timer.Start(); InvalidateVisual(); }
        public void Stop() { Running = false; _timer.Stop(); }

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            double s = Math.Min(Bounds.Width, Bounds.Height);
            if (s <= 8) return;
            var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var pen = new Pen(new SolidColorBrush(RingColor), 1.4);
            var penDim = new Pen(new SolidColorBrush(Color.FromArgb(0x66, RingColor.R, RingColor.G, RingColor.B)), 1);
            ctx.DrawEllipse(null, penDim, c, s * 0.48, s * 0.48);
            ctx.DrawEllipse(null, pen, c, s * 0.34, s * 0.34);
            // vach chia xoay theo _angle
            for (int i = 0; i < 12; i++)
            {
                double a = _angle + i * Math.PI / 6;
                var p1 = new Point(c.X + Math.Cos(a) * s * 0.40, c.Y + Math.Sin(a) * s * 0.40);
                var p2 = new Point(c.X + Math.Cos(a) * s * 0.48, c.Y + Math.Sin(a) * s * 0.48);
                ctx.DrawLine(i % 3 == 0 ? pen : penDim, p1, p2);
            }
            // hai cung xoay nguoc chieu (ve bang doan thang ngan)
            DrawArc(ctx, c, s * 0.41, -_angle * 1.7, 1.9, pen);
            DrawArc(ctx, c, s * 0.27, _angle * 2.3 + 1.2, 1.4, pen);
            ctx.DrawEllipse(new SolidColorBrush(RingColor), null, c, 2.2, 2.2);
        }

        private static void DrawArc(DrawingContext ctx, Point c, double r, double start, double sweep, IPen pen)
        {
            const int seg = 18;
            Point prev = default;
            for (int i = 0; i <= seg; i++)
            {
                double a = start + sweep * i / seg;
                var p = new Point(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r);
                if (i > 0) ctx.DrawLine(pen, prev, p);
                prev = p;
            }
        }
    }

    /// <summary>
    /// Lop phu duong ke ngang CRT (scanline) cho theme 550C: ke den mo
    /// cach 3px + mot dai sang chay cham xuong duoi cho cam giac man hinh
    /// dang quet. Chi ve, khong tu chay timer (phu thuoc vao Invalidate cua
    /// cha) — ban nay dung tinh (ke) de tiet kiem CPU.
    /// </summary>
    public sealed class ScanlineOverlay : Control
    {
        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            var line = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0));
            for (double y = 0; y < Bounds.Height; y += 3)
                ctx.DrawRectangle(line, null, new Rect(0, y, Bounds.Width, 1));
            // phat sang phoshor nhe o giua
            var glow = new RadialGradientBrush
            {
                Center = new RelativePoint(0.5, 0.45, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.5, 0.45, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0x14, 0xFF, 0xB0, 0x20), 0),
                    new GradientStop(Color.FromArgb(0x00, 0xFF, 0xB0, 0x20), 1),
                },
            };
            ctx.FillRectangle(glow, new Rect(Bounds.Size));
        }
    }

    /// <summary>
    /// Canh MOSS · Ignition (v3.10): tai hien PHONG CACH bang hinh ve thu
    /// tuc 100% — 4 hoi cua GreetingSequence "moss": lien ket than kinh
    /// (luoi diem + vong quet) -> mat do khoa muc tieu -> tach doi man
    /// hinh -> hanh tinh ve gradient + hat lua + vong quy dao -> lo ten
    /// app. Khong dung bat ky tu lieu phim/anh ngoai nao.
    /// </summary>
    public sealed class MossSceneControl : Control
    {
        private readonly DispatcherTimer _timer;
        private readonly GreetingSequence _seq = GreetingSequence.For(GreetingStyles.Moss);
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private int _elapsedMs;
        private readonly Random _rng = new Random(550);
        private readonly List<Point> _stars = new List<Point>();
        private readonly float[] _fireX, _fireY, _fireV, _fireLife;
        private const int FireCount = 420;
        public bool Running { get; private set; }
        public string PilotName = "T OPERATOR";

        public MossSceneControl()
        {
            for (int i = 0; i < 130; i++)
                _stars.Add(new Point(_rng.NextDouble(), _rng.NextDouble()));
            _fireX = new float[FireCount]; _fireY = new float[FireCount];
            _fireV = new float[FireCount]; _fireLife = new float[FireCount];
            for (int i = 0; i < FireCount; i++) ResetFire(i, true);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += (s, e) =>
            {
                // Thoi gian theo dong ho that (Stopwatch), khong dem tick:
                // tren may cham tick bi tre/gop, dem tick lam hoi chay cham
                // hon thoi gian that va lech voi timer tu tat man chao.
                _elapsedMs = (int)_clock.ElapsedMilliseconds;
                StepFire();
                InvalidateVisual();
                if (_seq.IsCompleteAt(_elapsedMs)) { /* giu khung cuoi, MainWindow tu tat */ }
            };
        }

        private void ResetFire(int i, bool randomAge)
        {
            _fireX[i] = (float)(0.5 + (_rng.NextDouble() - 0.5) * 0.55);
            _fireY[i] = (float)(0.98 + _rng.NextDouble() * 0.05);
            _fireV[i] = (float)(0.10 + _rng.NextDouble() * 0.22);
            _fireLife[i] = randomAge ? (float)_rng.NextDouble() : 0f;
        }

        private void StepFire()
        {
            for (int i = 0; i < FireCount; i++)
            {
                _fireLife[i] += 0.012f;
                _fireY[i] -= _fireV[i] * 0.033f;
                _fireX[i] += (float)Math.Sin((_fireLife[i] * 5) + i) * 0.0006f;
                if (_fireLife[i] >= 1f || _fireY[i] < 0.35f) ResetFire(i, false);
            }
        }

        public void Play() { _elapsedMs = 0; _clock.Restart(); Running = true; _timer.Start(); InvalidateVisual(); }
        public void Stop() { Running = false; _timer.Stop(); _clock.Stop(); }

        private static FormattedText Text(string s, Color color, double size, bool bold = false)
            => new FormattedText(s, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas, 'Courier New', monospace", FontStyle.Normal,
                    bold ? FontWeight.Bold : FontWeight.Normal, FontStretch.Normal),
                size, new SolidColorBrush(color));

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            var rect = new Rect(Bounds.Size);
            double w = rect.Width, h = rect.Height;
            if (w < 40 || h < 40) return;
            ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0x04, 0x05, 0x0A)), rect);
            int phase = _seq.PhaseIndexAt(_elapsedMs);
            double prog = _seq.PhaseProgressAt(_elapsedMs);
            var center = new Point(w / 2, h / 2);

            // sao nen (mo dan tu hoi ignition)
            double starA = phase >= 3 ? 0.8 : 0.25;
            var starBrush = new SolidColorBrush(Color.FromArgb((byte)(150 * starA), 0xC9, 0xD4, 0xFF));
            foreach (var st in _stars)
                ctx.DrawRectangle(starBrush, null, new Rect(st.X * w, st.Y * h, 1.6, 1.6));

            if (phase == 0)
            {
                // NEURAL LINK: luoi diem + vong quet + dong trang thai
                var dot = new SolidColorBrush(Color.FromArgb(0x38, 0x67, 0xE8, 0xF9));
                for (double x = 14; x < w; x += 26)
                    for (double y = 12; y < h; y += 26)
                        ctx.DrawRectangle(dot, null, new Rect(x, y, 2, 2));
                double r = 34 + 16 * Math.Sin(_elapsedMs / 280.0);
                var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x67, 0xE8, 0xF9)), 1.6);
                ctx.DrawEllipse(null, pen, center, r, r);
                ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0x67, 0xE8, 0xF9)), 1), center, r * 1.5, r * 1.5);
                var lines = new[]
                {
                    "NEURAL LINK ......... SYNC",
                    "PILOT: " + (PilotName ?? "T OPERATOR"),
                    "ORBITAL UPLINK ...... OK",
                };
                int show = Math.Min(lines.Length, 1 + (int)(prog * lines.Length));
                for (int i = 0; i < show; i++)
                    ctx.DrawText(Text(lines[i], Color.FromRgb(0x9B, 0xF5, 0xE8), 11.5),
                        new Point(16, h - 58 + i * 17));
            }
            else if (phase == 1)
            {
                // RED EYE: mat do khoa muc tieu, pulse theo nhip
                double pulse = 0.5 + 0.5 * Math.Sin(_elapsedMs / 160.0);
                double r = h * 0.30 * (0.92 + 0.08 * pulse);
                for (int i = 5; i >= 1; i--)
                {
                    byte a = (byte)(16 * i + 26 * pulse);
                    ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(a, 0xFF, 0x2A, 0x2A)), null, center, r * (1 + i * 0.16), r * (1 + i * 0.16));
                }
                ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30)), 2.2), center, r, r);
                ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xFF, 0x59, 0x48)), null, center, r * 0.34, r * 0.34);
                ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x2A, 0x02, 0x02)), null, center, r * 0.15, r * 0.15);
                var cross = new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0x3B, 0x30)), 1);
                ctx.DrawLine(cross, new Point(center.X - r * 1.5, center.Y), new Point(center.X - r * 1.15, center.Y));
                ctx.DrawLine(cross, new Point(center.X + r * 1.15, center.Y), new Point(center.X + r * 1.5, center.Y));
                ctx.DrawLine(cross, new Point(center.X, center.Y - r * 1.5), new Point(center.X, center.Y - r * 1.15));
                ctx.DrawLine(cross, new Point(center.X, center.Y + r * 1.15), new Point(center.X, center.Y + r * 1.5));
                var t = Text("TARGET LOCK · MOSS ONLINE", Color.FromRgb(0xFF, 0x8A, 0x80), 12, true);
                ctx.DrawText(t, new Point(center.X - t.Width / 2, h - 30));
            }
            else if (phase == 2)
            {
                // SPLIT: hai nua man hinh truot ra, khe sang o giua
                double off = prog * w * 0.30;
                var panel = new SolidColorBrush(Color.FromRgb(0x0A, 0x0C, 0x16));
                ctx.DrawRectangle(panel, null, new Rect(-off, 0, w / 2, h));
                ctx.DrawRectangle(panel, null, new Rect(w / 2 + off, 0, w / 2, h));
                double seam = 2 + prog * 26;
                var seamBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0x00, 0x67, 0xE8, 0xF9), 0),
                        new GradientStop(Color.FromArgb(0xE0, 0xE8, 0xFB, 0xFF), 0.5),
                        new GradientStop(Color.FromArgb(0x00, 0x67, 0xE8, 0xF9), 1),
                    },
                };
                ctx.DrawRectangle(seamBrush, null, new Rect(center.X - seam / 2, 0, seam, h));
            }
            else
            {
                // IGNITION + LOGO REVEAL: hanh tinh gradient, vong quy dao,
                // hat lua boc len; hoi cuoi mo dan sang ten app.
                double rise = phase == 3 ? prog : 1;
                double pr = h * 0.52;
                var pc = new Point(center.X, h * 1.02 - rise * h * 0.18);
                var planetBrush = new RadialGradientBrush
                {
                    Center = new RelativePoint(0.42, 0.38, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(0.42, 0.38, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(0.55, RelativeUnit.Relative),
                    RadiusY = new RelativeScalar(0.55, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromRgb(0xBF, 0xDB, 0xFE), 0),
                        new GradientStop(Color.FromRgb(0x3B, 0x82, 0xF6), 0.45),
                        new GradientStop(Color.FromRgb(0x17, 0x2B, 0x52), 1),
                    },
                };
                // vong quy dao (ellipse nghieng bang doan thang)
                var orbitPen = new Pen(new SolidColorBrush(Color.FromArgb(0xB0, 0xF5, 0x9E, 0x0B)), 1.6);
                Point prev = default;
                for (int i = 0; i <= 60; i++)
                {
                    double a = i / 60.0 * Math.PI * 2;
                    var p = new Point(pc.X + Math.Cos(a) * pr * 1.25, pc.Y + Math.Sin(a) * pr * 0.30);
                    if (i > 0) ctx.DrawLine(orbitPen, prev, p);
                    prev = p;
                }
                using (ctx.PushClip(new RoundedRect(rect, 10)))
                {
                    ctx.DrawEllipse(planetBrush, null, pc, pr, pr);
                    // hat lua
                    for (int i = 0; i < FireCount; i++)
                    {
                        float life = _fireLife[i];
                        byte a = (byte)(200 * (1 - life));
                        var fb = new SolidColorBrush(Color.FromArgb(a, 0xFF, (byte)(120 + 100 * life), 0x2A));
                        ctx.DrawRectangle(fb, null, new Rect(_fireX[i] * w, _fireY[i] * h, 2.2, 2.2));
                    }
                }
                if (phase == 4)
                {
                    byte a = (byte)(255 * Math.Min(1, prog * 1.4));
                    var t = Text("TIEN VU KEYBOARD STUDIO", Color.FromArgb(a, 0xF4, 0xF4, 0xF8), 17, true);
                    ctx.DrawText(t, new Point(center.X - t.Width / 2, h * 0.16));
                    var t2 = Text("IGNITION COMPLETE", Color.FromArgb(a, 0xF5, 0x9E, 0x0B), 11);
                    ctx.DrawText(t2, new Point(center.X - t2.Width / 2, h * 0.16 + 26));
                }
                else
                {
                    var t = Text("IGNITION SEQUENCE", Color.FromRgb(0xF5, 0x9E, 0x0B), 11.5);
                    ctx.DrawText(t, new Point(16, 14));
                }
            }
        }
    }

    /// <summary>
    /// Hat Bui TV (v3.10): truong hat xoay kieu xoan oc (phyllotaxis) roi
    /// TU THANH hinh logo Mau F (diem dich lay mau tu anh logo bang
    /// LogoParticleSampler), lang xuong, roi lo ten app. Ve bang hinh chu
    /// nhat nho gom theo 5 bac mau de re CPU; so hat thuc te = ParticleCount.
    /// </summary>
    public sealed class ParticleMorphControl : Control
    {
        public const int ParticleCount = 3200;

        private readonly DispatcherTimer _timer;
        private readonly GreetingSequence _seq = GreetingSequence.For(GreetingStyles.Particles);
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private int _elapsedMs;
        private readonly float[] _x, _y, _tx, _ty;
        private readonly byte[] _brushIdx;
        private readonly IBrush[] _brushes;
        public bool Running { get; private set; }

        private static readonly (byte R, byte G, byte B)[] Palette =
        {
            (0x7C, 0x3A, 0xED), (0x38, 0x5C, 0xF6), (0x22, 0xD3, 0xEE), (0xE0, 0x93, 0xF9), (0xF4, 0xF4, 0xF8),
        };

        public ParticleMorphControl()
        {
            _x = new float[ParticleCount]; _y = new float[ParticleCount];
            _tx = new float[ParticleCount]; _ty = new float[ParticleCount];
            _brushIdx = new byte[ParticleCount];
            _brushes = new IBrush[Palette.Length];
            for (int i = 0; i < Palette.Length; i++)
                _brushes[i] = new SolidColorBrush(Color.FromRgb(Palette[i].R, Palette[i].G, Palette[i].B));
            // dich du phong: vong xoan (neu chua co diem logo)
            for (int i = 0; i < ParticleCount; i++)
            {
                double f = (i + 0.5) / ParticleCount;
                double r = Math.Sqrt(f) * 0.46, a = i * 2.399963;
                _tx[i] = (float)(0.5 + Math.Cos(a) * r);
                _ty[i] = (float)(0.5 + Math.Sin(a) * r);
                _brushIdx[i] = (byte)(i % Palette.Length);
            }
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += (s, e) => { _elapsedMs = (int)_clock.ElapsedMilliseconds; Step(); InvalidateVisual(); };
        }

        /// <summary>Nap diem dich (chuan hoa 0..1 theo hop bao logo) + mau.</summary>
        public void SetTargets(IReadOnlyList<LogoParticleSampler.TargetPoint> targets)
        {
            if (targets == null || targets.Count == 0) return;
            for (int i = 0; i < ParticleCount; i++)
            {
                var t = targets[i % targets.Count];
                _tx[i] = (float)t.X; _ty[i] = (float)t.Y;
                _brushIdx[i] = NearestBrush(t.R, t.G, t.B);
            }
        }

        private static byte NearestBrush(byte r, byte g, byte b)
        {
            int best = 0, bestD = int.MaxValue;
            for (int i = 0; i < Palette.Length; i++)
            {
                int dr = r - Palette[i].R, dg = g - Palette[i].G, db = b - Palette[i].B;
                int d = dr * dr + dg * dg + db * db;
                if (d < bestD) { bestD = d; best = i; }
            }
            return (byte)best;
        }

        private int _lastStepMs;

        private void Step()
        {
            // Do cung chuan hoa theo dt that: tick cang thưa, moi buoc nhay
            // cang xa — hat hoi tu dung toc do du may nhanh hay cham.
            int dt = Math.Max(1, _elapsedMs - _lastStepMs);
            _lastStepMs = _elapsedMs;
            double t = _elapsedMs / 1000.0;
            int phase = _seq.PhaseIndexAt(_elapsedMs);
            double morph = phase == 0 ? 0 : phase == 1 ? EaseOut(_seq.PhaseProgressAt(_elapsedMs)) : 1;
            double rot = t * 0.55;
            double stiff = 1 - Math.Pow(1 - (phase >= 2 ? 0.30 : 0.16), dt / 33.0);
            for (int i = 0; i < ParticleCount; i++)
            {
                double f = (i + 0.5) / ParticleCount;
                double r = Math.Sqrt(f) * 0.46, a = i * 2.399963 + rot;
                double sx = 0.5 + Math.Cos(a) * r, sy = 0.5 + Math.Sin(a) * r * 0.82;
                double gx = sx + (_tx[i] - sx) * morph;
                double gy = sy + (_ty[i] - sy) * morph;
                _x[i] += (float)((gx - _x[i]) * stiff);
                _y[i] += (float)((gy - _y[i]) * stiff);
                if (phase == 0 && _elapsedMs < 40) { _x[i] = (float)sx; _y[i] = (float)sy; }
            }
        }

        private static double EaseOut(double p) => 1 - Math.Pow(1 - p, 3);

        public void Play()
        {
            _elapsedMs = 0; _lastStepMs = 0; _clock.Restart();
            for (int i = 0; i < ParticleCount; i++) { _x[i] = 0.5f; _y[i] = 0.5f; }
            Running = true; _timer.Start(); InvalidateVisual();
        }

        public void Stop() { Running = false; _timer.Stop(); _clock.Stop(); }

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            var rect = new Rect(Bounds.Size);
            double w = rect.Width, h = rect.Height;
            if (w < 40 || h < 40) return;
            ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x0C)), rect);
            int phase = _seq.PhaseIndexAt(_elapsedMs);
            // Mot he toa do duy nhat cho moi hoi: "san khau" la hinh vuong
            // canh min(w,h)*0.94 o giua khung; ca xoan oc lan diem dich logo
            // deu song trong he nay nen morph khong nhay hinh.
            double side = Math.Min(w, h) * 0.94;
            double ox = (w - side) / 2, oy = (h - side) / 2;
            double alpha = phase == 3 ? Math.Max(0.45, 1 - _seq.PhaseProgressAt(_elapsedMs) * 0.55) : 1;
            using (alpha < 0.99 ? (System.IDisposable)ctx.PushOpacity(alpha) : null)
            {
                for (int i = 0; i < ParticleCount; i++)
                {
                    double px = ox + _x[i] * side, py = oy + _y[i] * side;
                    ctx.DrawRectangle(_brushes[_brushIdx[i]], null, new Rect(px - 1.1, py - 1.1, 2.2, 2.2));
                }
            }
            if (phase == 3)
            {
                double p = _seq.PhaseProgressAt(_elapsedMs);
                byte a = (byte)(255 * Math.Min(1, p * 1.6));
                var ft = new FormattedText("TIEN VU", System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Consolas, 'Courier New', monospace", FontStyle.Normal, FontWeight.Bold, FontStretch.Normal),
                    15, new SolidColorBrush(Color.FromArgb(a, 0xF4, 0xF4, 0xF8)));
                ctx.DrawText(ft, new Point(w / 2 - ft.Width / 2, h - 30));
            }
        }
    }
}
