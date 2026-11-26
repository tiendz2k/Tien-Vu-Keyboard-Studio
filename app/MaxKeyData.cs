using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // Bang phim AULA F75 Max — trich truc tiep tu layouts/rgb-keyboard.xml cua
    // driver chinh hang (80 phim, 79 light_index duy nhat).
    // QUIRK DA BIET: Shift_R va Alt_R CUNG mang light_index 0x54 trong XML cua
    // hang — gan nhu chac chan la loi du lieu cua hang; ca hai phim deu duoc
    // giu trong bang (moi phim mot o stream rieng), chi giai quyet dut diem
    // khi test tren phan cung that.
    public static class MaxKeyData
    {
        // Wire id theo thu tu tao phim cua KeyboardView (hang 0..5, trai->phai).
        public static readonly byte[] ViewKeyWireIds =
        {
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C,
            0x0D, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D,
            0x1E, 0x1F, 0x67, 0x77, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C,
            0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x43, 0x76, 0x37, 0x38, 0x39, 0x3A, 0x3B,
            0x3C, 0x3D, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x55, 0x79, 0x49, 0x4A, 0x4B,
            0x4C, 0x4D, 0x4E, 0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x65, 0x78, 0x5B,
            0x5C, 0x5D, 0x5E, 0x54, 0x60, 0x63, 0x64, 0x66,
        };

        // Ten phim theo cung thu tu tren (de log/debug).
        public static readonly string[] ViewKeyNames =
        {
            "Esc", "F1", "F2", "F3", "F4", "F5",
            "F6", "F7", "F8", "F9", "F10", "F11",
            "F12", "~`", "!1", "@2", "#3", "$4",
            "%5", "^6", "&7", "*8", "(9", ")0",
            "_-", "+=", "Backspace", "delete", "Tab", "Q",
            "W", "E", "R", "T", "Y", "U",
            "I", "O", "P", "{[", "}}", "|\\",
            "page up", "CapsLock", "A", "S", "D", "F",
            "G", "H", "J", "K", "L", ":;",
            "\"'", "Enter", "page down", "Shift_L", "Z", "X",
            "C", "V", "B", "N", "M", ",<",
            ".>", "/?", "Shift_R", "↑", "end", "Ctrl_L",
            "Win_L", "Alt_L", "Space", "Alt_R", "Fn", "←",
            "↓", "→",
        };

        // Tap 79 id hop le cua F75 Max (dung de validate khi doc/ghi per-key).
        public static readonly HashSet<byte> KeyIds = new HashSet<byte>
        {
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C,
            0x0D, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D,
            0x1E, 0x1F, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E,
            0x2F, 0x30, 0x31, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F,
            0x40, 0x41, 0x42, 0x43, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F, 0x50,
            0x51, 0x52, 0x53, 0x54, 0x55, 0x5B, 0x5C, 0x5D, 0x5E, 0x60, 0x63, 0x64,
            0x65, 0x66, 0x67, 0x76, 0x77, 0x78, 0x79,
        };

        // Thu tu stream 0x20 (gia thuyet da tai lieu hoa: sap xep rect theo
        // (top,left) — dung phuong phap Graham da kiem chung tren L99).
        // Moi o: wire id + toa do tam rect chuan hoa 0..1 theo vung phim
        // (left 90..707, top 28..267 trong XML) + index phim tren KeyboardView.
        public struct StreamSlot { public byte Id; public double CX, CY; public int ViewIndex; }
        public static readonly StreamSlot[] StreamSlots =
        {
            new StreamSlot { Id = 0x01, CX = 0.0259, CY = 0.0628, ViewIndex = 0 }, // Esc
            new StreamSlot { Id = 0x02, CX = 0.1021, CY = 0.0628, ViewIndex = 1 }, // F1
            new StreamSlot { Id = 0x03, CX = 0.1653, CY = 0.0628, ViewIndex = 2 }, // F2
            new StreamSlot { Id = 0x04, CX = 0.2285, CY = 0.0628, ViewIndex = 3 }, // F3
            new StreamSlot { Id = 0x05, CX = 0.2917, CY = 0.0628, ViewIndex = 4 }, // F4
            new StreamSlot { Id = 0x06, CX = 0.3679, CY = 0.0628, ViewIndex = 5 }, // F5
            new StreamSlot { Id = 0x07, CX = 0.4311, CY = 0.0628, ViewIndex = 6 }, // F6
            new StreamSlot { Id = 0x08, CX = 0.4943, CY = 0.0628, ViewIndex = 7 }, // F7
            new StreamSlot { Id = 0x09, CX = 0.5575, CY = 0.0628, ViewIndex = 8 }, // F8
            new StreamSlot { Id = 0x0A, CX = 0.6337, CY = 0.0628, ViewIndex = 9 }, // F9
            new StreamSlot { Id = 0x0B, CX = 0.6969, CY = 0.0628, ViewIndex = 10 }, // F10
            new StreamSlot { Id = 0x0C, CX = 0.7601, CY = 0.0628, ViewIndex = 11 }, // F11
            new StreamSlot { Id = 0x0D, CX = 0.8233, CY = 0.0628, ViewIndex = 12 }, // F12
            new StreamSlot { Id = 0x13, CX = 0.0259, CY = 0.2845, ViewIndex = 13 }, // ~`
            new StreamSlot { Id = 0x14, CX = 0.0891, CY = 0.2845, ViewIndex = 14 }, // !1
            new StreamSlot { Id = 0x15, CX = 0.1524, CY = 0.2845, ViewIndex = 15 }, // @2
            new StreamSlot { Id = 0x16, CX = 0.2156, CY = 0.2845, ViewIndex = 16 }, // #3
            new StreamSlot { Id = 0x17, CX = 0.2788, CY = 0.2845, ViewIndex = 17 }, // $4
            new StreamSlot { Id = 0x18, CX = 0.3420, CY = 0.2845, ViewIndex = 18 }, // %5
            new StreamSlot { Id = 0x19, CX = 0.4052, CY = 0.2845, ViewIndex = 19 }, // ^6
            new StreamSlot { Id = 0x1A, CX = 0.4700, CY = 0.2845, ViewIndex = 20 }, // &7
            new StreamSlot { Id = 0x1B, CX = 0.5332, CY = 0.2845, ViewIndex = 21 }, // *8
            new StreamSlot { Id = 0x1C, CX = 0.5964, CY = 0.2845, ViewIndex = 22 }, // (9
            new StreamSlot { Id = 0x1D, CX = 0.6596, CY = 0.2845, ViewIndex = 23 }, // )0
            new StreamSlot { Id = 0x1E, CX = 0.7229, CY = 0.2845, ViewIndex = 24 }, // _-
            new StreamSlot { Id = 0x1F, CX = 0.7861, CY = 0.2845, ViewIndex = 25 }, // +=
            new StreamSlot { Id = 0x67, CX = 0.8809, CY = 0.2845, ViewIndex = 26 }, // Backspace
            new StreamSlot { Id = 0x77, CX = 0.9741, CY = 0.2845, ViewIndex = 27 }, // delete
            new StreamSlot { Id = 0x25, CX = 0.0413, CY = 0.4435, ViewIndex = 28 }, // Tab
            new StreamSlot { Id = 0x26, CX = 0.1199, CY = 0.4435, ViewIndex = 29 }, // Q
            new StreamSlot { Id = 0x27, CX = 0.1831, CY = 0.4435, ViewIndex = 30 }, // W
            new StreamSlot { Id = 0x28, CX = 0.2464, CY = 0.4435, ViewIndex = 31 }, // E
            new StreamSlot { Id = 0x29, CX = 0.3096, CY = 0.4435, ViewIndex = 32 }, // R
            new StreamSlot { Id = 0x2A, CX = 0.3728, CY = 0.4435, ViewIndex = 33 }, // T
            new StreamSlot { Id = 0x2B, CX = 0.4376, CY = 0.4435, ViewIndex = 34 }, // Y
            new StreamSlot { Id = 0x2C, CX = 0.5008, CY = 0.4435, ViewIndex = 35 }, // U
            new StreamSlot { Id = 0x2D, CX = 0.5640, CY = 0.4435, ViewIndex = 36 }, // I
            new StreamSlot { Id = 0x2E, CX = 0.6272, CY = 0.4435, ViewIndex = 37 }, // O
            new StreamSlot { Id = 0x2F, CX = 0.6904, CY = 0.4435, ViewIndex = 38 }, // P
            new StreamSlot { Id = 0x30, CX = 0.7536, CY = 0.4435, ViewIndex = 39 }, // {[
            new StreamSlot { Id = 0x31, CX = 0.8169, CY = 0.4435, ViewIndex = 40 }, // }}
            new StreamSlot { Id = 0x43, CX = 0.8971, CY = 0.4435, ViewIndex = 41 }, // |\
            new StreamSlot { Id = 0x76, CX = 0.9741, CY = 0.4435, ViewIndex = 42 }, // page up
            new StreamSlot { Id = 0x37, CX = 0.0486, CY = 0.6109, ViewIndex = 43 }, // CapsLock
            new StreamSlot { Id = 0x38, CX = 0.1361, CY = 0.6109, ViewIndex = 44 }, // A
            new StreamSlot { Id = 0x39, CX = 0.1994, CY = 0.6109, ViewIndex = 45 }, // S
            new StreamSlot { Id = 0x3A, CX = 0.2626, CY = 0.6109, ViewIndex = 46 }, // D
            new StreamSlot { Id = 0x3B, CX = 0.3258, CY = 0.6109, ViewIndex = 47 }, // F
            new StreamSlot { Id = 0x3C, CX = 0.3906, CY = 0.6109, ViewIndex = 48 }, // G
            new StreamSlot { Id = 0x3D, CX = 0.4538, CY = 0.6109, ViewIndex = 49 }, // H
            new StreamSlot { Id = 0x3E, CX = 0.5170, CY = 0.6109, ViewIndex = 50 }, // J
            new StreamSlot { Id = 0x3F, CX = 0.5802, CY = 0.6109, ViewIndex = 51 }, // K
            new StreamSlot { Id = 0x40, CX = 0.6434, CY = 0.6109, ViewIndex = 52 }, // L
            new StreamSlot { Id = 0x41, CX = 0.7066, CY = 0.6109, ViewIndex = 53 }, // :;
            new StreamSlot { Id = 0x42, CX = 0.7699, CY = 0.6109, ViewIndex = 54 }, // "'
            new StreamSlot { Id = 0x55, CX = 0.8728, CY = 0.6109, ViewIndex = 55 }, // Enter
            new StreamSlot { Id = 0x79, CX = 0.9741, CY = 0.6109, ViewIndex = 56 }, // page down
            new StreamSlot { Id = 0x49, CX = 0.0648, CY = 0.7699, ViewIndex = 57 }, // Shift_L
            new StreamSlot { Id = 0x4A, CX = 0.1669, CY = 0.7699, ViewIndex = 58 }, // Z
            new StreamSlot { Id = 0x4B, CX = 0.2301, CY = 0.7699, ViewIndex = 59 }, // X
            new StreamSlot { Id = 0x4C, CX = 0.2934, CY = 0.7699, ViewIndex = 60 }, // C
            new StreamSlot { Id = 0x4D, CX = 0.3582, CY = 0.7699, ViewIndex = 61 }, // V
            new StreamSlot { Id = 0x4E, CX = 0.4214, CY = 0.7699, ViewIndex = 62 }, // B
            new StreamSlot { Id = 0x4F, CX = 0.4846, CY = 0.7699, ViewIndex = 63 }, // N
            new StreamSlot { Id = 0x50, CX = 0.5478, CY = 0.7699, ViewIndex = 64 }, // M
            new StreamSlot { Id = 0x51, CX = 0.6110, CY = 0.7699, ViewIndex = 65 }, // ,<
            new StreamSlot { Id = 0x52, CX = 0.6742, CY = 0.7699, ViewIndex = 66 }, // .>
            new StreamSlot { Id = 0x53, CX = 0.7391, CY = 0.7699, ViewIndex = 67 }, // /?
            new StreamSlot { Id = 0x54, CX = 0.8250, CY = 0.7699, ViewIndex = 68 }, // Shift_R
            new StreamSlot { Id = 0x65, CX = 0.9125, CY = 0.7699, ViewIndex = 69 }, // ↑
            new StreamSlot { Id = 0x78, CX = 0.9741, CY = 0.7699, ViewIndex = 70 }, // end
            new StreamSlot { Id = 0x5B, CX = 0.0340, CY = 0.9372, ViewIndex = 71 }, // Ctrl_L
            new StreamSlot { Id = 0x5C, CX = 0.1118, CY = 0.9372, ViewIndex = 72 }, // Win_L
            new StreamSlot { Id = 0x5D, CX = 0.1896, CY = 0.9372, ViewIndex = 73 }, // Alt_L
            new StreamSlot { Id = 0x5E, CX = 0.4295, CY = 0.9372, ViewIndex = 74 }, // Space
            new StreamSlot { Id = 0x54, CX = 0.6677, CY = 0.9372, ViewIndex = 75 }, // Alt_R
            new StreamSlot { Id = 0x60, CX = 0.7472, CY = 0.9372, ViewIndex = 76 }, // Fn
            new StreamSlot { Id = 0x63, CX = 0.8493, CY = 0.9372, ViewIndex = 77 }, // ←
            new StreamSlot { Id = 0x64, CX = 0.9125, CY = 0.9372, ViewIndex = 78 }, // ↓
            new StreamSlot { Id = 0x66, CX = 0.9741, CY = 0.9372, ViewIndex = 79 }, // →
        };
    }
}
