using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.11 — Nhap plugin THU CONG: goi plugin la DU LIEU THUAN (manifest
    // .json + anh/GIF), app doc bang engine man chao tich hop — KHONG bao
    // gio chay ma ngoai. Kiem tra nghiem ngat: archive chua file thuc thi
    // (.exe/.dll/.js/.py/.bat/.cmd/.sh/.ps1...) hoac thieu/sai manifest
    // thi bi tu choi voi loi tieng Viet ro rang.
    //
    // Dinh dang manifest (tai lieu day du trong README.md cua ban phat
    // hanh):
    // {
    //   "id": "ten-plugin", "name": "...", "description": "...",
    //   "author": "...", "version": "1.0.0", "kind": "greeting",
    //   "scenes": [
    //     { "type": "image", "asset": "assets/bg.png", "durationMs": 1800 },
    //     { "type": "gif",   "asset": "assets/anim.gif", "durationMs": 2400 },
    //     { "type": "title", "text": "TIEN VU", "durationMs": 1500 },
    //     { "type": "lines", "lines": ["dong 1", "dong 2"], "durationMs": 2200 },
    //     { "type": "logo",  "durationMs": 1500 }
    //   ]
    // }
    // =====================================================================

    /// <summary>Mot canh trong man chao cua plugin tu nhap.</summary>
    public sealed class PluginSceneDef
    {
        public string Type { get; set; } = "";        // image | gif | title | lines | logo
        public string Asset { get; set; } = "";       // duong dan tuong doi trong goi (image/gif)
        public string Text { get; set; } = "";        // title
        public List<string> Lines { get; set; } = new List<string>(); // lines
        public int DurationMs { get; set; } = 1500;
    }

    /// <summary>Manifest cua mot goi plugin du lieu.</summary>
    public sealed class PluginManifestDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "";
        public string Version { get; set; } = "";
        public string Kind { get; set; } = "";
        public List<PluginSceneDef> Scenes { get; set; } = new List<PluginSceneDef>();

        /// <summary>Tong thoi luong man chao (ms) — moi canh kep ve 300..15000.</summary>
        public int TotalDurationMs
        {
            get
            {
                int sum = 0;
                if (Scenes != null)
                    foreach (var s in Scenes)
                        sum += Math.Max(300, Math.Min(15000, s.DurationMs <= 0 ? 1500 : s.DurationMs));
                return Math.Max(1200, sum);
            }
        }
    }

    /// <summary>Ket qua kiem tra mot goi plugin.</summary>
    public sealed class PluginValidation
    {
        public bool Ok;
        public string Error;                 // loi tieng Viet than thien (khi !Ok)
        public PluginManifestDef Manifest;   // khi Ok
    }

    /// <summary>Quy tac kiem tra goi plugin — thuan logic, unit test duoc.</summary>
    public static class PluginPackageRules
    {
        public const string KindGreeting = "greeting";

        /// <summary>Duoi file bi cam tuyet doi trong goi plugin (ma thuc thi/script).</summary>
        public static readonly string[] ForbiddenExtensions =
            { ".exe", ".dll", ".js", ".py", ".bat", ".cmd", ".sh", ".ps1", ".com", ".msi", ".scr", ".vbs", ".jse", ".jar" };

        /// <summary>Duoi file tai nguyen (anh/GIF) duoc phep lam asset cua canh.</summary>
        public static readonly string[] AssetExtensions =
            { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" };

        private static readonly Regex IdPattern =
            new Regex("^[a-z0-9][a-z0-9-]{1,40}$", RegexOptions.Compiled);

        public static bool IsValidId(string id) => id != null && IdPattern.IsMatch(id);

        public static bool IsForbidden(string entryName)
        {
            string ext = Path.GetExtension(entryName ?? "").ToLowerInvariant();
            foreach (var f in ForbiddenExtensions)
                if (ext == f) return true;
            return false;
        }

        public static bool IsAssetFile(string entryName)
        {
            string ext = Path.GetExtension(entryName ?? "").ToLowerInvariant();
            foreach (var a in AssetExtensions)
                if (ext == a) return true;
            return false;
        }

        /// <summary>Parse manifest JSON; null neu sai cu phap/thieu truong bat buoc.</summary>
        public static PluginManifestDef ParseManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var m = JsonSerializer.Deserialize<PluginManifestDef>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (m == null) return null;
                if (m.Scenes == null) m.Scenes = new List<PluginSceneDef>();
                foreach (var s in m.Scenes)
                    if (s.Lines == null) s.Lines = new List<string>();
                return m;
            }
            catch { return null; }
        }

        /// <summary>
        /// Kiem tra toan bo mot goi: danh sach ten file trong archive (duong
        /// dan tuong doi, dau "/") + noi dung manifest.json da doc san.
        /// manifestJson == null nghia la archive khong co manifest.json o goc.
        /// </summary>
        public static PluginValidation Validate(IReadOnlyCollection<string> entryNames, string manifestJson)
        {
            PluginValidation Fail(string e) => new PluginValidation { Ok = false, Error = e };

            if (entryNames != null)
                foreach (var n in entryNames)
                    if (IsForbidden(n))
                        return Fail("Gói plugin bị từ chối: chứa file thực thi/script “" + n +
                                    "”. Plugin của Tien Vu Keyboard Studio chỉ là DỮ LIỆU (manifest + ảnh/GIF) — app không bao giờ chạy mã ngoài.");

            if (manifestJson == null)
                return Fail("Gói plugin bị từ chối: thiếu file manifest.json ở thư mục gốc của gói. Xem định dạng manifest trong README của app.");

            var m = ParseManifest(manifestJson);
            if (m == null)
                return Fail("Gói plugin bị từ chối: manifest.json sai cú pháp JSON hoặc thiếu cấu trúc bắt buộc.");

            if (!IsValidId(m.Id))
                return Fail("Gói plugin bị từ chối: trường \"id\" trong manifest không hợp lệ (chỉ chữ thường, số và dấu -, 2–41 ký tự, bắt đầu bằng chữ/số).");
            if (string.IsNullOrWhiteSpace(m.Name))
                return Fail("Gói plugin bị từ chối: manifest thiếu trường \"name\".");
            if (!string.Equals(m.Kind, KindGreeting, StringComparison.OrdinalIgnoreCase))
                return Fail("Gói plugin bị từ chối: hiện tại app chỉ hỗ trợ plugin loại \"greeting\" (màn hình chào). Manifest đang ghi kind = \"" + (m.Kind ?? "") + "\".");
            if (m.Scenes == null || m.Scenes.Count == 0)
                return Fail("Gói plugin bị từ chối: manifest chưa khai báo cảnh nào (\"scenes\" rỗng).");

            var entrySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (entryNames != null)
                foreach (var n in entryNames) entrySet.Add(NormalizeEntry(n));

            foreach (var s in m.Scenes)
            {
                string t = (s.Type ?? "").ToLowerInvariant();
                if (t != "image" && t != "gif" && t != "title" && t != "lines" && t != "logo")
                    return Fail("Gói plugin bị từ chối: loại cảnh \"" + s.Type + "\" không được hỗ trợ (chỉ image / gif / title / lines / logo).");
                if (t == "image" || t == "gif")
                {
                    if (string.IsNullOrWhiteSpace(s.Asset) || !IsAssetFile(s.Asset))
                        return Fail("Gói plugin bị từ chối: cảnh " + t + " phải trỏ tới một file ảnh/GIF (.png/.jpg/.gif/.webp/.bmp) trong gói.");
                    if (!entrySet.Contains(NormalizeEntry(s.Asset)))
                        return Fail("Gói plugin bị từ chối: manifest nhắc tới file \"" + s.Asset + "\" nhưng file đó không có trong gói.");
                }
                if (t == "title" && string.IsNullOrWhiteSpace(s.Text))
                    return Fail("Gói plugin bị từ chối: cảnh title thiếu \"text\".");
                if (t == "lines" && (s.Lines == null || s.Lines.Count == 0))
                    return Fail("Gói plugin bị từ chối: cảnh lines cần ít nhất một dòng chữ trong \"lines\".");
            }
            return new PluginValidation { Ok = true, Manifest = m };
        }

        /// <summary>Chuan hoa duong dan trong archive ve dang so sanh (bo "./", doi "\" thanh "/").</summary>
        public static string NormalizeEntry(string name)
        {
            string n = (name ?? "").Replace('\\', '/').Trim();
            while (n.StartsWith("./")) n = n.Substring(2);
            return n;
        }
    }

    /// <summary>
    /// Phan giai link GitHub -> zip: link repo github.com/owner/repo thi
    /// hoi API lay nhanh mac dinh roi dung URL codeload; link .zip truc
    /// tiep thi dung nguyen. Thuan logic (URL), unit test duoc.
    /// </summary>
    public static class GitHubZip
    {
        public static bool IsDirectZipUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
            if (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps) return false;
            return u.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseRepoUrl(string url, out string owner, out string repo)
        {
            owner = null; repo = null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
            if (!string.Equals(u.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return false;
            var parts = u.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;
            owner = parts[0];
            repo = parts[1];
            if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) repo = repo.Substring(0, repo.Length - 4);
            return !string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo);
        }

        public static string BuildRepoApiUrl(string owner, string repo)
            => "https://api.github.com/repos/" + owner + "/" + repo;

        public static string BuildCodeloadUrl(string owner, string repo, string branch)
            => "https://codeload.github.com/" + owner + "/" + repo + "/zip/refs/heads/" + Uri.EscapeDataString(branch ?? "main");

        /// <summary>Doc nhanh mac dinh tu JSON cua GitHub API (repos/:owner/:repo).</summary>
        public static string ParseDefaultBranch(string apiJson)
        {
            if (string.IsNullOrWhiteSpace(apiJson)) return null;
            try
            {
                using (var doc = JsonDocument.Parse(apiJson))
                {
                    if (doc.RootElement.TryGetProperty("default_branch", out var b) &&
                        b.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(b.GetString()))
                        return b.GetString();
                }
            }
            catch { }
            return null;
        }
    }

    /// <summary>
    /// Cai dat / liet ke plugin tu nhap trong %AppData%\TienKeyboardStudio\
    /// Plugins\&lt;id&gt;\. Moi thao tac ghi deu di qua kiem tra cua
    /// PluginPackageRules truoc.
    /// </summary>
    public static class PluginInstaller
    {
        public static string PluginsDir => Path.Combine(GifLibraryStore.RootDir, "Plugins");
        public const long MaxZipBytes = 60L * 1024 * 1024;

        /// <summary>
        /// Cai mot file .zip: giai nen an toan (chan Zip Slip) vao thu muc
        /// tam, tim goc goi (manifest.json o goc hoac trong dung mot thu
        /// muc con duy nhat — dang zip cua GitHub), kiem tra, roi sao chep
        /// vao Plugins/&lt;id&gt;. Tra ve ket qua kiem tra; thanh cong thi
        /// installedDir la thu muc da cai.
        /// </summary>
        public static PluginValidation InstallFromZip(string zipPath, out string installedDir)
        {
            installedDir = null;
            string tmp = Path.Combine(Path.GetTempPath(), "tvks-plugin-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tmp);
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    var names = new List<string>();
                    foreach (var e in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(e.Name)) continue; // thu muc
                        string rel = PluginPackageRules.NormalizeEntry(e.FullName);
                        // Chan Zip Slip: duong dan giai nen khong duoc thoat khoi tmp
                        string dest = Path.GetFullPath(Path.Combine(tmp, rel.Replace('/', Path.DirectorySeparatorChar)));
                        if (!dest.StartsWith(Path.GetFullPath(tmp) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            return new PluginValidation { Ok = false, Error = "Gói plugin bị từ chối: đường dẫn trong archive không an toàn (“" + e.FullName + "”)." };
                        names.Add(rel);
                    }
                    // Kiem tra file cam tren DANH SACH TEN truoc khi giai nen
                    foreach (var n in names)
                        if (PluginPackageRules.IsForbidden(n))
                            return new PluginValidation { Ok = false, Error = "Gói plugin bị từ chối: chứa file thực thi/script “" + n + "”. Plugin chỉ là DỮ LIỆU (manifest + ảnh/GIF) — app không bao giờ chạy mã ngoài." };
                    foreach (var e in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(e.Name)) continue;
                        string rel = PluginPackageRules.NormalizeEntry(e.FullName);
                        string dest = Path.GetFullPath(Path.Combine(tmp, rel.Replace('/', Path.DirectorySeparatorChar)));
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        e.ExtractToFile(dest, overwrite: true);
                    }
                }

                // Tim goc goi: manifest.json o goc tmp, hoac trong mot thu muc con duy nhat
                string root = tmp;
                if (!File.Exists(Path.Combine(root, "manifest.json")))
                {
                    var subs = Directory.GetDirectories(tmp);
                    if (subs.Length == 1 && File.Exists(Path.Combine(subs[0], "manifest.json")))
                        root = subs[0];
                }
                string manifestPath = Path.Combine(root, "manifest.json");
                string manifestJson = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null;

                var relNames = new List<string>();
                foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    relNames.Add(PluginPackageRules.NormalizeEntry(f.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

                var check = PluginPackageRules.Validate(relNames, manifestJson);
                if (!check.Ok) return check;

                installedDir = Path.Combine(PluginsDir, check.Manifest.Id);
                Directory.CreateDirectory(PluginsDir);
                if (Directory.Exists(installedDir)) Directory.Delete(installedDir, true);
                CopyDir(root, installedDir);
                check.Manifest = LoadManifest(installedDir) ?? check.Manifest;
                return check;
            }
            catch (Exception ex)
            {
                return new PluginValidation { Ok = false, Error = "Không cài được gói plugin: " + ex.Message };
            }
            finally
            {
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            }
        }

        private static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: true);
            foreach (var d in Directory.GetDirectories(src))
                CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        /// <summary>Doc manifest cua mot plugin da cai (null neu hong/thieu).</summary>
        public static PluginManifestDef LoadManifest(string pluginDir)
        {
            try
            {
                string p = Path.Combine(pluginDir, "manifest.json");
                if (!File.Exists(p)) return null;
                return PluginPackageRules.ParseManifest(File.ReadAllText(p));
            }
            catch { return null; }
        }

        /// <summary>Liet ke plugin da cai hop le: (manifest, thu muc).</summary>
        public static List<(PluginManifestDef Manifest, string Dir)> ListInstalled()
        {
            var list = new List<(PluginManifestDef, string)>();
            try
            {
                if (!Directory.Exists(PluginsDir)) return list;
                foreach (var d in Directory.GetDirectories(PluginsDir))
                {
                    var m = LoadManifest(d);
                    if (m != null && PluginPackageRules.IsValidId(m.Id)) list.Add((m, d));
                }
            }
            catch { }
            list.Sort((x, y) => string.Compare(x.Item1.Name, y.Item1.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>Tim mot plugin da cai theo id manifest (null neu khong co).</summary>
        public static (PluginManifestDef Manifest, string Dir) FindInstalled(string manifestId)
        {
            foreach (var it in ListInstalled())
                if (string.Equals(it.Manifest.Id, manifestId, StringComparison.OrdinalIgnoreCase))
                    return it;
            return (null, null);
        }
    }
}
