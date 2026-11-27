using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Win32;
using QRCoder;
using SixLabors.ImageSharp.Processing;
using PathShape = Avalonia.Controls.Shapes.Path;
using EllipseShape = Avalonia.Controls.Shapes.Ellipse;

namespace TienKeyboardStudio3
{
    public partial class MainWindow : Window
    {
        // ================= Trang thai dung chung =================
        private readonly F75Device _device = new F75Device();
        private readonly ExperimentalDevice _exp = new ExperimentalDevice();
        // v3.4 Driver Pack 1: ho Sinowealth kb_by_v2 (transport rieng, cung khoa an toan)
        private readonly KbByV2Device _kb = new KbByV2Device();
        private TextBlock _kbIdStatus;
        private TextBlock _kbInfoStatus;
        private bool _dryRun = true;
        private bool _experimental = false;
        private bool _busy;
        private KeyboardModel _selectedModel = ModelDatabase.F75Max;
        private List<DetectedDevice> _detected = new List<DetectedDevice>();

        private readonly RgbAnimState _rgb = new RgbAnimState { Effect = 10, Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3, Speed = 2, Direction = 0 };
        private readonly RgbAnimState _music = new RgbAnimState { Effect = 101, Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3, Speed = 2, MusicGain = 1.0, MusicBg = 1 };
        private readonly AudioCapture _audio = new AudioCapture();
        private bool _musicPlaying;
        private int _musicMode; // 0 visualizer stream, 1 nhip firmware, 2 feed 0x78
        private CancellationTokenSource _musicSendCts;
        private bool _musicExpLogged;
        private bool _lastBeat;
        private long _lastPulseTick;

        private readonly Dictionary<string, Control> _pages = new Dictionary<string, Control>();
        private readonly List<(string Key, Button Btn, PathShape Icon)> _nav = new List<(string, Button, PathShape)>();
        private readonly List<(Button Btn, string Reason)> _writeButtons = new List<(Button, string)>();
        private string _currentPage = "";

        private DispatcherTimer _toastTimer;
        private DispatcherTimer _toastHideTimer;

        // Log dung chung (trang chu + trang per-key cung xem mot dong log)
        private readonly List<string> _logLines = new List<string>();
        private TextBox _logBox;
        private TextBox _expLogBox;

        // Trang chu
        private TextBlock _homeFxValue;
        private TextBlock _homeGifValue;
        private TextBlock _homeDryValue;
        private StackPanel _detectedPanel;

        // Trang GIF
        private GifItem _selectedGif;
        private ProgressBar _gifProgress;
        private TextBlock _gifInfo;
        private WrapPanel _gifWrap;
        private readonly List<GifCard> _gifCards = new List<GifCard>();
        private string _gifFilter = null; // null = tat ca; "Mine" = Cua toi; "Online" = Thu vien Online dong goi san; con lai = group
        private GifFitMode _gifFit = GifFitMode.Fill;

        // Trang Widget
        private readonly DispatcherTimer _widgetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private KeyboardView _widgetKb;
        private int _widgetKind; // 0 thong so, 1 dang phat, 2 dong ho
        private SystemStats _lastStats;
        private WeatherInfo _weather;
        private bool _widgetAuto;
        private int _widgetTickCount;
        private ProgressBar _widgetProgress;

        // Trang Cai dat
        private TextBlock _clockBig;
        private TextBlock _clockDate;
        private PillSwitch _drySwitch;
        private bool _syncingDry;

        // Stage 2
        private readonly AppProfileService _profiles = new AppProfileService();
        private readonly MacroService _macros = new MacroService();
        private readonly TypingStatsService _typing = new TypingStatsService();
        private bool _profilesOn;
        private bool _typingOn;
        private TextBlock _profileCurrent;
        private StackPanel _profileList;
        private TextBlock _macroStatus;
        private StackPanel _macroList;
        private TextBlock _typingStatus;
        private KeyboardView _heatKb;
        private readonly RgbAnimState _heatState = new RgbAnimState { Effect = 103, Brightness = 3 };

        // Dien thoai
        private readonly PhoneRemoteServer _phone = new PhoneRemoteServer();
        private Avalonia.Controls.Image _qrImage;
        private TextBlock _phoneUrlText;
        private StackPanel _pinRow;
        private TextBlock _phoneStatus;

        private bool _trayEnabled = true;
        private bool _reallyExit;

        // v3.3: cai dat giao dien/plugin/GameSense (luu trong settings.json)
        private readonly LibrarySettings _ui = GifLibraryStore.Load();
        private static readonly Dictionary<string, Bitmap> SkinCache = new Dictionary<string, Bitmap>();
        private GreetingSweep _sweep;
        private DispatcherTimer _greetingTimer;
        private DispatcherTimer _terminalTimer;
        private string _greetingStyleNow = GreetingStyles.Galaxy;
        private TextBlock _greetingStyleText;

        // v3.3: GameSense
        private readonly GameSenseServer _gsi = new GameSenseServer();
        private FlashOverlay _flashOverlay;
        private EllipseShape _gsDot;
        private TextBlock _gsStatusText;
        private TextBlock _gsLastEvent;
        private bool _flashDimApplied;

        public MainWindow()
        {
            InitializeComponent();
            Toast.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(240) } };

            _device.Log = AppendLog;
            _exp.Log = AppendLog;
            _kb.Log = AppendLog;
            _device.DryRun = true;
            _exp.DryRun = true;
            _kb.DryRun = true;

            DryToggle.IsCheckedChanged += (s, e) => SetDryRun(DryToggle.IsChecked == true, fromUi: true);

