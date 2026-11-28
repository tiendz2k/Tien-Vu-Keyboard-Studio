using System;
using System.Runtime.InteropServices;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.15 — Chup man hinh chinh bang GDI thuan P/Invoke (Windows):
    // GetDC -> StretchBlt downscale thang xuong bitmap nho ngay o tang
    // chup (driver lam viec thu nho) -> GetDIBits lay buffer BGRA cho
    // ScreenSampler (AmbilightCore.cs). Khong dung System.Drawing,
    // khong DXGI, khong ffmpeg — xem DESIGN.md §1.1.
    // Chi chay duoc tren Windows; moi loi (ke ca khong phai Windows)
    // deu thanh false de Ambilight suy thoai mem. Luu y gioi han cung:
    // noi dung DRM/HDCP chup ra khung DEN (gioi han cua GDI).
    // =====================================================================
    public static class ScreenCapture
    {
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const uint SRCCOPY = 0x00CC0020;
        private const int HALFTONE = 4;
        private const uint DIB_RGB_COLORS = 0;

        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool StretchBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
            IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, uint rop);
        [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint lines,
            byte[] pixels, ref BITMAPINFO bmi, uint usage);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public uint[] bmiColors;
        }

        /// <summary>
        /// Chup man hinh chinh, downscale ve (targetW x targetH), tra
        /// buffer BGRA (4 byte/pixel, hang tren xuong — biHeight am).
        /// That bai -> false va bgra = null.
        /// </summary>
        public static bool CapturePrimaryBgra(int targetW, int targetH, out byte[] bgra)
        {
            bgra = null;
            if (targetW <= 0 || targetH <= 0) return false;
            IntPtr screenDc = IntPtr.Zero, memDc = IntPtr.Zero, bmp = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                int sw = GetSystemMetrics(SM_CXSCREEN);
                int sh = GetSystemMetrics(SM_CYSCREEN);
                if (sw <= 0 || sh <= 0) return false;
                screenDc = GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero) return false;
                memDc = CreateCompatibleDC(screenDc);
                bmp = CreateCompatibleBitmap(screenDc, targetW, targetH);
                if (memDc == IntPtr.Zero || bmp == IntPtr.Zero) return false;
                old = SelectObject(memDc, bmp);
                SetStretchBltMode(memDc, HALFTONE);
                if (!StretchBlt(memDc, 0, 0, targetW, targetH, screenDc, 0, 0, sw, sh, SRCCOPY))
                    return false;
                var bmi = new BITMAPINFO
                {
                    bmiHeader = new BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                        biWidth = targetW,
                        biHeight = -targetH, // am = top-down
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = 0, // BI_RGB
                    },
                    bmiColors = new uint[1],
                };
                var buf = new byte[targetW * targetH * 4];
                int lines = GetDIBits(memDc, bmp, 0, (uint)targetH, buf, ref bmi, DIB_RGB_COLORS);
                if (lines != targetH) return false;
                bgra = buf;
                return true;
            }
            catch
            {
                // Khong phai Windows / thieu DLL / loi GDI — suy thoai mem.
                return false;
            }
            finally
            {
                if (old != IntPtr.Zero && memDc != IntPtr.Zero) SelectObject(memDc, old);
                if (bmp != IntPtr.Zero) DeleteObject(bmp);
                if (memDc != IntPtr.Zero) DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }
}
