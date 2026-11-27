using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Mot muc registry AULA Hub — sinh tu devices.json trich tu client
    // AULA Hub 1.1.22 + web hub (docs/PROTOCOLS-AULA-HUB.md, 114 muc ban
    // phim). Ban nay CHI gom 3 ho chua co o cac Driver Pack truoc:
    //  HFD (49), SparkLink V1 (6), SparkLink V2 (30) = 85 muc.
    // Cac ho da co (RLW = WIN60 HE Pack 4, Bytech HE = HERO68 Pack 4,
    // BPTK/Sinowealth Pack 1, nhom web F75 Max = loi app) KHONG lap lai.
    // ScreenNote: nguyen van truong screen trong devices.json
    //  ("dot-matrix", "dot-matrix (Decorate1)", "TFT 240x135" hoac "").
    // =====================================================================
    public sealed class AulaHubEntry
    {
        public string Name;
        public string Family;      // HFD | SparkLinkV1 | SparkLinkV2
        public int Vid, Pid;
        public int UsagePage, Usage;
        public string Connection;  // wired | 2.4G dongle
        public bool HasHE, HasPerKey, HasStream;
        public string ScreenNote;  // "" = khong man hinh

        public string Brand => "AULA";
        public bool HasScreen => ScreenNote.Length > 0;
        public string DisplayName => Name.StartsWith(Brand, System.StringComparison.OrdinalIgnoreCase)
            || Name.StartsWith("Epomaker", System.StringComparison.OrdinalIgnoreCase)
            ? Name : Brand + " " + Name;

        public AulaHubEntry(string name, string family, int vid, int pid, int usagePage, int usage,
            string connection, bool hasHE, bool hasPerKey, bool hasStream, string screenNote)
        {
            Name = name; Family = family; Vid = vid; Pid = pid; UsagePage = usagePage; Usage = usage;
            Connection = connection; HasHE = hasHE; HasPerKey = hasPerKey; HasStream = hasStream;
            ScreenNote = screenNote;
        }
    }

    public static class AulaHubRegistryData
    {
        public static readonly List<AulaHubEntry> Entries = new List<AulaHubEntry>
        {
            new AulaHubEntry("8K Wireless Receiver", "HFD", 0x0C45, 0xFEF9, 0xFF60, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA F108Pro V2", "HFD", 0x38A6, 0x271A, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F108Pro V2 Dongle", "HFD", 0x0C45, 0xFEFE, 0xFF68, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA F108ProV2", "HFD", 0x38A6, 0x2739, 0xFF68, 97, "wired", true, true, true, "dot-matrix"),
            new AulaHubEntry("AULA F108ProV2C", "HFD", 0x38A6, 0x273A, 0xFF68, 97, "wired", true, true, true, "dot-matrix"),
            new AulaHubEntry("AULA F108ProV2C", "HFD", 0x38A6, 0x2749, 0xFF68, 97, "wired", true, true, true, "dot-matrix"),
            new AulaHubEntry("AULA F108ProV2C Dongle", "HFD", 0x0C45, 0xFEFC, 0xFF68, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA F75 HE", "HFD", 0x38A6, 0x2729, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F75 HE", "HFD", 0x38A6, 0x2742, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F75 HE Dongle", "HFD", 0x0C45, 0xFEFE, 0xFF60, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA F75 V2", "HFD", 0x38A6, 0x2768, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F75 V2 Dongle", "HFD", 0x0C45, 0xFEFC, 0xFF68, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2C", "HFD", 0x38A6, 0x2712, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2C", "HFD", 0x38A6, 0x2738, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2C", "HFD", 0x38A6, 0x2747, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2C Dongle", "HFD", 0x0C45, 0xFEFC, 0xFF68, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2D", "HFD", 0x38A6, 0x2711, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2D", "HFD", 0x38A6, 0x2737, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2D", "HFD", 0x38A6, 0x2746, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2D  Dongle", "HFD", 0x0C45, 0xFEFE, 0xFF68, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2D Dongle", "HFD", 0x0C45, 0xFEFE, 0xFF68, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2Z", "HFD", 0x38A6, 0x2710, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2Z", "HFD", 0x38A6, 0x2736, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2Z", "HFD", 0x38A6, 0x2748, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA F87ProV2Z Dongle", "HFD", 0x0C45, 0xFEFE, 0xFF68, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA F87S 8K", "HFD", 0x38A6, 0x2908, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA NOVA75 Pro", "HFD", 0x38A6, 0x275C, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA NOVA75 Pro", "HFD", 0x0C45, 0xFEFC, 0xFF68, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA NOVA87", "HFD", 0x38A6, 0x275A, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA NOVA87", "HFD", 0x38A6, 0x275E, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("AULA NOVA87 Dongle", "HFD", 0x0C45, 0xFEFC, 0xFF68, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("AULA NOVA98", "HFD", 0x38A6, 0x273B, 0xFF68, 97, "wired", true, true, true, "TFT 240x135"),
            new AulaHubEntry("AULA NOVA98", "HFD", 0x38A6, 0x275D, 0xFF68, 97, "wired", true, true, true, "TFT 240x135"),
            new AulaHubEntry("AULA NOVA98 Dongle", "HFD", 0x0C45, 0xFEFC, 0xFF80, 113, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("Epomaker x Aula F75 V2", "HFD", 0x38A6, 0x273F, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("F75 Master", "HFD", 0x38A6, 0x2758, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("F75 Master Dongle", "HFD", 0x0C45, 0xFEF9, 0xFF60, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("F75 V2 Dongle", "HFD", 0x0C45, 0xFEFC, 0xFF68, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("MINI 60 HE", "HFD", 0x0C45, 0x8032, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("MINI 60 HE PRO", "HFD", 0x0C45, 0x80A2, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("MINI 60 HE PRO", "HFD", 0x0C45, 0x80B2, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("MINI 60 HE PRO Dongle", "HFD", 0x0C45, 0xFEFE, 0xFF60, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("MINI 68 HE", "HFD", 0x38A6, 0x2722, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("MINI 68 HE PRO", "HFD", 0x38A6, 0x2723, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("MINI 68 HE PRO Dongle", "HFD", 0x0C45, 0xFEFE, 0xFF60, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("MINI60HE MAX", "HFD", 0x0C45, 0x80A1, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("MINI60HE MAX Dongle", "HFD", 0x0C45, 0xFEFC, 0xFF80, 1, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("MINI68 HE MAX", "HFD", 0x38A6, 0x2724, 0xFF68, 97, "wired", true, true, true, ""),
            new AulaHubEntry("MINI68HE MAX Dongle", "HFD", 0x0C45, 0xFEFC, 0xFF60, 97, "2.4G dongle", true, true, true, ""),
            new AulaHubEntry("HERO 68 HE PRO", "SparkLinkV1", 0x1CA5, 0x0409, 0xFFA0, 1, "wired", true, true, false, ""),
            new AulaHubEntry("HERO 68 HE ULTRA", "SparkLinkV1", 0x1CA5, 0x0407, 0xFFA0, 1, "wired", true, true, false, ""),
            new AulaHubEntry("WIN 60 HE MAX", "SparkLinkV1", 0x1CA2, 0x1902, 0xFFA0, 1, "wired", true, true, false, ""),
            new AulaHubEntry("WIN 60 HE PRO", "SparkLinkV1", 0x1CA2, 0x1902, 0xFFA0, 1, "wired", true, true, false, ""),
            new AulaHubEntry("WIN 68 HE MAX", "SparkLinkV1", 0x1CA2, 0x1901, 0xFFA0, 1, "wired", true, true, false, ""),
            new AulaHubEntry("WIN 68 HE PRO", "SparkLinkV1", 0x1CA2, 0x1901, 0xFFA0, 1, "wired", true, true, false, ""),
            new AulaHubEntry("AG60", "SparkLinkV2", 0x38A6, 0x28D0, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG60 MAX", "SparkLinkV2", 0x38A6, 0x28D2, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG60 MAX", "SparkLinkV2", 0x38A6, 0x28DD, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG60 PRO", "SparkLinkV2", 0x38A6, 0x28D1, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG60 PRO", "SparkLinkV2", 0x38A6, 0x28DC, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG63", "SparkLinkV2", 0x38A6, 0x272D, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG63 PRO", "SparkLinkV2", 0x38A6, 0x272C, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG63 PRO", "SparkLinkV2", 0x1A86, 0xFE81, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG63 pro", "SparkLinkV2", 0x38A6, 0x272C, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG63 pro", "SparkLinkV2", 0x1A86, 0xFE81, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG75", "SparkLinkV2", 0x38A6, 0x28DF, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG75", "SparkLinkV2", 0x38A6, 0x28F0, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG75 MAX", "SparkLinkV2", 0x38A6, 0x28E1, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG75 MAX", "SparkLinkV2", 0x38A6, 0x28F2, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG75 PRO", "SparkLinkV2", 0x38A6, 0x28E0, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG75 PRO", "SparkLinkV2", 0x38A6, 0x28F1, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG75 Ultra", "SparkLinkV2", 0x38A6, 0x28E1, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AG75 Ultra", "SparkLinkV2", 0x38A6, 0x28F2, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AULA 60", "SparkLinkV2", 0x38A6, 0x2902, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AULA 66", "SparkLinkV2", 0x38A6, 0x2903, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("AULA60", "SparkLinkV2", 0x38A6, 0x2902, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("BOX 63", "SparkLinkV2", 0x38A6, 0x2730, 0xFFB0, 1, "wired", true, true, true, "dot-matrix (Decorate1)"),
            new AulaHubEntry("BOX 63", "SparkLinkV2", 0x38A6, 0x2731, 0xFFB0, 1, "wired", true, true, true, "dot-matrix (Decorate1)"),
            new AulaHubEntry("BOX 63MAX", "SparkLinkV2", 0x38A6, 0x2731, 0xFFB0, 1, "wired", true, true, true, "dot-matrix (Decorate1)"),
            new AulaHubEntry("BOX 63MAX", "SparkLinkV2", 0x38A6, 0x2767, 0xFFB0, 1, "wired", true, true, true, "dot-matrix (Decorate1)"),
            new AulaHubEntry("F68RX", "SparkLinkV2", 0x38A6, 0x272E, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("F68rx", "SparkLinkV2", 0x38A6, 0x272E, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("GALAXY 600", "SparkLinkV2", 0x38A6, 0x2904, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("WIN 60 EVO", "SparkLinkV2", 0x38A6, 0x290A, 0xFFB0, 1, "wired", true, true, true, ""),
            new AulaHubEntry("WIN 68 EVO", "SparkLinkV2", 0x38A6, 0x2909, 0xFFB0, 1, "wired", true, true, true, ""),
        };

        /// <summary>Tra cac muc khop VID:PID + usage page (mot cap VID:PID
        /// co the thuoc nhieu ho khac nhau — usage page moi quyet dinh).</summary>
        public static List<AulaHubEntry> Match(int vid, int pid, int usagePage)
        {
            var found = new List<AulaHubEntry>();
            foreach (var e in Entries)
                if (e.Vid == vid && e.Pid == pid && e.UsagePage == usagePage) found.Add(e);
            return found;
        }
    }
}
