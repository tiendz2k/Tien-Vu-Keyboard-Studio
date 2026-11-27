using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Mot muc registry DareU All-in-One — sinh tu devices-dareu.json trich
    // tu WEB driver chinh hang dr.dareu.com (190 JSON cau hinh theo PID;
    // 187 muc tai duoc). Tat ca cung VID 0x260D; PID -> FwType chon ho
    // giao thuc (co che dinh danh cua chinh hang).
    //  FwType: 0 TG, 1 HS (bien the TG), 2 JM, 3 QF, 4/5 OHID, 6 TG-RT,
    //  7 dongle TG-OHID.
    // TravelVersion: EK75 RT khong khai bao -> mac dinh 1 theo ho so;
    //  cac muc TG-RT khac cung ghi 1 (mac dinh cua hang khi khong khai
    //  bao); muc khong phai TG-RT de -1 (khong ap dung).
    // =====================================================================
    public sealed class DareUEntry
    {
        public string Name;          // product_name trong JSON cua hang
        public string InternalModel; // model noi bo (vd TK51G)
        public string Type;          // keyboard | mouse | dongle
        public int FwType;
        public string Family;        // TG | HS | JM | QF | OHID | TG-RT | TG-Dongle
        public int Vid, Pid;
        public int KeyCount;         // -1 = khong ro
        public bool? HasBattery;
        public bool? HasLcd;
        public double TravelMinMm, TravelMaxMm, TravelStepMm; // 0 = khong khai bao
        public int TravelVersion;    // -1 = khong ap dung/khong ro

        public string Brand => "DareU";
        public bool IsKeyboard => Type == "keyboard";
        public bool IsHe => FwType == 6; // chi TG-RT co lop Magnetic Axis
        public string DisplayName => Name.StartsWith(Brand, System.StringComparison.OrdinalIgnoreCase)
            ? Name : Brand + " " + Name;

        public DareUEntry(string name, string internalModel, string type, int fwType, string family,
            int vid, int pid, int keyCount, bool? hasBattery, bool? hasLcd,
            double travelMinMm, double travelMaxMm, double travelStepMm, int travelVersion)
        {
            Name = name; InternalModel = internalModel; Type = type; FwType = fwType; Family = family;
            Vid = vid; Pid = pid; KeyCount = keyCount; HasBattery = hasBattery; HasLcd = hasLcd;
            TravelMinMm = travelMinMm; TravelMaxMm = travelMaxMm; TravelStepMm = travelStepMm;
            TravelVersion = travelVersion;
        }
    }

    public static class DareURegistryData
    {
        public static readonly List<DareUEntry> Entries = new List<DareUEntry>
        {
            new DareUEntry("Z82", "TK51A", "keyboard", 0, "TG", 0x260D, 0x0030, 85, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("USB 2.4G Receiver", "Dongle", "dongle", 0, "TG", 0x260D, 0x0037, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("USB 2.4G Receiver", "Dongle", "dongle", 0, "TG", 0x260D, 0x0042, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A104", "TK51QU", "keyboard", 0, "TG", 0x260D, 0x0043, 104, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EK75", "TK51G", "keyboard", 0, "TG", 0x260D, 0x0045, 83, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A98", "TK51V", "keyboard", 0, "TG", 0x260D, 0x0051, 97, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A87 PRO", "TK51CU", "keyboard", 0, "TG", 0x260D, 0x0056, 87, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EK87 PRO", "TK51CF", "keyboard", 0, "TG", 0x260D, 0x0060, 87, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CK98 PRO RT", "TK51WU", "keyboard", 4, "OHID", 0x260D, 0x0067, 100, false, false, 0.1, 4.0, 0.02, -1),
            new DareUEntry("Z82 PRO", "TK51ARGB", "keyboard", 0, "TG", 0x260D, 0x0070, 85, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EK106 PRO", "TK51QF", "keyboard", 0, "TG", 0x260D, 0x0074, 106, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EK106 RT 8K", "TK51QRT8K", "keyboard", 4, "OHID", 0x260D, 0x0075, 106, false, false, 0.1, 4.0, 0.02, -1),
            new DareUEntry("A75 PRO", "TK52L", "keyboard", 4, "OHID", 0x260D, 0x0078, 79, true, false, 0.1, 4.0, 0.02, -1),
            new DareUEntry("EK87 RT 8K", "TK51CU", "keyboard", 4, "OHID", 0x260D, 0x0081, 87, false, false, 0.1, 4.0, 0.02, -1),
            new DareUEntry("A75 HE", "TK52L", "keyboard", 4, "OHID", 0x260D, 0x0083, 79, false, false, 0.1, 3.5, 0.02, -1),
            new DareUEntry("EK106 PRO 8K", "TK51QU", "keyboard", 0, "TG", 0x260D, 0x0090, 106, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EK87 PRO 8K", "TK51CU", "keyboard", 0, "TG", 0x260D, 0x0091, 87, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EK815 PRO", "TK52K", "keyboard", 6, "TG-RT", 0x260D, 0x0092, 100, false, false, 0.3, 3.8, 0.02, 1),
            new DareUEntry("EK75", "TK51G", "keyboard", 0, "TG", 0x260D, 0x0101, 83, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CK87 MAX", "TK52U", "keyboard", 6, "TG-RT", 0x260D, 0x0103, 87, false, false, 0.2, 3.8, 0.02, 1),
            new DareUEntry("EK87RT WP", "TK51CU", "keyboard", 6, "TG-RT", 0x260D, 0x0105, 87, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("EK108 RT", "TK51Q", "keyboard", 6, "TG-RT", 0x260D, 0x0106, 108, false, false, 0.2, 3.8, 0.02, 1),
            new DareUEntry("EK75", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0107, 83, false, false, 0.3, 3.3, 0.02, 1),
            new DareUEntry("USB 2.4G Receiver", "Dongle", "dongle", 7, "TG-Dongle", 0x260D, 0x0112, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("VICTOR HE", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0113, 83, false, false, 0.3, 3.3, 0.02, 1),
            new DareUEntry("EK75", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0115, 83, false, false, 0.3, 3.3, 0.02, 1),
            new DareUEntry("COOL 60 / EK60 HE", "TK599", "keyboard", 6, "TG-RT", 0x260D, 0x0117, 61, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("COOL 60 / EK60 HE", "TK599", "keyboard", 6, "TG-RT", 0x260D, 0x0119, 61, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("COOL 60 / EK60 HE", "TK599", "keyboard", 6, "TG-RT", 0x260D, 0x0123, 61, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("CK75 PRO", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0124, 83, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("影刃", "TK52W", "keyboard", 6, "TG-RT", 0x260D, 0x0126, 68, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("COOL 60 / EK60 HE", "TK599", "keyboard", 6, "TG-RT", 0x260D, 0x0127, 61, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("CK75", "TK51D", "keyboard", 6, "TG-RT", 0x260D, 0x0129, 81, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("EK75", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0131, 83, false, false, 0.3, 3.3, 0.02, 1),
            new DareUEntry("EK75", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0132, 83, false, false, 0.3, 3.3, 0.02, 1),
            new DareUEntry("A98 RT", "TK51V", "keyboard", 6, "TG-RT", 0x260D, 0x0136, 97, true, false, 0.3, 3.8, 1.0, 1),
            new DareUEntry("A104 Master  8K", "TK51Q", "keyboard", 2, "JM", 0x260D, 0x0137, 104, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A87 Master 8K", "TK51CU", "keyboard", 2, "JM", 0x260D, 0x0138, 84, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CK100", "TK51N", "keyboard", 6, "TG-RT", 0x260D, 0x0139, 99, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("CK98 MAX", "TK50TRT", "keyboard", 6, "TG-RT", 0x260D, 0x0140, 97, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("A75 HE", "TK52L", "keyboard", 4, "OHID", 0x260D, 0x0145, 79, false, false, 0.1, 4.0, 0.02, -1),
            new DareUEntry("USB 2.4G Receiver", "TK51Q", "dongle", 2, "JM", 0x260D, 0x0146, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EK81 MAX", "TK51D", "keyboard", 0, "TG", 0x260D, 0x0150, 81, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CK75 PRO", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0152, 83, false, false, 0.3, 3.3, 0.02, 1),
            new DareUEntry("EK87 RT 8K", "TK51C", "keyboard", 4, "OHID", 0x260D, 0x0159, 87, false, false, 0.1, 4.0, 0.02, -1),
            new DareUEntry("EK106 RT 8K", "TK51Q", "keyboard", 4, "OHID", 0x260D, 0x0160, 106, false, false, 0.1, 4.0, 0.02, -1),
            new DareUEntry("COOL 98", "TK52Q", "keyboard", 0, "TG", 0x260D, 0x0161, 97, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CK75 08G", "TK51D", "keyboard", 4, "OHID", 0x260D, 0x0162, 81, false, false, 0.2, 4.0, 0.02, -1),
            new DareUEntry("DK108 PRO", "TK51Q", "keyboard", 0, "TG", 0x260D, 0x0163, 108, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CK100", "TK51N", "keyboard", 6, "TG-RT", 0x260D, 0x0164, 99, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("CK98 MAX", "TK50TRT", "keyboard", 6, "TG-RT", 0x260D, 0x0165, 97, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("CK98 Ultra", "TK52Q", "keyboard", 6, "TG-RT", 0x260D, 0x0166, 97, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("DK87 PRO", "TK529", "keyboard", 0, "TG", 0x260D, 0x0167, 87, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CK87 MAX", "TK52U", "keyboard", 6, "TG-RT", 0x260D, 0x0169, 87, false, false, 0.2, 3.8, 0.02, 1),
            new DareUEntry("EK815 MAX", "TK52K", "keyboard", 0, "TG", 0x260D, 0x0170, 100, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CK75 PRO", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0174, 80, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("A68", "TK53H", "keyboard", 6, "TG-RT", 0x260D, 0x0175, 68, false, false, 0.1, 3.3, 0.01, 1),
            new DareUEntry("FK98", "TK51V", "keyboard", 0, "TG", 0x260D, 0x0178, 97, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EK108 RT", "TK51Q", "keyboard", 6, "TG-RT", 0x260D, 0x0179, 108, false, false, 0.2, 3.8, 0.02, 1),
            new DareUEntry("EK87RT WP", "TK51C", "keyboard", 6, "TG-RT", 0x260D, 0x0180, 87, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("CK75 PRO", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0187, 80, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("猎光75", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0188, 80, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("COOL 98", "TK52Q", "keyboard", 6, "TG-RT", 0x260D, 0x0191, 97, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("CK75 PRO", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0192, 80, false, false, 0.2, 3.2, 0.02, 1),
            new DareUEntry("CK75", "TK51D", "keyboard", 6, "TG-RT", 0x260D, 0x0193, 81, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("CK98 Ultra", "TK52Q", "keyboard", 6, "TG-RT", 0x260D, 0x0194, 97, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("CK98 Ultra", "TK52Q", "keyboard", 6, "TG-RT", 0x260D, 0x0195, 97, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("CK98 Ultra", "TK52Q", "keyboard", 6, "TG-RT", 0x260D, 0x0196, 97, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("COOL 60 / EK60 HE", "TK599", "keyboard", 6, "TG-RT", 0x260D, 0x0198, 61, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("COOL 60 / EK60 HE", "TK599", "keyboard", 6, "TG-RT", 0x260D, 0x0199, 61, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("CK75", "TK51D", "keyboard", 6, "TG-RT", 0x260D, 0x0201, 81, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("FLEX 75", "TK53N", "keyboard", 0, "TG", 0x260D, 0x0202, 82, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CK100", "TK51N", "keyboard", 6, "TG-RT", 0x260D, 0x0205, 99, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("A104PRO8K V2", "TK51QU", "keyboard", 0, "TG", 0x260D, 0x0206, 104, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CK98 MAX", "TK50TRT", "keyboard", 6, "TG-RT", 0x260D, 0x0208, 97, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("CK98 Ultra", "TK52Q", "keyboard", 6, "TG-RT", 0x260D, 0x0209, 97, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("FLEX 87", "TK53P", "keyboard", 1, "HS", 0x260D, 0x0210, 87, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("USB 2.4G Receiver", "Dongle", "dongle", 1, "HS", 0x260D, 0x0211, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("COOL 60 / EK60 HE", "TK599", "keyboard", 6, "TG-RT", 0x260D, 0x0214, 61, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("CK98 Ultra", "TK52Q", "keyboard", 6, "TG-RT", 0x260D, 0x0216, 97, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("CK75", "TK51D", "keyboard", 6, "TG-RT", 0x260D, 0x0223, 81, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("CK100", "TK51N", "keyboard", 6, "TG-RT", 0x260D, 0x0224, 99, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("COOL 60 / EK60 HE", "TK599", "keyboard", 6, "TG-RT", 0x260D, 0x0229, 61, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("CK75", "TK51D", "keyboard", 6, "TG-RT", 0x260D, 0x0230, 81, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("CK75", "TK51D", "keyboard", 6, "TG-RT", 0x260D, 0x0232, 81, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("Flex 98", "TK53M", "keyboard", 1, "HS", 0x260D, 0x0233, 98, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("FLEX 75", "TK53N", "keyboard", 6, "TG-RT", 0x260D, 0x0234, 79, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("影刃75", "TK53Q", "keyboard", 6, "TG-RT", 0x260D, 0x0236, 79, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("EK87RT WP", "TK51C", "keyboard", 6, "TG-RT", 0x260D, 0x0237, 87, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("EK108 RT", "TK51Q", "keyboard", 6, "TG-RT", 0x260D, 0x0238, 108, false, false, 0.2, 3.8, 0.02, 1),
            new DareUEntry("猎光75", "TK51G", "keyboard", 6, "TG-RT", 0x260D, 0x0239, 80, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("CK87 MAX", "TK52U", "keyboard", 6, "TG-RT", 0x260D, 0x0241, 87, false, false, 0.2, 3.8, 0.02, 1),
            new DareUEntry("冰刃75", "TK53V", "keyboard", 6, "TG-RT", 0x260D, 0x0244, 81, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("锋刃 75", "TK50G", "keyboard", 6, "TG-RT", 0x260D, 0x0246, 84, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("CK75", "TK51D", "keyboard", 6, "TG-RT", 0x260D, 0x0249, 81, false, false, 0.2, 3.3, 0.02, 1),
            new DareUEntry("CK108 HE", "TK51Q", "keyboard", 6, "TG-RT", 0x260D, 0x0252, 108, false, false, 0.2, 3.8, 0.02, 1),
            new DareUEntry("CK87 MAX", "TK52U", "keyboard", 6, "TG-RT", 0x260D, 0x0253, 87, false, false, 0.2, 3.8, 0.02, 1),
            new DareUEntry("FLEX 75", "TK53N", "keyboard", 6, "TG-RT", 0x260D, 0x0261, 79, false, false, 0.2, 3.3, 0.01, 1),
            new DareUEntry("A980", "TM238", "mouse", 0, "TG", 0x260D, 0x1045, 11, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM243FDongle", "dongle", 2, "JM", 0x260D, 0x1074, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM610 PRO", "TM243", "mouse", 0, "TG", 0x260D, 0x1082, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950", "TM243F", "mouse", 2, "JM", 0x260D, 0x1084, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EM903", "TM230G", "mouse", 2, "JM", 0x260D, 0x1107, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM230GDongle", "dongle", 2, "JM", 0x260D, 0x1108, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 PRO", "TM265", "mouse", 2, "JM", 0x260D, 0x1112, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM265Dongle", "dongle", 2, "JM", 0x260D, 0x1114, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM265Dongle", "dongle", 2, "JM", 0x260D, 0x1115, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 PRO Mg", "TM271F", "mouse", 2, "JM", 0x260D, 0x1117, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950G", "TM265KR", "mouse", 2, "JM", 0x260D, 0x1118, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A980 PRO MAX", "TM270LCD", "mouse", 1, "HS", 0x260D, 0x1129, 9, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A980 PRO", "TM270", "mouse", 1, "HS", 0x260D, 0x1130, 9, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("Wireless Dongle", "TM270FDongle", "dongle", 1, "HS", 0x260D, 0x1131, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM610 PRO", "TM243F", "mouse", 2, "JM", 0x260D, 0x1134, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM243FDongle", "dongle", 2, "JM", 0x260D, 0x1135, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM670 PRO", "TM242F", "mouse", 2, "JM", 0x260D, 0x1136, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM610 V2", "TM265U", "mouse", 0, "TG", 0x260D, 0x1141, 8, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 AIR", "TM275F", "mouse", 1, "HS", 0x260D, 0x1147, 8, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A980 PRO", "TM270FAIM", "mouse", 1, "HS", 0x260D, 0x1148, 9, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A980 PRO MAX", "TM270FAIMLCD", "mouse", 1, "HS", 0x260D, 0x1150, 9, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A980 PRO MAX", "TM270FAIMLCD", "mouse", 1, "HS", 0x260D, 0x1151, 9, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM610 V2", "TM265U", "mouse", 0, "TG", 0x260D, 0x1152, 8, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 PRO Mg", "TM271F", "mouse", 2, "JM", 0x260D, 0x1153, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM275DGDongle", "dongle", 2, "JM", 0x260D, 0x1154, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM610 MAX", "TM243G", "mouse", 2, "JM", 0x260D, 0x1155, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM265D", "dongle", 2, "JM", 0x260D, 0x1156, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM610 V2", "TM265U", "mouse", 0, "TG", 0x260D, 0x1159, 7, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM290", "dongle", 2, "JM", 0x260D, 0x1163, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM610 V2 MAX", "TM265D", "mouse", 2, "JM", 0x260D, 0x1165, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A980 PRO", "TM270F", "mouse", 1, "HS", 0x260D, 0x1166, 9, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM610 MAX", "TM243F", "mouse", 2, "JM", 0x260D, 0x1168, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 AIR", "TM275F", "mouse", 1, "HS", 0x260D, 0x1169, 8, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 AIR", "TM275F", "mouse", 2, "JM", 0x260D, 0x1174, 5, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM275FGDongle", "dongle", 2, "JM", 0x260D, 0x1175, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM610 V2 MAX", "TM265D", "mouse", 2, "JM", 0x260D, 0x1181, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 LCD", "TM265D", "mouse", 2, "JM", 0x260D, 0x1182, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 AIR", "TM275D", "mouse", 2, "JM", 0x260D, 0x1185, 5, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 AIR", "TM275D", "mouse", 2, "JM", 0x260D, 0x1193, 5, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 Wing", "TM275D", "mouse", 2, "JM", 0x260D, 0x1196, 5, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 Ultra", "TM283", "mouse", 2, "JM", 0x260D, 0x1217, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("极风", "TM290D", "mouse", 2, "JM", 0x260D, 0x1218, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM2XX", "dongle", 2, "JM", 0x260D, 0x1219, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("羽翼", "TM287D", "mouse", 2, "JM", 0x260D, 0x1220, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE6", "TM290F", "mouse", 2, "JM", 0x260D, 0x1226, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM290F", "dongle", 2, "JM", 0x260D, 0x1227, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE6 PRO", "TM290F", "mouse", 2, "JM", 0x260D, 0x1233, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE7 PRO", "TM287F", "mouse", 2, "JM", 0x260D, 0x1235, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("极风Max", "TM290F", "mouse", 2, "JM", 0x260D, 0x1237, 8, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM290F", "dongle", 2, "JM", 0x260D, 0x1238, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 GM", "TM265F", "mouse", 2, "JM", 0x260D, 0x1244, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950GD", "TM265", "mouse", 2, "JM", 0x260D, 0x1245, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM01AF", "dongle", 2, "JM", 0x260D, 0x1246, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE7", "TM287", "mouse", 2, "JM", 0x260D, 0x1250, 8, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM287", "dongle", 2, "JM", 0x260D, 0x1257, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM290", "dongle", 2, "JM", 0x260D, 0x1258, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950GD", "TM265F", "mouse", 2, "JM", 0x260D, 0x1259, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("Ultra 07", "TM00J", "mouse", 2, "JM", 0x260D, 0x1260, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM290", "dongle", 2, "JM", 0x260D, 0x1261, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM620", "TM290", "mouse", 1, "HS", 0x260D, 0x1263, 8, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE6", "TM290", "mouse", 2, "JM", 0x260D, 0x1264, 9, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE6", "TM290", "mouse", 2, "JM", 0x260D, 0x1265, 9, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("LM180T", "TM00AF", "mouse", 2, "JM", 0x260D, 0x1271, 12, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950G", "TM265F", "mouse", 2, "JM", 0x260D, 0x1283, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950G", "TM265D", "mouse", 2, "JM", 0x260D, 0x1284, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950GD", "TM265F", "mouse", 2, "JM", 0x260D, 0x1286, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("极光", "TM01A", "mouse", 2, "JM", 0x260D, 0x1287, 7, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("DAREU Receiver", "TM265F", "dongle", 2, "JM", 0x260D, 0x1288, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("猎风V3", "TM00P", "mouse", 2, "JM", 0x260D, 0x1289, 9, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("Ultra09", "TM00T", "mouse", 2, "JM", 0x260D, 0x1292, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("Ultra07", "TP040", "mouse", 2, "JM", 0x260D, 0x1293, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("极风3", "TM290D", "mouse", 2, "JM", 0x260D, 0x1295, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EM910G", "TM00PF", "mouse", 2, "JM", 0x260D, 0x1298, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE6", "TM290F", "mouse", 2, "JM", 0x260D, 0x1299, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950G", "TM01AF", "mouse", 2, "JM", 0x260D, 0x1301, 7, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM610 ultra v2", "TM265D", "mouse", 2, "JM", 0x260D, 0x1302, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950GD PRO", "TM265D", "mouse", 2, "JM", 0x260D, 0x1303, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("CM620", "TM290", "mouse", 2, "JM", 0x260D, 0x1304, 8, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EM905 Pro", "TM173", "mouse", 2, "JM", 0x260D, 0x1305, 5, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EM901", "TM224", "mouse", 2, "JM", 0x260D, 0x1319, 5, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE6", "TM290F", "mouse", 2, "JM", 0x260D, 0x1328, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 SE", "TM265D", "mouse", 2, "JM", 0x260D, 0x1329, 5, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE6 V2", "TM290F", "mouse", 2, "JM", 0x260D, 0x1330, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE5", "TM01D", "mouse", 2, "JM", 0x260D, 0x1332, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("2.4G Receiver", "TM01D", "dongle", 2, "JM", 0x260D, 0x1333, -1, null, null, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A950 Wing", "TM275D", "mouse", 2, "JM", 0x260D, 0x1334, 5, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("EM911T", "TM259F", "mouse", 2, "JM", 0x260D, 0x1335, 6, true, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("AE6", "TM290D", "mouse", 2, "JM", 0x260D, 0x1337, 6, false, false, 0.0, 0.0, 0.0, -1),
            new DareUEntry("A75 HE", "TK52L", "keyboard", 4, "OHID", 0x260D, 0xF055, 79, false, false, 0.1, 4.0, 0.02, -1),
        };

        /// <summary>Tra muc registry theo PID (VID DareU duy nhat 0x260D).</summary>
        public static DareUEntry Match(int vid, int pid)
        {
            foreach (var e in Entries)
                if (e.Vid == vid && e.Pid == pid) return e;
            return null;
        }
    }
}