            // v3.3: hieu ung mo cho modal plugin + man hinh chao
            GreetingOverlay.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) } };
            PluginModal.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) } };
            PluginModalDim.PointerPressed += (s, e) => ClosePluginModal();
            GreetingOverlay.PointerPressed += (s, e) => DismissGreeting();
            KeyDown += (s, e) =>
            {
                if (e.Key != Key.Escape) return;
                if (GreetingOverlay.IsVisible) DismissGreeting();
                else if (PluginModal.IsVisible) ClosePluginModal();
            };
            _gsi.Log = msg => AppendLog(msg);
            _gsi.EventReceived += (ev, payload) => Dispatcher.UIThread.Post(() => HandleGsiEvent(ev));

            BuildNav();
            _pages["home"] = BuildHomePage();
            _pages["rgb"] = BuildRgbPage();
            _pages["music"] = BuildMusicPage();
            _pages["gamesense"] = BuildGameSensePage();
            _pages["gif"] = BuildGifPage();
            _pages["online"] = BuildOnlinePage();
            _pages["perkey"] = BuildPerKeyPage();
            _pages["kbbyv2"] = BuildKbByV2Page();
            _pages["store"] = BuildStorePage();
            _pages["widget"] = BuildWidgetPage();
            _pages["settings"] = BuildSettingsPage();
            _pages["phone"] = BuildPhonePage();
            foreach (var p in _pages.Values) { p.IsVisible = false; PageHost.Children.Add(p); }

            // v3.3: ap dung giao dien da luu (mau chu de + hinh nen)
            ApplyAccentByIndex(_ui.AccentIndex, save: false);
            ApplyBackground(_ui.Background, save: false);
            if (_ui.GameSense.Listening) StartGsi();

            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DropEvent, OnDropFiles);
            AddHandler(DragDrop.DragOverEvent, (s, e) => { e.DragEffects = DragDropEffects.Copy; });

            _macros.Load();
            _macros.StatusChanged += msg => Dispatcher.UIThread.Post(() =>
            {
                if (_macroStatus != null) _macroStatus.Text = msg;
                RefreshMacroList();
                ShowToast(msg);
            });
            _profiles.Load();
            _profiles.ForegroundChanged += exe => Dispatcher.UIThread.Post(() => OnForegroundAppChanged(exe));

            Opened += (s, e) =>
            {
                try
                {
                    var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                    if (handle != IntPtr.Zero)
                    {
                        _macros.RegisterHotkeys(handle);
                        HotkeyWindow.Attach(handle, id => _macros.OnHotkey(id));
                    }
                }
                catch { }
                _ = ScanDevicesAsync(auto: true);
                if (!_ui.SkipGreeting) ShowGreeting();
            };
            Closing += (s, e) =>
            {
                if (_trayEnabled && !_reallyExit)
                {
                    e.Cancel = true;
                    Hide();
                    ShowToast("App vẫn chạy ở khay hệ thống — bấm đúp biểu tượng khay để mở lại, hoặc chuột phải → Thoát.");
                    return;
                }
                Cleanup();
            };
            SetupTray();

            RefreshNavVisibility();
            RefreshWriteStates();
            ShowPage("home");
            AppendLog("T Keyboard Studio v3.4.0 đã mở. Dry-run ĐANG BẬT — mọi thao tác chỉ ghi log, không ghi xuống phím.");
            AppendLog("Lộ trình an toàn lần đầu: Quét thiết bị → thử RGB (dry-run) → tắt Dry-run → RGB thật → Cài đặt 04 17 → Đồng hồ → GIF TFT nhỏ → Đọc thử 0xF5.");
        }

        private void Cleanup()
        {
            try { _gsi.Stop(); } catch { }
            try { _flashOverlay?.Close(); } catch { }
            try { _phone.Stop(); } catch { }
            try { _audio.Stop(); } catch { }
            try { _musicSendCts?.Cancel(); } catch { }
            try { _profiles.Stop(); } catch { }
            try { _typing.Stop(); } catch { }
            try { _macros.Dispose(); } catch { }
            try { _exp.Dispose(); } catch { }
            try { _kb.Dispose(); } catch { }
            try { _device.Dispose(); } catch { }
        }

        // ================= Tien ich UI (giu nguyen tu ban Preview) =================
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

        // Nut ghi xuong thiet bi: tu khoa khi model dang chon khong ho tro ghi.
        private Button WriteBtn(string text, Action onClick, bool primary = true)
        {
            var b = primary ? PrimaryBtn(text, onClick) : GhostBtn(text, onClick);
            _writeButtons.Add((b, text));
            return b;
        }

        private void RefreshWriteStates()
        {
            bool can = _selectedModel.FullSupport;
            foreach (var (btn, _) in _writeButtons)
            {
                btn.IsEnabled = can;
                ToolTip.SetTip(btn, can ? null : "Model đang chọn chưa hỗ trợ ghi xuống phím — chỉ xem trước.");
            }
        }

        private Control Scroll(Control content)
            => new ScrollViewer
            {
                Content = new Border { Child = content, Padding = new Thickness(2, 2, 10, 2) },
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            };

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

        // ================= Dieu huong + Toast + Log =================
        private static PathShape NavIcon(string data, Color color) => new PathShape
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
                ("perkey", "Per-key (thử nghiệm)", "M4 4h4v4H4z M10 4h4v4h-4z M16 4h4v4h-4z M4 10h4v4H4z M10 10h4v4h-4z M16 10h4v4h-4z M4 16h4v4H4z M10 16h4v4h-4z M16 16h4v4h-4z"),
                ("kbbyv2", "Họ Sinowealth (kb_by_v2)", "M4 5h16M4 12h16M4 19h16 M7 5v14 M12 5v14 M17 5v14"),
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

        // An/hien trang theo nang luc cua model dang chon (quy tac cua Tien:
        // phim khong co man hinh thi khong hien trang GIF/Widget; per-key chi
        // danh cho nen tang F75 Max).
        private void RefreshNavVisibility()
        {
            foreach (var (key, btn, _) in _nav)
            {
                bool show = true;
                if (key == "gif" || key == "widget") show = _selectedModel.HasScreen;
                if (key == "perkey") show = _selectedModel.FullSupport;
                if (key == "kbbyv2") show = _selectedModel.Family == "kb_by_v2";
                btn.IsVisible = show;
            }
            if (!_pages.ContainsKey(_currentPage)) return;
            bool curHidden = (_currentPage == "gif" || _currentPage == "widget") && !_selectedModel.HasScreen
                             || _currentPage == "perkey" && !_selectedModel.FullSupport
                             || _currentPage == "kbbyv2" && _selectedModel.Family != "kb_by_v2";
            if (curHidden) ShowPage("home");
        }

        private void ShowPage(string key)
        {
            if (_currentPage == key && _pages.ContainsKey(key)) { OnPageShown(key); return; }
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
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.0) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0) } },
                },
            };
            anim.RunAsync(c);
        }

        private void OnPageShown(string key)
        {
            _widgetTimer.IsEnabled = key == "widget";
            _clockTimer.IsEnabled = key == "settings";
            if (key == "widget") RenderWidgetFrame();
            if (key == "settings") { UpdateClockLabels(); RefreshTypingLabels(); RefreshGreetingStyleText(); }
            if (key != "perkey") _ledPlayer?.Stop();
            if (key != "online") _onlineLedPlayer?.Stop();
            if (key != "music" && _musicPlaying) StopMusic();
        }

        public void ShowToast(string msg)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => ShowToast(msg));
                return;
            }
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

        private void AppendLog(string msg)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => AppendLog(msg));
                return;
            }
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            _logLines.Add(line);
            if (_logLines.Count > 500) _logLines.RemoveRange(0, _logLines.Count - 500);
            string text = string.Join("\n", _logLines);
            if (_logBox != null) { _logBox.Text = text; _logBox.CaretIndex = text.Length; }
            if (_expLogBox != null) { _expLogBox.Text = text; _expLogBox.CaretIndex = text.Length; }
        }

        // ================= Dry-run toan cuc =================
        private void SetDryRun(bool on, bool fromUi)
        {
            _dryRun = on;
            _device.DryRun = on;
            _exp.DryRun = on;
            _kb.DryRun = on;
            DryTitle.Text = on ? "Dry-run đang bật" : "ĐANG GHI THẬT XUỐNG PHÍM";
            DryTitle.Foreground = new SolidColorBrush(on ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0xF5, 0x9B, 0x9B));
            DryDesc.Text = on
                ? "Chế độ an toàn: thao tác chỉ ghi log, không ghi xuống phím thật."
                : "Cẩn thận: thao tác sẽ ghi thật xuống bàn phím qua USB-C.";
            if (DryToggle.IsChecked != on) DryToggle.IsChecked = on;
            if (_drySwitch != null && !_syncingDry)
            {
                _syncingDry = true;
                _drySwitch.IsChecked = on;
                _syncingDry = false;
            }
            if (_homeDryValue != null) _homeDryValue.Text = on ? "Dry-run đang bật" : "Ghi thật (Dry-run tắt)";
            AppendLog(on ? "Dry-run: BẬT (chỉ ghi log)." : "Dry-run: TẮT — các thao tác sẽ ghi THẬT xuống phím.");
        }

        private void SetExperimental(bool on)
        {
            _experimental = on;
            _exp.ExperimentalEnabled = on;
            _kb.ExperimentalEnabled = on;
            AppendLog(on
                ? "Lớp THỬ NGHIỆM per-key: BẬT. Lệnh 0x23/0x20/0x78 sẽ gửi THẬT khi Dry-run cũng tắt. Đây là giao thức tài liệu hoá trên AULA L99 (cùng nền tảng), CHƯA kiểm chứng trên F75 Max."
                : "Lớp THỬ NGHIỆM per-key: TẮT. Lệnh 0x23/0x20/0x78 chỉ ghi log gói sẽ gửi.");
            UpdateExpStatus();
        }

        // ================= Lop thiet bi: chay lenh verified =================
        private async Task RunStepsAsync(string title, Func<List<F75Protocol.Step>> build)
        {
            if (_busy) { ShowToast("Đang bận gửi lệnh trước — đợi một chút nhé."); return; }
            if (!_selectedModel.FullSupport)
            {
                ShowToast("Model đang chọn chưa hỗ trợ ghi xuống phím — chỉ xem trước.");
                return;
            }
            _busy = true;
            try
            {
                var steps = build();
                await Task.Run(() => _device.RunCommandSequence(title, steps));
                ShowToast(_dryRun ? $"[Dry-run] Đã ghi log lệnh: {title}" : $"Đã gửi xuống phím: {title}");
            }
            catch (Exception ex)
            {
                AppendLog("LỖI: " + ex.Message);
                ShowToast("Lỗi: " + ex.Message);
            }
            finally { _busy = false; }
        }

        private Task ApplyRgbAsync(int effect, Color c, bool multi, int brightUi, int speedUi, int direction)
            => RunStepsAsync("Áp dụng hiệu ứng RGB",
                () => F75Protocol.BuildLighting(effect, c.R, c.G, c.B, multi, brightUi + 1, speedUi + 1, direction));

        private async Task UploadStreamAsync(byte[] stream, string title, ProgressBar bar)
        {
            if (_busy) { ShowToast("Đang bận nạp dữ liệu trước — đợi một chút nhé."); return; }
            if (!_selectedModel.FullSupport)
            {
                ShowToast("Model đang chọn chưa hỗ trợ nạp màn hình — chỉ xem trước.");
                return;
            }
            _busy = true;
            try
            {
                if (bar != null) { bar.IsVisible = true; bar.Value = 0; }
                await Task.Run(() => _device.UploadDisplay(stream, 0, (done, total) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (bar != null) bar.Value = total > 0 ? 100.0 * done / total : 0;
                    });
                }));
                ShowToast(_dryRun ? $"[Dry-run] Đã ghi log nạp: {title}" : $"Đã nạp lên màn hình phím: {title}");
            }
            catch (Exception ex)
            {
                AppendLog("LỖI nạp màn hình: " + ex.Message);
                ShowToast("Lỗi nạp màn hình: " + ex.Message);
            }
            finally
            {
                _busy = false;
                if (bar != null) bar.IsVisible = false;
            }
        }

        private async Task UploadGifFileAsync(string path, FitMode fit, ProgressBar bar)
        {
            byte[] stream = null;
            string name = System.IO.Path.GetFileName(path);
            try
            {
                stream = await Task.Run(() => DisplayEncoder.Encode(path, fit, msg => AppendLog(msg)));
            }
            catch (Exception ex)
            {
                AppendLog("LỖI mã hoá GIF: " + ex.Message);
                ShowToast("Lỗi mã hoá GIF: " + ex.Message);
                return;
            }
            await UploadStreamAsync(stream, "GIF " + name, bar);
        }

        // GIF nhung trong exe -> trich ra file tam de dung chung pipeline file.
        private string ExtractEmbeddedGif(GifEntry entry)
        {
            string dir = System.IO.Path.Combine(GifLibraryStore.RootDir, "Temp");
            Directory.CreateDirectory(dir);
            string safe = string.Concat(entry.Name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')) + ".gif";
            string path = System.IO.Path.Combine(dir, safe);
            var bytes = GifTools.LoadBytes(entry.AssetUri);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private string ResolveGifPath(GifItem item) => item.IsFile ? item.FilePath : ExtractEmbeddedGif(item.Entry);

        // ================= TRANG 1: TONG QUAN =================
        private Control BuildHomePage()
        {
            _detectedPanel = new StackPanel { Spacing = 10 };
            _detectedPanel.Children.Add(Tb("Chưa quét — bấm “Quét bàn phím” để tìm thiết bị HID thật.", "sub"));

            var scanBtn = PrimaryBtn("Quét bàn phím", async () => await ScanDevicesAsync(auto: false));
            var connectHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            connectHeader.Children.Add(Tb("Bàn phím đang kết nối", "h2"));
            Grid.SetColumn(scanBtn, 1);
            connectHeader.Children.Add(scanBtn);
            var connectCard = Card(V(12, connectHeader, _detectedPanel));

            var homeKb = new KeyboardView { Mode = KeyboardMode.Animated, State = _rgb };
            var heroCard = Card(homeKb, 14);

            _homeFxValue = Tb("Sóng (Wave)", "h3");
            _homeGifValue = Tb("Tien Galaxy Spiral", "h3");
            _homeDryValue = Tb("Dry-run đang bật", "h3");
            var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 12 };
            var s1 = StatCard("Hiệu ứng RGB", _homeFxValue);
            var s2 = StatCard("GIF màn hình", _homeGifValue);
            var s3 = StatCard("Chế độ an toàn", _homeDryValue);
            stats.Children.Add(s1); Grid.SetColumn(s2, 1); stats.Children.Add(s2); Grid.SetColumn(s3, 2); stats.Children.Add(s3);

            var models = BuildModelGrid();

            _logBox = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                Height = 170,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                Text = string.Join("\n", _logLines),
            };
            var logCard = Card(V(8, Tb("Nhật ký thao tác", "h2"), _logBox));

            var root = V(14,
                Tb("Tổng quan", "h1"),
                Tb("T Keyboard Studio v3.4.0 — bản thật: quét thiết bị HID thật, ghi xuống F75 Max qua giao thức đã kiểm chứng; Driver Pack 1 thêm họ Sinowealth kb_by_v2 (định danh 0x82 trước khi ghi). Dry-run bật mặc định để an toàn.", "sub"),
                connectCard,
                heroCard,
                stats,
                Tb("Tất cả mẫu được hỗ trợ", "h2"),
                models,
                Tb("App chỉ tích hợp ghi thật cho mẫu đã kiểm chứng giao thức (hiện tại: AULA F75 Max). Các mẫu đã công khai giao thức (F75 thường, Hi75C Pro, Keychron VIA, Wooting, MADLIONS…) sẽ được tích hợp dần — chọn mẫu khác để xem trước giao diện/hiệu ứng, tính năng ghi sẽ khoá kèm giải thích.", "faint"),
                logCard);
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

        private async Task ScanDevicesAsync(bool auto)
        {
            List<DetectedDevice> found;
            List<HidInterfaceInfo> f75Ifaces;
            try
            {
                var result = await Task.Run(() =>
                {
                    var d = DeviceScanner.Scan();
                    var f = F75Device.Enumerate();
                    return (d, f);
                });
                found = result.d;
                f75Ifaces = result.f;
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi quét thiết bị: " + ex.Message);
                if (!auto) ShowToast("Lỗi quét thiết bị: " + ex.Message);
                return;
            }
            _detected = found;
            _detectedPanel.Children.Clear();
            var max = found.FirstOrDefault(d => d.Model.FullSupport);
            if (max != null)
            {
                string detail = $"{max.Connection} · VID {max.Vid:X4}:{max.Pid:X4} · {max.InterfaceCount} giao diện HID";
                if (f75Ifaces.Count > 0)
                    detail += " · " + string.Join(", ", f75Ifaces.Select(i => i.Role).Distinct());
                bool selected = _selectedModel.FullSupport;
                _detectedPanel.Children.Add(DeviceCard(
                    max.Model.Name, selected ? "Đang sử dụng" : "Đã phát hiện", Color.FromRgb(0x34, 0xD3, 0x99),
                    detail + " · Hỗ trợ đầy đủ tính năng (nạp GIF cần cáp USB-C)", null));
            }
            foreach (var d in found.Where(d => !d.Model.FullSupport))
            {
                var btn = GhostBtn("Xem trước mẫu này", () => SelectModel(d.Model));
                _detectedPanel.Children.Add(DeviceCard(
                    d.Model.Name, "Đã phát hiện", Color.FromRgb(0xC4, 0xB5, 0xFD),
                    $"{d.Connection} · VID {d.Vid:X4}:{d.Pid:X4} · {d.Model.ProtocolStatus}", btn));
            }
            if (found.Count == 0)
            {
                _detectedPanel.Children.Add(Tb("Không tìm thấy bàn phím nào trong danh sách hỗ trợ. Hãy cắm F75 Max bằng cáp USB-C rồi quét lại (Bluetooth/đầu thu 2.4G không nạp được màn hình và cài đặt).", "sub"));
            }
            else
            {
                // 258A:010C dung chung cho nhieu phim — canh bao trung lap neu co
                var shared = found.GroupBy(d => (d.Vid, d.Pid)).Where(g => g.Count() > 1).ToList();
                foreach (var g in shared)
                    _detectedPanel.Children.Add(Tb($"Lưu ý: VID {g.Key.Vid:X4}:{g.Key.Pid:X4} được dùng chung bởi {string.Join(" và ", g.Select(x => x.Model.Name))} — với họ kb_by_v2, app chỉ cho ghi sau khi định danh bằng lệnh 0x82 khớp registry (trang 'Họ Sinowealth'); các họ khác chờ driver riêng.", "faint"));
            }
            AppendLog($"Quét thiết bị: tìm thấy {found.Count} mẫu khớp ({string.Join(", ", found.Select(f => f.Model.Name))}).");
            if (!auto) ShowToast(found.Count > 0 ? $"Quét xong: {found.Count} thiết bị khớp." : "Không tìm thấy bàn phím hỗ trợ nào.");
            if (max != null && !_selectedModel.FullSupport) SelectModel(max.Model);
        }

        private void SelectModel(KeyboardModel model)
        {
            _selectedModel = model;
            RefreshNavVisibility();
            RefreshWriteStates();
            AppendLog($"Đã chọn mẫu: {model.Name} — {model.ProtocolStatus}.");
            ShowToast(model.FullSupport
                ? $"Đang dùng {model.Name} (hỗ trợ đầy đủ)."
                : model.Family == "kb_by_v2"
                    ? $"{model.Name}: thuộc họ Sinowealth (Driver Pack 1) — mở trang 'Họ Sinowealth (kb_by_v2)' và chạy Nhận diện (0x82) trước khi ghi."
                    : $"{model.Name}: chế độ XEM TRƯỚC — giao diện/hiệu ứng xem được, tính năng ghi xuống phím đang khoá vì driver cho mẫu này chưa tích hợp.");
            if ((_currentPage == "gif" || _currentPage == "widget") && !model.HasScreen) ShowPage("home");
            if (_currentPage == "perkey" && !model.FullSupport) ShowPage("home");
            if (_currentPage == "kbbyv2" && model.Family != "kb_by_v2") ShowPage("home");
        }

        private Control BuildModelGrid()
        {
            var wrap = new WrapPanel();
            foreach (var model in ModelDatabase.All)
            {
                var m = model;
                Color pillText, pillBg;
                if (m.StatusKind == 0) { pillText = Color.FromRgb(0x34, 0xD3, 0x99); pillBg = Color.FromArgb(0x26, 0x34, 0xD3, 0x99); }
                else if (m.StatusKind == 1) { pillText = Color.FromRgb(0xC4, 0xB5, 0xFD); pillBg = Color.FromArgb(0x2E, 0x7C, 0x3A, 0xED); }
                else { pillText = Color.FromRgb(0x8A, 0x87, 0xA0); pillBg = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF); }
                var pill = new Border
                {
                    Background = new SolidColorBrush(pillBg),
                    CornerRadius = new CornerRadius(999),
                    Padding = new Thickness(8, 3),
                    Child = new TextBlock { Text = m.ProtocolStatus, FontSize = 10, Foreground = new SolidColorBrush(pillText) },
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                string caps = (m.HasScreen ? "Có màn hình" : "Không màn hình") + (m.HasKnob ? " · núm xoay" : "");
                var cardContent = V(5, Tb(m.Name, "h3"), Tb(m.Layout + " · " + caps, "faint"), pill);
                var card = new Border
                {
                    Classes = { "pickcard" },
                    Child = cardContent,
                    Width = 218,
                    Margin = new Thickness(0, 0, 10, 10),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                if (m.FullSupport)
                {
                    card.BorderBrush = UiBrushes.AccentGradient();
                    card.BorderThickness = new Thickness(1.6);
                }
                card.PointerPressed += (s, e) => SelectModel(m);
                wrap.Children.Add(card);
            }
            return wrap;
        }

        // ================= TRANG HO SINOWEALTH kb_by_v2 (Driver Pack 1, v3.4) =================
        private Control BuildKbByV2Page()
        {
            var warn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xF5, 0x9B, 0x9B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9B, 0x9B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                Child = Tb("Chưa kiểm chứng trên phần cứng thật — giao thức kb_by_v2 được tài liệu hoá từ driver chính hãng (YUNZII hub + app tham chiếu họ A), nhưng không ai trong đội sở hữu các mẫu phím này. Mọi lệnh ghi đều khoá sau: định danh 0x82 khớp registry + công tắc Thử nghiệm (trang Per-key) + tắt Dry-run. Đường dongle 2.4G hiện chỉ ghi log gói, chưa mở transport thật.", null, 12),
            };

            _kbIdStatus = Tb("Chưa nhận diện. Hãy cắm phím họ Sinowealth (VID 258A) bằng cáp USB rồi bấm Nhận diện.", "sub");
            var idCard = Card(V(10,
                Tb("1 · Nhận diện thiết bị (bắt buộc trước khi ghi)", "h2"),
                Tb("VID:PID 258A:010C bị dùng chung bởi nhiều board khác nhau (AULA F75, Leobog Hi75, YUNZII C75/C98, GravaStar K1…). App đọc định danh từ chính thiết bị bằng lệnh 0x82 (chỉ đọc) và đối chiếu registry: khớp mới mở khoá ghi; không khớp hoặc không đọc được → chỉ xem. Leobog Hi75/Hi75C Pro chưa có UUID trong tài liệu đã trích nên luôn ở chế độ chỉ xem.", "sub"),
                PrimaryBtn("Nhận diện thiết bị (đọc 0x82 — chỉ đọc)", async () =>
                {
                    if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
                    _busy = true;
                    try
                    {
                        var (vid, pid, _) = _selectedModel.Ids.Count > 0 ? _selectedModel.Ids[0] : (0x258A, 0x010C, "");
                        var id = await Task.Run(() => _kb.Identify(vid, pid));
                        _kbIdStatus.Text = id.Known
                            ? $"Đã định danh: {id.ModelName} — {id.Method} · UUID {id.UuidHex} · 6 byte: {id.IdHex}. {id.Note}"
                            : $"Chưa định danh được. 6 byte đọc được: {id.IdHex}. {id.Note}";
                        ShowToast(id.Known ? $"Đã định danh: {id.ModelName}" : "Không khớp registry — chế độ chỉ xem.");
                    }
                    catch (Exception ex) { AppendLog("LỖI nhận diện kb_by_v2: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
                    finally { _busy = false; }
                }),
                _kbIdStatus));

            _kbInfoStatus = Tb("", "sub");
            var readCard = Card(V(10,
                Tb("2 · Đọc thông tin (chỉ đọc — chạy live)", "h2"),
                H(10,
                    GhostBtn("Đọc Basic Info (0x84)", async () => await KbReadAsync("Basic Info", KbByV2Protocol.BuildGetBasicInfo())),
                    GhostBtn("Đọc pin (0x87)", async () =>
                    {
                        var reply = await KbReadAsync("Pin", KbByV2Protocol.BuildGetBattery());
                        if (reply != null)
                        {
                            var (b0, b1) = KbByV2Protocol.ParseBattery(reply);
                            _kbInfoStatus.Text = $"Pin: 2 byte sau header = 0x{b0:X2} 0x{b1:X2} (ngữ nghĩa phần trăm/chế độ sạc cần kiểm chứng trên phím thật).";
                        }
                    }),
                    GhostBtn("Đọc đèn từng phím (0x86)", async () => await KbReadAsync("Custom light", KbByV2Protocol.BuildGetCustomLight()))),
                _kbInfoStatus));

            var writeCard = Card(V(10,
                Tb("3 · Ghi xuống phím (khoá an toàn 3 lớp)", "h2"),
                Tb("Chỉ gửi thật khi: đã định danh khớp registry + công tắc Thử nghiệm BẬT + Dry-run TẮT. Thiếu một điều kiện, app chỉ ghi log gói sẽ gửi (hoặc từ chối nếu chưa định danh). Sau lệnh ghi keymap, app chờ 100 ms theo tài liệu gốc.", "sub"),
                H(10,
                    PrimaryBtn("Ghi đèn mẫu cầu vồng (0x06)", async () =>
                    {
                        var slots = new List<(byte R, byte G, byte B)>();
                        for (int i = 0; i < 128; i++)
                        {
                            double h = i / 128.0;
                            var c = KeyboardView.Hsv(h, 0.85, 0.9);
                            slots.Add((c.R, c.G, c.B));
                        }
                        var planes = KbByV2Protocol.BuildCustomLightPlanes(slots);
                        await KbWriteAsync("Ghi đèn mẫu cầu vồng (0x06)", () => _kb.SendWrite("Ghi den mau kb_by_v2", KbByV2Protocol.BuildSetCustomLight(planes)));
                    }),
                    GhostBtn("Khôi phục cài đặt gốc (0x11)", async () =>
                        await KbWriteAsync("Khôi phục cài đặt gốc (0x11)", () => _kb.SendWrite("Factory reset kb_by_v2", KbByV2Protocol.BuildFactoryReset(), verifyEcho: false))),
                    GhostBtn("Xem gói 2.4G send_batch (chỉ log)", () =>
                    {
                        var packets = KbByV2Wireless.BuildBatch(1, 0, new byte[KbByV2Protocol.KeymapLength + 8]);
                        _kb.LogWirelessBatch("send_batch keymap lop 0 (dongle 2.4G)", packets);
                        ShowToast("Đã ghi log gói 2.4G — dongle chưa mở transport thật ở bản này.");
                    }))));

            var root = V(14,
                Tb("Họ Sinowealth (kb_by_v2)", "h1"),
                warn,
                idCard,
                readCard,
                writeCard);
            return Scroll(root);
        }

        private async Task<byte[]> KbReadAsync(string title, byte[] packet)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return null; }
            _busy = true;
            try
            {
                var reply = await Task.Run(() => _kb.SendRead(title, packet));
                if (reply != null && _kbInfoStatus != null)
                    _kbInfoStatus.Text = $"{title}: nhận {reply.Length} byte, header echo: {string.Join(" ", reply.Take(7).Select(b => b.ToString("X2")))}…";
                return reply;
            }
            catch (Exception ex) { AppendLog("LỖI đọc kb_by_v2: " + ex.Message); ShowToast("Lỗi: " + ex.Message); return null; }
            finally { _busy = false; }
        }

        private async Task KbWriteAsync(string title, Func<bool> send)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
            _busy = true;
            try
            {
                bool sent = await Task.Run(send);
                ShowToast(sent ? $"Đã gửi THẬT xuống phím: {title}" : $"[Chỉ log] {title} — chưa mở khoá đủ điều kiện ghi thật.");
            }
            catch (Exception ex) { AppendLog("LỖI ghi kb_by_v2: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
            finally { _busy = false; }
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

            var applyBtn = WriteBtn("Áp dụng xuống phím", async () =>
                await ApplyRgbAsync(_rgb.Effect, _rgb.Brush, _rgb.Multi, (int)_rgb.Brightness, (int)_rgb.Speed, _rgb.Direction));
            applyBtn.HorizontalAlignment = HorizontalAlignment.Left;

            var root = V(14,
                Tb("Ánh sáng RGB", "h1"),
                Tb("Chọn hiệu ứng để xem chạy trực tiếp trên bàn phím. “Áp dụng xuống phím” gửi khối 04 13 đã kiểm chứng xuống F75 Max (tôn trọng Dry-run). Với mẫu khác, trang này chỉ xem trước.", "sub"),
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

        // ================= TRANG 3: THEO NHAC (THAT) =================
        private TextBlock _musicChipText;
        private Button _musicPlayBtn;
        private readonly DispatcherTimer _musicTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(55) };

        private Control BuildMusicPage()
        {
            var kb = new KeyboardView { Mode = KeyboardMode.Animated, State = _music };
            _music.ExtBands = _audio.Bands01;
            _musicTimer.Tick += MusicTick;

            _musicChipText = Tb("Chưa phát — bấm Phát để bắt âm thanh hệ thống (WASAPI)", null, 12);
            var chip = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0x7C, 0x3A, 0xED)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xA7, 0x8B, 0xFA)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(12, 6),
                Child = _musicChipText,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            _musicPlayBtn = PrimaryBtn("Phát theo nhạc", () => { if (_musicPlaying) StopMusic(); else StartMusic(); });
            _musicPlayBtn.HorizontalAlignment = HorizontalAlignment.Left;

            var (modeSeg, _) = Segmented(
                new[] { "Visualizer stream 0x20", "Nhịp firmware 04 13", "Feed phổ 0x78" }, 0,
                i =>
                {
                    _musicMode = i;
                    _musicExpLogged = false;
                    if (_musicPlaying) RestartMusicSend();
                    ShowToast(i == 0 ? "Chế độ Visualizer: cột phím chạy trong app; gửi xuống phím qua stream 0x20 (lớp thử nghiệm)."
                        : i == 1 ? "Chế độ Nhịp firmware: bắt nhịp bass và pulse độ sáng bằng hiệu ứng firmware đã kiểm chứng (04 13) — chạy live theo Dry-run thường."
                        : "Chế độ Feed 0x78: gửi 23 dải phổ xuống phím (lớp thử nghiệm, dry-run/gated).");
                });

            var gain = new Slider { Minimum = 0.4, Maximum = 1.8, Value = 1.0, Width = 230 };
            gain.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) _music.MusicGain = gain.Value;
            };
            var (bgSeg, _) = Segmented(new[] { "Tắt", "Mờ", "Đầy đủ" }, 1, i => _music.MusicBg = i);

            var controlCard = Card(V(12,
                Tb("Điều khiển", "h2"),
                _musicPlayBtn,
                H(10, Tb("Chế độ gửi:", "sub"), modeSeg),
                V(4, Tb("Độ nhạy (biên độ cột sóng)", "sub"), gain),
                H(10, Tb("Chế độ nền:", "sub"), bgSeg)));

            var howCard = Card(V(8,
                Tb("Hoạt động thế nào?", "h2"),
                Tb("• Bắt toàn bộ âm thanh hệ thống qua WASAPI loopback (NAudio) — không cần micro; FFT ra 23 dải phổ 30Hz–16kHz.", "sub"),
                Tb("• Visualizer: cột sóng chạy ngay trong app. Gửi xuống phím thật dùng stream 0x20 — thuộc lớp THỬ NGHIỆM (tài liệu trên L99, chưa kiểm chứng trên Max) nên bị khoá sau công tắc thử nghiệm + Dry-run.", "sub"),
                Tb("• Nhịp firmware: pulse độ sáng bằng chính hiệu ứng 04 13 đã kiểm chứng — chế độ duy nhất của trang này chạy live theo Dry-run thường.", "sub"),
                Tb("• Feed 0x78: khối phổ nhạc của nền tảng (rhythm/nền/biên độ/23 mức) — cũng thuộc lớp thử nghiệm.", "sub"),
                Tb("• Nếu máy không bắt được âm thanh (không có thiết bị phát), app tự chuyển sang phổ mô phỏng để xem trước.", "faint")));

            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 14 };
            cols.Children.Add(controlCard);
            Grid.SetColumn(howCard, 1);
            cols.Children.Add(howCard);

            var root = V(14,
                Tb("Theo nhạc", "h1"),
                Tb("Bàn phím nhảy theo nhạc như một dàn đèn equalizer — bắt nhạc thật từ máy tính của bạn.", "sub"),
                chip,
                Card(kb, 14),
                cols);
            return Scroll(root);
        }

        private void StartMusic()
        {
            _audio.Start();
            _musicPlaying = true;
            _musicTimer.Start();
            if (_musicPlayBtn != null) _musicPlayBtn.Content = "Dừng";
            if (_musicChipText != null)
                _musicChipText.Text = _audio.Available
                    ? "Đang bắt âm thanh THẬT qua WASAPI loopback"
                    : "Không bắt được âm thanh thật (" + _audio.Error + ") — đang dùng phổ MÔ PHỎNG để xem trước";
            AppendLog(_audio.Available ? "Theo nhạc: đã bắt WASAPI loopback." : "Theo nhạc: WASAPI lỗi (" + _audio.Error + ") — chuyển phổ mô phỏng.");
            RestartMusicSend();
        }

        private void StopMusic()
        {
            _musicPlaying = false;
            _musicTimer.Stop();
            _musicSendCts?.Cancel();
            _musicSendCts = null;
            _audio.Stop();
            if (_musicPlayBtn != null) _musicPlayBtn.Content = "Phát theo nhạc";
            if (_musicChipText != null) _musicChipText.Text = "Đã dừng.";
        }

        private void RestartMusicSend()
        {
            _musicSendCts?.Cancel();
            _musicExpLogged = false;
            if (!_musicPlaying || _musicMode == 1) return; // che do nhip firmware khong dung vong gui rieng
            _musicSendCts = new CancellationTokenSource();
            var ct = _musicSendCts.Token;
            int mode = _musicMode;
            _ = Task.Run(async () =>
            {
                bool sendBusy = false;
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        if (!sendBusy)
                        {
                            List<F75Protocol.Step> steps = null;
                            if (mode == 0) steps = L99Protocol.BuildStreamFrameSteps(BandsToSlots());
                            else if (mode == 2) steps = L99Protocol.BuildAudioFrameSteps(_audio.Levels100);
                            if (steps != null)
                            {
                                if (_exp.WritesArmed)
                                {
                                    sendBusy = true;
                                    _ = Task.Run(() =>
                                    {
                                        try { _exp.SendSteps(mode == 0 ? "Stream nhac 0x20" : "Feed pho 0x78", steps); }
                                        catch (Exception ex) { AppendLog("Lỗi gửi nhạc: " + ex.Message); }
                                        sendBusy = false;
                                    });
                                }
                                else if (!_musicExpLogged)
                                {
                                    _musicExpLogged = true;
                                    _exp.SendSteps(mode == 0 ? "Stream nhac 0x20 (frame mẫu)" : "Feed pho 0x78 (frame mẫu)", steps);
                                    AppendLog("Theo nhạc: các frame tiếp theo không ghi log nữa (tránh spam) — bật công tắc Thử nghiệm + tắt Dry-run để gửi thật.");
                                }
                            }
                        }
                        await Task.Delay(mode == 0 ? 59 : 47, ct);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { AppendLog("Lỗi vòng gửi nhạc: " + ex.Message); break; }
                }
            }, ct);
        }

        // Pho -> o stream: cot theo CX, chieu cao cot theo muc pho tai cot do.
        private List<L99Protocol.StreamQuad> BandsToSlots()
        {
            var bands = _audio.Available ? _audio.Bands01 : SyntheticBands();
            var slots = new List<L99Protocol.StreamQuad>(MaxKeyData.StreamSlots.Length);
            foreach (var slot in MaxKeyData.StreamSlots)
            {
                int bi = (int)(slot.CX * (bands.Length - 1));
                double level = Math.Min(1.0, bands[bi] * _music.MusicGain);
                double fromBottom = 1.0 - slot.CY;
                byte r = 0, g = 0, b = 0;
                if (fromBottom < level * 1.02)
                {
                    var c = KeyboardView.Hsv(0.54 - 0.54 * fromBottom, 0.85, 1.0);
                    double s = 0.55 + 0.45 * (1.0 - Math.Min(1.0, level - fromBottom));
                    r = (byte)(c.R * s); g = (byte)(c.G * s); b = (byte)(c.B * s);
                }
                else if (_music.MusicBg > 0)
                {
                    var c = KeyboardView.Hsv(0.54 - 0.54 * fromBottom, 0.85, 1.0);
                    double s = _music.MusicBg == 2 ? 0.30 : 0.13;
                    r = (byte)(c.R * s); g = (byte)(c.G * s); b = (byte)(c.B * s);
                }
                slots.Add(new L99Protocol.StreamQuad(slot.Id, r, g, b));
            }
            return slots;
        }

        private float[] _synthBands;
        private float[] SyntheticBands()
        {
            if (_synthBands == null) _synthBands = new float[L99Protocol.AudioBandCount];
            double t = DateTime.Now.TimeOfDay.TotalSeconds;
            for (int b = 0; b < _synthBands.Length; b++)
            {
                double v = 0.45 + 0.35 * Math.Sin(t * 1.7 + b * 0.6) + 0.25 * Math.Sin(t * 3.1 + b * 1.7);
                v *= 1.0 - b / (double)_synthBands.Length * 0.55; // bass manh hon treble
                _synthBands[b] = (float)Math.Max(0.03, Math.Min(1.0, v));
            }
            return _synthBands;
        }

        private void MusicTick(object sender, EventArgs e)
        {
            if (!_musicPlaying) return;
            if (!_audio.Available) _music.ExtBands = SyntheticBands();
            // Che do nhip firmware: pulse do sang theo beat qua 04 13 verified.
            if (_musicMode == 1)
            {
                bool beat = _audio.Available
                    ? _audio.Beat
                    : SyntheticBands()[0] + SyntheticBands()[1] > 1.55;
                long now = Environment.TickCount64;
                if (beat && !_lastBeat && now - _lastPulseTick > 280)
                {
                    _lastPulseTick = now;
                    int fx = _rgb.Effect == 0 ? 10 : _rgb.Effect;
                    if (_dryRun)
                    {
                        AppendLog($"[NHỊP] Beat! (dry-run — bản live sẽ pulse 04 13 hiệu ứng {fx}, độ sáng 5→2)");
                    }
                    else
                    {
                        _ = PulseBeatAsync(fx);
                    }
                }
                _lastBeat = beat;
            }
        }

        private async Task PulseBeatAsync(int fx)
        {
            try
            {
                await RunStepsAsync("Nhịp: pulse sáng", () => F75Protocol.BuildLighting(fx, _rgb.Brush.R, _rgb.Brush.G, _rgb.Brush.B, _rgb.Multi, 5, (int)_rgb.Speed + 1, _rgb.Direction));
                await Task.Delay(130);
                await RunStepsAsync("Nhịp: hạ sáng", () => F75Protocol.BuildLighting(fx, _rgb.Brush.R, _rgb.Brush.G, _rgb.Brush.B, _rgb.Multi, 2, (int)_rgb.Speed + 1, _rgb.Direction));
            }
            catch { }
        }

        // ================= TRANG 4: MAN HINH GIF =================
        private sealed class GifItem
        {
            public GifEntry Entry;
            public bool IsFile;
            public string FilePath;
            public string GroupKey;   // NganHa/PixelGame/ThuGian/Mine/Online
            public bool Deletable;    // chi file trong MyGIFs
        }

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
            _gifInfo = Tb("Chọn một GIF trong thư viện bên dưới.", "sub");
            _gifInfo.TextAlignment = TextAlignment.Center;

            List<Bitmap> screenFrames = null;
            async Task ApplySelection(GifItem item)
            {
                _selectedGif = item;
                if (_homeGifValue != null) _homeGifValue.Text = item.Entry.Name;
                string path = ResolveGifPath(item);
                var info = await Task.Run(() => DisplayEncoder.Probe(path));
                _gifInfo.Text = $"{item.Entry.Name} · {info.SourceWidth}×{info.SourceHeight} · {info.SourceFrames} frame gốc" +
                                (info.Capped ? $" → cắt còn {info.FrameCount}" : $" → {info.FrameCount} frame") +
                                $" · chuẩn hoá 128×128 ({_gifFit}) · {info.ChunkCount} chunk";
                var frames = await Task.Run(() => item.IsFile
                    ? GifTools.LoadFramesFile(path, 128, _gifFit, 48)
                    : GifTools.LoadFrames(item.Entry.AssetUri, 128, _gifFit, 48));
                var old = screenFrames;
                screenFrames = frames;
                kb.ScreenFrames = frames;
                GifTools.DisposeFrames(old);
            }
            _applyGifSelection = ApplySelection;

            _gifWrap = new WrapPanel();
            RefreshGifGrid();

            var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var filters = new (string Label, string Group)[]
            {
                ("Tất cả", null), ("Ngân hà", "NganHa"), ("Pixel & Game", "PixelGame"),
                ("Thư giãn", "ThuGian"), ("Của tôi", "Mine"), ("Thư viện Online", "Online"),
            };
            var filterBtns = new List<Button>();
            foreach (var (label, group) in filters)
            {
                var b = new Button { Content = label };
                b.Classes.Add("chip");
                if (group == null) b.Classes.Add("sel");
                string g = group;
                b.Click += (s, e) =>
                {
                    foreach (var o in filterBtns) o.Classes.Remove("sel");
                    b.Classes.Add("sel");
                    _gifFilter = g;
                    ApplyGifFilter();
                };
                filterBtns.Add(b);
                filterRow.Children.Add(b);
            }

            var (fitSeg, _) = Segmented(new[] { "Fill", "Fit", "Stretch" }, 0, i =>
            {
                _gifFit = i == 0 ? GifFitMode.Fill : i == 1 ? GifFitMode.Fit : GifFitMode.Stretch;
                if (_selectedGif != null) _ = ApplySelection(_selectedGif);
            });
            _gifProgress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Width = 180, IsVisible = false };
            var uploadBtn = WriteBtn("Nạp lên phím", async () =>
            {
                if (_selectedGif == null) { ShowToast("Hãy chọn một GIF trước nhé."); return; }
                var fit = _gifFit == GifFitMode.Fill ? FitMode.Fill : _gifFit == GifFitMode.Fit ? FitMode.Fit : FitMode.Stretch;
                await UploadGifFileAsync(ResolveGifPath(_selectedGif), fit, _gifProgress);
            });
            var actionRow = H(12, fitSeg, uploadBtn, _gifProgress);
            actionRow.Children[0].VerticalAlignment = VerticalAlignment.Center;

            // Quan ly thu vien (ke thua v3.1)
            var urlBox = new TextBox { Watermark = "Dán link ảnh/GIF (https://…) rồi bấm Tải về", Width = 420 };
            urlBox.Classes.Add("search");
            var libRow = H(10,
                GhostBtn("Thêm thư mục nguồn", async () => await AddGifFolderAsync()),
                urlBox,
                GhostBtn("Tải về", async () => await DownloadGifFromUrlAsync(urlBox)),
                GhostBtn("Mở thư mục Của tôi", () =>
                {
                    try
                    {
                        GifLibraryStore.EnsureDirs();
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", GifLibraryStore.MyGifsDir) { UseShellExecute = true });
                    }
                    catch (Exception ex) { ShowToast("Không mở được thư mục: " + ex.Message); }
                }),
                GhostBtn("Xoá GIF đang chọn", () => DeleteSelectedGif()));
            var topGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,300"), ColumnSpacing = 14 };
            topGrid.Children.Add(Card(kb, 14));
            var previewCard = Card(V(10, Tb("Xem trước lớn", "h3"), bigImg, _gifInfo));
            Grid.SetColumn(previewCard, 1);
            topGrid.Children.Add(previewCard);

            var root = V(14,
                Tb("Màn hình GIF", "h1"),
                Tb("GIF phát ngay trong ô màn hình TFT trên bàn phím và ở khung xem trước lớn. Mọi GIF đều được app tự chuẩn hoá về 128×128, tối đa 255 frame, rồi nạp qua kênh màn hình đã kiểm chứng (cần cáp USB-C). Kéo-thả file/thư mục GIF vào cửa sổ app để thêm vào thư viện.", "sub"),
                topGrid,
                Tb("Thư viện GIF", "h2"),
                filterRow,
                _gifWrap,
                actionRow,
                Card(V(10, Tb("Thêm GIF của bạn", "h3"), libRow,
                    Tb("GIF tự thêm được chuẩn hoá về 128×128 và lưu vào “Của tôi” (%AppData%\\TienKeyboardStudio\\MyGIFs). Thư mục nguồn được nhớ trong settings.json. Nguồn ngoài gợi ý: GIPHY Pixel Art · Pixabay GIFs · GitHub RecalBoxDMD (~600 GIF retro).", "faint"))));
            return Scroll(root);
        }

        private Func<GifItem, Task> _applyGifSelection;

        private List<GifItem> CollectGifItems()
        {
            var items = new List<GifItem>();
            foreach (var e in GifCatalog.Library())
                items.Add(new GifItem { Entry = e, IsFile = false, GroupKey = e.Group });
            // Thu muc Library canh exe (ban dong goi kem theo)
            string baseLib = System.IO.Path.Combine(AppContext.BaseDirectory, "Library");
            foreach (var (group, label, _) in GifCatalog.LibraryGroups)
            {
                string dir = System.IO.Path.Combine(baseLib, group);
                foreach (var f in GifLibraryStore.ScanFolder(dir))
                {
                    // tranh trung voi ban nhung: ten file giong nhau thi bo qua
                    string name = GifCatalog.PrettyName(f);
                    if (items.Any(i => !i.IsFile && i.Entry.Name == name)) continue;
                    items.Add(new GifItem
                    {
                        Entry = new GifEntry { Name = name, Group = group, GroupLabel = label },
                        IsFile = true, FilePath = f, GroupKey = group,
                    });
                }
            }
            GifLibraryStore.EnsureDirs();
            foreach (var f in GifLibraryStore.ScanFolder(GifLibraryStore.MyGifsDir))
                items.Add(new GifItem
                {
                    Entry = new GifEntry { Name = GifCatalog.PrettyName(f), Group = "Mine", GroupLabel = "Của tôi" },
                    IsFile = true, FilePath = f, GroupKey = "Mine", Deletable = true,
                });
            var settings = GifLibraryStore.Load();
            foreach (var folder in settings.Folders)
                foreach (var f in GifLibraryStore.ScanFolder(folder))
                    items.Add(new GifItem
                    {
                        Entry = new GifEntry { Name = GifCatalog.PrettyName(f), Group = "Mine", GroupLabel = "Của tôi" },
                        IsFile = true, FilePath = f, GroupKey = "Mine",
                    });
            // Thu muc OnlineLibrary canh exe (16 hieu ung dong goi san, v3.2.2)
            if (Directory.Exists(OnlineCatalog.OnlineLibraryDir))
                foreach (var f in GifLibraryStore.ScanFolder(OnlineCatalog.OnlineLibraryDir))
                    items.Add(new GifItem
                    {
                        Entry = new GifEntry { Name = "Online · " + GifCatalog.PrettyName(f), Group = "Online", GroupLabel = "Thư viện Online" },
                        IsFile = true, FilePath = f, GroupKey = "Online",
                    });
            return items;
        }

        private void RefreshGifGrid()
        {
            if (_gifWrap == null) return;
            _gifWrap.Children.Clear();
            _gifCards.Clear();
            var items = CollectGifItems();
            foreach (var item in items)
            {
                var it = item;
                var card = new GifCard(it.Entry)
                {
                    Margin = new Thickness(0, 0, 10, 10),
                    FramesLoader = e => Task.Run(() => it.IsFile
                        ? GifTools.LoadFramesFile(it.FilePath, 96, GifFitMode.Fit, 24)
                        : GifTools.LoadFrames(e.AssetUri, 96, GifFitMode.Fit, 24)),
                };
                card.Clicked += c =>
                {
                    foreach (var o in _gifCards) o.Selected = ReferenceEquals(o, c);
                    _ = _applyGifSelection?.Invoke(it);
                };
                _gifCards.Add(card);
                _gifWrap.Children.Add(card);
            }
            ApplyGifFilter();
            _ = Task.Run(async () =>
            {
                foreach (var c in _gifCards)
                {
                    try
                    {
                        var item2 = items[_gifCards.IndexOf(c)];
                        var bmp = item2.IsFile
                            ? await Task.Run(() => GifTools.LoadFrameFile(item2.FilePath, 96))
                            : await Task.Run(() => GifTools.LoadFrame(c.Entry.AssetUri, 96));
                        await Dispatcher.UIThread.InvokeAsync(() => c.SetFirstFrame(bmp));
                    }
                    catch { }
                }
            });
            // Chon mac dinh GIF dau tien cua thu vien nhung neu chua chon
            if (_selectedGif == null && items.Count > 0)
            {
                var def = items.FirstOrDefault(i => i.Entry.Name.Contains("Galaxy Spiral")) ?? items[0];
                _ = _applyGifSelection?.Invoke(def);
            }
        }

        private void ApplyGifFilter()
        {
            foreach (var c in _gifCards)
            {
                string g = c.Entry.Group;
                c.IsVisible = _gifFilter == null || g == _gifFilter;
            }
        }

        private async Task AddGifFolderAsync()
        {
            try
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Chọn thư mục chứa GIF/ảnh",
                    AllowMultiple = false,
                });
                if (folders.Count == 0) return;
                string path = folders[0].Path.LocalPath;
                var settings = GifLibraryStore.Load();
                if (!settings.Folders.Contains(path))
                {
                    settings.Folders.Add(path);
                    GifLibraryStore.Save(settings);
                }
                RefreshGifGrid();
                ShowToast("Đã thêm thư mục nguồn: " + path);
            }
            catch (Exception ex) { ShowToast("Không thêm được thư mục: " + ex.Message); }
        }

        private async Task DownloadGifFromUrlAsync(TextBox urlBox)
        {
            string url = (urlBox.Text ?? "").Trim();
            if (url.Length == 0) { ShowToast("Hãy dán link ảnh/GIF trước nhé."); return; }
            try
            {
                ShowToast("Đang tải ảnh từ link…");
                string tmp = await GifLibraryStore.DownloadImageAsync(url);
                var fit = _gifFit == GifFitMode.Fill ? FitMode.Fill : _gifFit == GifFitMode.Fit ? FitMode.Fit : FitMode.Stretch;
                var (outPath, before, after) = await Task.Run(() => GifLibraryStore.NormalizeToMyGifs(tmp, fit, null));
                RefreshGifGrid();
                ShowToast($"Đã thêm vào Của tôi: {before} → {after}");
                AppendLog($"Tải GIF từ URL: {url} → {outPath} ({before} → {after})");
                urlBox.Text = "";
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi tải GIF từ URL: " + ex.Message);
                ShowToast("Tải không thành công: " + ex.Message);
            }
        }

        private void DeleteSelectedGif()
        {
            if (_selectedGif == null || !_selectedGif.Deletable)
            {
                ShowToast("Chỉ xoá được GIF nằm trong thư mục Của tôi (MyGIFs).");
                return;
            }
            try
            {
                File.Delete(_selectedGif.FilePath);
                AppendLog("Đã xoá GIF: " + _selectedGif.FilePath);
                _selectedGif = null;
                RefreshGifGrid();
                ShowToast("Đã xoá GIF khỏi Của tôi.");
            }
            catch (Exception ex) { ShowToast("Không xoá được: " + ex.Message); }
        }

        private async void OnDropFiles(object sender, DragEventArgs e)
        {
            try
            {
                var files = e.Data.GetFiles();
                if (files == null) return;
                int added = 0;
                foreach (var f in files)
                {
                    string path = f.Path.LocalPath;
                    if (Directory.Exists(path))
                    {
                        var settings = GifLibraryStore.Load();
                        if (!settings.Folders.Contains(path)) { settings.Folders.Add(path); GifLibraryStore.Save(settings); }
                        added++;
                    }
                    else if (File.Exists(path))
                    {
                        var fit = _gifFit == GifFitMode.Fill ? FitMode.Fill : _gifFit == GifFitMode.Fit ? FitMode.Fit : FitMode.Stretch;
                        string src = path;
                        await Task.Run(() => GifLibraryStore.NormalizeToMyGifs(src, fit, null));
                        added++;
                    }
                }
                if (added > 0)
                {
                    RefreshGifGrid();
                    ShowToast($"Đã thêm {added} mục vào thư viện GIF.");
                    if (_currentPage != "gif" && _selectedModel.HasScreen) ShowPage("gif");
                }
            }
            catch (Exception ex) { ShowToast("Kéo-thả lỗi: " + ex.Message); }
        }

        // ================= GIF -> LED: bo phat dung chung =================
        private sealed class LedPlayer
        {
            public GifLedAnimation Anim;
            public int FrameIndex;
            public readonly DispatcherTimer Timer = new DispatcherTimer();
            public RgbAnimState State;
            public KeyboardView Target;
            public ExperimentalDevice Exp;
            public Action<string> Log;
            public bool Sending;
            private bool _sendBusy;
            private bool _loggedOnce;

            public LedPlayer() { Timer.Tick += Tick; }

            public void Load(GifLedAnimation anim, RgbAnimState state, KeyboardView target)
            {
                Stop();
                Anim = anim; State = state; Target = target;
                FrameIndex = 0;
                ApplyFrame();
            }

            public void Play()
            {
                if (Anim == null || Anim.Frames.Count == 0) return;
                Timer.Interval = TimeSpan.FromMilliseconds(IntervalMs());
                Timer.Start();
            }

            public void Stop() { Timer.Stop(); Sending = false; }

            public void SetSending(bool on)
            {
                Sending = on;
                _loggedOnce = false;
                if (on && Anim != null)
                {
                    if (!Timer.IsEnabled) Play();
                    MaybeSend();
                }
            }

            private int IntervalMs()
            {
                if (Anim == null || Anim.DelaysMs.Count == 0) return 59;
                int d = Anim.DelaysMs[Math.Min(FrameIndex, Anim.DelaysMs.Count - 1)];
                return Sending ? Math.Max(59, Math.Min(500, d)) : Math.Max(30, Math.Min(500, d));
            }

            private void Tick(object sender, EventArgs e)
            {
                if (Anim == null || Anim.Frames.Count == 0) { Stop(); return; }
                FrameIndex = (FrameIndex + 1) % Anim.Frames.Count;
                ApplyFrame();
                Timer.Interval = TimeSpan.FromMilliseconds(IntervalMs());
                MaybeSend();
            }

            private void ApplyFrame()
            {
                if (Anim == null || State == null) return;
                var vf = Anim.ViewFrames[FrameIndex];
                var dict = new Dictionary<string, Color>();
                foreach (var kv in vf)
                    dict["k" + kv.Key] = Color.FromRgb(kv.Value.R, kv.Value.G, kv.Value.B);
                State.FrameColors = dict;
                State.PaintMode = false;
                Target?.InvalidateVisual();
            }

            private void MaybeSend()
            {
                if (!Sending || Anim == null || Exp == null) return;
                if (Exp.WritesArmed)
                {
                    if (_sendBusy) return;
                    _sendBusy = true;
                    var steps = L99Protocol.BuildStreamFrameSteps(Anim.Frames[FrameIndex].ToList());
                    Task.Run(() =>
                    {
                        try { Exp.SendSteps("Stream GIF→LED 0x20", steps); }
                        catch (Exception ex) { Log?.Invoke("Lỗi stream GIF→LED: " + ex.Message); }
                        _sendBusy = false;
                    });
                }
                else if (!_loggedOnce)
                {
                    _loggedOnce = true;
                    var steps = L99Protocol.BuildStreamFrameSteps(Anim.Frames[0].ToList());
                    Exp.SendSteps("Stream GIF→LED 0x20 (frame mẫu)", steps);
                    Log?.Invoke($"GIF→LED “{Anim.Name}”: {Anim.Frames.Count} frame ({Anim.SourceWidth}×{Anim.SourceHeight}). Lớp thử nghiệm chưa mở khoá gửi thật nên chỉ ghi log frame mẫu; các frame sau phát trong app.");
                }
            }
        }

        private readonly LedPlayer _ledPlayer = new LedPlayer();
        private readonly LedPlayer _onlineLedPlayer = new LedPlayer();

        // ================= TRANG 5: THU VIEN ONLINE =================
        private Control BuildOnlinePage()
        {
            _onlineLedPlayer.Exp = _exp;
            _onlineLedPlayer.Log = AppendLog;
            var ledState = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3 };
            var ledKb = new KeyboardView { Mode = KeyboardMode.Animated, State = ledState };
            var ledStatus = Tb("Chọn một hiệu ứng bên dưới — GIF đã đóng gói sẵn trong máy, app chuyển ngay thành đèn từng phím (không cần mạng).", "sub");
            OnlineGifItem selectedItem = null;
            string selectedPath = null;

            var wrap = new WrapPanel();
            var cards = new List<OnlineGifCard>();
            List<OnlineGifItem> catalog;
            try { catalog = OnlineCatalog.LoadCatalog(); }
            catch (Exception ex) { catalog = new List<OnlineGifItem>(); AppendLog("Lỗi đọc catalog online: " + ex.Message); }

            async Task SelectOnlineAsync(OnlineGifCard card)
            {
                foreach (var o in cards) o.SetSelected(ReferenceEquals(o, card));
                selectedItem = card.Item;
                try
                {
                    string path = OnlineCatalog.ResolvePath(card.Item);
                    if (path == null)
                    {
                        ledStatus.Text = $"Không thấy file của “{card.Item.Name}” trong thư mục OnlineLibrary cạnh exe — hãy giải nén đủ cả thư mục từ file zip nhé.";
                        AppendLog($"Online: thiếu file local cho “{card.Item.Name}”.");
                        return;
                    }
                    selectedPath = path;
                    ledStatus.Text = $"Đang chuyển “{card.Item.Name}” thành đèn phím…";
                    var anim = await Task.Run(() => GifLedConverter.Convert(path));
                    _onlineLedPlayer.Load(anim, ledState, ledKb);
                    ledStatus.Text = $"“{card.Item.Name}”: GIF gốc {anim.SourceWidth}×{anim.SourceHeight}, {anim.SourceFrames} frame → đã chuyển thành {anim.Frames.Count} frame đèn phím. Bấm Phát để xem trong app.";
                    AppendLog($"Online: đã chuyển LED “{card.Item.Name}” từ file local ({anim.Frames.Count} frame).");
                }
                catch (Exception ex)
                {
                    ledStatus.Text = "Chuyển thất bại: " + ex.Message;
                    AppendLog("Lỗi online: " + ex.Message);
                }
            }

            foreach (var item in catalog)
            {
                var card = new OnlineGifCard(item, OnlineCatalog.ResolvePath(item) != null) { Margin = new Thickness(0, 0, 10, 10) };
                card.SelectedChanged += c => _ = SelectOnlineAsync(c);
                card.UseClicked += c => _ = SelectOnlineAsync(c);
                cards.Add(card);
                wrap.Children.Add(card);
                var captured = card;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var bmp = await OnlineCatalog.LoadThumbnailAsync(captured.Item);
                        if (bmp != null)
                            await Dispatcher.UIThread.InvokeAsync(() => captured.SetThumb(bmp));
                    }
                    catch { }
                });
            }

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
                    bool show = q.Length == 0 || c.Item.Name.ToLowerInvariant().Contains(q) || (c.Item.Source ?? "").ToLowerInvariant().Contains(q);
                    c.IsVisible = show;
                    if (show) visible++;
                }
                emptyNote.IsVisible = visible == 0;
            }
            search.TextChanged += (s, e) => ApplyFilter();

            var playBtn = GhostBtn("Phát / Dừng", () =>
            {
                if (_onlineLedPlayer.Anim == null) { ShowToast("Hãy chọn một hiệu ứng trước nhé."); return; }
                if (_onlineLedPlayer.Timer.IsEnabled) _onlineLedPlayer.Stop();
                else _onlineLedPlayer.Play();
            });
            var sendBtn = GhostBtn("Chạy lên đèn phím (thử nghiệm)", () =>
            {
                if (_onlineLedPlayer.Anim == null) { ShowToast("Hãy chọn một hiệu ứng trước nhé."); return; }
                bool on = !_onlineLedPlayer.Sending;
                _onlineLedPlayer.SetSending(on);
                ShowToast(on
                    ? (_exp.WritesArmed ? "Đang stream THẬT lên đèn phím qua 0x20 (~17fps)." : "Đang phát trong app + ghi log gói stream (lớp thử nghiệm chưa mở khoá gửi thật).")
                    : "Đã ngừng chạy lên đèn phím.");
            });
            var tftBtn = WriteBtn("Nạp GIF này lên màn hình TFT", async () =>
            {
                if (selectedPath == null) { ShowToast("Hãy chọn một hiệu ứng trước nhé."); return; }
                await UploadGifFileAsync(selectedPath, FitMode.Fill, null);
            }, primary: false);

            var ledCard = Card(V(10,
                Tb("Phát lên đèn phím — GIF→LED", "h2"),
                ledKb,
                ledStatus,
                H(10, playBtn, sendBtn, tftBtn)));

            var root = V(14,
                Tb("Thư viện Online", "h1"),
                Tb("16 hiệu ứng cộng đồng ĐÓNG GÓI SẴN trong thư mục OnlineLibrary cạnh exe — dùng hoàn toàn offline, không tải gì từ mạng. GIF gốc là hiệu ứng LED toàn phím dạng dải rộng; app lấy mẫu màu tại tâm từng phím để phát lên đèn phím, hoặc cắt về 128×128 để nạp màn hình TFT.", "sub"),
                search,
                wrap,
                emptyNote,
                ledCard,
                CardFlat(Tb("Nguồn ngoài khác để tự tìm GIF: GIPHY Pixel Art · Pixabay GIFs · GitHub RecalBoxDMD (~600 GIF retro) — tải về rồi kéo-thả vào app để thêm vào thư viện. Tôn trọng bản quyền của tác giả GIF khi dùng lại.", "faint")));
            return Scroll(root);
        }

        private sealed class OnlineGifCard : Border
        {
            private readonly Avalonia.Controls.Image _img;
            private readonly TextBlock _status;
            private readonly Button _useBtn;
            private bool _selected;

            public OnlineGifItem Item { get; }
            public event Action<OnlineGifCard> SelectedChanged;
            public event Action<OnlineGifCard> UseClicked;

            public OnlineGifCard(OnlineGifItem item, bool available)
            {
                Item = item;
                Classes.Add("pickcard");
                Cursor = new Cursor(StandardCursorType.Hand);
                Width = 196;
                _img = new Avalonia.Controls.Image
                {
                    Width = 168, Height = 84, Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                _status = new TextBlock
                {
                    Text = available ? "Có sẵn trong máy · dùng ngay offline" : "Thiếu file (kiểm tra thư mục OnlineLibrary)",
                    FontSize = 10, Foreground = UiBrushes.TextSecondary,
                };
                _useBtn = new Button { Content = "Dùng hiệu ứng này", HorizontalAlignment = HorizontalAlignment.Stretch };
                _useBtn.Classes.Add("ghost");
                _useBtn.Click += (s, e) => UseClicked?.Invoke(this);
                var stack = new StackPanel { Spacing = 6 };
                stack.Children.Add(_img);
                stack.Children.Add(new TextBlock { Text = item.Name, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
                stack.Children.Add(_status);
                stack.Children.Add(_useBtn);
                Child = stack;
                PointerPressed += (s, e) => SelectedChanged?.Invoke(this);
            }

            public void SetThumb(Bitmap bmp) { if (bmp != null) _img.Source = bmp; }

            public void SetSelected(bool sel)
            {
                _selected = sel;
                BorderBrush = sel ? UiBrushes.AccentGradient() : UiBrushes.GlassBorder;
                BorderThickness = new Thickness(sel ? 1.8 : 1);
            }
        }

        // ================= TRANG 6: PER-KEY (THU NGHIEM) =================
        private readonly Dictionary<string, Color> _painted = new Dictionary<string, Color>();
        private Color _paintColor = Color.FromRgb(0x7C, 0x3A, 0xED);
        private bool _eraseMode;
        private TextBlock _expStatus;
        private TextBlock _paintInfo;
        private PillSwitch _expSwitch;

        private void UpdateExpStatus()
        {
            if (_expStatus == null) return;
            if (_exp.WritesArmed)
                _expStatus.Text = "TRẠNG THÁI: ĐÃ MỞ KHOÁ GỬI THẬT — lệnh 0x23/0x20/0x78 sẽ gửi xuống F75 Max thật. Hãy chắc chắn đã sao lưu/đã biết cách khôi phục (Fn+Esc giữ 5 giây = khôi phục cài đặt gốc).";
            else if (_experimental)
                _expStatus.Text = "TRẠNG THÁI: Công tắc thử nghiệm đang BẬT nhưng Dry-run toàn cục đang bật — lệnh vẫn chỉ ghi log. Tắt Dry-run ở thanh bên để gửi thật.";
            else
                _expStatus.Text = "TRẠNG THÁI: Chỉ ghi log (dry-run lớp thử nghiệm) — app ghi lại đúng các gói sẽ gửi, không gửi gì xuống phím.";
        }

        private Control BuildPerKeyPage()
        {
            _ledPlayer.Exp = _exp;
            _ledPlayer.Log = AppendLog;

            var warn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xF5, 0x9B, 0x9B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9B, 0x9B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                Child = Tb("KHU VỰC THỬ NGHIỆM — Các lệnh per-key (0x23), stream (0x20) và feed nhạc (0x78) được tài liệu hoá công khai cho AULA L99 — mẫu dùng CHUNG nền tảng SONiX 0C45:800A với F75 Max (Gavin Graham, github.com/Revisory676/aula-l99-linux) — nhưng CHƯA được kiểm chứng trên F75 Max thật. Đọc 0xF5 là thao tác chỉ-đọc, an toàn để thử trước. Mọi lệnh ghi đều bị khoá sau công tắc thử nghiệm riêng + Dry-run; khi chưa mở khoá, app chỉ ghi log đúng các gói sẽ gửi.", null, 12),
            };

            _expSwitch = new PillSwitch();
            _expSwitch.IsChecked = false;
            _expSwitch.Changed += v => SetExperimental(v);
            _expStatus = Tb("", "sub");
            UpdateExpStatus();
            var gateCard = Card(V(10,
                Tb("Công tắc lớp thử nghiệm", "h2"),
                H(12, _expSwitch, Tb("Cho phép gửi thật lệnh 0x23 / 0x20 / 0x78 (vẫn cần tắt thêm Dry-run toàn cục)", "sub")),
                _expStatus));

            // --- Ve mau tung phim ---
            var paintState = new RgbAnimState { PaintMode = true, Brightness = 4 };
            var kb = new KeyboardView { Mode = KeyboardMode.Animated, State = paintState, Interactive = true, Painted = _painted };
            _paintInfo = Tb("Chạm vào từng phím trên hình để tô màu. Shift_R và Alt_R dùng chung light_index 0x54 trong dữ liệu của hãng — khi gửi, hai phím này nhận cùng một màu (giới hạn của dữ liệu gốc, chờ kiểm chứng trên phím thật).", "sub");
            kb.KeyClicked += (id, label) =>
            {
                if (_eraseMode) _painted.Remove(id);
                else _painted[id] = _paintColor;
                kb.InvalidateVisual();
                _paintInfo.Text = $"Đã tô {_painted.Count} phím. Phím vừa chọn: {label} ({id}).";
            };

            var palette = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var paletteColors = new (string Name, Color C)[]
            {
                ("Tím", Color.FromRgb(0x7C, 0x3A, 0xED)), ("Cyan", Color.FromRgb(0x22, 0xD3, 0xEE)),
                ("Hồng", Color.FromRgb(0xF4, 0x72, 0xB6)), ("Đỏ", Color.FromRgb(0xEF, 0x44, 0x44)),
                ("Cam", Color.FromRgb(0xF9, 0x73, 0x16)), ("Vàng", Color.FromRgb(0xFA, 0xCC, 0x15)),
                ("Lục", Color.FromRgb(0x34, 0xD3, 0x99)), ("Dương", Color.FromRgb(0x3B, 0x82, 0xF6)),
                ("Trắng", Color.FromRgb(0xF8, 0xFA, 0xFC)),
            };
            foreach (var (name, col) in paletteColors)
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
                sw.PointerPressed += (s, e) => { _paintColor = captured; _eraseMode = false; };
                palette.Children.Add(sw);
            }
            var eraseChip = new Button { Content = "Tẩy (tắt phím)" };
            eraseChip.Classes.Add("chip");
            eraseChip.Click += (s, e) => { _eraseMode = !_eraseMode; SetCls(eraseChip, "sel", _eraseMode); };
            palette.Children.Add(eraseChip);
            palette.Children.Add(GhostBtn("Tô tất cả", () =>
            {
                foreach (var k in KeyboardView.AllKeys) _painted[k.Id] = _paintColor;
                kb.InvalidateVisual();
            }));
            palette.Children.Add(GhostBtn("Xoá hết", () =>
            {
                _painted.Clear();
                kb.InvalidateVisual();
            }));

            var probeBtn = GhostBtn("Đọc thử màu phím (0xF5) — chỉ đọc", async () => await ProbeColorsAsync(kb));
            var sendBtn = GhostBtn("Gửi màu 0x23 + bật Custom 0x80", async () => await SendPaintedAsync());
            var paintCard = Card(V(12,
                Tb("Vẽ màu từng phím", "h2"),
                kb,
                palette,
                _paintInfo,
                H(10, probeBtn, sendBtn),
                Tb("Đọc 0xF5 chạy LIVE (chỉ đọc, không ghi gì) kể cả khi Dry-run đang bật — đây là bước kiểm chứng an toàn đầu tiên. Đóng app chính hãng/app của shop trước khi đọc để tránh tranh kênh. Gửi 0x23: khi chưa mở khoá, app ghi log đủ 13 gói của phiên ghi (begin → header → 9 khối → commit → end) + khối chọn hiệu ứng Custom 0x80.", "faint")));

            // --- GIF -> LED ---
            var ledState = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3 };
            var ledKb = new KeyboardView { Mode = KeyboardMode.Animated, State = ledState };
            var ledPicker = new ComboBox { Width = 340 };
            var ledItems = CollectGifItems();
            ledPicker.ItemsSource = ledItems.Select(i => i.Entry.Name).ToList();
            if (ledItems.Count > 0) ledPicker.SelectedIndex = 0;
            var ledInfo = Tb("Chọn GIF bất kỳ (thư viện, Của tôi, Thư viện Online đóng gói sẵn — kể cả GIF dải LED rộng) rồi bấm Nạp GIF: app lấy mẫu màu tại tâm rect từng phím theo hình học XML của F75 Max.", "sub");
            var loadLedBtn = GhostBtn("Nạp GIF", async () =>
            {
                int idx = ledPicker.SelectedIndex;
                if (idx < 0 || idx >= ledItems.Count) { ShowToast("Hãy chọn một GIF trước nhé."); return; }
                try
                {
                    var item = ledItems[idx];
                    string path = ResolveGifPath(item);
                    ledInfo.Text = "Đang chuyển GIF thành frame đèn phím…";
                    var anim = await Task.Run(() => GifLedConverter.Convert(path));
                    _ledPlayer.Load(anim, ledState, ledKb);
                    ledInfo.Text = $"“{item.Entry.Name}”: {anim.SourceWidth}×{anim.SourceHeight}, {anim.SourceFrames} frame gốc → {anim.Frames.Count} frame LED. Bấm Phát để xem trong app (~17fps khi chạy lên phím).";
                }
                catch (Exception ex) { ledInfo.Text = "Lỗi chuyển GIF: " + ex.Message; }
            });
            var playLedBtn = GhostBtn("Phát / Dừng", () =>
            {
                if (_ledPlayer.Anim == null) { ShowToast("Hãy nạp một GIF trước nhé."); return; }
                if (_ledPlayer.Timer.IsEnabled) _ledPlayer.Stop(); else _ledPlayer.Play();
            });
            var sendLedBtn = GhostBtn("Chạy lên đèn phím (thử nghiệm)", () =>
            {
                if (_ledPlayer.Anim == null) { ShowToast("Hãy nạp một GIF trước nhé."); return; }
                bool on = !_ledPlayer.Sending;
                _ledPlayer.SetSending(on);
                ShowToast(on
                    ? (_exp.WritesArmed ? "Đang stream THẬT lên đèn phím (~17fps)." : "Đang phát trong app + ghi log gói stream mẫu (chưa mở khoá gửi thật).")
                    : "Đã ngừng chạy lên đèn phím.");
            });
            var ledCard = Card(V(12,
                Tb("GIF → đèn phím (stream 0x20)", "h2"),
                ledKb,
                H(10, ledPicker, loadLedBtn),
                ledInfo,
                H(10, playLedBtn, sendLedBtn)));

            _expLogBox = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                Height = 150,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                Text = string.Join("\n", _logLines),
            };
            var logCard = Card(V(8, Tb("Nhật ký gói tin", "h2"), _expLogBox));

            var root = V(14,
                Tb("Per-key (thử nghiệm)", "h1"),
                warn,
                gateCard,
                paintCard,
                ledCard,
                logCard);
            return Scroll(root);
        }

        private async Task ProbeColorsAsync(KeyboardView kb)
        {
            if (!_selectedModel.FullSupport)
            {
                ShowToast("Bản đồ màu per-key này chỉ dành cho nền tảng F75 Max.");
                return;
            }
            try
            {
                ShowToast("Đang đọc màu phím bằng 0xF5 (chỉ đọc)…");
                var colors = await Task.Run(() => _exp.ReadColors());
                int shown = 0;
                for (int i = 0; i < MaxKeyData.ViewKeyWireIds.Length && i < KeyboardView.AllKeys.Count; i++)
                {
                    byte wire = MaxKeyData.ViewKeyWireIds[i];
                    if (colors.TryGetValue(wire, out var c))
                    {
                        _painted["k" + i] = Color.FromRgb(c.R, c.G, c.B);
                        shown++;
                    }
                }
                kb.InvalidateVisual();
                AppendLog($"0xF5: đã hiển thị {shown} phím lên hình vẽ (từ {colors.Count} id đọc được).");
                ShowToast($"Đọc xong: {colors.Count} id có màu, hiển thị {shown} phím lên hình. Nếu khớp đèn thật thì kênh đọc của F75 Max đã thông!");
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi đọc 0xF5: " + ex.Message);
                ShowToast("Đọc 0xF5 thất bại: " + ex.Message);
            }
        }

        private async Task SendPaintedAsync()
        {
            // Anh xa mau da to (theo phim hien thi) -> wire id. Shift_R/Alt_R
            // cung id 0x54: phim sau (Alt_R) thang — ghi log de minh bach.
            var colors = new Dictionary<byte, (byte R, byte G, byte B)>();
            for (int i = 0; i < MaxKeyData.ViewKeyWireIds.Length && i < KeyboardView.AllKeys.Count; i++)
            {
                if (_painted.TryGetValue("k" + i, out var c))
                {
                    byte wire = MaxKeyData.ViewKeyWireIds[i];
                    if (colors.ContainsKey(wire))
                        AppendLog($"Lưu ý: id 0x{wire:X2} bị trùng (Shift_R/Alt_R) — dùng màu của phím sau ({MaxKeyData.ViewKeyNames[i]}).");
                    colors[wire] = (c.R, c.G, c.B);
                }
            }
            if (colors.Count == 0) { ShowToast("Chưa tô phím nào — hãy tô vài phím trước nhé."); return; }
            try
            {
                var steps = L99Protocol.BuildColorWriteSteps(colors);
                steps.AddRange(L99Protocol.BuildCustomColorTrigger());
                bool sent = await Task.Run(() => _exp.SendSteps($"Ghi màu per-key 0x23 ({colors.Count} phím)", steps));
                ShowToast(sent
                    ? "Đã gửi THẬT màu per-key + bật hiệu ứng Custom 0x80. Kiểm tra đèn phím nhé!"
                    : "Đã ghi log đầy đủ các gói 0x23 + 0x80 (chưa gửi thật — xem Nhật ký gói tin).");
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi gửi 0x23: " + ex.Message);
                ShowToast("Gửi 0x23 thất bại: " + ex.Message);
            }
        }

        // ================= TRANG 7: WIDGET TFT =================
        private bool _samplingStats;
        private bool _weatherTried;

        private Control BuildWidgetPage()
        {
            _widgetKb = new KeyboardView { Mode = KeyboardMode.Screen, State = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED) } };
            _widgetTimer.Tick += (s, e) => RenderWidgetFrame();
            _widgetProgress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Width = 180, IsVisible = false };

            var kinds = new (string Title, string Desc)[]
            {
                ("Thông số máy", "CPU · RAM thật của máy này + GPU NVIDIA qua nvidia-smi (nếu có)"),
                ("Đang phát", "Đang phát triển — SMTC của Windows chưa tích hợp; hiện bản mô phỏng có ghi rõ"),
                ("Đồng hồ & thời tiết", "Giờ thật của máy + thời tiết Hà Nội (Open-Meteo, không cần API key)"),
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

            var autoSwitch = new PillSwitch();
            autoSwitch.Changed += v => { _widgetAuto = v; ShowToast(v ? "Tự động đẩy widget lên TFT mỗi 5 giây (tôn trọng Dry-run)." : "Đã tắt tự động đẩy widget."); };
            var uploadBtn = WriteBtn("Đẩy widget lên TFT", async () => await UploadCurrentWidgetAsync(), primary: false);
            var weatherBtn = GhostBtn("Cập nhật thời tiết", async () => await FetchWeatherAsync());

            var root = V(14,
                Tb("Widget TFT", "h1"),
                Tb("Widget được vẽ ngay trên máy tính (host) thành ảnh 128×128 rồi nạp vào màn hình phím qua kênh màn hình đã kiểm chứng — cùng cơ chế với nạp GIF.", "sub"),
                Card(_widgetKb, 14),
                Tb("Chọn widget", "h2"),
                selectorRow,
                H(12, uploadBtn, _widgetProgress, autoSwitch, Tb("Tự động đẩy mỗi 5 giây", "sub"), weatherBtn),
                Tb("Widget “Đang phát”: Windows SMTC (System Media Transport Controls) cần tích hợp WinRT riêng — để tránh làm nặng/không ổn định bản build này, tính năng để ở trạng thái Đang phát triển (xem BUILD-INFO). Widget thông số và đồng hồ là dữ liệu thật.", "faint"));
            return Scroll(root);
        }

        private async Task FetchWeatherAsync()
        {
            _weatherTried = true;
            var w = await WeatherService.FetchHanoiAsync();
            if (w != null)
            {
                _weather = w;
                AppendLog($"Thời tiết Hà Nội: {w.TempC}°C, {w.Description}, độ ẩm {w.Humidity}%.");
                if (_widgetKind == 2) RenderWidgetFrame();
            }
            else AppendLog("Không tải được thời tiết (mạng?) — widget đồng hồ chỉ hiện giờ.");
        }

        private void RenderWidgetFrame()
        {
            if (_widgetKb == null) return;
            _widgetTickCount++;
            if (_widgetKind == 0)
            {
                if (!_samplingStats)
                {
                    _samplingStats = true;
                    _ = Task.Run(() =>
                    {
                        var s = SystemStats.Sample();
                        Dispatcher.UIThread.Post(() =>
                        {
                            _lastStats = s;
                            _samplingStats = false;
                            if (_widgetKind == 0 && _currentPage == "widget") RenderWidgetFrame();
                        });
                    });
                }
                if (_lastStats == null) return; // cho mau dau tien
                var bmpStats = WidgetRenderer.RenderStatsReal(_lastStats);
                SetWidgetBitmap(bmpStats);
            }
            else if (_widgetKind == 1)
            {
                double t = (DateTime.Now - DateTime.Today).TotalSeconds;
                var bmp = WidgetRenderer.RenderNowPlaying(t % WidgetRenderer.SongDuration, t);
                SetWidgetBitmap(bmp);
            }
            else
            {
                if (!_weatherTried) _ = FetchWeatherAsync();
                string weatherLine = _weather != null
                    ? $"Hà Nội {_weather.TempC}°C · {_weather.Description}"
                    : "Hà Nội · chưa có dữ liệu thời tiết";
                var bmp = WidgetRenderer.RenderClock(DateTime.Now, weatherLine, "T Keyboard Studio");
                SetWidgetBitmap(bmp);
            }
            if (_widgetAuto && _widgetTickCount % 5 == 0 && !_busy && _currentPage == "widget")
                _ = UploadCurrentWidgetAsync();
        }

        private void SetWidgetBitmap(Bitmap bmp)
        {
            var old = _widgetKb.ScreenImage;
            _widgetKb.ScreenImage = bmp;
            if (old != null && !ReferenceEquals(old, bmp)) old.Dispose();
        }

        private async Task UploadCurrentWidgetAsync()
        {
            try
            {
                Bitmap bmp;
                if (_widgetKind == 0)
                {
                    var s = _lastStats ?? await Task.Run(() => SystemStats.Sample());
                    _lastStats = s;
                    bmp = WidgetRenderer.RenderStatsReal(s);
                }
                else if (_widgetKind == 1)
                {
                    double t = (DateTime.Now - DateTime.Today).TotalSeconds;
                    bmp = WidgetRenderer.RenderNowPlaying(t % WidgetRenderer.SongDuration, t);
                }
                else
                {
                    string weatherLine = _weather != null
                        ? $"Hà Nội {_weather.TempC}°C · {_weather.Description}"
                        : "Hà Nội · chưa có dữ liệu thời tiết";
                    bmp = WidgetRenderer.RenderClock(DateTime.Now, weatherLine, "T Keyboard Studio");
                }
                byte[] stream;
                using (var img = BitmapToImageSharp(bmp))
                    stream = DisplayStreamBuilder.Build(new[] { img }, 1000);
                await UploadStreamAsync(stream, "Widget TFT", _widgetProgress);
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi đẩy widget: " + ex.Message);
                ShowToast("Lỗi đẩy widget: " + ex.Message);
            }
        }

        private static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> BitmapToImageSharp(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms);
                ms.Position = 0;
                return SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(ms);
            }
        }

        // ================= TRANG 8: DONG HO & CAI DAT =================
        private Control SettingRow(string label, string sub, bool initial, Action<bool> onChanged)
        {
            var sw = new PillSwitch();
            sw.IsChecked = initial;
            sw.Changed += v => onChanged(v);
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

        private Control BuildSettingsPage()
        {
            _clockTimer.Tick += (s, e) => { UpdateClockLabels(); RefreshTypingLabels(); };
            _clockBig = new TextBlock
            {
                FontSize = 44, FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF8)),
            };
            _clockDate = Tb("", "sub");
            UpdateClockLabels();

            var syncBtn = WriteBtn("Đồng bộ đồng hồ", async () =>
                await RunStepsAsync("Đồng bộ đồng hồ", () => F75Protocol.BuildClockSync(DateTime.Now)), primary: false);
            var clockCard = Card(V(10,
                Tb("Đồng hồ phím", "h2"),
                _clockBig,
                _clockDate,
                syncBtn));

            // --- Khoi cai dat thiet bi 04 17 ---
            bool winLock = false, altF4 = false, altTab = false, fnSwitch = false;
            int sleepVal = 2, responseVal = 2;
            var devSetCard = Card(V(8,
                Tb("Cài đặt thiết bị (khối 04 17)", "h2"),
                Tb("Khối cài đặt này được mohin7 và VitalyArt tài liệu hoá NGAY TRÊN F75 Max (Win lock, khoá Alt+F4/Alt+Tab, chuyển Fn, giờ ngủ, độ phản hồi phím 1–5) và đi qua cùng khung giao dịch với khối sáng. Vẫn tôn trọng Dry-run như mọi thao tác ghi.", "sub"),
                SettingRow("Khoá phím Windows", "Chặn phím Win khi chơi game", false, v => winLock = v),
                SettingRow("Khoá Alt+F4", "Chặn tổ hợp Alt+F4", false, v => altF4 = v),
                SettingRow("Khoá Alt+Tab", "Chặn tổ hợp Alt+Tab", false, v => altTab = v),
                SettingRow("Chuyển đổi Fn", "Hoán đổi vai trò lớp Fn", false, v => fnSwitch = v),
                H(10, Tb("Tự ngủ:", "sub"), Segmented(new[] { "Không ngủ", "1 phút", "5 phút", "30 phút" }, 2, i => sleepVal = i).Root),
                V(4, Tb("Độ phản hồi phím (1 = nhanh nhất … 5 = chậm nhất, chống rung)", "sub"),
                    new Slider { Minimum = 1, Maximum = 5, Value = 2, Width = 260, TickFrequency = 1, IsSnapToTickEnabled = true }
                        .Also(sl => sl.PropertyChanged += (s, e) => { if (e.Property == Slider.ValueProperty) responseVal = (int)sl.Value; })),
                WriteBtn("Gửi cài đặt xuống phím", async () =>
                    await RunStepsAsync("Gửi cài đặt thiết bị 04 17",
                        () => L99Protocol.BuildSettingsSteps(winLock, altF4, altTab, fnSwitch, sleepVal, responseVal)))));

            // --- Cai dat app ---
            var appCard = Card(V(6,
                Tb("Cài đặt app", "h2"),
                SettingRow("Dry-run (chế độ an toàn)", "Bật: mọi thao tác ghi chỉ ghi log, không ghi xuống phím", true, v =>
                {
                    if (_syncingDry) return;
                    SetDryRun(v, fromUi: true);
                }).Also(c => _drySwitch = FindPill(c)),
                SettingRow("Khởi động cùng Windows", "Tự mở app khi đăng nhập Windows (ghi vào HKCU Run)", GetAutostart(), v => SetAutostart(v)),
                SettingRow("Thu nhỏ xuống khay hệ thống", "Đóng cửa sổ thì app vẫn chạy nền ở khay", true, v => _trayEnabled = v),
                Tb("App không tự kiểm tra cập nhật và không gọi mạng nào ngoài: tải GIF bạn chủ động bấm, thời tiết khi mở trang Widget, và server điều khiển qua LAN khi bạn bật.", "faint")));

            // --- Stage 2: ho so theo app ---
            _profileCurrent = Tb("App đang mở: —", "sub");
            _profileList = new StackPanel { Spacing = 6 };
            RefreshProfileList();
            var profileCard = Card(V(10,
                Tb("Hồ sơ theo ứng dụng (Stage 2)", "h2"),
                Tb("Khi bật, app theo dõi cửa sổ đang active; nếu app đó đã được gán hồ sơ, app tự áp hiệu ứng RGB (+ GIF TFT nếu có) đã lưu. Áp dụng vẫn tôn trọng Dry-run.", "sub"),
                SettingRow("Bật theo dõi ứng dụng", "GetForegroundWindow, kiểm tra mỗi 0,7 giây", false, v =>
                {
                    _profilesOn = v;
                    if (v) _profiles.Start(); else _profiles.Stop();
                    ShowToast(v ? "Đã bật hồ sơ theo ứng dụng." : "Đã tắt hồ sơ theo ứng dụng.");
                }),
                _profileCurrent,
                GhostBtn("Gán app đang mở cho hiệu ứng RGB + GIF đang chọn", () =>
                {
                    string exe = _profiles.CurrentExe;
                    if (string.IsNullOrEmpty(exe)) { ShowToast("Chưa xác định được app đang mở — hãy bấm sang app khác rồi quay lại."); return; }
                    var p = _profiles.Find(exe) ?? new AppProfile { ExeName = exe };
                    p.EffectId = _rgb.Effect;
                    p.TftGifPath = _selectedGif != null ? ResolveGifPath(_selectedGif) : "";
                    if (_profiles.Find(exe) == null) _profiles.Profiles.Add(p);
                    _profiles.Save();
                    RefreshProfileList();
                    ShowToast($"Đã gán hồ sơ cho {exe}: hiệu ứng {EffectFriendlyNames[p.EffectId]}" + (string.IsNullOrEmpty(p.TftGifPath) ? "" : " + GIF TFT đã chọn"));
                }),
                _profileList));

            // --- Stage 2: macro host ---
            _macroStatus = Tb("Macro chạy trên máy (host) bằng SendInput — không ghi xuống bộ nhớ phím. Phím tắt toàn cục: Ctrl+Alt+R ghi/dừng, Ctrl+Alt+P phát macro gần nhất.", "sub");
            _macroList = new StackPanel { Spacing = 6 };
            RefreshMacroList();
            var macroCard = Card(V(10,
                Tb("Macro trên máy (Stage 2)", "h2"),
                _macroStatus,
                H(10,
                    GhostBtn("Ghi macro mới", () => _macros.StartRecording("Macro " + (_macros.Macros.Count + 1))),
                    GhostBtn("Dừng ghi", () => _macros.StopRecording()),
                    GhostBtn("Phát macro gần nhất", () =>
                    {
                        var m = _macros.Macros.Count > 0 ? _macros.Macros[_macros.Macros.Count - 1] : null;
                        if (m == null) ShowToast("Chưa có macro nào.");
                        else _macros.Play(m);
                    })),
                _macroList));

            // --- Stage 2: thong ke go phim ---
            _typingStatus = Tb("Chưa bật. Khi bật, app CHỈ ĐẾM số lần nhấn theo vị trí phím (heatmap + WPM) — không ghi nội dung, không ghi chuỗi phím, không lưu lại khi tắt app.", "sub");
            _heatKb = new KeyboardView { Mode = KeyboardMode.Animated, State = _heatState };
            var typingCard = Card(V(10,
                Tb("Thống kê gõ phím (Stage 2)", "h2"),
                SettingRow("Bật đếm phím", "Hook bàn phím mức thấp, chỉ đếm — không ghi nội dung", false, v =>
                {
                    _typingOn = v;
                    if (v) { _typing.Start(); ShowToast("Đã bật thống kê gõ phím (chỉ đếm)."); }
                    else { _typing.Stop(); ShowToast("Đã tắt thống kê gõ phím."); }
                    RefreshTypingLabels();
                }),
                _typingStatus,
                _heatKb));

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
                Tb("T Keyboard Studio", "h2"),
                Tb("Phiên bản 3.4.0 — bản thật: F75 Max hỗ trợ đầy đủ qua giao thức đã kiểm chứng; lớp per-key/stream/nhạc là thử nghiệm có khoá an toàn (xem trang Per-key). Mới: Liquid Glass, hình nền + màu chủ đề, màn hình chào 3 phong cách, Chợ plugin, GameSense CS2 (GSI). v3.4 thêm Driver Pack 1: họ Sinowealth kb_by_v2 (AULA F75, Leobog Hi75, YUNZII Cake, GravaStar K1) — trang riêng, định danh bằng UUID 0x82 trước khi ghi, chưa kiểm chứng trên phần cứng thật. Dry-run bật mặc định.", "sub"),
                Tb("Logo Mẫu C — chữ “T” tạo hình từ phím spacebar — đã được người dùng duyệt.", "sub"),
                Tb("Font chữ: Be Vietnam Pro (SIL OFL 1.1). GIF thư viện: tự làm cho Tiến. Giao thức per-key: Gavin Graham (AULA L99).", "faint"));
            var aboutCard = Card(H(14, logoWrap, aboutTexts));

            var root = V(14,
                Tb("Đồng hồ & Cài đặt", "h1"),
                BuildAppearanceCard(),
                clockCard,
                devSetCard,
                appCard,
                profileCard,
                macroCard,
                typingCard,
                aboutCard);
            return Scroll(root);
        }

        private static PillSwitch FindPill(Control row)
        {
            // SettingRow tra ve CardFlat(Grid(texts, PillSwitch))
            if (row is Border b && b.Child is Grid g)
                foreach (var c in g.Children)
                    if (c is PillSwitch p) return p;
            return null;
        }

        private void UpdateClockLabels()
        {
            if (_clockBig == null) return;
            var now = DateTime.Now;
            _clockBig.Text = now.ToString("HH:mm:ss");
            _clockDate.Text = "Giờ hiện tại trên máy tính — bấm Đồng bộ để gửi giờ này xuống màn hình phím (khối 04 28 đã kiểm chứng).";
        }

        private static bool GetAutostart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                    return key?.GetValue("TKeyboardStudio") != null;
            }
            catch { return false; }
        }

        private static void SetAutostart(bool on)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (on) key.SetValue("TKeyboardStudio", "\"" + (Environment.ProcessPath ?? "") + "\"");
                    else key.DeleteValue("TKeyboardStudio", false);
                }
            }
            catch { }
        }

        // ---------- Stage 2: ho so theo app ----------
        private void OnForegroundAppChanged(string exe)
        {
            if (_profileCurrent != null) _profileCurrent.Text = "App đang mở: " + exe;
            if (!_profilesOn) return;
            if (exe.IndexOf("TienKeyboardStudio", StringComparison.OrdinalIgnoreCase) >= 0) return;
            var p = _profiles.Find(exe);
            if (p == null) return;
            AppendLog($"Hồ sơ app: {exe} → hiệu ứng {p.EffectId} ({EffectFriendlyNames[p.EffectId]})" + (string.IsNullOrEmpty(p.TftGifPath) ? "" : " + GIF TFT"));
            _ = ApplyRgbAsync(p.EffectId, _rgb.Brush, _rgb.Multi, (int)_rgb.Brightness, (int)_rgb.Speed, _rgb.Direction);
            _rgb.Effect = p.EffectId;
            if (!string.IsNullOrEmpty(p.TftGifPath) && File.Exists(p.TftGifPath) && _selectedModel.HasScreen)
                _ = UploadGifFileAsync(p.TftGifPath, FitMode.Fill, null);
        }

        private void RefreshProfileList()
        {
            if (_profileList == null) return;
            _profileList.Children.Clear();
            if (_profiles.Profiles.Count == 0)
            {
                _profileList.Children.Add(Tb("Chưa có hồ sơ nào. Mở app/game bạn muốn gán, quay lại đây và bấm nút gán.", "faint"));
                return;
            }
            foreach (var p in _profiles.Profiles)
            {
                var prof = p;
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                row.Children.Add(Tb($"{prof.ExeName} — {EffectFriendlyNames[prof.EffectId]}" + (string.IsNullOrEmpty(prof.TftGifPath) ? "" : " + GIF TFT"), "sub"));
                var del = GhostBtn("Xoá", () =>
                {
                    _profiles.Profiles.Remove(prof);
                    _profiles.Save();
                    RefreshProfileList();
                });
                Grid.SetColumn(del, 1);
                row.Children.Add(del);
                _profileList.Children.Add(row);
            }
        }

        // ---------- Stage 2: macro ----------
        private void RefreshMacroList()
        {
            if (_macroList == null) return;
            _macroList.Children.Clear();
            if (_macros.Macros.Count == 0)
            {
                _macroList.Children.Add(Tb("Chưa có macro nào.", "faint"));
                return;
            }
            foreach (var m in _macros.Macros)
            {
                var macro = m;
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
                row.Children.Add(Tb($"{macro.Name} — {macro.Steps.Count} bước", "sub"));
                var play = GhostBtn("Phát", () => _macros.Play(macro));
                Grid.SetColumn(play, 1);
                row.Children.Add(play);
                var del = GhostBtn("Xoá", () =>
                {
                    _macros.Macros.Remove(macro);
                    _macros.Save();
                    RefreshMacroList();
                });
                Grid.SetColumn(del, 2);
                row.Children.Add(del);
                _macroList.Children.Add(row);
            }
        }

        // ---------- Stage 2: thong ke go phim ----------
        private void RefreshTypingLabels()
        {
            if (_typingStatus == null) return;
            if (!_typingOn)
            {
                _typingStatus.Text = "Đang tắt. Khi bật, app CHỈ ĐẾM số lần nhấn theo vị trí phím (heatmap + WPM) — không ghi nội dung, không ghi chuỗi phím, không lưu lại khi tắt app.";
                return;
            }
            var mins = (DateTime.Now - _typing.SessionStart).TotalMinutes;
            int max = Math.Max(1, _typing.KeyCounts.Max());
            var heat = new Dictionary<string, double>();
            for (int i = 0; i < 80; i++)
                if (_typing.KeyCounts[i] > 0) heat["k" + i] = _typing.KeyCounts[i] / (double)max;
            _heatState.Heat = heat;
            _heatKb?.InvalidateVisual();
            int topIdx = Array.IndexOf(_typing.KeyCounts, max);
            string topName = topIdx >= 0 && topIdx < MaxKeyData.ViewKeyNames.Length ? MaxKeyData.ViewKeyNames[topIdx] : "—";
            _typingStatus.Text = $"Tổng số phím đã gõ: {_typing.TotalKeys} · Tốc độ hiện tại: {_typing.Wpm:F0} WPM · Phiên: {mins:F1} phút · Phím gõ nhiều nhất: {topName} ({max} lần). Heatmap bên dưới nóng dần theo số lần gõ.";
        }

        // ================= TRANG 9: DIEU KHIEN TU DIEN THOAI =================
        private Control BuildPhonePage()
        {
            _phone.GetState = () => new RemoteState
            {
                DryRun = _dryRun,
                Experimental = _experimental,
                Model = _selectedModel.Name,
                Effects = EffectFriendlyNames.Select((n, i) => (i, n)).ToList(),
                Gifs = GifCatalog.Library().Select(e => e.Name).ToList(),
            };
            _phone.ApplyEffect = (id, r, g, b, bright, speed, dir) => Dispatcher.UIThread.Post(async () =>
            {
                _rgb.Effect = id;
                _rgb.Brush = Color.FromRgb(r, g, b);
                _rgb.Brightness = bright;
                _rgb.Speed = speed;
                _rgb.Direction = dir;
                if (_homeFxValue != null) _homeFxValue.Text = EffectFriendlyNames[id];
                AppendLog($"Điện thoại: áp hiệu ứng {EffectFriendlyNames[id]}.");
                await ApplyRgbAsync(id, _rgb.Brush, _rgb.Multi, bright, speed, dir);
            });
            _phone.ApplyGif = name => Dispatcher.UIThread.Post(async () =>
            {
                var entry = GifCatalog.Library().FirstOrDefault(e => e.Name == name);
                if (entry == null) { AppendLog("Điện thoại: không tìm thấy GIF " + name); return; }
                AppendLog("Điện thoại: nạp GIF " + name);
                await UploadGifFileAsync(ExtractEmbeddedGif(entry), FitMode.Fill, null);
            });
            _phone.ApplyWidget = kind => Dispatcher.UIThread.Post(async () =>
            {
                _widgetKind = kind;
                AppendLog("Điện thoại: đẩy widget " + (kind == 0 ? "Thông số máy" : "Đồng hồ"));
                await UploadCurrentWidgetAsync();
            });

            _phoneStatus = Tb("Server đang TẮT. Bật server để điều khiển từ điện thoại trong cùng mạng Wi-Fi.", "sub");
            _qrImage = new Avalonia.Controls.Image { Width = 168, Height = 168, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Center };
            var qrMock = new QrMock { Width = 168, Height = 168, HorizontalAlignment = HorizontalAlignment.Center };
            _phoneUrlText = new TextBlock
            {
                Text = "—",
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xD5, 0xE6)),
            };
            _pinRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center };

            Button toggleBtn = null;
            toggleBtn = PrimaryBtn("Bật server điều khiển", () =>
            {
                if (_phone.Running)
                {
                    _phone.Stop();
                    _phoneStatus.Text = "Server đang TẮT.";
                    _qrImage.IsVisible = false;
                    qrMock.IsVisible = true;
                    _phoneUrlText.Text = "—";
                    _pinRow.Children.Clear();
                    toggleBtn.Content = "Bật server điều khiển";
                    AppendLog("Server điện thoại: đã tắt.");
                }
                else
                {
                    try
                    {
                        _phone.Start();
                        var gen = new QRCodeGenerator();
                        var data = gen.CreateQrCode(_phone.Url, QRCodeGenerator.ECCLevel.M);
                        var png = new PngByteQRCode(data).GetGraphic(12);
                        _qrImage.Source = new Bitmap(new MemoryStream(png));
                        _qrImage.IsVisible = true;
                        qrMock.IsVisible = false;
                        _phoneUrlText.Text = _phone.Url;
                        _pinRow.Children.Clear();
                        foreach (char d in _phone.Pin)
                        {
                            _pinRow.Children.Add(new Border
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
                        _phoneStatus.Text = $"Server đang CHẠY tại {_phone.Url} — quét QR bằng điện thoại (cùng Wi-Fi) để mở trang điều khiển. Mọi thao tác từ điện thoại đều tôn trọng Dry-run như trên PC.";
                        toggleBtn.Content = "Tắt server";
                        AppendLog($"Server điện thoại: đang chạy {_phone.Url} (PIN {_phone.Pin}).");
                    }
                    catch (Exception ex)
                    {
                        _phoneStatus.Text = "Không bật được server: " + ex.Message;
                        AppendLog("Lỗi server điện thoại: " + ex.Message);
                    }
                }
            });
            toggleBtn.HorizontalAlignment = HorizontalAlignment.Left;

            var qrCard = Card(V(12,
                Tb("Kết nối điện thoại", "h2"),
                _phoneStatus,
                toggleBtn,
                qrMock,
                _qrImage,
                _phoneUrlText,
                Tb("Mã PIN đi kèm (đã nằm sẵn trong link QR)", "h3"),
                _pinRow,
                Tb("PIN tự sinh mới mỗi lần bật server. Chỉ dùng trong mạng nhà của bạn; server chỉ nghe trong LAN và tắt mặc định.", "faint")));

            var stepsCard = Card(V(10,
                Tb("Cách dùng", "h2"),
                Tb("1) Cắm F75 Max vào PC bằng cáp USB-C và mở app này.", "sub"),
                Tb("2) Bấm “Bật server điều khiển” ở thẻ bên trái.", "sub"),
                Tb("3) Dùng điện thoại (cùng Wi-Fi với PC) quét mã QR — trang điều khiển mở ngay, PIN đã điền sẵn trong link.", "sub"),
                Tb("4) Trên điện thoại: chọn nhanh hiệu ứng đèn, nạp GIF lên màn hình TFT, hoặc đẩy widget — PC nhận lệnh và ghi xuống phím (vẫn tôn trọng Dry-run).", "sub"),
                Tb("Lần đầu bật server, Windows Firewall có thể hỏi — hãy cho phép trong mạng Private/Home của bạn.", "faint")));

            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("360,*"), ColumnSpacing = 14 };
            cols.Children.Add(qrCard);
            Grid.SetColumn(stepsCard, 1);
            cols.Children.Add(stepsCard);

            var root = V(14,
                Tb("Điều khiển từ điện thoại", "h1"),
                Tb("PC cắm phím và chạy app → app hiện mã QR thật → điện thoại quét (cùng Wi-Fi) → mở trang điều khiển → chọn hiệu ứng/GIF → PC ghi xuống phím.", "sub"),
                cols,
                CardFlat(Tb("Lưu ý: điện thoại không chỉnh trực tiếp được phím — F75 Max không nhận cài đặt qua Bluetooth và iOS giới hạn kết nối với phụ kiện. PC là cầu nối bắt buộc và phải đang mở app; điện thoại chỉ gửi lệnh qua mạng nhà tới PC.", "faint")));
            return Scroll(root);
        }

        // ================= v3.3: GIAO DIEN (nen + mau chu de) =================
        private void SaveUi()
        {
            try { GifLibraryStore.Save(_ui); }
            catch (Exception ex) { AppendLog("Không lưu được settings.json: " + ex.Message); }
        }

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
        public void ApplyBackground(string skin, bool save = true)
        {
            if (skin == "custom")
            {
                string path = BackgroundStore.ResolveCustomPath(_ui);
                if (path == null)
                {
                    if (save) ShowToast("Chưa có ảnh nền riêng — hãy bấm “Ảnh của bạn…” để chọn ảnh trước.");
                    skin = "default";
                }
                else
                {
                    try
                    {
                        BackdropImage.Source = new Bitmap(path);
                        BackdropImage.IsVisible = true;
                        BackdropShade.IsVisible = true;
                        _ui.Background = "custom";
                        if (save) SaveUi();
                        return;
                    }
                    catch (Exception ex) { AppendLog("Không nạp được ảnh nền riêng: " + ex.Message); skin = "default"; }
                }
            }
            string file = skin == "aurora" ? "skin-aurora.jpg"
                        : skin == "sunset" ? "skin-sunset.jpg"
                        : skin == "forest" ? "skin-forest.jpg"
                        : skin == "space" ? "skin-space.jpg"
                        : null;
            if (file == null)
            {
                BackdropImage.IsVisible = false;
                BackdropShade.IsVisible = false;
                _ui.Background = "default";
            }
            else
            {
                BackdropImage.Source = LoadSkin(file);
                BackdropImage.IsVisible = true;
                BackdropShade.IsVisible = true;
                _ui.Background = skin;
            }
            if (save) SaveUi();
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

        private void ApplyAccentByIndex(int index, bool save = true)
        {
            var acc = AccentTable.Get(index);
            _ui.AccentIndex = index >= 0 && index < AccentTable.All.Length ? index : 0;
            ApplyAccent(
                Color.FromRgb(acc.A.R, acc.A.G, acc.A.B),
                Color.FromRgb(acc.B.R, acc.B.G, acc.B.B));
            if (save) SaveUi();
        }

        private static Color Lighten(Color c, double f) => Color.FromRgb(
            (byte)Math.Min(255, c.R * f), (byte)Math.Min(255, c.G * f), (byte)Math.Min(255, c.B * f));

        // Chon anh nen cua nguoi dung: copy vao %AppData%\TienKeyboardStudio\
        // Backgrounds, nen/thu nho anh lon (canh dai nhat 1920, JPEG q82).
        private async Task ImportCustomBackgroundAsync()
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Chọn ảnh nền cho app",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Ảnh") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp" } },
                    },
                });
                if (files.Count == 0) return;
                string src = files[0].Path.LocalPath;
                string savedName = await Task.Run(() => ImportBackgroundFile(src));
                _ui.CustomBackgroundFile = savedName;
                ApplyBackground("custom");
                RefreshSkinSelection();
                ShowToast("Đã đặt ảnh của bạn làm nền app (đã nén vào thư mục Backgrounds của app).");
                AppendLog("Ảnh nền riêng: " + src + " -> Backgrounds/" + savedName);
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi chọn ảnh nền: " + ex.Message);
                ShowToast("Không dùng được ảnh này: " + ex.Message);
            }
        }

        private static string ImportBackgroundFile(string srcPath)
        {
            Directory.CreateDirectory(BackgroundStore.BackgroundsDir);
            using (var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(srcPath))
            {
                var (tw, th) = AppearanceMath.ComputeTargetSize(img.Width, img.Height, 1920);
                if (tw != img.Width || th != img.Height)
                    img.Mutate(m => m.Resize(tw, th));
                string name = "custom-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jpg";
                string dest = System.IO.Path.Combine(BackgroundStore.BackgroundsDir, name);
                using (var fs = File.Create(dest))
                    img.Save(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 82 });
                return name;
            }
        }

        // ================= v3.3: MAN HINH CHAO =================
        private PluginState GetPluginState(string id)
        {
            if (!_ui.Plugins.TryGetValue(id, out var st) || st == null)
            {
                st = new PluginState { Enabled = id == GreetingStyles.PluginGalaxy };
                _ui.Plugins[id] = st;
            }
            if (st.Config == null) st.Config = new Dictionary<string, string>();
            return st;
        }

        private List<string> EnabledBootPluginIds()
        {
            var ids = new List<string>();
            foreach (var id in new[] { GreetingStyles.PluginGalaxy, GreetingStyles.PluginDeepSeek, GreetingStyles.Plugin550C })
                if (GetPluginState(id).Enabled) ids.Add(id);
            return ids;
        }

        private string ResolveCurrentGreetingStyle()
            => GreetingStyles.Resolve(EnabledBootPluginIds(), _ui.GreetingPlugin);

        private void RefreshGreetingStyleText()
        {
            if (_greetingStyleText == null) return;
            string style = ResolveCurrentGreetingStyle();
            string name = style == GreetingStyles.Terminal ? "DeepSeek Harness (terminal)"
                        : style == GreetingStyles.Amber550 ? "550C Boot (CRT hổ phách)"
                        : style == GreetingStyles.Galaxy ? "Tien Galaxy Boot (vệt sáng)"
                        : "Không hiệu ứng (chỉ logo)";
            _greetingStyleText.Text = "Phong cách đang dùng: " + name + " — đổi tại trang Chợ plugin (bật plugin khởi động bạn muốn).";
        }

        public void ShowGreeting() => ShowGreeting(null);

        private void ShowGreeting(string forceStyle)
        {
            string style = forceStyle ?? ResolveCurrentGreetingStyle();
            _greetingStyleNow = style;
            BuildGreetingBody(style);
            GreetingOverlay.IsVisible = true;
            GreetingOverlay.Opacity = 1;
            GreetingStatus.Text = "Đang nạp giao diện…";
            _greetingTimer?.Stop();
            _greetingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(style == GreetingStyles.Terminal ? 2600 : 2000) };
            _greetingTimer.Tick += (s, e) =>
            {
                _greetingTimer.Stop();
                GreetingStatus.Text = "Sẵn sàng";
                DismissGreeting();
            };
            _greetingTimer.Start();
        }

        private void BuildGreetingBody(string style)
        {
            _terminalTimer?.Stop();
            _sweep?.Stop();
            GreetingBodyHost.Children.Clear();

            if (style == GreetingStyles.Terminal)
            {
                GreetingSub.Text = "DeepSeek Harness — màn chào kiểu terminal";
                var st = GetPluginState(GreetingStyles.PluginDeepSeek);
                string name = st.GetCfg("name", "T OPERATOR");
                string code = st.GetCfg("code", "TKB-001");
                var lines = new[]
                {
                    "T KEYBOARD // DEEPSEEK HARNESS v1.2.0",
                    "[ok] nap module rgb ............ OK",
                    "[ok] nap module tft + widget ... OK",
                    "[ok] quet thiet bi HID ......... OK",
                    $"[ok] identity: {name} · {code}",
                    "READY _",
                };
                var text = new TextBlock
                {
                    FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                    FontSize = 12.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)),
                    LineHeight = 18,
                };
                var panel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x05, 0x08, 0x07)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x4A, 0xDE, 0x80)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 10),
                    Child = text,
                };
                GreetingBodyHost.Children.Add(panel);
                int shown = 0;
                _terminalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
                _terminalTimer.Tick += (s, e) =>
                {
                    if (shown >= lines.Length) { _terminalTimer.Stop(); return; }
                    text.Text = string.Join("\n", lines.Take(shown + 1));
                    shown++;
                };
                _terminalTimer.Start();
            }
            else if (style == GreetingStyles.Amber550)
            {
                GreetingSub.Text = "550C Boot — đồng hồ CRT hổ phách";
                _sweep = new GreetingSweep { AmberMode = true, Height = 30, VerticalAlignment = VerticalAlignment.Center };
                var stack = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
                stack.Children.Add(_sweep);
                var lbl = Tb("5 5 0 C", null, 12);
                lbl.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x20));
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.FontFamily = new FontFamily("Consolas, 'Courier New', monospace");
                stack.Children.Add(lbl);
                GreetingBodyHost.Children.Add(stack);
                _sweep.Play();
            }
            else if (style == GreetingStyles.Galaxy)
            {
                GreetingSub.Text = "Chào mừng bạn trở lại";
                _sweep = new GreetingSweep { Height = 30, VerticalAlignment = VerticalAlignment.Center };
                GreetingBodyHost.Children.Add(_sweep);
                _sweep.Play();
            }
            else
            {
                GreetingSub.Text = "Chào mừng bạn trở lại";
                var lbl = Tb("Sẵn sàng", "faint");
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.VerticalAlignment = VerticalAlignment.Center;
                GreetingBodyHost.Children.Add(lbl);
            }
        }

        public void DismissGreeting()
        {
            if (!GreetingOverlay.IsVisible) return;
            _greetingTimer?.Stop();
            _terminalTimer?.Stop();
            _sweep?.Stop();
            GreetingOverlay.Opacity = 0;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
            t.Tick += (s, e) => { t.Stop(); GreetingOverlay.IsVisible = false; };
            t.Start();
        }

        // ================= v3.3: THE GIAO DIEN trong Cai dat =================
        private readonly List<(string Id, Border Card)> _skinCards = new List<(string, Border)>();
        private Avalonia.Controls.Image _customSkinThumb;

        private void RefreshSkinSelection()
        {
            foreach (var (id, card) in _skinCards)
            {
                bool sel = id == _ui.Background;
                card.BorderBrush = sel ? UiBrushes.AccentGradient() : UiBrushes.GlassBorder;
                card.BorderThickness = new Thickness(sel ? 1.8 : 1);
            }
            if (_customSkinThumb != null)
            {
                string p = BackgroundStore.ResolveCustomPath(_ui);
                if (p != null)
                {
                    try { _customSkinThumb.Source = new Bitmap(p); } catch { }
                }
            }
        }

        /// <summary>
        /// Muc "Giao dien" trong Cai dat (port tu Preview v0.10 da duyet, noi
        /// that): hinh nen app (mac dinh / skin co san / anh cua ban qua hop
        /// chon file that), mau chu de 4 mau ap dung ngay, xem truoc Sang/Toi
        /// va tuy chon man hinh chao. Moi lua chon luu vao settings.json.
        /// </summary>
        private Control BuildAppearanceCard()
        {
            _skinCards.Clear();
            var skins = new (string Id, string Name, string File)[]
            {
                ("default", "Mặc định", null),
                ("aurora", "Cực quang", "skin-aurora.jpg"),
                ("sunset", "Hoàng hôn", "skin-sunset.jpg"),
                ("forest", "Rừng sâu", "skin-forest.jpg"),
                ("space", "Vũ trụ sâu", "skin-space.jpg"),
            };
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
                    RefreshSkinSelection();
                    ShowToast(sid == "default"
                        ? "Đã về nền mặc định (phẳng, trung tính)"
                        : "Đã đổi hình nền app: " + sname + " — panel kính cho nền xuyên qua");
                };
                _skinCards.Add((id, card));
                skinRow.Children.Add(card);
            }
            // The "Anh cua ban" (hien anh custom neu da co)
            {
                _customSkinThumb = new Avalonia.Controls.Image
                {
                    Height = 56, Stretch = Stretch.UniformToFill,
                };
                Control customPreview;
                string cp = BackgroundStore.ResolveCustomPath(_ui);
                if (cp != null)
                {
                    try { _customSkinThumb.Source = new Bitmap(cp); } catch { }
                    customPreview = _customSkinThumb;
                }
                else
                {
                    customPreview = new Border
                    {
                        Height = 56, CornerRadius = new CornerRadius(9),
                        Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
                        Child = Tb("＋ Ảnh riêng", null, 11.5).Also(t =>
                        {
                            t.HorizontalAlignment = HorizontalAlignment.Center;
                            t.VerticalAlignment = VerticalAlignment.Center;
                        }),
                    };
                }
                var previewHost = new Border { CornerRadius = new CornerRadius(9), ClipToBounds = true, Child = customPreview };
                var card = new Border
                {
                    Classes = { "pickcard" },
                    Child = V(6, previewHost, Tb("Ảnh của bạn", null, 11.5)),
                    Width = 118,
                    Margin = new Thickness(0, 0, 9, 9),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                card.PointerPressed += (s, e) =>
                {
                    if (BackgroundStore.ResolveCustomPath(_ui) != null)
                    {
                        ApplyBackground("custom");
                        RefreshSkinSelection();
                        ShowToast("Đã dùng ảnh của bạn làm nền app.");
                    }
                    else _ = ImportCustomBackgroundAsync();
                };
                _skinCards.Add(("custom", card));
                skinRow.Children.Add(card);
            }
            RefreshSkinSelection();

            var customBtn = GhostBtn("Ảnh của bạn…", async () => await ImportCustomBackgroundAsync());

            // --- Mau chu de (4 mau) ---
            var accentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
            var accentSwatches = new List<Border>();
            for (int i = 0; i < AccentTable.All.Length; i++)
            {
                var acc = AccentTable.All[i];
                var ca = Color.FromRgb(acc.A.R, acc.A.G, acc.A.B);
                var cb = Color.FromRgb(acc.B.R, acc.B.G, acc.B.B);
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
                ToolTip.SetTip(sw, acc.Name);
                int idx = i;
                sw.PointerPressed += (s, e) =>
                {
                    ApplyAccentByIndex(idx);
                    foreach (var o in accentSwatches)
                    {
                        bool sel = ReferenceEquals(o, sw);
                        o.BorderBrush = new SolidColorBrush(sel ? Colors.White : Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
                        o.BorderThickness = new Thickness(sel ? 2.2 : 1);
                    }
                    ShowToast("Đã áp dụng màu chủ đề: " + AccentTable.Get(idx).Name);
                };
                accentSwatches.Add(sw);
                accentRow.Children.Add(sw);
            }
            if (_ui.AccentIndex >= 0 && _ui.AccentIndex < accentSwatches.Count)
            {
                accentSwatches[_ui.AccentIndex].BorderBrush = new SolidColorBrush(Colors.White);
                accentSwatches[_ui.AccentIndex].BorderThickness = new Thickness(2.2);
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
                else
                {
                    card.Cursor = new Cursor(StandardCursorType.Hand);
                    card.PointerPressed += (s, e) =>
                        ShowToast("Chế độ Sáng đang được phát triển — sẽ có ở bản sau. Bản này giữ chế độ Tối như bản đã duyệt.");
                }
                return card;
            }
            var modeRow = H(10,
                ModePreview(true, "Tối", "đang dùng"),
                ModePreview(false, "Sáng", "sắp có ở bản sau"));

            // --- Man hinh chao ---
            var replayBtn = GhostBtn("Xem lại màn hình chào", () => ShowGreeting());
            _greetingStyleText = Tb("", "faint");
            RefreshGreetingStyleText();
            var skipSw = new PillSwitch();
            skipSw.IsChecked = _ui.SkipGreeting;
            skipSw.Changed += v => { _ui.SkipGreeting = v; SaveUi(); };
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
                Tb("Chọn nền có sẵn hoặc dùng ảnh của bạn — các panel kính mờ (Liquid Glass) sẽ cho nền xuyên qua. Ảnh riêng được nén (cạnh dài nhất 1920px) và lưu trong thư mục Backgrounds của app; mọi lựa chọn được nhớ trong settings.json.", "sub"),
                skinRow,
                customBtn,
                Tb("Màu chủ đề", "h3"),
                accentRow,
                Tb("Chế độ giao diện", "h3"),
                modeRow,
                Tb("Màn hình chào", "h3"),
                _greetingStyleText,
                H(10, replayBtn),
                skipRow));
        }

        // ================= v3.3: CHO PLUGIN =================
        // Trinh bay theo Preview v0.10.1 da duyet: luoi the 2 cot co anh bia,
        // tab danh muc, modal chi tiet o giua man hinh. Khac ban xem truoc:
        // cong tac Bat + cau hinh cua cac plugin KHOI DONG la THAT (quyet
        // dinh man hinh chao + luu settings.json). Hai plugin "Y tuong/dang
        // phat trien" duoc gan nhan ro rang, khoa cong tac, khong gia vo
        // chuc nang va khong co bat ky loi goi mang nao.
        private sealed class PluginItem
        {
            public string Id, Name, Author, Cat, CatLabel, Desc, Detail, Meta, Thumb;
            public bool BuiltIn;
            public bool Idea; // true = "Y tuong/dang phat trien": nhan ro, khoa bat
        }

        private static readonly Dictionary<string, Bitmap> ThumbCache = new Dictionary<string, Bitmap>();

        private static Bitmap LoadThumb(string file)
        {
            if (ThumbCache.TryGetValue(file, out var b)) return b;
            using (var s = AssetLoader.Open(GifTools.Asset("Assets/Plugins/" + file)))
            {
                var bmp = new Bitmap(s);
                ThumbCache[file] = bmp;
                return bmp;
            }
        }

        private static List<PluginItem> StoreItems() => new List<PluginItem>
        {
            new PluginItem
            {
                Id = "deepseek-harness",
                Name = "DeepSeek Harness Boot", Author = "Cộng đồng GitHub", Cat = "boot", CatLabel = "Hiệu ứng khởi động",
                Desc = "Màn hình chào kiểu terminal “AI harness”: chuỗi bước xác nhận nạp module, có đọc bằng giọng nói.",
                Detail = "Mỗi lần khởi động, app chạy chuỗi xác nhận từng bước (nạp module → kiểm tra thiết bị → sẵn sàng) trên màn hình chào kiểu terminal. Tên và mã định danh của bạn hiển thị thật trong chuỗi khởi động. Tuỳ chọn đọc bằng giọng nói đang được phát triển — cấu hình được lưu sẵn, giọng đọc sẽ có ở bản sau.",
                Meta = "Phiên bản 1.2.0 · Có sẵn trong app · Miễn phí", Thumb = "thumb-ds-harness.png",
            },
            new PluginItem
            {
                Id = "550c-boot",
                Name = "550C Boot", Author = "Cộng đồng GitHub", Cat = "boot", CatLabel = "Hiệu ứng khởi động",
                Desc = "Màn hình chào giả lập 550C: đồng hồ CRT màu hổ phách, kim quét một vòng rồi hiện logo.",
                Detail = "Bản cổng cộng đồng của hiệu ứng boot 550C: nền CRT hổ phách, vệt quét chạy ngang kèm phát sáng, kết thúc bằng logo app. Thời lượng ~2 giây, bỏ qua được bằng Esc.",
                Meta = "Phiên bản 1.0.3 · Có sẵn trong app · Miễn phí", Thumb = "thumb-550c.png",
            },
            new PluginItem
            {
                Id = "wallpaper-engine-sync",
                Name = "Wallpaper Engine Sync", Author = "Cộng đồng GitHub", Cat = "wallpaper", CatLabel = "Công cụ hình nền",
                Desc = "Dùng hình nền cực bá của Wallpaper Engine làm nền ứng dụng — không đổi hình nền Windows.",
                Detail = "Ý tưởng: công cụ nền đọc cảnh đang chạy của Wallpaper Engine và dùng khung hình của nó làm hình nền app. Plugin đang ở trạng thái Ý TƯỞNG / ĐANG PHÁT TRIỂN — chưa bật được, app không đọc dữ liệu của Wallpaper Engine và không gọi mạng cho mục này.",
                Meta = "Ý tưởng · Đang phát triển", Thumb = "thumb-we-sync.png", Idea = true,
            },
            new PluginItem
            {
                Id = "aurora-wave-pack",
                Name = "Aurora Wave Pack", Author = "T Keyboard Team", Cat = "led", CatLabel = "Hiệu ứng đèn",
                Desc = "Gói 6 hiệu ứng sóng đèn từng phím qua kênh stream: sóng ngang, sóng chéo, mưa sao, nhịp thở đôi…",
                Detail = "Ý tưởng: 6 hiệu ứng chạy bằng kênh stream từng phím của T Keyboard. Plugin đang ở trạng thái Ý TƯỞNG / ĐANG PHÁT TRIỂN — chưa bật được và chưa gửi bất kỳ gói tin nào xuống phím.",
                Meta = "Ý tưởng · Đang phát triển", Thumb = "thumb-aurora-wave.png", Idea = true,
            },
            new PluginItem
            {
                Id = "tien-galaxy-boot",
                Name = "Tien Galaxy Boot", Author = "T Keyboard Team", Cat = "boot", CatLabel = "Hiệu ứng khởi động",
                Desc = "Màn hình chào riêng của T Keyboard: vệt sáng cầu vồng quét qua hàng phím + logo Mẫu C.",
                Detail = "Đây là màn hình chào mặc định của app — vệt sáng quét ngang hàng phím trong ~2 giây, bấm hoặc nhấn Esc để bỏ qua. Xem lại bất cứ lúc nào ở Cài đặt → Giao diện.",
                Meta = "Phiên bản 1.0.0 · Có sẵn trong app · Miễn phí", Thumb = "thumb-tien-galaxy.png", BuiltIn = true,
            },
        };

        private static Border IdeaBadge() => new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xF9, 0x73, 0x16)),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(8, 3),
            Child = new TextBlock { Text = "Ý tưởng · đang phát triển", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xFD, 0xBA, 0x74)) },
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        private Border StoreCard(PluginItem it)
        {
            var thumbBox = new Border
            {
                Height = 148, ClipToBounds = true,
                CornerRadius = new CornerRadius(13, 13, 0, 0),
                Child = new Avalonia.Controls.Image { Source = LoadThumb(it.Thumb), Stretch = Stretch.UniformToFill },
            };
            var title = Tb(it.Name, "h3");
            title.FontSize = 13.5;
            var desc = new TextBlock
            {
                Text = it.Desc, FontSize = 12, Foreground = UiBrushes.TextSecondary,
                TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1,
            };
            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0x7C, 0x3A, 0xED)),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(8, 3),
                Child = new TextBlock { Text = it.CatLabel, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xB5, 0xFD)) },
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var srcLink = new TextBlock
            {
                Text = "Mã nguồn", FontSize = 11.5, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xE0, 0xF8)),
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand),
                VerticalAlignment = VerticalAlignment.Center,
            };
            srcLink.PointerPressed += (s, e) =>
            {
                e.Handled = true;
                ShowToast("Các hiệu ứng này được đóng gói sẵn trong app — app không tự tải hay cài thêm gì từ mạng.");
            };
            var author = Tb(it.Author, "faint");
            var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            footer.Children.Add(srcLink);
            Grid.SetColumn(author, 1);
            footer.Children.Add(author);

            var body = new StackPanel { Spacing = 7, Margin = new Thickness(13, 11, 13, 12) };
            body.Children.Add(title);
            body.Children.Add(desc);
            var pillRow = H(6, pill);
            if (it.Idea) pillRow.Children.Add(IdeaBadge());
            body.Children.Add(pillRow);
            body.Children.Add(footer);
            var stack = new StackPanel();
            stack.Children.Add(thumbBox);
            stack.Children.Add(body);
            var card = new Border
            {
                Classes = { "pickcard" },
                Padding = new Thickness(0),
                Child = stack,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            card.PointerPressed += (s, e) => OpenPluginModal(it);
            return card;
        }

        private Control BuildStorePage()
        {
            var items = StoreItems();
            var wrap = new WrapPanel();
            var cards = new List<Border>();
            for (int i = 0; i < items.Count; i++)
            {
                var card = StoreCard(items[i]);
                card.Width = 418;
                card.Margin = new Thickness(0, 0, i % 2 == 0 ? 10 : 0, 10);
                cards.Add(card);
                wrap.Children.Add(card);
            }

            var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var cats = new (string Label, string Cat)[]
            {
                ("Tất cả", "all"), ("Hiệu ứng khởi động", "boot"), ("Công cụ hình nền", "wallpaper"), ("Hiệu ứng đèn", "led"),
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
                Tb("Kho cộng đồng: hiệu ứng khởi động, công cụ hình nền và gói hiệu ứng đèn — bấm vào một thẻ để mở panel chi tiết: bật/tắt, cấu hình và xem trước.", "sub"),
                filterRow,
                wrap,
                CardFlat(Tb("Cơ chế thật của trang này: 3 plugin khởi động là tính năng THẬT của app — plugin đang bật sẽ quyết định màn hình chào mỗi lần mở app, cấu hình lưu trong settings.json. Hai mục gắn nhãn “Ý tưởng · đang phát triển” chỉ là ý tưởng đang làm — chưa bật được, không có chức năng ẩn và app không tải/cài bất cứ thứ gì từ mạng.", "faint")));
            return Scroll(root);
        }

        // ---------- MODAL CHI TIET PLUGIN ----------
        private void OpenPluginModal(PluginItem it)
        {
            PluginModalHost.Children.Clear();
            PluginModalHost.Children.Add(BuildPluginModalPanel(it));
            PluginModal.IsVisible = true;
            PluginModal.Opacity = 1;
        }

        private void ClosePluginModal()
        {
            if (!PluginModal.IsVisible) return;
            PluginModal.Opacity = 0;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
            t.Tick += (s, e) => { t.Stop(); PluginModal.IsVisible = false; };
            t.Start();
        }

        private Control BuildPluginModalPanel(PluginItem it)
        {
            var state = GetPluginState(it.Id);
            var title = Tb(it.Name, "h2");
            title.VerticalAlignment = VerticalAlignment.Center;
            var closeBtn = new Button { Content = "✕", FontSize = 13, Padding = new Thickness(10, 5) };
            closeBtn.Classes.Add("ghost");
            closeBtn.Click += (s, e) => ClosePluginModal();
            var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            head.Children.Add(title);
            Grid.SetColumn(closeBtn, 1);
            head.Children.Add(closeBtn);

            var preview = new Border
            {
                Height = 196, ClipToBounds = true, CornerRadius = new CornerRadius(12),
                Child = new Avalonia.Controls.Image { Source = LoadThumb(it.Thumb), Stretch = Stretch.UniformToFill },
            };

            var enableSw = new PillSwitch { IsChecked = state.Enabled, IsEnabled = !it.Idea };
            enableSw.Changed += v =>
            {
                state.Enabled = v;
                if (v && it.Cat == "boot")
                {
                    _ui.GreetingPlugin = it.Id;
                    ShowToast("Đã bật “" + it.Name + "” — màn hình chào lần mở app tới sẽ dùng phong cách này.");
                }
                else ShowToast(v ? "Đã bật “" + it.Name + "”" : "Đã tắt “" + it.Name + "”");
                SaveUi();
                RefreshGreetingStyleText();
            };
            var enableRow = H(10, Tb("Bật", "h3"), enableSw);
            enableRow.Children[0].VerticalAlignment = VerticalAlignment.Center;
            if (it.Idea) enableRow.Children.Add(Tb("Chưa bật được — đang phát triển", "faint"));

            var inner = new StackPanel { Spacing = 11, Margin = new Thickness(20, 16, 20, 18) };
            inner.Children.Add(head);
            inner.Children.Add(preview);
            inner.Children.Add(Tb(it.CatLabel + " · " + it.Author + " · " + it.Meta, "faint"));
            inner.Children.Add(Tb(it.Detail, "sub"));
            inner.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)) });
            inner.Children.Add(enableRow);
            foreach (var row in PluginConfigRows(it, state)) inner.Children.Add(row);
            inner.Children.Add(H(10,
                PrimaryBtn("Xem trước", () =>
                {
                    if (it.Cat == "boot") { ClosePluginModal(); ShowGreeting(GreetingStyles.StyleForPlugin(it.Id)); }
                    else ShowToast("Plugin đang ở trạng thái ý tưởng — chưa có bản xem trước thật.");
                }),
                GhostBtn("Đóng", () => ClosePluginModal())));

            return new Border
            {
                Classes = { "card" },
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x1B, 0x1B, 0x26)),
                Width = 560,
                Padding = new Thickness(0),
                Child = inner,
            };
        }

        private IEnumerable<Control> PluginConfigRows(PluginItem it, PluginState state)
        {
            var rows = new List<Control>();
            Control Row(string label, Control ctl)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("190,*") };
                var lb = Tb(label, "sub");
                lb.VerticalAlignment = VerticalAlignment.Center;
                g.Children.Add(lb);
                ctl.HorizontalAlignment = HorizontalAlignment.Left;
                ctl.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(ctl, 1);
                g.Children.Add(ctl);
                return g;
            }

            if (it.Idea)
            {
                rows.Add(Tb("Plugin đang ở trạng thái Ý TƯỞNG / ĐANG PHÁT TRIỂN — phần cấu hình sẽ mở khi plugin được phát triển xong. Hiện tại app không chạy bất kỳ mã nào của plugin này.", "faint"));
                return rows;
            }

            if (it.Cat == "boot")
            {
                var voice = new PillSwitch { IsChecked = state.GetCfgBool("voice", true) };
                voice.Changed += v =>
                {
                    state.Config["voice"] = v.ToString();
                    SaveUi();
                    if (v) ShowToast("Đã lưu tuỳ chọn giọng nói — tính năng đọc thành tiếng đang phát triển, sẽ có ở bản sau.");
                };
                rows.Add(Row("Đọc bằng giọng nói (đang phát triển)", voice));

                var volVal = Tb(state.GetCfgInt("volume", 90) + "%", "sub");
                var vol = new Slider { Minimum = 0, Maximum = 100, Value = state.GetCfgInt("volume", 90), Width = 220 };
                vol.PropertyChanged += (s, e) =>
                {
                    if (e.Property == Slider.ValueProperty)
                    {
                        volVal.Text = (int)vol.Value + "%";
                        state.Config["volume"] = ((int)vol.Value).ToString();
                        SaveUi();
                    }
                };
                rows.Add(Row("Âm lượng", H(10, vol, volVal)));

                var nameBox = new TextBox { Text = state.GetCfg("name", "T OPERATOR"), Width = 220 };
                nameBox.Classes.Add("search");
                nameBox.LostFocus += (s, e) => { state.Config["name"] = (nameBox.Text ?? "").Trim(); SaveUi(); };
                rows.Add(Row("Tên định danh", nameBox));

                var codeBox = new TextBox { Text = state.GetCfg("code", "TKB-001"), Width = 220 };
                codeBox.Classes.Add("search");
                codeBox.LostFocus += (s, e) => { state.Config["code"] = (codeBox.Text ?? "").Trim(); SaveUi(); };
                rows.Add(Row("Mã định danh", codeBox));

                if (it.Id == GreetingStyles.PluginDeepSeek)
                    rows.Add(Tb("Tên + mã định danh hiển thị thật trong chuỗi khởi động của màn chào terminal.", "faint"));
            }
            return rows;
        }

        // ================= v3.3: GAMESENSE (CS2 GSI — THAT) =================
        // CS2 tu gui trang thai tran ve 127.0.0.1:<port> qua file cfg ma app
        // tao (chuan mo Game State Integration cua Valve). App chi NHAN du
        // lieu game gui; khong doc bo nho game, khong inject, khong nghe
        // ngoai localhost. Phan ung den phim (neu bat) di qua dung
        // ExperimentalDevice voi cung khoa Thu nghiem + Dry-run nhu lenh
        // 0x20 o trang Per-key.
        private void StartGsi()
        {
            try { _gsi.Start(_ui.GameSense.Port); }
            catch (Exception ex)
            {
                AppendLog($"GameSense: không mở được cổng {_ui.GameSense.Port} — {ex.Message}");
                ShowToast("GameSense: không mở được cổng lắng nghe — " + ex.Message);
            }
            UpdateGsStatus();
        }

        private void UpdateGsStatus()
        {
            if (_gsDot == null) return;
            bool on = _gsi.Running;
            _gsDot.Fill = new SolidColorBrush(on ? Color.FromRgb(0x34, 0xD3, 0x99) : Color.FromRgb(0x8A, 0x87, 0xA0));
            _gsStatusText.Text = on
                ? $"Đang lắng nghe tại 127.0.0.1:{_gsi.Port} — chờ CS2 kết nối (cần file cfg + CS2 đang chạy)…"
                  + (_gsi.LastPayloadAt != DateTime.MinValue ? $" · Dữ liệu gần nhất: {_gsi.LastPayloadAt:HH:mm:ss} (CS2 đã kết nối)" : "")
                : "Đang tắt — bật công tắc để bắt đầu lắng nghe GSI trên localhost.";
        }

        private Control GsEventRow(string title, string desc, string[] actions, int initialAction, bool initialOn,
            Action<bool> onToggle, Action<int> onAction, Control extra = null)
        {
            var sw = new PillSwitch();
            sw.IsChecked = initialOn;
            sw.Changed += v => onToggle(v);
            var texts = V(2, Tb(title, "h3"), Tb(desc, "sub"));
            var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            top.Children.Add(texts);
            sw.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(sw, 1);
            top.Children.Add(sw);
            var (seg, _) = Segmented(actions, initialAction, i => onAction(i));
            var body = V(8, top, seg);
            if (extra != null) body.Children.Add(extra);
            var row = CardFlat(body, 12);
            row.Margin = new Thickness(0, 4);
            return row;
        }

        private Control BuildGameSensePage()
        {
            var gs = _ui.GameSense;
            _gsi.PayloadReceived += () => Dispatcher.UIThread.Post(UpdateGsStatus);

            // --- The trang thai dich vu GSI ---
            _gsDot = new EllipseShape { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.FromRgb(0x8A, 0x87, 0xA0)), VerticalAlignment = VerticalAlignment.Center };
            _gsStatusText = Tb("", "sub");
            var listenSw = new PillSwitch();
            listenSw.IsChecked = gs.Listening;
            listenSw.Changed += v =>
            {
                gs.Listening = v;
                SaveUi();
                if (v) StartGsi();
                else { _gsi.Stop(); UpdateGsStatus(); }
            };
            var listenTop = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            listenTop.Children.Add(V(2,
                Tb("Dịch vụ lắng nghe CS2 (GSI)", "h3"),
                Tb("CS2 tự gửi trạng thái trận đấu về máy bạn qua Game State Integration — chuẩn mở của Valve. App chỉ nhận dữ liệu game tự gửi trên localhost, không đọc bộ nhớ game.", "sub")));
            listenSw.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(listenSw, 1);
            listenTop.Children.Add(listenSw);
            var portBox = new TextBox { Text = gs.Port.ToString(), Width = 96 };
            portBox.Classes.Add("search");
            portBox.LostFocus += (s, e) =>
            {
                if (int.TryParse(portBox.Text, out var p) && p >= 1024 && p <= 65535)
                {
                    if (p != gs.Port)
                    {
                        gs.Port = p;
                        SaveUi();
                        if (_gsi.Running) StartGsi();
                        ShowToast("Đã đổi cổng GSI sang " + p + " — nhớ tạo lại file cfg cho CS2.");
                    }
                }
                else portBox.Text = gs.Port.ToString();
            };
            var cfgBtn = GhostBtn("Tạo file cfg cho CS2", async () => await GenerateCfgAsync());
            var statusRow = H(8, _gsDot, _gsStatusText);
            statusRow.Children[1].VerticalAlignment = VerticalAlignment.Center;
            _gsLastEvent = Tb("Chưa nhận sự kiện nào trong phiên này.", "faint");
            var serviceCard = Card(V(10,
                listenTop,
                H(10, Tb("Cổng lắng nghe:", "sub"), portBox, cfgBtn),
                statusRow,
                _gsLastEvent));

            // --- Extra: chet -> URL / chuong trinh ---
            var deathUrlBox = new TextBox { Text = gs.DeathUrl, Width = 380 };
            deathUrlBox.Classes.Add("search");
            deathUrlBox.LostFocus += (s, e) => { gs.DeathUrl = (deathUrlBox.Text ?? "").Trim(); SaveUi(); };
            var deathProgBox = new TextBox { Text = gs.DeathProgram, Width = 380 };
            deathProgBox.Classes.Add("search");
            deathProgBox.LostFocus += (s, e) => { gs.DeathProgram = (deathProgBox.Text ?? "").Trim(); SaveUi(); };
            var deathExtra = V(6,
                H(8, Tb("Trang web khi chết:", "sub"), deathUrlBox),
                H(8, Tb("Chương trình khi chết:", "sub"), deathProgBox,
                    GhostBtn("Chọn…", async () =>
                    {
                        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                        {
                            Title = "Chọn chương trình chạy khi chết",
                            AllowMultiple = false,
                            FileTypeFilter = new[] { new FilePickerFileType("Chương trình") { Patterns = new[] { "*.exe", "*.bat", "*.cmd", "*.lnk" } } },
                        });
                        if (files.Count > 0) { deathProgBox.Text = files[0].Path.LocalPath; gs.DeathProgram = deathProgBox.Text; SaveUi(); }
                    })));

            // --- Extra: flash -> anh phu + do mo ---
            var flashChips = new List<Button>();
            string FlashLabel()
                => gs.FlashImage == "black" ? "Đen tuyền" : gs.FlashImage == "gray" ? "Xám dịu" : "Ảnh của bạn";
            var flashState = Tb("Ảnh phủ hiện tại: " + FlashLabel(), "faint");
            void FlashChip(string label, string spec)
            {
                var b = new Button { Content = label };
                b.Classes.Add("chip");
                if (gs.FlashImage == spec) b.Classes.Add("sel");
                b.Click += (s, e) =>
                {
                    gs.FlashImage = spec;
                    SaveUi();
                    foreach (var o in flashChips) o.Classes.Remove("sel");
                    b.Classes.Add("sel");
                    flashState.Text = "Ảnh phủ hiện tại: " + FlashLabel();
                };
                flashChips.Add(b);
            }
            FlashChip("Đen tuyền", "black");
            FlashChip("Xám dịu", "gray");
            var flashCustomBtn = new Button { Content = "Ảnh của bạn…" };
            flashCustomBtn.Classes.Add("chip");
            if (gs.FlashImage != "black" && gs.FlashImage != "gray") flashCustomBtn.Classes.Add("sel");
            flashCustomBtn.Click += async (s, e) =>
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Chọn ảnh phủ chống loá",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { new FilePickerFileType("Ảnh") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp" } } },
                });
                if (files.Count == 0) return;
                try
                {
                    string dir = System.IO.Path.Combine(GifLibraryStore.RootDir, "GameSense");
                    Directory.CreateDirectory(dir);
                    string dest = System.IO.Path.Combine(dir, "flash-custom" + System.IO.Path.GetExtension(files[0].Path.LocalPath));
                    File.Copy(files[0].Path.LocalPath, dest, true);
                    gs.FlashImage = dest;
                    SaveUi();
                    foreach (var o in flashChips) o.Classes.Remove("sel");
                    flashCustomBtn.Classes.Add("sel");
                    flashState.Text = "Ảnh phủ hiện tại: " + FlashLabel();
                }
                catch (Exception ex) { ShowToast("Không dùng được ảnh này: " + ex.Message); }
            };
            flashChips.Add(flashCustomBtn);
            var flashOpacityVal = Tb((int)(gs.FlashOpacity * 100) + "%", "sub");
            var flashOpacity = new Slider { Minimum = 0.3, Maximum = 1, Value = gs.FlashOpacity, Width = 200 };
            flashOpacity.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                {
                    gs.FlashOpacity = flashOpacity.Value;
                    flashOpacityVal.Text = (int)(flashOpacity.Value * 100) + "%";
                    SaveUi();
                }
            };
            var flashChipRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var c in flashChips) flashChipRow.Children.Add(c);
            var flashExtra = V(6,
                H(8, Tb("Ảnh phủ chống loá:", "sub"), flashChipRow),
                flashState,
                H(10, Tb("Độ mờ lớp phủ:", "sub"), flashOpacity, flashOpacityVal));

            // --- Anh xa su kien -> hanh dong ---
            var mapCard = Card(V(6,
                Tb("Ánh xạ sự kiện → hành động", "h2"),
                Tb("Mỗi sự kiện trong trận có thể kích hoạt một hành động trên máy — và (chỉ có ở T Keyboard) cả phản ứng bằng đèn phím qua lớp thử nghiệm.", "sub"),
                GsEventRow("Khi bạn chết",
                    "Sự kiện: player_state.health về 0",
                    new[] { "Mở trang web", "Chạy chương trình", "Nháy đèn phím" }, gs.DeathAction, gs.DeathEnabled,
                    v => { gs.DeathEnabled = v; SaveUi(); }, i => { gs.DeathAction = i; SaveUi(); }, deathExtra),
                GsEventRow("Khi bị flashbang",
                    "Sự kiện: player_state.flashed tăng vọt — hiện lớp phủ bảo vệ mắt toàn màn hình",
                    new[] { "Overlay chống loá", "Làm tối đèn", "Không làm gì" }, gs.FlashAction, gs.FlashEnabled,
                    v => { gs.FlashEnabled = v; SaveUi(); }, i => { gs.FlashAction = i; SaveUi(); }, flashExtra),
                GsEventRow("Khi thắng round",
                    "Sự kiện: round kết thúc và đội của bạn thắng",
                    new[] { "Đèn ăn mừng (stream)", "Mở trang web", "Không làm gì" }, gs.WinAction, gs.WinEnabled,
                    v => { gs.WinEnabled = v; SaveUi(); }, i => { gs.WinAction = i; SaveUi(); }),
                GsEventRow("Khi đặt bom",
                    "Sự kiện: bomb.state = planted",
                    new[] { "Nháy đèn phím", "Không làm gì" }, gs.BombAction, gs.BombEnabled,
                    v => { gs.BombEnabled = v; SaveUi(); }, i => { gs.BombAction = i; SaveUi(); },
                    Tb("Đèn đếm ngược theo nhịp bom: ĐANG PHÁT TRIỂN — hiện tại sự kiện đặt bom chỉ nháy đèn một nhịp (nếu chọn) và ghi log.", "faint")),
                SettingRow("Phản ứng bằng đèn phím (lớp thử nghiệm)", "Mọi phản ứng đèn của GameSense đều đi qua ExperimentalDevice — chỉ gửi thật khi công tắc Thử nghiệm ở trang Per-key BẬT và Dry-run TẮT; ngoài ra chỉ ghi log gói sẽ gửi.", gs.LightingEnabled,
                    v => { gs.LightingEnabled = v; SaveUi(); })));

            // --- Diem hon ban goc ---
            var plusCard = Card(V(8,
                Tb("Điểm hơn bản gốc", "h2"),
                Tb("Ở app tham khảo, GameSense chỉ mở trang web / chạy chương trình khi chết và phủ overlay chống loá khi bị flash — KHÔNG có phản ứng bằng đèn theo game. Ở T Keyboard, cùng sự kiện đó còn đẩy xuống ĐÈN PHÍM qua kênh stream của chính app (qua lớp thử nghiệm có khoá an toàn).", "sub")));
            plusCard.BorderBrush = UiBrushes.AccentGradient();
            plusCard.BorderThickness = new Thickness(1.6);

            // --- File cfg mau (sinh that theo cong hien tai) ---
            var cfgText = new TextBlock
            {
                Text = GsiConfig.BuildCfg(gs.Port, GsiConfig.Token),
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xD5, 0xE6)),
            };
            var cfgCard = Card(V(8, Tb("File cfg (game tự gửi dữ liệu về)", "h3"), cfgText,
                Tb("Nút “Tạo file cfg cho CS2” ở trên sẽ ghi đúng nội dung này vào thư mục game\\csgo\\cfg của CS2 (tự tìm qua Steam) hoặc cho bạn tự chọn nơi lưu.", "faint")));

            UpdateGsStatus();
            var root = V(14,
                Tb("GameSense", "h1"),
                Tb("Phản ứng theo sự kiện trong game CS2: chết thì mở sẵn trang web/chương trình, bị flash thì có lớp phủ chống loá — và ở app mình, thêm cả phản ứng bằng đèn phím.", "sub"),
                serviceCard,
                mapCard,
                plusCard,
                cfgCard);
            return Scroll(root);
        }

        private async Task GenerateCfgAsync()
        {
            string content = GsiConfig.BuildCfg(_ui.GameSense.Port, GsiConfig.Token);
            string dir = Cs2Locator.FindCfgDir();
            if (dir != null)
            {
                try
                {
                    string path = System.IO.Path.Combine(dir, GsiConfig.CfgFileName);
                    File.WriteAllText(path, content);
                    AppendLog("GameSense: đã ghi file cfg vào " + path);
                    ShowToast("Đã ghi file cfg vào thư mục CS2: " + path + " — khởi động lại CS2 để game bắt đầu gửi dữ liệu.");
                    return;
                }
                catch (Exception ex) { AppendLog("GameSense: ghi cfg vào thư mục CS2 thất bại: " + ex.Message); }
            }
            try
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Lưu file cfg GSI — hãy đặt vào thư mục game\\csgo\\cfg của CS2",
                    SuggestedFileName = GsiConfig.CfgFileName,
                    DefaultExtension = "cfg",
                    FileTypeChoices = new[] { new FilePickerFileType("File cfg") { Patterns = new[] { "*.cfg" } } },
                });
                if (file != null)
                {
                    File.WriteAllText(file.Path.LocalPath, content);
                    AppendLog("GameSense: đã lưu file cfg tại " + file.Path.LocalPath);
                    ShowToast("Đã lưu file cfg. Hãy đặt nó vào thư mục game\\csgo\\cfg của CS2 rồi khởi động lại game.");
                }
                else AppendLog("GameSense: không tự tìm thấy thư mục CS2 trên máy này và bạn đã huỷ lưu file cfg.");
            }
            catch (Exception ex)
            {
                AppendLog("GameSense: lỗi tạo file cfg: " + ex.Message);
                ShowToast("Không tạo được file cfg: " + ex.Message);
            }
        }

        // ---------- Xu ly su kien GSI (tren UI thread) ----------
        private void HandleGsiEvent(GsiEventKind ev)
        {
            var gs = _ui.GameSense;
            string label = ev == GsiEventKind.Death ? "Bạn vừa chết trong game"
                         : ev == GsiEventKind.FlashStart ? "Bị flashbang (loá mạnh)"
                         : ev == GsiEventKind.FlashEnd ? "Hết loá"
                         : ev == GsiEventKind.RoundWin ? "Thắng round!"
                         : "Bom đã được đặt";
            if (_gsLastEvent != null)
                _gsLastEvent.Text = $"Sự kiện gần nhất: {label} · {DateTime.Now:HH:mm:ss}";
            AppendLog("GameSense: " + label + ".");

            switch (ev)
            {
                case GsiEventKind.Death:
                    if (!gs.DeathEnabled) break;
                    if (gs.DeathAction == 0) OpenUrlSafe(gs.DeathUrl);
                    else if (gs.DeathAction == 1) RunProgramSafe(gs.DeathProgram);
                    else SendGsStream("GameSense: nháy đỏ khi chết", AllKeysColor(0xE0, 0x10, 0x10), restoreAfter: true);
                    break;
                case GsiEventKind.FlashStart:
                    if (!gs.FlashEnabled) break;
                    if (gs.FlashAction == 0) ShowFlashOverlay();
                    else if (gs.FlashAction == 1)
                    {
                        SendGsStream("GameSense: làm tối đèn khi bị flash", AllKeysColor(0, 0, 0), restoreAfter: false);
                        _flashDimApplied = gs.LightingEnabled;
                    }
                    break;
                case GsiEventKind.FlashEnd:
                    HideFlashOverlay();
                    if (_flashDimApplied)
                    {
                        _flashDimApplied = false;
                        _ = ApplyRgbAsync(_rgb.Effect, _rgb.Brush, _rgb.Multi, (int)_rgb.Brightness, (int)_rgb.Speed, _rgb.Direction);
                    }
                    break;
                case GsiEventKind.RoundWin:
                    if (!gs.WinEnabled) break;
                    if (gs.WinAction == 0) CelebrateLighting();
                    else if (gs.WinAction == 1) OpenUrlSafe(gs.WinUrl);
                    break;
                case GsiEventKind.BombPlanted:
                    if (gs.BombEnabled && gs.BombAction == 0)
                        SendGsStream("GameSense: nháy cam khi đặt bom", AllKeysColor(0xF9, 0x73, 0x16), restoreAfter: true);
                    break;
            }
        }

        private void OpenUrlSafe(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) { AppendLog("GameSense: chưa cấu hình trang web để mở."); return; }
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    AppendLog("GameSense: URL không hợp lệ (chỉ http/https): " + url);
                    return;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                AppendLog("GameSense: đã mở trang web " + url);
            }
            catch (Exception ex) { AppendLog("GameSense: mở web lỗi: " + ex.Message); }
        }

        private void RunProgramSafe(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    AppendLog("GameSense: chưa cấu hình chương trình hợp lệ để chạy khi chết.");
                    return;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                AppendLog("GameSense: đã chạy chương trình " + path);
            }
            catch (Exception ex) { AppendLog("GameSense: chạy chương trình lỗi: " + ex.Message); }
        }

        private FlashOverlay GetFlashOverlay()
        {
            if (_flashOverlay == null)
            {
                _flashOverlay = new FlashOverlay();
                _flashOverlay.Closed += (s, e) => _flashOverlay = null;
            }
            return _flashOverlay;
        }

        private void ShowFlashOverlay()
        {
            try
            {
                var gs = _ui.GameSense;
                string spec = gs.FlashImage == "black" || gs.FlashImage == "gray" ? gs.FlashImage : gs.FlashImage;
                GetFlashOverlay().ShowOverlay(spec, gs.FlashOpacity);
                AppendLog("GameSense: đã hiện lớp phủ chống loá.");
            }
            catch (Exception ex) { AppendLog("GameSense: lỗi lớp phủ: " + ex.Message); }
        }

        private void HideFlashOverlay()
        {
            try { _flashOverlay?.HideOverlay(); } catch { }
        }

        private static List<L99Protocol.StreamQuad> AllKeysColor(byte r, byte g, byte b)
            => MaxKeyData.StreamSlots.Select(s => new L99Protocol.StreamQuad(s.Id, r, g, b)).ToList();

        // Gui mot frame stream phan ung game qua ExperimentalDevice (cung khoa
        // Thu nghiem + Dry-run nhu trang Per-key). restoreAfter: gui xong thi
        // tra lai hieu ung RGB hien tai bang kenh da kiem chung 04 13.
        private void SendGsStream(string title, List<L99Protocol.StreamQuad> quads, bool restoreAfter)
        {
            if (!_ui.GameSense.LightingEnabled)
            {
                AppendLog("GameSense: phản ứng đèn đang TẮT trong cài đặt GameSense — bỏ qua gửi đèn.");
                return;
            }
            var steps = L99Protocol.BuildStreamFrameSteps(quads);
            _ = Task.Run(() =>
            {
                try
                {
                    bool sent = _exp.SendSteps(title, steps);
                    if (sent && restoreAfter)
                    {
                        Thread.Sleep(420);
                        Dispatcher.UIThread.Post(() =>
                            _ = ApplyRgbAsync(_rgb.Effect, _rgb.Brush, _rgb.Multi, (int)_rgb.Brightness, (int)_rgb.Speed, _rgb.Direction));
                    }
                }
                catch (Exception ex) { AppendLog("GameSense: lỗi gửi đèn: " + ex.Message); }
            });
        }

        private void CelebrateLighting()
        {
            if (!_ui.GameSense.LightingEnabled)
            {
                AppendLog("GameSense: phản ứng đèn đang TẮT trong cài đặt GameSense — bỏ qua đèn ăn mừng.");
                return;
            }
            _ = Task.Run(() =>
            {
                try
                {
                    var hues = new (byte, byte, byte)[] { (0x7C, 0x3A, 0xED), (0x22, 0xD3, 0xEE), (0x34, 0xD3, 0x99) };
                    bool anySent = false;
                    for (int i = 0; i < hues.Length; i++)
                    {
                        var steps = L99Protocol.BuildStreamFrameSteps(AllKeysColor(hues[i].Item1, hues[i].Item2, hues[i].Item3));
                        anySent = _exp.SendSteps("GameSense: đèn ăn mừng thắng round", steps) || anySent;
                        Thread.Sleep(260);
                    }
                    if (anySent)
                        Dispatcher.UIThread.Post(() =>
                            _ = ApplyRgbAsync(_rgb.Effect, _rgb.Brush, _rgb.Multi, (int)_rgb.Brightness, (int)_rgb.Speed, _rgb.Direction));
                }
                catch (Exception ex) { AppendLog("GameSense: lỗi đèn ăn mừng: " + ex.Message); }
            });
        }

        // ================= Khay he thong =================
        private void SetupTray()
        {
            try
            {
                var icon = new WindowIcon(AssetLoader.Open(GifTools.Asset("Assets/logo.png")));
                var tray = new TrayIcon { Icon = icon, ToolTipText = "T Keyboard Studio" };
                var menu = new NativeMenu();
                var openItem = new NativeMenuItem("Mở T Keyboard Studio");
                openItem.Click += (s, e) => { Show(); Activate(); };
                var exitItem = new NativeMenuItem("Thoát");
                exitItem.Click += (s, e) => { _reallyExit = true; Close(); };
                menu.Items.Add(openItem);
                menu.Items.Add(exitItem);
                tray.Menu = menu;
                tray.Clicked += (s, e) => { Show(); Activate(); };
                if (Application.Current != null)
                    TrayIcon.SetIcons(Application.Current, new TrayIcons { tray });
            }
            catch { /* khay la phu — loi thi bo qua */ }
        }
    }

    internal static class ControlExtensions
    {
        public static T Also<T>(this T control, Action<T> action) where T : Control
        {
            action(control);
            return control;
        }
    }
}
