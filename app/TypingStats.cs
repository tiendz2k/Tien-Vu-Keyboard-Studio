using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // STAGE 2 — Thong ke go phim (count-only): hook WH_KEYBOARD_LL CHI DEM
    // so lan nhan theo VI TRI phim (de ve heatmap) + tong so phim + WPM.
    // TUYET DOI khong ghi noi dung, khong ghi chuoi phim, khong luu thu tu
    // go — chi co bo dem tong hop trong RAM, xoa khi tat app.
    // v3.15: hook con phat su kien KeyPositionPressed (CHI index vi tri
    // phim, khong ma VK) cho hieu ung go phim phan ung (ReactiveCore) —
    // DUNG CHUNG mot cong tac opt-in nay: tat dem phim la khong con su
    // kien nao duoc tao ra. Su kien chi song trong RAM cua engine phan
    // ung, khong ghi log/file.
    // =====================================================================
    public sealed class TypingStatsService : IDisposable
    {
        /// <summary>v3.15: phat khi mot phim trong bang duoc nhan — tham
        /// so CHI la index vi tri phim tren KeyboardView (0..79).</summary>
        public event Action<int> KeyPositionPressed;
        // Bo dem theo index phim cua KeyboardView (0..79); -1 = phim ngoai bang.
        public int[] KeyCounts { get; } = new int[80];
        public long TotalKeys { get; private set; }
        public DateTime SessionStart { get; private set; } = DateTime.Now;
        private readonly Queue<long> _recentTicks = new Queue<long>(); // chi thoi diem (ms) de tinh WPM truot 60s
        private readonly object _gate = new object();

        private IntPtr _hook = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;
        private volatile bool _running;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG lpMsg);
        [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int ptX, ptY; }

        private uint _hookThreadId;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;

        // VK -> index phim KeyboardView (khop thu tu MaxKeyData.ViewKeyWireIds)
        private static readonly Dictionary<int, int> VkToView = BuildMap();

        private static Dictionary<int, int> BuildMap()
        {
            var m = new Dictionary<int, int>();
            void Add(int vk, int view) => m[vk] = view;
            Add(0x1B, 0); // Esc
            for (int i = 0; i < 12; i++) Add(0x70 + i, 1 + i); // F1..F12
            Add(0xC0, 13); // `
            for (int i = 0; i < 9; i++) Add(0x31 + i, 14 + i); // 1..9
            Add(0x30, 23); // 0
            Add(0xBD, 24); Add(0xBB, 25); Add(0x08, 26); Add(0x2E, 27); // - = Bksp Del
            Add(0x09, 28); // Tab
            int[] letters1 = { 0x51, 0x57, 0x45, 0x52, 0x54, 0x59, 0x55, 0x49, 0x4F, 0x50 }; // Q..P
            for (int i = 0; i < 10; i++) Add(letters1[i], 29 + i);
            Add(0xDB, 39); Add(0xDD, 40); Add(0xDC, 41); Add(0x21, 42); // [ ] \ PgUp
            Add(0x14, 43); // Caps
            int[] letters2 = { 0x41, 0x53, 0x44, 0x46, 0x47, 0x48, 0x4A, 0x4B, 0x4C }; // A..L
            for (int i = 0; i < 9; i++) Add(letters2[i], 44 + i);
            Add(0xBA, 53); Add(0xDE, 54); Add(0x0D, 55); Add(0x22, 56); // ; ' Enter PgDn
            Add(0xA0, 57); Add(0x10, 57); // Shift trai (ca VK_SHIFT chung)
            int[] letters3 = { 0x5A, 0x58, 0x43, 0x56, 0x42, 0x4E, 0x4D }; // Z..M
            for (int i = 0; i < 7; i++) Add(letters3[i], 58 + i);
            Add(0xBC, 65); Add(0xBE, 66); Add(0xBF, 67); Add(0xA1, 68); Add(0x26, 69); Add(0x23, 70); // , . / ShiftR Up End
            Add(0xA2, 71); Add(0x11, 71); // Ctrl trai
            Add(0x5B, 72); Add(0xA4, 73); Add(0x20, 74); Add(0xA5, 75); // Win Alt Space AltR
            // 76 = Fn: khong co VK — khong bao gio dem duoc (dung thiet ke).
            Add(0x25, 77); Add(0x28, 78); Add(0x27, 79); // Left Down Right
            return m;
        }

        public double Wpm
        {
            get
            {
                lock (_gate)
                {
                    long now = Environment.TickCount64;
                    while (_recentTicks.Count > 0 && now - _recentTicks.Peek() > 60000) _recentTicks.Dequeue();
                    return _recentTicks.Count / 5.0; // 5 ky tu = 1 "tu", cua so 60 giay
                }
            }
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            SessionStart = DateTime.Now;
            _proc = HookCallback;
            new Thread(() =>
            {
                // Hook LL chi nhan callback khi thread cai dat CO message pump.
                _hookThreadId = GetCurrentThreadId();
                using (var cur = System.Diagnostics.Process.GetCurrentProcess())
                using (var mod = cur.MainModule)
                    _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(mod.ModuleName), 0);
                while (_running && GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
                if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            })
            { IsBackground = true, Name = "TypingStatsHook" }.Start();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    TotalKeys++;
                    lock (_gate)
                    {
                        _recentTicks.Enqueue(Environment.TickCount64);
                        if (_recentTicks.Count > 20000) _recentTicks.Dequeue();
                    }
                    if (VkToView.TryGetValue((int)data.vkCode, out int view) && view >= 0 && view < 80)
                    {
                        KeyCounts[view]++;
                        try { KeyPositionPressed?.Invoke(view); } catch { }
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Stop()
        {
            _running = false;
            if (_hookThreadId != 0) PostThreadMessage(_hookThreadId, 0x0012 /*WM_QUIT*/, IntPtr.Zero, IntPtr.Zero);
        }
        public void Dispose() => Stop();
    }
}
