using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Thong so he thong that cho widget TFT:
    // - CPU %: GetSystemTimes (kernel32) — chenh lech idle/kernel/user.
    // - RAM %: GlobalMemoryStatusEx (kernel32).
    // - GPU: nvidia-smi neu co (may Tien dung RTX 3070 Ti); khong co thi
    //   tra null va widget tu an dong GPU (graceful fallback).
    // - Nhiet do CPU: khong doc duoc neu khong co driver cam bien -> de trong.
    // =====================================================================
    public sealed class SystemStats
    {
        public int CpuPercent;
        public int RamPercent;
        public int? GpuPercent;
        public int? GpuTemp;
        public int? GpuMemPercent;
        public bool HasGpu => GpuPercent.HasValue;

        private static long _lastIdle, _lastKernel, _lastUser;
        private static bool _primed;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        public static SystemStats Sample()
        {
            var s = new SystemStats();
            try
            {
                if (GetSystemTimes(out long idle, out long kernel, out long user))
                {
                    if (_primed)
                    {
                        long idleD = idle - _lastIdle, totalD = (kernel - _lastKernel) + (user - _lastUser);
                        if (totalD > 0)
                            s.CpuPercent = (int)Math.Round(100.0 * (totalD - idleD) / totalD);
                    }
                    _lastIdle = idle; _lastKernel = kernel; _lastUser = user; _primed = true;
                }
            }
            catch { }
            try
            {
                var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref mem)) s.RamPercent = (int)mem.dwMemoryLoad;
            }
            catch { }
            try
            {
                var psi = new ProcessStartInfo("nvidia-smi",
                    "--query-gpu=utilization.gpu,temperature.gpu,memory.used,memory.total --format=csv,noheader,nounits")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var p = Process.Start(psi))
                {
                    if (p != null && p.WaitForExit(2000))
                    {
                        string line = p.StandardOutput.ReadToEnd().Trim().Split('\n')[0];
                        var parts = line.Split(',');
                        if (parts.Length >= 4 &&
                            int.TryParse(parts[0].Trim(), out int gpu) &&
                            int.TryParse(parts[1].Trim(), out int temp) &&
                            int.TryParse(parts[2].Trim(), out int used) &&
                            int.TryParse(parts[3].Trim(), out int total) && total > 0)
                        {
                            s.GpuPercent = gpu;
                            s.GpuTemp = temp;
                            s.GpuMemPercent = (int)Math.Round(100.0 * used / total);
                        }
                    }
                }
            }
            catch { /* khong co nvidia-smi -> bo qua GPU */ }
            return s;
        }
    }
}
