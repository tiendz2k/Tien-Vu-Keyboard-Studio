using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // STAGE 2 — Ho so theo ung dung: theo doi cua so dang active
    // (GetForegroundWindow), khi doi app thi goi callback de MainWindow ap
    // dung hieu ung firmware + anh TFT da gan cho app do.
    // Luu o %AppData%\TienKeyboardStudio\app-profiles.json.
    // =====================================================================
    public sealed class AppProfile
    {
        public string ExeName { get; set; } = "";
        public int EffectId { get; set; } = 11;
        public string TftGifPath { get; set; } = "";
        // v3.12: ho so luu DU trang thai den (truoc day chi EffectId).
        // HasLighting = false voi file app-profiles.json cu (khong co cac
        // truong nay) -> ap dung kieu cu (chi doi hieu ung, giu mau hien tai).
        public bool HasLighting { get; set; } = false;
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
        public bool Multi { get; set; }
        public int BrightnessUi { get; set; } = 3;
        public int SpeedUi { get; set; } = 2;
        public int Direction { get; set; }
    }

    public sealed class AppProfileService : IDisposable
    {
        public List<AppProfile> Profiles = new List<AppProfile>();
        public string CurrentExe { get; private set; } = "";
        public event Action<string> ForegroundChanged;

        private Thread _thread;
        private volatile bool _running;
        private static string StorePath => Path.Combine(GifLibraryStore.RootDir, "app-profiles.json");

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        public void Load()
        {
            try
            {
                GifLibraryStore.EnsureDirs();
                if (File.Exists(StorePath))
                    Profiles = JsonSerializer.Deserialize<List<AppProfile>>(File.ReadAllText(StorePath)) ?? new List<AppProfile>();
            }
            catch { Profiles = new List<AppProfile>(); }
        }

        public void Save()
        {
            try
            {
                GifLibraryStore.EnsureDirs();
                File.WriteAllText(StorePath, JsonSerializer.Serialize(Profiles, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public AppProfile Find(string exe)
            => Profiles.Find(p => string.Equals(p.ExeName, exe, StringComparison.OrdinalIgnoreCase));

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(() =>
            {
                while (_running)
                {
                    try
                    {
                        var h = GetForegroundWindow();
                        if (h != IntPtr.Zero && GetWindowThreadProcessId(h, out uint pid) != 0)
                        {
                            string exe = "";
                            try { exe = Process.GetProcessById((int)pid).ProcessName + ".exe"; } catch { }
                            if (!string.IsNullOrEmpty(exe) && !string.Equals(exe, CurrentExe, StringComparison.OrdinalIgnoreCase))
                            {
                                CurrentExe = exe;
                                ForegroundChanged?.Invoke(exe);
                            }
                        }
                    }
                    catch { }
                    Thread.Sleep(700);
                }
            })
            { IsBackground = true, Name = "AppProfileWatcher" };
            _thread.Start();
        }

        public void Stop() => _running = false;
        public void Dispose() => Stop();
    }
}
