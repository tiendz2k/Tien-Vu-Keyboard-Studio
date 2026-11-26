using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

namespace TienKeyboardStudioPreview
{
    public partial class MainWindow : Window
    {
        private const string PreviewToast = "Bản xem trước — tính năng này chưa nối phím thật";

        private readonly RgbAnimState _rgb = new RgbAnimState { Effect = 10, Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3, Speed = 2, Direction = 0 };
        private readonly RgbAnimState _music = new RgbAnimState { Effect = 100, Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3, Speed = 2, MusicGain = 1.0, MusicBg = 1 };

        private readonly Dictionary<string, Control> _pages = new Dictionary<string, Control>();
        private readonly List<(string Key, Button Btn, Path Icon)> _nav = new List<(string, Button, Path)>();
        private string _currentPage = "";

        private DispatcherTimer _toastTimer;
        private DispatcherTimer _toastHideTimer;

        // Trang chu
        private TextBlock _homeFxValue;
        private TextBlock _homeGifValue;

        public MainWindow()
        {
            InitializeComponent();
            Toast.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(240) } };

            BuildNav();
            _pages["home"] = BuildHomePage();
            _pages["rgb"] = BuildRgbPage();
            _pages["music"] = BuildMusicPage();
            _pages["gif"] = BuildGifPage();
            _pages["online"] = BuildOnlinePage();
            _pages["widget"] = BuildWidgetPage();
            _pages["settings"] = BuildSettingsPage();
            _pages["phone"] = BuildPhonePage();
            foreach (var p in _pages.Values) { p.IsVisible = false; PageHost.Children.Add(p); }
            ShowPage("home");
        }

        // ================= Tien ich UI =================
        private static void SetCls(Control c, string name, bool on)
        {
            if (on) { if (!c.Classes.Contains(name)) c.Classes.Add(name); }
            else c.Classes.Remove(name);
        }

        private static TextBlock Tb(string text, string cls = null, double size = 0)
        {
            var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
            if (cls != null) t.Classes.Add(cls);
            if (size > 0) t.FontSize = size;
            return t;
        }

        private static Border Card(Control content, double pad = 18)
            => new Border { Classes = { "card" }, Child = content, Padding = new Thickness(pad) };

        private static Border CardFlat(Control content, double pad = 14)
            => new Border { Classes = { "cardflat" }, Child = content, Padding = new Thickness(pad) };

        private static StackPanel V(double spacing, params Control[] children)
        {
            var s = new StackPanel { Spacing = spacing };
            foreach (var c in children) s.Children.Add(c);
            return s;
        }

        private static StackPanel H(double spacing, params Control[] children)
        {
            var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
            foreach (var c in children) s.Children.Add(c);
            return s;
        }

        private Button PrimaryBtn(string text, Action onClick)
        {
            var b = new Button { Content = text };
            b.Classes.Add("primary");
            b.Click += (s, e) => onClick();
            return b;
        }

        private Button GhostBtn(string text, Action onClick)
        {
            var b = new Button { Content = text };
            b.Classes.Add("ghost");
            b.Click += (s, e) => onClick();
            return b;
        }

        private Control Scroll(Control content)
            => new ScrollViewer
            {
                Content = new Border { Child = content, Padding = new Thickness(2, 2, 10, 2) },
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            };

        /// <summary>Segmented control dang pill trong khay kinh; tra ve (control, ham chon theo index).</summary>
        private (Control Root, Action<int> Select) Segmented(string[] labels, int initial, Action<int> onChange)
        {
            var tray = new Border { Classes = { "segtray" } };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            var btns = new List<Button>();
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = new Button { Content = labels[i] };
                b.Classes.Add("seg");
                b.Click += (s, e) => { Select(idx); onChange(idx); };
                btns.Add(b);
                row.Children.Add(b);
            }
            tray.Child = row;
            void Select(int idx)
            {
                for (int i = 0; i < btns.Count; i++)
                    SetCls(btns[i], "sel", i == idx);
            }
            Select(initial);
            return (tray, Select);
        }

        // ================= Dieu huong + Toast =================
        private static Path NavIcon(string data, Color color) => new Path
        {
            Data = StreamGeometry.Parse(data),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.7,
            Width = 17,
            Height = 17,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
        };

        private void BuildNav()
        {
            var items = new (string Key, string Label, string Icon)[]
            {
                ("home", "Tổng quan", "M3 9.5 12 3l9 6.5V21a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 21Z M9.5 22v-8h5v8"),
                ("rgb", "Ánh sáng RGB", "M8 12 A4 4 0 1 1 16 12 A4 4 0 1 1 8 12 M12 2.5v2 M12 19.5v2 M4.8 4.8l1.4 1.4 M17.8 17.8l1.4 1.4 M2.5 12h2 M19.5 12h2 M4.8 19.2l1.4-1.4 M17.8 6.2l1.4-1.4"),
                ("music", "Theo nhạc", "M9 18.5V6l11-2v12.5 M3.8 18.5 A2.7 2.7 0 1 1 9.2 18.5 A2.7 2.7 0 1 1 3.8 18.5 M14.8 16.5 A2.7 2.7 0 1 1 20.2 16.5 A2.7 2.7 0 1 1 14.8 16.5"),
                ("gif", "Màn hình GIF", "M4 4.5 A1.5 1.5 0 0 1 5.5 3h13A1.5 1.5 0 0 1 20 4.5v15a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 19.5Z M8.3 10.2 a1.4 1.4 0 1 0 .1 0 M20 16.5 15.5 12 5 21"),
                ("online", "Thư viện Online", "M12 12 m-9.5 0 a9.5 9.5 0 1 0 19 0 a9.5 9.5 0 1 0-19 0 M2.5 12h19 M12 2.5c2.9 2.7 4.4 5.9 4.4 9.5s-1.5 6.8-4.4 9.5c-2.9-2.7-4.4-5.9-4.4-9.5S9.1 5.2 12 2.5Z"),
                ("widget", "Widget TFT", "M4.5 15.5 A8.5 8.5 0 1 1 19.5 15.5 M12 15.5 16 10 M12 15.5 m-1.2 0 a1.2 1.2 0 1 0 2.4 0 a1.2 1.2 0 1 0-2.4 0"),
                ("settings", "Đồng hồ & Cài đặt", "M12 12 m-9 0 a9 9 0 1 0 18 0 a9 9 0 1 0-18 0 M12 7v5.2l3.4 2"),
                ("phone", "Điều khiển từ điện thoại", "M7 2.5h10A1.5 1.5 0 0 1 18.5 4v16A1.5 1.5 0 0 1 17 21.5H7A1.5 1.5 0 0 1 5.5 20V4A1.5 1.5 0 0 1 7 2.5Z M10.3 5.2h3.4"),
            };
            var idle = Color.FromRgb(0xB9, 0xB6, 0xCC);
            foreach (var (key, label, iconData) in items)
            {
                var icon = NavIcon(iconData, idle);
                var content = H(11, icon, Tb(label, null, 13.5));
                var b = new Button { Content = content };
                b.Classes.Add("nav");
                string k = key;
                b.Click += (s, e) => ShowPage(k);
                _nav.Add((key, b, icon));
                NavPanel.Children.Add(b);
            }
        }

        private void ShowPage(string key)
        {
            if (_currentPage == key && _pages.ContainsKey(key)) return;
            _currentPage = key;
            foreach (var kv in _pages) kv.Value.IsVisible = kv.Key == key;
            foreach (var (k, btn, icon) in _nav)
            {
                bool sel = k == key;
                SetCls(btn, "sel", sel);
                icon.Stroke = new SolidColorBrush(sel ? Colors.White : Color.FromRgb(0xB9, 0xB6, 0xCC));
            }
            if (_pages.TryGetValue(key, out var page)) AnimateIn(page);
            OnPageShown(key);
        }

        private static void AnimateIn(Control c)
        {
            var anim = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(240),
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(OpacityProperty, 0.0),
                        },
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(OpacityProperty, 1.0),
                        },
                    },
                },
            };
            anim.RunAsync(c);
        }

        private void OnPageShown(string key)
        {
            _widgetTimer.IsEnabled = key == "widget";
            _clockTimer.IsEnabled = key == "settings";
            if (key == "widget") RenderWidgetFrame();
            if (key == "settings") UpdateClockLabels();
        }

        public void ShowToast(string msg)
        {
            ToastText.Text = msg;
            Toast.IsVisible = true;
            Toast.Opacity = 1;
            _toastTimer?.Stop();
            _toastHideTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2900) };
            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                Toast.Opacity = 0;
                _toastHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
                _toastHideTimer.Tick += (s2, e2) => { _toastHideTimer.Stop(); Toast.IsVisible = false; };
                _toastHideTimer.Start();
            };
            _toastTimer.Start();
        }

        // ================= TRANG 1: TONG QUAN =================
        private Control BuildHomePage()
        {
            // --- Khu thiet bi dang ket noi (mo phong quet HID) ---
            var results = new StackPanel { Spacing = 10 };
            results.Children.Add(DeviceCard(
                "AULA F75 Max", "Đang sử dụng", Color.FromRgb(0x34, 0xD3, 0x99),
                "USB-C · VID 0C45:800A · Pin 87% · Hỗ trợ đầy đủ tính năng", null));
            var f75Row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var f75Texts = V(2,
                Tb("AULA F75", "h3"),
                Tb("Bluetooth · Chưa có driver đầy đủ", "sub"));
            f75Row.Children.Add(f75Texts);
            var switchBtn = GhostBtn("Chuyển sang", () => ShowToast(PreviewToast));
            Grid.SetColumn(switchBtn, 1);
            f75Row.Children.Add(switchBtn);
            var f75Card = CardFlat(f75Row);
            results.Children.Add(f75Card);
            results.Children.Add(Tb("1 thiết bị HID không xác định — chưa có driver", "faint"));

            var scanPanel = V(8,
                new ProgressBar { IsIndeterminate = true, Height = 4, Foreground = UiBrushes.AccentGradient() },
                Tb("Đang quét thiết bị HID…", "sub"));
            scanPanel.IsVisible = false;

            Button scanBtn = null;
            scanBtn = PrimaryBtn("Quét bàn phím", async () =>
            {
                scanBtn.IsEnabled = false;
                results.IsVisible = false;
                scanPanel.IsVisible = true;
                await Task.Delay(1100);
                scanPanel.IsVisible = false;
                results.IsVisible = true;
                scanBtn.IsEnabled = true;
                ShowToast("Quét xong (mô phỏng): tìm thấy AULA F75 Max đang kết nối qua USB-C");
            });
            var connectHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            connectHeader.Children.Add(Tb("Bàn phím đang kết nối", "h2"));
            Grid.SetColumn(scanBtn, 1);
            connectHeader.Children.Add(scanBtn);
            var connectCard = Card(V(12, connectHeader, scanPanel, results));

            // --- Ban phim anh hung ---
            var homeKb = new KeyboardView { Mode = KeyboardMode.Animated, State = _rgb };
            var heroCard = Card(homeKb, 14);

            // --- 3 the thong ke ---
            _homeFxValue = Tb("Sóng (Wave)", "h3");
            _homeGifValue = Tb("Tien Galaxy Spiral", "h3");
            var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 12 };
            var s1 = StatCard("Hiệu ứng RGB", _homeFxValue);
            var s2 = StatCard("GIF màn hình", _homeGifValue);
            var s3 = StatCard("Chế độ an toàn", Tb("Dry-run đang bật", "h3"));
            stats.Children.Add(s1); Grid.SetColumn(s2, 1); stats.Children.Add(s2); Grid.SetColumn(s3, 2); stats.Children.Add(s3);

            // --- Luoi mau phim ---
            var models = BuildModelGrid();

            var root = V(14,
                Tb("Tổng quan", "h1"),
                Tb("Bản xem trước giao diện của Tien Keyboard Studio — mọi thao tác chỉ mô phỏng, chưa nối phím thật.", "sub"),
                connectCard,
                heroCard,
                stats,
                Tb("Tất cả mẫu được hỗ trợ", "h2"),
                models,
                Tb("App chỉ tích hợp các mẫu đã công khai giao thức (SDK chính thức, VIA/QMK, hoặc driver mã nguồn mở đã công bố) — mẫu giao thức kín sẽ để sau.", "faint"));
            return Scroll(root);
        }

        private static Border StatCard(string label, TextBlock value)
        {
            var s = V(3, Tb(label.ToUpperInvariant(), "faint"), value);
            return CardFlat(s);
        }

        private Border DeviceCard(string name, string badge, Color badgeColor, string sub, Control extra)
        {
            var badgeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x26, badgeColor.R, badgeColor.G, badgeColor.B)),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(9, 3),
                Child = new TextBlock { Text = badge, FontSize = 10.5, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(badgeColor) },
                VerticalAlignment = VerticalAlignment.Center,
            };
            var nameRow = H(9, Tb(name, "h3"), badgeBorder);
            var texts = V(3, nameRow, Tb(sub, "sub"));
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            grid.Children.Add(texts);
            if (extra != null) { Grid.SetColumn(extra, 1); grid.Children.Add(extra); }
            return CardFlat(grid);
        }

        private Control BuildModelGrid()
        {
            var models = new (string Name, string Layout, string Status, int Kind)[]
            {
                ("AULA F75 Max", "75% · TFT + núm xoay", "Hỗ trợ đầy đủ", 0),
                ("AULA F75", "75% · 80 phím", "Đã có giao thức mở — sẽ tích hợp", 1),
                ("Leobog Hi75C Pro", "75% · 81 phím", "Chung giao thức F75 — sẽ tích hợp", 1),
                ("Keychron V1", "65% · 84 phím", "VIA chính thức — sẽ tích hợp", 1),
                ("Wooting 60HE", "60% · 61 phím", "SDK chính thức — sẽ tích hợp", 1),
                ("MADLIONS MAD60 HE", "60% · 61 phím", "Đã có giao thức mở — sẽ tích hợp", 1),
                ("Royal Kludge RK84", "75% · 84 phím", "Đã có giao thức mở — sẽ tích hợp", 1),
                ("DrunkDeer A75", "75%", "Đã có giao thức mở — sẽ tích hợp", 1),
                ("AULA S75 Pro", "75% · 80 phím", "Giao thức kín — để sau", 2),
                ("AULA Nova75", "75%", "Giao thức kín — để sau", 2),
                ("AULA WIN60 HE", "60% · 61 phím", "Giao thức kín — để sau", 2),
                ("AULA WIN68 HE", "65% · 68 phím", "Giao thức kín — để sau", 2),
                ("AULA HERO68 HE", "65% · 68 phím", "Giao thức kín — để sau", 2),
                ("Akko 5075B", "75% · núm xoay", "Giao thức kín — để sau", 2),
                ("GravaStar Mercury K1", "75% · 79 phím", "Giao thức kín — để sau", 2),
                ("MonsGeek M1", "75% · núm xoay", "Giao thức kín — để sau", 2),
            };
            var wrap = new WrapPanel();
            foreach (var (name, layout, status, kind) in models)
            {
                Color pillText, pillBg;
                if (kind == 0) { pillText = Color.FromRgb(0x34, 0xD3, 0x99); pillBg = Color.FromArgb(0x26, 0x34, 0xD3, 0x99); }
                else if (kind == 1) { pillText = Color.FromRgb(0xC4, 0xB5, 0xFD); pillBg = Color.FromArgb(0x2E, 0x7C, 0x3A, 0xED); }
                else { pillText = Color.FromRgb(0x8A, 0x87, 0xA0); pillBg = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF); }
                var pill = new Border
                {
                    Background = new SolidColorBrush(pillBg),
                    CornerRadius = new CornerRadius(999),
                    Padding = new Thickness(8, 3),
                    Child = new TextBlock { Text = status, FontSize = 10, Foreground = new SolidColorBrush(pillText) },
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                var cardContent = V(5, Tb(name, "h3"), Tb(layout, "faint"), pill);
                var card = new Border
                {
                    Classes = { "pickcard" },
                    Child = cardContent,
                    Width = 218,
                    Margin = new Thickness(0, 0, 10, 10),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                if (kind == 0)
                {
                    card.BorderBrush = UiBrushes.AccentGradient();
                    card.BorderThickness = new Thickness(1.6);
                }
                int k = kind;
                card.PointerPressed += (s, e) =>
                {
                    if (k == 0) ShowToast("Đang dùng AULA F75 Max (thiết bị mô phỏng)");
                    else ShowToast("Mẫu này đang được nghiên cứu driver — hiện xem trước giao diện/hiệu ứng, ghi xuống phím sẽ mở khi driver sẵn sàng");
                };
                wrap.Children.Add(card);
            }
            return wrap;
        }

        // ================= TRANG 2: ANH SANG RGB =================
        private static readonly string[] EffectFriendlyNames =
        {
            "Tắt (LED Off)", "Tĩnh (Static)", "Reactive sáng khi gõ", "Reactive mờ dần",
            "Lấp lánh (Glittering)", "Mưa rơi (Falling)", "Nhiều màu (Colourful)", "Thở (Breathing)",
            "Quang phổ (Spectrum)", "Lan ra ngoài (Outward)", "Sóng (Wave)", "Cuộn (Rolling)",
            "Xoay (Rotating)", "Bùng nổ (Explode)", "Phóng (Launch)", "Gợn sóng (Ripples)",
            "Chảy (Flowing)", "Nhịp (Pulsating)", "Nghiêng (Tilt)", "Con thoi (Shuttle)",
        };

        private Control BuildRgbPage()
        {
            var kb = new KeyboardView { Mode = KeyboardMode.Animated, State = _rgb };

            // Luoi 20 the hieu ung
            var previews = new List<EffectPreview>();
            var cards = new List<Border>();
            var wrap = new WrapPanel();
            for (int i = 0; i < 20; i++)
            {
                int idx = i;
                var pv = new EffectPreview { Effect = idx, BaseColor = _rgb.Brush };
                var content = V(7, pv, Tb(EffectFriendlyNames[idx], null, 11.5));
                var card = new Border
                {
                    Classes = { "pickcard" },
                    Child = content,
                    Width = 172,
                    Margin = new Thickness(0, 0, 10, 10),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                card.PointerPressed += (s, e) => SelectEffect(idx, previews, cards);
                previews.Add(pv);
                cards.Add(card);
                wrap.Children.Add(card);
            }
            SelectEffectVisual(10, previews, cards);

            // Mau chinh
            var swatchColors = new (string Name, Color C)[]
            {
                ("Tím", Color.FromRgb(0x7C, 0x3A, 0xED)), ("Cyan", Color.FromRgb(0x22, 0xD3, 0xEE)),
                ("Hồng", Color.FromRgb(0xF4, 0x72, 0xB6)), ("Đỏ", Color.FromRgb(0xEF, 0x44, 0x44)),
                ("Cam", Color.FromRgb(0xF9, 0x73, 0x16)), ("Vàng", Color.FromRgb(0xFA, 0xCC, 0x15)),
                ("Lục", Color.FromRgb(0x34, 0xD3, 0x99)), ("Dương", Color.FromRgb(0x3B, 0x82, 0xF6)),
                ("Trắng", Color.FromRgb(0xF8, 0xFA, 0xFC)),
            };
            var swRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var (name, col) in swatchColors)
            {
                var sw = new Border
                {
                    Width = 30, Height = 30, CornerRadius = new CornerRadius(9),
                    Background = new SolidColorBrush(col),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                ToolTip.SetTip(sw, name);
                var captured = col;
                sw.PointerPressed += (s, e) =>
                {
                    _rgb.Brush = captured;
                    _rgb.Multi = false;
                    foreach (var p in previews) p.BaseColor = captured;
                    kb.InvalidateVisual();
                };
                swRow.Children.Add(sw);
            }
            var multiChip = new Button { Content = "Nhiều màu (cầu vồng)" };
            multiChip.Classes.Add("chip");
            multiChip.Click += (s, e) =>
            {
                _rgb.Multi = !_rgb.Multi;
                SetCls(multiChip, "sel", _rgb.Multi);
                kb.InvalidateVisual();
                ShowToast(_rgb.Multi ? "Đã bật chế độ nhiều màu cho các hiệu ứng hỗ trợ" : "Đã tắt chế độ nhiều màu");
            };
            swRow.Children.Add(multiChip);

            // Slider do sang / toc do
            var bright = new Slider { Minimum = 0, Maximum = 4, Value = 3, Width = 210 };
            bright.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) _rgb.Brightness = bright.Value;
            };
            var speed = new Slider { Minimum = 0, Maximum = 4, Value = 2, Width = 210 };
            speed.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) _rgb.Speed = speed.Value;
            };
            var sliderGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = 26 };
            sliderGrid.Children.Add(V(4, Tb("Độ sáng", "sub"), bright));
            var spCol = V(4, Tb("Tốc độ", "sub"), speed);
            Grid.SetColumn(spCol, 1);
            sliderGrid.Children.Add(spCol);

            var (dirSeg, _) = Segmented(new[] { "Phải", "Xuống", "Trái", "Lên" }, 0, i => { _rgb.Direction = i; kb.InvalidateVisual(); });
            var dirRow = H(10, Tb("Hướng chạy:", "sub"), dirSeg);
            dirRow.Children[0].VerticalAlignment = VerticalAlignment.Center;

            var applyBtn = PrimaryBtn("Áp dụng xuống phím", () => ShowToast(PreviewToast));
            applyBtn.HorizontalAlignment = HorizontalAlignment.Left;

            var root = V(14,
                Tb("Ánh sáng RGB", "h1"),
                Tb("Chọn hiệu ứng để xem chạy trực tiếp trên bàn phím. Độ sáng, tốc độ, hướng và màu áp dụng ngay cho bản xem trước.", "sub"),
                Card(kb, 14),
                Tb("Hiệu ứng firmware (20)", "h2"),
                wrap,
                Card(V(12,
                    Tb("Màu chính", "h3"), swRow,
                    sliderGrid,
                    dirRow,
                    applyBtn)));
            return Scroll(root);
        }

        private void SelectEffect(int idx, List<EffectPreview> previews, List<Border> cards)
        {
            _rgb.Effect = idx;
            SelectEffectVisual(idx, previews, cards);
            if (_homeFxValue != null) _homeFxValue.Text = EffectFriendlyNames[idx];
        }

        private static void SelectEffectVisual(int idx, List<EffectPreview> previews, List<Border> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                bool sel = i == idx;
                previews[i].Active = sel;
                cards[i].BorderBrush = sel ? UiBrushes.AccentGradient() : UiBrushes.GlassBorder;
                cards[i].BorderThickness = new Thickness(sel ? 1.8 : 1);
            }
        }

        // ================= TRANG 3: THEO NHAC (MO PHONG) =================
        private Control BuildMusicPage()
        {
            var kb = new KeyboardView { Mode = KeyboardMode.Animated, State = _music };

            var simChip = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0x7C, 0x3A, 0xED)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xA7, 0x8B, 0xFA)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(12, 6),
                Child = Tb("Mô phỏng — bản thật sẽ bắt nhạc thật từ máy tính", null, 12),
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            bool playing = true;
            Button playBtn = null;
            playBtn = GhostBtn("Tạm dừng mô phỏng", () =>
            {
                playing = !playing;
                _music.Effect = playing ? 100 : 0;
                playBtn.Content = playing ? "Tạm dừng mô phỏng" : "Phát mô phỏng";
                kb.InvalidateVisual();
            });

            var gain = new Slider { Minimum = 0.4, Maximum = 1.8, Value = 1.0, Width = 230 };
            gain.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) _music.MusicGain = gain.Value;
            };
            var (bgSeg, _) = Segmented(new[] { "Tắt", "Mờ", "Đầy đủ" }, 1, i => _music.MusicBg = i);

            var controlCard = Card(V(12,
                Tb("Điều khiển mô phỏng", "h2"),
                Tb("Đang phát (mô phỏng): Midnight Drive — TienMix", "sub"),
                playBtn,
                V(4, Tb("Độ nhạy (biên độ cột sóng)", "sub"), gain),
                H(10, Tb("Chế độ nền:", "sub"), bgSeg)));

            var howCard = Card(V(8,
                Tb("Bản thật sẽ hoạt động thế nào?", "h2"),
                Tb("• Bắt toàn bộ âm thanh hệ thống qua WASAPI loopback — không cần micro.", "sub"),
                Tb("• Phân tích phổ tần (FFT): bass, mid, treble ánh xạ thành các cột phím nhảy theo nhạc.", "sub"),
                Tb("• Chế độ an toàn Dry-run vẫn áp dụng: xem trước thoải mái, chỉ ghi xuống phím khi bạn tắt Dry-run.", "sub"),
                Tb("• Trang này dùng nhịp tổng hợp để bạn hình dung hiệu ứng — chưa bắt âm thanh thật.", "faint")));

            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 14 };
            cols.Children.Add(controlCard);
            Grid.SetColumn(howCard, 1);
            cols.Children.Add(howCard);

            var root = V(14,
                Tb("Theo nhạc", "h1"),
                Tb("Bàn phím nhảy theo nhạc như một dàn đèn equalizer.", "sub"),
                simChip,
                Card(kb, 14),
                cols);
            return Scroll(root);
        }

        // ================= TRANG 4: MAN HINH GIF =================
        private Control BuildGifPage()
        {
            var kbState = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED) };
            var kb = new KeyboardView { Mode = KeyboardMode.Screen, State = kbState };
            var bigImg = new Avalonia.Controls.Image
            {
                Width = 232, Height = 232, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            kb.ScreenFrameChanged += f => { bigImg.Source = f; };
            var info = Tb("Chọn một GIF trong thư viện bên dưới.", "sub");
            info.TextAlignment = TextAlignment.Center;

            var fit = FitMode.Fill;
            List<Bitmap> screenFrames = null;
            GifEntry current = null;

            async Task ApplySelection(GifEntry entry)
            {
                current = entry;
                if (_homeGifValue != null) _homeGifValue.Text = entry.Name;
                int total = await Task.Run(() => GifTools.ProbeFrameCount(entry.AssetUri));
                info.Text = entry.Name + " · nhóm " + entry.GroupLabel + " · " + total +
                            " frame gốc → chuẩn hoá 128×128 (" + fit + ")";
                var frames = await Task.Run(() => GifTools.LoadFrames(entry.AssetUri, 128, fit, 48));
                var old = screenFrames;
                screenFrames = frames;
                kb.ScreenFrames = frames;
                GifTools.DisposeFrames(old);
            }

            // Luoi GIF thu vien
            var entries = GifCatalog.Library();
            var cards = new List<GifCard>();
            var wrap = new WrapPanel();
            foreach (var entry in entries)
            {
                var card = new GifCard(entry)
                {
                    Margin = new Thickness(0, 0, 10, 10),
                    FramesLoader = e => Task.Run(() => GifTools.LoadFrames(e.AssetUri, 96, FitMode.Fit, 24)),
                };
                card.Clicked += c =>
                {
                    foreach (var o in cards) o.Selected = ReferenceEquals(o, c);
                    _ = ApplySelection(c.Entry);
                };
                cards.Add(card);
                wrap.Children.Add(card);
            }
            // Nap frame dau cho tung the (nen, khong chan UI)
            _ = Task.Run(async () =>
            {
                foreach (var c in cards)
                {
                    try
                    {
                        var bmp = GifTools.LoadFrame(c.Entry.AssetUri, 96);
                        await Dispatcher.UIThread.InvokeAsync(() => c.SetFirstFrame(bmp));
                    }
                    catch { }
                }
            });

            // Loc theo nhom
            var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var filterBtns = new List<Button>();
            var filters = new (string Label, string Group)[]
            {
                ("Tất cả", null), ("Ngân hà", "NganHa"), ("Pixel & Game", "PixelGame"), ("Thư giãn", "ThuGian"),
            };
            foreach (var (label, group) in filters)
            {
                var b = new Button { Content = label };
                b.Classes.Add("chip");
                if (group == null) b.Classes.Add("sel");
                b.Click += (s, e) =>
                {
                    foreach (var o in filterBtns) o.Classes.Remove("sel");
                    b.Classes.Add("sel");
                    foreach (var c in cards)
                        c.IsVisible = group == null || c.Entry.Group == group;
                };
                filterBtns.Add(b);
                filterRow.Children.Add(b);
            }

            var (fitSeg, _) = Segmented(new[] { "Fill", "Fit", "Stretch" }, 0, i =>
            {
                fit = i == 0 ? FitMode.Fill : i == 1 ? FitMode.Fit : FitMode.Stretch;
                if (current != null) _ = ApplySelection(current);
            });
            var uploadBtn = PrimaryBtn("Nạp lên phím", () => ShowToast(PreviewToast));
            var actionRow = H(12, fitSeg, uploadBtn);
            actionRow.Children[0].VerticalAlignment = VerticalAlignment.Center;

            var topGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,300"), ColumnSpacing = 14 };
            topGrid.Children.Add(Card(kb, 14));
            var previewCard = Card(V(10,
                Tb("Xem trước lớn", "h3"),
                bigImg,
                info));
            Grid.SetColumn(previewCard, 1);
            topGrid.Children.Add(previewCard);

            // Chon mac dinh: Tien Galaxy Spiral
            var def = entries.FirstOrDefault(e => e.Name.Contains("Galaxy Spiral")) ?? entries[0];
            foreach (var c in cards) c.Selected = ReferenceEquals(c.Entry, def);
            _ = ApplySelection(def);

            var root = V(14,
                Tb("Màn hình GIF", "h1"),
                Tb("GIF phát ngay trong ô màn hình TFT trên bàn phím và ở khung xem trước lớn — đúng như khi hiện trên phím thật. Mọi GIF đều được app tự chuẩn hoá về 128×128, tối đa 255 frame.", "sub"),
                topGrid,
                Tb("Thư viện GIF tích hợp (21)", "h2"),
                filterRow,
                wrap,
                actionRow);
            return Scroll(root);
        }

        // ================= TRANG 5: THU VIEN ONLINE =================
        private sealed class EffectMeta
        {
            public string File { get; set; }
            public string Name { get; set; }
            public string Source { get; set; }
            public int[] Size { get; set; }
            public int Frames { get; set; }
            public int TftFrames { get; set; }
        }

        private Control BuildOnlinePage()
        {
            var kbState = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED) };
            var kb = new KeyboardView { Mode = KeyboardMode.Screen, State = kbState };
            List<Bitmap> screenFrames = null;

            // Doc manifest nhung trong exe
            var metas = new List<EffectMeta>();
            try
            {
                using (var s = AssetLoader.Open(GifTools.Asset("Assets/effects-manifest.json")))
                using (var ms = new System.IO.MemoryStream())
                {
                    s.CopyTo(ms);
                    metas = JsonSerializer.Deserialize<List<EffectMeta>>(ms.ToArray(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<EffectMeta>();
                }
            }
            catch { }

            var wrap = new WrapPanel();
            var cards = new List<OnlineCard>();
            foreach (var meta in metas)
            {
                var uri = GifTools.Asset("Assets/Gifs/Effects/" + meta.File);
                string sub = meta.Size != null && meta.Size.Length == 2
                    ? "Gốc LED " + meta.Size[0] + "×" + meta.Size[1] + " · " + meta.Frames + " frame"
                    : "";
                var card = new OnlineCard(meta.Name, "Hoàng Phúc Keyboard", sub, uri)
                {
                    Margin = new Thickness(0, 0, 10, 10),
                };
                card.SelectedChanged += async c =>
                {
                    foreach (var o in cards) o.SetSelected(ReferenceEquals(o, c));
                    var frames = await Task.Run(() => GifTools.LoadFrames(c.AssetUri, 128, FitMode.Fill, 48));
                    var old = screenFrames;
                    screenFrames = frames;
                    kb.ScreenFrames = frames;
                    GifTools.DisposeFrames(old);
                };
                card.SaveClicked += c => ShowToast("Đã lưu “" + c.Title + "” vào kho Của tôi (mô phỏng — bản xem trước)");
                cards.Add(card);
                wrap.Children.Add(card);
            }
            _ = Task.Run(async () =>
            {
                foreach (var c in cards)
                {
                    try
                    {
                        var bmp = GifTools.LoadFrame(c.AssetUri, 104);
                        await Dispatcher.UIThread.InvokeAsync(() => c.SetFirstFrame(bmp));
                    }
                    catch { }
                }
                if (cards.Count > 0)
                    await Dispatcher.UIThread.InvokeAsync(() => cards[0].RaiseSelected());
            });

            var emptyNote = Tb("Không có hiệu ứng nào khớp — thử từ khoá khác nhé.", "faint");
            emptyNote.IsVisible = false;
            var search = new TextBox { Watermark = "Tìm hiệu ứng… (neon, cầu vồng, nhịp tim)" };
            search.Classes.Add("search");
            void ApplyFilter()
            {
                string q = (search.Text ?? "").Trim().ToLowerInvariant();
                int visible = 0;
                foreach (var c in cards)
                {
                    bool show = q.Length == 0 || c.Title.ToLowerInvariant().Contains(q);
                    c.IsVisible = show;
                    if (show) visible++;
                }
                emptyNote.IsVisible = visible == 0;
            }
            search.TextChanged += (s, e) => ApplyFilter();

            var tagRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
            foreach (var tag in new[] { "#cầu vồng", "#neon", "#nhịp tim", "#pixel", "#trái tim", "#sóng", "#xoắn ốc", "#anime" })
            {
                var b = new Button { Content = tag };
                b.Classes.Add("chip");
                string word = tag.TrimStart('#');
                b.Click += (s, e) => { search.Text = word; };
                tagRow.Children.Add(b);
            }

            var root = V(14,
                Tb("Thư viện Online", "h1"),
                Tb("Hiệu ứng cộng đồng từ Hoàng Phúc Keyboard. Trong bản xem trước này, cả 16 hiệu ứng đã được chuẩn hoá sẵn cho màn hình TFT và nhúng trong app — duyệt xem không cần mạng.", "sub"),
                Card(V(8, kb, Tb("Hiệu ứng đang chọn phát trong ô màn hình TFT của bàn phím.", "faint")), 14),
                search,
                tagRow,
                wrap,
                emptyNote,
                CardFlat(Tb("Lưu ý: GIF gốc của shop là hiệu ứng LED toàn phím (900 × ~300) — đèn từng phím sáng theo mẫu, app của shop phát qua USB. Bản đưa lên màn hình TFT sẽ cắt phần giữa về 128×128 như bạn đang xem trước ở trên. Nguồn ngoài khác: GIPHY Pixel Art · Pixabay GIFs · GitHub RecalBoxDMD (~600 GIF retro).", "faint")));
            return Scroll(root);
        }

        // ================= TRANG 6: WIDGET TFT =================
        private readonly DispatcherTimer _widgetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private KeyboardView _widgetKb;
        private int _widgetKind; // 0 thong so, 1 dang phat, 2 dong ho
        private readonly DateTime _appStart = DateTime.Now;
        private TextBlock _clockBig;
        private TextBlock _clockDate;

        private Control BuildWidgetPage()
        {
            _widgetKb = new KeyboardView { Mode = KeyboardMode.Screen, State = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED) } };
            _widgetTimer.Tick += (s, e) => RenderWidgetFrame();

            var kinds = new (string Title, string Desc)[]
            {
                ("Thông số máy", "CPU · GPU · RAM · nhiệt độ GPU (giá trị mô phỏng, dao động nhẹ)"),
                ("Đang phát", "Bài hát mô phỏng: tên bài, ca sĩ, cột sóng và thanh tiến trình"),
                ("Đồng hồ & thời tiết", "Giờ thật của máy tính này + thời tiết mô phỏng"),
            };
            var selectorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var cards = new List<Border>();
            for (int i = 0; i < kinds.Length; i++)
            {
                int idx = i;
                var content = V(4, Tb(kinds[i].Title, "h3"), Tb(kinds[i].Desc, "faint"));
                var card = new Border
                {
                    Classes = { "pickcard" },
                    Child = content,
                    Width = 252,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                card.PointerPressed += (s, e) =>
                {
                    _widgetKind = idx;
                    for (int j = 0; j < cards.Count; j++)
                    {
                        cards[j].BorderBrush = j == idx ? UiBrushes.AccentGradient() : UiBrushes.GlassBorder;
                        cards[j].BorderThickness = new Thickness(j == idx ? 1.8 : 1);
                    }
                    RenderWidgetFrame();
                };
                cards.Add(card);
                selectorRow.Children.Add(card);
            }
            cards[0].BorderBrush = UiBrushes.AccentGradient();
            cards[0].BorderThickness = new Thickness(1.8);

            var root = V(14,
                Tb("Widget TFT", "h1"),
                Tb("Widget được vẽ ngay trên máy tính (host) thành ảnh 128×128 rồi đưa vào màn hình phím — đúng cơ chế bản thật sẽ dùng để hiện thông số máy, nhạc đang phát hay đồng hồ.", "sub"),
                Card(_widgetKb, 14),
                Tb("Chọn widget", "h2"),
                selectorRow,
                Tb("Tần suất làm tươi trong bản xem trước: 1 giây/lần. Khi sang trang khác, widget ngừng vẽ để tiết kiệm CPU.", "faint"));
            return Scroll(root);
        }

        private void RenderWidgetFrame()
        {
            if (_widgetKb == null) return;
            double t = (DateTime.Now - _appStart).TotalSeconds;
            Bitmap bmp;
            if (_widgetKind == 0)
            {
                double cpu = Clamp01(0.46 + 0.16 * Math.Sin(t * 0.6) + 0.05 * Math.Sin(t * 2.1));
                double gpu = Clamp01(0.62 + 0.20 * Math.Sin(t * 0.45 + 1.3));
                double ram = Clamp01(0.58 + 0.04 * Math.Sin(t * 0.2));
                int temp = 62 + (int)(6 * Math.Sin(t * 0.45 + 1.3)) + 4;
                bmp = WidgetRenderer.RenderStats(cpu, gpu, ram, temp);
            }
            else if (_widgetKind == 1)
            {
                double progress = t % WidgetRenderer.SongDuration;
                bmp = WidgetRenderer.RenderNowPlaying(progress, t);
            }
            else
            {
                bmp = WidgetRenderer.RenderClock(DateTime.Now);
            }
            var old = _widgetKb.ScreenImage;
            _widgetKb.ScreenImage = bmp;
            old?.Dispose();
        }

        private static double Clamp01(double v) => Math.Max(0.02, Math.Min(0.98, v));

        // ================= TRANG 7: DONG HO & CAI DAT =================
        private Control BuildSettingsPage()
        {
            _clockTimer.Tick += (s, e) => UpdateClockLabels();
            _clockBig = new TextBlock
            {
                FontSize = 44, FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF8)),
            };
            _clockDate = Tb("", "sub");
            UpdateClockLabels();

            var clockCard = Card(V(10,
                Tb("Đồng hồ phím", "h2"),
                _clockBig,
                _clockDate,
                GhostBtn("Đồng bộ đồng hồ", () => ShowToast(PreviewToast))));

            var settingsCard = Card(V(6,
                Tb("Cài đặt", "h2"),
                SettingRow("Dry-run (chế độ an toàn)", "Bật: mọi thao tác chỉ mô phỏng, không ghi xuống phím", true),
                SettingRow("Khởi động cùng Windows", "Tự mở app khi đăng nhập Windows", false),
                SettingRow("Thu nhỏ xuống khay hệ thống", "Đóng cửa sổ thì app vẫn chạy nền ở khay", true),
                SettingRow("Tự động kiểm tra bản cập nhật", "Báo khi có bản Tien Keyboard Studio mới", false)));

            var logoImg = new Avalonia.Controls.Image
            {
                Source = new Bitmap(AssetLoader.Open(GifTools.Asset("Assets/logo.png"))),
                Width = 64, Height = 64,
            };
            var logoWrap = new Border
            {
                CornerRadius = new CornerRadius(14), ClipToBounds = true,
                Width = 64, Height = 64, Child = logoImg,
            };
            var aboutTexts = V(4,
                Tb("Tien Keyboard Studio", "h2"),
                Tb("Bản xem trước giao diện v0.9.1 — UI-only: không chứa mã thiết bị/HID, không ghi xuống phím thật. Mọi nút bấm liên quan tới phím đều chỉ mô phỏng.", "sub"),
                Tb("Logo Mẫu C — chữ “T” tạo hình từ phím spacebar — đã được người dùng duyệt.", "sub"),
                Tb("Font chữ: Be Vietnam Pro (SIL OFL 1.1). GIF thư viện: tự làm cho Tiến. Hiệu ứng cộng đồng: Hoàng Phúc Keyboard.", "faint"));
            var aboutRow = H(14, logoWrap, aboutTexts);
            var aboutCard = Card(aboutRow);

            var root = V(14,
                Tb("Đồng hồ & Cài đặt", "h1"),
                clockCard,
                settingsCard,
                aboutCard);
            return Scroll(root);
        }

        private Control SettingRow(string label, string sub, bool initial)
        {
            var sw = new PillSwitch();
            sw.IsChecked = initial;
            sw.Changed += v => ShowToast("Bản xem trước — công tắc “" + label + "” chỉ minh hoạ");
            var texts = V(2, Tb(label, "h3"), Tb(sub, "sub"));
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            grid.Children.Add(texts);
            sw.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(sw, 1);
            grid.Children.Add(sw);
            var row = CardFlat(grid, 12);
            row.Margin = new Thickness(0, 4);
            return row;
        }

        private void UpdateClockLabels()
        {
            if (_clockBig == null) return;
            var now = DateTime.Now;
            _clockBig.Text = now.ToString("HH:mm:ss");
            _clockDate.Text = "Giờ hiện tại trên máy tính — bấm Đồng bộ để gửi giờ này xuống màn hình phím (mô phỏng).";
        }

        // ================= TRANG 8: DIEU KHIEN TU DIEN THOAI =================
        private Control BuildPhonePage()
        {
            // --- The QR mo phong ---
            var qr = new QrMock { Width = 168, Height = 168, HorizontalAlignment = HorizontalAlignment.Center };
            var pinRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (char d in "4826")
            {
                pinRow.Children.Add(new Border
                {
                    Width = 34, Height = 42, CornerRadius = new CornerRadius(9),
                    Background = new SolidColorBrush(Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)),
                    BorderBrush = UiBrushes.GlassBorder, BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = d.ToString(), FontSize = 17, FontWeight = FontWeight.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    },
                });
            }
            var qrCard = Card(V(12,
                Tb("Quét để kết nối", "h2"),
                qr,
                new TextBlock
                {
                    Text = "http://192.168.1.20:8765",
                    FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                    FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xD5, 0xE6)),
                },
                Tb("Mã PIN đi kèm", "h3"),
                pinRow,
                Tb("Mã chỉ dùng trong mạng nhà, có mã PIN đi kèm. Mã tự đổi mỗi lần mở app (đây là mã mô phỏng).", "faint")));

            // --- Khung dien thoai mo phong ---
            var phoneFx = new[] { "Sóng cầu vồng", "Breathing", "Tĩnh", "Theo nhạc" };
            var fxGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                ColumnSpacing = 8, RowSpacing = 8,
            };
            var fxBorders = new List<Border>();
            for (int i = 0; i < phoneFx.Length; i++)
            {
                var b = new Border
                {
                    Classes = { "pickcard" },
                    Padding = new Thickness(8),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Child = Tb(phoneFx[i], null, 11.5),
                };
                int idx = i;
                b.PointerPressed += (s, e) =>
                {
                    for (int j = 0; j < fxBorders.Count; j++)
                    {
                        fxBorders[j].BorderBrush = j == idx ? UiBrushes.AccentGradient() : UiBrushes.GlassBorder;
                        fxBorders[j].BorderThickness = new Thickness(j == idx ? 1.8 : 1);
                    }
                };
                fxBorders.Add(b);
                Grid.SetColumn(b, i % 2);
                Grid.SetRow(b, i / 2);
                fxGrid.Children.Add(b);
            }
            fxBorders[0].BorderBrush = UiBrushes.AccentGradient();
            fxBorders[0].BorderThickness = new Thickness(1.8);

            // 3 GIF mau trong dien thoai (frame dau, tu thu vien)
            var phoneGifRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var lib = GifCatalog.Library();
            var picks = new[] { "Galaxy Spiral", "Pixel Heart", "Vinyl Spin" };
            var phoneImgs = new List<Avalonia.Controls.Image>();
            foreach (var pick in picks)
            {
                var entry = lib.FirstOrDefault(e => e.Name.Contains(pick));
                var img = new Avalonia.Controls.Image { Width = 64, Height = 64, Stretch = Stretch.Uniform };
                phoneImgs.Add(img);
                var holder = new Border
                {
                    CornerRadius = new CornerRadius(10), ClipToBounds = true,
                    BorderBrush = UiBrushes.GlassBorder, BorderThickness = new Thickness(1),
                    Child = img,
                };
                phoneGifRow.Children.Add(holder);
                if (entry != null)
                {
                    var captured = entry;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var bmp = GifTools.LoadFrame(captured.AssetUri, 64);
                            await Dispatcher.UIThread.InvokeAsync(() => img.Source = bmp);
                        }
                        catch { }
                    });
                }
            }

            var applyPhone = PrimaryBtn("Áp dụng", () => ShowToast("Bản xem trước — điện thoại chỉ là mô hình"));
            applyPhone.HorizontalAlignment = HorizontalAlignment.Stretch;

            var notch = new Border
            {
                Width = 92, Height = 17, CornerRadius = new CornerRadius(9),
                Background = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center,
            };
            var phoneInner = V(11,
                notch,
                H(7,
                    new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)), VerticalAlignment = VerticalAlignment.Center },
                    Tb("Tien Keyboard Remote", null, 14)),
                Tb("Đã kết nối tới PC (mô phỏng)", "faint"),
                Tb("Hiệu ứng nhanh", "h3"),
                fxGrid,
                Tb("GIF màn hình", "h3"),
                phoneGifRow,
                applyPhone);
            var phoneScreen = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x1D)),
                CornerRadius = new CornerRadius(30),
                Padding = new Thickness(14),
                Child = phoneInner,
            };
            var phoneFrame = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x14)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x44)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(38),
                Padding = new Thickness(9),
                Width = 302,
                Child = phoneScreen,
                HorizontalAlignment = HorizontalAlignment.Center,
                BoxShadow = BoxShadows.Parse("0 18 40 0 #80000000"),
            };

            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("360,*"), ColumnSpacing = 14 };
            cols.Children.Add(qrCard);
            Grid.SetColumn(phoneFrame, 1);
            cols.Children.Add(phoneFrame);

            var root = V(14,
                Tb("Điều khiển từ điện thoại", "h1"),
                Tb("Luồng hoạt động: PC cắm phím và chạy app → app hiện mã QR → điện thoại quét (cùng Wi-Fi) → mở trang điều khiển → chọn hiệu ứng/GIF → PC ghi xuống phím.", "sub"),
                cols,
                CardFlat(Tb("Lưu ý: điện thoại không chỉnh trực tiếp được phím — F75 Max không có giao diện điều khiển qua điện thoại và iOS giới hạn kết nối với phụ kiện. PC là cầu nối bắt buộc và phải đang mở app; điện thoại chỉ gửi lệnh qua mạng nhà tới PC.", "faint")));
            return Scroll(root);
        }
    }

    /// <summary>
    /// The hieu ung o trang Thu vien Online: anh dong khi hover, co nut "Lay ve may".
    /// </summary>
    internal sealed class OnlineCard : Border
    {
        private readonly Avalonia.Controls.Image _img;
        private readonly DispatcherTimer _timer;
        private List<Bitmap> _frames;
        private int _idx;
        private bool _hover;
        private bool _selected;
        private bool _saved;
        private readonly Button _saveBtn;

        public string Title { get; }
        public Uri AssetUri { get; }
        public event Action<OnlineCard> SelectedChanged;
        public event Action<OnlineCard> SaveClicked;

        public OnlineCard(string title, string source, string sub, Uri assetUri)
        {
            Title = title;
            AssetUri = assetUri;
            Classes.Add("pickcard");
            Cursor = new Cursor(StandardCursorType.Hand);
            Width = 196;

            _img = new Avalonia.Controls.Image
            {
                Width = 104, Height = 104, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var tags = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            tags.Children.Add(TagPill("LED toàn phím"));
            tags.Children.Add(TagPill("Cộng đồng"));
            _saveBtn = new Button { Content = "Lấy về máy", HorizontalAlignment = HorizontalAlignment.Stretch };
            _saveBtn.Classes.Add("ghost");
            _saveBtn.Click += (s, e) =>
            {
                if (_saved) return;
                _saved = true;
                _saveBtn.Content = "Đã lưu ✓";
                SaveClicked?.Invoke(this);
            };
            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(_img);
            stack.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(new TextBlock { Text = source, FontSize = 10, Foreground = UiBrushes.TextSecondary });
            if (!string.IsNullOrEmpty(sub))
                stack.Children.Add(new TextBlock { Text = sub, FontSize = 9.5, Foreground = UiBrushes.TextSecondary, TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(tags);
            stack.Children.Add(_saveBtn);
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
            PointerPressed += (s, e) => { SelectedChanged?.Invoke(this); };
        }

        private static Border TagPill(string text) => new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x2E, 0x7C, 0x3A, 0xED)),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(7, 2),
            Child = new TextBlock { Text = text, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xB5, 0xFD)) },
        };

        public void SetFirstFrame(Bitmap bmp) { _img.Source = bmp; }

        public void RaiseSelected() => SelectedChanged?.Invoke(this);

        public void SetSelected(bool sel)
        {
            _selected = sel;
            BorderBrush = sel ? UiBrushes.AccentGradient() : UiBrushes.GlassBorder;
            BorderThickness = new Thickness(sel ? 1.8 : 1);
            if (sel) StartAnim(); else if (!_hover) StopAnim();
        }

        private async void StartAnim()
        {
            if (_frames == null)
            {
                try { _frames = await Task.Run(() => GifTools.LoadFrames(AssetUri, 104, FitMode.Fit, 24)); }
                catch { _frames = null; }
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
}
