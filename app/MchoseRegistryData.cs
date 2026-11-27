using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Mot muc registry MCHOSE — ho CZ (tu tinh/Hall-effect), sinh tu
    // devices.json trich tu MCHOSE M HUB (cardList.json tren CDN cua hang
    // + bang ALL_DEVICE_DATA trong SDK CZ dung chung).
    //  - PidMain: PID kenh ban phim (identity chinh, usagePage 0x0001).
    //  - PidVendor: PID kenh vendor (usagePage 0xFF00 voi GLW, 0xFF80 voi
    //    QHW) — thuong la PidMain + 1 (PID boot/vendor theo cardList).
    // Khong model MCHOSE nao co man hinh — HasScreen luon false.
    // =====================================================================
    public sealed class MchoseCzEntry
    {
        public string Name;
        public string Stack;           // "GLW" hoac "QHW"
        public int Vid;
        public int PidMain;
        public int PidVendor;
        public int VendorUsagePage;    // 0xFF00 (GLW) / 0xFF80 (QHW)

        public string Brand => "MCHOSE";
        public bool IsGlw => Stack == "GLW";
        public bool IsQhw => Stack == "QHW";
        public string DisplayName => Name.StartsWith(Brand, System.StringComparison.OrdinalIgnoreCase)
            ? Name : Brand + " " + Name;

        public MchoseCzEntry(string name, string stack, int vid, int pidMain, int pidVendor, int vendorUsagePage)
        {
            Name = name; Stack = stack; Vid = vid;
            PidMain = pidMain; PidVendor = pidVendor; VendorUsagePage = vendorUsagePage;
        }
    }

    // =====================================================================
    // Mot muc registry MCHOSE — ho BY (co). Ho BY chinh la ho kb_by_v2
    // (Driver Pack 1): cac model nay dang ky vao cung xu ly ho
    // kb_by_v2 trong ModelDatabase. Dinh danh cua hang dua vao
    // productName USB (getDevName) vi nhieu model dung chung mot PID
    // (258A:010C dung cho 8 model MCHOSE); registry kb_by_v2 KHONG co
    // UUID/chu ky cho bat ky model MCHOSE BY nao -> tat ca CHI XEM
    // nhu Leobog, khong suy doan.
    // =====================================================================
    public sealed class MchoseByEntry
    {
        public string Name;
        public (int Vid, int Pid)[] Ids;

        public string Brand => "MCHOSE";
        public string DisplayName => Name.StartsWith(Brand, System.StringComparison.OrdinalIgnoreCase)
            ? Name : Brand + " " + Name;

        public MchoseByEntry(string name, (int Vid, int Pid)[] ids)
        {
            Name = name; Ids = ids;
        }
    }

    public static class MchoseRegistryData
    {
        public static readonly List<MchoseCzEntry> CzEntries = new List<MchoseCzEntry>
        {
            new MchoseCzEntry("Ace 60", "GLW", 0x41E4, 0x2101, 0x2102, 0xFF00),
            new MchoseCzEntry("Ace 60 Pro", "GLW", 0x41E4, 0x2103, 0x2104, 0xFF00),
            new MchoseCzEntry("Ace 60 Pro ISO FR", "GLW", 0x3837, 0x3040, 0x3041, 0xFF00),
            new MchoseCzEntry("Ace 60 Pro Nordic", "GLW", 0x3837, 0x3002, 0x3022, 0xFF00),
            new MchoseCzEntry("Ace 60X I", "GLW", 0x41E4, 0x2126, 0x2127, 0xFF00),
            new MchoseCzEntry("Ace 60X II", "GLW", 0x41E4, 0x2112, 0x2113, 0xFF00),
            new MchoseCzEntry("Ace 68 I", "GLW", 0x41E4, 0x2114, 0x2115, 0xFF00),
            new MchoseCzEntry("Ace 68 Pro", "GLW", 0x41E4, 0x2116, 0x2117, 0xFF00),
            new MchoseCzEntry("Ace 68 III", "GLW", 0x3837, 0x3003, 0x302A, 0xFF00),
            new MchoseCzEntry("Ace 68 Air II", "GLW", 0x41E4, 0x2120, 0x2121, 0xFF00),
            new MchoseCzEntry("Ace 68 Air III", "GLW", 0x41E4, 0x2132, 0x2133, 0xFF00),
            new MchoseCzEntry("Ace 68 Air 2", "GLW", 0x3837, 0x300A, 0x300B, 0xFF00),
            new MchoseCzEntry("Ace 68 GT", "GLW", 0x3837, 0x3007, 0x3009, 0xFF00),
            new MchoseCzEntry("Ace 68 Turbo 16K", "GLW", 0x3837, 0x3026, 0x3027, 0xFF00),
            new MchoseCzEntry("Ace 68 Turbo 8K", "GLW", 0x3837, 0x3028, 0x3029, 0xFF00),
            new MchoseCzEntry("Ace 68 V2 III", "GLW", 0x3837, 0x3024, 0x3025, 0xFF00),
            new MchoseCzEntry("Ace 75 8K", "GLW", 0x3837, 0x303C, 0x303D, 0xFF00),
            new MchoseCzEntry("G75 V2", "GLW", 0x3837, 0x2021, 0x2022, 0xFF00),
            new MchoseCzEntry("G87 V2", "GLW", 0x3837, 0x2015, 0x2019, 0xFF00),
            new MchoseCzEntry("G87 V3", "QHW", 0x3837, 0x2029, 0x2029, 0xFF80),
            new MchoseCzEntry("G98 V3", "QHW", 0x3837, 0x2025, 0x2025, 0xFF80),
            new MchoseCzEntry("God 60", "GLW", 0x3837, 0x3020, 0x301A, 0xFF00),
            new MchoseCzEntry("GT 75", "GLW", 0x3837, 0x302D, 0x302F, 0xFF00),
            new MchoseCzEntry("Jet 75 I", "GLW", 0x41E4, 0x2118, 0x2119, 0xFF00),
            new MchoseCzEntry("Jet 75 II", "GLW", 0x41E4, 0x211A, 0x211B, 0xFF00),
            new MchoseCzEntry("K87 V3", "QHW", 0x3837, 0x2023, 0x2023, 0xFF80),
            new MchoseCzEntry("K87S", "GLW", 0x3837, 0x200E, 0x200F, 0xFF00),
            new MchoseCzEntry("K99 V3", "GLW", 0x3837, 0x2020, 0x201F, 0xFF00),
            new MchoseCzEntry("Mix 87 I", "GLW", 0x41E4, 0x2122, 0x2123, 0xFF00),
            new MchoseCzEntry("Mix 87 III", "GLW", 0x3837, 0x300D, 0x302C, 0xFF00),
            new MchoseCzEntry("Zero75X", "GLW", 0x41E4, 0x211C, 0x211D, 0xFF00),
        };

        public static readonly List<MchoseByEntry> ByEntries = new List<MchoseByEntry>
        {
            new MchoseByEntry("G87", new[] { (0x41E4, 0x2001), (0x41E4, 0x2201) }),
            new MchoseByEntry("G87 V2 (1K)", new[] { (0x3837, 0x201A), (0x3837, 0x201B) }),
            new MchoseByEntry("K99", new[] { (0x05AC, 0x024F) }),
            new MchoseByEntry("K99 V2", new[] { (0x258A, 0x010C), (0x3554, 0xFA09) }),
            new MchoseByEntry("G98", new[] { (0x258A, 0x010C), (0x3554, 0xFA09) }),
            new MchoseByEntry("G98 Pro", new[] { (0x258A, 0x01B9), (0x258A, 0x01BB) }),
            new MchoseByEntry("G98 V2", new[] { (0x41E4, 0x2203), (0x41E4, 0x2004) }),
            new MchoseByEntry("UT98", new[] { (0x41E4, 0x2203), (0x41E4, 0x2004) }),
            new MchoseByEntry("X75", new[] { (0x258A, 0x010C), (0x3554, 0xFA09) }),
            new MchoseByEntry("X75 V2", new[] { (0x258A, 0x010C), (0x3554, 0xFA09) }),
            new MchoseByEntry("GX87", new[] { (0x258A, 0x010C), (0x3554, 0xFA09) }),
            new MchoseByEntry("GX87 V2", new[] { (0x258A, 0x010C), (0x3554, 0xFA09) }),
            new MchoseByEntry("K87", new[] { (0x05AC, 0x024F), (0x3554, 0xFA09) }),
            new MchoseByEntry("KX75", new[] { (0x258A, 0x010C) }),
            new MchoseByEntry("G75 Pro", new[] { (0x258A, 0x010C), (0x3554, 0xFA09) }),
            new MchoseByEntry("G75 V2 (1K, BY variant)", new[] { (0x3837, 0x3016), (0x41E4, 0x2001) }),
            new MchoseByEntry("Z75", new[] { (0x258A, 0x01B9), (0x258A, 0x01BB) }),
            new MchoseByEntry("Z75S", new[] { (0x258A, 0x01B9), (0x258A, 0x01BB) }),
        };

        /// <summary>Tra muc CZ theo VID:PID (khop ca PID chinh lan PID vendor/boot).</summary>
        public static MchoseCzEntry MatchCz(int vid, int pid)
        {
            foreach (var e in CzEntries)
                if (e.Vid == vid && (e.PidMain == pid || e.PidVendor == pid)) return e;
            return null;
        }
    }
}
