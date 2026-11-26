using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TienKeyboardStudioPreview
{
    public static class UiBrushes
    {
        public static LinearGradientBrush AccentGradient() => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x7C, 0x3A, 0xED), 0),
                new GradientStop(Color.FromRgb(0x22, 0xD3, 0xEE), 1),
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
}
