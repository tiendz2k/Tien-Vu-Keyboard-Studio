using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Color = Avalonia.Media.Color;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.11 — Phan MainWindow cho ban giao dien AULA (Preview v0.11.3 da
    // duyet) + nen dong + nhap plugin thu cong + thoi tiet tu dong:
    // thanh tren cung, PANEL CAI DAT phu (mini-sidebar), muc Giao dien chu
    // de THAT, nen dong tu video ngan, the vi tri thoi tiet, nhap plugin
    // du lieu va engine man chao cho plugin tu nhap. Cac file giao thuc
    // duoc bao ve khong he bi dong toi.
    // =====================================================================
    public partial class MainWindow
    {
        // --- Thanh tren cung + panel cai dat ---
        private readonly List<Border> _topAccentDots = new List<Border>();
        private bool _lightUi;
        private readonly Dictionary<string, Control> _settingsSections = new Dictionary<string, Control>();
        private readonly List<(string Key, Button Btn)> _settingsNav = new List<(string, Button)>();
        private string _settingsSection = "";

        // --- Muc Giao dien chu de ---
        private readonly List<(ThemeTileDef Tile, Border Card, Button Btn)> _paletteTiles = new List<(ThemeTileDef, Border, Button)>();
        private readonly List<(string Id, Border Tile, Button Btn)> _wallpaperTiles = new List<(string, Border, Button)>();
        private Border _wallpaperUploadTile;
        private Button _wallpaperUploadBtn;
        private Border _liveTile;
        private Button _liveTileBtn;
        private TextBlock _liveStatusText;
        private Button _livePauseBtn;

        // --- Nen dong ---
        private List<Bitmap> _liveFrames;
        private List<int> _liveDelays;
        private int _liveIdx;
        private DispatcherTimer _liveTimer;
        private string _liveLoadedPath;

        // --- Thoi tiet ---
        private TextBlock _weatherLocText;
        private TextBlock _weatherStatusText; // v3.11.1: dong trang thai tai thoi tiet (hien ro loi)
        private TextBox _weatherCityBox;
        private string _weatherPlace = "Hà Nội";

        // --- Cho plugin: nhap thu cong ---
        private TextBox _importUrlBox;
        private TextBlock _importStatus;
        private StackPanel _myPluginsHost;

        // --- Man chao plugin tu nhap ---
        private string _greetingCustomId;
        private DispatcherTimer _customSceneTimer;
        private DispatcherTimer _customAnimTimer;
        private Action _customAnimTick;

        private static Bitmap _logoBmpCache;
        private static Bitmap LogoBitmap()
            => _logoBmpCache ??= new Bitmap(AssetLoader.Open(GifTools.Asset("Assets/logo.png")));

        // ================= THANH TREN CUNG (kieu AULA Hub) =================
        private void BuildTopBar()
        {
            for (int i = 0; i < ThemeTiles.All.Length; i++)
            {
                var t = ThemeTiles.All[i];
                var ca = Color.FromRgb(t.A.R, t.A.G, t.A.B);
                var cb = Color.FromRgb(t.B.R, t.B.G, t.B.B);
                var dot = new Border
                {
                    Width = 18, Height = 18, CornerRadius = new CornerRadius(999),
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(ca, 0), new GradientStop(cb, 1) },
                    },
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ToolTip.SetTip(dot, "Màu chủ đề: " + t.Name);
                if (t.Legacy)
                {
                    // Loi trang o giua — dau nhan dien rieng cua "Nguyen ban"
                    // (cung cap mau voi "Cuc quang" nhung doi ca CHROME).
                    dot.Child = new Avalonia.Controls.Shapes.Ellipse
                    {
                        Width = 6, Height = 6,
                        Fill = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                }
                string tid = t.Id;
                dot.PointerPressed += (s, e) => ApplyThemeTile(tid);
                _topAccentDots.Add(dot);
                TopAccentRow.Children.Add(dot);
            }
            RefreshTopDots();

            TopThemeBtn.Click += (s, e) =>
            {
                _lightUi = !_lightUi;
                TopThemeIcon.Data = StreamGeometry.Parse(_lightUi
                    ? "M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10Z M12 2v2 M12 20v2 M4.9 4.9l1.4 1.4 M17.7 17.7l1.4 1.4 M2 12h2 M20 12h2 M4.9 19.1l1.4-1.4 M17.7 6.3l1.4-1.4"
                    : "M20 13.2A8.5 8.5 0 1 1 10.8 4 6.8 6.8 0 0 0 20 13.2Z");
                ShowToast(_lightUi
                    ? "Chế độ Sáng đang được phát triển — hiện mới có ở dạng THẺ XEM TRƯỚC trong Cài đặt → Giao diện chủ đề, chưa áp dụng toàn app."
                    : "Đã về chế độ Tối");
            };
            TopSettingsBtn.Click += (s, e) => OpenSettings("general");
        }

        private void RefreshTopDots()
        {
            string cur = ThemeTiles.ResolveId(_ui);
            for (int i = 0; i < _topAccentDots.Count; i++)
            {
                bool sel = ThemeTiles.All[i].Id == cur;
                _topAccentDots[i].BorderBrush = sel ? UiBrushes.SelBorder : new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
                _topAccentDots[i].BorderThickness = new Thickness(sel ? 1.6 : 1);
            }
        }

        // ================= PANEL CAI DAT (phu, mini-sidebar) =================
        private static readonly Dictionary<string, string> SettingsTitles = new Dictionary<string, string>
        {
            { "general", "Cài đặt chung" },
            { "theme", "Giao diện chủ đề" },
            { "gamesense", "GameSense" },
            { "feedback", "Phản hồi" },
            { "about", "Giới thiệu" },
        };

        private void BuildSettingsOverlay()
        {
            _settingsSections["general"] = BuildGeneralSection();
            _settingsSections["theme"] = BuildThemeSectionReal();
            _settingsSections["gamesense"] = BuildGameSensePage();
            _settingsSections["feedback"] = BuildFeedbackSection();
            _settingsSections["about"] = BuildAboutSectionReal();
            foreach (var s in _settingsSections.Values) { s.IsVisible = false; SettingsHost.Children.Add(s); }

            var items = new (string Key, string Label)[]
            {
                ("general", "Cài đặt chung"),
                ("theme", "Giao diện chủ đề"),
                ("gamesense", "GameSense"),
                ("feedback", "Phản hồi"),
                ("about", "Giới thiệu"),
            };
            foreach (var (key, label) in items)
            {
                var b = new Button { Content = Tb(label, null, 13.5) };
                b.Classes.Add("nav");
                string k = key;
                b.Click += (s, e) => ShowSettingsSection(k);
                _settingsNav.Add((key, b));
                SettingsNavPanel.Children.Add(b);
            }
            SettingsBackBtn.Click += (s, e) => CloseSettings();
            SettingsDim.PointerPressed += (s, e) => CloseSettings();
            ShowSettingsSection("general");
        }

        /// <summary>Mo panel Cai dat tai mot muc (mac dinh: Cai dat chung).</summary>
        public void OpenSettings(string section)
        {
            SettingsOverlay.IsVisible = true;
            SettingsOverlay.Opacity = 1;
            ShowSettingsSection(section);
            _clockTimer.IsEnabled = true;
            UpdateClockLabels();
            RefreshTypingLabels();
            RefreshGreetingStyleText();
        }

        private void CloseSettings()
        {
            if (!SettingsOverlay.IsVisible) return;
            SettingsOverlay.Opacity = 0;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
            t.Tick += (s, e) => { t.Stop(); SettingsOverlay.IsVisible = false; };
            t.Start();
            _clockTimer.IsEnabled = false;
        }

        private void ShowSettingsSection(string key)
        {
            if (!_settingsSections.ContainsKey(key)) key = "general";
            _settingsSection = key;
            foreach (var kv in _settingsSections) kv.Value.IsVisible = kv.Key == key;
            foreach (var (k, btn) in _settingsNav) SetCls(btn, "sel", k == key);
            if (SettingsTitles.TryGetValue(key, out var title)) SettingsTitle.Text = title;
        }

        // ---------- Muc 1: Cai dat chung (dong ho + 04 17 + app + Stage 2) ----------
        private Control BuildGeneralSection()
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
                Tb("Các công tắc dưới đây ghi xuống khối cài đặt của phím (khoá Win, tự ngủ, độ nhạy…) — chỉ áp dụng cho F75 Max.", "sub"),
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
                SettingRow("Hiện nhật ký thao tác", "Hiện panel nhật ký ở Trang chủ và trang Per-key — chỉ cần khi kiểm tra/gỡ lỗi. Mặc định tắt cho gọn giao diện; nhật ký vẫn được ghi ngầm bên trong, bật lên là thấy lịch sử gần đây.", _ui.ShowOperationLog, v =>
                {
                    _ui.ShowOperationLog = v;
                    SaveUi();
                    ApplyLogVisibility();
                }),
                SettingRow("Khởi động cùng Windows", "Tự mở app khi đăng nhập Windows (ghi vào HKCU Run)", GetAutostart(), v => SetAutostart(v)),
                SettingRow("Thu nhỏ xuống khay hệ thống", "Đóng cửa sổ thì app vẫn chạy nền ở khay", true, v => _trayEnabled = v),
                Tb("App không tự kiểm tra cập nhật; chỉ gọi mạng khi bạn chủ động bấm (tải GIF, thời tiết, danh sách mẫu).", "faint")));

            // --- Bang thiet bi cong dong ---
            _regStatus = Tb(RegistryStatusText(), "sub");
            var regCard = Card(V(8,
                Tb("Bảng thiết bị cộng đồng", "h2"),
                Tb("Danh sách mẫu có thể cập nhật không cần bản app mới — bấm nút dưới để tải bản mới nhất từ repo của dự án.", "sub"),
                _regStatus,
                GhostBtn("Kiểm tra cập nhật bảng thiết bị", async () => await CheckRegistryUpdateAsync())));

            // --- Stage 2: ho so theo app ---
            _profileCurrent = Tb("App đang mở: —", "sub");
            _profileList = new StackPanel { Spacing = 6 };
            RefreshProfileList();
            var profileCard = Card(V(10,
                Tb("Hồ sơ theo ứng dụng (Stage 2)", "h2"),
                Tb("Khi bật, app tự áp hồ sơ RGB/GIF đã gán khi bạn chuyển sang app đó.", "sub"),
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
                    // v3.12: luu DU trang thai den vao ho so (khong chi EffectId)
                    p.HasLighting = true;
                    p.R = _rgb.Brush.R; p.G = _rgb.Brush.G; p.B = _rgb.Brush.B;
                    p.Multi = _rgb.Multi;
                    p.BrightnessUi = (int)_rgb.Brightness;
                    p.SpeedUi = (int)_rgb.Speed;
                    p.Direction = _rgb.Direction;
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

            var root = V(14, clockCard, devSetCard, appCard, regCard, profileCard, macroCard, typingCard);
            return Scroll(root);
        }

        // ---------- Muc 4: Phan hoi (that: soan -> sao chep / mo GitHub Issues) ----------
        private Control BuildFeedbackSection()
        {
            var box = new TextBox
            {
                Watermark = "Kể cho tác giả nghe: lỗi bạn gặp, tính năng bạn muốn có…",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 120,
                FontSize = 13,
            };
            box.Classes.Add("search");
            var card = Card(V(10,
                Tb("Gửi phản hồi", "h2"),
                Tb("Phản hồi đi tới trang GitHub Issues của dự án (công khai).", "sub"),
                box,
                H(10,
                    PrimaryBtn("Sao chép phản hồi", async () =>
                    {
                        if (string.IsNullOrWhiteSpace(box.Text)) { ShowToast("Hãy viết vài dòng phản hồi trước đã."); return; }
                        var clip = TopLevel.GetTopLevel(this)?.Clipboard;
                        if (clip != null) { await clip.SetTextAsync(box.Text); ShowToast("Đã sao chép phản hồi — dán vào issue mới trên GitHub nhé."); }
                    }),
                    GhostBtn("Mở GitHub Issues", () => OpenUrlSafe("https://github.com/tiendz2k/Tien-Vu-Keyboard-Studio/issues")),
                    GhostBtn("Xoá nội dung", () => box.Text = ""))));
            return Scroll(V(14, card));
        }

        // ---------- Muc 5: Gioi thieu ----------
        // v3.11.1: ghi cong MIT gom ve MOT noi duy nhat trong UI — khoi
        // nay o muc Gioi thieu cua panel Cai dat (trang Cho plugin va
        // modal chi tiet khong con dong ghi cong). Ban day du nam trong
        // README/NOTICE di kem ban phat hanh — nhu vay van dung nghia vu
        // ghi cong cua giay phep MIT.
        private Control BuildAboutCredits()
        {
            var lines = new List<Control> { Tb("Nguồn theme & giấy phép", "h3") };
            foreach (var line in GreetingThemeRegistry.AboutCreditLines())
                lines.Add(Tb(line, "faint"));
            var creditsClosing = Tb("Mọi theme đều được viết lại bằng Avalonia trong app này; app không tải hay chạy mã của bên thứ ba. Bản đầy đủ của các giấy phép nằm trong README đi kèm bản phát hành.", "faint");
            creditsClosing.MaxWidth = 640;
            lines.Add(creditsClosing);
            return V(3, lines.ToArray());
        }

        private Control BuildAboutSectionReal()
        {
            var logoImg = new Avalonia.Controls.Image
            {
                Source = LogoBitmap(),
                Width = 64, Height = 64,
            };
            var logoWrap = new Border
            {
                CornerRadius = new CornerRadius(14), ClipToBounds = true,
                Width = 64, Height = 64, Child = logoImg,
            };
            var aboutPara = Tb("Phiên bản 3.12.0 — nâng cấp ánh sáng: bộ chọn màu tự do (vòng HSV + nhập RGB/hex + màu gần đây) cho trang RGB và vẽ per-key; scheme đèn có tên (lưu/áp dụng/đổi tên/xoá, xuất/nhập JSON + XML); mở khoá tham số nhạc 0x78 (kiểu nhịp, biên độ, nền, sáng nền); hiệu ứng thời gian thực stream 0x20 có tên + slider độ sáng/ngưỡng đen cho GIF→LED; trạng thái đèn được nhớ qua các lần mở app và hồ sơ theo ứng dụng lưu đủ bộ thông số. F75 Max hỗ trợ đầy đủ; các họ còn lại định danh bằng probe đọc trước khi ghi, chưa kiểm chứng trên phần cứng thật. Dry-run bật mặc định.", "sub");
            aboutPara.MaxWidth = 640; // cot ngang H() cho chieu rong vo han — gioi han de chu xuong dong trong the
            var aboutTexts = V(4,
                Tb("Tien Vu Keyboard Studio", "h2"),
                aboutPara,
                Tb("Phát triển bởi Vũ Minh Tiến — github.com/tiendz2k", "sub"),
                Tb("Mã nguồn + bản chính thức: github.com/tiendz2k/Tien-Vu-Keyboard-Studio · " + AboutInfo.SignatureLine, "faint"),
                Tb("Font chữ: Be Vietnam Pro (SIL OFL 1.1). GIF thư viện: tự làm cho Tiến. Giao thức per-key: Gavin Graham (AULA L99).", "faint"),
                BuildAboutCredits());
            var aboutCard = Card(H(14, logoWrap, aboutTexts));
            return Scroll(V(14, aboutCard));
        }

        // ================= MUC 2: GIAO DIEN CHU DE (THAT) =================
        // Port tu Preview v0.11.3 da duyet, noi vao trang thai that:
        // o mau lon -> ApplyThemeTile (luu settings.json); hang hinh nen ->
        // ApplyBackground that (ke ca 2 tranh muc va tai len 4MB da nen
        // JPEG that); nen dong tu video ngan; do mo kinh luu lai.

        /// <summary>Ap dung mot o mau chu de: mau + chrome (AULA hoac Nguyen ban).</summary>
        public void ApplyThemeTile(string tileId, bool save = true)
        {
            var t = ThemeTiles.Find(tileId) ?? ThemeTiles.All[0];
            _ui.ThemeTile = t.Id;
            _ui.AccentIndex = t.AccentIndex;
            ApplyAccent(Color.FromRgb(t.A.R, t.A.G, t.A.B), Color.FromRgb(t.B.R, t.B.G, t.B.B));
            ApplyChromePalette(ChromePalettes.For(t));
            UiBrushes.LegacyChrome = t.Legacy;
            SetCls(this, "legacy", t.Legacy);
            foreach (var sw in this.GetVisualDescendants().OfType<PillSwitch>()) sw.InvalidateVisual();
            RefreshTopDots();
            RefreshPaletteTiles();
            RefreshNavSelectionColors();
            if (save)
            {
                SaveUi();
                ShowToast("Đã áp dụng màu chủ đề: " + t.Name + (t.Legacy ? " — chrome gradient của bản trước" : ""));
            }
        }

        // v3.11.2: ap bo mau chrome cua o dang chon len MOI be mat lon.
        // Nguyen nhan ban cu "doi o mau ma khong thay gi": ApplyAccent chi
        // doi gradient accent (diem nhan nho) trong khi the kinh/thanh
        // ben/panel Cai dat/lop phu hinh nen la mau trung tinh co dinh va
        // trang thai dang chon kieu AULA la pill trang trung tinh. Gio:
        // doi .Color TAI CHO tren cac brush dung chung (ca XAML resource
        // lan UiBrushes) — moi control dang giu instance deu tu ve lai,
        // khong can dung lai trang, ke ca tren hinh nen phu kin cua so.
        private void ApplyChromePalette(ChromePalette p)
        {
            // Brush code-side dung chung
            UiBrushes.CardFill.Color = Color.FromArgb(p.CardTint.A, p.CardTint.R, p.CardTint.G, p.CardTint.B);
            UiBrushes.GlassBorder.Color = Color.FromArgb(p.BorderTint.A, p.BorderTint.R, p.BorderTint.G, p.BorderTint.B);
            UiBrushes.SelBorder.Color = Color.FromRgb(p.SelTint.R, p.SelTint.G, p.SelTint.B);
            UiBrushes.ToggleOn.Color = Color.FromRgb(p.SelTint.R, p.SelTint.G, p.SelTint.B);
            UiBrushes.ShadeBase = Color.FromRgb(p.ShadeBase.R, p.ShadeBase.G, p.ShadeBase.B);
            // Tai nguyen XAML dung chung (style the kinh / pill dang chon /
            // slider / panel Cai dat deu tro toi cac brush nay)
            SetResourceBrushColor("PanelTintBrush", Color.FromArgb(p.PanelTint.A, p.PanelTint.R, p.PanelTint.G, p.PanelTint.B));
            SetResourceBrushColor("CardTintBrush", Color.FromArgb(p.CardTint.A, p.CardTint.R, p.CardTint.G, p.CardTint.B));
            SetResourceBrushColor("CardBorderBrush", Color.FromArgb(p.BorderTint.A, p.BorderTint.R, p.BorderTint.G, p.BorderTint.B));
            SetResourceBrushColor("SelTintBrush", Color.FromRgb(p.SelTint.R, p.SelTint.G, p.SelTint.B));
            SetResourceBrushColor("AccentSolidBrush", Color.FromRgb(p.AccentMid.R, p.AccentMid.G, p.AccentMid.B));
            SetResourceBrushColor("SettingsPanelBrush", Color.FromArgb(p.SettingsPanel.A, p.SettingsPanel.R, p.SettingsPanel.G, p.SettingsPanel.B));
            SetResourceBrushColor("SettingsContentBrush", Color.FromArgb(p.SettingsContent.A, p.SettingsContent.R, p.SettingsContent.G, p.SettingsContent.B));
            // Lop phu toi tren hinh nen: giu nguyen alpha (do mo kinh),
            // chi doi mau goc sang hue cua bang mau
            if (BackdropShade != null && BackdropShade.Background is LinearGradientBrush g && g.GradientStops.Count >= 2)
            {
                g.GradientStops[0].Color = Color.FromArgb(g.GradientStops[0].Color.A, p.ShadeBase.R, p.ShadeBase.G, p.ShadeBase.B);
                g.GradientStops[1].Color = Color.FromArgb(g.GradientStops[1].Color.A, p.ShadeBase.R, p.ShadeBase.G, p.ShadeBase.B);
            }
        }

        private static void SetResourceBrushColor(string key, Color color)
        {
            if (Application.Current != null &&
                Application.Current.Resources.TryGetValue(key, out var res) &&
                res is SolidColorBrush b)
                b.Color = color;
        }

        private void RefreshNavSelectionColors()
        {
            foreach (var (k, btn, icon) in _nav)
            {
                bool sel = k == _currentPage;
                icon.Stroke = new SolidColorBrush(sel
                    ? (UiBrushes.LegacyChrome ? Colors.White : Color.FromRgb(0x12, 0x12, 0x18))
                    : Color.FromRgb(0xB9, 0xB6, 0xCC));
            }
        }

        private void RefreshPaletteTiles()
        {
            string cur = ThemeTiles.ResolveId(_ui);
            foreach (var (tile, card, btn) in _paletteTiles)
            {
                bool sel = tile.Id == cur;
                card.BorderBrush = sel ? UiBrushes.SelBorder : UiBrushes.GlassBorder;
                card.BorderThickness = new Thickness(sel ? 2 : 1);
                btn.Content = sel ? "Đã áp dụng" : "Áp dụng một lần";
                SetCls(btn, "primary", sel);
                SetCls(btn, "ghost", !sel);
            }
        }

        private void SyncWallpaperSelection()
        {
            foreach (var (id, tile, btn) in _wallpaperTiles)
            {
                bool sel = _ui.Background == id;
                tile.BorderBrush = sel ? UiBrushes.SelBorder : UiBrushes.GlassBorder;
                tile.BorderThickness = new Thickness(sel ? 2 : 1);
                btn.Content = sel ? "Đã áp dụng" : "Áp dụng một lần";
                SetCls(btn, "primary", sel);
                SetCls(btn, "ghost", !sel);
            }
            if (_wallpaperUploadTile != null)
            {
                bool sel = _ui.Background == "custom";
                _wallpaperUploadTile.BorderBrush = sel ? UiBrushes.SelBorder : UiBrushes.GlassBorder;
                _wallpaperUploadTile.BorderThickness = new Thickness(sel ? 2 : 1);
                _wallpaperUploadBtn.Content = sel ? "Đã áp dụng" : "Áp dụng một lần";
                SetCls(_wallpaperUploadBtn, "primary", sel);
                SetCls(_wallpaperUploadBtn, "ghost", !sel);
            }
            if (_liveTile != null)
            {
                bool sel = _ui.Background == "live";
                _liveTile.BorderBrush = sel ? UiBrushes.SelBorder : UiBrushes.GlassBorder;
                _liveTile.BorderThickness = new Thickness(sel ? 2 : 1);
                _liveTileBtn.Content = sel ? "Đã áp dụng" : "Áp dụng một lần";
                SetCls(_liveTileBtn, "primary", sel);
                SetCls(_liveTileBtn, "ghost", !sel);
            }
        }

        private static Border MockLine(double w, Color c) => new Border
        {
            Height = 8, Width = w, CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(c),
        };

        private Control BuildThemeSectionReal()
        {
            // --- 1. Mau chu de: o xem truoc lon ---
            var paletteRow = new WrapPanel();
            foreach (var t in ThemeTiles.All)
            {
                var ca = Color.FromRgb(t.A.R, t.A.G, t.A.B);
                var cb = Color.FromRgb(t.B.R, t.B.G, t.B.B);
                bool lightTile = t.Id == ThemeTiles.MayNhat;
                var barMain = lightTile ? Color.FromArgb(0xCC, 0x12, 0x12, 0x1A) : Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF);
                var barSub = lightTile ? Color.FromArgb(0x66, 0x12, 0x12, 0x1A) : Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF);
                var bars = new StackPanel { Spacing = 7, Margin = new Thickness(15, 15, 15, 0), VerticalAlignment = VerticalAlignment.Top };
                bars.Children.Add(MockLine(92, barMain));
                bars.Children.Add(MockLine(148, barSub));
                Border accentLine = t.Legacy
                    ? new Border
                    {
                        Height = 10, Width = 74, CornerRadius = new CornerRadius(5),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Background = new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                            GradientStops = { new GradientStop(ca, 0), new GradientStop(cb, 1) },
                        },
                    }
                    : MockLine(62, cb);
                bars.Children.Add(accentLine);
                Control nameBlock;
                if (t.Legacy)
                {
                    var nsp = new StackPanel { Margin = new Thickness(15), VerticalAlignment = VerticalAlignment.Bottom, Spacing = 1 };
                    nsp.Children.Add(new TextBlock
                    {
                        Text = t.Name, FontSize = 14.5, FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                    });
                    nsp.Children.Add(new TextBlock
                    {
                        Text = t.Sub, FontSize = 10.5,
                        Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                    });
                    nameBlock = nsp;
                }
                else
                {
                    nameBlock = new TextBlock
                    {
                        Text = t.Name, FontSize = 14.5, FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(lightTile ? Color.FromRgb(0x12, 0x12, 0x18) : Color.FromRgb(0xFF, 0xFF, 0xFF)),
                        Margin = new Thickness(15), VerticalAlignment = VerticalAlignment.Bottom,
                    };
                }
                var tileGrid = new Grid();
                tileGrid.Children.Add(bars);
                tileGrid.Children.Add(nameBlock);
                var tile = new Border
                {
                    Width = 216, Height = 128, CornerRadius = new CornerRadius(18), ClipToBounds = true,
                    BorderBrush = UiBrushes.GlassBorder, BorderThickness = new Thickness(1),
                    Background = t.Legacy
                        ? (IBrush)new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                            GradientStops =
                            {
                                new GradientStop(Color.FromRgb(0x23, 0x23, 0x30), 0),
                                new GradientStop(Color.FromRgb(0x12, 0x12, 0x1A), 1),
                            },
                        }
                        : new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                            GradientStops = { new GradientStop(ca, 0), new GradientStop(cb, 1) },
                        },
                    Child = tileGrid,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                string tid = t.Id;
                tile.PointerPressed += (s, e) => ApplyThemeTile(tid);
                var pbtn = new Button { HorizontalAlignment = HorizontalAlignment.Left };
                pbtn.Click += (s, e) => ApplyThemeTile(tid);
                _paletteTiles.Add((t, tile, pbtn));
                var cell = V(8, tile, pbtn);
                cell.Margin = new Thickness(0, 0, 14, 12);
                paletteRow.Children.Add(cell);
            }
            RefreshPaletteTiles();
            var paletteSection = V(10,
                Tb("Màu chủ đề", "h2"),
                Tb("Bấm một ô để áp dụng ngay cho toàn app (panel, thanh bên, mục đang chọn đều nhuộm hue của ô). Ô “Nguyên bản” khôi phục chrome gradient của bản trước.", "sub"),
                paletteRow);

            // --- 2. Che do chu de: the mo phong UI thu nho ---
            Control ModeMockCard(bool dark, string label, string state, bool applied, Action onClick)
            {
                var sidePill = new Border
                {
                    Height = 12, CornerRadius = new CornerRadius(6), Margin = new Thickness(7, 11, 7, 0),
                    Background = new SolidColorBrush(dark ? Color.FromRgb(0xF4, 0xF4, 0xF6) : Color.FromRgb(0x12, 0x12, 0x18)),
                };
                var sideLine1 = new Border { Height = 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(9, 9, 13, 0), Background = new SolidColorBrush(dark ? Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x40, 0x12, 0x12, 0x18)) };
                var sideLine2 = new Border { Height = 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(9, 7, 17, 0), Background = new SolidColorBrush(dark ? Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x40, 0x12, 0x12, 0x18)) };
                var sideStack = new StackPanel();
                sideStack.Children.Add(sidePill);
                sideStack.Children.Add(sideLine1);
                sideStack.Children.Add(sideLine2);
                var side = new Border
                {
                    Width = 48,
                    Background = new SolidColorBrush(dark ? Color.FromRgb(0x1C, 0x1C, 0x2A) : Color.FromRgb(0xFF, 0xFF, 0xFF)),
                    Child = sideStack,
                };
                var lines = new StackPanel { Spacing = 9, Margin = new Thickness(13, 14, 12, 10) };
                lines.Children.Add(MockLine(104, dark ? Color.FromRgb(0x3A, 0x3A, 0x4C) : Color.FromRgb(0xC9, 0xCD, 0xD8)));
                lines.Children.Add(MockLine(138, dark ? Color.FromRgb(0x2C, 0x2C, 0x3C) : Color.FromRgb(0xDD, 0xE0, 0xE8)));
                lines.Children.Add(new Border
                {
                    Height = 8, Width = 62, CornerRadius = new CornerRadius(4),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = UiBrushes.AccentGradient(),
                });
                var mockGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("48,*") };
                mockGrid.Children.Add(side);
                Grid.SetColumn(lines, 1);
                mockGrid.Children.Add(lines);
                var mock = new Border
                {
                    Width = 216, Height = 126, CornerRadius = new CornerRadius(16), ClipToBounds = true,
                    BorderBrush = applied ? UiBrushes.SelBorder : UiBrushes.GlassBorder,
                    BorderThickness = new Thickness(applied ? 2 : 1),
                    Background = new SolidColorBrush(dark ? Color.FromRgb(0x14, 0x14, 0x1E) : Color.FromRgb(0xF2, 0xF3, 0xF7)),
                    Child = mockGrid,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                mock.PointerPressed += (s, e) => onClick();
                var mbtn = new Button { Content = applied ? "Đã áp dụng" : "Áp dụng một lần", HorizontalAlignment = HorizontalAlignment.Left };
                if (applied) mbtn.Classes.Add("primary"); else mbtn.Classes.Add("ghost");
                mbtn.Click += (s, e) => onClick();
                var cell = V(8, mock, H(8, Tb(label, "h3"), Tb("· " + state, "faint")), mbtn);
                cell.Margin = new Thickness(0, 0, 14, 4);
                return cell;
            }
            var modeRow = new WrapPanel();
            modeRow.Children.Add(ModeMockCard(true, "Tối", "đang dùng", true,
                () => ShowToast("Đang dùng chế độ Tối")));
            modeRow.Children.Add(ModeMockCard(false, "Sáng", "đang phát triển", false,
                () => ShowToast("Chế độ Sáng đang được phát triển — sẽ có ở bản sau. Bản này giữ chế độ Tối như bản đã duyệt.")));
            var modeSection = V(10,
                Tb("Chế độ chủ đề", "h2"),
                Tb("Hai thẻ là hình mô phỏng giao diện thu nhỏ. Chế độ Sáng hiện mới ở dạng thẻ xem trước có nhãn “đang phát triển”, chưa áp dụng toàn app.", "sub"),
                modeRow);

            // --- 3. Hinh nen: hang thumbnail + nen dong + the tai len 4MB ---
            var wallpapers = new (string Id, string Name, string File)[]
            {
                ("default", "Nền mặc định", null),
                ("aurora", "Cực quang", "skin-aurora.jpg"),
                ("sunset", "Hoàng hôn", "skin-sunset.jpg"),
                ("forest", "Rừng sâu", "skin-forest.jpg"),
                ("space", "Vũ trụ sâu", "skin-space.jpg"),
                ("inkred", "Kiếm sĩ mực đỏ", "skin-ink-red.jpg"),
                ("inkteal", "Trăng mực xanh", "skin-ink-teal.jpg"),
            };
            var wpRow = new WrapPanel();
            foreach (var (id, name, file) in wallpapers)
            {
                Control visual;
                if (file == null)
                {
                    visual = new Border
                    {
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
                    visual = new Avalonia.Controls.Image { Stretch = Stretch.UniformToFill, Source = LoadSkin(file) };
                }
                var tile = new Border
                {
                    Width = 172, Height = 106, CornerRadius = new CornerRadius(14), ClipToBounds = true,
                    BorderBrush = UiBrushes.GlassBorder, BorderThickness = new Thickness(1),
                    Child = visual, Cursor = new Cursor(StandardCursorType.Hand),
                };
                string sid = id; string sname = name;
                tile.PointerPressed += (s, e) =>
                {
                    ApplyBackground(sid);
                    ShowToast(sid == "default"
                        ? "Đã về nền mặc định (phẳng, trung tính)"
                        : "Đã áp dụng hình nền: " + sname + " — phủ toàn cửa sổ, panel kính nổi phía trên");
                };
                var wbtn = new Button { HorizontalAlignment = HorizontalAlignment.Left };
                wbtn.Click += (s, e) => ApplyBackground(sid);
                _wallpaperTiles.Add((id, tile, wbtn));
                var cell = V(6, tile, Tb(name, null, 12), wbtn);
                cell.Margin = new Thickness(0, 0, 12, 12);
                wpRow.Children.Add(cell);
            }
            // The NEN DONG (live wallpaper tu video ngan)
            {
                var liveTexts = new StackPanel
                {
                    Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var livePlay = Tb("▶", null, 22);
                livePlay.HorizontalAlignment = HorizontalAlignment.Center;
                var liveNote = Tb("Video ngắn ≤ 12 giây", "faint");
                liveNote.HorizontalAlignment = HorizontalAlignment.Center;
                liveTexts.Children.Add(livePlay);
                liveTexts.Children.Add(liveNote);
                _liveTile = new Border
                {
                    Width = 172, Height = 106, CornerRadius = new CornerRadius(14), ClipToBounds = true,
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops =
                        {
                            new GradientStop(Color.FromRgb(0x2A, 0x1B, 0x4A), 0),
                            new GradientStop(Color.FromRgb(0x0E, 0x2A, 0x3A), 1),
                        },
                    },
                    BorderBrush = UiBrushes.GlassBorder, BorderThickness = new Thickness(1),
                    Child = liveTexts, Cursor = new Cursor(StandardCursorType.Hand),
                };
                _liveTile.PointerPressed += async (s, e) =>
                {
                    if (ResolveLiveWallpaperPath() != null) ApplyBackground("live");
                    else await PickLiveWallpaperAsync();
                };
                _liveTileBtn = new Button { HorizontalAlignment = HorizontalAlignment.Left };
                _liveTileBtn.Classes.Add("ghost");
                _liveTileBtn.Click += async (s, e) =>
                {
                    if (ResolveLiveWallpaperPath() != null) ApplyBackground("live");
                    else await PickLiveWallpaperAsync();
                };
                var liveCell = V(6, _liveTile, Tb("Nền động — chọn video", null, 12), _liveTileBtn);
                liveCell.Margin = new Thickness(0, 0, 12, 12);
                wpRow.Children.Add(liveCell);
            }
            // The tai len tuy chinh (nhan 4MB nhu AULA; ban that tu nen JPEG <=1920px va LUU THAT)
            {
                var upTexts = new StackPanel
                {
                    Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var upPlus = Tb("+", null, 24);
                upPlus.HorizontalAlignment = HorizontalAlignment.Center;
                var upNote = Tb("Giới hạn kích thước ảnh 4MB", "faint");
                upNote.HorizontalAlignment = HorizontalAlignment.Center;
                upNote.TextAlignment = Avalonia.Media.TextAlignment.Center;
                upTexts.Children.Add(upPlus);
                upTexts.Children.Add(upNote);
                _wallpaperUploadTile = new Border
                {
                    Width = 172, Height = 106, CornerRadius = new CornerRadius(14), ClipToBounds = true,
                    Background = new SolidColorBrush(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF)),
                    BorderBrush = UiBrushes.GlassBorder, BorderThickness = new Thickness(1),
                    Child = upTexts, Cursor = new Cursor(StandardCursorType.Hand),
                };
                _wallpaperUploadTile.PointerPressed += async (s, e) => await ImportCustomBackgroundAsync();
                _wallpaperUploadBtn = new Button { HorizontalAlignment = HorizontalAlignment.Left };
                _wallpaperUploadBtn.Classes.Add("ghost");
                _wallpaperUploadBtn.Click += async (s, e) => await ImportCustomBackgroundAsync();
                var upCell = V(6, _wallpaperUploadTile, Tb("Tải lên tuỳ chỉnh", null, 12), _wallpaperUploadBtn);
                upCell.Margin = new Thickness(0, 0, 12, 12);
                wpRow.Children.Add(upCell);
            }
            SyncWallpaperSelection();

            _liveStatusText = Tb(string.IsNullOrEmpty(_ui.LiveWallpaperFile)
                ? "Chưa có nền động. Chọn một video NGẮN (app chỉ lấy tối đa 12 giây đầu) hoặc một GIF — video được chuyển thành GIF vòng lặp bằng ffmpeg (tải khi cần, cùng cơ chế TikTok) và lưu trong thư mục Backgrounds."
                : "Nền động đã lưu: " + _ui.LiveWallpaperFile + " — bấm ô “Nền động” ở trên để áp dụng.", "faint");
            _livePauseBtn = GhostBtn(_ui.LiveWallpaperPaused ? "Phát tiếp nền động" : "Tạm dừng nền động", () =>
            {
                _ui.LiveWallpaperPaused = !_ui.LiveWallpaperPaused;
                SaveUi();
                UpdateLivePlayback();
                ShowToast(_ui.LiveWallpaperPaused ? "Đã tạm dừng nền động." : "Đang phát nền động.");
            });
            var liveRow = V(8,
                _liveStatusText,
                H(10,
                    GhostBtn("Chọn video/GIF…", async () => await PickLiveWallpaperAsync()),
                    _livePauseBtn,
                    Tb("Nền động tự tạm dừng khi cửa sổ app mất focus.", "faint")));
            var wallpaperSection = V(10,
                Tb("Hình nền", "h2"),
                Tb("Hình nền phủ toàn cửa sổ, panel kính nổi phía trên. Hai hình “mực” là tranh gốc của app.", "sub"),
                wpRow,
                Tb("Nền động (live wallpaper)", "h3"),
                liveRow);

            // --- 4. Do mo kinh (thang 4-10, luu lai) ---
            var glassValue = Tb(((int)Math.Round(GlassMath.ClampLevel(_ui.GlassBlur))) + " px", "h3");
            var glassSlider = new Slider
            {
                Minimum = GlassMath.MinLevel, Maximum = GlassMath.MaxLevel, Value = GlassMath.ClampLevel(_ui.GlassBlur),
                TickFrequency = 1, IsSnapToTickEnabled = true,
                Width = 320, VerticalAlignment = VerticalAlignment.Center,
            };
            glassSlider.ValueChanged += (s, e) =>
            {
                ApplyGlass(e.NewValue);
                glassValue.Text = ((int)Math.Round(e.NewValue)) + " px";
            };
            var glassSection = V(10,
                Tb("Độ mờ kính", "h2"),
                Tb("Độ mờ làm mềm hình nền sau panel (4–10). Kéo để chỉnh, app nhớ lựa chọn.", "sub"),
                H(12, Tb("4 · trong, nét", "faint"), glassSlider, Tb("10 · mờ, dịu", "faint"), glassValue));

            // --- 5. Man hinh chao ---
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
            var greetingSection = V(10,
                Tb("Màn hình chào", "h2"),
                _greetingStyleText,
                H(10, replayBtn),
                CardFlat(skipGrid, 12));

            var root = V(16,
                paletteSection,
                modeSection,
                wallpaperSection,
                glassSection,
                greetingSection);
            return Scroll(root);
        }

        /// <summary>Ap do mo kinh: BlurEffect len anh nen + alpha lop phu theo thang 4–10.</summary>
        public void ApplyGlass(double level, bool save = true)
        {
            level = GlassMath.ClampLevel(level);
            _ui.GlassBlur = level;
            BackdropImage.Effect = new BlurEffect { Radius = level };
            BackdropAnimImage.Effect = new BlurEffect { Radius = level };
            if (BackdropShade.Background is LinearGradientBrush g && g.GradientStops.Count >= 2)
            {
                var (top, bottom) = GlassMath.ShadeAlphas(level);
                // v3.11.2: mau goc cua lop phu = hue cua o mau dang chon
                // (UiBrushes.ShadeBase), khong con den trung tinh co dinh
                var sb = UiBrushes.ShadeBase;
                g.GradientStops[0].Color = Color.FromArgb(top, sb.R, sb.G, sb.B);
                g.GradientStops[1].Color = Color.FromArgb(bottom, sb.R, sb.G, sb.B);
            }
            if (save) SaveUi();
        }

        // ================= NEN DONG (live wallpaper) =================
        private string ResolveLiveWallpaperPath()
        {
            if (string.IsNullOrEmpty(_ui.LiveWallpaperFile)) return null;
            string p = Path.Combine(BackgroundStore.BackgroundsDir, _ui.LiveWallpaperFile);
            return File.Exists(p) ? p : null;
        }

        private async void StartLiveWallpaper(string path)
        {
            StopLiveWallpaper();
            _liveLoadedPath = path;
            try
            {
                var loaded = await Task.Run(() => LoadLiveFrames(path));
                if (_liveLoadedPath != path) return; // da doi nen khac trong luc tai
                if (loaded.Frames.Count == 0)
                {
                    AppendLog("Nền động: không đọc được khung hình từ " + path);
                    return;
                }
                _liveFrames = loaded.Frames;
                _liveDelays = loaded.Delays;
                _liveIdx = 0;
                BackdropImage.IsVisible = false;
                BackdropAnimImage.Source = _liveFrames[0];
                BackdropAnimImage.IsVisible = true;
                BackdropShade.IsVisible = true;
                _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_liveDelays[0]) };
                _liveTimer.Tick += LiveTick;
                UpdateLivePlayback();
                AppendLog($"Nền động: đang phát {_liveFrames.Count} khung hình từ {Path.GetFileName(path)}.");
            }
            catch (Exception ex) { AppendLog("Nền động lỗi: " + ex.Message); }
        }

        private void LiveTick(object sender, EventArgs e)
        {
            if (_liveFrames == null || _liveFrames.Count == 0) return;
            _liveIdx = (_liveIdx + 1) % _liveFrames.Count;
            BackdropAnimImage.Source = _liveFrames[_liveIdx];
            if (_liveTimer != null && _liveDelays != null && _liveIdx < _liveDelays.Count)
                _liveTimer.Interval = TimeSpan.FromMilliseconds(_liveDelays[_liveIdx]);
        }

        private void UpdateLivePlayback()
        {
            UpdateLivePauseBtn();
            if (_liveTimer == null) return;
            bool play = _liveFrames != null && !_ui.LiveWallpaperPaused && IsActive;
            if (play && !_liveTimer.IsEnabled) _liveTimer.Start();
            if (!play && _liveTimer.IsEnabled) _liveTimer.Stop();
        }

        private void UpdateLivePauseBtn()
        {
            if (_livePauseBtn != null)
                _livePauseBtn.Content = _ui.LiveWallpaperPaused ? "Phát tiếp nền động" : "Tạm dừng nền động";
        }

        private void StopLiveWallpaper()
        {
            _liveLoadedPath = null;
            if (_liveTimer != null)
            {
                _liveTimer.Stop();
                _liveTimer.Tick -= LiveTick;
                _liveTimer = null;
            }
            if (BackdropAnimImage != null)
            {
                BackdropAnimImage.IsVisible = false;
                BackdropAnimImage.Source = null;
            }
            if (_liveFrames != null)
            {
                foreach (var f in _liveFrames) f.Dispose();
                _liveFrames = null;
            }
            _liveDelays = null;
        }

        /// <summary>
        /// Giai ma khung hinh GIF nen dong: rong toi da 768px (phia sau kinh
        /// + lop phu nen khong can sac net hon), toi da 120 khung, giu
        /// nguyen delay tung khung cua GIF (kep 30–1000 ms).
        /// </summary>
        private static (List<Bitmap> Frames, List<int> Delays) LoadLiveFrames(string path)
        {
            var frames = new List<Bitmap>();
            var delays = new List<int>();
            using (var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(path))
            {
                int total = img.Frames.Count;
                int step = Math.Max(1, (int)Math.Ceiling(total / 120.0));
                for (int i = 0; i < total && frames.Count < 120; i += step)
                {
                    int delay = 100;
                    try
                    {
                        var md = img.Frames[i].Metadata.GetGifMetadata();
                        delay = Math.Max(30, Math.Min(1000, md.FrameDelay * 10));
                    }
                    catch { }
                    using (var f = img.Frames.CloneFrame(i))
                    {
                        if (f.Width > 768) f.Mutate(m => m.Resize(768, 0));
                        frames.Add(GifTools.ToBitmap(f));
                    }
                    delays.Add(delay);
                }
            }
            return (frames, delays);
        }

        private void SetLiveStatus(string s) { if (_liveStatusText != null) _liveStatusText.Text = s; }

        private async Task PickLiveWallpaperAsync()
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Chọn video ngắn hoặc GIF làm nền động",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Video ngắn / GIF") { Patterns = new[] { "*.mp4", "*.mov", "*.webm", "*.mkv", "*.avi", "*.gif" } },
                    },
                });
                if (files.Count == 0) return;
                await PrepareLiveWallpaperAsync(files[0].Path.LocalPath);
            }
            catch (Exception ex)
            {
                AppendLog("Lỗi chọn nền động: " + ex.Message);
                ShowToast("Không chọn được file: " + ex.Message);
            }
        }

        private async Task PrepareLiveWallpaperAsync(string src)
        {
            string ext = Path.GetExtension(src);
            long size = 0;
            try { size = new FileInfo(src).Length; } catch { }
            var route = LiveWallpaper.DecideRoute(ext, size);
            if (route == LiveWallpaperRoute.Reject)
            {
                SetLiveStatus("Định dạng chưa hỗ trợ (" + ext + ") — hãy chọn .mp4 hoặc .gif.");
                ShowToast("Định dạng chưa hỗ trợ — hãy chọn .mp4 hoặc .gif.");
                return;
            }
            Directory.CreateDirectory(BackgroundStore.BackgroundsDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            if (route == LiveWallpaperRoute.UseGifDirect)
            {
                string name = "live-" + stamp + ".gif";
                File.Copy(src, Path.Combine(BackgroundStore.BackgroundsDir, name), overwrite: true);
                _ui.LiveWallpaperFile = name;
                ApplyBackground("live");
                SetLiveStatus("Đang dùng nền động từ GIF: " + Path.GetFileName(src));
                ShowToast("Đã đặt nền động từ GIF của bạn.");
                return;
            }
            string ffmpeg = LocateFfmpeg();
            if (ffmpeg == null)
            {
                SetLiveStatus("Cần công cụ ffmpeg để chuyển video → app đang tải về thư mục Tools; tải xong hãy bấm chọn video lại.");
                ShowToast("Chưa có ffmpeg — app đang tải công cụ chuyển đổi; xong thì bấm chọn video lại.");
                if (_liveStatusText != null) await DownloadFfmpegToolAsync(_liveStatusText);
                return;
            }
            string outName = "live-" + stamp + ".gif";
            string outPath = Path.Combine(BackgroundStore.BackgroundsDir, outName);
            SetLiveStatus("Đang chuyển video (tối đa 12 giây đầu) thành GIF nền…");
            string args = LiveWallpaper.BuildConvertArguments(src, outPath);
            AppendLog("ffmpeg nền động: " + args);
            int code = await Task.Run(() =>
            {
                var psi = new System.Diagnostics.ProcessStartInfo(ffmpeg, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return p.ExitCode;
                }
            });
            if (code != 0 || !File.Exists(outPath))
            {
                SetLiveStatus("Chuyển video thất bại (ffmpeg thoát mã " + code + "). Hãy thử video khác.");
                ShowToast("Không chuyển được video này thành nền động.");
                return;
            }
            _ui.LiveWallpaperFile = outName;
            ApplyBackground("live");
            SetLiveStatus("Đang dùng nền động từ video: " + Path.GetFileName(src) + " (đoạn lặp ≤12 giây, 15 fps).");
            ShowToast("Đã đặt nền động — video ngắn của bạn đang chạy vòng lặp sau giao diện.");
        }

        // ================= WIDGET TFT: THE VI TRI THOI TIET =================
        private Control BuildWeatherLocationCard()
        {
            _weatherLocText = Tb("", "sub");
            _weatherStatusText = Tb("Chưa tải thời tiết — mở widget “Đồng hồ & thời tiết” ở trên hoặc bấm “Cập nhật thời tiết”.", "sub");
            UpdateWeatherLocText();
            _weatherCityBox = new TextBox
            {
                Text = _ui.WeatherCity ?? "",
                Width = 240,
                Watermark = "Tên thành phố (vd: Kuwana)",
            };
            _weatherCityBox.Classes.Add("search");
            var (modeSeg, _) = Segmented(
                new[] { "Tự động theo mạng", "Chọn tay" },
                WeatherLocationLogic.NormalizeMode(_ui.WeatherMode) == WeatherLocationLogic.ModeManual ? 1 : 0,
                i =>
                {
                    _ui.WeatherMode = i == 1 ? WeatherLocationLogic.ModeManual : WeatherLocationLogic.ModeAuto;
                    SaveUi();
                    UpdateWeatherLocText();
                    ShowToast(i == 1
                        ? "Thời tiết: chế độ CHỌN TAY — dùng thành phố bạn nhập (ghi đè tự động)."
                        : "Thời tiết: TỰ ĐỘNG THEO MẠNG — vị trí đo qua IP, nhớ 24 giờ. Lưu ý: bật VPN thì vị trí theo đầu ra VPN.");
                    _ = FetchWeatherAsync();
                });
            return Card(V(8,
                Tb("Vị trí thời tiết", "h2"),
                Tb("Tự động theo mạng (mặc định): app đoán vị trí qua địa chỉ IP rồi lấy thời tiết. Bật VPN thì vị trí theo đầu ra VPN — khi đó nên chọn tay.", "sub"),
                modeSeg,
                _weatherLocText,
                _weatherStatusText,
                H(10, _weatherCityBox, GhostBtn("Áp dụng thành phố", () =>
                {
                    _ui.WeatherCity = (_weatherCityBox.Text ?? "").Trim();
                    _ui.WeatherMode = WeatherLocationLogic.ModeManual;
                    SaveUi();
                    UpdateWeatherLocText();
                    _ = FetchWeatherAsync();
                }))));
        }

        // ================= CHO PLUGIN: NHAP THU CONG =================
        private Control BuildImportCard()
        {
            _importUrlBox = new TextBox
            {
                Watermark = "Dán link .zip trực tiếp hoặc link repo GitHub…",
                MinWidth = 360,
            };
            _importUrlBox.Classes.Add("search");
            _importStatus = Tb("Gói plugin chỉ là dữ liệu (manifest + ảnh/GIF); app từ chối mọi gói chứa file chạy/script.", "faint");
            return Card(V(8,
                Tb("Thêm plugin thủ công", "h2"),
                Tb("Dán link .zip hoặc link repo GitHub, hoặc chọn file .zip trên máy.", "sub"),
                H(10, _importUrlBox,
                    PrimaryBtn("Tải & cài từ link", async () => await ImportFromUrlAsync()),
                    GhostBtn("Chọn file .zip…", async () => await ImportFromFileAsync())),
                _importStatus));
        }

        private void SetImportStatus(string s) { if (_importStatus != null) _importStatus.Text = s; }

        private async Task ImportFromUrlAsync()
        {
            string url = (_importUrlBox?.Text ?? "").Trim();
            if (url.Length == 0) { SetImportStatus("Hãy dán link .zip hoặc link repo GitHub vào ô trên trước đã."); return; }
            string tmpZip = Path.Combine(Path.GetTempPath(), "tvks-plugin-dl-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                string zipUrl = url;
                if (GitHubZip.IsDirectZipUrl(url))
                {
                    SetImportStatus("Đang tải gói từ link .zip…");
                }
                else if (GitHubZip.TryParseRepoUrl(url, out var owner, out var repo))
                {
                    SetImportStatus("Đang hỏi GitHub nhánh mặc định của " + owner + "/" + repo + "…");
                    string branch = "main";
                    try
                    {
                        using (var http = NewPluginHttp())
                        {
                            string apiJson = await http.GetStringAsync(GitHubZip.BuildRepoApiUrl(owner, repo));
                            branch = GitHubZip.ParseDefaultBranch(apiJson) ?? "main";
                        }
                    }
                    catch { /* repo cong khai van tai duoc qua codeload voi nhanh main */ }
                    zipUrl = GitHubZip.BuildCodeloadUrl(owner, repo, branch);
                    SetImportStatus("Đang tải gói từ GitHub (nhánh " + branch + ")…");
                }
                else
                {
                    SetImportStatus("Link không hợp lệ: chỉ chấp nhận link .zip trực tiếp (http/https) hoặc link repo dạng github.com/<tên>/<repo>.");
                    return;
                }
                using (var http = NewPluginHttp())
                using (var resp = await http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    resp.EnsureSuccessStatusCode();
                    if (resp.Content.Headers.ContentLength > PluginInstaller.MaxZipBytes)
                    {
                        SetImportStatus("Gói quá lớn (>60 MB) — không tải.");
                        return;
                    }
                    using (var fs = File.Create(tmpZip))
                        await resp.Content.CopyToAsync(fs);
                }
                InstallDownloadedZip(tmpZip);
            }
            catch (Exception ex)
            {
                SetImportStatus("Tải/cài plugin thất bại: " + ex.Message);
            }
            finally
            {
                try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
            }
        }

        private async Task ImportFromFileAsync()
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Chọn gói plugin (.zip)",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Gói plugin") { Patterns = new[] { "*.zip" } },
                    },
                });
                if (files.Count == 0) return;
                string path = files[0].Path.LocalPath;
                if (new FileInfo(path).Length > PluginInstaller.MaxZipBytes)
                {
                    SetImportStatus("Gói quá lớn (>60 MB) — không cài.");
                    return;
                }
                var check = PluginInstaller.InstallFromZip(path, out _);
                FinishImport(check);
            }
            catch (Exception ex)
            {
                SetImportStatus("Không cài được gói plugin: " + ex.Message);
            }
        }

        private void InstallDownloadedZip(string zipPath)
        {
            var check = PluginInstaller.InstallFromZip(zipPath, out _);
            FinishImport(check);
        }

        private void FinishImport(PluginValidation check)
        {
            if (check.Ok)
            {
                SetImportStatus("Đã cài plugin “" + check.Manifest.Name + "” (v" + check.Manifest.Version + ") — xem ở mục “Plugin của tôi” bên dưới. Bật công tắc của nó để dùng làm màn hình chào.");
                ShowToast("Đã cài plugin: " + check.Manifest.Name);
                AppendLog("Plugin thủ công: đã cài “" + check.Manifest.Name + "” (id " + check.Manifest.Id + ").");
                RefreshMyPlugins();
            }
            else
            {
                SetImportStatus(check.Error);
                AppendLog("Plugin thủ công bị từ chối: " + check.Error);
            }
        }

        private static HttpClient NewPluginHttp()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TienVuKeyboardStudio/3.11");
            return http;
        }

        private void RefreshMyPlugins()
        {
            if (_myPluginsHost == null) return;
            _myPluginsHost.Children.Clear();
            var installed = PluginInstaller.ListInstalled();
            if (installed.Count == 0)
            {
                _myPluginsHost.Children.Add(Tb("Chưa có plugin nào được thêm thủ công. Hãy thử gói mẫu đi kèm bản phát hành (file .zip trong thư mục cài app) bằng nút “Chọn file .zip…” ở trên.", "faint"));
            }
            else
            {
                foreach (var (m, dir) in installed)
                    _myPluginsHost.Children.Add(MyPluginCard(m, dir));
            }
            RefreshGreetingStyleText();
        }

        private Border MyPluginCard(PluginManifestDef m, string dir)
        {
            string cid = GreetingStyles.CustomPrefix + m.Id;
            var item = new PluginItem
            {
                Id = cid,
                Name = m.Name,
                Author = string.IsNullOrEmpty(m.Author) ? "Plugin của tôi" : m.Author,
                Cat = "boot",
                CatLabel = "Hiệu ứng khởi động",
                Desc = m.Description ?? "",
                Detail = (string.IsNullOrEmpty(m.Description) ? "Plugin màn hình chào do bạn tự thêm." : m.Description)
                         + " Gói dữ liệu thuần được engine màn chào tích hợp của app trình diễn theo manifest — app không chạy mã nào của gói.",
                Meta = "Phiên bản " + (string.IsNullOrEmpty(m.Version) ? "1.0.0" : m.Version) + " · Plugin của tôi · Đã cài trên máy này",
                Thumb = null,
            };
            var state = GetPluginState(cid);
            var sw = new PillSwitch { IsChecked = state.Enabled };
            sw.Changed += v =>
            {
                state.Enabled = v;
                if (v)
                {
                    _ui.GreetingPlugin = cid;
                    ShowToast("Đã bật “" + m.Name + "” — màn hình chào lần mở app tới sẽ dùng plugin này.");
                }
                else ShowToast("Đã tắt “" + m.Name + "”");
                SaveUi();
                RefreshGreetingStyleText();
            };
            var logoImg = new Avalonia.Controls.Image { Source = LogoBitmap(), Width = 54, Height = 54 };
            var logoWrap = new Border
            {
                CornerRadius = new CornerRadius(11), ClipToBounds = true,
                Width = 54, Height = 54, Child = logoImg,
            };
            var texts = V(3,
                Tb(m.Name, "h3"),
                Tb(item.Author + " · v" + (string.IsNullOrEmpty(m.Version) ? "1.0.0" : m.Version) + " · " + m.Scenes.Count + " cảnh", "faint"),
                Tb(string.IsNullOrEmpty(m.Description) ? "Plugin màn hình chào do bạn tự thêm." : m.Description, "sub"));
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            grid.Children.Add(texts);
            var btns = H(8, sw,
                GhostBtn("Xem trước", () => PreviewPlugin(item)),
                GhostBtn("Chi tiết", () => OpenPluginModal(item)),
                GhostBtn("Xoá", () => DeleteCustomPlugin(item)));
            btns.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(btns, 1);
            grid.Children.Add(btns);
            var inner = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            inner.Children.Add(logoWrap);
            Grid.SetColumn(grid, 1);
            grid.Margin = new Thickness(12, 0, 0, 0);
            inner.Children.Add(grid);
            return CardFlat(inner, 12);
        }

        private void PreviewPlugin(PluginItem it)
        {
            if (GreetingStyles.IsCustomId(it.Id)) _greetingCustomId = it.Id;
            ShowGreeting(GreetingStyles.StyleForPlugin(it.Id));
        }

        private void DeleteCustomPlugin(PluginItem it)
        {
            try
            {
                var (m, dir) = PluginInstaller.FindInstalled(GreetingStyles.ManifestIdOf(it.Id));
                if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true);
                if (_ui.GreetingPlugin == it.Id) _ui.GreetingPlugin = GreetingStyles.PluginGalaxy;
                _ui.Plugins.Remove(it.Id);
                SaveUi();
                RefreshMyPlugins();
                ShowToast("Đã xoá plugin “" + it.Name + "”.");
                AppendLog("Plugin thủ công: đã xoá “" + it.Name + "”.");
            }
            catch (Exception ex)
            {
                ShowToast("Không xoá được plugin: " + ex.Message);
            }
        }

        // ================= MAN CHAO CHO PLUGIN TU NHAP =================
        // Engine canh (scene sequencer) chay tren GreetingBodyHost: moi canh
        // cua manifest hien dung DurationMs roi chuyen canh; GIF phat khung
        // bang timer rieng; canh lines go chu dan (typewriter).
        private void BuildCustomGreeting()
        {
            string pluginId = string.IsNullOrEmpty(_greetingCustomId) ? _ui.GreetingPlugin : _greetingCustomId;
            var (m, dir) = PluginInstaller.FindInstalled(GreetingStyles.ManifestIdOf(pluginId));
            if (m == null || m.Scenes.Count == 0)
            {
                GreetingSub.Text = "Chào mừng bạn trở lại";
                var lbl = Tb("Sẵn sàng", "faint");
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.VerticalAlignment = VerticalAlignment.Center;
                GreetingBodyHost.Children.Add(lbl);
                return;
            }
            GreetingSub.Text = m.Name + " — plugin của tôi";
            var mono = new FontFamily("Consolas, 'Courier New', monospace");

            var sceneControls = new List<Control>();
            Action animTick = null;
            foreach (var s in m.Scenes)
            {
                string type = (s.Type ?? "").ToLowerInvariant();
                Control ctl = null;
                if (type == "image" || type == "gif")
                {
                    string assetPath = Path.Combine(dir, (s.Asset ?? "").Replace('/', Path.DirectorySeparatorChar));
                    var img = new Avalonia.Controls.Image { Stretch = Stretch.Uniform };
                    if (type == "image")
                    {
                        try { img.Source = new Bitmap(assetPath); } catch { }
                    }
                    else
                    {
                        try
                        {
                            var frames = GifTools.LoadFramesFile(assetPath, 560, GifFitMode.Fit, 60);
                            if (frames.Count > 0)
                            {
                                img.Source = frames[0];
                                int fi = 0;
                                var prev = animTick;
                                animTick = () =>
                                {
                                    prev?.Invoke();
                                    if (img.IsVisible && frames.Count > 1)
                                    {
                                        fi = (fi + 1) % frames.Count;
                                        img.Source = frames[fi];
                                    }
                                };
                            }
                        }
                        catch { }
                    }
                    ctl = new Border
                    {
                        CornerRadius = new CornerRadius(12), ClipToBounds = true,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                        BorderThickness = new Thickness(1),
                        Child = img,
                    };
                }
                else if (type == "title")
                {
                    ctl = new TextBlock
                    {
                        Text = s.Text, FontSize = 30, FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF8)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = Avalonia.Media.TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                    };
                }
                else if (type == "lines")
                {
                    var tb = new TextBlock
                    {
                        FontFamily = mono, FontSize = 13.5, LineHeight = 21,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xE6, 0xA0)),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    string full = string.Join("\n", s.Lines);
                    int shown = 0;
                    var prev = animTick;
                    animTick = () =>
                    {
                        prev?.Invoke();
                        if (tb.IsVisible && shown < full.Length)
                        {
                            shown = Math.Min(full.Length, shown + 2);
                            tb.Text = full.Substring(0, shown);
                        }
                    };
                    ctl = tb;
                }
                else // logo
                {
                    var logoImg = new Avalonia.Controls.Image { Source = LogoBitmap(), Width = 84, Height = 84 };
                    var logoWrap = new Border
                    {
                        CornerRadius = new CornerRadius(16), ClipToBounds = true,
                        Width = 84, Height = 84, HorizontalAlignment = HorizontalAlignment.Center,
                        Child = logoImg,
                    };
                    var name = Tb("Tien Vu Keyboard Studio", null, 17);
                    name.FontWeight = FontWeight.Bold;
                    name.HorizontalAlignment = HorizontalAlignment.Center;
                    var sp = new StackPanel
                    {
                        Spacing = 8, VerticalAlignment = VerticalAlignment.Center,
                    };
                    sp.Children.Add(logoWrap);
                    sp.Children.Add(name);
                    ctl = sp;
                }
                if (ctl != null)
                {
                    ctl.IsVisible = false;
                    sceneControls.Add(ctl);
                }
            }
            var grid = new Grid();
            foreach (var c in sceneControls) grid.Children.Add(c);
            GreetingBodyHost.Children.Add(grid);
            if (sceneControls.Count == 0) return;

            _customAnimTick = animTick;
            if (animTick != null)
            {
                _customAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                _customAnimTimer.Tick += (s2, e2) => _customAnimTick?.Invoke();
                _customAnimTimer.Start();
            }
            int idx = 0;
            void ShowScene()
            {
                for (int i = 0; i < sceneControls.Count; i++) sceneControls[i].IsVisible = i == idx;
                var sc = m.Scenes[Math.Min(idx, m.Scenes.Count - 1)];
                int dur = Math.Max(300, Math.Min(15000, sc.DurationMs <= 0 ? 1500 : sc.DurationMs));
                _customSceneTimer?.Stop();
                if (idx < sceneControls.Count - 1)
                {
                    _customSceneTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(dur) };
                    _customSceneTimer.Tick += (s2, e2) =>
                    {
                        _customSceneTimer.Stop();
                        idx++;
                        ShowScene();
                    };
                    _customSceneTimer.Start();
                }
            }
            ShowScene();
        }
    }
}
