using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TienKeyboardStudio3;

namespace TienVuUpdater
{
    // =====================================================================
    // TienVuUpdater — helper cua tinh nang cap nhat mot nut bam (v3.17).
    //
    // App chinh chep helper nay vao %AppData%\TienKeyboardStudio\Updates\
    // roi mo no tu do (ban chep chay o Updates nen file helper GOC trong
    // thu muc cai duoc thay tu do — mot exe dang chay khong tu de len
    // chinh no duoc). Tham so duy nhat: duong dan update-job.json.
    //
    // Luong: doc job -> doi app thoat han (theo PID + mo khoa file exe)
    // -> UpdaterCore.PerformSwap (staging/backup/rollback) -> ghi
    // update-result.json -> mo lai app -> don job (+ zip khi thanh
    // cong; that bai thi GIU zip de thu lai khong phai tai lai).
    // Moi loi deu thanh result + mo lai ban cu — khong bao gio de
    // nguoi dung o lai voi mot thu muc cai do dang.
    // =====================================================================
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
                return 2;
            string jobPath = args[0];
            string updatesDir = Path.GetDirectoryName(Path.GetFullPath(jobPath)) ?? "";

            UpdateJob job = null;
            try { job = UpdaterCore.ParseJob(File.ReadAllText(jobPath)); } catch { }
            if (job == null) return 2; // job hong — khong biet mo lai cai gi

            // Bao "con song" cho app: app chi thoat khi thay file nay.
            try { File.WriteAllText(UpdaterCore.AlivePathIn(updatesDir), DateTime.UtcNow.ToString("o")); }
            catch { }

            WaitForAppExit(job);

            string workDir = Path.Combine(updatesDir, "swap-work");
            var (ok, error) = UpdaterCore.PerformSwap(job.ZipPath, job.TargetDir, job.RelaunchExe, workDir);

            UpdaterCore.WriteResult(updatesDir, new UpdateResultNote
            {
                Ok = ok,
                Version = job.ToVersion ?? "",
                Message = ok
                    ? "Đã cập nhật lên bản " + job.ToVersion + "."
                    : "Cập nhật tự động không thành — vẫn đang dùng bản cũ. Chi tiết: " + error,
            });

            TryStartApp(job);

            // Don dep: job + file alive luon xoa; zip chi xoa khi thanh
            // cong (that bai giu lai de lan sau khoi tai lai).
            try { File.Delete(jobPath); } catch { }
            try { File.Delete(UpdaterCore.AlivePathIn(updatesDir)); } catch { }
            if (ok) { try { File.Delete(job.ZipPath); } catch { } }
            return ok ? 0 : 1;
        }

        /// <summary>Doi tien trinh app thoat han (toi da ~30s), roi doi
        /// them den khi file exe het bi khoa (toi da ~15s nua).</summary>
        private static void WaitForAppExit(UpdateJob job)
        {
            if (job.AppPid > 0)
            {
                try
                {
                    var p = Process.GetProcessById(job.AppPid);
                    p.WaitForExit(30000);
                }
                catch { /* app da thoat san / PID khong con */ }
            }
            string exePath = Path.Combine(job.TargetDir, job.RelaunchExe);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 15000)
            {
                try
                {
                    using (var fs = File.Open(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    break; // mo duoc doc-quyen -> het khoa
                }
                catch { Thread.Sleep(250); }
            }
        }

        private static void TryStartApp(UpdateJob job)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(job.TargetDir, job.RelaunchExe),
                    WorkingDirectory = job.TargetDir,
                    UseShellExecute = true,
                });
            }
            catch { /* mo lai that bai — result da ghi, nguoi dung mo tay */ }
        }
    }
}
