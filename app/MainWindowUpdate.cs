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
    //    theo manifest roi cho mo thu muc — KHONG bao gio tu thay exe
    //    dang chay.
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
        private string _updDownloadedPath;

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
            var appCard = Card(V(8,
                Tb("Phiên bản app", "h2"),
                Tb("Phiên bản hiện tại: " + AboutInfo.Version, "sub"),
                _updAppStatus,
                _updProgress,
                H(10,
                    GhostBtn("Kiểm tra cập nhật", async () => await CheckAppUpdateAsync(manual: true)),
                    _updDownloadBtn,
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

            RefreshUpdateLabels();
            return Scroll(V(14, appCard, regCard, gifCard));
        }

        private void RefreshUpdateLabels()
        {
            if (_updAppStatus != null)
            {
                if (UpdateCore.ShouldShowBadge(_ui.UpdateLatestVersion, AboutInfo.Version))
                {
                    _updAppStatus.Text = $"Có bản mới: {_ui.UpdateLatestVersion}" +
                        (string.IsNullOrWhiteSpace(_ui.UpdateLatestNotes) ? "" : " — " + _ui.UpdateLatestNotes) +
                        " Bấm “Tải bản mới” để tải về thư mục Updates (app không tự thay bản đang chạy).";
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
                    _updAppStatus.Text = $"Đã tải xong bản {version} và kiểm tra mã băm khớp. Giải nén file trong thư mục Updates để dùng bản mới (app không tự thay bản đang chạy).";
                if (_updOpenDirBtn != null) _updOpenDirBtn.IsVisible = true;
                ShowToast($"Đã tải xong bản {version}.");
                AppendLog($"[Cập nhật] Đã tải + kiểm băm OK: {dest}");
            }
            else
            {
                AppendLog("[Cập nhật] Tải bản mới thất bại: " + error);
                if (_updAppStatus != null) _updAppStatus.Text = "Tải thất bại — thử lại sau. (" + error + ")";
            }
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
