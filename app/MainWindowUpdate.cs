using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.14 — Muc "Cap nhat" trong panel Cai dat (mini-sidebar) + kiem tra
    // am tham khi mo app:
    //  - Ban app: doc docs/update.json tren repo; co ban moi thi tai zip
    //    ban Light ve %AppData%\TienKeyboardStudio\Updates\, kiem SHA-256
    //    theo manifest roi cho mo thu muc.
    // v3.17 (scope C) — CAP NHAT MOT NUT BAM (yeu cau cua Tien): tai +
    // kiem bam xong thi co nut "Cap nhat ngay": app ghi update-job.json,
    // chep TienVuUpdater.exe vao Updates va mo tu do, cho helper bao
    // "con song" (file alive) roi thoat han; helper de zip len thu muc
    // cai (UpdaterCore.PerformSwap, co backup/rollback), mo lai app va
    // ghi update-result.json de lan mo sau hien mot dong ghi chu. That
    // bai o bat ky buoc nao: ban cu nguyen ven. "Mo thu muc" van la lua
    // chon phu (cai tay nhu truoc).
    //  - Du lieu thiet bi: dung lai co che bang thiet bi cong dong san co
    //    (DeviceRegistry: RawUrl + TryParseValid + LocalPath).
    //  - Thu vien GIF online: tai lai docs/online-library-drive.json,
    //    kiem hop le, ghi cache (OnlineCatalog uu tien cache) va dung lai
    //    trang Thu vien Online de GIF moi xuat hien ngay.
    // Moi loi mang chi thanh MOT dong trang thai — khong hop thoai loi.
    // Logic thuan (parse/so sanh/tai+kiem bam) nam o UpdateCore.cs.
    // =====================================================================
    public partial class MainWindow
    {
        private TextBlock _updateNavLabel;
        private TextBlock _updAppStatus;
        private TextBlock _updRegStatus;
        private TextBlock _updGifStatus;
        private ProgressBar _updProgress;
        private Button _updDownloadBtn;
        private Button _updOpenDirBtn;
        private Button _updInstallBtn;
        private string _updDownloadedPath;
        // v3.15: kho scheme den cong dong
        private TextBlock _schemeLibStatus;
        private StackPanel _schemeLibPanel;

        // ---------------- Muc Cap nhat ----------------
        private Control BuildUpdateSection()
        {
            _updAppStatus = Tb("", "sub");
            _updProgress = new ProgressBar { Minimum = 0, Maximum = 1, Value = 0, IsVisible = false, Height = 5 };
            _updDownloadBtn = PrimaryBtn("Tải bản mới", async () => await DownloadAppUpdateAsync());
            _updDownloadBtn.IsVisible = false;
            _updOpenDirBtn = GhostBtn("Mở thư mục", () =>
            {
                try
                {
                    Directory.CreateDirectory(UpdateCore.UpdatesDir);
                    Process.Start(new ProcessStartInfo { FileName = UpdateCore.UpdatesDir, UseShellExecute = true });
                }
                catch (Exception ex) { ShowToast("Không mở được thư mục: " + ex.Message); }
            });
            _updOpenDirBtn.IsVisible = false;
            // v3.17 (scope C): nut chinh cua luong mot-nut-bam — chi hien
            // sau khi tai + kiem bam xong.
            _updInstallBtn = PrimaryBtn("Cập nhật ngay", async () => await InstallDownloadedUpdateAsync());
            _updInstallBtn.IsVisible = false;
            var appCard = Card(V(8,
                Tb("Phiên bản app", "h2"),
                Tb("Phiên bản hiện tại: " + AboutInfo.Version, "sub"),
                _updAppStatus,
                _updProgress,
                H(10,
                    GhostBtn("Kiểm tra cập nhật", async () => await CheckAppUpdateAsync(manual: true)),
                    _updDownloadBtn,
                    _updInstallBtn,
                    _updOpenDirBtn)));

            _regStatus = Tb("", "sub");
            _updRegStatus = Tb("", "sub");
            var regCard = Card(V(8,
                Tb("Dữ liệu thiết bị", "h2"),
                Tb($"Tổng số mẫu trong app: {ModelDatabase.All.Count}. Bảng cộng đồng tải thêm mẫu mới mà không cần bản app mới.", "sub"),
                _regStatus,
                _updRegStatus,
                GhostBtn("Kiểm tra / cập nhật", async () => await RefreshRegistryFromUpdateAsync())));

            _updGifStatus = Tb("", "sub");
            var gifCard = Card(V(8,
                Tb("Thư viện GIF online", "h2"),
                _updGifStatus,
                GhostBtn("Kiểm tra / cập nhật", async () => await RefreshGifCatalogAsync())));

            // --- v3.15: kho scheme den cong dong (catalog tren repo;
            // tai ve -> kiem bam -> ghi thang vao thu muc
            // LightingSchemes nen tu xuat hien trong "Bo den cua toi") ---
            _schemeLibStatus = Tb("Bấm “Tải danh sách” để xem các scheme đèn cộng đồng.", "sub");
            _schemeLibPanel = new StackPanel { Spacing = 6 };
            var schemeCard = Card(V(8,
                Tb("Kho scheme đèn cộng đồng", "h2"),
                Tb("Scheme tải về nằm ngay trong “Bộ đèn của tôi” ở trang Per-key. Chia sẻ scheme của bạn bằng mã TVKS1 ở trang Per-key.", "sub"),
                _schemeLibStatus,
                _schemeLibPanel,
                GhostBtn("Tải danh sách", async () => await FetchSchemeCatalogAsync())));

            RefreshUpdateLabels();
            return Scroll(V(14, appCard, regCard, gifCard, schemeCard));
        }

        // ---------------- v3.15: Kho scheme cong dong ----------------
        private async Task FetchSchemeCatalogAsync()
        {
            if (_schemeLibStatus != null) _schemeLibStatus.Text = "Đang tải danh sách…";
            try
            {
                string json;
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("TienVuKeyboardStudio/" + AboutInfo.Version);
                    json = await http.GetStringAsync(SchemeCatalogCore.CatalogUrl);
                }
                var items = SchemeCatalogCore.ParseCatalog(json);
                if (items.Count == 0)
                {
                    if (_schemeLibStatus != null) _schemeLibStatus.Text = "Không kiểm tra được — thử lại sau.";
                    return;
                }
                if (_schemeLibStatus != null)
                    _schemeLibStatus.Text = $"Có {items.Count} scheme trong kho cộng đồng:";
                if (_schemeLibPanel != null)
                {
                    _schemeLibPanel.Children.Clear();
                    var store = new LightingSchemeStore(SchemesDir);
                    foreach (var item in items)
                    {
                        var captured = item;
                        bool owned = store.Load(item.Name) != null;
                        var rowStatus = Tb(owned ? "Đã có trong Bộ đèn của tôi" : "", "faint");
                        var btn = GhostBtn(owned ? "Tải lại" : "Tải về", async () =>
                        {
                            await DownloadSchemeAsync(captured, rowStatus);
                        });
                        _schemeLibPanel.Children.Add(V(2,
                            H(10, Tb(item.Name, "h3"), Tb(string.IsNullOrWhiteSpace(item.Author) ? "" : "· " + item.Author, "faint")),
                            H(10, btn, rowStatus)));
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog("[Cập nhật] Lỗi tải kho scheme: " + ex.Message);
                if (_schemeLibStatus != null) _schemeLibStatus.Text = "Không kiểm tra được — thử lại sau.";
            }
        }

        private async Task DownloadSchemeAsync(SchemeCatalogItem item, TextBlock rowStatus)
        {
            string url = SchemeCatalogCore.DownloadUrlOf(item);
            if (string.IsNullOrWhiteSpace(url))
            {
                if (rowStatus != null) rowStatus.Text = "Mục này thiếu link tải.";
                return;
            }
            string dest = Path.Combine(SchemesDir,
                LightingSchemeStore.SanitizeFileName(item.Name) + ".json");
            if (rowStatus != null) rowStatus.Text = "Đang tải…";
            var (ok, error) = await UpdateCore.DownloadAndVerifyAsync(url, dest, item.Sha256);
            if (!ok)
            {
                AppendLog("[Cập nhật] Tải scheme thất bại: " + error);
                if (rowStatus != null) rowStatus.Text = "Tải thất bại — thử lại sau.";
                return;
            }
            // Kiem lai noi dung: phai la scheme hop le cua app.
            try
            {
                string text = File.ReadAllText(dest);
                if (!SchemeShare.ValidateDownloaded(text, out var sc, out string verr))
                {
                    try { File.Delete(dest); } catch { }
                    if (rowStatus != null) rowStatus.Text = "File scheme không hợp lệ: " + verr;
                    return;
                }
                // Ghi lai bang chinh store (ten chuan cua scheme).
                new LightingSchemeStore(SchemesDir).Save(sc);
                if (!string.Equals(sc.Name, item.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // File tam theo ten catalog khac ten scheme -> xoa ban trung.
                    try { if (File.Exists(dest)) File.Delete(dest); } catch { }
                }
            }
            catch (Exception ex)
            {
                AppendLog("[Cập nhật] Kiểm scheme tải về lỗi: " + ex.Message);
                if (rowStatus != null) rowStatus.Text = "Tải xong nhưng kiểm tra lỗi — thử lại sau.";
                return;
            }
            _refreshSchemeList?.Invoke();
            if (rowStatus != null) rowStatus.Text = "Đã có trong Bộ đèn của tôi (trang Per-key).";
            ShowToast($"Đã tải scheme “{item.Name}”.");
            AppendLog($"[Cập nhật] Đã tải scheme cộng đồng: {item.Name}");
        }

        private void RefreshUpdateLabels()
        {
            if (_updAppStatus != null)
            {
                if (UpdateCore.ShouldShowBadge(_ui.UpdateLatestVersion, AboutInfo.Version))
                {
                    _updAppStatus.Text = $"Có bản mới: {_ui.UpdateLatestVersion}" +
                        (string.IsNullOrWhiteSpace(_ui.UpdateLatestNotes) ? "" : " — " + _ui.UpdateLatestNotes) +
                        " Bấm “Tải bản mới”, tải xong bấm “Cập nhật ngay” — app tự đóng, thay bản mới rồi mở lại; cài đặt và dữ liệu giữ nguyên.";
                    if (_updDownloadBtn != null) _updDownloadBtn.IsVisible = true;
                }
                else
                    _updAppStatus.Text = "Bạn đang dùng bản mới nhất. " + UpdateCore.FormatLastCheck(_ui.UpdateLastCheckUtc);
            }
            if (_updRegStatus != null)
                _updRegStatus.Text = UpdateCore.FormatLastCheck(_ui.RegistryLastCheckUtc);
            if (_regStatus != null)
                _regStatus.Text = RegistryStatusText();
            if (_updGifStatus != null)
            {
                int count = _ui.GifCatalogCount >= 0 ? _ui.GifCatalogCount : OnlineCatalog.LoadCatalog().Count;
                _updGifStatus.Text = $"Hiện có {count} hiệu ứng trong thư viện. " + UpdateCore.FormatLastCheck(_ui.GifCatalogLastCheckUtc);
            }
            RefreshUpdateBadge();
        }

        private void RefreshUpdateBadge()
        {
            if (_updateNavLabel == null) return;
            _updateNavLabel.Text = UpdateCore.ShouldShowBadge(_ui.UpdateLatestVersion, AboutInfo.Version)
                ? "Cập nhật • Có bản mới"
                : "Cập nhật";
        }

        // ---------------- Kiem tra ban app ----------------
        private async Task<AppUpdateManifest> FetchManifestAsync(int timeoutSeconds)
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TienVuKeyboardStudio/" + AboutInfo.Version);
                string json = await http.GetStringAsync(UpdateCore.ManifestUrl);
                return UpdateCore.ParseManifest(json);
            }
        }

        private void ApplyManifest(AppUpdateManifest m)
        {
            _ui.UpdateLastCheckUtc = UpdateCore.NowIso();
            if (m != null && UpdateCore.IsNewer(m.Version, AboutInfo.Version))
            {
                _ui.UpdateLatestVersion = m.Version;
                _ui.UpdateLatestNotes = m.Notes;
                _ui.UpdateLightUrl = m.LightUrl;
                _ui.UpdateSha256 = m.Sha256;
            }
            else if (m != null)
            {
                // Ban dang chay >= ban tren repo: xoa thong tin ban moi cu.
                _ui.UpdateLatestVersion = "";
                _ui.UpdateLatestNotes = "";
                _ui.UpdateLightUrl = "";
                _ui.UpdateSha256 = "";
            }
            SaveUi();
        }

        private async Task CheckAppUpdateAsync(bool manual)
        {
            if (_updAppStatus != null) _updAppStatus.Text = "Đang kiểm tra…";
            try
            {
                var m = await FetchManifestAsync(20);
                if (m == null)
                {
                    if (_updAppStatus != null) _updAppStatus.Text = "Không kiểm tra được — thử lại sau.";
                    return;
                }
                ApplyManifest(m);
                RefreshUpdateLabels();
                if (manual)
                    ShowToast(UpdateCore.IsNewer(m.Version, AboutInfo.Version)
                        ? $"Có bản mới: {m.Version}."
                        : "Bạn đang dùng bản mới nhất.");
            }
            catch (Exception ex)
            {
                AppendLog("[Cập nhật] Lỗi kiểm tra bản app: " + ex.Message);
                if (_updAppStatus != null) _updAppStatus.Text = "Không kiểm tra được — thử lại sau.";
            }
        }

        /// <summary>
        /// Kiem tra AM THAM mot lan khi mo app (chi phien ban app): co ban
        /// moi thi chi them dau "Co ban moi" o muc Cap nhat — khong popup,
        /// khong tu tai. Moi loi deu im lang (chi ghi log).
        /// </summary>
        private async Task SilentAppUpdateCheckAsync()
        {
            try
            {
                await Task.Delay(4000); // cho app mo xong han
                var m = await FetchManifestAsync(10);
                if (m == null) return;
                bool hadBadge = UpdateCore.ShouldShowBadge(_ui.UpdateLatestVersion, AboutInfo.Version);
                ApplyManifest(m);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshUpdateLabels();
                    if (!hadBadge && UpdateCore.ShouldShowBadge(_ui.UpdateLatestVersion, AboutInfo.Version))
                        AppendLog($"[Cập nhật] Có bản mới: {_ui.UpdateLatestVersion} — xem mục Cài đặt → Cập nhật.");
                });
            }
            catch (Exception ex) { AppendLog("[Cập nhật] Kiểm tra âm thầm lỗi (bỏ qua): " + ex.Message); }
        }

        private async Task DownloadAppUpdateAsync()
        {
            string version = _ui.UpdateLatestVersion;
            string url = _ui.UpdateLightUrl;
            string sha = _ui.UpdateSha256;
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(url))
            {
                ShowToast("Chưa có thông tin bản mới — hãy bấm Kiểm tra cập nhật trước.");
                return;
            }
            string dest = Path.Combine(UpdateCore.UpdatesDir, $"Tien-Vu-Keyboard-Studio-v{version}-Light.zip");
            if (_updProgress != null) { _updProgress.IsVisible = true; _updProgress.Value = 0; _updProgress.IsIndeterminate = true; }
            if (_updAppStatus != null) _updAppStatus.Text = $"Đang tải bản {version}…";
            var progress = new Progress<double>(p =>
            {
                if (_updProgress == null) return;
                if (p >= 0) { _updProgress.IsIndeterminate = false; _updProgress.Value = p; }
            });
            var (ok, error) = await UpdateCore.DownloadAndVerifyAsync(url, dest, sha, progress);
            if (_updProgress != null) { _updProgress.IsVisible = false; _updProgress.IsIndeterminate = false; }
            if (ok)
            {
                _updDownloadedPath = dest;
                if (_updAppStatus != null)
                    _updAppStatus.Text = $"Đã tải xong bản {version} và kiểm tra mã băm khớp. Bấm “Cập nhật ngay” để app tự đóng, thay bản mới và mở lại — hoặc “Mở thư mục” nếu muốn cài tay.";
                if (_updInstallBtn != null) _updInstallBtn.IsVisible = true;
                if (_updOpenDirBtn != null) _updOpenDirBtn.IsVisible = true;
                ShowToast($"Đã tải xong bản {version} — bấm Cập nhật ngay để hoàn tất.");
                AppendLog($"[Cập nhật] Đã tải + kiểm băm OK: {dest}");
            }
            else
            {
                AppendLog("[Cập nhật] Tải bản mới thất bại: " + error);
                if (_updAppStatus != null) _updAppStatus.Text = "Tải thất bại — thử lại sau. (" + error + ")";
            }
        }

        // ---------------- v3.17 (scope C): cap nhat mot nut bam ----------------

        /// <summary>
        /// "Cap nhat ngay": ghi job, chep helper vao Updates, mo helper
        /// tu ban chep, cho no bao "con song" (file alive, toi da ~3s)
        /// roi thoat han de helper thay ban. Helper khong bao song ->
        /// huy, app khong thoat, ban cu nguyen ven.
        /// </summary>
        private async Task InstallDownloadedUpdateAsync()
        {
            string zip = _updDownloadedPath;
            string version = _ui.UpdateLatestVersion;
            if (string.IsNullOrWhiteSpace(zip) || !File.Exists(zip))
            {
                ShowToast("Chưa có file bản mới đã tải — hãy bấm Tải bản mới trước.");
                return;
            }
            string baseDir = AppContext.BaseDirectory;
            string helperSrc = Path.Combine(baseDir, UpdaterCore.HelperExeName);
            if (!File.Exists(helperSrc))
            {
                if (_updAppStatus != null)
                    _updAppStatus.Text = "Bản đang chạy thiếu " + UpdaterCore.HelperExeName +
                        " nên chưa tự cập nhật được — dùng “Mở thư mục” để cài tay nhé.";
                if (_updOpenDirBtn != null) _updOpenDirBtn.IsVisible = true;
                return;
            }
            try
            {
                Directory.CreateDirectory(UpdateCore.UpdatesDir);
                string helperCopy = UpdaterCore.HelperCopyPathIn(UpdateCore.UpdatesDir);
                File.Copy(helperSrc, helperCopy, overwrite: true);
                string alive = UpdaterCore.AlivePathIn(UpdateCore.UpdatesDir);
                try { if (File.Exists(alive)) File.Delete(alive); } catch { }
                // Xoa result cu (neu con) de lan mo sau khong hien nham.
                try { string rp = UpdaterCore.ResultPathIn(UpdateCore.UpdatesDir); if (File.Exists(rp)) File.Delete(rp); } catch { }
                string relaunch = "TienKeyboardStudio.exe";
                try { string pp = Environment.ProcessPath; if (!string.IsNullOrEmpty(pp)) relaunch = Path.GetFileName(pp); } catch { }
                var job = new UpdateJob
                {
                    ZipPath = zip,
                    TargetDir = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    RelaunchExe = relaunch,
                    AppPid = Environment.ProcessId,
                    ToVersion = version ?? "",
                };
                string jobPath = UpdaterCore.JobPathIn(UpdateCore.UpdatesDir);
                File.WriteAllText(jobPath, UpdaterCore.SerializeJob(job));
                SaveUi();
                Process.Start(new ProcessStartInfo
                {
                    FileName = helperCopy,
                    Arguments = "\"" + jobPath + "\"",
                    UseShellExecute = false,
                });
                if (_updAppStatus != null) _updAppStatus.Text = "Đang khởi động trình cập nhật…";
                bool aliveSeen = false;
                for (int i = 0; i < 30 && !aliveSeen; i++)
                {
                    await Task.Delay(100);
                    aliveSeen = File.Exists(alive);
                }
                if (!aliveSeen)
                {
                    AppendLog("[Cập nhật] Helper không báo alive trong 3 giây — huỷ cập nhật tự động, app không thoát.");
                    if (_updAppStatus != null)
                        _updAppStatus.Text = "Không khởi động được trình cập nhật phụ — bản hiện tại vẫn nguyên vẹn. Hãy dùng “Mở thư mục” để cài tay.";
                    if (_updOpenDirBtn != null) _updOpenDirBtn.IsVisible = true;
                    return;
                }
                AppendLog($"[Cập nhật] Helper đã sẵn sàng — app thoát để thay bản {version}.");
                _reallyExit = true;
                Close();
                // Phong xa: cua so khong dong han thi ep thoat sau 1.5s
                // (helper chi tien hanh khi PID nay da chet).
                _ = Task.Run(async () => { await Task.Delay(1500); Environment.Exit(0); });
            }
            catch (Exception ex)
            {
                AppendLog("[Cập nhật] Lỗi khởi động cập nhật tự động: " + ex.Message);
                if (_updAppStatus != null)
                    _updAppStatus.Text = "Không bắt đầu được cập nhật tự động — bản hiện tại vẫn nguyên vẹn. (" + ex.Message + ")";
            }
        }

        /// <summary>
        /// Doc update-result.json do helper ghi (neu co) o lan mo app ke
        /// tiep: hien MOT dong ghi chu roi xoa file. Dong thoi don ban
        /// chep helper con sot trong Updates (helper da thoat nen xoa
        /// duoc; rieng lan cap nhat THAT BAI thi app cu mo lai cung chay
        /// qua day va don sach).
        /// </summary>
        private async Task ShowPendingUpdateResultAsync()
        {
            try
            {
                await Task.Delay(1200);
                try { string hc = UpdaterCore.HelperCopyPathIn(UpdateCore.UpdatesDir); if (File.Exists(hc)) File.Delete(hc); } catch { }
                var r = UpdaterCore.ReadResult(UpdateCore.UpdatesDir);
                if (r == null) return;
                try { File.Delete(UpdaterCore.ResultPathIn(UpdateCore.UpdatesDir)); } catch { }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AppendLog("[Cập nhật] " + r.Message);
                    if (_updAppStatus != null) _updAppStatus.Text = r.Message;
                    ShowToast(r.Ok ? r.Message
                        : "Cập nhật tự động chưa thành — vẫn dùng bản cũ, xem Cài đặt → Cập nhật.");
                });
            }
            catch { }
        }

        // ---------------- Lam moi du lieu ----------------
        private async Task RefreshRegistryFromUpdateAsync()
        {
            DateTime before = File.Exists(DeviceRegistry.LocalPath)
                ? File.GetLastWriteTimeUtc(DeviceRegistry.LocalPath) : DateTime.MinValue;
            await CheckRegistryUpdateAsync();
            bool saved = File.Exists(DeviceRegistry.LocalPath)
                && File.GetLastWriteTimeUtc(DeviceRegistry.LocalPath) > before;
            if (saved)
            {
                _ui.RegistryLastCheckUtc = UpdateCore.NowIso();
                SaveUi();
            }
            RefreshUpdateLabels();
        }

        private async Task RefreshGifCatalogAsync()
        {
            if (_updGifStatus != null) _updGifStatus.Text = "Đang kiểm tra…";
            try
            {
                string json;
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("TienVuKeyboardStudio/" + AboutInfo.Version);
                    json = await http.GetStringAsync(UpdateCore.GifCatalogUrl);
                }
                var (ok, count, error) = UpdateCore.ValidateCatalogJson(json);
                if (!ok)
                {
                    AppendLog("[Cập nhật] Catalog GIF tải về không hợp lệ: " + error);
                    if (_updGifStatus != null) _updGifStatus.Text = "Không kiểm tra được — thử lại sau.";
                    return;
                }
                OnlineCatalog.SaveCatalogCache(json);
                _ui.GifCatalogLastCheckUtc = UpdateCore.NowIso();
                _ui.GifCatalogCount = count;
                SaveUi();
                RefreshOnlinePage();
                RefreshUpdateLabels();
                ShowToast($"Đã cập nhật thư viện GIF: {count} hiệu ứng.");
                AppendLog($"[Cập nhật] Đã làm mới catalog GIF online: {count} mục.");
            }
            catch (Exception ex)
            {
                AppendLog("[Cập nhật] Lỗi làm mới catalog GIF: " + ex.Message);
                if (_updGifStatus != null) _updGifStatus.Text = "Không kiểm tra được — thử lại sau.";
            }
        }

        /// <summary>Dung lai trang Thu vien Online de catalog cache moi xuat hien ngay.</summary>
        private void RefreshOnlinePage()
        {
            if (!_pages.TryGetValue("online", out var old)) return;
            var np = BuildOnlinePage();
            np.IsVisible = _currentPage == "online";
            int idx = PageHost.Children.IndexOf(old);
            PageHost.Children.Remove(old);
            if (idx >= 0 && idx <= PageHost.Children.Count) PageHost.Children.Insert(idx, np);
            else PageHost.Children.Add(np);
            _pages["online"] = np;
        }
    }
}
