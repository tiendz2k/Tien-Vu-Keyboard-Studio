using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
        // v3.5 Driver Pack 2: nen tang RongYuan (Akko/MonsGeek/EPOMAKER)
        private readonly RongYuanDevice _ry = new RongYuanDevice();
        private TextBlock _ryIdStatus;
        private TextBlock _ryInfoStatus;
        private TextBlock _ryModelInfo;
        private TextBlock _ryCalStatus;
        private Border _ryHeCard;
        private Border _ryScreenCard;
        private int _ryScale = RongYuanHe.DefaultScale;
        // v3.6 Driver Pack 3: MCHOSE (stack GLW/CZ + QHW; ho BY dung chung Pack 1)
        private readonly MchoseDevice _mc = new MchoseDevice();
        private TextBlock _mcIdStatus;
        private TextBlock _mcInfoStatus;
        private TextBlock _mcModelInfo;
        private Border _mcHeCard;
        private Border _mcQhwCard;
        private int _mcProfile;
        // v3.7 Driver Pack 4: cac ho anh em SONiX (B S75/Nova, C MAD60, D HERO68, E WIN60 HE, F2 WIN60 SE)
        private readonly SonixSiblingsDevice _sx = new SonixSiblingsDevice();
        private TextBlock _sxIdStatus;
        private TextBlock _sxInfoStatus;
        private Border _sxCardB;
        private Border _sxCardC;
        private Border _sxCardD;
        private Border _sxCardE;
        private Button _sxBStreamBtn; // v3.14: an voi Nova75 (trinh ghi frame rieng chua ro)
        // v3.7 Driver Pack 4: nen tang WitMod (Redragon K617 HE...)
        private readonly WitModDevice _wm = new WitModDevice();
        private TextBlock _wmIdStatus;
        private TextBlock _wmInfoStatus;
        // v3.9 Driver Pack 5: ho DareU (TG/TG-RT/OHID/JM/QF — dinh danh bang registry PID)
        private readonly DareUDevice _du = new DareUDevice();
        private TextBlock _duIdStatus;
        private TextBlock _duInfoStatus;
        private TextBlock _duModelInfo;
        private Border _duHeCard;
        private Border _duOhidCard;
        private Border _duJmCard;
        // v3.9 Driver Pack 5: cac ho AULA Hub con lai (HFD / SparkLink V1 / SparkLink V2)
        private readonly AulaHubDevice _ah = new AulaHubDevice();
        private TextBlock _ahIdStatus;
        private TextBlock _ahInfoStatus;
        private TextBlock _ahModelInfo;
        private Border _ahHfdCard;
        private Border _ahSlV1Card;
        private Border _ahSlV2Card;
        private TextBlock _regStatus;
        private bool _busy;
        private KeyboardModel _selectedModel = ModelDatabase.F75Max;
        private List<DetectedDevice> _detected = new List<DetectedDevice>();

        private readonly RgbAnimState _rgb = new RgbAnimState { Effect = 10, Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3, Speed = 2, Direction = 0 };
        private readonly RgbAnimState _music = new RgbAnimState { Effect = 101, Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3, Speed = 2, MusicGain = 1.0, MusicBg = 1 };
        private readonly AudioCapture _audio = new AudioCapture();
        private bool _musicPlaying;
        private int _musicMode; // 0 visualizer stream, 1 nhip firmware, 2 feed 0x78
        private CancellationTokenSource _musicSendCts;
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
        // v3.9.1: hai the chua panel nhat ky — an mac dinh theo cai dat
        // ShowOperationLog (chi an HIEN THI; _logLines van thu thap lien tuc).
        private Border _homeLogCard;
        private Border _expLogCard;

        // Trang chu
        private Border _integrityBanner;
        private TextBlock _homeFxValue;
        private TextBlock _homeGifValue;
        private TextBlock _homeVerValue;
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
        private TextBlock _widgetWeatherDesc; // v3.11.1: mo ta the "Dong ho & thoi tiet" (theo noi dang dung)
        private bool _widgetAuto;
        private int _widgetTickCount;
        private ProgressBar _widgetProgress;

        // Trang Cai dat
        private TextBlock _clockBig;
        private TextBlock _clockDate;

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
        private MossSceneControl _mossCtrl;
        private ParticleMorphControl _particleCtrl;
        private ReticleControl _reticle;
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
            _ry.Log = AppendLog;
            _mc.Log = AppendLog;
            _sx.Log = AppendLog;
            _wm.Log = AppendLog;
            _du.Log = AppendLog;
            _ah.Log = AppendLog;
            // v3.14 (ban hoan thien): khong con Dry-run / cong tac thu nghiem
            // cho nguoi dung. Cac lop thiet bi cu con giu hai co noi bo —
            // app gan co dinh o che do GUI THAT ngay tu khoi tao (duong ong
            // noi bo, khong phai cong tac); viec ghi van chi dien ra khi
            // thiet bi da duoc dinh danh bang probe doc nhu truoc.
            _device.DryRun = false;
            _kb.DryRun = false; _kb.ExperimentalEnabled = true;
            _ry.DryRun = false; _ry.ExperimentalEnabled = true;
            _mc.DryRun = false; _mc.ExperimentalEnabled = true;
            _sx.DryRun = false; _sx.ExperimentalEnabled = true;
            _wm.DryRun = false; _wm.ExperimentalEnabled = true;
            _du.DryRun = false; _du.ExperimentalEnabled = true;
            _ah.DryRun = false; _ah.ExperimentalEnabled = true;
            // v3.7: ap dung bang thiet bi cong dong da tai ve (neu co va hop
            // le) — chi BO SUNG mau moi, khong ghi de bang tich hop.
            try
            {
                int addedReg = DeviceRegistry.ApplyTo(ModelDatabase.All);
                if (addedReg > 0)
                    AppendLog($"Bảng thiết bị cộng đồng: đã bổ sung {addedReg} mẫu từ file đã tải về trước đó.");
            }
            catch { }

            // v3.3: hieu ung mo cho modal plugin + man hinh chao
            GreetingOverlay.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) } };
            SettingsOverlay.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) } };
            PluginModal.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) } };
            PluginModalDim.PointerPressed += (s, e) => ClosePluginModal();
            GreetingOverlay.PointerPressed += (s, e) => DismissGreeting();
            KeyDown += (s, e) =>
            {
                if (e.Key != Key.Escape) return;
                if (GreetingOverlay.IsVisible) DismissGreeting();
                else if (PluginModal.IsVisible) ClosePluginModal();
                else if (SettingsOverlay.IsVisible) CloseSettings();
            };
            _gsi.Log = msg => AppendLog(msg);
            _gsi.EventReceived += (ev, payload) => Dispatcher.UIThread.Post(() => HandleGsiEvent(ev));

            RestoreLightingState(); // v3.12: truoc khi dung trang (trang doc _rgb/_painted)
            // v3.15: hook vi tri phim -> engine go phim phan ung. Hook
            // chi chay khi cong tac Thong ke go phim dang bat (opt-in
            // dung chung) nen ngoai luc do khong co su kien nao ca.
            _typing.KeyPositionPressed += view => _reactivePlayer.OnKey(view);
            InitSchedule(); // v3.15: lich trinh den (baseline, khong gui ngay)
            BuildNav();
            _pages["home"] = BuildHomePage();
            _pages["rgb"] = BuildRgbPage();
            _pages["music"] = BuildMusicPage();
            _pages["gamesense"] = BuildGameSensePage();
            _pages["gif"] = BuildGifPage();
            _pages["online"] = BuildOnlinePage();
            _pages["perkey"] = BuildPerKeyPage();
            _pages["kbbyv2"] = BuildKbByV2Page();
            _pages["rongyuan"] = BuildRongYuanPage();
            _pages["mchose"] = BuildMchosePage();
            _pages["sonix"] = BuildSonixPage();
            _pages["witmod"] = BuildWitModPage();
            _pages["dareu"] = BuildDareUPage();
            _pages["aulahub"] = BuildAulaHubPage();
            _pages["store"] = BuildStorePage();
            _pages["widget"] = BuildWidgetPage();
            _pages["phone"] = BuildPhonePage();
            // v3.11: Cai dat khong con la mot TRANG — la PANEL PHU kieu AULA Hub
            // (mini-sidebar rieng) + thanh tren cung, dung sau khi cac trang co san.
            BuildTopBar();
            BuildSettingsOverlay();
            foreach (var p in _pages.Values) { p.IsVisible = false; PageHost.Children.Add(p); }

            // v3.11: ap dung giao dien da luu — o mau chu de (ke ca chrome
            // "Nguyen ban"), hinh nen (gom ca nen dong) va do mo kinh.
            ApplyThemeTile(ThemeTiles.ResolveId(_ui), save: false);
            ApplyBackground(_ui.Background, save: false);
            ApplyGlass(_ui.GlassBlur, save: false);
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

            // v3.11: nen dong tu tam dung khi cua so mat focus (tiet kiem CPU)
            Activated += (s, e) => UpdateLivePlayback();
            Deactivated += (s, e) => UpdateLivePlayback();
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
            _ = RunIntegrityCheckAsync();
            AppendLog($"Tien Vu Keyboard Studio v{AboutInfo.Version} đã mở. Mọi thao tác áp dụng trực tiếp xuống phím qua USB-C.");
            AppendLog("Lần đầu: Quét thiết bị → chọn đúng mẫu → dùng các trang tính năng; các họ khác cần bấm Nhận diện (probe đọc) trước khi ghi.");
            _ = SilentAppUpdateCheckAsync();
            // v3.17 (scope C): doc ghi chu ket qua cap nhat tu dong do
            // helper ghi lai (neu lan chay truoc vua cap nhat).
            _ = ShowPendingUpdateResultAsync();
        }

        // v3.8: tu kiem tra toan ven ban phat hanh (khong chan tinh nang)
        private async Task RunIntegrityCheckAsync()
        {
            try
            {
                var manifest = IntegrityCheck.LoadEmbedded();
                if (manifest == null) return;
                var bad = await Task.Run(() => IntegrityCheck.Verify(AppContext.BaseDirectory, manifest));
                if (bad.Count > 0)
                {
                    if (_integrityBanner != null) _integrityBanner.IsVisible = true;
                    AppendLog("Kiểm tra toàn vẹn: phát hiện file lệch băm so với bản gốc (" +
                              string.Join(", ", bad) + "). " + IntegrityCheck.TamperWarning);
                }
            }
            catch { }
        }

        private void Cleanup()
        {
            try { _liveTimer?.Stop(); } catch { }
            try { _gsi.Stop(); } catch { }
            try { _flashOverlay?.Close(); } catch { }
            try { _phone.Stop(); } catch { }
            try { _audio.Stop(); } catch { }
            try { _musicSendCts?.Cancel(); } catch { }
            try { _rtPlayer.Stop(); } catch { }
            try { _profiles.Stop(); } catch { }
            try { _typing.Stop(); } catch { }
            try { _macros.Dispose(); } catch { }
            try { _exp.Dispose(); } catch { }
            try { _kb.Dispose(); } catch { }
            try { _ry.Dispose(); } catch { }
            try { _mc.Dispose(); } catch { }
            try { _sx.Dispose(); } catch { }
            try { _wm.Dispose(); } catch { }
            try { _du.Dispose(); } catch { }
            try { _ah.Dispose(); } catch { }
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
            var row = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = double.NaN, ItemHeight = double.NaN };
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
                ("perkey", "Per-key", "M4 4h4v4H4z M10 4h4v4h-4z M16 4h4v4h-4z M4 10h4v4H4z M10 10h4v4h-4z M16 10h4v4h-4z M4 16h4v4H4z M10 16h4v4h-4z M16 16h4v4h-4z"),
                ("kbbyv2", "Họ Sinowealth (kb_by_v2)", "M4 5h16M4 12h16M4 19h16 M7 5v14 M12 5v14 M17 5v14"),
                ("rongyuan", "Họ RongYuan", "M12 3.5 4 8v8l8 4.5 8-4.5V8Z M12 12 4 8 M12 12l8-4 M12 12v8.5"),
                ("mchose", "Họ MCHOSE", "M12 21s-7.5-4.6-7.5-10.5A7.5 7.5 0 0 1 19.5 10.5C19.5 16.4 12 21 12 21Z M12 12.8v-5 M9.8 10.2 12 7.8l2.2 2.4"),
                ("sonix", "Họ SONiX (S75/MAD60/HERO)", "M5 5h14v14H5z M9 5V3 M15 5V3 M9 19v2 M15 19v2 M5 10H3 M5 15H3 M19 10h2 M19 15h2"),
                ("witmod", "Họ WitMod (Redragon)", "M4 17 17 4l3 3L7 20H4Z M14 7l3 3"),
                ("dareu", "Họ DareU", "M12 3v3 M12 18v3 M3 12h3 M18 12h3 M5.6 5.6l2.1 2.1 M16.3 16.3l2.1 2.1 M18.4 5.6l-2.1 2.1 M7.7 16.3l-2.1 2.1 M12 15.5 A3.5 3.5 0 1 1 12 8.5 A3.5 3.5 0 1 1 12 15.5"),
                ("aulahub", "Họ AULA Hub", "M4 6.5 A1.5 1.5 0 0 1 5.5 5h13A1.5 1.5 0 0 1 20 6.5v11A1.5 1.5 0 0 1 18.5 19h-13A1.5 1.5 0 0 1 4 17.5Z M7.5 9.5h9 M7.5 13h5"),
                ("store", "Chợ plugin", "M5.5 8h13l-.9 11.5A1.5 1.5 0 0 1 16.1 21H7.9a1.5 1.5 0 0 1-1.5-1.5L5.5 8Z M8.5 10.5V6.8a3.5 3.5 0 0 1 7 0v3.7"),
                ("widget", "Widget TFT", "M4.5 15.5 A8.5 8.5 0 1 1 19.5 15.5 M12 15.5 16 10 M12 15.5 m-1.2 0 a1.2 1.2 0 1 0 2.4 0 a1.2 1.2 0 1 0-2.4 0"),
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
            // v3.11: nut "Cai dat" co dinh o day thanh ben — mo PANEL CAI DAT
            // (khong con la trang nhu truoc v3.11).
            var setIcon = NavIcon("M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z M19.4 15a1.7 1.7 0 0 0 .34 1.87l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06A1.7 1.7 0 0 0 15 19.4a1.7 1.7 0 0 0-1 1.55V21a2 2 0 1 1-4 0v-.09A1.7 1.7 0 0 0 9 19.4a1.7 1.7 0 0 0-1.87.34l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.7 1.7 0 0 0 4.6 15a1.7 1.7 0 0 0-1.55-1H3a2 2 0 1 1 0-4h.09A1.7 1.7 0 0 0 4.6 9a1.7 1.7 0 0 0-.34-1.87l-.06-.06A1.7 1.7 0 0 0 9 4.6a1.7 1.7 0 0 0 1-1.55V3a2 2 0 1 1 4 0v.09a1.7 1.7 0 0 0 1 1.51 1.7 1.7 0 0 0 1.87-.34l.06-.06A1.7 1.7 0 1 0 19.4 9c.24.6.86 1 1.51 1H21a2 2 0 1 1 0 4h-.09a1.7 1.7 0 0 0-1.51 1Z", idle);
            var setBtn = new Button { Content = H(11, setIcon, Tb("Cài đặt", null, 13.5)) };
            setBtn.Classes.Add("nav");
            setBtn.Click += (s, e) => OpenSettings("general");
            SideSettingsHost.Children.Add(setBtn);
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
                if (key == "rongyuan") show = _selectedModel.Family == "rongyuan";
                if (key == "mchose") show = _selectedModel.Family == "mchose";
                if (key == "sonix") show = _selectedModel.Family == "sonix_sib";
                if (key == "witmod") show = _selectedModel.Family == "witmod";
                if (key == "dareu") show = _selectedModel.Family == "dareu";
                if (key == "aulahub") show = _selectedModel.Family == "aulahub";
                btn.IsVisible = show;
            }
            if (!_pages.ContainsKey(_currentPage)) return;
            bool curHidden = (_currentPage == "gif" || _currentPage == "widget") && !_selectedModel.HasScreen
                             || _currentPage == "perkey" && !_selectedModel.FullSupport
                             || _currentPage == "kbbyv2" && _selectedModel.Family != "kb_by_v2"
                             || _currentPage == "rongyuan" && _selectedModel.Family != "rongyuan"
                             || _currentPage == "mchose" && _selectedModel.Family != "mchose"
                             || _currentPage == "sonix" && _selectedModel.Family != "sonix_sib"
                             || _currentPage == "witmod" && _selectedModel.Family != "witmod"
                             || _currentPage == "dareu" && _selectedModel.Family != "dareu"
                             || _currentPage == "aulahub" && _selectedModel.Family != "aulahub";
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
                icon.Stroke = new SolidColorBrush(sel
                    ? (UiBrushes.LegacyChrome ? Colors.White : Color.FromRgb(0x12, 0x12, 0x18))
                    : Color.FromRgb(0xB9, 0xB6, 0xCC));
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
            if (key == "widget") { UpdateWeatherLocText(); RenderWidgetFrame(); }
            if (key == "store") RefreshMyPlugins();
            if (key == "rongyuan") RefreshRyPage();
            if (key == "mchose") RefreshMcPage();
            if (key == "sonix") RefreshSxPage();
            if (key == "witmod") RefreshWmPage();
            if (key == "dareu") RefreshDareUPage();
            if (key == "aulahub") RefreshAulaHubPage();
            if (key != "perkey") { _ledPlayer?.Stop(); _rtPlayer?.Stop(); }
            if (key == "perkey") _refreshSchemeList?.Invoke();
            if (key != "online") _onlineLedPlayer?.Stop();
            if (key != "music" && _musicPlaying) StopMusic();
        }

        // v3.14: hop xac nhan chuan cho thao tac khoi phuc cai dat goc
        // (ban hoan thien khong con cong tac an toan — xac nhan la UX chuan).
        private async Task<bool> ConfirmAsync(string title, string message)
        {
            bool result = false;
            var win = new Window
            {
                Title = title,
                Width = 430,
                Height = 196,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x1E)),
            };
            var okBtn = new Button { Content = "Khôi phục", HorizontalAlignment = HorizontalAlignment.Right };
            okBtn.Classes.Add("primary");
            okBtn.Click += (s, e) => { result = true; win.Close(); };
            var cancelBtn = new Button { Content = "Huỷ", HorizontalAlignment = HorizontalAlignment.Right };
            cancelBtn.Classes.Add("ghost");
            cancelBtn.Click += (s, e) => win.Close();
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
            row.Children.Add(cancelBtn);
            row.Children.Add(okBtn);
            var body = new StackPanel { Spacing = 14, Margin = new Thickness(20) };
            body.Children.Add(Tb(title, "h2"));
            body.Children.Add(Tb(message, "sub"));
            body.Children.Add(row);
            win.Content = body;
            await win.ShowDialog(this);
            return result;
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

        // v3.9.1: ap dung cai dat an/hien nhat ky NGAY khi doi cong tac trong
        // Cai dat (khong can khoi dong lai). Panel an bang IsVisible=false
        // trong StackPanel se tu nhuong lai khoang trong cho noi dung khac.
        // Khi vua bat lai, dong bo noi dung tu bo dem _logLines (bo dem nay
        // van duoc AppendLog cap nhat lien tuc ke ca khi panel dang an).
        private void ApplyLogVisibility()
        {
            bool show = _ui.ShowOperationLog;
            if (show)
            {
                string text = string.Join("\n", _logLines);
                if (_logBox != null) { _logBox.Text = text; _logBox.CaretIndex = text.Length; }
                if (_expLogBox != null) { _expLogBox.Text = text; _expLogBox.CaretIndex = text.Length; }
            }
            if (_homeLogCard != null) _homeLogCard.IsVisible = show;
            if (_expLogCard != null) _expLogCard.IsVisible = show;
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
                ShowToast($"Đã gửi xuống phím: {title}");
            }
            catch (Exception ex)
            {
                AppendLog("LỖI: " + ex.Message);
                ShowToast("Lỗi: " + ex.Message);
            }
            finally { _busy = false; }
        }

        private Task ApplyRgbAsync(int effect, Color c, bool multi, int brightUi, int speedUi, int direction)
        {
            // v3.15: ap den bang tay (ke ca lich trinh goi ham nay —
            // ApplyScheduleRule da ghi nho Key truoc) -> thang lich
            // trinh cho den bien luat tiep theo.
            MarkManualLightingChange();
            return RunStepsAsync("Áp dụng hiệu ứng RGB",
                () => F75Protocol.BuildLighting(effect, c.R, c.G, c.B, multi, brightUi + 1, speedUi + 1, direction));
        }

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
                ShowToast($"Đã nạp lên màn hình phím: {title}");
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
                // v3.15: cache chuyen doi — file da ma hoa (chon truoc
                // o trang Man hinh GIF la da tien chuyen nen) thi gui
                // ngay, khong con buoc loading giai ma/ma hoa.
                var res = await Task.Run(() => _displayCache.GetOrEncode(path, fit,
                    (p, f) => DisplayEncoder.Encode(p, f, msg => AppendLog(msg))));
                stream = res.Stream;
                AppendLog(res.FromCache
                    ? $"Màn hình TFT: dùng cache chuyển đổi của “{name}” ({res.Chunks} chunk) — bỏ qua mã hoá."
                    : $"Màn hình TFT: đã mã hoá “{name}” ({res.Chunks} chunk) và lưu cache cho lần sau.");
            }
            catch (Exception ex)
            {
                AppendLog("LỖI mã hoá GIF: " + ex.Message);
                ShowToast("Lỗi mã hoá GIF: " + ex.Message);
                return;
            }
            _widgetGate.Reset(); // man hinh vua doi sang GIF — widget cu khong con tren man
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
            var connectCard = Card(V(12, connectHeader, _detectedPanel,
                Tb("Sao chép thông tin thiết bị để báo lỗi hoặc dùng với mẫu chưa có trong bảng.", "faint"),
                CopyInfoButton()));

            var homeKb = new KeyboardView { Mode = KeyboardMode.Animated, State = _rgb };
            var heroCard = Card(homeKb, 14);

            _homeFxValue = Tb(EffectFriendlyNames[_rgb.Effect], "h3");
            _homeGifValue = Tb("Tien Galaxy Spiral", "h3");
            _homeVerValue = Tb("v" + AboutInfo.Version, "h3");
            var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 12 };
            var s1 = StatCard("Hiệu ứng RGB", _homeFxValue);
            var s2 = StatCard("GIF màn hình", _homeGifValue);
            var s3 = StatCard("Phiên bản app", _homeVerValue);
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
            _homeLogCard = logCard;
            logCard.IsVisible = _ui.ShowOperationLog; // v3.9.1: an mac dinh

            _integrityBanner = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xF5, 0x9B, 0x9B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9B, 0x9B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                IsVisible = false,
                Child = Tb(IntegrityCheck.TamperWarning, null, 12),
            };
            var root = V(14,
                Tb("Tổng quan", "h1"),
                _integrityBanner,
                connectCard,
                heroCard,
                stats,
                Tb("Tất cả mẫu được hỗ trợ", "h2"),
                models,
                Tb("“Hỗ trợ” = app dùng được với mẫu đó hôm nay; “Chưa hỗ trợ” = mới có trong danh sách, chưa có driver.", "faint"),
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
                // v3.11.2: the chi con ten + pill + MOT dong ngan (DeviceCardLine);
                // VID/so giao dien/vai tro kenh la thong so ky thuat — xem o
                // "Sao chep thong tin thiet bi", khong in len the nua.
                bool selected = _selectedModel.FullSupport;
                _detectedPanel.Children.Add(DeviceCard(
                    max.Model.Name, selected ? "Đang sử dụng" : "Đã phát hiện", Color.FromRgb(0x34, 0xD3, 0x99),
                    DeviceCardLine.Build(max.Connection, fullSupport: true), null));
            }
            foreach (var d in found.Where(d => !d.Model.FullSupport))
            {
                var btn = GhostBtn("Xem trước mẫu này", () => SelectModel(d.Model));
                _detectedPanel.Children.Add(DeviceCard(
                    d.Model.Name, "Đã phát hiện", Color.FromRgb(0xC4, 0xB5, 0xFD),
                    DeviceCardLine.Build(d.Connection, fullSupport: false), btn));
            }
            if (found.Count == 0)
            {
                _detectedPanel.Children.Add(Tb("Không tìm thấy bàn phím nào. Hãy cắm phím bằng cáp USB rồi quét lại.", "sub"));
            }
            else
            {
                // 258A:010C dung chung cho nhieu phim — canh bao trung lap neu co
                var shared = found.GroupBy(d => (d.Vid, d.Pid)).Where(g => g.Count() > 1).ToList();
                foreach (var g in shared)
                    _detectedPanel.Children.Add(Tb("Lưu ý: các mẫu trên dùng chung một mã kết nối — app định danh chính xác ở trang riêng của từng họ.", "faint"));
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
            // v3.11.2: toast rut gon — bo so hieu driver pack/stack/opcode,
            // chi con huong dan mo trang ho + chay Nhan dien.
            string familyPage =
                model.Family == "kb_by_v2" ? "Họ Sinowealth" :
                model.Family == "rongyuan" ? "Họ RongYuan" :
                model.Family == "mchose" ? "Họ MCHOSE" :
                model.Family == "sonix_sib" ? "Họ SONiX" :
                model.Family == "witmod" ? "Họ WitMod" :
                model.Family == "dareu" ? "Họ DareU" :
                model.Family == "aulahub" ? "Họ AULA Hub" : null;
            ShowToast(model.FullSupport
                ? $"Đang dùng {model.Name} (hỗ trợ đầy đủ)."
                : familyPage != null
                    ? $"{model.Name}: mở trang '{familyPage}' và chạy Nhận diện trước khi ghi."
                    : $"{model.Name}: chế độ XEM TRƯỚC — xem được giao diện/hiệu ứng, chưa ghi xuống phím được.");
            if ((_currentPage == "gif" || _currentPage == "widget") && !model.HasScreen) ShowPage("home");
            if (_currentPage == "perkey" && !model.FullSupport) ShowPage("home");
            if (_currentPage == "kbbyv2" && model.Family != "kb_by_v2") ShowPage("home");
            if (_currentPage == "rongyuan" && model.Family != "rongyuan") ShowPage("home");
            if (_currentPage == "mchose" && model.Family != "mchose") ShowPage("home");
            if (_currentPage == "sonix" && model.Family != "sonix_sib") ShowPage("home");
            if (_currentPage == "witmod" && model.Family != "witmod") ShowPage("home");
            if (_currentPage == "dareu" && model.Family != "dareu") ShowPage("home");
            if (_currentPage == "aulahub" && model.Family != "aulahub") ShowPage("home");
            if (_currentPage == "rongyuan") RefreshRyPage();
            if (_currentPage == "mchose") RefreshMcPage();
            if (_currentPage == "sonix") RefreshSxPage();
            if (_currentPage == "witmod") RefreshWmPage();
            if (_currentPage == "dareu") RefreshDareUPage();
            if (_currentPage == "aulahub") RefreshAulaHubPage();
        }

        // v3.11.1: danh sach mau gon theo yeu cau cua Tien — gom theo HANG,
        // moi mau chi con ten + pill nhi phan "Ho tro / Chua ho tro"
        // (ModelCatalog). Bo dong spec va dong trang thai dai cua Driver
        // Pack; chi tiet kha nang nam o trang rieng cua tung ho giao thuc.
        // Bam vao mot mau van chon mau do nhu truoc; mau dang chon / ho tro
        // day du giu vien accent de nhan dien.
        private Control BuildModelGrid()
        {
            var root = new StackPanel { Spacing = 16 };
            foreach (var group in ModelCatalog.GroupByBrand(ModelDatabase.All))
            {
                var section = new StackPanel { Spacing = 8 };
                section.Children.Add(H(8,
                    Tb(group.Brand, "h3"),
                    Tb("· " + group.Models.Count + " mẫu · " + group.SupportedCount + " hỗ trợ", "faint")));
                var wrap = new WrapPanel();
                foreach (var model in group.Models)
                {
                    var m = model;
                    bool supported = ModelCatalog.IsSupported(m);
                    var pill = new Border
                    {
                        Background = new SolidColorBrush(supported
                            ? Color.FromArgb(0x26, 0x34, 0xD3, 0x99)
                            : Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
                        CornerRadius = new CornerRadius(999),
                        Padding = new Thickness(8, 2),
                        Child = new TextBlock
                        {
                            Text = supported ? "Hỗ trợ" : "Chưa hỗ trợ",
                            FontSize = 10,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(supported
                                ? Color.FromRgb(0x34, 0xD3, 0x99)
                                : Color.FromRgb(0x8A, 0x87, 0xA0)),
                        },
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    var chip = new Border
                    {
                        Classes = { "pickcard" },
                        Child = H(8, Tb(m.Name, null, 12.5), pill),
                        Margin = new Thickness(0, 0, 8, 8),
                        Cursor = new Cursor(StandardCursorType.Hand),
                    };
                    if (m.FullSupport || m == _selectedModel)
                    {
                        chip.BorderBrush = UiBrushes.AccentGradient();
                        chip.BorderThickness = new Thickness(1.6);
                    }
                    chip.PointerPressed += (s2, e2) => SelectModel(m);
                    wrap.Children.Add(chip);
                }
                section.Children.Add(wrap);
                root.Children.Add(section);
            }
            return root;
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
                Child = Tb("Chưa kiểm chứng trên phần cứng thật — giao thức kb_by_v2 được tài liệu hoá từ driver chính hãng (YUNZII hub + app tham chiếu họ A), nhưng không ai trong đội sở hữu các mẫu phím này. Lệnh ghi chỉ chạy khi thiết bị đã định danh 0x82 khớp registry.", null, 12),
            };

            _kbIdStatus = Tb("Chưa nhận diện. Hãy cắm phím họ Sinowealth (VID 258A) bằng cáp USB rồi bấm Nhận diện.", "sub");
            var idCard = Card(V(10,
                Tb("1 · Nhận diện thiết bị (bắt buộc trước khi ghi)", "h2"),
                Tb("Nhiều mẫu dùng chung một mã USB nên app định danh bằng lệnh đọc từ chính thiết bị trước khi cho ghi.", "sub"),
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
                Tb("3 · Ghi xuống phím", "h2"),
                Tb("Áp dụng trực tiếp xuống phím khi thiết bị đã được định danh khớp.", "sub"),
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
                    {
                        if (!await ConfirmAsync("Khôi phục cài đặt gốc?", "Toàn bộ cài đặt trên phím sẽ trở về mặc định của hãng. Thao tác này không hoàn tác được.")) return;
                        await KbWriteAsync("Khôi phục cài đặt gốc (0x11)", () => _kb.SendWrite("Factory reset kb_by_v2", KbByV2Protocol.BuildFactoryReset(), verifyEcho: false));
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
                ShowToast(sent ? $"Đã gửi xuống phím: {title}" : $"{title}: chưa gửi được — hãy bấm Nhận diện thiết bị trước.");
            }
            catch (Exception ex) { AppendLog("LỖI ghi kb_by_v2: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
            finally { _busy = false; }
        }

        // ================= TRANG HO RONGYUAN (Driver Pack 2, v3.5) =================
        // Nen tang dung chung cua Akko Cloud v4 / MonsGeek V4 / EPOMAKER
        // Driver. Trang chi hien khi model dang chon co Family == "rongyuan";
        // muc HE chi hien voi model Hall-effect, muc man hinh chi hien voi
        // model co man hinh (quy tac capability). Noi dung cac muc duoc lam
        // moi theo model dang chon trong RefreshRyPage().
        private Control BuildRongYuanPage()
        {
            var warn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xF5, 0x9B, 0x9B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9B, 0x9B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                Child = Tb("Chưa kiểm chứng trên phần cứng thật — giao thức RongYuan được tài liệu hoá từ driver chính hãng (Akko Cloud v4, MonsGeek V4, EPOMAKER Driver đều là cùng một nền tảng OEM), nhưng không ai trong đội sở hữu các mẫu phím này. Model chưa có dev_id đã kiểm chứng trong registry (toàn bộ EPOMAKER và phần lớn Akko) ở chế độ chỉ xem.", null, 12),
            };

            _ryModelInfo = Tb("", "sub");
            _ryIdStatus = Tb("Chưa nhận diện. Hãy cắm phím nền RongYuan (VID 3151…) bằng cáp USB rồi bấm Nhận diện.", "sub");
            var idCard = Card(V(10,
                Tb("1 · Nhận diện thiết bị (bắt buộc trước khi ghi)", "h2"),
                _ryModelInfo,
                Tb("Các mẫu nền này dùng chung mã USB; app đọc mã định danh từ chính thiết bị và đối chiếu danh sách — khớp mới cho ghi, còn lại chỉ xem.", "sub"),
                PrimaryBtn("Nhận diện thiết bị (đọc 0x8F — chỉ đọc)", async () =>
                {
                    if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
                    _busy = true;
                    try
                    {
                        var (vid, pid, _) = _selectedModel.Ids.Count > 0 ? _selectedModel.Ids[0] : (0x3151, 0x5030, "");
                        var id = await Task.Run(() => _ry.Identify(vid, pid));
                        _ryIdStatus.Text = id.Known
                            ? $"Đã định danh: {id.ModelName} — dev_id {id.DevIdHex} (chip {id.Entry.ChipFamily}, VID {id.Entry.Vid:X4}). {id.Note}"
                            : $"Chưa định danh được. dev_id đọc được: {id.DevIdHex}. {id.Note}";
                        ShowToast(id.Known ? $"Đã định danh: {id.ModelName}" : "Không khớp registry — chế độ chỉ xem.");
                    }
                    catch (Exception ex) { AppendLog("LỖI nhận diện RongYuan: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
                    finally { _busy = false; }
                }),
                _ryIdStatus));

            _ryInfoStatus = Tb("", "sub");
            var readCard = Card(V(10,
                Tb("2 · Đọc thông tin (chỉ đọc — chạy live)", "h2"),
                H(10,
                    GhostBtn("Danh sách tính năng (230)", async () =>
                    {
                        var reply = await RyReadAsync("Feature list", RongYuanProtocol.BuildGetFeatureList());
                        if (reply != null)
                            _ryInfoStatus.Text = RongYuanProtocol.CheckFeatureListAck(reply)
                                ? "Feature list: thiết bị trả lời hợp lệ (resp[1] = 0xAA) — bitmap tính năng nằm ở các byte tiếp theo, ngữ nghĩa từng bit cần kiểm chứng trên phím thật."
                                : "Feature list: đáp trả không có dấu 0xAA ở byte 1 — có thể model này không hỗ trợ lệnh 230.";
                    }),
                    GhostBtn("Đọc profile hiện tại (132)", async () => await RyReadAsync("Profile", RongYuanProtocol.BuildGetProfile())),
                    GhostBtn("Đọc keymap manh 0 (138)", async () => await RyReadAsync("Keymap chunk 0", RongYuanProtocol.BuildKeymatrixReadChunk(0, 0)))),
                _ryInfoStatus));

            var profileSeg = Segmented(new[] { "Profile 0", "Profile 1", "Profile 2", "Profile 3" }, 0, async idx =>
                await RyWriteAsync($"Chọn profile {idx} (0x04)", () => _ry.SendWrite("Chon profile RongYuan", RongYuanProtocol.BuildSetProfile(idx))));
            var writeCard = Card(V(10,
                Tb("3 · Ghi xuống phím", "h2"),
                Tb("Áp dụng trực tiếp xuống phím khi thiết bị đã được định danh khớp.", "sub"),
                Tb("Chọn profile đang hoạt động:", "sub"),
                profileSeg.Root,
                H(10,
                    PrimaryBtn("Ghi đèn từng phím cầu vồng (0x0C)", async () =>
                    {
                        int keys = RyKeyCount();
                        var slots = new List<(byte R, byte G, byte B)>();
                        for (int i = 0; i < keys; i++)
                        {
                            var c = KeyboardView.Hsv(i / (double)Math.Max(1, keys), 0.85, 0.9);
                            slots.Add((c.R, c.G, c.B));
                        }
                        var data = RongYuanProtocol.FlattenRgb(slots);
                        var packets = RongYuanProtocol.BuildUserPicWrite(0, data);
                        await RyWriteAsync("Ghi đèn từng phím cầu vồng (0x0C)", () => _ry.SendChunkedWrite("UserPic cau vong RongYuan", packets));
                    }),
                    GhostBtn("Đèn chính: tĩnh, tím, sáng 4/5 (0x07)", async () =>
                        await RyWriteAsync("Đèn chính tĩnh tím (0x07)", () => _ry.SendWrite("LED chinh RongYuan", RongYuanProtocol.BuildSetLed(0, 0, 4, 0, false, 0x7C, 0x3A, 0xED)))),
                    GhostBtn("Xoá flash màn hình (0x2C)", async () =>
                        await RyWriteAsync("Xoá flash (0x2C)", () => _ry.SendWrite("Flash erase RongYuan", RongYuanProtocol.BuildFlashErase()))))));

            // --- Muc Hall-effect: chi hien voi model HE (RefreshRyPage) ---
            var scaleSeg = Segmented(new[] { "Hệ số ×100", "Hệ số ×200", "Hệ số ×1000", "Hệ số ×10" }, 0, idx =>
            {
                _ryScale = new[] { 100, 200, 1000, 10 }[idx];
                AppendLog($"[RONGYUAN] He so quy doi hanh trinh HE: x{_ryScale} (chon theo lop firmware — mac dinh x100 la pho bien nhat).");
            });
            var actSeg = Segmented(new[] { "0.5 mm", "1.0 mm", "1.5 mm", "2.0 mm" }, 1, async idx =>
            {
                double mm = new[] { 0.5, 1.0, 1.5, 2.0 }[idx];
                await RyWriteHeBulkAsync($"Điểm kích hoạt {mm:0.0} mm mọi phím", RongYuanHe.SubActuation,
                    RongYuanHe.SerializeU16(RyRepeatRaw(RongYuanHe.MmToRaw(mm, _ryScale))));
            });
            _ryCalStatus = Tb("Hiệu chuẩn chỉ chạy khi cắm dây, theo đúng luồng của driver gốc.", "sub");
            var heContent = V(10,
                Tb("4 · Hall-effect (lệnh gộp 0x65/0xE5)", "h2"),
                Tb("Mọi cấu hình từ tính (điểm kích hoạt/nhả, Rapid Trigger, DKS…) đi qua một lệnh gộp của nền tảng.", "sub"),
                Tb("Hệ số quy đổi theo lớp firmware:", "sub"),
                scaleSeg.Root,
                Tb("Điểm kích hoạt cho mọi phím:", "sub"),
                actSeg.Root,
                H(10,
                    GhostBtn("Bật Rapid Trigger mọi phím", async () =>
                        await RyWriteHeBulkAsync("Bật Rapid Trigger mọi phím", RongYuanHe.SubMode,
                            RyRepeatByte(RongYuanHe.EncodeModeByte(RongYuanHe.ModeNormal, true)))),
                    GhostBtn("Tắt Rapid Trigger", async () =>
                        await RyWriteHeBulkAsync("Tắt Rapid Trigger", RongYuanHe.SubMode,
                            RyRepeatByte(RongYuanHe.EncodeModeByte(RongYuanHe.ModeNormal, false)))),
                    GhostBtn("Vùng chết đáy 0.3 mm", async () =>
                        await RyWriteHeBulkAsync("Vùng chết đáy 0.3 mm", RongYuanHe.SubDeadZone,
                            RongYuanHe.SerializeU16(RyRepeatRaw(RongYuanHe.MmToRaw(0.3, _ryScale))))),
                    GhostBtn("Đọc hành trình realtime (254)", async () =>
                    {
                        var data = await RyReadChunksAsync("Hành trình realtime (254)",
                            i => RongYuanHe.BuildBulkReadChunk(RongYuanHe.SubRealtimeTravel, i),
                            RongYuanHe.ChunksForBytes(RyKeyCount() * 2));
                        if (data != null && data.Length >= 2)
                        {
                            var vals = RongYuanHe.ParseU16(data, Math.Min(RyKeyCount(), data.Length / 2));
                            _ryInfoStatus.Text = $"Realtime 254: {vals.Length} phím, giá trị thô đầu tiên: {string.Join(", ", vals.Take(8))} (chia cho hệ số ×{_ryScale} ra mm).";
                        }
                    })),
                Tb("Hiệu chuẩn switch từ tính (theo luồng driver gốc):", "h3"),
                H(10,
                    PrimaryBtn("Bước 1 · Bắt đầu: mốc MIN", async () =>
                    {
                        await RyWriteAsync("Hiệu chuẩn: bật báo cáo (27)", () => _ry.SendWrite("Calib report on", RongYuanHe.BuildSetCalibrationReport(true)));
                        await RyWriteAsync("Hiệu chuẩn: ghi mốc MIN (28)", () => _ry.SendWrite("Calib min on", RongYuanHe.BuildSetCalibrationMin(true)));
                        _ryCalStatus.Text = "Đang ghi mốc MIN — THẢ HẾT PHÍM ra, chờ khoảng 2 giây rồi sang Bước 2.";
                    }),
                    GhostBtn("Bước 2 · Sang mốc MAX", async () =>
                    {
                        await RyWriteAsync("Hiệu chuẩn: tắt mốc MIN (28)", () => _ry.SendWrite("Calib min off", RongYuanHe.BuildSetCalibrationMin(false)));
                        await RyWriteAsync("Hiệu chuẩn: ghi mốc MAX (30)", () => _ry.SendWrite("Calib max on", RongYuanHe.BuildSetCalibrationMax(true)));
                        _ryCalStatus.Text = "Đang ghi mốc MAX — NHẤN HẾT CỠ từng phím một lượt rồi sang Bước 3.";
                    }),
                    GhostBtn("Bước 3 · Kết thúc", async () =>
                    {
                        await RyWriteAsync("Hiệu chuẩn: tắt mốc MAX (30)", () => _ry.SendWrite("Calib max off", RongYuanHe.BuildSetCalibrationMax(false)));
                        await RyWriteAsync("Hiệu chuẩn: tắt báo cáo (27)", () => _ry.SendWrite("Calib report off", RongYuanHe.BuildSetCalibrationReport(false)));
                        _ryCalStatus.Text = "Đã kết thúc hiệu chuẩn (nếu các lệnh đã gửi thật). Nên đọc lại hành trình realtime (254) để đối chiếu.";
                    })),
                _ryCalStatus);
            _ryHeCard = Card(heContent);

            // --- Muc man hinh: chi hien voi model co man hinh (RefreshRyPage) ---
            var screenContent = V(10,
                Tb("5 · Màn hình TFT", "h2"),
                Tb("Đường nạp màn hình EPOMAKER đã trích được ở mức giao thức; các mục tương ứng nằm bên dưới.", "sub"),
                H(10,
                    PrimaryBtn("Nạp ảnh mẫu (gradient) lên màn hình", async () =>
                    {
                        var m = _selectedModel;
                        int w = m.ScreenW > 0 ? m.ScreenW : 128;
                        int h = m.ScreenH > 0 ? m.ScreenH : 128;
                        bool rgb888 = m.ScreenRgb888;
                        var rgb = RongYuanScreen.BuildTestPatternRgb(w, h);
                        var data = rgb888 ? rgb : RongYuanScreen.PackRgb565BE(rgb);
                        await RyWriteAsync($"Nạp ảnh mẫu {w}x{h}", () => _ry.UploadScreenFrame("Anh mau RongYuan", rgb888, data, w, h, eraseFirst: false));
                    }),
                    GhostBtn("Đồng bộ đồng hồ màn hình (0x28)", async () =>
                        await RyWriteAsync("Đồng bộ đồng hồ (0x28)", () => _ry.SendWrite("OLED clock RongYuan", RongYuanScreen.BuildOledClock(DateTime.Now))))));
            _ryScreenCard = Card(screenContent);

            var root = V(14,
                Tb("Họ RongYuan (Akko · MonsGeek · EPOMAKER)", "h1"),
                warn,
                idCard,
                readCard,
                writeCard,
                _ryHeCard,
                _ryScreenCard);
            return Scroll(root);
        }

        /// <summary>Lam moi trang RongYuan theo model dang chon: an/hien muc HE va man hinh theo capability.</summary>
        private void RefreshRyPage()
        {
            var m = _selectedModel;
            if (_ryHeCard != null) _ryHeCard.IsVisible = m.Family == "rongyuan" && m.HasHE;
            if (_ryScreenCard != null) _ryScreenCard.IsVisible = m.Family == "rongyuan" && m.HasScreen;
            if (_ryModelInfo != null)
            {
                string caps = (m.HasHE ? "Hall-effect" : "Cơ/thường")
                    + (m.HasScreen ? $" · màn hình {m.ScreenW}x{m.ScreenH} {(m.ScreenRgb888 ? "RGB888" : "RGB565")}" : " · không màn hình")
                    + (m.HasKnob ? " · núm xoay" : "");
                string ids = m.Ids.Count > 0
                    ? string.Join(", ", m.Ids.Select(t => $"{t.Vid:X4}:{t.Pid:X4}"))
                    : "(chưa có VID:PID trong registry)";
                _ryModelInfo.Text = $"Model đang chọn: {m.Name} — {caps}. VID:PID đã biết: {ids}. " +
                    (m.DevIds.Count > 0
                        ? $"dev_id đã kiểm chứng: {string.Join(", ", m.DevIds.Select(d => "0x" + d.ToString("X8")))}."
                        : "Model này CHƯA có dev_id đã kiểm chứng — chỉ xem.");
            }
        }

        /// <summary>So phim cua model RongYuan dang chon (tu registry; mac dinh 126 o nhu driver goc).</summary>
        private int RyKeyCount()
        {
            int keys = 0;
            foreach (var e in RongYuanRegistryData.Entries)
                if (string.Equals(e.DisplayName, _selectedModel.Name, StringComparison.OrdinalIgnoreCase))
                    keys = Math.Max(keys, e.Keys);
            return keys > 0 ? keys : 126;
        }

        private ushort[] RyRepeatRaw(ushort raw)
        {
            var arr = new ushort[RyKeyCount()];
            for (int i = 0; i < arr.Length; i++) arr[i] = raw;
            return arr;
        }

        private byte[] RyRepeatByte(byte value)
        {
            var arr = new byte[RyKeyCount()];
            for (int i = 0; i < arr.Length; i++) arr[i] = value;
            return arr;
        }

        private async Task<byte[]> RyReadAsync(string title, byte[] packet)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return null; }
            _busy = true;
            try
            {
                var reply = await Task.Run(() => _ry.SendRead(title, packet));
                if (reply != null && _ryInfoStatus != null)
                    _ryInfoStatus.Text = $"{title}: nhận 64 byte, đầu gói: {string.Join(" ", reply.Take(12).Select(b => b.ToString("X2")))}…";
                return reply;
            }
            catch (Exception ex) { AppendLog("LỖI đọc RongYuan: " + ex.Message); ShowToast("Lỗi: " + ex.Message); return null; }
            finally { _busy = false; }
        }

        private async Task<byte[]> RyReadChunksAsync(string title, Func<int, byte[]> requestForChunk, int chunkCount)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return null; }
            _busy = true;
            try { return await Task.Run(() => _ry.SendReadChunks(title, requestForChunk, chunkCount)); }
            catch (Exception ex) { AppendLog("LỖI đọc RongYuan: " + ex.Message); ShowToast("Lỗi: " + ex.Message); return null; }
            finally { _busy = false; }
        }

        private async Task RyWriteAsync(string title, Func<bool> send)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
            _busy = true;
            try
            {
                bool sent = await Task.Run(send);
                ShowToast(sent ? $"Đã gửi xuống phím: {title}" : $"{title}: chưa gửi được — hãy bấm Nhận diện thiết bị trước.");
            }
            catch (Exception ex) { AppendLog("LỖI ghi RongYuan: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
            finally { _busy = false; }
        }

        private async Task RyWriteHeBulkAsync(string title, byte subOp, byte[] data)
        {
            var packets = RongYuanHe.BuildBulkWrite(subOp, data);
            await RyWriteAsync(title, () => _ry.SendChunkedWrite(title, packets));
        }

        // ================= CONG DONG (v3.7): sao chep thong tin + bang thiet bi =================
        private string RegistryStatusText()
        {
            var reg = DeviceRegistry.LoadDownloaded();
            return reg != null
                ? $"Đang có bảng đã tải về: phiên bản {reg.Version}, cập nhật {reg.Updated}, {reg.Devices.Count} mục — áp dụng ở lần mở app sau (chỉ bổ sung mẫu mới, không ghi đè bảng tích hợp)."
                : "Chưa có bảng tải về — đang dùng bảng tích hợp sẵn trong app.";
        }

        private async Task CheckRegistryUpdateAsync()
        {
            if (_busy) { ShowToast("Đang bận — đợi thao tác trước xong đã."); return; }
            _busy = true;
            try
            {
                ShowToast("Đang tải bảng thiết bị từ GitHub…");
                string json;
                using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                    json = await http.GetStringAsync(DeviceRegistry.RawUrl);
                if (!DeviceRegistry.TryParseValid(json, out var reg, out string error))
                {
                    ShowToast("Bảng tải về không hợp lệ: " + error);
                    AppendLog("[Registry] Từ chối bảng tải về: " + error);
                    return;
                }
                Directory.CreateDirectory(GifLibraryStore.RootDir);
                File.WriteAllText(DeviceRegistry.LocalPath, json);
                if (_regStatus != null) _regStatus.Text = RegistryStatusText();
                ShowToast($"Đã lưu bảng thiết bị v{reg.Version} ({reg.Devices.Count} mục) — mở lại app để áp dụng.");
                AppendLog($"[Registry] Đã tải và lưu bảng thiết bị v{reg.Version} ({reg.Devices.Count} mục, cập nhật {reg.Updated}).");
            }
            catch (Exception ex)
            {
                ShowToast("Không tải được bảng thiết bị: " + ex.Message);
                AppendLog("[Registry] Lỗi tải bảng: " + ex.Message);
            }
            finally { _busy = false; }
        }

        private List<DeviceProbeInfo> CollectProbeInfos()
        {
            var summaries = new List<string>();
            void Add(string label, string s) { if (!string.IsNullOrWhiteSpace(s)) summaries.Add(label + ": " + s); }
            Add("kb_by_v2", _kbIdStatus?.Text);
            Add("RongYuan", _ryIdStatus?.Text);
            Add("MCHOSE", _mcIdStatus?.Text);
            Add("SONiX anh em", _sx.LastIdSummary);
            Add("WitMod", _wm.LastIdSummary);
            Add("DareU", _duIdStatus?.Text);
            Add("AULA Hub", _ahIdStatus?.Text);
            string probeText = summaries.Count > 0 ? string.Join(" | ", summaries) : "";

            var list = new List<DeviceProbeInfo>();
            foreach (var d in _detected)
            {
                list.Add(new DeviceProbeInfo
                {
                    VidPid = $"{d.Vid:X4}:{d.Pid:X4}",
                    ProductName = d.Model.Name,
                    ProbeResults = string.IsNullOrEmpty(probeText) ? d.Model.ProtocolStatus : probeText,
                    IdentifiedAs = d.Model.Name + " — " + d.Model.ProtocolStatus,
                });
            }
            if (list.Count == 0 && summaries.Count > 0)
            {
                list.Add(new DeviceProbeInfo
                {
                    VidPid = _selectedModel.Ids.Count > 0 ? $"{_selectedModel.Ids[0].Vid:X4}:{(_selectedModel.Ids[0].Pid < 0 ? "????" : _selectedModel.Ids[0].Pid.ToString("X4"))}" : "",
                    ProductName = _selectedModel.Name,
                    ProbeResults = probeText,
                    IdentifiedAs = _selectedModel.Name,
                });
            }
            return list;
        }

        private async Task CopyDeviceInfoAsync()
        {
            try
            {
                string text = DeviceInfoReport.Build(AboutInfo.Version, _selectedModel, CollectProbeInfos());
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null) await clipboard.SetTextAsync(text);
                ShowToast("Đã sao chép thông tin thiết bị — dán vào GitHub issue là xong.");
                AppendLog("[Cộng đồng] Đã sao chép thông tin thiết bị vào clipboard.");
            }
            catch (Exception ex)
            {
                ShowToast("Không sao chép được: " + ex.Message);
            }
        }

        private Button CopyInfoButton()
            => GhostBtn("Sao chép thông tin thiết bị", async () => await CopyDeviceInfoAsync());

        private static (byte R, byte G, byte B) RainbowColor(double t)
        {
            // HSV -> RGB don gian (S=V=1) de tao frame cau vong cho stream thu
            double h = (t - Math.Floor(t)) * 6.0;
            double x = 1 - Math.Abs(h % 2 - 1);
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = 1; g = x; }
            else if (h < 2) { r = x; g = 1; }
            else if (h < 3) { g = 1; b = x; }
            else if (h < 4) { g = x; b = 1; }
            else if (h < 5) { r = x; b = 1; }
            else { r = 1; b = x; }
            return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        // ================= TRANG HO SONiX ANH EM (Driver Pack 4, v3.7) =================
        // 5 stack trong mot trang, moi stack mot the; the nao hien tuy
        // thuoc vao Stack cua model dang chon (quy tac capability):
        //  B = S75 Pro/Nova75 (chung nen F75 Max — probe 0xF5 khong phan
        //      biet duoc voi F75 Max, trang co canh bao ro rang)
        //  C = MAD60 (bat tay fw protocol v9), D = HERO68 HE (chu ky 130),
        //  E = WIN60 HE (lenh 7). (WIN60 SE khong con muc rieng tu v3.14:
        //  duong ghi cua no chua ro nen app khong dua thao tac nao cho no.)
        private Control BuildSonixPage()
        {
            var warn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xF5, 0x9B, 0x9B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9B, 0x9B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                Child = Tb("Chưa kiểm chứng trên phần cứng thật — các họ này được trích byte-level từ một ứng dụng driver đã giải phẫu (xem docs/PROTOCOLS-REFERENCE-APP.md trong repo), nhưng đội phát triển không sở hữu các mẫu phím này. Lệnh ghi/stream chỉ chạy sau probe nhận diện của họ. Ứng dụng gốc là streamer LED thuần (không ghi bền) và luôn sao lưu màu gốc rồi khôi phục khi dừng — app này cũng vậy.", null, 12),
            };

            _sxIdStatus = Tb("Chưa chạy probe/nhận diện cho họ này.", "sub");
            _sxInfoStatus = Tb("", "sub");
            var idCard = Card(V(10,
                Tb("Nhận diện & probe (chạy thật, chỉ đọc)", "h2"),
                _sxIdStatus,
                _sxInfoStatus,
                CopyInfoButton()));

            // --- The B: S75 Pro / Nova75 ---
            _sxCardB = Card(V(10,
                Tb("Họ B — S75 Pro / Nova75 (nền SONiX 64 byte)", "h2"),
                Tb("⚠ S75 Pro và Nova75 dùng chung mã USB và giao thức với F75 Max — lệnh đọc không phân biệt được chúng với F75 Max; hãy chắc chắn đúng mẫu đang cắm trước khi ghi.", "sub"),
                Tb("Probe 0xF5: gửi lệnh đọc (byte[8]=9), kiểm ACK (reply[1]=opcode, reply[3]=1 sẵn sàng / 0xFF bận), đọc 9 khối trạng thái màu phím hiện tại.", "sub"),
                PrimaryBtn("Probe 0xF5 (đọc trạng thái)", async () => await SxProbeAsync("Probe 0xF5 họ B", () => _sx.ProbeSonixB())),
                (_sxBStreamBtn = WriteBtn("Stream 1 frame cầu vồng", async () => await SxWriteAsync("Stream frame họ B", () =>
                {
                    var colors = new Dictionary<byte, (byte R, byte G, byte B)>();
                    for (int i = 0; i < SonixBProtocol.S75ProKeyIds.Length; i++)
                        colors[SonixBProtocol.S75ProKeyIds[i]] = RainbowColor(i / (double)SonixBProtocol.S75ProKeyIds.Length);
                    return _sx.StreamFrameSonixB(_selectedModel.Name, colors);
                })))));

            // --- The C: MAD60 ---
            _sxCardC = Card(V(10,
                Tb("Họ C — MADLIONS MAD60 HE (V2 / thường / RGB)", "h2"),
                Tb("MAD60 HE kết nối qua kênh riêng của hãng; bắt tay cần đúng phiên bản firmware mới cho ghi.", "sub"),
                PrimaryBtn("Bắt tay kiểm tra (fw protocol v9)", async () => await SxProbeAsync("Bắt tay MAD60", () => _sx.ProbeMad60())),
                WriteBtn("Stream 1 frame cầu vồng", async () => await SxWriteAsync("Stream frame MAD60", () =>
                {
                    var rgb = new byte[Mad60Protocol.ColorBytes];
                    for (int i = 0; i < Mad60Protocol.LedSlots; i++)
                    {
                        var c = RainbowColor(i / (double)Mad60Protocol.LedSlots);
                        rgb[i * 3] = c.R; rgb[i * 3 + 1] = c.G; rgb[i * 3 + 2] = c.B;
                    }
                    return _sx.StreamFrameMad60(rgb);
                }))));

            // --- The D: HERO68 HE ---
            _sxCardD = Card(V(10,
                Tb("Họ D — AULA HERO68 HE", "h2"),
                Tb("HERO68 HE dùng kênh riêng kèm checksum; nhận diện bằng lệnh đọc trước khi ghi.", "sub"),
                PrimaryBtn("Nhận diện (lệnh 130 + đọc đèn 132)", async () => await SxProbeAsync("Nhận diện HERO68", () => _sx.ProbeHero68())),
                WriteBtn("Stream 1 frame cầu vồng", async () => await SxWriteAsync("Stream frame HERO68", () =>
                {
                    var keys = new List<(byte Id, byte R, byte G, byte B)>();
                    for (int i = 0; i < Hero68Protocol.KeyIds.Length; i++)
                    {
                        var c = RainbowColor(i / (double)Hero68Protocol.KeyIds.Length);
                        keys.Add((Hero68Protocol.KeyIds[i], c.R, c.G, c.B));
                    }
                    return _sx.StreamFrameHero68(keys);
                }))));

            // --- The E: WIN60 HE ---
            _sxCardE = Card(V(10,
                Tb("Họ E — AULA WIN60 HE", "h2"),
                Tb("WIN60 HE dùng lưới phím riêng trên kênh của hãng; nhận diện trước khi ghi.", "sub"),
                PrimaryBtn("Đọc trạng thái đèn (lệnh 7)", async () => await SxProbeAsync("Đọc đèn WIN60 HE", () => _sx.ProbeWin60He())),
                WriteBtn("Stream 1 frame cầu vồng", async () => await SxWriteAsync("Stream frame WIN60 HE", () =>
                {
                    var rgb = new byte[Win60HeProtocol.FrameBytes];
                    for (int i = 0; i < Win60HeProtocol.GridSlots; i++)
                    {
                        var c = RainbowColor(i / (double)Win60HeProtocol.GridSlots);
                        rgb[i * 3] = c.R; rgb[i * 3 + 1] = c.G; rgb[i * 3 + 2] = c.B;
                    }
                    return _sx.StreamFrameWin60He(rgb);
                }))));

            var root = V(14,
                Tb("Họ SONiX (S75 / MAD60 / HERO68 / WIN60)", "h1"),
                warn,
                idCard,
                _sxCardB,
                _sxCardC,
                _sxCardD,
                _sxCardE);
            return Scroll(root);
        }

        private void RefreshSxPage()
        {
            var m = _selectedModel;
            if (_sxCardB != null) _sxCardB.IsVisible = m.Stack == "B";
            if (_sxCardC != null) _sxCardC.IsVisible = m.Stack == "C";
            if (_sxCardD != null) _sxCardD.IsVisible = m.Stack == "D";
            if (_sxCardE != null) _sxCardE.IsVisible = m.Stack == "E";
            // v3.14: nut stream ho B an voi Nova75 — trinh ghi frame tang toc
            // rieng cua Nova75 chua ro cach chia goi nen app khong dua thao
            // tac stream cho mau nay (S75 Pro dung chung the, stream binh thuong).
            if (_sxBStreamBtn != null) _sxBStreamBtn.IsVisible = !(m.Name ?? "").Contains("Nova");
            if (_sxIdStatus != null)
                _sxIdStatus.Text = _sx.LastId != null
                    ? $"Đã nhận diện: {_sx.LastId.ModelName} (stack {_sx.LastId.Stack}) — {_sx.LastId.Note}"
                    : $"Mẫu đang chọn: {m.Name} (stack {(string.IsNullOrEmpty(m.Stack) ? "—" : m.Stack)}). Chưa chạy probe/nhận diện cho họ này.";
            if (_sxInfoStatus != null)
                _sxInfoStatus.Text = string.IsNullOrEmpty(_sx.LastIdSummary) ? "" : "Probe gần nhất: " + _sx.LastIdSummary;
        }

        private async Task SxProbeAsync(string title, Action work)
        {
            if (_busy) { ShowToast("Đang bận — đợi thao tác trước xong đã."); return; }
            _busy = true;
            try
            {
                await Task.Run(work);
                RefreshSxPage();
                ShowToast("Xong: " + title + " (xem trạng thái + nhật ký).");
            }
            catch (Exception ex) { AppendLog("LỖI: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
            finally { _busy = false; }
        }

        private async Task SxWriteAsync(string title, Func<bool> work)
        {
            if (_busy) { ShowToast("Đang bận — đợi thao tác trước xong đã."); return; }
            _busy = true;
            try
            {
                bool sent = await Task.Run(work);
                RefreshSxPage();
                ShowToast(sent ? $"Đã gửi: {title}" : $"{title}: chưa gửi được — hãy chạy probe/nhận diện của họ trước.");
            }
            catch (Exception ex) { AppendLog("LỖI: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
            finally { _busy = false; }
        }

        // ================= TRANG HO WITMOD (Driver Pack 4, v3.7) =================
        // Nhan dien: quet bang filter 36 cap VID:PID (usagePage 0xFF1B),
        // probe quyet dinh = lenh 0x0D (chuoi san pham CSV, truong 4) +
        // khoi support cua lenh 0x01. Khong qua probe -> chi xem.
        private Control BuildWitModPage()
        {
            var warn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xF5, 0x9B, 0x9B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9B, 0x9B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                Child = Tb("Chưa kiểm chứng trên phần cứng thật — giao thức WitMod được trích byte-level từ web driver chính hãng đã giải mã (docs/PROTOCOLS-WITMOD-FULL.md trong repo). WitMod là nền tảng nhãn trắng dùng chung cho nhiều hãng nên 36 cặp VID:PID trong bảng filter KHÔNG nói lên mẫu máy; chỉ probe 0x0D + 0x01 mới quyết định. VID:PID riêng của K617 HE không có trong code web (app desktop của hãng nhận diện bằng tiền tố productName \"7153\") — app này không đoán. Lệnh ghi chỉ chạy khi thiết bị đã qua probe.", null, 12),
            };

            _wmIdStatus = Tb("Chưa chạy nhận diện WitMod.", "sub");
            _wmInfoStatus = Tb("", "sub");
            var idCard = Card(V(10,
                Tb("Nhận diện (probe 0x0D + 0x01 — chạy thật, chỉ đọc)", "h2"),
                Tb("Quét thiết bị khớp bảng của hãng rồi đọc chuỗi sản phẩm để định danh trước khi ghi.", "sub"),
                _wmIdStatus,
                _wmInfoStatus,
                PrimaryBtn("Nhận diện WitMod", async () => await WmProbeAsync()),
                CopyInfoButton()));

            var lightCard = Card(V(10,
                Tb("Đèn chính (lệnh 0x07)", "h2"),
                Tb("Đọc cấu hình đèn hiện tại (chỉ đọc) hoặc ghi một cấu hình mẫu để kiểm tra.", "sub"),
                GhostBtn("Đọc cấu hình đèn", async () => await WmReadLightAsync()),
                WriteBtn("Ghi cấu hình đèn mẫu (tím)", async () => await SxWriteAsync("Ghi đèn WitMod", () =>
                    _wm.WriteMainLightConfig(new WitModProtocol.LightConfig
                    {
                        Mode = 1, Brightness = 5, Speed = 3,
                        FgR = 0x7C, FgG = 0x3A, FgB = 0xED,
                        BgR = 0, BgG = 0, BgB = 0,
                        Direction = 0, FullColor = 0, Power = 1, Timer = 0,
                    })))));

            var advCard = Card(V(10,
                Tb("Trigger / keymap / deadband (họ lệnh 0x21, 0x18, 0x26)", "h2"),
                Tb("Đọc thử keymap + thông số trigger (chỉ đọc). Ghi deadband nằm ở mục bên dưới.", "sub"),
                GhostBtn("Đọc trang keymap 0 + trigger/poll-rate", async () => await WmReadAdvancedAsync()),
                WriteBtn("Reset deadband (ghi 0 toàn bộ)", async () => await SxWriteAsync("Reset deadband WitMod", () =>
                    _wm.WriteDeadband(new byte[WitModProtocol.DeadbandImageBytes])))));

            var root = V(14,
                Tb("Họ WitMod (Redragon K617 HE)", "h1"),
                warn,
                idCard,
                lightCard,
                advCard);
            return Scroll(root);
        }

        private void RefreshWmPage()
        {
            if (_wmIdStatus != null)
                _wmIdStatus.Text = _wm.LastId != null && _wm.LastId.Known
                    ? $"Đã nhận diện: {_wm.LastId.ModelName} ({_wm.LastId.Vid:X4}:{_wm.LastId.Pid:X4}) — product \"{_wm.LastId.ProductId}\", productName \"{_wm.LastId.ProductName}\"."
                    : "Chưa chạy nhận diện WitMod (hoặc chưa thiết bị nào qua được probe).";
            if (_wmInfoStatus != null)
                _wmInfoStatus.Text = _wm.LastId != null && _wm.LastId.Known
                    ? _wm.LastId.Note
                    : (string.IsNullOrEmpty(_wm.LastIdSummary) ? "" : "Probe gần nhất: " + _wm.LastIdSummary);
        }

        private async Task WmProbeAsync()
        {
            if (_busy) { ShowToast("Đang bận — đợi thao tác trước xong đã."); return; }
            _busy = true;
            try
            {
                await Task.Run(() => _wm.Identify());
                RefreshWmPage();
                ShowToast(_wm.LastId != null && _wm.LastId.Known
                    ? $"Đã nhận diện: {_wm.LastId.ModelName}."
                    : "Không thiết bị WitMod nào qua được probe 0x0D/0x01.");
            }
            catch (Exception ex) { AppendLog("LỖI: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
            finally { _busy = false; }
        }

        private async Task WmReadLightAsync()
        {
            if (_busy) { ShowToast("Đang bận — đợi thao tác trước xong đã."); return; }
            _busy = true;
            try
            {
                var cfg = await Task.Run(() => _wm.ReadMainLightConfig());
                ShowToast(cfg != null ? "Đã đọc cấu hình đèn (xem nhật ký)." : "Chưa đọc được — hãy chạy Nhận diện trước.");
            }
            catch (Exception ex) { AppendLog("LỖI: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
            finally { _busy = false; }
        }

        private async Task WmReadAdvancedAsync()
        {
            if (_busy) { ShowToast("Đang bận — đợi thao tác trước xong đã."); return; }
            _busy = true;
            try
            {
                string a = await Task.Run(() => _wm.ReadKeymapPageSummary());
                string b = await Task.Run(() => _wm.ReadTriggerSummary());
                ShowToast(a != null || b != null ? "Đã đọc keymap/trigger (xem nhật ký)." : "Chưa đọc được — hãy chạy Nhận diện trước.");
            }
            catch (Exception ex) { AppendLog("LỖI: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
            finally { _busy = false; }
        }

        // ================= TRANG HO MCHOSE (Driver Pack 3, v3.6) =================
        // Cac stack cua M HUB: GLW/CZ (Hall-effect, trang nay), QHW (trang
        // nay), BY = kb_by_v2 (dung chung trang Ho Sinowealth — cac model
        // MCHOSE BY chi xem vi khong co UUID). Stack legacy 0x11/0x12 khong
        // con muc rieng tu v3.14 (transport chua ro — app khong dua thao tac).
        // Trang chi hien khi model dang chon co Family == "mchose"; muc HE
        // chi hien voi stack GLW, muc QHW chi hien voi stack QHW (quy tac
        // capability). Khong model MCHOSE nao co man hinh.
        private Control BuildMchosePage()
        {
            var warn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xF5, 0x9B, 0x9B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9B, 0x9B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                Child = Tb("Chưa kiểm chứng trên phần cứng thật — giao thức MCHOSE được tài liệu hoá từ M HUB chính hãng đã giải phẫu (stack BY, GLW/CZ, QHW), nhưng không ai trong đội sở hữu các mẫu phím này. LƯU Ý: checksum của stack GLW là GIẢ THUYẾT tổng-byte-mod-256 — hàm checksum gốc nằm trong một module không có trong các file đã thu thập; nếu giả thuyết sai, firmware sẽ từ chối gói GLW. App luôn chạy bước định danh bằng lệnh đọc trước khi ghi.", null, 12),
            };

            _mcModelInfo = Tb("", "sub");
            _mcIdStatus = Tb("Chưa nhận diện. Hãy cắm phím MCHOSE bằng cáp USB rồi bấm Nhận diện.", "sub");
            var idCard = Card(V(10,
                Tb("1 · Nhận diện thiết bị (bắt buộc trước khi ghi)", "h2"),
                _mcModelInfo,
                Tb("Định danh hai lớp: mã USB + probe đọc từ thiết bị; khớp mới mở các mục ghi.", "sub"),
                PrimaryBtn("Nhận diện thiết bị (probe đọc — chỉ đọc)", async () =>
                {
                    if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
                    _busy = true;
                    try
                    {
                        var (vid, pid, _) = _selectedModel.Ids.Count > 0 ? _selectedModel.Ids[0] : (0, 0, "");
                        var id = await Task.Run(() => _mc.Identify(vid, pid));
                        _mcIdStatus.Text = id.Known
                            ? $"Đã định danh: {id.ModelName} (stack {id.Entry.Stack}). {id.Note}"
                            : $"Chưa định danh được. {id.Note}";
                        ShowToast(id.Known ? $"Đã định danh: {id.ModelName}" : "Không khớp registry — chế độ chỉ xem.");
                    }
                    catch (Exception ex) { AppendLog("LỖI nhận diện MCHOSE: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
                    finally { _busy = false; }
                }),
                _mcIdStatus));

            _mcInfoStatus = Tb("", "sub");
            var readCard = Card(V(10,
                Tb("2 · Đọc thông tin (chỉ đọc — chạy live, cần Nhận diện trước)", "h2"),
                Tb("Stack GLW (Ace/Jet/G-series HE):", "h3"),
                H(10,
                    GhostBtn("Thông tin FW (cmd 3)", async () =>
                    {
                        var reply = await McGlwTransactAsync("getInfo", GlwProtocol.BuildRead(GlwProtocol.CmdGetInfo, 0, 56));
                        if (reply != null)
                        {
                            var info = GlwProtocol.ParseInfo(GlwProtocol.ReplyData(reply));
                            _mcInfoStatus.Text = $"GLW getInfo: FW = {info.FwVer}, FW RF = {info.RfFwVer}, report-rate-enhanced = {info.ReportRateEnhanced}, U3 mode = {info.U3Mode}.";
                        }
                    }),
                    GhostBtn("Thứ tự profile (cmd 4)", async () =>
                    {
                        var reply = await McGlwTransactAsync("getBase", GlwProtocol.BuildRead(GlwProtocol.CmdGetBase, 0, 56));
                        if (reply != null)
                        {
                            var b = GlwProtocol.ParseBase(GlwProtocol.ReplyData(reply));
                            _mcInfoStatus.Text = $"GLW getBase: profile đang dùng = {b.ProfileIndex}, số profile = {b.ProfileLength}, thứ tự = [{string.Join(", ", b.Order)}].";
                        }
                    }),
                    GhostBtn("Cấu hình chức năng (cmd 5)", async () =>
                    {
                        var data = await McGlwReadRegionAsync("getFuncConfig", GlwProtocol.CmdGetFuncConfig, GlwProtocol.FuncConfigOffset(_mcProfile), GlwProtocol.FuncConfigBytes);
                        if (data != null)
                        {
                            var c = GlwProtocol.ParseFuncConfig(data);
                            _mcInfoStatus.Text = $"GLW FuncConfig (profile {_mcProfile}): pin {c.BatteryPercent}%{(c.Charging ? " (đang sạc)" : "")}, chỉ số polling = {c.ReportRateIndex}, vùng chết chung = {c.GlobalDeadZone}, khoá Win = {c.LockWin}, chế độ Mac = {c.MacMode}, RT đáy = {c.BottomRapidTrigger}.";
                        }
                    }),
                    GhostBtn("Đọc vùng trigger (0xA0)", async () =>
                    {
                        var data = await McGlwReadRegionAsync("getKeyTrigger", GlwProtocol.CmdGetKeyTrigger, GlwProtocol.TriggerRegionOffset(_mcProfile), GlwProtocol.TriggerRegionBytes);
                        if (data != null && data.Length >= 8)
                        {
                            var t = GlwProtocol.GlwTrigger.Decode(data, 0);
                            _mcInfoStatus.Text = $"GLW trigger (profile {_mcProfile}, phím 0): hành trình thô = {t.Actuation}, RT nhấn thô = {t.RtPress}, vùng chết nhấn = {t.PressDeadZone}, mode = {t.KeyMode}. Đơn vị là giá trị thô 9-bit (lưu −1) — quy đổi sang mm nằm ở tầng giao diện của hãng và chưa thu hồi được, app không hiển thị mm như sự thật.";
                        }
                    })),
                Tb("Stack QHW (G87 V3 / G98 V3 / K87 V3):", "h3"),
                H(10,
                    GhostBtn("Thông tin nền (0x0900)", async () =>
                    {
                        var data = await McQhwTransactAsync("getBaseInfo", QhwProtocol.CmdGetBaseInfo, null);
                        if (data != null)
                        {
                            var b = QhwProtocol.ParseBaseInfo(data);
                            _mcInfoStatus.Text = $"QHW baseInfo: thiết bị tự khai {b.Vid:X4}:{b.Pid:X4}, số profile tối đa = {b.MaxProfileLength}, pin = {b.BatteryLevel}%, kết nối = {(b.ConnectStatus == 1 ? "có dây" : b.ConnectStatus == 2 ? "không dây" : b.ConnectStatus.ToString())}, macro {(b.MacroShared ? "dùng chung" : "độc lập")}.";
                        }
                    }),
                    GhostBtn("Phiên bản FW (0x0901)", async () =>
                        await McQhwTransactAsync("getFwVersion", QhwProtocol.CmdGetFwVersion, new byte[] { 0 })),
                    GhostBtn("Pin (0x0904)", async () =>
                    {
                        var data = await McQhwTransactAsync("getBattery", QhwProtocol.CmdGetBattery, null);
                        if (data != null)
                        {
                            var (charge, level) = QhwProtocol.ParseBattery(data);
                            _mcInfoStatus.Text = $"QHW pin: {level}% — trạng thái sạc = {charge} (0 = không sạc, 1 = đang sạc, 2 = đầy).";
                        }
                    })),
                _mcInfoStatus));

            var profileSeg = Segmented(new[] { "Profile 0", "Profile 1", "Profile 2", "Profile 3" }, 0, idx =>
            {
                _mcProfile = idx;
                AppendLog($"[MCHOSE] Profile lam viec tren trang MCHOSE: {idx} (offset vung trigger = {GlwProtocol.TriggerRegionOffset(idx)}, FuncConfig = {GlwProtocol.FuncConfigOffset(idx)}).");
            });

            // --- Muc Hall-effect GLW: chi hien voi stack GLW (RefreshMcPage) ---
            var actSeg = Segmented(new[] { "Thô 150", "Thô 250", "Thô 350" }, 1, async idx =>
            {
                int raw = new[] { 150, 250, 350 }[idx];
                var keys = new GlwProtocol.GlwTrigger[GlwProtocol.MaxKeys];
                for (int i = 0; i < keys.Length; i++)
                    keys[i] = new GlwProtocol.GlwTrigger { Actuation = raw, RtPress = raw, RtRelease = raw, PressDeadZone = 10, ReleaseDeadZone = 10 };
                var region = GlwProtocol.BuildTriggerRegion(keys);
                await McWriteAsync($"Ghi vùng trigger đồng nhất (hành trình thô {raw})",
                    () => _mc.GlwWriteRegion("setKeyTrigger", GlwProtocol.CmdSetKeyTrigger, GlwProtocol.TriggerRegionOffset(_mcProfile), region));
            });
            var heContent = V(10,
                Tb("3 · Hall-effect GLW (cmd 0xA0/0xA1 + hiệu chuẩn)", "h2"),
                Tb("Vùng trigger gồm điểm kích hoạt/nhả và các chế độ nâng cao cho từng phím.", "sub"),
                Tb("Profile làm việc (dùng cho cả mục Đọc và mục này):", "sub"),
                profileSeg.Root,
                Tb("Ghi đồng nhất cả 128 phím — CẨN THẬN: ghi đè toàn bộ vùng trigger của profile, kể cả các phím bạn đã tinh chỉnh riêng. Nên đọc vùng trigger hiện tại (mục 2) trước:", "sub"),
                actSeg.Root,
                H(10,
                    PrimaryBtn("Hiệu chuẩn: bắt đầu (168)", async () =>
                        await McWriteAsync("Bắt đầu hiệu chuẩn (168)", () => _mc.GlwWriteSingle("startCalibration", GlwProtocol.BuildCalibration(GlwProtocol.CmdStartCalibration)))),
                    GhostBtn("Hiệu chuẩn: kết thúc (169)", async () =>
                        await McWriteAsync("Kết thúc hiệu chuẩn (169)", () => _mc.GlwWriteSingle("endCalibration", GlwProtocol.BuildCalibration(GlwProtocol.CmdEndCalibration)))),
                    GhostBtn("Hiệu chuẩn: khởi động lại (177)", async () =>
                        await McWriteAsync("Khởi động lại hiệu chuẩn (177)", () => _mc.GlwWriteSingle("reStartCalibration", GlwProtocol.BuildCalibration(GlwProtocol.CmdRestartCalibration)))),
                    GhostBtn("Khôi phục cài đặt gốc (238)", async () =>
                    {
                        if (!await ConfirmAsync("Khôi phục cài đặt gốc?", "Toàn bộ cài đặt trên phím sẽ trở về mặc định của hãng. Thao tác này không hoàn tác được.")) return;
                        await McWriteAsync("Khôi phục cài đặt gốc GLW (238)", () => _mc.GlwWriteSingle("restoreFactorySettings", GlwProtocol.BuildFactoryResetAll()));
                    })));
            _mcHeCard = Card(heContent);

            // --- Muc QHW: chi hien voi stack QHW (RefreshMcPage) ---
            var qhwContent = V(10,
                Tb("3 · QHW: đèn LED + hiệu ứng LED toàn phím", "h2"),
                Tb("Stack QHW không có lệnh trigger trong kênh của hãng — các mục trigger không áp dụng cho stack này.", "sub"),
                H(10,
                    PrimaryBtn("Gửi màu cầu vồng realtime 5 phím đầu (0x0306)", async () =>
                    {
                        var colors = new List<(byte, byte, byte, byte)>();
                        for (int i = 0; i < 5; i++)
                        {
                            var c = KeyboardView.Hsv(i / 5.0, 0.85, 0.9);
                            colors.Add(((byte)i, c.R, c.G, c.B));
                        }
                        var payload = QhwProtocol.BuildKeyColors(colors);
                        await McWriteAsync("Màu realtime QHW (0x0306)", () => _mc.QhwWrite("setKeyColorsRealtime", QhwProtocol.CmdSetKeyColorsRealtime, payload));
                    }),
                    GhostBtn("Khôi phục cài đặt gốc (0x030C)", async () =>
                    {
                        if (!await ConfirmAsync("Khôi phục cài đặt gốc?", "Toàn bộ cài đặt trên phím sẽ trở về mặc định của hãng. Thao tác này không hoàn tác được.")) return;
                        await McWriteAsync("Khôi phục cài đặt gốc QHW (0x030C)", () => _mc.QhwWrite("restoreFactorySettings", QhwProtocol.CmdRestore, QhwProtocol.RestoreAllPayload(), 8000));
                    })));
            _mcQhwCard = Card(qhwContent);

            var root = V(14,
                Tb("Họ MCHOSE (Driver Pack 3)", "h1"),
                warn,
                idCard,
                readCard,
                _mcHeCard,
                _mcQhwCard);
            return Scroll(root);
        }

        /// <summary>Lam moi trang MCHOSE theo model dang chon: an/hien muc HE (GLW) va muc QHW theo stack.</summary>
        private void RefreshMcPage()
        {
            var m = _selectedModel;
            if (_mcHeCard != null) _mcHeCard.IsVisible = m.Family == "mchose" && m.Stack == "GLW";
            if (_mcQhwCard != null) _mcQhwCard.IsVisible = m.Family == "mchose" && m.Stack == "QHW";
            if (_mcModelInfo != null)
            {
                string ids = m.Ids.Count > 0
                    ? string.Join(", ", m.Ids.Select(t => $"{t.Vid:X4}:{t.Pid:X4} ({t.Conn})"))
                    : "(chưa có VID:PID trong registry)";
                _mcModelInfo.Text = $"Model đang chọn: {m.Name} — stack {m.Stack}, {(m.HasHE ? "Hall-effect" : "không có lệnh HE trong kênh vendor")}, không màn hình. VID:PID đã biết: {ids}. {m.IdNote}";
            }
        }

        private async Task<byte[]> McGlwTransactAsync(string title, byte[] packet)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return null; }
            _busy = true;
            try { return await Task.Run(() => _mc.GlwTransact(title, packet)); }
            catch (Exception ex) { AppendLog("LỖI đọc MCHOSE GLW: " + ex.Message); ShowToast("Lỗi: " + ex.Message); return null; }
            finally { _busy = false; }
        }

        private async Task<byte[]> McGlwReadRegionAsync(string title, byte cmd, int baseOffset, int total)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return null; }
            _busy = true;
            try { return await Task.Run(() => _mc.GlwReadRegion(title, cmd, baseOffset, total)); }
            catch (Exception ex) { AppendLog("LỖI đọc MCHOSE GLW: " + ex.Message); ShowToast("Lỗi: " + ex.Message); return null; }
            finally { _busy = false; }
        }

        private async Task<byte[]> McQhwTransactAsync(string title, ushort cmd, byte[] payload)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return null; }
            _busy = true;
            try
            {
                var data = await Task.Run(() => _mc.QhwTransact(title, cmd, payload));
                if (_mcInfoStatus != null)
                    _mcInfoStatus.Text = $"QHW {title}: nhận {data.Length} byte payload: {string.Join(" ", data.Take(16).Select(b => b.ToString("X2")))}…";
                return data;
            }
            catch (Exception ex) { AppendLog("LỖI đọc MCHOSE QHW: " + ex.Message); ShowToast("Lỗi: " + ex.Message); return null; }
            finally { _busy = false; }
        }

        private async Task McWriteAsync(string title, Func<bool> send)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
            _busy = true;
            try
            {
                bool sent = await Task.Run(send);
                ShowToast(sent ? $"Đã gửi xuống phím: {title}" : $"{title}: chưa gửi được — hãy bấm Nhận diện thiết bị trước.");
            }
            catch (Exception ex) { AppendLog("LỖI ghi MCHOSE: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
            finally { _busy = false; }
        }

        // ================= TRANG HO DAREU (Driver Pack 5) =================
        // Trang chi hien khi model dang chon co Family == "dareu"; muc
        // Magnetic Axis chi hien voi model TG-RT (HasHE), muc OHID/JM chi
        // hien voi dung ho cua model (quy tac capability).
        private Control BuildDareUPage()
        {
            var warn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xF5, 0x9B, 0x9B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9B, 0x9B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                Child = Tb("Chưa kiểm chứng trên phần cứng thật — giao thức DareU được trích byte-level từ WEB driver chính hãng dr.dareu.com (bản cài desktop của hãng CHƯA BAO GIỜ được kiểm toán: CDN của hãng chặn mọi lần tải thử). PID lạ không có trong registry 187 mục → chỉ xem.", null, 12),
            };

            _duModelInfo = Tb("", "sub");
            _duIdStatus = Tb("Chưa nhận diện. Hãy cắm phím DareU bằng cáp USB rồi bấm Nhận diện.", "sub");
            var idCard = Card(V(10,
                Tb("1 · Nhận diện thiết bị (bắt buộc trước khi ghi)", "h2"),
                _duModelInfo,
                Tb("Định danh bằng mã USB → danh sách của hãng → họ giao thức tương ứng.", "sub"),
                PrimaryBtn("Nhận diện thiết bị (mở + probe đọc — chỉ đọc)", async () =>
                {
                    if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
                    _busy = true;
                    try
                    {
                        var (vid, pid, _) = _selectedModel.Ids.Count > 0 ? _selectedModel.Ids[0] : (0x260D, 0, "");
                        bool opened = await Task.Run(() => _du.OpenByVidPid(vid, pid));
                        if (opened && _du.Identified)
                        {
                            string probe = await Task.Run(() => _du.ProbeRead());
                            _duIdStatus.Text = $"Đã định danh: {_du.IdentifiedEntry.DisplayName} — họ {_du.IdentifiedEntry.Family} (FwType {_du.IdentifiedEntry.FwType}), model nội bộ {_du.IdentifiedEntry.InternalModel}. {probe}";
                            ShowToast($"Đã định danh: {_du.IdentifiedEntry.DisplayName}");
                        }
                        else if (opened)
                        {
                            _duIdStatus.Text = "Đã mở thiết bị nhưng PID không có trong registry 187 mục — CHẾ ĐỘ CHỈ XEM, cấm ghi. Hãy dùng nút sao chép thông tin thiết bị để bổ sung registry.";
                            ShowToast("PID lạ — chế độ chỉ xem.");
                        }
                        else
                        {
                            _duIdStatus.Text = "Không mở được thiết bị. Kiểm tra cáp USB và VID:PID của model đang chọn.";
                            ShowToast("Không mở được thiết bị DareU.");
                        }
                        RefreshDareUPage();
                    }
                    catch (Exception ex) { AppendLog("LỖI nhận diện DareU: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
                    finally { _busy = false; }
                }),
                _duIdStatus,
                CopyInfoButton()));

            _duInfoStatus = Tb("", "sub");
            var tgCard = Card(V(10,
                Tb("2 · Họ TG / HS: đèn LED + đọc trạng thái", "h2"),
                Tb("Khung lệnh TG đã trích từ driver web chính hãng; các mục ghi nằm bên dưới.", "sub"),
                H(10,
                    GhostBtn("Đọc lại probe (FW / version / CID)", async () =>
                    {
                        if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
                        _busy = true;
                        try { _duInfoStatus.Text = await Task.Run(() => _du.ProbeRead()); }
                        catch (Exception ex) { AppendLog("LỖI probe DareU: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
                        finally { _busy = false; }
                    }),
                    PrimaryBtn("Gửi LED EFFECT mẫu (hiệu ứng thô 1, tím, tốc độ 2)", async () =>
                        await DuWriteAsync("TG LED EFFECT mẫu", () => _du.SetLedEffect(1, 0, 2, (0x7C, 0x3A, 0xED))))),
                _duInfoStatus));

            // --- Muc Magnetic Axis: chi hien voi model TG-RT (RefreshDareUPage) ---
            var travelSeg = Segmented(new[] { "0.5 mm", "1.0 mm", "1.5 mm", "2.0 mm", "3.0 mm" }, 2, async idx =>
            {
                double mm = new[] { 0.5, 1.0, 1.5, 2.0, 3.0 }[idx];
                await DuWriteAsync($"Hành trình toàn cục {mm} mm", () => _du.SetGlobalTravel(mm));
            });
            var heContent = V(10,
                Tb("3 · Magnetic Axis (lớp 13 — chỉ TG-RT)", "h2"),
                Tb("Hành trình phím mã hoá theo từng model (bước và dải mm khác nhau) — app tự dùng đúng thông số của model đã định danh.", "sub"),
                Tb("Hành trình toàn cục (áp cho mọi phím):", "sub"),
                travelSeg.Root,
                H(10,
                    GhostBtn("Đọc hành trình toàn cục hiện tại", async () =>
                    {
                        if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
                        _busy = true;
                        try
                        {
                            var mm = await Task.Run(() => _du.ReadGlobalTravel());
                            _duInfoStatus.Text = mm.HasValue ? $"Hành trình toàn cục hiện tại ≈ {mm.Value:0.00} mm." : "Không đọc được hành trình.";
                        }
                        catch (Exception ex) { AppendLog("LỖI đọc hành trình DareU: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
                        finally { _busy = false; }
                    }),
                    GhostBtn("Single Trigger phím 0 = 1.0 mm", async () =>
                        await DuWriteAsync("Single Trigger phím 0", () => _du.SetSingleTrigger(0, 1.0))),
                    GhostBtn("Rapid Trigger phím 0 (0.5 / 0.3 / 0.3 mm)", async () =>
                        await DuWriteAsync("Rapid Trigger phím 0", () => _du.SetRapidTrigger(0, 0.5, 0.3, 0.3)))),
                H(10,
                    PrimaryBtn("Hiệu chuẩn: VÀO hiệu chuẩn", async () =>
                        await DuWriteAsync("Vào hiệu chuẩn hành trình", () => _du.TravelCalibrationEnter())),
                    GhostBtn("Hiệu chuẩn: THOÁT hiệu chuẩn", async () =>
                        await DuWriteAsync("Thoát hiệu chuẩn hành trình", () => _du.TravelCalibrationExit())),
                    GhostBtn("Game Mode: Bật", async () =>
                        await DuWriteAsync("Game Mode bật", () => _du.SetGameMode(true))),
                    GhostBtn("Game Mode: Tắt", async () =>
                        await DuWriteAsync("Game Mode tắt", () => _du.SetGameMode(false)))));
            _duHeCard = Card(heContent);

            // --- Muc OHID: chi hien voi model OHID (RefreshDareUPage) ---
            var ohidContent = V(10,
                Tb("3 · OHID: trigger + lưu cấu hình", "h2"),
                Tb("Khung OHID có checksum riêng; hầu hết lệnh ghi phải kèm trạng thái khoá của phím.", "sub"),
                H(10,
                    PrimaryBtn("Single Trigger phím 0 (travel thô LE 500)", async () =>
                        await DuWriteAsync("OHID Single Trigger phím 0", () =>
                            _du.OhidSetSingleTrigger(new List<(byte, ushort)> { (0, 500) }))),
                    GhostBtn("RT phím 0 (giá trị thô LE24 500)", async () =>
                        await DuWriteAsync("OHID RT phím 0", () =>
                            _du.OhidSetRt(new List<(byte, int)> { (0, 500) }))),
                    GhostBtn("Khôi phục cài đặt gốc (ORDER FACTORY)", async () =>
                    {
                        if (!await ConfirmAsync("Khôi phục cài đặt gốc?", "Toàn bộ cài đặt trên phím sẽ trở về mặc định của hãng. Thao tác này không hoàn tác được.")) return;
                        await DuWriteAsync("OHID khôi phục cài đặt gốc", () => _du.OhidFactoryReset());
                    })));
            _duOhidCard = Card(ohidContent);

            // --- Muc JM/QF: chi hien voi model JM/QF (RefreshDareUPage) ---
            var jmContent = V(10,
                Tb("3 · JM / QF: truyền buffer theo địa chỉ", "h2"),
                Tb("Khung ngắn 16 byte với checksum theo công thức của hãng.", "sub"),
                H(10,
                    PrimaryBtn("Ghi buffer mẫu 20 byte @0x0000", async () =>
                        await DuWriteAsync("JM ghi buffer mẫu", () =>
                            _du.JmWriteBuffer(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 })))));
            _duJmCard = Card(jmContent);

            var root = V(14,
                Tb("Họ DareU (Driver Pack 5)", "h1"),
                warn,
                idCard,
                tgCard,
                _duHeCard,
                _duOhidCard,
                _duJmCard);
            return Scroll(root);
        }

        private void RefreshDareUPage()
        {
            var m = _selectedModel;
            if (_duHeCard != null) _duHeCard.IsVisible = m.Family == "dareu" && m.HasHE;
            if (_duOhidCard != null) _duOhidCard.IsVisible = m.Family == "dareu" && m.Stack == "OHID";
            if (_duJmCard != null) _duJmCard.IsVisible = m.Family == "dareu" && (m.Stack == "JM" || m.Stack == "QF");
            if (_duModelInfo != null)
            {
                string ids = m.Ids.Count > 0
                    ? string.Join(", ", m.Ids.Select(t => $"{t.Vid:X4}:{t.Pid:X4} ({t.Conn})"))
                    : "(chưa có VID:PID trong registry)";
                _duModelInfo.Text = $"Model đang chọn: {m.Name} — họ {m.Stack}, {(m.HasHE ? "Hall-effect (Magnetic Axis)" : "cơ / không HE")}, không màn hình LCD. VID:PID đã biết: {ids}. {m.IdNote}";
            }
        }

        private async Task DuWriteAsync(string title, Action send)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
            _busy = true;
            try
            {
                await Task.Run(send);
                ShowToast(_du.Identified ? $"Đã gửi xuống phím: {title}" : $"{title}: chưa gửi được — hãy bấm Nhận diện thiết bị trước.");
            }
            catch (Exception ex) { AppendLog("LỖI ghi DareU: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
            finally { _busy = false; }
        }

        // ================= TRANG HO AULA HUB (Driver Pack 5) =================
        // Trang chi hien khi model dang chon co Family == "aulahub"; cac muc
        // HFD / SparkLink V1 / SparkLink V2 chi hien voi dung ho cua model.
        private Control BuildAulaHubPage()
        {
            var warn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xF5, 0x9B, 0x9B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9B, 0x9B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10),
                Child = Tb("Chưa kiểm chứng trên phần cứng thật — các họ HFD / SparkLink trích từ client AULA Hub 1.1.22 + web hub đã giải phẫu. Trang này CHỈ gồm các họ chưa có ở các pack trước (RLW = WIN60 HE và Bytech HE = HERO68 HE đã ở Pack 4; Sinowealth ở Pack 1). Lệnh ghi chỉ chạy khi thiết bị đã định danh khớp registry.", null, 12),
            };

            _ahModelInfo = Tb("", "sub");
            _ahIdStatus = Tb("Chưa nhận diện. Hãy cắm phím bằng cáp USB rồi bấm Nhận diện.", "sub");
            var idCard = Card(V(10,
                Tb("1 · Nhận diện thiết bị (bắt buộc trước khi ghi)", "h2"),
                _ahModelInfo,
                Tb("Định danh bằng mã USB + usage page theo danh sách AULA Hub.", "sub"),
                PrimaryBtn("Nhận diện thiết bị (mở + probe đọc — chỉ đọc)", async () =>
                {
                    if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
                    _busy = true;
                    try
                    {
                        AulaHubEntry found = null;
                        foreach (var e in AulaHubRegistryData.Entries)
                        {
                            if (!string.Equals(e.DisplayName, _selectedModel.Name, StringComparison.OrdinalIgnoreCase)) continue;
                            if (!_selectedModel.Ids.Any(t => t.Vid == e.Vid && t.Pid == e.Pid)) continue;
                            found = e; break;
                        }
                        if (found == null)
                        {
                            _ahIdStatus.Text = "Model đang chọn không có mục registry AULA Hub khớp — chỉ xem.";
                            ShowToast("Không có mục registry khớp.");
                            return;
                        }
                        bool opened = await Task.Run(() => _ah.Open(found));
                        if (opened && _ah.Identified)
                        {
                            string probe = await Task.Run(() => _ah.ProbeRead());
                            _ahIdStatus.Text = $"Đã định danh: {found.DisplayName} — họ {found.Family}, usage page 0x{found.UsagePage:X4}, report {_ah.ReportLength} byte. {probe}";
                            ShowToast($"Đã định danh: {found.DisplayName}");
                        }
                        else
                        {
                            _ahIdStatus.Text = "Không mở được interface khớp usage page của model đang chọn.";
                            ShowToast("Không mở được thiết bị AULA Hub.");
                        }
                        RefreshAulaHubPage();
                    }
                    catch (Exception ex) { AppendLog("LỖI nhận diện AULA Hub: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
                    finally { _busy = false; }
                }),
                _ahIdStatus,
                CopyInfoButton()));

            _ahInfoStatus = Tb("", "sub");
            var readCard = Card(V(10,
                Tb("2 · Đọc thông tin (chỉ đọc — chạy live, cần Nhận diện trước)", "h2"),
                H(10,
                    GhostBtn("Chạy lại probe đọc của họ hiện tại", async () =>
                    {
                        if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
                        _busy = true;
                        try { _ahInfoStatus.Text = await Task.Run(() => _ah.ProbeRead()); }
                        catch (Exception ex) { AppendLog("LỖI probe AULA Hub: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
                        finally { _busy = false; }
                    })),
                _ahInfoStatus));

            // --- Muc HFD: chi hien voi model HFD (RefreshAulaHubPage) ---
            var hfdContent = V(10,
                Tb("3 · HFD: đèn, per-key, RT, màn hình TFT", "h2"),
                Tb("Khung HFD là họ lớn nhất của AULA Hub; chi tiết lệnh nằm ở các mục bên dưới.", "sub"),
                H(10,
                    PrimaryBtn("Đặt hiệu ứng LED tĩnh (mode 1, tím)", async () =>
                        await AhWriteAsync("HFD LED tĩnh", () => _ah.HfdSetLedEffect(1, 4, 2, (0x7C, 0x3A, 0xED)))),
                    GhostBtn("Per-key: phủ tím toàn bộ 128 ô", async () =>
                        await AhWriteAsync("HFD per-key tím", () =>
                        {
                            var colors = new List<(byte, byte, byte)>();
                            for (int i = 0; i < 128; i++) colors.Add((0x7C, 0x3A, 0xED));
                            _ah.HfdSetPerKey(colors);
                        })),
                    GhostBtn("Ghi bản ghi RT ô 0 (1.5 / 0.5 / 0.5 mm)", async () =>
                        await AhWriteAsync("HFD RT ô 0", () => _ah.HfdSetRtRecord(0, true, 1.5, 0.5, 0.5)))),
                Tb("Nạp animation TFT (NOVA98) cần giao diện phụ riêng — đang hoàn thiện, hiện xem trước là chính.", "sub"));
            _ahHfdCard = Card(hfdContent);

            // --- Muc SparkLink V1: chi hien voi model SLV1 (RefreshAulaHubPage) ---
            var slv1Content = V(10,
                Tb("3 · SparkLink V1 (KB2): đèn bền + hành trình", "h2"),
                Tb("Per-key bền + trigger của họ này đã có bộ dựng lệnh; gửi thật vẫn qua cổng an toàn chung.", "sub"),
                H(10,
                    PrimaryBtn("Đặt khối đèn PRGB (tĩnh, tím, sáng 4/5)", async () =>
                        await AhWriteAsync("SparkLink V1 PRGB", () =>
                        {
                            var colors = new List<(byte, byte, byte)>();
                            for (int i = 0; i < 7; i++) colors.Add((0x7C, 0x3A, 0xED));
                            _ah.SlV1SetPrgb(colors, 4, 0, 3);
                        })),
                    GhostBtn("Per-key: phím 0 tím + lưu", async () =>
                        await AhWriteAsync("SparkLink V1 per-key phím 0", () =>
                            _ah.SlV1SetPerKey(new List<(byte, byte, byte, byte)> { (0, 0x7C, 0x3A, 0xED) }))),
                    GhostBtn("Hành trình 1.5 mm (DB)", async () =>
                        await AhWriteAsync("SparkLink V1 DB 1.5 mm", () => _ah.SlV1SetGlobalTravel(1.5, 0.1, 0.1))),
                    GhostBtn("Lưu tham số (ORDER 2)", async () =>
                        await AhWriteAsync("SparkLink V1 lưu tham số", () => _ah.SlV1SaveParams()))));
            _ahSlV1Card = Card(slv1Content);

            // --- Muc SparkLink V2: chi hien voi model SLV2 (RefreshAulaHubPage) ---
            var slv2Content = V(10,
                Tb("3 · SparkLink V2: đèn, per-key, stream DirectDrive, trigger", "h2"),
                Tb("Ma trận phím 6×21; các mục bên dưới thao tác theo từng vùng của ma trận.", "sub"),
                H(10,
                    PrimaryBtn("Đặt đèn nền (tĩnh, tím, sáng 80/100)", async () =>
                        await AhWriteAsync("SparkLink V2 đèn nền", () => _ah.SlV2SetLightingBase(0, 1, 0, 80, 3))),
                    GhostBtn("Per-key bền: chunk 0 phủ tím", async () =>
                        await AhWriteAsync("SparkLink V2 per-key chunk 0", () =>
                        {
                            var cells = new List<(byte, byte, byte, bool)>();
                            for (int i = 0; i < 15; i++) cells.Add((0x7C, 0x3A, 0xED, true));
                            _ah.SlV2SetPerKeyChunk(0, 0, cells);
                        })),
                    GhostBtn("Stream DirectDrive: chunk 0 tím (1 khung)", async () =>
                        await AhWriteAsync("SparkLink V2 DirectDrive chunk 0", () =>
                        {
                            var cells = new List<(byte, byte, byte, bool)>();
                            for (int i = 0; i < 15; i++) cells.Add((0x7C, 0x3A, 0xED, true));
                            _ah.SlV2StreamChunk(0, 0, cells);
                        })),
                    GhostBtn("Trigger ô (0,0): RT 1.5 / 0.5 mm", async () =>
                        await AhWriteAsync("SparkLink V2 trigger (0,0)", () => _ah.SlV2SetTrigger(0, 0, 1, 1.5, 1.5, 0.5, 0.5))),
                    GhostBtn("Lưu tham số (Global 2)", async () =>
                        await AhWriteAsync("SparkLink V2 lưu tham số", () => _ah.SlV2SaveParams()))));
            _ahSlV2Card = Card(slv2Content);

            var root = V(14,
                Tb("Họ AULA Hub (Driver Pack 5)", "h1"),
                warn,
                idCard,
                readCard,
                _ahHfdCard,
                _ahSlV1Card,
                _ahSlV2Card);
            return Scroll(root);
        }

        private void RefreshAulaHubPage()
        {
            var m = _selectedModel;
            if (_ahHfdCard != null) _ahHfdCard.IsVisible = m.Family == "aulahub" && m.Stack == "HFD";
            if (_ahSlV1Card != null) _ahSlV1Card.IsVisible = m.Family == "aulahub" && m.Stack == "SLV1";
            if (_ahSlV2Card != null) _ahSlV2Card.IsVisible = m.Family == "aulahub" && m.Stack == "SLV2";
            if (_ahModelInfo != null)
            {
                string ids = m.Ids.Count > 0
                    ? string.Join(", ", m.Ids.Select(t => $"{t.Vid:X4}:{t.Pid:X4} ({t.Conn})"))
                    : "(chưa có VID:PID trong registry)";
                _ahModelInfo.Text = $"Model đang chọn: {m.Name} — họ {m.Stack}, {(m.HasHE ? "Hall-effect" : "cơ")}{(m.HasScreen ? ", màn hình " + (m.ScreenW > 0 ? $"{m.ScreenW}×{m.ScreenH}" : "dot-matrix") : ", không màn hình")}. VID:PID đã biết: {ids}. {m.IdNote}";
            }
        }

        private async Task AhWriteAsync(string title, Action send)
        {
            if (_busy) { ShowToast("Đang bận thao tác trước — đợi một chút nhé."); return; }
            _busy = true;
            try
            {
                await Task.Run(send);
                ShowToast(_ah.Identified ? $"Đã gửi xuống phím: {title}" : $"{title}: chưa gửi được — hãy bấm Nhận diện thiết bị trước.");
            }
            catch (Exception ex) { AppendLog("LỖI ghi AULA Hub: " + ex.Message); ShowToast("Lỗi: " + ex.Message); }
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
                var pv = new EffectPreview { Effect = idx, BaseColor = _rgb.Brush, Multi = _rgb.Multi };
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
            SelectEffectVisual(_rgb.Effect, previews, cards);
            _rgbPreviews = previews; // v3.15: lich trinh can dong bo lai hinh
            _rgbCards = cards;

            // v3.12: bo chon mau TU DO (o SV + thanh hue + nhap RGB/hex +
            // 9 mau mac dinh + mau gan day) thay hang o mau co dinh.
            var swatchColors = new (string Name, Color C)[]
            {
                ("Tím", Color.FromRgb(0x7C, 0x3A, 0xED)), ("Cyan", Color.FromRgb(0x22, 0xD3, 0xEE)),
                ("Hồng", Color.FromRgb(0xF4, 0x72, 0xB6)), ("Đỏ", Color.FromRgb(0xEF, 0x44, 0x44)),
                ("Cam", Color.FromRgb(0xF9, 0x73, 0x16)), ("Vàng", Color.FromRgb(0xFA, 0xCC, 0x15)),
                ("Lục", Color.FromRgb(0x34, 0xD3, 0x99)), ("Dương", Color.FromRgb(0x3B, 0x82, 0xF6)),
                ("Trắng", Color.FromRgb(0xF8, 0xFA, 0xFC)),
            };
            // v3.13: CHE DO MAU lo ro — Da sac (co nhieu mau byte[8]=1,
            // firmware tu rai cau vong) / Don sac (mau da chon). Chon
            // hieu ung moi thi che do nhay ve mac dinh cua hieu ung do
            // (xem SelectEffect); nguoi dung doi tay thi lua chon do
            // duoc luu trong trang thai den nhu truong Multi truoc day.
            var (colorModeSeg, selectColorMode) = Segmented(
                new[] { "Đơn sắc", "Đa sắc" }, _rgb.Multi ? 1 : 0, i =>
                {
                    _rgb.Multi = i == 1;
                    foreach (var p in previews) { p.Multi = _rgb.Multi; p.InvalidateVisual(); }
                    kb.InvalidateVisual();
                    QueuePersistLighting();
                    ShowToast(_rgb.Multi
                        ? "Đa sắc: firmware tự rải nhiều màu cho hiệu ứng."
                        : "Đơn sắc: dùng đúng màu đã chọn.");
                });
            _selectFwColorMode = selectColorMode;
            var picker = new ColorPickerPanel(swatchColors);
            picker.SetColor(_rgb.Brush);
            picker.SetRecents(RecentColorList());
            picker.ColorChanged += c =>
            {
                _rgb.Brush = c;
                if (_rgb.Multi)
                {
                    _rgb.Multi = false;
                    selectColorMode(0);
                    foreach (var p in previews) p.Multi = false;
                }
                foreach (var p in previews) p.BaseColor = c;
                kb.InvalidateVisual();
                QueuePersistLighting();
            };
            picker.ColorCommitted += c =>
            {
                _ui.RecentColors = ColorMath.PushRecent(_ui.RecentColors, ColorMath.ToHex(c.R, c.G, c.B));
                picker.SetRecents(RecentColorList());
                PersistLightingNow();
            };
            var colorRow = H(16, picker, V(8, Tb("Chế độ màu", "sub"), colorModeSeg));
            colorRow.Children[1].VerticalAlignment = VerticalAlignment.Top;

            var bright = new Slider { Minimum = 0, Maximum = 4, Value = _rgb.Brightness, Width = 210 };
            bright.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _rgb.Brightness = bright.Value; QueuePersistLighting(); }
            };
            var speed = new Slider { Minimum = 0, Maximum = 4, Value = _rgb.Speed, Width = 210 };
            speed.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _rgb.Speed = speed.Value; QueuePersistLighting(); }
            };
            var sliderGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = 26 };
            sliderGrid.Children.Add(V(4, Tb("Độ sáng", "sub"), bright));
            var spCol = V(4, Tb("Tốc độ", "sub"), speed);
            Grid.SetColumn(spCol, 1);
            sliderGrid.Children.Add(spCol);

            var (dirSeg, _) = Segmented(new[] { "Phải", "Xuống", "Trái", "Lên" }, _rgb.Direction, i => { _rgb.Direction = i; kb.InvalidateVisual(); QueuePersistLighting(); });
            var dirRow = H(10, Tb("Hướng chạy:", "sub"), dirSeg);
            dirRow.Children[0].VerticalAlignment = VerticalAlignment.Center;

            var applyBtn = WriteBtn("Áp dụng xuống phím", async () =>
                await ApplyRgbAsync(_rgb.Effect, _rgb.Brush, _rgb.Multi, (int)_rgb.Brightness, (int)_rgb.Speed, _rgb.Direction));
            applyBtn.HorizontalAlignment = HorizontalAlignment.Left;

            // --- v3.15: lich trinh den theo gio ---
            _schedPanel = new StackPanel { Spacing = 6 };
            RefreshScheduleRows();
            var schedCard = Card(V(10,
                Tb("Lịch trình đèn theo giờ", "h3"),
                SettingRow("Bật lịch trình", "Đến giờ đã đặt, app tự áp độ sáng / hiệu ứng / scheme / tắt đèn. Đổi đèn bằng tay sẽ thắng cho đến luật tiếp theo.", LightingState.ScheduleEnabled, v =>
                {
                    LightingState.ScheduleEnabled = v;
                    SaveSchedule();
                    ShowToast(v ? "Đã bật lịch trình đèn." : "Đã tắt lịch trình đèn.");
                }),
                _schedPanel,
                GhostBtn("Thêm luật", () =>
                {
                    LightingState.ScheduleRules.Add(new ScheduleRuleData
                    { StartMinutes = 22 * 60, Kind = ScheduleRuleData.KindBrightness, BrightnessPercent = 40 });
                    SaveSchedule();
                    RefreshScheduleRows();
                }),
                Tb("Mỗi luật: giờ bắt đầu (HH:mm) · loại (Độ sáng / Hiệu ứng / Tắt đèn / Scheme đèn) · giá trị (% độ sáng 0–100, số hiệu ứng 0–19 hoặc tên scheme). Luật sau ghi đè luật trước theo giờ.", "faint")));

            var root = V(14,
                Tb("Ánh sáng RGB", "h1"),
                Tb("Chọn hiệu ứng để xem trên bàn phím mô phỏng; “Áp dụng xuống phím” gửi hiệu ứng xuống F75 Max.", "sub"),
                Card(kb, 14),
                Tb("Hiệu ứng firmware (20)", "h2"),
                wrap,
                Card(V(12,
                    Tb("Màu chính", "h3"), colorRow,
                    sliderGrid,
                    dirRow,
                    applyBtn)),
                schedCard);
            return Scroll(root);
        }

        /// <summary>v3.12: mau gan day (hex trong settings) -> Color de
        /// dua vao ColorPickerPanel.</summary>
        private List<Color> RecentColorList()
        {
            var list = new List<Color>();
            foreach (var h in _ui.RecentColors ?? new List<string>())
                if (ColorMath.TryParseHex(h, out var c)) list.Add(Color.FromRgb(c.R, c.G, c.B));
            return list;
        }

        // v3.13: ham chon lai cua segmented Che do mau o trang RGB
        // (de SelectEffect nhay che do ve mac dinh cua hieu ung).
        private Action<int> _selectFwColorMode;

        private void SelectEffect(int idx, List<EffectPreview> previews, List<Border> cards)
        {
            _rgb.Effect = idx;
            // v3.13: hieu ung dong mac dinh Da sac, Tat/Tinh mac dinh
            // Don sac — nhay che do theo hieu ung vua chon.
            bool defMulti = ColorModes.DefaultMultiForEffect(idx);
            if (_rgb.Multi != defMulti)
            {
                _rgb.Multi = defMulti;
                _selectFwColorMode?.Invoke(defMulti ? 1 : 0);
            }
            foreach (var p in previews) { p.Multi = _rgb.Multi; p.InvalidateVisual(); }
            SelectEffectVisual(idx, previews, cards);
            if (_homeFxValue != null) _homeFxValue.Text = EffectFriendlyNames[idx];
            QueuePersistLighting();
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
                new[] { "Visualizer", "Nhịp firmware", "Feed phổ" }, 0,
                i =>
                {
                    _musicMode = i;
                    if (_musicPlaying) RestartMusicSend();
                    ShowToast(i == 0 ? "Visualizer: cột sóng trong app và stream xuống đèn phím qua 0x20."
                        : i == 1 ? "Nhịp firmware: pulse độ sáng theo bass bằng hiệu ứng có sẵn trong phím."
                        : "Feed phổ: gửi 23 dải phổ xuống phím qua 0x78.");
                });

            var gain = new Slider { Minimum = 0.4, Maximum = 1.8, Value = _music.MusicGain, Width = 230 };
            gain.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _music.MusicGain = gain.Value; QueuePersistLighting(); }
            };
            var (bgSeg, _) = Segmented(new[] { "Tắt", "Mờ", "Đầy đủ" }, _music.MusicBg, i => { _music.MusicBg = i; QueuePersistLighting(); });

            // v3.12: mo khoa tham so 0x78 (builder da co san cac truong) —
            // kieu nhip (15 kieu), bien do (scale), do sang nen. Do nhay +
            // che do nen o tren cung tac dong that vao feed 0x78.
            var styleBox = new ComboBox { Width = 230, ItemsSource = L99Protocol.AudioStyleNames, SelectedIndex = _rhythmStyle };
            styleBox.SelectionChanged += (s, e) =>
            {
                if (styleBox.SelectedIndex >= 0) { _rhythmStyle = styleBox.SelectedIndex; QueuePersistLighting(); if (_musicPlaying && _musicMode == 2) RestartMusicSend(); }
            };
            var amp = new Slider { Minimum = 10, Maximum = 100, Value = _rhythmAmplitude, Width = 230 };
            amp.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _rhythmAmplitude = (int)Math.Round(amp.Value); QueuePersistLighting(); }
            };
            var bgBright = new Slider { Minimum = 0, Maximum = 100, Value = _rhythmBgBrightness, Width = 230 };
            bgBright.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _rhythmBgBrightness = (int)Math.Round(bgBright.Value); QueuePersistLighting(); }
            };

            var controlCard = Card(V(12,
                Tb("Điều khiển", "h2"),
                _musicPlayBtn,
                V(4, Tb("Chế độ gửi:", "sub"), modeSeg),
                V(4, Tb("Độ nhạy (biên độ cột sóng)", "sub"), gain),
                V(4, Tb("Chế độ nền:", "sub"), bgSeg),
                V(4, Tb("Kiểu nhịp (Feed phổ 0x78):", "sub"), styleBox),
                V(4, Tb("Biên độ phổ (Feed phổ 0x78)", "sub"), amp),
                V(4, Tb("Độ sáng nền (Feed phổ 0x78)", "sub"), bgBright)));

            var howCard = Card(V(8,
                Tb("Hoạt động thế nào?", "h2"),
                Tb("App bắt âm thanh hệ thống (WASAPI) và biến thành cột sóng trên phím; không bắt được thì dùng phổ mô phỏng.", "sub"),
                Tb("Visualizer stream qua 0x20, Feed phổ qua 0x78 — cả hai gửi thẳng xuống đèn phím khi đang phát.", "sub")));

            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 14 };
            cols.Children.Add(controlCard);
            Grid.SetColumn(howCard, 1);
            cols.Children.Add(howCard);

            var root = V(14,
                Tb("Theo nhạc", "h1"),
                Tb("Bàn phím nhảy theo nhạc — bắt nhạc thật từ máy tính của bạn.", "sub"),
                chip,
                Card(kb, 14),
                cols);
            return Scroll(root);
        }

        private void StartMusic()
        {
            ClaimStream(StreamOwner.Music); // v3.15: mot chu stream 0x20
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
            if (!_musicPlaying || _musicMode == 1) return; // che do nhip firmware khong dung vong gui rieng
            _musicSendCts = new CancellationTokenSource();
            var ct = _musicSendCts.Token;
            int mode = _musicMode;
            if (mode == 2)
                AppendLog($"Feed phổ 0x78: kiểu “{L99Protocol.AudioStyleNames[_rhythmStyle]}” (byte 0x{MusicRhythmMap.RhythmByteForStyle(_rhythmStyle):X2}), biên độ scale={_rhythmAmplitude}, byte nền=0x{MusicRhythmMap.BgModeByte(_music.MusicBg):X2}, sáng nền={MusicRhythmMap.BgBrightnessByte(_rhythmBgBrightness)}/255.");
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
                            else if (mode == 2)
                            {
                                // v3.12: do nhay (gain) + bien do (scale) +
                                // kieu nhip + nen — tat ca chay that vao 0x78.
                                var bands = _audio.Available ? _audio.Bands01 : SyntheticBands();
                                var levels = MusicRhythmMap.BuildLevels(bands, _music.MusicGain, _rhythmAmplitude);
                                steps = L99Protocol.BuildAudioFrameSteps(levels, _rhythmAmplitude,
                                    MusicRhythmMap.RhythmByteForStyle(_rhythmStyle),
                                    MusicRhythmMap.BgModeByte(_music.MusicBg),
                                    MusicRhythmMap.BgBrightnessByte(_rhythmBgBrightness));
                            }
                            if (steps != null)
                            {
                                sendBusy = true;
                                _ = Task.Run(() =>
                                {
                                    try { _exp.SendSteps(mode == 0 ? "Stream nhac 0x20" : "Feed pho 0x78", steps); }
                                    catch (Exception ex) { AppendLog("Lỗi gửi nhạc: " + ex.Message); }
                                    sendBusy = false;
                                });
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
                    _ = PulseBeatAsync(fx);
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
            // v3.17 (scope B): nhom be mat — TFT (man 128x128) hay LED
            // (dai rong cho den phim). Trang Man hinh GIF chi liet ke
            // Tft; cac be mat GIF->LED chi liet ke Led.
            public GifKind Kind;
        }

        // v3.17 (scope B): cache phan loai theo (duong dan + dau file)
        // de khong phai doc header anh lai moi lan dung luoi.
        private readonly Dictionary<string, (long Stamp, GifKind Kind)> _gifKindCache =
            new Dictionary<string, (long, GifKind)>();

        /// <summary>v3.17 (scope B): nhom hieu luc cua mot file GIF —
        /// the nguoi dung (settings.GifGroupOverrides) thang phan loai
        /// theo kich thuoc doc tu header anh (Identify, khong giai ma
        /// frame). Loi doc -> mac dinh TFT (duong chuan hoa an toan).</summary>
        private GifKind ResolveGifKind(string path, LibrarySettings settings)
        {
            if (settings?.GifGroupOverrides != null &&
                settings.GifGroupOverrides.TryGetValue(path, out var tag) &&
                GifGroups.TryParseTag(tag, out var tagged))
                return tagged;
            long stamp = 0;
            try { var fi = new FileInfo(path); stamp = fi.Length * 31L + fi.LastWriteTimeUtc.Ticks; }
            catch { }
            if (_gifKindCache.TryGetValue(path, out var hit) && hit.Stamp == stamp) return hit.Kind;
            var kind = GifKind.Tft;
            try
            {
                var info = SixLabors.ImageSharp.Image.Identify(path);
                if (info != null) kind = GifGroups.Classify(info.Width, info.Height);
            }
            catch { }
            _gifKindCache[path] = (stamp, kind);
            return kind;
        }

        /// <summary>v3.17 (scope B): gan the nhom cho mot file (nut
        /// "Chuyen nhom") va luu xuong settings ngay.</summary>
        private void SetGifKindOverride(string path, GifKind kind)
        {
            var s = GifLibraryStore.Load();
            if (s.GifGroupOverrides == null) s.GifGroupOverrides = new Dictionary<string, string>();
            s.GifGroupOverrides[path] = GifGroups.TagOf(kind);
            GifLibraryStore.Save(s);
            _gifKindCache.Remove(path);
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
                // v3.15: TIEN CHUYEN DOI NEN ngay khi chon GIF — bam
                // "Nap len phim" sau do chi viec gui (cache da san).
                string prePath = path;
                var preFit = _gifFit == GifFitMode.Fill ? FitMode.Fill : _gifFit == GifFitMode.Fit ? FitMode.Fit : FitMode.Stretch;
                _ = Task.Run(() =>
                {
                    try
                    {
                        var r = _displayCache.GetOrEncode(prePath, preFit,
                            (p, f) => DisplayEncoder.Encode(p, f, null));
                        if (!r.FromCache)
                            AppendLog($"Màn hình GIF: đã tiền chuyển đổi “{item.Entry.Name}” ({r.Chunks} chunk) — nạp lên phím sẽ tức thì.");
                    }
                    catch { /* tien chuyen doi la lop tang toc — loi thi bo qua */ }
                });
            }
            _applyGifSelection = ApplySelection;

            _gifWrap = new WrapPanel();
            RefreshGifGrid();

            var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            // v3.17 (scope B): bo chip "Thu vien Online" khoi trang TFT —
            // GIF online la dai LED, thuoc nhom den phim (trang Per-key /
            // Thu vien Online), khong con xuat hien o day.
            var filters = new (string Label, string Group)[]
            {
                ("Tất cả", null), ("Ngân hà", "NganHa"), ("Pixel & Game", "PixelGame"),
                ("Thư giãn", "ThuGian"), ("Của tôi", "Mine"),
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
                GhostBtn("Xoá GIF đang chọn", () => DeleteSelectedGif()),
                // v3.17 (scope B): chuyen nhom cho GIF dang chon — ghi the
                // "led" vao settings; item bien khoi luoi TFT va xuat
                // hien o cac be mat GIF->LED.
                GhostBtn("Chuyển sang nhóm đèn LED", () =>
                {
                    if (_selectedGif == null) { ShowToast("Hãy chọn một GIF trước nhé."); return; }
                    if (!_selectedGif.IsFile || string.IsNullOrEmpty(_selectedGif.FilePath))
                    { ShowToast("GIF nhúng sẵn của app thuộc nhóm màn TFT sẵn rồi, không cần chuyển."); return; }
                    string nm = _selectedGif.Entry.Name;
                    SetGifKindOverride(_selectedGif.FilePath, GifKind.Led);
                    _selectedGif = null;
                    RefreshGifGrid();
                    ShowToast($"Đã chuyển “{nm}” sang nhóm đèn LED — dùng ở trang Per-key (GIF → đèn phím) nhé.");
                }));
            var topGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,300"), ColumnSpacing = 14 };
            topGrid.Children.Add(Card(kb, 14));
            var previewCard = Card(V(10, Tb("Xem trước lớn", "h3"), bigImg, _gifInfo));
            Grid.SetColumn(previewCard, 1);
            topGrid.Children.Add(previewCard);

            // v3.8: Them GIF tu link TikTok (THU NGHIEM — kenh phan giai cong
            // khai tikwm, khong chinh thuc; chuyen doi bang ffmpeg tai theo nhu cau)
            var tiktokBox = new TextBox { Watermark = "Dán link video TikTok (https://www.tiktok.com/…)", Width = 420 };
            tiktokBox.Classes.Add("search");
            var tiktokStatus = Tb("Tuỳ chọn: chuyển thành GIF dải LED (rộng 900px, cho trang Thư viện Online/đèn phím) hoặc GIF 128×128 (cho màn hình TFT). Tối đa 12 giây đầu, 15 fps.", "faint");
            int tiktokTarget = 0; // 0 = dai LED, 1 = TFT 128
            var (tiktokSeg, _) = Segmented(new[] { "Dải LED 900px", "TFT 128×128" }, 0, i => tiktokTarget = i);
            var tiktokRow = H(10,
                tiktokBox,
                tiktokSeg,
                GhostBtn("Thêm từ TikTok", async () => await AddFromTikTokAsync(tiktokBox, tiktokStatus, () => tiktokTarget)),
                GhostBtn(FfmpegTools.EssentialsNote, async () => await DownloadFfmpegToolAsync(tiktokStatus)));

            var root = V(14,
                Tb("Màn hình GIF", "h1"),
                Tb("GIF được chuẩn hoá về 128×128 rồi phát trên màn hình TFT của phím.", "sub"),
                topGrid,
                Tb("Thư viện GIF", "h2"),
                filterRow,
                _gifWrap,
                actionRow,
                Card(V(10, Tb("Thêm GIF của bạn", "h3"), libRow,
                    Tb("GIF tự thêm lưu vào “Của tôi” trong thư mục dữ liệu của app.", "faint"),
                    Tb("Thêm từ link TikTok (Thử nghiệm)", "h3"),
                    tiktokRow,
                    Tb("Lấy GIF từ link TikTok là tính năng thử nghiệm (qua dịch vụ phân giải công khai) — có thể lỗi khi TikTok đổi cơ chế.", "faint"),
                    tiktokStatus)));
            return Scroll(root);
        }

        private Func<GifItem, Task> _applyGifSelection;

        private List<GifItem> CollectGifItems()
        {
            var items = new List<GifItem>();
            // v3.17 (scope B): nap settings mot lan o dau — chua ca the
            // nhom nguoi dung (GifGroupOverrides) de phan loai tung item.
            var settings = GifLibraryStore.Load();
            foreach (var e in GifCatalog.Library())
                // GIF nhung san cua app deu 128x128 -> nhom TFT theo thiet ke.
                items.Add(new GifItem { Entry = e, IsFile = false, GroupKey = e.Group, Kind = GifKind.Tft });
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
                        Kind = ResolveGifKind(f, settings),
                    });
                }
            }
            GifLibraryStore.EnsureDirs();
            foreach (var f in GifLibraryStore.ScanFolder(GifLibraryStore.MyGifsDir))
                items.Add(new GifItem
                {
                    Entry = new GifEntry { Name = GifCatalog.PrettyName(f), Group = "Mine", GroupLabel = "Của tôi" },
                    IsFile = true, FilePath = f, GroupKey = "Mine", Deletable = true,
                    Kind = ResolveGifKind(f, settings),
                });
            foreach (var folder in settings.Folders)
                foreach (var f in GifLibraryStore.ScanFolder(folder))
                    items.Add(new GifItem
                    {
                        Entry = new GifEntry { Name = GifCatalog.PrettyName(f), Group = "Mine", GroupLabel = "Của tôi" },
                        IsFile = true, FilePath = f, GroupKey = "Mine",
                        Kind = ResolveGifKind(f, settings),
                    });
            // v3.8: muc Online cua luoi GIF = cac GIF da tai ve cache
            // (%AppData%/TienKeyboardStudio/OnlineLibrary) — khong con dong goi san.
            // v3.17 (scope B): cac file nay la dai LED rong -> Kind = Led
            // theo kich thuoc, tu dong bi loai khoi luoi TFT o
            // RefreshGifGrid va xuat hien o bo chon GIF->LED.
            if (Directory.Exists(OnlineCatalog.CacheDir))
                foreach (var f in GifLibraryStore.ScanFolder(OnlineCatalog.CacheDir))
                    items.Add(new GifItem
                    {
                        Entry = new GifEntry { Name = "Online · " + GifCatalog.PrettyName(f), Group = "Online", GroupLabel = "Thư viện Online" },
                        IsFile = true, FilePath = f, GroupKey = "Online",
                        Kind = ResolveGifKind(f, settings),
                    });
            return items;
        }

        private void RefreshGifGrid()
        {
            if (_gifWrap == null) return;
            _gifWrap.Children.Clear();
            _gifCards.Clear();
            // v3.17 (scope B): trang Man hinh GIF chi liet ke nhom TFT —
            // GIF dai LED khong con tron vao luoi nay nua.
            var items = CollectGifItems().Where(i => i.Kind == GifKind.Tft).ToList();
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
            // v3.8 (sua RAM): luoi chi doc thumb PNG nho da cache cho file local;
            // GIF day du chi giai ma khi nguoi dung mo/phat. GIF nhung san (nho)
            // giu duong cu.
            _ = Task.Run(async () =>
            {
                string thumbDir = System.IO.Path.Combine(GifLibraryStore.RootDir, "ThumbCache");
                foreach (var c in _gifCards)
                {
                    try
                    {
                        var item2 = items[_gifCards.IndexOf(c)];
                        Bitmap bmp;
                        if (item2.IsFile)
                        {
                            string png = GifThumbCache.EnsureThumbnail(item2.FilePath, thumbDir, 96);
                            bmp = png != null ? new Bitmap(png) : null;
                        }
                        else bmp = await Task.Run(() => GifTools.LoadFrame(c.Entry.AssetUri, 96));
                        if (bmp != null)
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

        // ===== v3.8: Them GIF tu link TikTok (thu nghiem) + ffmpeg theo nhu cau =====
        private static string ToolsDir => System.IO.Path.Combine(GifLibraryStore.RootDir, "Tools");

        private string LocateFfmpeg()
            => FfmpegTools.Locate(AppContext.BaseDirectory, ToolsDir,
                Environment.GetEnvironmentVariable("PATH") ?? "");

        private async Task AddFromTikTokAsync(TextBox urlBox, TextBlock status, Func<int> targetOf)
        {
            string url = (urlBox.Text ?? "").Trim();
            if (!TikTokResolver.LooksLikeTikTokUrl(url))
            {
                ShowToast("Hãy dán link video TikTok hợp lệ trước nhé.");
                return;
            }
            string ffmpeg = LocateFfmpeg();
            if (ffmpeg == null)
            {
                status.Text = "Chưa tìm thấy ffmpeg — bấm nút “" + FfmpegTools.EssentialsNote + "” ở trên để tải về thư mục Tools của app trước nhé.";
                ShowToast("Chưa có ffmpeg để chuyển đổi video → GIF.");
                return;
            }
            try
            {
                status.Text = "Đang phân giải link TikTok qua tikwm.com…";
                TikTokResolveResult res;
                using (var http = new System.Net.Http.HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(45);
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("TienVuKeyboardStudio/3.8");
                    string json = await http.GetStringAsync(TikTokResolver.BuildApiUrl(url));
                    res = TikTokResolver.ParseResponse(json);
                }
                if (res == null)
                {
                    status.Text = "Không lấy được video từ link này (tikwm không trả về video không watermark). Thử video khác nhé — đây là kênh không chính thức nên không phải video nào cũng lấy được.";
                    AppendLog("TikTok: phân giải thất bại cho " + url);
                    return;
                }
                GifLibraryStore.EnsureDirs();
                string mp4 = System.IO.Path.Combine(GifLibraryStore.RootDir, "tiktok-tmp.mp4");
                status.Text = $"Đang tải video{(string.IsNullOrEmpty(res.Title) ? "" : ": " + res.Title)}…";
                using (var http = new System.Net.Http.HttpClient())
                {
                    http.Timeout = TimeSpan.FromMinutes(3);
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("TienVuKeyboardStudio/3.8");
                    var bytes = await http.GetByteArrayAsync(res.PlayUrl);
                    await Task.Run(() => File.WriteAllBytes(mp4, bytes));
                }
                var target = targetOf() == 1 ? GifTarget.Tft : GifTarget.LedStrip;
                string baseName = "tiktok-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string outGif = System.IO.Path.Combine(GifLibraryStore.MyGifsDir, baseName + ".gif");
                status.Text = "Đang chuyển video thành GIF bằng ffmpeg…";
                string args = FfmpegTools.BuildConvertArguments(mp4, outGif, target);
                int exit = await Task.Run(() =>
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(ffmpeg, args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                    };
                    using (var p = System.Diagnostics.Process.Start(psi))
                    {
                        p.StandardError.ReadToEnd();
                        p.WaitForExit(120000);
                        return p.ExitCode;
                    }
                });
                try { File.Delete(mp4); } catch { }
                if (exit != 0 || !File.Exists(outGif))
                {
                    status.Text = "ffmpeg chuyển đổi thất bại (mã lỗi " + exit + "). Video có thể dùng định dạng lạ — thử video khác nhé.";
                    AppendLog("TikTok: ffmpeg lỗi " + exit + " cho " + url);
                    return;
                }
                RefreshGifGrid();
                status.Text = $"Đã thêm GIF từ TikTok vào “Của tôi”: {baseName}.gif ({(target == GifTarget.Tft ? "128×128 cho TFT" : "dải LED 900px")}).";
                AppendLog($"TikTok: {url} → {outGif}");
                ShowToast("Đã thêm GIF từ TikTok vào thư viện.");
                urlBox.Text = "";
            }
            catch (Exception ex)
            {
                status.Text = "Thêm từ TikTok thất bại: " + ex.Message;
                AppendLog("Lỗi TikTok: " + ex.Message);
            }
        }

        private async Task DownloadFfmpegToolAsync(TextBlock status)
        {
            try
            {
                status.Text = "Đang tải ffmpeg essentials từ gyan.dev (khoảng 30 MB)…";
                string zipPath = System.IO.Path.Combine(GifLibraryStore.RootDir, "ffmpeg-essentials.zip");
                GifLibraryStore.EnsureDirs();
                using (var http = new System.Net.Http.HttpClient())
                {
                    http.Timeout = TimeSpan.FromMinutes(10);
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("TienVuKeyboardStudio/3.8");
                    var bytes = await http.GetByteArrayAsync(FfmpegTools.EssentialsUrl);
                    await Task.Run(() => File.WriteAllBytes(zipPath, bytes));
                }
                string dest = System.IO.Path.Combine(ToolsDir, "ffmpeg.exe");
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(ToolsDir);
                    using (var zip = System.IO.Compression.ZipFile.OpenRead(zipPath))
                    {
                        System.IO.Compression.ZipArchiveEntry entry = null;
                        foreach (var e in zip.Entries)
                            if (e.FullName.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase)) { entry = e; break; }
                        if (entry == null) throw new InvalidOperationException("Không thấy ffmpeg.exe trong gói tải về.");
                        entry.ExtractToFile(dest, overwrite: true);
                    }
                });
                try { File.Delete(zipPath); } catch { }
                status.Text = "Đã cài ffmpeg vào thư mục Tools của app — giờ dán link TikTok và bấm “Thêm từ TikTok” được rồi.";
                AppendLog("Đã tải + cài ffmpeg essentials vào " + dest);
                ShowToast("Đã cài xong công cụ chuyển đổi ffmpeg.");
            }
            catch (Exception ex)
            {
                status.Text = "Tải ffmpeg thất bại: " + ex.Message;
                AppendLog("Lỗi tải ffmpeg: " + ex.Message);
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
            // v3.15: dieu phoi stream 0x20 — mot chu stream tai mot thoi
            // diem (ClaimStream o MainWindow dung cac player khac).
            public Action Claim;
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
                Claim?.Invoke();
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
                    Claim?.Invoke();
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
        }

        // v3.12: bo phat hieu ung THOI GIAN THUC (generator thu tuc ->
        // stream 0x20), cung ky luat gui voi LedPlayer: gui that tung
        // frame (busy thi bo frame). Xem truoc trong app qua
        // State.FrameColors.
        private sealed class RealtimeLedPlayer
        {
            public readonly DispatcherTimer Timer = new DispatcherTimer();
            public RgbAnimState State;
            public KeyboardView Target;
            public ExperimentalDevice Exp;
            public Action<string> Log;
            public Action Claim; // v3.15: dieu phoi stream (ClaimStream)
            public int EffectId = RealtimeEffects.OffIndex;
            public (byte R, byte G, byte B) BaseColor = (0x7C, 0x3A, 0xED);
            public double Brightness01 = 0.75;
            public int SpeedUi = 2;
            // v3.13: che do mau stream (0 Don sac, 1 Cau vong, 2 Bang mau)
            public int ColorMode = ColorModes.Rainbow;
            private double _t;
            private bool _sendBusy;
            private bool _loggedOnce;

            public RealtimeLedPlayer() { Timer.Tick += Tick; }
            public bool Running => Timer.IsEnabled;

            public void Start(int effectId)
            {
                EffectId = effectId;
                _loggedOnce = false;
                Claim?.Invoke();
                Timer.Interval = TimeSpan.FromMilliseconds(RealtimeEffects.FrameIntervalMs(SpeedUi));
                Timer.Start();
                Tick(null, null);
            }

            public void Stop() { Timer.Stop(); }

            private void Tick(object sender, EventArgs e)
            {
                if (State == null) return;
                Timer.Interval = TimeSpan.FromMilliseconds(RealtimeEffects.FrameIntervalMs(SpeedUi));
                _t += Timer.Interval.TotalSeconds * (0.6 + SpeedUi * 0.35);
                var quads = RealtimeEffects.Generate(EffectId, _t, BaseColor, Brightness01, ColorMode);
                var dict = new Dictionary<string, Color>();
                for (int i = 0; i < quads.Length; i++)
                    dict["k" + MaxKeyData.StreamSlots[i].ViewIndex] = Color.FromRgb(quads[i].R, quads[i].G, quads[i].B);
                State.FrameColors = dict;
                State.PaintMode = false;
                Target?.InvalidateVisual();
                MaybeSend(quads);
            }

            private void MaybeSend(L99Protocol.StreamQuad[] quads)
            {
                if (Exp == null) return;
                if (_sendBusy) return;
                _sendBusy = true;
                var steps = L99Protocol.BuildStreamFrameSteps(quads.ToList());
                Task.Run(() =>
                {
                    try { Exp.SendSteps("Stream thời gian thực 0x20", steps); }
                    catch (Exception ex) { Log?.Invoke("Lỗi stream thời gian thực: " + ex.Message); }
                    _sendBusy = false;
                });
            }
        }

        private readonly LedPlayer _ledPlayer = new LedPlayer();
        private readonly LedPlayer _onlineLedPlayer = new LedPlayer();
        private readonly RealtimeLedPlayer _rtPlayer = new RealtimeLedPlayer();
        private readonly AmbilightPlayer _ambiPlayer = new AmbilightPlayer();
        private readonly ReactiveLedPlayer _reactivePlayer = new ReactiveLedPlayer();

        // ================= v3.15: DIEU PHOI STREAM 0x20 =================
        // Mot chu stream tai mot thoi diem (DESIGN §0.1): player nao
        // Start thi ClaimStream dung moi nguon stream khac. Khong hang
        // doi, khong tron frame.
        private enum StreamOwner { None, Gif, Realtime, Music, Ambilight, Reactive }
        private StreamOwner _streamOwner = StreamOwner.None;

        private void ClaimStream(StreamOwner owner)
        {
            _streamOwner = owner;
            if (owner != StreamOwner.Gif) { _ledPlayer.Stop(); _onlineLedPlayer.Stop(); }
            if (owner != StreamOwner.Realtime) _rtPlayer.Stop();
            if (owner != StreamOwner.Ambilight) _ambiPlayer.Stop();
            if (owner != StreamOwner.Reactive) _reactivePlayer.Stop();
            if (owner != StreamOwner.Music && _musicPlaying) StopMusic();
        }

        // v3.15: bo phat AMBILIGHT — chup man hinh chinh (GDI) -> lay
        // mau median theo vung phim (ScreenSampler) -> stream 0x20.
        // Chup + lay mau tren thread nen; frame cham thi bo nhip, khong
        // xep hang (cung ky luat _sendBusy cua cac player khac).
        private sealed class AmbilightPlayer
        {
            public readonly DispatcherTimer Timer = new DispatcherTimer();
            public RgbAnimState State;
            public KeyboardView Target;
            public ExperimentalDevice Exp;
            public Action<string> Log;
            public Action Claim;
            public int Mode = ScreenSampler.ModeFull;
            public int Fps = 15;
            public double Brightness = 1.0;
            public int BlackLevel = ScreenSampler.DefaultBlackLevel;
            // v3.16: bo chinh mau + lam muot (co che Hyperion.ng, MIT)
            public ColorCorrectionSettings Correction = new ColorCorrectionSettings();
            public int SmoothingMs = 200;
            private readonly ColorSmoother _smoother = new ColorSmoother();
            private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
            private bool _busy;
            private bool _sendBusy;
            private bool _captureFailed;

            public AmbilightPlayer() { Timer.Tick += Tick; }
            public bool Running => Timer.IsEnabled;

            public void Start()
            {
                Claim?.Invoke();
                _smoother.Reset();
                _clock.Restart();
                Timer.Interval = TimeSpan.FromMilliseconds(1000 / Math.Max(10, Math.Min(20, Fps)));
                Timer.Start();
                Tick(null, null);
            }

            public void Stop() { Timer.Stop(); }

            private void Tick(object sender, EventArgs e)
            {
                if (State == null || _busy) return;
                _busy = true;
                int mode = Mode, bl = BlackLevel, smoothMs = SmoothingMs;
                double br = Brightness;
                var corr = Correction;
                Task.Run(() =>
                {
                    L99Protocol.StreamQuad[] quads = null;
                    bool ok = ScreenCapture.CapturePrimaryBgra(192, 108, out var bgra);
                    if (ok)
                    {
                        quads = ScreenSampler.SampleAll(bgra, 192, 108, mode, bl, br);
                        // Pipeline Hyperion: lay mau -> CHINH MAU -> lam muot
                        quads = ColorCorrection.ApplyToFrame(quads, corr);
                    }
                    Dispatcher.UIThread.Post(() =>
                    {
                        _busy = false;
                        if (!Timer.IsEnabled) return;
                        if (!ok || quads == null)
                        {
                            if (!_captureFailed)
                            {
                                _captureFailed = true;
                                Log?.Invoke("Ambilight: không chụp được màn hình — tính năng chỉ chạy trên Windows (màn hình chính).");
                            }
                            return;
                        }
                        _captureFailed = false;
                        double dt = Math.Max(0, Math.Min(0.5, _clock.Elapsed.TotalSeconds));
                        _clock.Restart();
                        quads = _smoother.Step(quads, dt, smoothMs);
                        var dict = new Dictionary<string, Color>();
                        for (int i = 0; i < quads.Length; i++)
                            dict["k" + MaxKeyData.StreamSlots[i].ViewIndex] = Color.FromRgb(quads[i].R, quads[i].G, quads[i].B);
                        State.FrameColors = dict;
                        State.PaintMode = false;
                        Target?.InvalidateVisual();
                        if (Exp == null || _sendBusy) return;
                        _sendBusy = true;
                        var steps = L99Protocol.BuildStreamFrameSteps(quads.ToList());
                        Task.Run(() =>
                        {
                            try { Exp.SendSteps("Ambilight 0x20", steps); }
                            catch (Exception ex) { Log?.Invoke("Lỗi Ambilight: " + ex.Message); }
                            _sendBusy = false;
                        });
                    });
                });
            }
        }

        // v3.15: bo phat hieu ung GO PHIM PHAN UNG — su kien (vi tri
        // phim, moc thoi gian) tu hook dung chung voi Thong ke go phim
        // dua vao ReactiveEngine; frame sinh o 20fps, gui qua 0x20 nhu
        // cac player stream khac.
        private sealed class ReactiveLedPlayer
        {
            public readonly DispatcherTimer Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            public RgbAnimState State;
            public KeyboardView Target;
            public ExperimentalDevice Exp;
            public Action<string> Log;
            public Action Claim;
            public readonly ReactiveEngine Engine = new ReactiveEngine();
            public int EffectId = -1;
            public (byte R, byte G, byte B) BaseColor = (0x7C, 0x3A, 0xED);
            public double Brightness01 = 0.75;
            public int ColorMode = ColorModes.Rainbow;
            private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
            // v3.17 (scope A): gui theo mo hinh "frame moi nhat luon
            // thang" — Tick chi nap frame moi nhat vao o chua, mot
            // thread gui rieng doc o chua ngay khi kenh ranh. Ban cu
            // "dang ban thi bo frame" lam mat luot go khi SendSteps keo
            // dai (ACK cham): su kien song 1,1–1,6 s nhung mot vong gui
            // ban co the nuot tron cua so do.
            private volatile L99Protocol.StreamQuad[] _latestFrame;
            private L99Protocol.StreamQuad[] _lastSentFrame; // chi thread gui dung
            private Thread _senderThread;
            private volatile bool _senderRun;
            /// <summary>v3.17: bao UI moi su kien go (CHI index vi tri)
            /// de dong trang thai "Phim vua nhan" — chi hien tren man
            /// hinh, khong luu/khong log, dung dieu khoan rieng tu cu.</summary>
            public Action<int> KeyEcho;

            public ReactiveLedPlayer() { Timer.Tick += Tick; }
            public bool Running => Timer.IsEnabled;
            public double NowSec => _clock.Elapsed.TotalSeconds;

            /// <summary>Nhan su kien go: CHI index vi tri phim (hook da
            /// dich VK -> vi tri o TypingStatsService).</summary>
            public void OnKey(int viewIndex)
            {
                Engine.AddEvent(viewIndex, NowSec);
                try { KeyEcho?.Invoke(viewIndex); } catch { }
            }

            public void Start(int effectId)
            {
                EffectId = effectId;
                Claim?.Invoke();
                Engine.Clear();
                _latestFrame = null;
                _lastSentFrame = null;
                StartSender();
                Timer.Start();
                Tick(null, null);
            }

            public void Stop()
            {
                Timer.Stop();
                _senderRun = false;
            }

            private void StartSender()
            {
                _senderRun = true;
                if (_senderThread != null && _senderThread.IsAlive) return;
                _senderThread = new Thread(SenderLoop) { IsBackground = true, Name = "ReactiveStreamSender" };
                _senderThread.Start();
            }

            // Vong gui: chi gui khi frame moi nhat KHAC frame da gui
            // (ReactiveFrameGate) — frame den im lang chi gui mot lan
            // luc chuyen trang thai. SendSteps tra false (bat tay chua
            // xong, frame bi bo o tang duoi) thi KHONG danh dau da gui:
            // frame se duoc thu lai o vong ke tiep.
            private void SenderLoop()
            {
                while (_senderRun)
                {
                    var frame = _latestFrame;
                    if (Exp == null || frame == null || !ReactiveFrameGate.ShouldSend(frame, _lastSentFrame))
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    try
                    {
                        var steps = L99Protocol.BuildStreamFrameSteps(frame.ToList());
                        if (Exp.SendSteps("Stream gõ phím phản ứng 0x20", steps))
                            _lastSentFrame = frame;
                        else
                            Thread.Sleep(50); // tang duoi vua bo frame — tho roi thu lai
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke("Lỗi gõ phím phản ứng: " + ex.Message);
                        Thread.Sleep(100);
                    }
                }
            }

            private void Tick(object sender, EventArgs e)
            {
                if (State == null || EffectId < 0) return;
                var quads = Engine.Generate(EffectId, NowSec, BaseColor, Brightness01, ColorMode);
                var dict = new Dictionary<string, Color>();
                for (int i = 0; i < quads.Length; i++)
                    dict["k" + MaxKeyData.StreamSlots[i].ViewIndex] = Color.FromRgb(quads[i].R, quads[i].G, quads[i].B);
                State.FrameColors = dict;
                State.PaintMode = false;
                Target?.InvalidateVisual();
                _latestFrame = quads; // sender doc o vong cua no
            }
        }

        // ================= TRANG 5: THU VIEN ONLINE =================
        private Control BuildOnlinePage()
        {
            _onlineLedPlayer.Exp = _exp;
            _onlineLedPlayer.Log = AppendLog;
            _onlineLedPlayer.Claim = () => { ClaimStream(StreamOwner.Gif); _ledPlayer.Stop(); };
            var ledState = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3 };
            var ledKb = new KeyboardView { Mode = KeyboardMode.Animated, State = ledState };
            var ledStatus = Tb("Chọn một hiệu ứng đã tải bên dưới để chuyển thành đèn từng phím. Hiệu ứng chưa tải thì bấm “Tải về” trước (tải một lần, dùng mãi về sau).", "sub");
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
                        ledStatus.Text = $"“{card.Item.Name}” chưa được tải về — bấm nút “Tải về” trên thẻ của hiệu ứng để tải từ Drive của dự án trước nhé.";
                        return;
                    }
                    selectedPath = path;
                    ledStatus.Text = $"Đang chuyển “{card.Item.Name}” thành đèn phím…";
                    var anim = await Task.Run(() => GifLedConverter.Convert(path));
                    _onlineLedPlayer.Load(anim, ledState, ledKb);
                    ledStatus.Text = $"“{card.Item.Name}”: GIF gốc {anim.SourceWidth}×{anim.SourceHeight}, {anim.SourceFrames} frame → đã chuyển thành {anim.Frames.Count} frame đèn phím. Bấm Phát để xem trong app.";
                    AppendLog($"Online: đã chuyển LED “{card.Item.Name}” từ file đã tải ({anim.Frames.Count} frame).");
                }
                catch (Exception ex)
                {
                    ledStatus.Text = "Chuyển thất bại: " + ex.Message;
                    AppendLog("Lỗi online: " + ex.Message);
                }
            }

            async Task DownloadOnlineAsync(OnlineGifCard card)
            {
                card.BeginDownload();
                card.SetDownloading(0);
                try
                {
                    var progress = new Progress<long>(n =>
                        Dispatcher.UIThread.Post(() => card.SetDownloading(
                            card.Item.Size > 0 ? (double)n / card.Item.Size : 0)));
                    string path = await OnlineCatalog.Store.DownloadAsync(card.Item, progress);
                    card.FinishDownload();
                    AppendLog($"Online: đã tải “{card.Item.Name}” ({OnlineCatalogCore.FormatSize(card.Item.Size)}) vào bộ nhớ đệm.");
                    ShowToast($"Đã tải xong “{card.Item.Name}”.");
                    _ = SelectOnlineAsync(card);
                }
                catch (Exception ex)
                {
                    card.FinishDownload();
                    ledStatus.Text = "Tải thất bại: " + ex.Message;
                    AppendLog("Lỗi tải online: " + ex.Message);
                    ShowToast("Tải không thành công: " + ex.Message);
                }
            }

            foreach (var item in catalog)
            {
                var card = new OnlineGifCard(item) { Margin = new Thickness(0, 0, 10, 10) };
                card.SelectedChanged += c => _ = SelectOnlineAsync(c);
                card.UseClicked += c => _ = SelectOnlineAsync(c);
                card.DownloadClicked += c => _ = DownloadOnlineAsync(c);
                card.DeleteClicked += c =>
                {
                    if (OnlineCatalog.Store.DeleteDownloaded(c.Item))
                    {
                        c.RefreshState();
                        if (ReferenceEquals(selectedItem, c.Item)) { selectedPath = null; }
                        ShowToast($"Đã xoá bản đã tải của “{c.Item.Name}”.");
                    }
                };
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
                if (_onlineLedPlayer.Anim == null) { ShowToast("Hãy chọn một hiệu ứng đã tải trước nhé."); return; }
                if (_onlineLedPlayer.Timer.IsEnabled) _onlineLedPlayer.Stop();
                else _onlineLedPlayer.Play();
            });
            var sendBtn = GhostBtn("Chạy lên đèn phím", () =>
            {
                if (_onlineLedPlayer.Anim == null) { ShowToast("Hãy chọn một hiệu ứng đã tải trước nhé."); return; }
                bool on = !_onlineLedPlayer.Sending;
                _onlineLedPlayer.SetSending(on);
                ShowToast(on
                    ? "Đang stream lên đèn phím qua 0x20 (~17fps)."
                    : "Đã ngừng chạy lên đèn phím.");
            });
            var tftBtn = WriteBtn("Nạp GIF này lên màn hình TFT", async () =>
            {
                if (selectedPath == null) { ShowToast("Hãy chọn một hiệu ứng đã tải trước nhé."); return; }
                await UploadGifFileAsync(selectedPath, FitMode.Fill, null);
            }, primary: false);

            var ledCard = Card(V(10,
                Tb("Phát lên đèn phím — GIF→LED", "h2"),
                ledKb,
                ledStatus,
                H(10, playBtn, sendBtn, tftBtn)));

            var root = V(14,
                Tb("Thư viện Online", "h1"),
                Tb("GIF cộng đồng lưu trên Drive của dự án — app hiện ảnh xem trước, bấm tải mới lấy GIF gốc về.", "sub"),
                search,
                wrap,
                emptyNote,
                ledCard,
                CardFlat(Tb("Nguồn GIF ngoài gợi ý: GIPHY Pixel Art · Pixabay GIFs · GitHub RecalBoxDMD.", "faint")));
            return Scroll(root);
        }

        private sealed class OnlineGifCard : Border
        {
            private readonly Avalonia.Controls.Image _img;
            private readonly TextBlock _status;
            private readonly Button _mainBtn;
            private readonly Button _delBtn;
            private readonly ProgressBar _progress;
            private bool _selected;
            private bool _finished;

            public OnlineGifItem Item { get; }
            public event Action<OnlineGifCard> SelectedChanged;
            public event Action<OnlineGifCard> UseClicked;
            public event Action<OnlineGifCard> DownloadClicked;
            public event Action<OnlineGifCard> DeleteClicked;

            public OnlineGifCard(OnlineGifItem item)
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
                _status = new TextBlock { FontSize = 10, Foreground = UiBrushes.TextSecondary, TextWrapping = TextWrapping.Wrap };
                _mainBtn = new Button { HorizontalAlignment = HorizontalAlignment.Stretch };
                _mainBtn.Classes.Add("ghost");
                _mainBtn.Click += (s, e) =>
                {
                    if (OnlineCatalog.Store.StateOf(Item) == OnlineCacheState.Downloaded) UseClicked?.Invoke(this);
                    else if (OnlineCatalog.Store.StateOf(Item) == OnlineCacheState.NotDownloaded) DownloadClicked?.Invoke(this);
                };
                _delBtn = new Button { Content = "Xoá bản đã tải", HorizontalAlignment = HorizontalAlignment.Stretch };
                _delBtn.Classes.Add("ghost");
                _delBtn.Click += (s, e) => DeleteClicked?.Invoke(this);
                _progress = new ProgressBar { Minimum = 0, Maximum = 1, Value = 0, IsVisible = false, Height = 4 };
                var stack = new StackPanel { Spacing = 6 };
                stack.Children.Add(_img);
                stack.Children.Add(new TextBlock { Text = item.Name, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
                stack.Children.Add(_status);
                stack.Children.Add(_progress);
                stack.Children.Add(_mainBtn);
                stack.Children.Add(_delBtn);
                Child = stack;
                PointerPressed += (s, e) => SelectedChanged?.Invoke(this);
                RefreshState();
            }

            public void RefreshState()
            {
                var state = OnlineCatalog.Store.StateOf(Item);
                string size = OnlineCatalogCore.FormatSize(Item.Size);
                switch (state)
                {
                    case OnlineCacheState.Downloaded:
                        _status.Text = $"Đã tải ({size}) · dùng ngay offline";
                        _mainBtn.Content = "Dùng hiệu ứng này";
                        _mainBtn.IsEnabled = true;
                        _delBtn.IsVisible = true;
                        _progress.IsVisible = false;
                        break;
                    case OnlineCacheState.Downloading:
                        _status.Text = $"Đang tải… ({size})";
                        _mainBtn.Content = "Đang tải…";
                        _mainBtn.IsEnabled = false;
                        _delBtn.IsVisible = false;
                        _progress.IsVisible = true;
                        break;
                    default:
                        _status.Text = $"Chưa tải · {size} · bấm Tải về để dùng";
                        _mainBtn.Content = "Tải về";
                        _mainBtn.IsEnabled = true;
                        _delBtn.IsVisible = false;
                        _progress.IsVisible = false;
                        break;
                }
            }

            /// <summary>Chot trang thai sau khi tai xong — cac callback tien do
            /// den tre (Progress bao qua hang doi UI) khong duoc ghi de nua.</summary>
            public void BeginDownload() { _finished = false; }

            public void FinishDownload()
            {
                _finished = true;
                RefreshState();
            }

            public void SetDownloading(double fraction)
            {
                if (_finished) return;
                _status.Text = $"Đang tải… {(int)(fraction * 100)}%";
                _progress.IsVisible = true;
                _progress.Value = fraction;
                _mainBtn.IsEnabled = false;
                _mainBtn.Content = "Đang tải…";
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

        // v3.12: trang thai anh sang mo rong (khoi phuc tu settings.json
        // o RestoreLightingState, luu qua QueuePersistLighting).
        private int _rhythmStyle = 4;        // chi so trong L99Protocol.AudioStyleNames
        private int _rhythmAmplitude = 100;  // scale byte 0x78 (10..100)
        private int _rhythmBgBrightness = 40; // 0..100 (%)
        private double _gifBrightness = 1.0;
        private int _gifBlackLevel = GifLedConverter.DefaultBlackLevel;
        private int _rtSelected = RealtimeEffects.OffIndex;
        private int _rtColorMode = ColorModes.Rainbow; // v3.13
        // v3.15: Ambilight + go phim phan ung (luu trong LightingStateData)
        private int _ambiMode = ScreenSampler.ModeFull;
        private int _ambiFps = 15;
        private double _ambiBrightness = 1.0;
        private int _ambiBlackLevel = ScreenSampler.DefaultBlackLevel;
        // v3.16: lam muot + chinh mau Ambilight (luu trong LightingStateData)
        private int _ambiSmoothingMs = 200;
        private double _ambiGammaR = 1.0, _ambiGammaG = 1.0, _ambiGammaB = 1.0;
        private double _ambiSaturation = 1.15;
        private double _ambiLumGain = 1.0;
        private double _ambiLumMin = 0.05;

        /// <summary>v3.16: dong bo bo chinh mau tu cac truong UI vao player.</summary>
        private void SyncAmbiCorrection()
        {
            _ambiPlayer.Correction = new ColorCorrectionSettings
            {
                GammaR = _ambiGammaR,
                GammaG = _ambiGammaG,
                GammaB = _ambiGammaB,
                SaturationGain = _ambiSaturation,
                LuminanceGain = _ambiLumGain,
                LuminanceMinimum = _ambiLumMin,
            };
            _ambiPlayer.SmoothingMs = _ambiSmoothingMs;
        }
        private int _reactiveSelected = -1; // -1 tat; 0..2 ReactiveEffects
        // v3.15: day TFT nhanh — cache chuyen doi + cong day widget
        private readonly DisplayCacheStore _displayCache = new DisplayCacheStore(DisplayCacheStore.DefaultDir);
        private readonly WidgetPushGate _widgetGate = new WidgetPushGate();
        // v3.15: Now Playing that (GSMTC) — khoi tao luoi khi mo widget
        private readonly NowPlayingService _nowPlaying = new NowPlayingService();
        // v3.17: state marquee tieu de widget Dang phat — offset ky tu
        // hien tai; renderer tra offset ke tiep moi lan dung khung,
        // doi bai thi reset ve 0. Chi song trong RAM.
        private string _npMarqueeTitle = "";
        private int _npMarqueeOffset;
        private string _activeSchemeName = "";
        private Action _refreshSchemeList;
        private Action _clearReactiveChips; // v3.15: bo chon chip go phim phan ung
        private DispatcherTimer _lightPersistTimer;
        private static string SchemesDir => Path.Combine(GifLibraryStore.RootDir, "LightingSchemes");

        private static int ClampI(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));
        private static double ClampD(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

        /// <summary>v3.12: khoi phuc trang thai den tu settings.json —
        /// goi trong constructor TRUOC khi dung cac trang (trang RGB doc
        /// _rgb lam gia tri khoi tao). File cu khong co Lighting -> giu
        /// nguyen mac dinh cung nhu truoc.</summary>
        private void RestoreLightingState()
        {
            var L = _ui.Lighting;
            if (L == null) return;
            _rgb.Effect = ClampI(L.EffectId, 0, 19);
            _rgb.Brush = Color.FromRgb(L.R, L.G, L.B);
            _rgb.Multi = L.Multi;
            _rgb.Brightness = ClampI(L.BrightnessUi, 0, 4);
            _rgb.Speed = ClampI(L.SpeedUi, 0, 4);
            _rgb.Direction = ClampI(L.Direction, 0, 3);
            _rhythmStyle = ClampI(L.MusicStyle, 0, L99Protocol.AudioStyleNames.Length - 1);
            _rhythmAmplitude = ClampI(L.MusicAmplitude, 10, 100);
            _rhythmBgBrightness = ClampI(L.MusicBgBrightness, 0, 100);
            _music.MusicBg = ClampI(L.MusicBgMode, 0, 2);
            _music.MusicGain = ClampD(L.MusicGain, 0.4, 1.8);
            _gifBrightness = ClampD(L.GifBrightness, 0.2, 1.5);
            _gifBlackLevel = ClampI(L.GifBlackLevel, 0, 60);
            _rtSelected = ClampI(L.RtEffect, 0, RealtimeEffects.OffIndex);
            _rtColorMode = ClampI(L.RtColorMode, 0, 2);
            _ambiMode = ClampI(L.AmbilightMode, 0, 1);
            _ambiFps = L.AmbilightFps == 10 || L.AmbilightFps == 20 ? L.AmbilightFps : 15;
            _ambiBrightness = ClampD(L.AmbilightBrightness, 0.2, 1.5);
            _ambiBlackLevel = ClampI(L.AmbilightBlackLevel, 0, 60);
            _ambiSmoothingMs = ClampI(L.AmbilightSmoothingMs, 0, 500);
            _ambiGammaR = ClampD(L.AmbilightGammaR, 0.4, 2.8);
            _ambiGammaG = ClampD(L.AmbilightGammaG, 0.4, 2.8);
            _ambiGammaB = ClampD(L.AmbilightGammaB, 0.4, 2.8);
            _ambiSaturation = ClampD(L.AmbilightSaturation, 0, 2);
            _ambiLumGain = ClampD(L.AmbilightLumGain, 0.4, 1.6);
            _ambiLumMin = ClampD(L.AmbilightLumMin, 0, 0.3);
            _reactiveSelected = ClampI(L.ReactiveEffect, -1, ReactiveEffects.Names.Length - 1);
            _activeSchemeName = L.PaintedSchemeName ?? "";
            if (!string.IsNullOrEmpty(_activeSchemeName))
            {
                try
                {
                    var sc = new LightingSchemeStore(SchemesDir).Load(_activeSchemeName);
                    if (sc != null)
                    {
                        _painted.Clear();
                        foreach (var kv in sc.ToPainted())
                            _painted[kv.Key] = Color.FromRgb(kv.Value.R, kv.Value.G, kv.Value.B);
                    }
                }
                catch { }
            }
        }

        /// <summary>v3.12: chup trang thai den hien tai vao settings.json
        /// (chong doi 700ms — slider keo lien tuc khong ghi file lien tuc).</summary>
        private void QueuePersistLighting()
        {
            if (_lightPersistTimer == null)
            {
                _lightPersistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                _lightPersistTimer.Tick += (s, e) => { _lightPersistTimer.Stop(); PersistLightingNow(); };
            }
            _lightPersistTimer.Stop();
            _lightPersistTimer.Start();
        }

        private void PersistLightingNow()
        {
            var L = _ui.Lighting ?? (_ui.Lighting = new LightingStateData());
            L.EffectId = _rgb.Effect;
            L.R = _rgb.Brush.R; L.G = _rgb.Brush.G; L.B = _rgb.Brush.B;
            L.Multi = _rgb.Multi;
            L.BrightnessUi = (int)_rgb.Brightness;
            L.SpeedUi = (int)_rgb.Speed;
            L.Direction = _rgb.Direction;
            L.PaintedSchemeName = _activeSchemeName ?? "";
            L.MusicStyle = _rhythmStyle;
            L.MusicAmplitude = _rhythmAmplitude;
            L.MusicBgMode = _music.MusicBg;
            L.MusicBgBrightness = _rhythmBgBrightness;
            L.MusicGain = _music.MusicGain;
            L.RtEffect = _rtSelected;
            L.RtColorMode = _rtColorMode;
            L.AmbilightMode = _ambiMode;
            L.AmbilightFps = _ambiFps;
            L.AmbilightBrightness = _ambiBrightness;
            L.AmbilightBlackLevel = _ambiBlackLevel;
            L.AmbilightSmoothingMs = _ambiSmoothingMs;
            L.AmbilightGammaR = _ambiGammaR;
            L.AmbilightGammaG = _ambiGammaG;
            L.AmbilightGammaB = _ambiGammaB;
            L.AmbilightSaturation = _ambiSaturation;
            L.AmbilightLumGain = _ambiLumGain;
            L.AmbilightLumMin = _ambiLumMin;
            L.ReactiveEffect = _reactiveSelected;
            L.GifBrightness = _gifBrightness;
            L.GifBlackLevel = _gifBlackLevel;
            try { GifLibraryStore.Save(_ui); } catch { }
        }

        /// <summary>v3.15: truy cap trang thai den (tao moi neu settings
        /// chua co) — dung cho lich trinh + cac the moi.</summary>
        private LightingStateData LightingState => _ui.Lighting ?? (_ui.Lighting = new LightingStateData());

        // ================= v3.15: LICH TRINH DEN THEO GIO =================
        private DispatcherTimer _scheduleTimer;
        private string _scheduleLastKey = "";
        private DateTime _scheduleManualUntil = DateTime.MinValue;
        private StackPanel _schedPanel;

        /// <summary>Khoi tao lich trinh: baseline = luat dang hieu luc
        /// coi nhu da ap (mo app KHONG gui lenh den bat ngo); timer 20s
        /// chi ap khi sang luat moi.</summary>
        private void InitSchedule()
        {
            try { _scheduleLastKey = LightingSchedule.ActiveKey(LightingState.ScheduleRules, DateTime.Now); }
            catch { _scheduleLastKey = ""; }
            _scheduleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _scheduleTimer.Tick += (s, e) => ScheduleTick();
            _scheduleTimer.Start();
        }

        /// <summary>Doi den bang tay -> thang lich trinh den bien luat
        /// tiep theo (ApplyRgbAsync goi ham nay; ApplyScheduleRule da
        /// ghi nho Key truoc khi ap nen khong tu khoa chinh no).</summary>
        private void MarkManualLightingChange()
        {
            var L = _ui.Lighting;
            if (L == null || !L.ScheduleEnabled) return;
            _scheduleManualUntil = LightingSchedule.NextBoundary(L.ScheduleRules, DateTime.Now);
        }

        private void ScheduleTick()
        {
            var L = _ui.Lighting;
            if (L == null || !L.ScheduleEnabled) return;
            var d = LightingSchedule.Evaluate(L.ScheduleRules, DateTime.Now, _scheduleLastKey, _scheduleManualUntil);
            if (!d.ShouldApply) return;
            _scheduleLastKey = d.Key;
            ApplyScheduleRule(d.Rule);
        }

        private void ApplyScheduleRule(ScheduleRuleData rule)
        {
            try
            {
                if (rule.Kind == ScheduleRuleData.KindOff)
                {
                    _rgb.Effect = 0;
                    RefreshRgbVisuals();
                    _ = ApplyRgbAsync(0, _rgb.Brush, false, (int)_rgb.Brightness, (int)_rgb.Speed, _rgb.Direction);
                    AppendLog("[Lịch trình] Đã tắt đèn theo lịch trình.");
                }
                else if (rule.Kind == ScheduleRuleData.KindScheme)
                {
                    // v3.16: ap scheme den theo ten (ghi 0x23 nhu nut
                    // ap scheme thu cong o trang Per-key).
                    var sc = string.IsNullOrWhiteSpace(rule.SchemeName)
                        ? null : new LightingSchemeStore(SchemesDir).Load(rule.SchemeName);
                    if (sc == null)
                    {
                        AppendLog($"[Lịch trình] Không tìm thấy scheme “{rule.SchemeName}” — bỏ qua luật này.");
                    }
                    else
                    {
                        _painted.Clear();
                        foreach (var kv in sc.ToPainted())
                            _painted[kv.Key] = Color.FromRgb(kv.Value.R, kv.Value.G, kv.Value.B);
                        _activeSchemeName = sc.Name;
                        _ = SendPaintedAsync();
                        AppendLog($"[Lịch trình] Đã áp scheme “{sc.Name}” theo lịch trình.");
                    }
                }
                else if (rule.Kind == ScheduleRuleData.KindEffect)
                {
                    _rgb.Effect = rule.EffectId;
                    _rgb.Multi = ColorModes.DefaultMultiForEffect(rule.EffectId);
                    RefreshRgbVisuals();
                    _ = ApplyRgbAsync(_rgb.Effect, _rgb.Brush, _rgb.Multi, (int)_rgb.Brightness, (int)_rgb.Speed, _rgb.Direction);
                    AppendLog($"[Lịch trình] Đã áp hiệu ứng “{EffectFriendlyNames[rule.EffectId]}” theo lịch trình.");
                }
                else
                {
                    int bui = LightingSchedule.BrightnessUiFromPercent(rule.BrightnessPercent);
                    _rgb.Brightness = bui;
                    _ = ApplyRgbAsync(_rgb.Effect, _rgb.Brush, _rgb.Multi, bui, (int)_rgb.Speed, _rgb.Direction);
                    AppendLog($"[Lịch trình] Đã đặt độ sáng {rule.BrightnessPercent}% theo lịch trình.");
                }
                QueuePersistLighting();
            }
            catch (Exception ex) { AppendLog("[Lịch trình] Lỗi áp luật: " + ex.Message); }
        }

        private void SaveSchedule()
        {
            _scheduleLastKey = ""; // sua luat xong: danh gia lai tu dau
            try { GifLibraryStore.Save(_ui); } catch { }
        }

        private void RefreshScheduleRows()
        {
            if (_schedPanel == null) return;
            _schedPanel.Children.Clear();
            var rules = LightingSchedule.Sorted(LightingState.ScheduleRules);
            LightingState.ScheduleRules = rules;
            foreach (var rule in rules)
            {
                var captured = rule;
                var timeBox = new TextBox { Text = LightingSchedule.FormatMinutes(rule.StartMinutes), Width = 70 };
                timeBox.LostFocus += (s, e) =>
                {
                    captured.StartMinutes = LightingSchedule.ParseTimeToMinutes(timeBox.Text, captured.StartMinutes);
                    timeBox.Text = LightingSchedule.FormatMinutes(captured.StartMinutes);
                    SaveSchedule();
                };
                var kindBox = new ComboBox
                {
                    Width = 130,
                    ItemsSource = new[] { "Độ sáng", "Hiệu ứng", "Tắt đèn", "Scheme đèn" },
                    SelectedIndex = Math.Max(0, Math.Min(3, rule.Kind)),
                };
                kindBox.SelectionChanged += (s, e) =>
                {
                    captured.Kind = kindBox.SelectedIndex < 0 ? 0 : kindBox.SelectedIndex;
                    SaveSchedule();
                    RefreshScheduleRows(); // doi loai -> doi o gia tri (so <-> danh sach scheme)
                };
                var valBox = new TextBox
                {
                    Text = rule.Kind == ScheduleRuleData.KindEffect ? rule.EffectId.ToString() : rule.BrightnessPercent.ToString(),
                    Width = 70,
                    IsVisible = rule.Kind != ScheduleRuleData.KindScheme,
                };
                valBox.LostFocus += (s, e) =>
                {
                    if (int.TryParse((valBox.Text ?? "").Trim(), out int v))
                    {
                        if (captured.Kind == ScheduleRuleData.KindEffect) captured.EffectId = ClampI(v, 0, 19);
                        else captured.BrightnessPercent = ClampI(v, 0, 100);
                    }
                    SaveSchedule();
                };
                // v3.16: o gia tri cho loai Scheme — chon ten scheme da luu
                var schemeNames = new LightingSchemeStore(SchemesDir).ListNames();
                var schemeBox = new ComboBox
                {
                    Width = 180,
                    ItemsSource = schemeNames,
                    IsVisible = rule.Kind == ScheduleRuleData.KindScheme,
                };
                if (schemeNames.Contains(rule.SchemeName)) schemeBox.SelectedItem = rule.SchemeName;
                schemeBox.SelectionChanged += (s, e) =>
                {
                    captured.SchemeName = schemeBox.SelectedItem as string ?? "";
                    SaveSchedule();
                };
                var delBtn = GhostBtn("Xoá", () =>
                {
                    LightingState.ScheduleRules.Remove(captured);
                    SaveSchedule();
                    RefreshScheduleRows();
                });
                _schedPanel.Children.Add(H(8, timeBox, kindBox, valBox, schemeBox, delBtn));
            }
        }

        /// <summary>v3.15: dong bo lai hinh trang RGB sau khi lich
        /// trinh (hoac nguon ngoai trang) doi hieu ung/mau.</summary>
        private List<EffectPreview> _rgbPreviews;
        private List<Border> _rgbCards;
        private void RefreshRgbVisuals()
        {
            if (_rgbPreviews != null && _rgbCards != null)
            {
                foreach (var p in _rgbPreviews) { p.Multi = _rgb.Multi; p.BaseColor = _rgb.Brush; p.InvalidateVisual(); }
                SelectEffectVisual(_rgb.Effect, _rgbPreviews, _rgbCards);
            }
            _selectFwColorMode?.Invoke(_rgb.Multi ? 1 : 0);
            if (_homeFxValue != null) _homeFxValue.Text = EffectFriendlyNames[_rgb.Effect];
        }
        private TextBlock _paintInfo;

        private Control BuildPerKeyPage()
        {
            _ledPlayer.Exp = _exp;
            _ledPlayer.Log = AppendLog;
            _ledPlayer.Claim = () => { ClaimStream(StreamOwner.Gif); _onlineLedPlayer.Stop(); };

            // --- Ve mau tung phim ---
            var paintState = new RgbAnimState { PaintMode = true, Brightness = 4 };
            var kb = new KeyboardView { Mode = KeyboardMode.Animated, State = paintState, Interactive = true, Painted = _painted };
            _paintInfo = Tb("Chạm vào từng phím trên hình để tô màu. (Shift_R và Alt_R dùng chung một kênh đèn trong dữ liệu hãng.)", "sub");
            kb.KeyClicked += (id, label) =>
            {
                if (_eraseMode) _painted.Remove(id);
                else _painted[id] = _paintColor;
                kb.InvalidateVisual();
                _paintInfo.Text = $"Đã tô {_painted.Count} phím. Phím vừa chọn: {label} ({id}).";
            };

            var paletteColors = new (string Name, Color C)[]
            {
                ("Tím", Color.FromRgb(0x7C, 0x3A, 0xED)), ("Cyan", Color.FromRgb(0x22, 0xD3, 0xEE)),
                ("Hồng", Color.FromRgb(0xF4, 0x72, 0xB6)), ("Đỏ", Color.FromRgb(0xEF, 0x44, 0x44)),
                ("Cam", Color.FromRgb(0xF9, 0x73, 0x16)), ("Vàng", Color.FromRgb(0xFA, 0xCC, 0x15)),
                ("Lục", Color.FromRgb(0x34, 0xD3, 0x99)), ("Dương", Color.FromRgb(0x3B, 0x82, 0xF6)),
                ("Trắng", Color.FromRgb(0xF8, 0xFA, 0xFC)),
            };
            // v3.12: bang ve dung CHUNG bo chon mau tu do voi trang RGB.
            var eraseChip = new Button { Content = "Tẩy (tắt phím)" };
            eraseChip.Classes.Add("chip");
            eraseChip.Click += (s, e) => { _eraseMode = !_eraseMode; SetCls(eraseChip, "sel", _eraseMode); };
            var paintPicker = new ColorPickerPanel(paletteColors);
            paintPicker.SetColor(_paintColor);
            paintPicker.SetRecents(RecentColorList());
            paintPicker.ColorChanged += c =>
            {
                _paintColor = c;
                if (_eraseMode) { _eraseMode = false; SetCls(eraseChip, "sel", false); }
            };
            paintPicker.ColorCommitted += c =>
            {
                _ui.RecentColors = ColorMath.PushRecent(_ui.RecentColors, ColorMath.ToHex(c.R, c.G, c.B));
                paintPicker.SetRecents(RecentColorList());
                try { GifLibraryStore.Save(_ui); } catch { }
            };
            var paintTools = H(10,
                eraseChip,
                GhostBtn("Tô tất cả", () =>
                {
                    foreach (var k in KeyboardView.AllKeys) _painted[k.Id] = _paintColor;
                    kb.InvalidateVisual();
                }),
                GhostBtn("Xoá hết", () =>
                {
                    _painted.Clear();
                    kb.InvalidateVisual();
                }));

            // --- v3.12: scheme den tuy chinh (luu ten / ap / doi ten / xoa /
            // xuat / nhap — JSON cua app + XML theo dinh dang app chinh chu) ---
            var schemeStore = new LightingSchemeStore(SchemesDir);
            var schemeNameBox = new TextBox { Watermark = "Tên scheme…", Width = 190 };
            var schemeBox = new ComboBox { Width = 210 };
            var schemeStatus = Tb("", "faint");
            void RefreshSchemes()
            {
                var names = schemeStore.ListNames();
                schemeBox.ItemsSource = names;
                if (!string.IsNullOrEmpty(_activeSchemeName) && names.Contains(_activeSchemeName))
                    schemeBox.SelectedItem = _activeSchemeName;
            }
            _refreshSchemeList = RefreshSchemes;
            RefreshSchemes();
            string SelectedScheme() => schemeBox.SelectedItem as string;
            var schemeSaveBtn = GhostBtn("Lưu scheme", () =>
            {
                string name = (schemeNameBox.Text ?? "").Trim();
                if (string.IsNullOrEmpty(name)) name = SelectedScheme() ?? "";
                if (string.IsNullOrEmpty(name)) { schemeStatus.Text = "Hãy nhập tên scheme trước."; return; }
                if (_painted.Count == 0) { schemeStatus.Text = "Chưa tô phím nào để lưu."; return; }
                try
                {
                    var map = new Dictionary<string, (byte, byte, byte)>();
                    foreach (var kv in _painted) map[kv.Key] = (kv.Value.R, kv.Value.G, kv.Value.B);
                    schemeStore.Save(LightingScheme.FromPainted(name, map));
                    _activeSchemeName = name;
                    PersistLightingNow();
                    RefreshSchemes();
                    schemeStatus.Text = $"Đã lưu scheme “{name}” ({_painted.Count} phím).";
                }
                catch (Exception ex) { schemeStatus.Text = "Lưu thất bại: " + ex.Message; }
            });
            var schemeApplyBtn = GhostBtn("Áp dụng", () =>
            {
                string name = SelectedScheme();
                if (name == null) { schemeStatus.Text = "Hãy chọn một scheme."; return; }
                var sc = schemeStore.Load(name);
                if (sc == null) { schemeStatus.Text = "Không đọc được scheme."; return; }
                _painted.Clear();
                foreach (var kv in sc.ToPainted()) _painted[kv.Key] = Color.FromRgb(kv.Value.R, kv.Value.G, kv.Value.B);
                _activeSchemeName = name;
                PersistLightingNow();
                kb.InvalidateVisual();
                schemeStatus.Text = $"Đã áp dụng “{name}” ({_painted.Count} phím) — bấm Gửi 0x23 để xuống phím.";
            });
            var schemeRenameBtn = GhostBtn("Đổi tên", () =>
            {
                string old = SelectedScheme();
                string nw = (schemeNameBox.Text ?? "").Trim();
                if (old == null) { schemeStatus.Text = "Hãy chọn scheme cần đổi tên."; return; }
                if (string.IsNullOrEmpty(nw)) { schemeStatus.Text = "Hãy nhập tên mới vào ô tên."; return; }
                try
                {
                    schemeStore.Rename(old, nw);
                    if (_activeSchemeName == old) { _activeSchemeName = nw; PersistLightingNow(); }
                    RefreshSchemes();
                    schemeStatus.Text = $"Đã đổi tên thành “{nw}”.";
                }
                catch (Exception ex) { schemeStatus.Text = "Đổi tên thất bại: " + ex.Message; }
            });
            var schemeDeleteBtn = GhostBtn("Xoá", () =>
            {
                string name = SelectedScheme();
                if (name == null) { schemeStatus.Text = "Hãy chọn scheme cần xoá."; return; }
                if (schemeStore.Delete(name))
                {
                    if (_activeSchemeName == name) { _activeSchemeName = ""; PersistLightingNow(); }
                    RefreshSchemes();
                    schemeStatus.Text = $"Đã xoá “{name}”.";
                }
                else schemeStatus.Text = "Xoá thất bại.";
            });
            var schemeExportBtn = GhostBtn("Xuất file", async () =>
            {
                string name = SelectedScheme();
                if (name == null) { schemeStatus.Text = "Hãy chọn scheme cần xuất."; return; }
                var sc = schemeStore.Load(name);
                if (sc == null) { schemeStatus.Text = "Không đọc được scheme."; return; }
                try
                {
                    var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Xuất scheme đèn",
                        SuggestedFileName = name + ".json",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("Scheme JSON (app này)") { Patterns = new[] { "*.json" } },
                            new FilePickerFileType("Scheme XML (định dạng app chính chủ)") { Patterns = new[] { "*.xml" } },
                        },
                    });
                    string path = file?.TryGetLocalPath();
                    if (path == null) return;
                    if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        File.WriteAllText(path, SchemeXml.Export(sc));
                    else
                        File.WriteAllText(path, JsonSerializer.Serialize(sc, new JsonSerializerOptions { WriteIndented = true }));
                    schemeStatus.Text = "Đã xuất: " + Path.GetFileName(path);
                }
                catch (Exception ex) { schemeStatus.Text = "Xuất thất bại: " + ex.Message; }
            });
            var schemeImportBtn = GhostBtn("Nhập file", async () =>
            {
                try
                {
                    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Nhập scheme đèn (JSON hoặc XML)",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Scheme đèn") { Patterns = new[] { "*.json", "*.xml" } },
                        },
                    });
                    string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
                    if (path == null) return;
                    string text = File.ReadAllText(path);
                    LightingScheme sc;
                    if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!SchemeXml.TryParse(text, out sc, out string err)) { schemeStatus.Text = "Nhập thất bại: " + err; return; }
                    }
                    else
                    {
                        sc = JsonSerializer.Deserialize<LightingScheme>(text);
                        if (sc == null) { schemeStatus.Text = "Nhập thất bại: file JSON không hợp lệ."; return; }
                    }
                    if (string.IsNullOrWhiteSpace(sc.Name)) sc.Name = Path.GetFileNameWithoutExtension(path);
                    schemeStore.Save(sc);
                    RefreshSchemes();
                    schemeBox.SelectedItem = sc.Name;
                    schemeStatus.Text = $"Đã nhập scheme “{sc.Name}” — bấm Áp dụng để dùng.";
                }
                catch (Exception ex) { schemeStatus.Text = "Nhập thất bại: " + ex.Message; }
            });
            // --- v3.15: chia se scheme bang MA TVKS1 (dan ma la nhan) ---
            var shareCodeBox = new TextBox { Watermark = "Dán mã TVKS1:… để nhập scheme được chia sẻ", Width = 340 };
            var shareBtn = GhostBtn("Chia sẻ bằng mã", async () =>
            {
                string name = SelectedScheme();
                if (name == null) { schemeStatus.Text = "Hãy chọn scheme cần chia sẻ."; return; }
                var sc = schemeStore.Load(name);
                if (sc == null) { schemeStatus.Text = "Không đọc được scheme."; return; }
                try
                {
                    string code = SchemeShare.Encode(sc);
                    shareCodeBox.Text = code;
                    if (Clipboard != null) await Clipboard.SetTextAsync(code);
                    schemeStatus.Text = $"Đã tạo mã chia sẻ cho “{name}” và sao chép vào clipboard — gửi mã này cho người khác dán vào ô mã là có scheme.";
                }
                catch (Exception ex) { schemeStatus.Text = "Tạo mã thất bại: " + ex.Message; }
            });
            var importCodeBtn = GhostBtn("Nhập mã", () =>
            {
                string code = (shareCodeBox.Text ?? "").Trim();
                if (code.Length == 0) { schemeStatus.Text = "Hãy dán mã TVKS1:… vào ô mã trước."; return; }
                if (!SchemeShare.TryDecode(code, out var sc, out string err))
                { schemeStatus.Text = "Nhập mã thất bại: " + err; return; }
                try
                {
                    schemeStore.Save(sc);
                    RefreshSchemes();
                    schemeBox.SelectedItem = sc.Name;
                    schemeStatus.Text = $"Đã nhập scheme “{sc.Name}” ({sc.Keys.Count} phím) từ mã chia sẻ — bấm Áp dụng để dùng.";
                }
                catch (Exception ex) { schemeStatus.Text = "Nhập mã thất bại: " + ex.Message; }
            });
            var schemeBlock = V(8,
                Tb("Scheme đèn của tôi", "h3"),
                H(10, schemeNameBox, schemeSaveBtn),
                H(10, schemeBox, schemeApplyBtn, schemeRenameBtn, schemeDeleteBtn),
                H(10, schemeExportBtn, schemeImportBtn, shareBtn),
                H(10, shareCodeBox, importCodeBtn),
                schemeStatus);

            var probeBtn = GhostBtn("Đọc thử màu phím (0xF5) — chỉ đọc", async () => await ProbeColorsAsync(kb));
            var sendBtn = GhostBtn("Gửi màu 0x23 + bật Custom 0x80", async () => await SendPaintedAsync());
            var paintCard = Card(V(12,
                Tb("Vẽ màu từng phím", "h2"),
                kb,
                paintPicker,
                paintTools,
                _paintInfo,
                H(10, probeBtn, sendBtn),
                Tb("Đọc thử 0xF5 chỉ đọc màu hiện tại của phím, không ghi gì xuống phím. Cần khôi phục gốc: giữ Fn+Esc 5 giây trên phím.", "faint"),
                schemeBlock));

            // --- GIF -> LED ---
            var ledState = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3 };
            var ledKb = new KeyboardView { Mode = KeyboardMode.Animated, State = ledState };
            var ledPicker = new ComboBox { Width = 340 };
            // v3.17 (scope B): bo chon GIF->LED chi liet ke nhom LED —
            // GIF vuong cho man TFT khong con tron vao day.
            var ledItems = CollectGifItems().Where(i => i.Kind == GifKind.Led).ToList();
            ledPicker.ItemsSource = ledItems.Select(i => i.Entry.Name).ToList();
            if (ledItems.Count > 0) ledPicker.SelectedIndex = 0;
            var ledInfo = Tb("Chọn GIF rồi bấm Nạp GIF để stream lên đèn phím.", "sub");
            string ledPath = null;
            var loadLedBtn = GhostBtn("Nạp GIF", async () =>
            {
                int idx = ledPicker.SelectedIndex;
                if (idx < 0 || idx >= ledItems.Count) { ShowToast("Hãy chọn một GIF trước nhé."); return; }
                try
                {
                    var item = ledItems[idx];
                    string path = ResolveGifPath(item);
                    ledPath = path;
                    ledInfo.Text = "Đang chuyển GIF thành frame đèn phím…";
                    var anim = await Task.Run(() => GifLedConverter.Convert(path, _gifBlackLevel, _gifBrightness));
                    _ledPlayer.Load(anim, ledState, ledKb);
                    ledInfo.Text = $"“{item.Entry.Name}”: {anim.SourceWidth}×{anim.SourceHeight}, {anim.SourceFrames} frame gốc → {anim.Frames.Count} frame LED. Bấm Phát để xem trong app (~17fps khi chạy lên phím).";
                }
                catch (Exception ex) { ledInfo.Text = "Lỗi chuyển GIF: " + ex.Message; }
            });
            // v3.12: do sang + nguong den cua GIF->LED thanh slider (truoc
            // day co dinh trong code: brightness 1.0, blackLevel 24). Doi
            // slider -> chuyen lai GIF dang nap (chong doi 500ms).
            var gifBright = new Slider { Minimum = 0.2, Maximum = 1.5, Value = _gifBrightness, Width = 200 };
            var gifBlack = new Slider { Minimum = 0, Maximum = 60, Value = _gifBlackLevel, Width = 200 };
            var reconvertTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            reconvertTimer.Tick += async (s, e) =>
            {
                reconvertTimer.Stop();
                if (ledPath == null) return;
                try
                {
                    var anim = await Task.Run(() => GifLedConverter.Convert(ledPath, _gifBlackLevel, _gifBrightness));
                    _ledPlayer.Load(anim, ledState, ledKb);
                }
                catch (Exception ex) { ledInfo.Text = "Lỗi chuyển GIF: " + ex.Message; }
            };
            gifBright.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _gifBrightness = gifBright.Value; QueuePersistLighting(); reconvertTimer.Stop(); reconvertTimer.Start(); }
            };
            gifBlack.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _gifBlackLevel = (int)Math.Round(gifBlack.Value); QueuePersistLighting(); reconvertTimer.Stop(); reconvertTimer.Start(); }
            };
            var playLedBtn = GhostBtn("Phát / Dừng", () =>
            {
                if (_ledPlayer.Anim == null) { ShowToast("Hãy nạp một GIF trước nhé."); return; }
                if (_ledPlayer.Timer.IsEnabled) _ledPlayer.Stop(); else _ledPlayer.Play();
            });
            var sendLedBtn = GhostBtn("Chạy lên đèn phím", () =>
            {
                if (_ledPlayer.Anim == null) { ShowToast("Hãy nạp một GIF trước nhé."); return; }
                bool on = !_ledPlayer.Sending;
                _ledPlayer.SetSending(on);
                ShowToast(on
                    ? "Đang stream lên đèn phím (~17fps)."
                    : "Đã ngừng chạy lên đèn phím.");
            });
            var ledCard = Card(V(12,
                Tb("GIF → đèn phím (stream 0x20)", "h2"),
                ledKb,
                H(10, ledPicker, loadLedBtn,
                    // v3.17 (scope B): chuyen muc dang chon sang nhom TFT.
                    GhostBtn("Chuyển sang nhóm màn TFT", () =>
                    {
                        int idx = ledPicker.SelectedIndex;
                        if (idx < 0 || idx >= ledItems.Count) { ShowToast("Hãy chọn một GIF trước nhé."); return; }
                        var item = ledItems[idx];
                        if (!item.IsFile || string.IsNullOrEmpty(item.FilePath))
                        { ShowToast("Mục này không chuyển nhóm được."); return; }
                        string nm = item.Entry.Name;
                        SetGifKindOverride(item.FilePath, GifKind.Tft);
                        ledItems = CollectGifItems().Where(i => i.Kind == GifKind.Led).ToList();
                        ledPicker.ItemsSource = ledItems.Select(i => i.Entry.Name).ToList();
                        if (ledItems.Count > 0) ledPicker.SelectedIndex = 0;
                        ledInfo.Text = $"Đã chuyển “{nm}” sang nhóm màn hình TFT — mở trang Màn hình GIF để dùng nhé.";
                    })),
                ledInfo,
                H(10, playLedBtn, sendLedBtn),
                H(24,
                    V(4, Tb("Độ sáng GIF", "sub"), gifBright),
                    V(4, Tb("Ngưỡng đen (blackLevel)", "sub"), gifBlack))));

            // --- v3.12: hieu ung THOI GIAN THUC (generator -> stream 0x20) ---
            _rtPlayer.Exp = _exp;
            _rtPlayer.Log = AppendLog;
            _rtPlayer.Claim = () => ClaimStream(StreamOwner.Realtime);
            _ambiPlayer.Exp = _exp;
            _ambiPlayer.Log = AppendLog;
            _ambiPlayer.Claim = () => ClaimStream(StreamOwner.Ambilight);
            _reactivePlayer.Exp = _exp;
            _reactivePlayer.Log = AppendLog;
            _reactivePlayer.Claim = () => ClaimStream(StreamOwner.Reactive);
            var rtState = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3 };
            var rtKb = new KeyboardView { Mode = KeyboardMode.Animated, State = rtState };
            _rtPlayer.State = rtState;
            _rtPlayer.Target = rtKb;
            var rtBtns = new List<Button>();
            var rtWrap = new WrapPanel();
            var rtBright = new Slider { Minimum = 0, Maximum = 4, Value = 3, Width = 200 };
            var rtSpeed = new Slider { Minimum = 0, Maximum = 4, Value = 2, Width = 200 };
            _rtPlayer.Brightness01 = 0.75;
            _rtPlayer.SpeedUi = 2;
            rtBright.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) _rtPlayer.Brightness01 = rtBright.Value / 4.0;
            };
            rtSpeed.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) _rtPlayer.SpeedUi = (int)Math.Round(rtSpeed.Value);
            };
            for (int i = 0; i < RealtimeEffects.Names.Length; i++)
            {
                int idx = i;
                var b = new Button { Content = RealtimeEffects.Names[idx], Margin = new Thickness(0, 0, 8, 8) };
                b.Classes.Add("chip");
                if (idx == _rtSelected) b.Classes.Add("sel");
                b.Click += (s, e) =>
                {
                    _rtSelected = idx;
                    _reactivePlayer.Stop(); // v3.15: mot chu stream
                    _clearReactiveChips?.Invoke();
                    foreach (var o in rtBtns) SetCls(o, "sel", o == b);
                    if (idx == RealtimeEffects.OffIndex)
                    {
                        _rtPlayer.Stop();
                        rtState.FrameColors = null;
                        rtKb.InvalidateVisual();
                    }
                    else
                    {
                        _rtPlayer.BaseColor = (_paintColor.R, _paintColor.G, _paintColor.B);
                        _rtPlayer.Brightness01 = rtBright.Value / 4.0;
                        _rtPlayer.SpeedUi = (int)Math.Round(rtSpeed.Value);
                        _rtPlayer.ColorMode = _rtColorMode;
                        _rtPlayer.Start(idx);
                    }
                    QueuePersistLighting();
                };
                rtBtns.Add(b);
                rtWrap.Children.Add(b);
            }
            // v3.13: che do mau cho stream — mac dinh Cau vong.
            _rtPlayer.ColorMode = _rtColorMode;
            var (rtModeSeg, _) = Segmented(RealtimeEffects.ColorModeNames, _rtColorMode, i =>
            {
                _rtColorMode = i;
                _rtPlayer.ColorMode = i;
                _reactivePlayer.ColorMode = i;
                QueuePersistLighting();
            });
            var rtModeRow = H(10, Tb("Chế độ màu:", "sub"), rtModeSeg);
            rtModeRow.Children[0].VerticalAlignment = VerticalAlignment.Center;
            var rtCard = Card(V(12,
                Tb("Hiệu ứng thời gian thực (stream 0x20)", "h2"),
                rtKb,
                rtWrap,
                rtModeRow,
                H(24,
                    V(4, Tb("Độ sáng", "sub"), rtBright),
                    V(4, Tb("Tốc độ", "sub"), rtSpeed)),
                Tb("Đơn sắc dùng màu vẽ per-key đang chọn; Cầu vồng và Bảng màu tự rải nhiều màu.", "faint")));

            // --- v3.15: hieu ung GO PHIM PHAN UNG (su kien vi tri phim
            // tu hook dung chung voi Thong ke go phim) ---
            var reactiveState = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3 };
            var reactiveKb = new KeyboardView { Mode = KeyboardMode.Animated, State = reactiveState };
            _reactivePlayer.State = reactiveState;
            _reactivePlayer.Target = reactiveKb;
            _reactivePlayer.ColorMode = _rtColorMode;
            var reactiveStatus = Tb(_typingOn
                ? "Đang theo dõi vị trí phím gõ. Chọn một hiệu ứng rồi gõ thử."
                : "Bật công tắc theo dõi phím bên dưới để hiệu ứng nhận phím gõ thật (xem trước trong app vẫn chạy).", "sub");
            // v3.17 (scope A): dong chan doan "Phim vua nhan" — neu dong
            // nay doi theo phim dang go thi hook dang chay tot (tach
            // bach loi hook khoi loi gui stream). Chi hien tren UI trong
            // phien chay, khong luu settings, khong ghi log.
            var lastKeyStatus = Tb("Phím vừa nhận: —", "faint");
            _reactivePlayer.KeyEcho = view => Dispatcher.UIThread.Post(() =>
                lastKeyStatus.Text = "Phím vừa nhận: " + MaxKeyData.ViewKeyNames[view]);
            var reactiveBtns = new List<Button>();
            var reactiveWrap = new WrapPanel();
            _clearReactiveChips = () =>
            {
                foreach (var o in reactiveBtns) SetCls(o, "sel", false);
                _reactiveSelected = -1;
            };
            for (int i = 0; i < ReactiveEffects.Names.Length; i++)
            {
                int idx = i;
                var b = new Button { Content = ReactiveEffects.Names[idx], Margin = new Thickness(0, 0, 8, 8) };
                b.Classes.Add("chip");
                if (idx == _reactiveSelected) b.Classes.Add("sel");
                b.Click += (s, e) =>
                {
                    _reactiveSelected = idx;
                    foreach (var o in reactiveBtns) SetCls(o, "sel", o == b);
                    _rtPlayer.Stop();
                    foreach (var o in rtBtns) SetCls(o, "sel", false);
                    _rtSelected = RealtimeEffects.OffIndex;
                    rtState.FrameColors = null;
                    rtKb.InvalidateVisual();
                    _reactivePlayer.BaseColor = (_paintColor.R, _paintColor.G, _paintColor.B);
                    _reactivePlayer.Brightness01 = rtBright.Value / 4.0;
                    _reactivePlayer.ColorMode = _rtColorMode;
                    _reactivePlayer.Start(idx);
                    if (!_typingOn)
                        reactiveStatus.Text = "Đang xem trước trong app. Muốn đèn chạy theo phím gõ thật: bật công tắc theo dõi phím bên dưới.";
                    QueuePersistLighting();
                };
                reactiveBtns.Add(b);
                reactiveWrap.Children.Add(b);
            }
            var reactiveOffBtn = GhostBtn("Tắt hiệu ứng gõ", () =>
            {
                _reactivePlayer.Stop();
                _clearReactiveChips?.Invoke();
                reactiveState.FrameColors = null;
                reactiveKb.InvalidateVisual();
                QueuePersistLighting();
            });
            var hookRow = SettingRow("Theo dõi vị trí phím gõ",
                "Dùng chung công tắc với Thống kê gõ phím — chỉ vị trí phím + thời điểm, trong RAM, không ghi nội dung/log",
                _typingOn, v =>
                {
                    _typingOn = v;
                    if (v) { _typing.Start(); reactiveStatus.Text = "Đang theo dõi vị trí phím gõ. Chọn một hiệu ứng rồi gõ thử."; }
                    else
                    {
                        _typing.Stop();
                        _reactivePlayer.Stop();
                        reactiveStatus.Text = "Đã tắt theo dõi phím — hiệu ứng gõ không còn nhận phím thật.";
                    }
                    RefreshTypingLabels();
                });
            var reactiveCard = Card(V(12,
                Tb("Hiệu ứng gõ phím phản ứng (stream 0x20)", "h2"),
                reactiveKb,
                reactiveWrap,
                H(10, reactiveOffBtn),
                hookRow,
                reactiveStatus,
                lastKeyStatus));

            // --- v3.15: AMBILIGHT (man hinh PC -> stream 0x20) ---
            var ambiState = new RgbAnimState { Brush = Color.FromRgb(0x7C, 0x3A, 0xED), Brightness = 3 };
            var ambiKb = new KeyboardView { Mode = KeyboardMode.Animated, State = ambiState };
            _ambiPlayer.State = ambiState;
            _ambiPlayer.Target = ambiKb;
            _ambiPlayer.Mode = _ambiMode;
            _ambiPlayer.Fps = _ambiFps;
            _ambiPlayer.Brightness = _ambiBrightness;
            _ambiPlayer.BlackLevel = _ambiBlackLevel;
            SyncAmbiCorrection();
            var ambiStatus = Tb("Đèn phím chạy theo màu trên màn hình chính của PC (xem phim/chơi game).", "sub");
            var (ambiModeSeg, _) = Segmented(new[] { "Toàn màn hình", "Viền màn hình" }, _ambiMode, i =>
            {
                _ambiMode = i;
                _ambiPlayer.Mode = i;
                QueuePersistLighting();
            });
            var (ambiFpsSeg, _) = Segmented(new[] { "10", "15", "20" },
                _ambiFps == 10 ? 0 : _ambiFps == 20 ? 2 : 1, i =>
                {
                    _ambiFps = i == 0 ? 10 : i == 2 ? 20 : 15;
                    _ambiPlayer.Fps = _ambiFps;
                    if (_ambiPlayer.Running) { _ambiPlayer.Stop(); _ambiPlayer.Start(); }
                    QueuePersistLighting();
                });
            var ambiBright = new Slider { Minimum = 0.2, Maximum = 1.5, Value = _ambiBrightness, Width = 200 };
            ambiBright.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _ambiBrightness = ambiBright.Value; _ambiPlayer.Brightness = _ambiBrightness; QueuePersistLighting(); }
            };
            var ambiBlack = new Slider { Minimum = 0, Maximum = 60, Value = _ambiBlackLevel, Width = 200 };
            ambiBlack.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _ambiBlackLevel = (int)Math.Round(ambiBlack.Value); _ambiPlayer.BlackLevel = _ambiBlackLevel; QueuePersistLighting(); }
            };
            // v3.16: lam muot mau + bo chinh mau (co che Hyperion.ng)
            var ambiSmooth = new Slider { Minimum = 0, Maximum = 500, Value = _ambiSmoothingMs, Width = 200 };
            ambiSmooth.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _ambiSmoothingMs = (int)Math.Round(ambiSmooth.Value); SyncAmbiCorrection(); QueuePersistLighting(); }
            };
            var ambiSat = new Slider { Minimum = 0, Maximum = 2, Value = _ambiSaturation, Width = 200 };
            ambiSat.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _ambiSaturation = ambiSat.Value; SyncAmbiCorrection(); QueuePersistLighting(); }
            };
            Slider GammaSlider(double init, Action<double> set)
            {
                var sl = new Slider { Minimum = 0.4, Maximum = 2.8, Value = init, Width = 130 };
                sl.PropertyChanged += (s, e) =>
                {
                    if (e.Property == Slider.ValueProperty) { set(sl.Value); SyncAmbiCorrection(); QueuePersistLighting(); }
                };
                return sl;
            }
            var ambiGammaR = GammaSlider(_ambiGammaR, v => _ambiGammaR = v);
            var ambiGammaG = GammaSlider(_ambiGammaG, v => _ambiGammaG = v);
            var ambiGammaB = GammaSlider(_ambiGammaB, v => _ambiGammaB = v);
            var ambiLumGain = new Slider { Minimum = 0.4, Maximum = 1.6, Value = _ambiLumGain, Width = 200 };
            ambiLumGain.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _ambiLumGain = ambiLumGain.Value; SyncAmbiCorrection(); QueuePersistLighting(); }
            };
            var ambiLumMin = new Slider { Minimum = 0, Maximum = 0.3, Value = _ambiLumMin, Width = 200 };
            ambiLumMin.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty) { _ambiLumMin = ambiLumMin.Value; SyncAmbiCorrection(); QueuePersistLighting(); }
            };
            var ambiBtn = GhostBtn("Bật Ambilight", () =>
            {
                if (_ambiPlayer.Running)
                {
                    _ambiPlayer.Stop();
                    ambiStatus.Text = "Đã tắt Ambilight.";
                }
                else
                {
                    _ambiPlayer.Mode = _ambiMode;
                    _ambiPlayer.Fps = _ambiFps;
                    _ambiPlayer.Brightness = _ambiBrightness;
                    _ambiPlayer.BlackLevel = _ambiBlackLevel;
                    SyncAmbiCorrection();
                    _ambiPlayer.Start();
                    ambiStatus.Text = "Đang chạy Ambilight — mở phim/game lên xem đèn theo màu màn hình.";
                }
            });
            var ambiCard = Card(V(12,
                Tb("Ambilight — đèn theo màn hình PC (stream 0x20)", "h2"),
                ambiKb,
                H(16,
                    V(4, Tb("Vùng lấy mẫu", "sub"), ambiModeSeg),
                    V(4, Tb("Khung hình/giây", "sub"), ambiFpsSeg)),
                H(24,
                    V(4, Tb("Độ sáng", "sub"), ambiBright),
                    V(4, Tb("Ngưỡng đen (blackLevel)", "sub"), ambiBlack)),
                H(24,
                    V(4, Tb("Làm mượt màu (ms)", "sub"), ambiSmooth),
                    V(4, Tb("Độ bão hoà màu", "sub"), ambiSat)),
                H(16,
                    V(4, Tb("Gamma đỏ", "sub"), ambiGammaR),
                    V(4, Tb("Gamma xanh lá", "sub"), ambiGammaG),
                    V(4, Tb("Gamma xanh dương", "sub"), ambiGammaB)),
                H(24,
                    V(4, Tb("Độ sáng tổng", "sub"), ambiLumGain),
                    V(4, Tb("Mức sáng tối thiểu", "sub"), ambiLumMin)),
                H(10, ambiBtn),
                ambiStatus,
                Tb("Chỉ màn hình chính. Nội dung có DRM (Netflix, app xem phim bản quyền…) chụp ra khung đen — giới hạn của Windows, không phải lỗi app.", "faint")));

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
            _expLogCard = logCard;
            logCard.IsVisible = _ui.ShowOperationLog; // v3.9.1: an mac dinh

            var root = V(14,
                Tb("Per-key", "h1"),
                Tb("Đèn từng phím, GIF→LED và hiệu ứng stream — cần cáp USB-C; đóng app hãng nếu đang mở để tránh tranh kênh.", "sub"),
                paintCard,
                ledCard,
                rtCard,
                reactiveCard,
                ambiCard,
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
                    ? "Đã gửi màu per-key + bật hiệu ứng Custom 0x80. Kiểm tra đèn phím nhé!"
                    : "Chưa gửi được — kiểm tra cáp USB-C và xem Nhật ký gói tin.");
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
                ("Đang phát", "Bài hát đang phát THẬT qua SMTC của Windows (Spotify, YouTube trên trình duyệt…)"),
                ("Đồng hồ & thời tiết", "Giờ thật của máy + thời tiết Hà Nội (Open-Meteo, không cần API key)"),
            };
            // v3.11.1: WrapPanel thay hang StackPanel ngang — cua so hep thi
            // the tu xuong dong thay vi bi cat ngang o mep phai cua so.
            var selectorRow = new WrapPanel();
            var cards = new List<Border>();
            for (int i = 0; i < kinds.Length; i++)
            {
                int idx = i;
                var descTb = Tb(kinds[i].Desc, "faint");
                if (idx == 2) _widgetWeatherDesc = descTb;
                var content = V(4, Tb(kinds[i].Title, "h3"), descTb);
                var card = new Border
                {
                    Classes = { "pickcard" },
                    Child = content,
                    Width = 252,
                    Margin = new Thickness(0, 0, 10, 10),
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
            autoSwitch.Changed += v => { _widgetAuto = v; ShowToast(v ? "Tự động đẩy widget lên TFT mỗi 5 giây." : "Đã tắt tự động đẩy widget."); };
            var uploadBtn = WriteBtn("Đẩy widget lên TFT", async () => await UploadCurrentWidgetAsync(), primary: false);
            var weatherBtn = GhostBtn("Cập nhật thời tiết", async () => await FetchWeatherAsync());

            var root = V(14,
                Tb("Widget TFT", "h1"),
                Tb("Widget được vẽ ngay trên máy tính (host) thành ảnh 128×128 rồi nạp vào màn hình phím qua kênh màn hình đã kiểm chứng — cùng cơ chế với nạp GIF.", "sub"),
                Card(_widgetKb, 14),
                Tb("Chọn widget", "h2"),
                selectorRow,
                H(12, uploadBtn, _widgetProgress, autoSwitch, Tb("Tự động đẩy mỗi 5 giây", "sub"), weatherBtn),
                BuildWeatherLocationCard(),
                Tb("Widget “Đang phát” lấy dữ liệu thật từ Windows (SMTC) — hành vi thật chỉ kiểm chứng được trên Windows.", "faint"));
            return Scroll(root);
        }

        // v3.11.1: trang thai thoi tiet HIEN RO cho nguoi dung qua dong
        // _weatherStatusText trong the "Vi tri thoi tiet" (theo loai:
        // dang tai / thanh cong / loi co the loai). Ban 3.11.0 chi ghi
        // loi vao nhat ky thao tac — panel an mac dinh — nen moi loi deu
        // vo hinh, widget dung yen o "Ha Noi - chua co du lieu".
        private enum WeatherStatusKind { Idle, Loading, Ok, Error }
        private DateTime _weatherLastAttemptUtc = DateTime.MinValue;
        private bool _weatherLastOk;

        private void SetWeatherStatus(string text, WeatherStatusKind kind)
        {
            if (_weatherStatusText == null) return;
            _weatherStatusText.Text = text;
            _weatherStatusText.Foreground = new SolidColorBrush(
                kind == WeatherStatusKind.Error ? Color.FromRgb(0xF5, 0x9B, 0x9B)
                : kind == WeatherStatusKind.Ok ? Color.FromRgb(0x34, 0xD3, 0x99)
                : Color.FromRgb(0xA6, 0xA3, 0xB8));
        }

        private async Task FetchWeatherAsync()
        {
            _weatherTried = true;
            _weatherLastAttemptUtc = DateTime.UtcNow;
            SetWeatherStatus("Đang tải thời tiết…", WeatherStatusKind.Loading);
            WeatherOutcome outcome = null;
            try
            {
                outcome = await WeatherFlow.RunAsync(WeatherService.HttpGetString, _ui, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi thời tiết không ngờ tới: " + ex.Message);
            }
            if (outcome == null)
            {
                _weatherLastOk = false;
                SetWeatherStatus(WeatherFlow.NetworkErrorMessage, WeatherStatusKind.Error);
                if (_widgetKind == 2) RenderWidgetFrame();
                return;
            }
            if (outcome.NewGeoCache != null)
            {
                var geo = outcome.NewGeoCache;
                _ui.GeoCity = geo.City; _ui.GeoCountry = geo.Country;
                _ui.GeoLat = geo.Lat; _ui.GeoLon = geo.Lon;
                _ui.GeoFetchedUtc = geo.FetchedUtc.ToString("o");
                SaveUi();
            }
            if (outcome.Ok)
            {
                _weather = outcome.Weather;
                _weatherPlace = outcome.Place;
                _weatherLastOk = true;
                UpdateWeatherLocText(outcome.Place, outcome.Via);
                SetWeatherStatus(outcome.UserMessage + " · cập nhật " + DateTime.Now.ToString("HH:mm"), WeatherStatusKind.Ok);
                AppendLog($"Thời tiết {outcome.Place} ({outcome.Via}): {outcome.Weather.TempC}°C, {outcome.Weather.Description}, độ ẩm {outcome.Weather.Humidity}%.");
            }
            else
            {
                _weatherLastOk = false;
                SetWeatherStatus(outcome.UserMessage, WeatherStatusKind.Error);
                AppendLog("Thời tiết: " + outcome.UserMessage);
            }
            if (_widgetKind == 2) RenderWidgetFrame();
        }

        private void UpdateWeatherLocText()
        {
            // Hien thi tu settings (chua can goi mang): chon tay -> thanh pho
            // da nhap; tu dong -> cache IP con tuoi neu co, khong thi noi
            // dang dung gan nhat (_weatherPlace).
            if (WeatherLocationLogic.NormalizeMode(_ui.WeatherMode) == WeatherLocationLogic.ModeManual)
            {
                string city = (_ui.WeatherCity ?? "").Trim();
                UpdateWeatherLocText(city.Length == 0 ? WeatherLocationLogic.HanoiName : city, "chọn tay");
                return;
            }
            var cache = WeatherLocationLogic.FromSettings(_ui);
            if (WeatherLocationLogic.IsCacheFresh(cache, DateTime.UtcNow))
                UpdateWeatherLocText(cache.DisplayName, "tự động theo mạng");
            else
                UpdateWeatherLocText(_weatherPlace, "tự động theo mạng");
        }

        private void UpdateWeatherLocText(string place, string via)
        {
            if (_weatherLocText != null)
                _weatherLocText.Text = "Đang dùng: " + place + " (" + via + ")";
            UpdateWidgetWeatherDesc();
        }

        // v3.11.1: the chon widget "Dong ho & thoi tiet" khong con ghi cung
        // "Ha Noi" trong mo ta — hien noi dang dung that.
        private void UpdateWidgetWeatherDesc()
        {
            if (_widgetWeatherDesc == null) return;
            string place = _weatherPlace;
            if (_weather == null
                && WeatherLocationLogic.NormalizeMode(_ui.WeatherMode) == WeatherLocationLogic.ModeManual)
            {
                string city = (_ui.WeatherCity ?? "").Trim();
                if (city.Length > 0) place = city;
            }
            _widgetWeatherDesc.Text = "Giờ thật của máy + thời tiết " + place + " (Open-Meteo, không cần API key)";
        }

        // Dong thoi tiet tren widget dong ho: xem truoc trong app va day
        // len TFT dung CHUNG mot cap ham (ban cu viet cung "Ha Noi" o duong
        // day len TFT nen ke ca khi tai duoc thoi tiet noi khac, man hinh
        // phim van hien Ha Noi).
        // v3.11.2: tach lam HAI dong cho khung TFT 128px — dong noi chon
        // rieng, dong nhiet do + trang thai rieng. Ban cu gop mot dong dai
        // ("Kuwana, Nhat Ban 12°C · Mua phun") nen tran ca hai ben man
        // hinh nho (Tien chup lai tu F75 Max that).
        private string WeatherPlaceText() => _weatherPlace;

        private string WeatherDetailText()
        {
            if (_weather != null)
                return $"{_weather.TempC}°C · {_weather.Description}";
            if (_weatherTried && !_weatherLastOk)
                return "chưa tải được thời tiết";
            return "chưa có dữ liệu thời tiết";
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
                SetWidgetBitmap(BuildNowPlayingBitmap());
            }
            else
            {
                // v3.11.1: chua co du lieu thi tu thu lai (lan loi truoc
                // cach it nhat 30 giay). Ban cu khoa cung bang _weatherTried:
                // mot lan loi la widget khong bao gio tu tai lai nua.
                if (_weather == null && (!_weatherTried ||
                    (!_weatherLastOk && (DateTime.UtcNow - _weatherLastAttemptUtc).TotalSeconds > 30)))
                    _ = FetchWeatherAsync();
                var bmp = WidgetRenderer.RenderClock(DateTime.Now, WeatherPlaceText(), WeatherDetailText());
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

        /// <summary>
        /// v3.15: dung bitmap widget Dang phat tu du lieu GSMTC that.
        /// Service chi khoi dong khi widget nay duoc xem/day (lazy —
        /// khong cham WinRT luc mo app). Chua co session -> khung Idle.
        /// </summary>
        private Bitmap BuildNowPlayingBitmap()
        {
            try { _nowPlaying.EnsureStarted(); } catch { }
            var snap = _nowPlaying.Snapshot;
            double phase = (DateTime.Now - DateTime.Today).TotalSeconds;
            if (snap == null || !snap.Available)
                return WidgetRenderer.RenderNowPlayingIdle();
            // v3.17: marquee — doi bai thi offset ve 0; moi lan dung
            // khung, renderer tra offset ke tiep (tien 3 ky tu/tick khi
            // tieu de qua dai, giu nguyen 0 khi tieu de xep vua 2 dong).
            string npTitle = snap.Title ?? "";
            if (!string.Equals(npTitle, _npMarqueeTitle, StringComparison.Ordinal))
            {
                _npMarqueeTitle = npTitle;
                _npMarqueeOffset = 0;
            }
            var data = new WidgetRenderer.MediaViewData
            {
                Title = snap.Title,
                Artist = snap.Artist,
                PositionSec = NowPlayingLogic.ProgressAt(snap, DateTime.UtcNow),
                DurationSec = snap.DurationSec,
                IsPlaying = snap.IsPlaying,
                ArtPngBytes = _nowPlaying.ArtPng,
                MarqueeOffset = _npMarqueeOffset,
            };
            var bmp = WidgetRenderer.RenderNowPlayingReal(data, phase, out int npNextOffset);
            _npMarqueeOffset = npNextOffset;
            return bmp;
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
                    bmp = BuildNowPlayingBitmap();
                }
                else
                {
                    bmp = WidgetRenderer.RenderClock(DateTime.Now, WeatherPlaceText(), WeatherDetailText());
                }
                byte[] stream;
                using (var img = BitmapToImageSharp(bmp))
                    stream = DisplayStreamBuilder.Build(new[] { img }, 1000);
                // v3.15: noi dung widget khong doi thi bo qua lan day
                // (dong ho dung yen, nhac tam dung o khung giong het…)
                // — tiet kiem kenh man hinh va thoi gian cho.
                if (!_widgetGate.ShouldPush(stream))
                {
                    AppendLog("Widget TFT: nội dung chưa thay đổi — bỏ qua lần đẩy này.");
                    ShowToast("Widget chưa thay đổi — không cần đẩy lại.");
                    return;
                }
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
            if (p.HasLighting)
            {
                // v3.12: ho so chua DU trang thai den — khoi phuc het.
                _rgb.Effect = p.EffectId;
                _rgb.Brush = Color.FromRgb((byte)ClampI(p.R, 0, 255), (byte)ClampI(p.G, 0, 255), (byte)ClampI(p.B, 0, 255));
                _rgb.Multi = p.Multi;
                _rgb.Brightness = ClampI(p.BrightnessUi, 0, 4);
                _rgb.Speed = ClampI(p.SpeedUi, 0, 4);
                _rgb.Direction = ClampI(p.Direction, 0, 3);
                _ = ApplyRgbAsync(p.EffectId, _rgb.Brush, _rgb.Multi, (int)_rgb.Brightness, (int)_rgb.Speed, _rgb.Direction);
                QueuePersistLighting();
            }
            else
            {
                // Ho so cu (chi co EffectId): giu hanh vi cu.
                _ = ApplyRgbAsync(p.EffectId, _rgb.Brush, _rgb.Multi, (int)_rgb.Brightness, (int)_rgb.Speed, _rgb.Direction);
                _rgb.Effect = p.EffectId;
                QueuePersistLighting();
            }
            if (_homeFxValue != null) _homeFxValue.Text = EffectFriendlyNames[p.EffectId];
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
                row.Children.Add(Tb($"{prof.ExeName} — {EffectFriendlyNames[prof.EffectId]}" +
                    (prof.HasLighting ? $" · #{prof.R:X2}{prof.G:X2}{prof.B:X2}" + (prof.Multi ? " · nhiều màu" : "") : "") +
                    (string.IsNullOrEmpty(prof.TftGifPath) ? "" : " + GIF TFT"), "sub"));
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
                        _phoneStatus.Text = $"Server đang CHẠY tại {_phone.Url} — quét QR bằng điện thoại (cùng Wi-Fi) để mở trang điều khiển. Mọi thao tác từ điện thoại áp dụng trực tiếp như trên PC.";
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
                Tb("4) Trên điện thoại: chọn nhanh hiệu ứng đèn, nạp GIF lên màn hình TFT, hoặc đẩy widget — PC nhận lệnh và ghi thẳng xuống phím.", "sub"),
                Tb("Lần đầu bật server, Windows Firewall có thể hỏi — hãy cho phép trong mạng Private/Home của bạn.", "faint")));

            var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("360,*"), ColumnSpacing = 14 };
            cols.Children.Add(qrCard);
            Grid.SetColumn(stepsCard, 1);
            cols.Children.Add(stepsCard);

            var root = V(14,
                Tb("Điều khiển từ điện thoại", "h1"),
                Tb("PC cắm phím và chạy app → app hiện mã QR thật → điện thoại quét (cùng Wi-Fi) → mở trang điều khiển → chọn hiệu ứng/GIF → PC ghi xuống phím.", "sub"),
                cols,
                CardFlat(Tb("Điện thoại không chỉnh trực tiếp được phím — điện thoại ra lệnh qua app PC, app PC mới ghi xuống phím.", "faint")));
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
            // v3.11: NEN DONG — GIF vong lap (tu video ngan) phat sau panel kinh
            if (skin == "live")
            {
                string lp = ResolveLiveWallpaperPath();
                if (lp == null)
                {
                    if (save) ShowToast("Chưa có nền động — hãy bấm “Nền động — chọn video” trong Cài đặt → Giao diện chủ đề để chọn video/GIF trước.");
                    skin = "default";
                }
                else
                {
                    StartLiveWallpaper(lp);
                    _ui.Background = "live";
                    if (save) SaveUi();
                    SyncWallpaperSelection();
                    return;
                }
            }
            StopLiveWallpaper();
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
                        SyncWallpaperSelection();
                        return;
                    }
                    catch (Exception ex) { AppendLog("Không nạp được ảnh nền riêng: " + ex.Message); skin = "default"; }
                }
            }
            string file = skin == "aurora" ? "skin-aurora.jpg"
                        : skin == "sunset" ? "skin-sunset.jpg"
                        : skin == "forest" ? "skin-forest.jpg"
                        : skin == "space" ? "skin-space.jpg"
                        : skin == "inkred" ? "skin-ink-red.jpg"
                        : skin == "inkteal" ? "skin-ink-teal.jpg"
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
            SyncWallpaperSelection();
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
                SyncWallpaperSelection();
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
            foreach (var id in new[] { GreetingStyles.PluginGalaxy, GreetingStyles.PluginDeepSeek, GreetingStyles.Plugin550C, GreetingStyles.PluginMoss, GreetingStyles.PluginParticles })
                if (GetPluginState(id).Enabled) ids.Add(id);
            // v3.11: plugin tu nhap dang bat cung la ung vien man chao
            foreach (var (m, _) in PluginInstaller.ListInstalled())
            {
                string cid = GreetingStyles.CustomPrefix + m.Id;
                if (GetPluginState(cid).Enabled) ids.Add(cid);
            }
            return ids;
        }

        private string ResolveCurrentGreetingStyle()
            => GreetingStyles.Resolve(EnabledBootPluginIds(), _ui.GreetingPlugin);

        private void RefreshGreetingStyleText()
        {
            if (_greetingStyleText == null) return;
            string style = ResolveCurrentGreetingStyle();
            string name = style == GreetingStyles.Custom
                        ? "Plugin của tôi: " + (PluginInstaller.FindInstalled(GreetingStyles.ManifestIdOf(_ui.GreetingPlugin)).Manifest?.Name ?? _ui.GreetingPlugin)
                        : style == GreetingStyles.Terminal ? "DeepSeek Harness — Rhine Lab (bản đầy đủ)"
                        : style == GreetingStyles.Amber550 ? "550C Boot + Thẻ ID (CRT hổ phách)"
                        : style == GreetingStyles.Moss ? "MOSS · Ignition"
                        : style == GreetingStyles.Particles ? "Hạt Bụi TV"
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
            // v3.10: thoi luong theo dung trinh tu cua tung theme
            // (GreetingSequence la nguon su that duy nhat cho cac hoi).
            int totalMs = style == GreetingStyles.Custom
                ? (PluginInstaller.FindInstalled(GreetingStyles.ManifestIdOf(
                       string.IsNullOrEmpty(_greetingCustomId) ? _ui.GreetingPlugin : _greetingCustomId)).Manifest?.TotalDurationMs ?? 4000)
                : GreetingSequence.For(style).TotalMs;
            _greetingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(totalMs) };
            _greetingTimer.Tick += (s, e) =>
            {
                _greetingTimer.Stop();
                GreetingStatus.Text = "Sẵn sàng";
                DismissGreeting();
            };
            _greetingTimer.Start();
        }

        private void StopGreetingAnims()
        {
            _terminalTimer?.Stop();
            _sweep?.Stop();
            _mossCtrl?.Stop();
            _particleCtrl?.Stop();
            _reticle?.Stop();
            _customSceneTimer?.Stop();
            _customAnimTimer?.Stop();
        }

        private void BuildGreetingBody(string style)
        {
            StopGreetingAnims();
            GreetingBodyHost.Children.Clear();
            // v3.10: cac theme giau (Rhine day du / 550C+The ID / MOSS / Hat
            // Bui) can san khau cao 302px; theme cu giu 118px nhu truoc.
            bool rich = style == GreetingStyles.Terminal || style == GreetingStyles.Amber550
                     || style == GreetingStyles.Moss || style == GreetingStyles.Particles
                     || style == GreetingStyles.Custom;
            GreetingBodyHost.Height = rich ? 302 : 118;

            if (style == GreetingStyles.Custom) BuildCustomGreeting();
            else if (style == GreetingStyles.Terminal) BuildRhineGreeting();
            else if (style == GreetingStyles.Amber550) Build550CGreeting();
            else if (style == GreetingStyles.Moss) BuildMossGreeting();
            else if (style == GreetingStyles.Particles) BuildParticlesGreeting();
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

        // ----- v3.10: DeepSeek Harness — Rhine Lab, BAN DAY DU 6 hoi -----
        // Cau truc 6 hoi cua ban goc (MIT, 6shenhonghong9/dsh-startup-screen)
        // tai hien bang Avalonia: bien den tieu de -> trang xoa -> ho so
        // tong am -> cap quyen 5 buoc -> lo dien sac do -> san sang.
        // Toan bo chu/thuong hieu fan-work da thay bang noi dung cua app.
        private void BuildRhineGreeting()
        {
            GreetingSub.Text = "DeepSeek Harness — Rhine Lab · cấp quyền 6 hồi";
            var st = GetPluginState(GreetingStyles.PluginDeepSeek);
            string name = st.GetCfg("name", "T OPERATOR");
            string code = st.GetCfg("code", "TKB-001");
            var mono = new FontFamily("Consolas, 'Courier New', monospace");
            var warm = Color.FromRgb(0xE8, 0xB4, 0x6A);

            var actLabel = new TextBlock
            {
                FontFamily = mono, FontSize = 11, LetterSpacing = 2,
                Foreground = new SolidColorBrush(warm), Text = "ACT I — TITLE SLATE",
            };
            // Tieu de 3 lop cho hoi lo dien sac do (do / trang / cyan lech nhau)
            var titleGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
            TextBlock TitleLayer(Color c, double dx) => new TextBlock
            {
                Text = "RHINE LAB // LIFE SCIENCES", FontFamily = mono, FontSize = 16.5,
                FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(c),
                Margin = new Thickness(dx, 0, 0, 0),
            };
            var layerR = TitleLayer(Color.FromRgb(0xFF, 0x4D, 0x4D), -1.6);
            var layerC = TitleLayer(Color.FromRgb(0x4D, 0xE8, 0xFF), 1.6);
            var layerW = TitleLayer(Color.FromRgb(0xF4, 0xF4, 0xF8), 0);
            layerR.Opacity = 0; layerC.Opacity = 0;
            titleGrid.Children.Add(layerR); titleGrid.Children.Add(layerC); titleGrid.Children.Add(layerW);

            _reticle = new ReticleControl { Width = 62, Height = 62, RingColor = warm };
            var dossier = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x13, 0x0A)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x59, 0xE8, 0xB4, 0x6A)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 9), Opacity = 0,
                Child = V(3,
                    Tb("HỒ SƠ NHÂN SỰ — RHINE LAB", null, 10.5),
                    Tb(name + " · " + code, null, 13),
                    Tb("Chức danh: OPERATOR · Cấp quyền: LEVEL 5", "faint", 11)),
            };
            ((TextBlock)((StackPanel)dossier.Child).Children[0]).Foreground = new SolidColorBrush(warm);
            ((TextBlock)((StackPanel)dossier.Child).Children[0]).FontFamily = mono;
            ((TextBlock)((StackPanel)dossier.Child).Children[1]).FontWeight = FontWeight.Bold;
            var dossierRow = H(12, _reticle, dossier);
            dossierRow.VerticalAlignment = VerticalAlignment.Center;

            var checkText = new TextBlock
            {
                FontFamily = mono, FontSize = 12, LineHeight = 18, Height = 92,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xE6, 0xA0)),
            };
            var barInner = new Border
            {
                Background = UiBrushes.AccentGradient(), CornerRadius = new CornerRadius(3),
                Height = 6, Width = 0, HorizontalAlignment = HorizontalAlignment.Left,
            };
            var barOuter = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(3), Height = 6, Child = barInner,
            };
            var flash = new Border { Background = Brushes.White, Opacity = 0, IsHitTestVisible = false };

            var stack = new StackPanel { Spacing = 9 };
            stack.Children.Add(actLabel);
            stack.Children.Add(titleGrid);
            stack.Children.Add(dossierRow);
            stack.Children.Add(checkText);
            stack.Children.Add(barOuter);
            var grid = new Grid();
            grid.Children.Add(stack);
            grid.Children.Add(flash);
            GreetingBodyHost.Children.Add(grid);

            var checks = new[]
            {
                "[1/5] xác thực danh tính ........ OK",
                "[2/5] đồng bộ hồ sơ thiết bị .... OK",
                "[3/5] nạp module RGB + TFT ...... OK",
                "[4/5] kiểm tra kênh HID ......... OK",
                "[5/5] cấp quyền OPERATOR ........ OK",
            };
            var acts = new[]
            {
                "ACT I — TITLE SLATE", "ACT II — WHITEOUT", "ACT III — DOSSIER",
                "ACT IV — AUTHORIZATION", "ACT V — CHROMATIC REVEAL", "ACT VI — READY",
            };
            int step = 0;
            const int totalSteps = 10;
            _terminalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _terminalTimer.Tick += (s, e) =>
            {
                step++;
                barInner.Width = 500.0 * Math.Min(1, (double)step / totalSteps);
                if (step == 1) { actLabel.Text = acts[1]; flash.Opacity = 0.92; }
                else if (step == 2) { actLabel.Text = acts[2]; flash.Opacity = 0; dossier.Opacity = 1; _reticle.Play(); }
                else if (step >= 3 && step <= 7)
                {
                    actLabel.Text = acts[3];
                    checkText.Text = string.Join("\n", checks.Take(step - 2));
                }
                else if (step == 8) { actLabel.Text = acts[4]; layerR.Opacity = 0.85; layerC.Opacity = 0.85; }
                else if (step == 9) { actLabel.Text = acts[5]; layerR.Opacity = 0; layerC.Opacity = 0; }
                else if (step >= 10)
                {
                    checkText.Text = string.Join("\n", checks) + $"\nACCESS GRANTED — WELCOME, {name}";
                    _terminalTimer.Stop();
                }
            };
            _terminalTimer.Start();
        }

        // ----- v3.10: 550C Boot + The ID -----
        // Boot terminal phoshor ho phach (co scanline) -> dang nhap UEG ->
        // THE ID ca nhan: ten + ma + serial suy ra tu dinh danh + ma QR THAT
        // (QRCoder) chua noi dung nguoi dung nhap. Ban goc MIT cua Ziyang
        // Song (intro 550C goc: Voidpocket) — tai hien bang Avalonia.
        private void Build550CGreeting()
        {
            GreetingSub.Text = "550C Boot + Thẻ ID — terminal hổ phách & thẻ truy cập cá nhân";
            var st = GetPluginState(GreetingStyles.Plugin550C);
            string name = st.GetCfg("name", "T OPERATOR");
            string code = st.GetCfg("code", "TKB-001");
            string serial = IdCardSerial.Build(name, code);
            var mono = new FontFamily("Consolas, 'Courier New', monospace");
            var amber = Color.FromRgb(0xFF, 0xB0, 0x20);

            var termText = new TextBlock
            {
                FontFamily = mono, FontSize = 12.5, LineHeight = 19,
                Foreground = new SolidColorBrush(amber),
            };
            var termInner = new Grid();
            termInner.Children.Add(termText);
            termInner.Children.Add(new ScanlineOverlay());
            var termPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x08, 0x02)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x59, 0xFF, 0xB0, 0x20)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 11), Child = termInner, Height = 218,
            };

            // --- The ID ---
            Bitmap qrBmp = null;
            try
            {
                var gen = new QRCodeGenerator();
                var data = gen.CreateQrCode(QrPayloadBuilder.Build(name, code, st.GetCfg("qrtext", "")), QRCodeGenerator.ECCLevel.M);
                var png = new PngByteQRCode(data).GetGraphic(8);
                qrBmp = new Bitmap(new System.IO.MemoryStream(png));
            }
            catch { qrBmp = null; }
            var avatar = new Border
            {
                Width = 58, Height = 58, CornerRadius = new CornerRadius(10),
                Background = UiBrushes.AccentGradient(),
                Child = new TextBlock
                {
                    Text = (name.Length > 0 ? name.Substring(0, 1) : "T").ToUpperInvariant(),
                    FontSize = 24, FontWeight = FontWeight.Bold, Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                },
            };
            var idTexts = V(3,
                Tb(name, null, 16),
                Tb("Mã: " + code + " · Serial: " + serial, "sub", 11.5),
                Tb("Cấp quyền: OPERATOR · Đơn vị: TIEN VU LAB", "faint", 11));
            ((TextBlock)((StackPanel)idTexts).Children[0]).FontWeight = FontWeight.Bold;
            var qrBox = new Border
            {
                Background = Brushes.White, CornerRadius = new CornerRadius(8),
                Padding = new Thickness(5), Width = 118, Height = 118,
                Child = qrBmp != null
                    ? (Control)new Avalonia.Controls.Image { Source = qrBmp, Width = 108, Height = 108 }
                    : new QrMock { Width = 108, Height = 108 },
            };
            var granted = Tb("ACCESS GRANTED", null, 12.5);
            granted.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
            granted.FontFamily = mono; granted.FontWeight = FontWeight.Bold;
            var idCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x20)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x59, 0x67, 0xE8, 0xF9)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 13), IsVisible = false,
                Child = V(9,
                    H(8, Tb("TV ACCESS CARD", null, 13), Tb("· TIEN VU KEYBOARD STUDIO", "faint", 11)),
                    H(14, avatar, idTexts, qrBox),
                    H(8, granted, Tb("Quét mã QR để xem nội dung thẻ", "faint", 10.5))),
            };
            ((TextBlock)((StackPanel)((StackPanel)idCard.Child).Children[0]).Children[0]).FontWeight = FontWeight.Bold;

            var host = new Grid();
            host.Children.Add(termPanel);
            host.Children.Add(idCard);
            GreetingBodyHost.Children.Add(host);

            var lines = new[]
            {
                "550C PERSONAL TERMINAL — PHOSPHOR BIOS v2.7",
                "MEM CHECK ............ 640K OK",
                "CRT TUBE ............. AMBER · WARM-UP OK",
                "KEYBOARD LINK ........ HID 0C45:800A OK",
                $"OPERATOR ............. {name} · {code}",
                "LOADING UEG LOGIN ...",
            };
            int step = 0;
            _terminalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _terminalTimer.Tick += (s, e) =>
            {
                step++;
                if (step <= lines.Length)
                    termText.Text = string.Join("\n", lines.Take(step)) + (step < lines.Length ? "\n_" : "");
                else if (step == lines.Length + 2)
                {
                    termText.Text = string.Join("\n", lines) + "\nUEG LOGIN: VERIFYING ... OK";
                }
                else if (step == lines.Length + 4)
                {
                    termPanel.IsVisible = false;
                    idCard.IsVisible = true;
                    _terminalTimer.Stop();
                }
            };
            _terminalTimer.Start();
        }

        // ----- v3.10: MOSS · Ignition (tai hien phong cach, ve thu tuc) -----
        private void BuildMossGreeting()
        {
            GreetingSub.Text = "MOSS · Ignition — tái hiện phong cách, vẽ thủ tục 100%";
            var st = GetPluginState(GreetingStyles.PluginMoss);
            _mossCtrl = new MossSceneControl { PilotName = st.GetCfg("name", "T OPERATOR"), Height = 302 };
            var wrap = new Border
            {
                CornerRadius = new CornerRadius(12), ClipToBounds = true,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0x3B, 0x30)),
                BorderThickness = new Thickness(1), Child = _mossCtrl,
            };
            GreetingBodyHost.Children.Add(wrap);
            _mossCtrl.Play();
        }

        // ----- v3.10: Hat Bui TV (hat tu thanh logo Mau F) -----
        private static List<LogoParticleSampler.TargetPoint> _logoTargetsCache;

        private static List<LogoParticleSampler.TargetPoint> LogoTargets()
        {
            if (_logoTargetsCache != null) return _logoTargetsCache;
            _logoTargetsCache = new List<LogoParticleSampler.TargetPoint>();
            try
            {
                using (var s = AssetLoader.Open(GifTools.Asset("Assets/logo.png")))
                using (var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(s))
                {
                    img.Mutate(m => m.Resize(240, 240));
                    var rgba = new byte[240 * 240 * 4];
                    img.CopyPixelDataTo(rgba);
                    _logoTargetsCache = LogoParticleSampler.Sample(rgba, 240, 240, ParticleMorphControl.ParticleCount, 20261127);
                }
            }
            catch { /* loi doc logo: control tu dung dich vong xoan du phong */ }
            return _logoTargetsCache;
        }

        private void BuildParticlesGreeting()
        {
            GreetingSub.Text = $"Hạt Bụi TV — {ParticleMorphControl.ParticleCount:N0} hạt tụ thành logo Mẫu F";
            _particleCtrl = new ParticleMorphControl { Height = 302 };
            _particleCtrl.SetTargets(LogoTargets());
            var wrap = new Border
            {
                CornerRadius = new CornerRadius(12), ClipToBounds = true,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x7C, 0x3A, 0xED)),
                BorderThickness = new Thickness(1), Child = _particleCtrl,
            };
            GreetingBodyHost.Children.Add(wrap);
            _particleCtrl.Play();
        }

        public void DismissGreeting()
        {
            if (!GreetingOverlay.IsVisible) return;
            _greetingTimer?.Stop();
            StopGreetingAnims();
            GreetingOverlay.Opacity = 0;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
            t.Tick += (s, e) => { t.Stop(); GreetingOverlay.IsVisible = false; };
            t.Start();
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
            public string Attribution; // v3.10: ghi cong ben thu ba — tu v3.11.1 chi con o Gioi thieu + README/NOTICE, modal khong hien nua
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
                Name = "DeepSeek Harness — Rhine Lab (bản đầy đủ)", Author = "6shenhonghong9", Cat = "boot", CatLabel = "Hiệu ứng khởi động",
                Desc = "Màn chào cấp quyền 6 hồi kiểu hồ sơ khoa học: biển tiêu đề → trắng xoá → hồ sơ → cấp quyền 5 bước → lộ diện sắc độ.",
                Detail = "Bản ĐẦY ĐỦ (v3.10) của màn chào terminal: 6 hồi như bản gốc cộng đồng — biển đen tiêu đề, flash trắng, thẻ hồ sơ tông ấm có vòng reticle xoay, checklist cấp quyền 5 bước tích lần lượt, kết thúc bằng lộ diện sắc độ và ACCESS GRANTED với tên/mã định danh của bạn. Cấu trúc 6 hồi được tái hiện bằng Avalonia theo bản gốc MIT; toàn bộ chữ/thương hiệu fan-work đã thay bằng nội dung của app. Tuỳ chọn đọc bằng giọng nói đang được phát triển — cấu hình được lưu sẵn.",
                Meta = "Phiên bản 2.0.0 · Có sẵn trong app · Miễn phí · MIT", Thumb = "thumb-ds-harness.png",
                Attribution = GreetingThemeRegistry.Find("deepseek-harness")?.Attribution,
            },
            new PluginItem
            {
                Id = "550c-boot",
                Name = "550C Boot + Thẻ ID", Author = "yannicksong0106 (Ziyang Song)", Cat = "boot", CatLabel = "Hiệu ứng khởi động",
                Desc = "Boot terminal phosphor hổ phách kiểu 550C, đăng nhập UEG, rồi in THẺ ID cá nhân có mã QR thật của bạn.",
                Detail = "Bản nâng cấp đầy đủ (v3.10): terminal CRT hổ phách có scanline chạy chuỗi boot (kiểm tra bộ nhớ → ống CRT → liên kết bàn phím → định danh), chuyển sang đăng nhập UEG và hiện THẺ TRUY CẬP cá nhân: tên, mã, serial suy ra từ định danh của bạn và mã QR THẬT chứa nội dung bạn tự nhập ở phần cấu hình (để trống thì QR chứa tên + mã định danh). Bỏ qua được bằng click/Esc như mọi màn chào.",
                Meta = "Phiên bản 2.0.0 · Có sẵn trong app · Miễn phí · MIT", Thumb = "thumb-550c.png",
                Attribution = GreetingThemeRegistry.Find("550c-boot")?.Attribution,
            },
            new PluginItem
            {
                Id = "moss-ignition",
                Name = "MOSS · Ignition", Author = "Tái hiện phong cách — QIFANAI", Cat = "boot", CatLabel = "Hiệu ứng khởi động",
                Desc = "Màn chào điện ảnh 4 hồi: liên kết thần kinh → mắt đỏ khoá mục tiêu → tách đôi → hành tinh bốc cháy.",
                Detail = "Bản TÁI HIỆN PHONG CÁCH (v3.10), vẽ thủ tục 100% bằng Avalonia — không dùng bất kỳ video/hình ảnh nào của bản gốc: lưới điểm + vòng quét đồng bộ thần kinh, mắt đỏ pulse khoá tâm ngắm, màn hình tách đôi, rồi hành tinh vẽ gradient với vòng quỹ đạo và 420 hạt lửa bốc lên, kết bằng tên app. Tiết tấu học từ theme MOSS 550W của cộng đồng DSH (MIT).",
                Meta = "Phiên bản 1.0.0 · Có sẵn trong app · Miễn phí", Thumb = "thumb-moss-ignition.png",
                Attribution = GreetingThemeRegistry.Find("moss-ignition")?.Attribution,
            },
            new PluginItem
            {
                Id = "hat-bui-tv",
                Name = "Hạt Bụi TV", Author = "Ý tưởng: inju0524 (INJU Particle Boot)", Cat = "boot", CatLabel = "Hiệu ứng khởi động",
                Desc = "3.200 hạt bụi xoay xoắn ốc rồi TỤ THÀNH đúng logo Mẫu F của app, mang màu chrome ngũ sắc của logo.",
                Detail = "Theme “của nhà trồng được” (v3.10): một trường 3.200 hạt vẽ bằng DrawingContext xoay theo xoắn ốc phyllotaxis, sau đó biến hình (morph) về các điểm đích lấy mẫu trực tiếp từ ảnh logo Mẫu F — hạt mang đúng màu của logo — rồi lắng xuống và lộ tên app. Ý tưởng hạt biến hình từ INJU Particle Boot (MIT); hình ảnh sinh 100% bằng code, không vay mượn asset của ai.",
                Meta = "Phiên bản 1.0.0 · Có sẵn trong app · Miễn phí · MIT", Thumb = "thumb-hat-bui.png",
                Attribution = GreetingThemeRegistry.Find("hat-bui-tv")?.Attribution,
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
                Desc = "Màn hình chào riêng của Tien Vu: vệt sáng cầu vồng quét qua hàng phím + logo Mẫu F.",
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
                Child = new Avalonia.Controls.Image { Source = it.Thumb != null ? LoadThumb(it.Thumb) : LogoBitmap(), Stretch = Stretch.UniformToFill },
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

            // v3.11.1: DA GO khoi ghi cong khoi cuoi trang Cho plugin theo
            // yeu cau gon giao dien cua Tien. Ghi cong MIT (bat buoc phap
            // ly) chi con o HAI noi: muc "Nguon theme & giay phep" trong
            // Cai dat -> Gioi thieu, va README/NOTICE di kem ban phat hanh.

            _myPluginsHost = new StackPanel { Spacing = 10 };
            var root = V(14,
                Tb("Chợ plugin", "h1"),
                Tb("Kho cộng đồng: hiệu ứng khởi động, công cụ hình nền và gói hiệu ứng đèn — bấm vào một thẻ để mở panel chi tiết: bật/tắt, cấu hình và xem trước.", "sub"),
                BuildImportCard(),
                Tb("Plugin của tôi", "h2"),
                _myPluginsHost,
                Tb("Plugin có sẵn trong app", "h2"),
                filterRow,
                wrap,
                CardFlat(Tb("Plugin đang bật sẽ là màn hình chào mỗi lần mở app. Mục “Ý tưởng · đang phát triển” chưa dùng được.", "faint")));
            RefreshMyPlugins();
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
                Child = new Avalonia.Controls.Image { Source = it.Thumb != null ? LoadThumb(it.Thumb) : LogoBitmap(), Stretch = Stretch.UniformToFill },
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
            // v3.11.1: modal khong con dong ghi cong (da gom ve Gioi thieu).
            inner.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)) });
            inner.Children.Add(enableRow);
            foreach (var row in PluginConfigRows(it, state)) inner.Children.Add(row);
            var modalBtns = new List<Control>
            {
                PrimaryBtn("Xem trước", () =>
                {
                    if (it.Cat == "boot") { ClosePluginModal(); PreviewPlugin(it); }
                    else ShowToast("Plugin đang ở trạng thái ý tưởng — chưa có bản xem trước thật.");
                }),
                GhostBtn("Đóng", () => ClosePluginModal()),
            };
            if (GreetingStyles.IsCustomId(it.Id))
                modalBtns.Add(GhostBtn("Xoá plugin", () => { ClosePluginModal(); DeleteCustomPlugin(it); }));
            inner.Children.Add(H(10, modalBtns.ToArray()));

            return new Border
            {
                Classes = { "card" },
                Background = new SolidColorBrush(Color.FromArgb(0xF5, 0x1B, 0x1B, 0x26)),
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
                    rows.Add(Tb("Tên + mã định danh hiển thị thật trong hồ sơ và dòng ACCESS GRANTED của màn chào Rhine Lab.", "faint"));

                if (it.Id == GreetingStyles.Plugin550C)
                {
                    var qrBox = new TextBox { Text = state.GetCfg("qrtext", ""), Width = 220, Watermark = "Để trống = tên + mã của bạn" };
                    qrBox.Classes.Add("search");
                    qrBox.LostFocus += (s, e) => { state.Config["qrtext"] = (qrBox.Text ?? "").Trim(); SaveUi(); };
                    rows.Add(Row("Nội dung mã QR trên thẻ", qrBox));
                    rows.Add(Tb("Mã QR trên thẻ ID là QR THẬT, quét được: chứa đúng nội dung bạn nhập (link, chữ…). Serial của thẻ tự suy ra từ tên + mã định danh.", "faint"));
                }
            }
            return rows;
        }

        // ================= v3.3: GAMESENSE (CS2 GSI — THAT) =================
        // CS2 tu gui trang thai tran ve 127.0.0.1:<port> qua file cfg ma app
        // tao (chuan mo Game State Integration cua Valve). App chi NHAN du
        // lieu game gui; khong doc bo nho game, khong inject, khong nghe
        // ngoai localhost. Phan ung den phim (neu bat) di qua dung
        // ExperimentalDevice nhu lenh 0x20 o trang Per-key (gui truc tiep).
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
                Tb("CS2 gửi trạng thái trận đấu về app qua Game State Integration (chuẩn mở của Valve).", "sub")));
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
                Tb("Mỗi sự kiện trong trận có thể kích hoạt một hành động trên máy — và (chỉ có ở T Keyboard) cả phản ứng bằng đèn phím.", "sub"),
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
                SettingRow("Phản ứng bằng đèn phím", "Mọi phản ứng đèn của GameSense gửi thẳng xuống đèn phím qua stream 0x20.", gs.LightingEnabled,
                    v => { gs.LightingEnabled = v; SaveUi(); })));

            // --- Diem hon ban goc ---
            var plusCard = Card(V(8,
                Tb("Điểm hơn bản gốc", "h2"),
                Tb("Tham khảo app gốc: GameSense ở đó cũng chỉ mở web/chạy chương trình + overlay chống loá — bản này làm tương tự, cộng thêm phản ứng bằng đèn phím.", "sub")));
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

        // Gui mot frame stream phan ung game qua ExperimentalDevice (gui
        // truc tiep nhu trang Per-key). restoreAfter: gui xong thi tra lai
        // hieu ung RGB hien tai bang kenh da kiem chung 04 13.
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
                var tray = new TrayIcon { Icon = icon, ToolTipText = "Tien Vu Keyboard Studio" };
                var menu = new NativeMenu();
                var openItem = new NativeMenuItem("Mở Tien Vu Keyboard Studio");
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
