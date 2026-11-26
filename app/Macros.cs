using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // STAGE 2 — Macro host-side: ghi lai chuoi phim + do tre bang hook
    // WH_KEYBOARD_LL, phat lai bang SendInput. Phim tat toan cuc:
    // Ctrl+Alt+R = bat/tat ghi, Ctrl+Alt+P = phat macro gan nhat.
    // Luu JSON o %AppData%\TienKeyboardStudio\macros.json.
    // LUU Y: macro chi chay tren may (host), KHONG ghi xuong ban phim.
    // =====================================================================
    public sealed class MacroStep { public int Vk { get; set; } public bool Down { get; set; } public int DelayMs { get; set; } }
    public sealed class MacroDef { public string Name { get; set; } = ""; public List<MacroStep> Steps { get; set; } = new List<MacroStep>(); }

    public sealed class MacroService : IDisposable
    {
        public List<MacroDef> Macros = new List<MacroDef>();
        public bool Recording { get; private set; }
        public MacroDef Current { get; private set; }
        public event Action<string> StatusChanged;

        private IntPtr _hook = IntPtr.Zero;
        private LowLevelKeyboardProc _proc; // giu tham chieu tranh GC
        private long _lastTick;
        private static string StorePath => Path.Combine(GifLibraryStore.RootDir, "macros.json");

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;

        public void Load()
        {
            try
            {
                GifLibraryStore.EnsureDirs();
                if (File.Exists(StorePath))
                    Macros = JsonSerializer.Deserialize<List<MacroDef>>(File.ReadAllText(StorePath)) ?? new List<MacroDef>();
            }
            catch { Macros = new List<MacroDef>(); }
        }

        public void Save()
        {
            try
            {
                GifLibraryStore.EnsureDirs();
                File.WriteAllText(StorePath, JsonSerializer.Serialize(Macros, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // Dang ky phim tat toan cuc tren cua so chinh (hwnd cua app).
        public void RegisterHotkeys(IntPtr hwnd)
        {
            const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_NOREPEAT = 0x4000;
            RegisterHotKey(hwnd, 9001, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x52); // Ctrl+Alt+R
            RegisterHotKey(hwnd, 9002, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x50); // Ctrl+Alt+P
        }

        public void UnregisterHotkeys(IntPtr hwnd)
        {
            UnregisterHotKey(hwnd, 9001);
            UnregisterHotKey(hwnd, 9002);
        }

        // Goi tu WndProc cua MainWindow khi nhan WM_HOTKEY (0x0312).
        public void OnHotkey(int id)
        {
            if (id == 9001)
            {
                if (Recording) StopRecording(); else StartRecording("Macro " + (Macros.Count + 1));
            }
            else if (id == 9002)
            {
                var m = Current ?? (Macros.Count > 0 ? Macros[Macros.Count - 1] : null);
                if (m != null) Play(m);
            }
        }

        public void StartRecording(string name)
        {
            if (Recording) return;
            Current = new MacroDef { Name = name };
            _lastTick = Environment.TickCount64;
            InstallHook();
            Recording = true;
            StatusChanged?.Invoke($"Đang ghi macro '{name}'… (Ctrl+Alt+R để dừng)");
        }

        public void StopRecording()
        {
            if (!Recording) return;
            Recording = false;
            RemoveHook();
            if (Current != null && Current.Steps.Count > 0)
            {
                Macros.Add(Current);
                Save();
                StatusChanged?.Invoke($"Đã lưu macro '{Current.Name}' ({Current.Steps.Count} bước).");
            }
            else StatusChanged?.Invoke("Macro rỗng — không lưu.");
        }

        private void InstallHook()
        {
            if (_hook != IntPtr.Zero) return;
            _proc = HookCallback;
            using (var cur = System.Diagnostics.Process.GetCurrentProcess())
            using (var mod = cur.MainModule)
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(mod.ModuleName), 0);
        }

        private void RemoveHook()
        {
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && Recording && Current != null)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_KEYUP || msg == WM_SYSKEYDOWN || msg == WM_SYSKEYUP)
                {
                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    long now = Environment.TickCount64;
                    Current.Steps.Add(new MacroStep
                    {
                        Vk = (int)data.vkCode,
                        Down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN,
                        DelayMs = (int)Math.Min(10000, now - _lastTick),
                    });
                    _lastTick = now;
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Play(MacroDef macro)
        {
            var steps = macro.Steps;
            new Thread(() =>
            {
                StatusChanged?.Invoke($"Đang phát macro '{macro.Name}'…");
                foreach (var s in steps)
                {
                    if (s.DelayMs > 0) Thread.Sleep(Math.Min(s.DelayMs, 5000));
                    var input = new INPUT
                    {
                        type = 1, // INPUT_KEYBOARD
                        U = new InputUnion { ki = new KEYBDINPUT { wVk = (ushort)s.Vk, dwFlags = s.Down ? 0u : 0x0002u } },
                    };
                    SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
                }
                StatusChanged?.Invoke($"Đã phát xong macro '{macro.Name}'.");
            })
            { IsBackground = true }.Start();
        }

        public void Dispose()
        {
            if (Recording) StopRecording();
            RemoveHook();
        }
    }

    // Gan vao cua so Avalonia (Win32) de nhan WM_HOTKEY qua SetWindowSubclass.
    public static class HotkeyWindow
    {
        private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)] private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);
        [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private static SubclassProc _proc; // giu tham chieu tranh GC
        private static Action<int> _onHotkey;
        private const uint WM_HOTKEY = 0x0312;

        public static void Attach(IntPtr hwnd, Action<int> onHotkey)
        {
            if (hwnd == IntPtr.Zero) return;
            _onHotkey = onHotkey;
            _proc = Proc;
            SetWindowSubclass(hwnd, _proc, (UIntPtr)1, IntPtr.Zero);
        }

        private static IntPtr Proc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_HOTKEY)
            {
                try { _onHotkey?.Invoke(wParam.ToInt32()); } catch { }
                return IntPtr.Zero;
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }
}
