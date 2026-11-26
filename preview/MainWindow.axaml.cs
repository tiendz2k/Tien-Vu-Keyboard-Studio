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

        // v0.10: nen app + man hinh chao
        private string _currentSkin = "default";
        private bool _skipGreeting;
        private GreetingSweep _sweep;
        private DispatcherTimer _greetingTimer;
        public bool GreetingHoldForShot; // chi ban chup anh dung: giu man hinh chao, khong tu tat

        public MainWindow()
        {
            InitializeComponent();
            Toast.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(240) } };
            GreetingOverlay.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) } };

            BuildNav();
            _pages["home"] = BuildHomePage();
            _pages["rgb"] = BuildRgbPage();
            _pages["music"] = BuildMusicPage();
            _pages["gamesense"] = BuildGameSensePage();
            _pages["gif"] = BuildGifPage();
            _pages["online"] = BuildOnlinePage();
            _pages["store"] = BuildStorePage();
            _pages["widget"] = BuildWidgetPage();
            _pages["settings"] = BuildSettingsPage();
            _pages["phone"] = BuildPhonePage();
            foreach (var p in _pages.Values) { p.IsVisible = false; PageHost.Children.Add(p); }
            ShowPage("home");

            // Man hinh chao: gan vet sang + su kien bo qua
            _sweep = new GreetingSweep();
            GreetingSweepHost.Children.Add(_sweep);
            GreetingOverlay.PointerPressed += (s, e) => DismissGreeting();
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && GreetingOverlay.IsVisible) DismissGreeting();
            };
            Opened += (s, e) => { if (!_skipGreeting) ShowGreeting(); };
        }

        // ================= MAN HINH CHAO =================
        public void ShowGreeting()
        {
            GreetingOverlay.IsVisible = true;
            GreetingOverlay.Opacity = 1;
            GreetingStatus.Text = "Đang nạp giao diện…";
            _sweep.Play();
            _greetingTimer?.Stop();
            if (GreetingHoldForShot) return;
            _greetingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            _greetingTimer.Tick += (s, e) =>
            {
                _greetingTimer.Stop();
                GreetingStatus.Text = "Sẵn sàng";
                DismissGreeting();
            };
            _greetingTimer.Start();
        }

        public void DismissGreeting()
        {
            if (!GreetingOverlay.IsVisible) return;
            _greetingTimer?.Stop();
            _sweep.Stop();
            GreetingOverlay.Opacity = 0;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
            t.Tick += (s, e) => { t.Stop(); GreetingOverlay.IsVisible = false; };
            t.Start();
        }

        // ================= NEN APP + MAU CHU DE =================
        private static readonly Dictionary<string, Bitmap> SkinCache = new Dictionary<string, Bitmap>();

        private static Bitmap LoadSkin(string file)
        {
            if (SkinCache.TryGetValue(file, out var b)) return b;
            using (var s = AssetLoader.Open(GifTools.Asset("Assets/Wallpapers/" + file)))
            {
                var bmp = new Bitmap(s);
                SkinCache[file] = bmp;
                return bmp;
            }
        }

        /// <summary>Doi hinh nen toan app (skin: default/aurora/sunset/forest/space/custom).</summary>
        public void ApplyBackground(string skin)
        {
            _currentSkin = skin;
            string file = skin == "aurora" ? "skin-aurora.jpg"
                        : skin == "sunset" ? "skin-sunset.jpg"
                        : skin == "forest" ? "skin-forest.jpg"
                        : skin == "space" ? "skin-space.jpg"
                        : null;
            if (file == null)
            {
                BackdropImage.IsVisible = false;
                BackdropShade.IsVisible = false;
            }
            else
            {
                BackdropImage.Source = LoadSkin(file);
                BackdropImage.IsVisible = true;
                BackdropShade.IsVisible = true;
            }
        }

        /// <summary>Doi mau chu de: sua truc tiep brush tai nguyen dung chung + mau code-side.</summary>
        public void ApplyAccent(Color a, Color b)
        {
            UiBrushes.AccentA = a;
            UiBrushes.AccentB = b;
            if (Application.Current != null &&
                Application.Current.Resources.TryGetValue("AccentGradient", out var res) &&
                res is LinearGradientBrush g && g.GradientStops.Count >= 2)
            {
                g.GradientStops[0].Color = a;
                g.GradientStops[1].Color = b;
            }
            if (Application.Current != null &&
                Application.Current.Resources.TryGetValue("AccentGradientHover", out var res2) &&
                res2 is LinearGradientBrush g2 && g2.GradientStops.Count >= 2)
            {
                g2.GradientStops[0].Color = Lighten(a, 1.25);
                g2.GradientStops[1].Color = Lighten(b, 1.2);
            }
        }

        private static Color Lighten(Color c, double f) => Color.FromRgb(
            (byte)Math.Min(255, c.R * f), (byte)Math.Min(255, c.G * f), (byte)Math.Min(255, c.B * f));

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
                ("gamesense", "GameSense", "M12 12 m-7.5 0 a7.5 7.5 0 1 0 15 0 a7.5 7.5 0 1 0-15 0 M12 2.5v3.5 M12 18v3.5 M2.5 12H6 M18 12h3.5 M12 12 m-1.7 0 a1.7 1.7 0 1 0 3.4 0 a1.7 1.7 0 1 0-3.4 0"),
                ("gif", "Màn hình GIF", "M4 4.5 A1.5 1.5 0 0 1 5.5 3h13A1.5 1.5 0 0 1 20 4.5v15a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 19.5Z M8.3 10.2 a1.4 1.4 0 1 0 .1 0 M20 16.5 15.5 12 5 21"),
                ("online", "Thư viện Online", "M12 12 m-9.5 0 a9.5 9.5 0 1 0 19 0 a9.5 9.5 0 1 0-19 0 M2.5 12h19 M12 2.5c2.9 2.7 4.4 5.9 4.4 9.5s-1.5 6.8-4.4 9.5c-2.9-2.7-4.4-5.9-4.4-9.5S9.1 5.2 12 2.5Z"),
                ("store", "Chợ plugin", "M5.5 8h13l-.9 11.5A1.5 1.5 0 0 1 16.1 21H7.9a1.5 1.5 0 0 1-1.5-1.5L5.5 8Z M8.5 10.5V6.8a3.5 3.5 0 0 1 7 0v3.7"),
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
                var card = new OnlineCard(meta.Name, "Cộng đồng", sub, uri)
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
                Tb("Hiệu ứng cộng đồng. Trong bản xem trước này, cả 16 hiệu ứng đã được chuẩn hoá sẵn cho màn hình TFT và nhúng trong app — duyệt xem không cần mạng.", "sub"),
                Card(V(8, kb, Tb("Hiệu ứng đang chọn phát trong ô màn hình TFT của bàn phím.", "faint")), 14),
                search,
                tagRow,
                wrap,
                emptyNote,
                CardFlat(Tb("Lưu ý: GIF gốc cộng đồng là hiệu ứng LED toàn phím (900 × ~300) — đèn từng phím sáng theo mẫu, phát qua USB. Bản đưa lên màn hình TFT sẽ cắt phần giữa về 128×128 như bạn đang xem trước ở trên. Nguồn ngoài khác: GIPHY Pixel Art · Pixabay GIFs · GitHub RecalBoxDMD (~600 GIF retro).", "faint")));
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
                Tb("Bản xem trước giao diện v0.10 — UI-only: không chứa mã thiết bị/HID, không ghi xuống phím thật. Mọi nút bấm liên quan tới phím đều chỉ mô phỏng.", "sub"),
                Tb("Logo Mẫu C — chữ “T” tạo hình từ phím spacebar — đã được người dùng duyệt.", "sub"),
                Tb("Font chữ: Be Vietnam Pro (SIL OFL 1.1). GIF thư viện: tự làm cho Tiến.", "faint"));
            var aboutRow = H(14, logoWrap, aboutTexts);
            var aboutCard = Card(aboutRow);

            // ===== Muc GIAO DIEN (v0.10, cau truc theo trang theme cua AULA) =====
            var appearanceCard = BuildAppearanceCard();

            var root = V(14,
                Tb("Đồng hồ & Cài đặt", "h1"),
                appearanceCard,
                clockCard,
                settingsCard,
                aboutCard);
            return Scroll(root);
        }

        /// <summary>
        /// Muc "Giao dien" trong Cai dat: hinh nen app (mac dinh / skin co san / anh cua ban),
        /// mau chu de (4 mau), xem truoc Sang/Toi va tuy chon man hinh chao.
        /// Doi nen ap dung NGAY cho toan app (panel kinh se cho nen xuyen qua).
        /// </summary>
        private Control BuildAppearanceCard()
        {
            // --- Chon hinh nen ---
            var skins = new (string Id, string Name, string File)[]
            {
                ("default", "Mặc định", null),
                ("aurora", "Cực quang", "skin-aurora.jpg"),
                ("sunset", "Hoàng hôn", "skin-sunset.jpg"),
                ("forest", "Rừng sâu", "skin-forest.jpg"),
                ("space", "Vũ trụ sâu", "skin-space.jpg"),
            };
            var skinCards = new List<Border>();
            var skinRow = new WrapPanel();
            foreach (var (id, name, file) in skins)
            {
                Control preview;
                if (file == null)
                {
                    preview = new Border
                    {
                        Height = 56, CornerRadius = new CornerRadius(9),
                        Background = new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                            GradientStops =
                            {
                                new GradientStop(Color.FromRgb(0x12, 0x12, 0x1A), 0),
                                new GradientStop(Color.FromRgb(0x0B, 0x0B, 0x10), 1),
                            },
                        },
                    };
                }
                else
                {
                    preview = new Avalonia.Controls.Image
                    {
                        Height = 56, Stretch = Stretch.UniformToFill,
                        Source = LoadSkin(file),
                    };
                }
                var previewHost = new Border
                {
                    CornerRadius = new CornerRadius(9), ClipToBounds = true,
                    Child = preview,
                };
                var cardContent = V(6, previewHost, Tb(name, null, 11.5));
                var card = new Border
                {
                    Classes = { "pickcard" },
                    Child = cardContent,
                    Width = 118,
                    Margin = new Thickness(0, 0, 9, 9),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                string sid = id; string sname = name;
                card.PointerPressed += (s, e) =>
                {
                    ApplyBackground(sid);
                    foreach (var c in skinCards)
                    {
                        bool sel = ReferenceEquals(c, card);
                        c.BorderBrush = sel ? UiBrushes.AccentGradient() : UiBrushes.GlassBorder;
                        c.BorderThickness = new Thickness(sel ? 1.8 : 1);
                    }
                    ShowToast(sid == "default"
                        ? "Đã về nền mặc định (phẳng, trung tính)"
                        : "Đã đổi hình nền app: " + sname + " — panel kính cho nền xuyên qua");
                };
                skinCards.Add(card);
                skinRow.Children.Add(card);
            }
            skinCards[0].BorderBrush = UiBrushes.AccentGradient();
            skinCards[0].BorderThickness = new Thickness(1.8);

            var customBtn = GhostBtn("Ảnh của bạn…", () =>
                ShowToast("Bản xem trước: bản thật sẽ mở hộp chọn ảnh (tối đa 4MB, tự nén) và đặt làm nền ngay như các skin có sẵn"));

            // --- Mau chu de (4 mau nhu AULA) ---
            var accents = new (string Name, Color A, Color B)[]
            {
                ("Tím", Color.FromRgb(0x7C, 0x3A, 0xED), Color.FromRgb(0x22, 0xD3, 0xEE)),
                ("Dương", Color.FromRgb(0x25, 0x63, 0xEB), Color.FromRgb(0x38, 0xBD, 0xF8)),
                ("Ngọc", Color.FromRgb(0x05, 0x96, 0x69), Color.FromRgb(0x34, 0xD3, 0x99)),
                ("Hồng", Color.FromRgb(0xDB, 0x27, 0x77), Color.FromRgb(0xFB, 0x71, 0x85)),
            };
            var accentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
            foreach (var (name, ca, cb) in accents)
            {
                var sw = new Border
                {
                    Width = 36, Height = 36, CornerRadius = new CornerRadius(11),
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(ca, 0), new GradientStop(cb, 1) },
                    },
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                ToolTip.SetTip(sw, name);
                var capA = ca; var capB = cb; string capName = name;
                sw.PointerPressed += (s, e) =>
                {
                    ApplyAccent(capA, capB);
                    ShowToast("Đã áp dụng màu chủ đề: " + capName);
                };
                accentRow.Children.Add(sw);
            }

            // --- Xem truoc Sang / Toi ---
            Border ModePreview(bool dark, string label, string state)
            {
                var lines = new StackPanel { Spacing = 5, Margin = new Thickness(10) };
                lines.Children.Add(new Border { Height = 7, CornerRadius = new CornerRadius(3.5), Width = 62, HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(dark ? Color.FromRgb(0x3A, 0x3A, 0x4C) : Color.FromRgb(0xC9, 0xCD, 0xD8)) });
                lines.Children.Add(new Border { Height = 7, CornerRadius = new CornerRadius(3.5), Width = 92, HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(dark ? Color.FromRgb(0x2C, 0x2C, 0x3C) : Color.FromRgb(0xDD, 0xE0, 0xE8)) });
                lines.Children.Add(new Border { Height = 7, CornerRadius = new CornerRadius(3.5), Width = 44, HorizontalAlignment = HorizontalAlignment.Left, Background = UiBrushes.AccentGradient() });
                var mock = new Border
                {
                    CornerRadius = new CornerRadius(9), Height = 74,
                    Background = new SolidColorBrush(dark ? Color.FromRgb(0x14, 0x14, 0x1E) : Color.FromRgb(0xF2, 0xF3, 0xF7)),
                    Child = lines,
                };
                var content = V(6, mock, H(6, Tb(label, "h3"), Tb("· " + state, "faint")));
                var card = new Border { Classes = { "pickcard" }, Child = content, Width = 168 };
                if (dark)
                {
                    card.BorderBrush = UiBrushes.AccentGradient();
                    card.BorderThickness = new Thickness(1.8);
                }
                return card;
            }
            var modeRow = H(10,
                ModePreview(true, "Tối", "đang dùng"),
                ModePreview(false, "Sáng", "sắp có ở bản thật"));

            // --- Man hinh chao ---
            var replayBtn = GhostBtn("Xem lại màn hình chào", () => ShowGreeting());
            var skipSw = new PillSwitch();
            skipSw.Changed += v => { _skipGreeting = v; };
            var skipTexts = V(2,
                Tb("Bỏ qua màn hình chào mừng", "h3"),
                Tb("Bật: mở app vào thẳng trang chính, không hiện màn hình chào", "sub"));
            var skipGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            skipGrid.Children.Add(skipTexts);
            skipSw.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(skipSw, 1);
            skipGrid.Children.Add(skipSw);
            var skipRow = CardFlat(skipGrid, 12);

            return Card(V(12,
                Tb("Giao diện", "h2"),
                Tb("Hình nền app", "h3"),
                Tb("Chọn nền có sẵn hoặc dùng ảnh của bạn — các panel kính mờ (Liquid Glass) sẽ cho nền xuyên qua. Nền mặc định là phẳng, trung tính.", "sub"),
                skinRow,
                customBtn,
                Tb("Màu chủ đề", "h3"),
                accentRow,
                Tb("Chế độ giao diện", "h3"),
                modeRow,
                Tb("Màn hình chào", "h3"),
                H(10, replayBtn),
                skipRow));
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

        // ================= TRANG MOI (v0.10): GAMESENSE (Y TUONG) =================
        private static Border ConceptChip(string text) => new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x2E, 0x7C, 0x3A, 0xED)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xA7, 0x8B, 0xFA)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(12, 6),
            Child = Tb(text, null, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        private Control GsEventRow(string title, string desc, string[] actions, int initialAction, bool initialOn,
            Control extra = null, Action<int> onAction = null)
        {
            var sw = new PillSwitch();
            sw.IsChecked = initialOn;
            var texts = V(2, Tb(title, "h3"), Tb(desc, "sub"));
            var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            top.Children.Add(texts);
            sw.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(sw, 1);
            top.Children.Add(sw);
            var (seg, _) = Segmented(actions, initialAction, i => onAction?.Invoke(i));
            var body = V(8, top, seg);
            if (extra != null) body.Children.Add(extra);
            var row = CardFlat(body, 12);
            row.Margin = new Thickness(0, 4);
            return row;
        }

        private Control BuildGameSensePage()
        {
            // --- The trang thai dich vu GSI ---
            var statusDot = new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.FromRgb(0x8A, 0x87, 0xA0)), VerticalAlignment = VerticalAlignment.Center };
            var statusText = Tb("Đang tắt — bật công tắc để bắt đầu lắng nghe (mô phỏng)", "sub");
            var listenSw = new PillSwitch();
            listenSw.Changed += v =>
            {
                statusDot.Fill = new SolidColorBrush(v ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0x8A, 0x87, 0xA0));
                statusText.Text = v
                    ? "Đang lắng nghe tại 127.0.0.1:13650 (mô phỏng) — chờ CS2 kết nối…"
                    : "Đang tắt — bật công tắc để bắt đầu lắng nghe (mô phỏng)";
            };
            var listenTop = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            listenTop.Children.Add(V(2,
                Tb("Dịch vụ lắng nghe CS2 (GSI)", "h3"),
                Tb("CS2 tự gửi trạng thái trận đấu về máy bạn qua Game State Integration — chuẩn mở của Valve. App chỉ nhận dữ liệu game tự gửi, không đọc bộ nhớ game.", "sub")));
            listenSw.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(listenSw, 1);
            listenTop.Children.Add(listenSw);
            var portBox = new TextBox { Text = "13650", Width = 96 };
            portBox.Classes.Add("search");
            var cfgBtn = GhostBtn("Tạo file cfg cho CS2", () =>
                ShowToast("Bản xem trước: bản thật sẽ ghi file gamestate_integration_tkeyboard.cfg vào thư mục cfg của CS2"));
            var statusRow = H(8, statusDot, statusText);
            statusRow.Children[1].VerticalAlignment = VerticalAlignment.Center;
            var serviceCard = Card(V(10,
                listenTop,
                H(10, Tb("Cổng lắng nghe:", "sub"), portBox, cfgBtn),
                statusRow));

            // --- Anh xa su kien -> hanh dong ---
            var flashExtra = V(6,
                H(8, Tb("Ảnh phủ chống loá:", "sub"),
                    ChipBtn("EldenRing.jpg"), ChipBtn("PureBlack.jpg"), ChipBtn("Ảnh của bạn…")),
                H(10, Tb("Độ mờ lớp phủ:", "sub"), new Slider { Minimum = 0.3, Maximum = 1, Value = 0.85, Width = 200 }));
            var mapCard = Card(V(6,
                Tb("Ánh xạ sự kiện → hành động", "h2"),
                Tb("Mỗi sự kiện trong trận có thể kích hoạt một hành động trên máy — và (chỉ có ở T Keyboard) cả phản ứng bằng đèn phím.", "sub"),
                GsEventRow("Khi bạn chết",
                    "Sự kiện: player_state.health về 0",
                    new[] { "Mở trang web", "Chạy chương trình", "Nháy đèn phím" }, 0, true),
                GsEventRow("Khi bị flashbang",
                    "Sự kiện: player_state.flashed tăng vọt — hiện lớp phủ bảo vệ mắt toàn màn hình",
                    new[] { "Overlay chống loá", "Làm tối đèn", "Không làm gì" }, 0, true, flashExtra),
                GsEventRow("Khi thắng round",
                    "Sự kiện: round.win_team là đội của bạn",
                    new[] { "Đèn ăn mừng (stream)", "Mở nhạc", "Không làm gì" }, 0, true),
                GsEventRow("Khi đặt bom",
                    "Sự kiện: bomb.state = planted",
                    new[] { "Đèn đếm ngược", "Không làm gì" }, 0, false)));

            // --- Diem hon ban goc ---
            var plusCard = Card(V(8,
                Tb("Điểm hơn bản gốc", "h2"),
                Tb("Ở app tham khảo, GameSense chỉ mở trang web / chạy chương trình khi chết và phủ overlay chống loá khi bị flash — KHÔNG có phản ứng bằng đèn theo game. Ở T Keyboard, cùng sự kiện đó còn đẩy xuống ĐÈN PHÍM thật qua kênh stream của chính app (mục “Nháy đèn phím”, “Đèn ăn mừng”, “Đèn đếm ngược” ở trên).", "sub")));
            plusCard.BorderBrush = UiBrushes.AccentGradient();
            plusCard.BorderThickness = new Thickness(1.6);

            // --- File cfg mau ---
            var cfgText = new TextBlock
            {
                Text = "\"Tien Keyboard Studio — CS2 GSI\"\n{\n  \"uri\"     \"http://127.0.0.1:13650\"\n  \"timeout\" \"5.0\"\n  \"auth\"    { \"token\" \"tk-token\" }\n  \"data\"    { \"player_state\" \"1\"   \"player_weapons\" \"1\"   \"round\" \"1\"   \"bomb\" \"1\" }\n}",
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xD5, 0xE6)),
            };
            var cfgCard = Card(V(8, Tb("File cfg mẫu (game tự gửi dữ liệu về)", "h3"), cfgText));

            var root = V(14,
                Tb("GameSense", "h1"),
                Tb("Phản ứng theo sự kiện trong game CS2: chết thì mở sẵn trang web/chương trình, bị flash thì có lớp phủ chống loá — và ở app mình, thêm cả phản ứng bằng đèn phím.", "sub"),
                ConceptChip("Ý tưởng — chưa có trong bản thật"),
                serviceCard,
                mapCard,
                plusCard,
                cfgCard);
            return Scroll(root);
        }

        private Button ChipBtn(string text)
        {
            var b = new Button { Content = text };
            b.Classes.Add("chip");
            b.Click += (s, e) => ShowToast("Bản xem trước — lựa chọn này chỉ minh hoạ");
            return b;
        }

        // ================= TRANG MOI (v0.10): CHO PLUGIN (Y TUONG) =================
        private sealed class PluginItem
        {
            public string Name, Author, Cat, CatLabel, Desc, Detail, Meta, Initials;
            public Color GA, GB;
        }

        private Control BuildStorePage()
        {
            var items = new List<PluginItem>
            {
                new PluginItem
                {
                    Name = "DeepSeek Harness Boot", Author = "Cộng đồng GitHub", Cat = "boot", CatLabel = "Hiệu ứng khởi động",
                    Desc = "Màn hình chào kiểu “AI harness”: logo hiện dần kèm dòng trạng thái nạp module — gọn, nhanh, rất công nghệ.",
                    Detail = "Khi mở app, màn hình chào chạy chuỗi nạp ngắn (logo → kiểm tra module → sẵn sàng) trong khoảng 2 giây, bấm để bỏ qua. Phong cách tối giản, hợp với người thích cảm giác “hệ thống AI đang khởi động”.",
                    Meta = "Phiên bản 1.2.0 · 0,4 MB · Miễn phí", Initials = "DS",
                    GA = Color.FromRgb(0x1D, 0x4E, 0xD8), GB = Color.FromRgb(0x0E, 0x9B, 0x8A),
                },
                new PluginItem
                {
                    Name = "550C Boot", Author = "Cộng đồng GitHub", Cat = "boot", CatLabel = "Hiệu ứng khởi động",
                    Desc = "Màn hình chào phong cách đồng hồ tốc độ: vòng tua quét một vòng rồi nhường chỗ cho logo.",
                    Detail = "Lấy cảm hứng từ bảng đồng hồ xe đua 550C: kim quét từ trái sang phải kèm vệt sáng, kết thúc bằng logo app. Âm thanh không kèm theo; thời lượng ~1,8 giây, bỏ qua được.",
                    Meta = "Phiên bản 1.0.3 · 0,8 MB · Miễn phí", Initials = "550C",
                    GA = Color.FromRgb(0xB9, 0x1C, 0x1C), GB = Color.FromRgb(0xF5, 0x9E, 0x0B),
                },
                new PluginItem
                {
                    Name = "Wallpaper Engine Sync", Author = "Cộng đồng GitHub", Cat = "wallpaper", CatLabel = "Hình nền",
                    Desc = "Đồng bộ hình nền app với Wallpaper Engine: đổi cảnh trong WE là nền app đổi theo.",
                    Detail = "Công cụ nền đọc cảnh đang chạy của Wallpaper Engine và dùng khung hình của nó làm hình nền app (mục Giao diện trong Cài đặt). Cần có Wallpaper Engine; chỉ đọc ảnh, không can thiệp vào WE.",
                    Meta = "Phiên bản 2.1.0 · 1,1 MB · Miễn phí", Initials = "WE",
                    GA = Color.FromRgb(0x43, 0x38, 0xCA), GB = Color.FromRgb(0x7C, 0x3A, 0xED),
                },
                new PluginItem
                {
                    Name = "Aurora Wave Pack", Author = "T Keyboard Team", Cat = "led", CatLabel = "Hiệu ứng đèn",
                    Desc = "Gói 6 hiệu ứng sóng đèn từng phím qua kênh stream: sóng ngang, sóng chéo, mưa sao, nhịp thở đôi…",
                    Detail = "6 hiệu ứng chạy bằng kênh stream từng phím của T Keyboard (xem trước ngay trên bàn phím trong app). Tốc độ và độ sáng dùng chung thanh trượt ở trang Ánh sáng RGB.",
                    Meta = "Phiên bản 1.0.0 · 0,2 MB · Miễn phí", Initials = "AW",
                    GA = Color.FromRgb(0x7C, 0x3A, 0xED), GB = Color.FromRgb(0x22, 0xD3, 0xEE),
                },
                new PluginItem
                {
                    Name = "Tien Galaxy Boot", Author = "T Keyboard Team", Cat = "boot", CatLabel = "Hiệu ứng khởi động",
                    Desc = "Màn hình chào riêng của T Keyboard: vệt sáng cầu vồng quét qua hàng phím + logo Mẫu C.",
                    Detail = "Đây chính là màn hình chào mặc định của app — vệt sáng quét ngang hàng phím trong ~2 giây, bấm hoặc nhấn Esc để bỏ qua. Đang bật sẵn; xem lại bất cứ lúc nào ở Cài đặt → Giao diện.",
                    Meta = "Phiên bản 1.0.0 · Có sẵn trong app · Miễn phí", Initials = "TG",
                    GA = Color.FromRgb(0x5B, 0x21, 0xB6), GB = Color.FromRgb(0x0E, 0x74, 0x90),
                },
            };

            // --- Chi tiet (cap nhat khi chon the) ---
            var detailName = Tb(items[0].Name, "h2");
            var detailMeta = Tb(items[0].Meta, "faint");
            var detailDesc = Tb(items[0].Detail, "sub");
            var detailCat = Tb(items[0].CatLabel + " · " + items[0].Author, "sub");
            var enableSw = new PillSwitch();
            enableSw.IsChecked = true;
            var detailCard = Card(V(10,
                detailName, detailCat, detailDesc, detailMeta,
                H(12,
                    H(8, Tb("Bật", "h3"), enableSw),
                    GhostBtn("Xem trước", () =>
                    {
                        if (detailName.Text != null && detailName.Text.Contains("Boot")) ShowGreeting();
                        else ShowToast("Bản xem trước — hiệu ứng sẽ chạy thử ngay trong app (mô phỏng)");
                    }),
                    PrimaryBtn("Cài đặt", () => ShowToast("Đã cài (mô phỏng) — bản xem trước không tải gì về máy")))));

            // --- Luoi the ---
            var wrap = new WrapPanel();
            var cards = new List<Border>();
            void SelectItem(int idx)
            {
                var it = items[idx];
                detailName.Text = it.Name;
                detailCat.Text = it.CatLabel + " · " + it.Author;
                detailDesc.Text = it.Detail;
                detailMeta.Text = it.Meta;
                for (int j = 0; j < cards.Count; j++)
                {
                    cards[j].BorderBrush = j == idx ? UiBrushes.AccentGradient() : UiBrushes.GlassBorder;
                    cards[j].BorderThickness = new Thickness(j == idx ? 1.8 : 1);
                }
            }
            for (int i = 0; i < items.Count; i++)
            {
                int idx = i;
                var it = items[i];
                var banner = new Border
                {
                    Height = 74, CornerRadius = new CornerRadius(9),
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(it.GA, 0), new GradientStop(it.GB, 1) },
                    },
                    Child = new TextBlock
                    {
                        Text = it.Initials, FontSize = 21, FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    },
                };
                var pill = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x2E, 0x7C, 0x3A, 0xED)),
                    CornerRadius = new CornerRadius(999),
                    Padding = new Thickness(8, 3),
                    Child = new TextBlock { Text = it.CatLabel, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xB5, 0xFD)) },
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                var content = V(7, banner, Tb(it.Name, "h3"), Tb(it.Author, "faint"), pill, Tb(it.Desc, "sub"));
                var card = new Border
                {
                    Classes = { "pickcard" },
                    Child = content,
                    Width = 252,
                    Margin = new Thickness(0, 0, 10, 10),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                card.PointerPressed += (s, e) => SelectItem(idx);
                cards.Add(card);
                wrap.Children.Add(card);
            }
            SelectItem(4); // mac dinh chon Tien Galaxy Boot (cua minh)

            // --- Loc theo danh muc ---
            var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var cats = new (string Label, string Cat)[]
            {
                ("Tất cả", "all"), ("Hiệu ứng khởi động", "boot"), ("Hình nền", "wallpaper"), ("Hiệu ứng đèn", "led"),
            };
            var filterBtns = new List<Button>();
            foreach (var (label, cat) in cats)
            {
                var b = new Button { Content = label };
                b.Classes.Add("chip");
                if (cat == "all") b.Classes.Add("sel");
                b.Click += (s, e) =>
                {
                    foreach (var o in filterBtns) o.Classes.Remove("sel");
                    b.Classes.Add("sel");
                    for (int j = 0; j < cards.Count; j++)
                        cards[j].IsVisible = cat == "all" || items[j].Cat == cat;
                };
                filterBtns.Add(b);
                filterRow.Children.Add(b);
            }

            var root = V(14,
                Tb("Chợ plugin", "h1"),
                Tb("Kho cộng đồng: hiệu ứng khởi động, công cụ hình nền và gói hiệu ứng đèn do cộng đồng đóng góp — chọn một món để xem chi tiết, bật/tắt và xem trước.", "sub"),
                ConceptChip("Ý tưởng — chưa có trong bản thật"),
                filterRow,
                wrap,
                detailCard,
                CardFlat(Tb("Ghi chú cơ chế thật: ở hệ sinh thái tham khảo, không có SDK plugin chính thức — “plugin” đến từ cộng đồng GitHub (các repo chia sẻ hiệu ứng/cấu hình) và chợ cấu hình đám mây của cộng đồng. Trang này mô phỏng trải nghiệm chợ; bản thật chỉ cài từ nguồn đã kiểm chứng.", "faint")));
            return Scroll(root);
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
