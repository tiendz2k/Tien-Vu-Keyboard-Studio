using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.17 (scope C) — CAP NHAT MOT NUT BAM (yeu cau chac chan cua
    // Tien): tu ban nay, bam "Cap nhat ngay" la app lam het: tai zip
    // (UpdateCore, da co tu v3.14) -> kiem SHA-256 -> ghi job -> mo
    // helper TienVuUpdater.exe -> thoat; helper doi app thoat han, giai
    // nen zip de len thu muc app, mo lai app, don job + zip.
    //
    // File nay THUAN LOGIC dung chung cho ca ba noi: app (ghi job, doc
    // ket qua), helper (PerformSwap) va unit test. Helper chi la cai vo
    // mong (doi PID, goi PerformSwap, ghi result, mo lai app).
    //
    // An toan cua PerformSwap:
    // - Giai nen vao thu muc staging truoc; zip co muc thoat ra ngoai
    //   (zip-slip) -> huy ngay.
    // - Truoc khi de file nao, sao luu ban dang co vao thu muc backup;
    //   file moi (truoc do khong co) duoc ghi nho de xoa khi rollback.
    // - Bat ky loi nao giua chung -> rollback: khoi phuc backup, xoa
    //   file moi them, ban cu nguyen ven; helper mo lai ban cu va ghi
    //   result loi de app hien mot dong ghi chu o lan mo sau.
    // - Settings/du lieu o %AppData% khong bao gio bi cham vao: swap
    //   chi ghi trong thu muc cai app (TargetDir).
    // =====================================================================

    /// <summary>Job app ghi cho helper truoc khi thoat.</summary>
    public sealed class UpdateJob
    {
        public string ZipPath { get; set; } = "";
        public string TargetDir { get; set; } = "";
        public string RelaunchExe { get; set; } = "";
        public int AppPid { get; set; }
        public string ToVersion { get; set; } = "";
    }

    /// <summary>Ket qua helper ghi lai cho app doc o lan mo ke tiep.</summary>
    public sealed class UpdateResultNote
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public string Version { get; set; } = "";
    }

    // JSON source-generation context: helper publish dang TRIM nen
    // khong duoc dung JsonSerializer reflection (IL2026 — cac property
    // co the bi cat, job/result se hong tren may nguoi dung). Context
    // nay dung cho ca app lan helper lan test.
    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(UpdateJob))]
    [JsonSerializable(typeof(UpdateResultNote))]
    internal sealed partial class UpdaterJsonContext : JsonSerializerContext
    {
    }

    public static class UpdaterCore
    {
        public const string HelperExeName = "TienVuUpdater.exe";
        public const string JobFileName = "update-job.json";
        public const string ResultFileName = "update-result.json";
        // File "con song": helper ghi ngay khi vua khoi dong (truoc khi
        // doi app thoat). App chi thoat khi thay file nay — neu helper
        // khong chay duoc (vd may thieu .NET Runtime cho ban
        // framework-dependent) app huy cap nhat va giu nguyen trang thai.
        public const string AliveFileName = "update-helper-alive.txt";

        public static string JobPathIn(string updatesDir) => Path.Combine(updatesDir, JobFileName);
        public static string ResultPathIn(string updatesDir) => Path.Combine(updatesDir, ResultFileName);
        public static string HelperCopyPathIn(string updatesDir) => Path.Combine(updatesDir, HelperExeName);
        public static string AlivePathIn(string updatesDir) => Path.Combine(updatesDir, AliveFileName);

        // ---------------- Job / result JSON ----------------

        public static string SerializeJob(UpdateJob job)
            => JsonSerializer.Serialize(job, UpdaterJsonContext.Default.UpdateJob);

        /// <summary>Parse job; null neu hong/thieu truong bat buoc.</summary>
        public static UpdateJob ParseJob(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var j = JsonSerializer.Deserialize(json, UpdaterJsonContext.Default.UpdateJob);
                if (j == null) return null;
                if (string.IsNullOrWhiteSpace(j.ZipPath) ||
                    string.IsNullOrWhiteSpace(j.TargetDir) ||
                    string.IsNullOrWhiteSpace(j.RelaunchExe)) return null;
                return j;
            }
            catch { return null; }
        }

        public static string SerializeResult(UpdateResultNote r)
            => JsonSerializer.Serialize(r, UpdaterJsonContext.Default.UpdateResultNote);

        public static UpdateResultNote ParseResult(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize(json, UpdaterJsonContext.Default.UpdateResultNote);
            }
            catch { return null; }
        }

        /// <summary>Ghi result (ghi de neu da co). Khong nem loi ra ngoai.</summary>
        public static bool WriteResult(string updatesDir, UpdateResultNote r)
        {
            try
            {
                Directory.CreateDirectory(updatesDir);
                File.WriteAllText(ResultPathIn(updatesDir), SerializeResult(r));
                return true;
            }
            catch { return false; }
        }

        /// <summary>Doc result neu co (khong xoa — app tu xoa sau khi hien).</summary>
        public static UpdateResultNote ReadResult(string updatesDir)
        {
            try
            {
                string p = ResultPathIn(updatesDir);
                return File.Exists(p) ? ParseResult(File.ReadAllText(p)) : null;
            }
            catch { return null; }
        }

        // ---------------- Hoan doi ban moi (helper dung) ----------------

        /// <summary>
        /// De noi dung zip len targetDir mot cach an toan (staging ->
        /// backup -> copy -> rollback khi loi). workDir la noi chua
        /// staging/backup tam (helper dat trong UpdatesDir). Tra
        /// (true, null) khi xong; (false, ly do) khi da rollback.
        /// </summary>
        public static (bool Ok, string Error) PerformSwap(
            string zipPath, string targetDir, string relaunchExeName, string workDir)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                return (false, "Không thấy file zip bản mới.");
            if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
                return (false, "Không thấy thư mục cài app.");
            if (string.IsNullOrWhiteSpace(relaunchExeName))
                return (false, "Job thiếu tên exe mở lại.");

            string stageDir = Path.Combine(workDir, "stage");
            string backupDir = Path.Combine(workDir, "backup");
            var addedFiles = new List<string>();    // file moi (truoc do khong co) — xoa khi rollback
            var backedUp = new List<string>();      // duong dan tuong doi da sao luu
            try
            {
                // Don staging/backup cu (neu con sot tu lan chay truoc).
                TryDeleteDir(stageDir); TryDeleteDir(backupDir);
                Directory.CreateDirectory(stageDir);
                Directory.CreateDirectory(backupDir);

                // 1) Giai nen vao staging, chan zip-slip.
                string stageFull = Path.GetFullPath(stageDir);
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    bool hasExe = false;
                    foreach (var e in zip.Entries)
                    {
                        if (string.Equals(e.Name, relaunchExeName, StringComparison.OrdinalIgnoreCase) &&
                            (e.FullName == e.Name || e.FullName.EndsWith("/" + e.Name)))
                            hasExe = true;
                    }
                    if (!hasExe)
                        return (false, "Zip không chứa " + relaunchExeName + " — không phải gói app hợp lệ.");
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // muc thu muc
                        string dest = Path.GetFullPath(Path.Combine(stageFull, entry.FullName));
                        if (!dest.StartsWith(stageFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                            !dest.Equals(stageFull, StringComparison.OrdinalIgnoreCase))
                            return (false, "Zip có mục thoát ra ngoài thư mục đích — đã huỷ.");
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        entry.ExtractToFile(dest, overwrite: true);
                    }
                }

                // 2) Thu ghi duoc khong (thu muc cai khong ghi duoc thi
                //    dung som, chua cham vao gi).
                string probe = Path.Combine(targetDir, ".tvks-write-probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);

                // 3) Sao luu cac file sap bi de + ghi nho file moi.
                var staged = new List<string>();
                foreach (var f in Directory.EnumerateFiles(stageDir, "*", SearchOption.AllDirectories))
                    staged.Add(Path.GetRelativePath(stageDir, f));
                foreach (var rel in staged)
                {
                    string targetFile = Path.Combine(targetDir, rel);
                    if (File.Exists(targetFile))
                    {
                        string bak = Path.Combine(backupDir, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(bak));
                        File.Copy(targetFile, bak, overwrite: true);
                        backedUp.Add(rel);
                    }
                    else addedFiles.Add(rel);
                }

                // 4) De ban moi len.
                foreach (var rel in staged)
                {
                    string targetFile = Path.Combine(targetDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                    File.Copy(Path.Combine(stageDir, rel), targetFile, overwrite: true);
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                // Rollback: khoi phuc backup, xoa file moi them.
                try
                {
                    foreach (var rel in backedUp)
                        File.Copy(Path.Combine(backupDir, rel), Path.Combine(targetDir, rel), overwrite: true);
                    foreach (var rel in addedFiles)
                    {
                        string t = Path.Combine(targetDir, rel);
                        if (File.Exists(t)) File.Delete(t);
                    }
                }
                catch { /* rollback het suc — ban cu co the da bi cham mot phan */ }
                return (false, ex.Message);
            }
            finally
            {
                TryDeleteDir(stageDir);
                TryDeleteDir(backupDir);
            }
        }

        private static void TryDeleteDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { }
        }
    }
}
