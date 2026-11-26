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
using PathShape = Avalonia.Controls.Shapes.Path;

namespace TienKeyboardStudio3
{
    public partial class MainWindow : Window
    {
        // ================= Trang thai dung chung =================
        private readonly F75Device _device = new F75Device();
        private readonly ExperimentalDevice _exp = new ExperimentalDevice();
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

        public MainWindow()
        {
            InitializeComponent();
            Toast.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(240) } };

            _device.Log = AppendLog;
            _exp.Log = AppendLog;
            _device.DryRun = true;
            _exp.DryRun = true;

            DryToggle.IsCheckedChanged += (s, e) => SetDryRun(DryToggle.IsChecked == true, fromUi: true);

            BuildNav();
            _pages["home"] = BuildHomePage();
            _pages["rgb"] = BuildRgbPage();
            _pages["music"] = BuildMusicPage();
            _pages["gif"] = BuildGifPage();
            _pages["online"] = BuildOnlinePage();
            _pages["perkey"] = BuildPerKeyPage();
            _pages["widget"] = BuildWidgetPage();
            _pages["settings"] = BuildSettingsPage();
            _pages["phone"] = BuildPhonePage();
            foreach (var p in _pages.Values) { p.IsVisible = false; PageHost.Children.Add(p); }

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
            AppendLog("T Keyboard Studio v3.2.2 đã mở. Dry-run ĐANG BẬT — mọi thao tác chỉ ghi log, không ghi xuống phím.");
            AppendLog("Lộ trình an toàn lần đầu: Quét thiết bị → thử RGB (dry-run) → tắt Dry-run → RGB thật → Cài đặt 04 17 → Đồng hồ → GIF TFT nhỏ → Đọc thử 0xF5.");
        }

        private void Cleanup()
        {
            try { _phone.Stop(); } catch { }
            try { _audio.Stop(); } catch { }
            try { _musicSendCts?.Cancel(); } catch { }
            try { _profiles.Stop(); } catch { }
            try { _typing.Stop(); } catch { }
            try { _macros.Dispose(); } catch { }
            try { _exp.Dispose(); } catch { }
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
                ("gif", "Màn hình GIF", "M4 4.5 A1.5 1.5 0 0 1 5.5 3h13A1.5 1.5 0 0 1 20 4.5v15a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 19.5Z M8.3 10.2 a1.4 1.4 0 1 0 .1 0 M20 16.5 15.5 12 5 21"),
                ("online", "Thư viện Online", "M12 12 m-9.5 0 a9.5 9.5 0 1 0 19 0 a9.5 9.5 0 1 0-19 0 M2.5 12h19 M12 2.5c2.9 2.7 4.4 5.9 4.4 9.5s-1.5 6.8-4.4 9.5c-2.9-2.7-4.4-5.9-4.4-9.5S9.1 5.2 12 2.5Z"),
                ("perkey", "Per-key (thử nghiệm)", "M4 4h4v4H4z M10 4h4v4h-4z M16 4h4v4h-4z M4 10h4v4H4z M10 10h4v4h-4z M16 10h4v4h-4z M4 16h4v4H4z M10 16h4v4h-4z M16 16h4v4h-4z"),
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
                btn.IsVisible = show;
            }
            if (!_pages.ContainsKey(_currentPage)) return;
            bool curHidden = (_currentPage == "gif" || _currentPage == "widget") && !_selectedModel.HasScreen
                             || _currentPage == "perkey" && !_selectedModel.FullSupport;
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
            if (key == "settings") { UpdateClockLabels(); RefreshTypingLabels(); }
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
                Tb("T Keyboard Studio v3.2.2 — bản thật: quét thiết bị HID thật, ghi xuống F75 Max qua giao thức đã kiểm chứng. Dry-run bật mặc định để an toàn.", "sub"),
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
                    _detectedPanel.Children.Add(Tb($"Lưu ý: VID {g.Key.Vid:X4}:{g.Key.Pid:X4} được dùng chung bởi {string.Join(" và ", g.Select(x => x.Model.Name))} — app sẽ không ghi xuống các mẫu này cho tới khi tích hợp driver riêng.", "faint"));
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
                : $"{model.Name}: chế độ XEM TRƯỚC — giao diện/hiệu ứng xem được, tính năng ghi xuống phím đang khoá vì driver cho mẫu này chưa tích hợp.");
            if ((_currentPage == "gif" || _currentPage == "widget") && !model.HasScreen) ShowPage("home");
            if (_currentPage == "perkey" && !model.FullSupport) ShowPage("home");
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
                Tb("Phiên bản 3.2.2 — bản thật: F75 Max hỗ trợ đầy đủ qua giao thức đã kiểm chứng; lớp per-key/stream/nhạc là thử nghiệm có khoá an toàn (xem trang Per-key). Dry-run bật mặc định.", "sub"),
                Tb("Logo Mẫu C — chữ “T” tạo hình từ phím spacebar — đã được người dùng duyệt.", "sub"),
                Tb("Font chữ: Be Vietnam Pro (SIL OFL 1.1). GIF thư viện: tự làm cho Tiến. Giao thức per-key: Gavin Graham (AULA L99).", "faint"));
            var aboutCard = Card(H(14, logoWrap, aboutTexts));

            var root = V(14,
                Tb("Đồng hồ & Cài đặt", "h1"),
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
