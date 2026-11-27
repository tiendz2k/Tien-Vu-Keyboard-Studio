using System;
using System.Collections.Generic;
using System.Linq;
using HidSharp;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Co so du lieu model ban phim: VID:PID nhan dien + co nang luc phan
    // cung (capability flags) + trang thai giao thuc. UI hien/an trang theo
    // capability cua model dang chon (quy tac cua Tien: phim khong co man
    // hinh thi khong hien trang Man hinh GIF / Widget TFT).
    // Trang thai giao thuc lay dung nhan cua ban Preview da duyet.
    // =====================================================================
    public sealed class KeyboardModel
    {
        public string Name;
        public string Layout;          // mo ta ngan (vd "75% · TFT + núm xoay")
        public string ProtocolStatus;  // nhan trang thai giao thuc
        public int StatusKind;         // 0 = ho tro day du, 1 = se tich hop, 2 = kin/de sau
        public bool HasScreen;
        public int ScreenSize;         // px (vuong), 0 = khong co
        public int ScreenW, ScreenH;   // kich thuoc man hinh chu nhat (Driver Pack 2), 0 = khong co
        public bool ScreenRgb888;      // man hinh dung RGB888 thay vi RGB565
        public bool HasHE;             // Hall-effect (Driver Pack 2)
        public string Brand = "";      // hang (Driver Pack 2)
        public List<uint> DevIds = new List<uint>(); // dev_id 0x8F da kiem chung (Driver Pack 2)
        public bool HasKnob;
        public bool FullSupport;       // ghi duoc that (chi F75 Max hien tai)
        public string Family = "";     // ho giao thuc (vd "kb_by_v2"); "" = chua co driver pack
        public string Stack = "";      // stack con trong ho (Driver Pack 3 MCHOSE: "GLW" / "QHW" / "BY")
        public string IdNote = "";     // cach dinh danh model (hien o trang ho giao thuc)
        // Cac cap VID:PID da biet cua model + nhan loai ket noi.
        public List<(int Vid, int Pid, string Conn)> Ids = new List<(int, int, string)>();
        // VID-only (Pid = -1): nhan dien theo hang (vd ho Keychron VIA).
        public bool Detectable => Ids.Count > 0;
    }

    public static class ModelDatabase
    {
        public static readonly List<KeyboardModel> All = new List<KeyboardModel>
        {
            new KeyboardModel
            {
                Name = "AULA F75 Max", Layout = "75% · TFT + núm xoay",
                ProtocolStatus = "Hỗ trợ đầy đủ", StatusKind = 0,
                HasScreen = true, ScreenSize = 128, HasKnob = true, FullSupport = true,
                Ids = { (0x0C45, 0x800A, "USB-C có dây"), (0x05AC, 0x024F, "Đầu thu 2.4G") },
            },
            new KeyboardModel
            {
                Name = "AULA F75", Layout = "75% · 80 phím",
                ProtocolStatus = "Driver Pack 1 (kb_by_v2) — chưa kiểm chứng phần cứng", StatusKind = 1,
                HasScreen = false, HasKnob = true, Family = "kb_by_v2",
                IdNote = "Định danh bằng chữ ký 0x82 [3,0,0,0,0,205] trước khi ghi",
                Ids = { (0x258A, 0x010C, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "Leobog Hi75", Layout = "75% · 81 phím",
                ProtocolStatus = "Driver Pack 1 (kb_by_v2) — chỉ xem (chưa có UUID)", StatusKind = 1,
                HasScreen = false, HasKnob = true, Family = "kb_by_v2",
                IdNote = "Chưa có UUID/chữ ký trong tài liệu đã trích — app chỉ cho xem, không cho ghi",
                Ids = { (0x258A, 0x010C, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "Leobog Hi75C Pro", Layout = "75% · 81 phím",
                ProtocolStatus = "Driver Pack 1 (kb_by_v2) — chỉ xem (chưa có UUID)", StatusKind = 1,
                HasScreen = false, HasKnob = true, Family = "kb_by_v2",
                IdNote = "Chưa có UUID/chữ ký trong tài liệu đã trích — app chỉ cho xem, không cho ghi",
                Ids = { (0x258A, 0x010C, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "YUNZII C75 Cake Meow", Layout = "75% · 85 phím",
                ProtocolStatus = "Driver Pack 1 (kb_by_v2) — chưa kiểm chứng phần cứng", StatusKind = 1,
                HasScreen = false, HasKnob = true, Family = "kb_by_v2",
                IdNote = "Định danh bằng UUID 0x82 = 0x030000000377 trước khi ghi",
                Ids = { (0x258A, 0x010C, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "YUNZII C98 Cake Meow (ANSI)", Layout = "98% · 101 phím",
                ProtocolStatus = "Driver Pack 1 (kb_by_v2) — chưa kiểm chứng phần cứng", StatusKind = 1,
                HasScreen = false, HasKnob = true, Family = "kb_by_v2",
                IdNote = "Định danh bằng UUID 0x82 = 0x0300000003FA trước khi ghi (trùng VID:PID với C75)",
                Ids = { (0x258A, 0x010C, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "YUNZII C98 Cake Meow ISO-DE", Layout = "98% · 102 phím",
                ProtocolStatus = "Driver Pack 1 (kb_by_v2) — chưa kiểm chứng phần cứng", StatusKind = 1,
                HasScreen = false, HasKnob = true, Family = "kb_by_v2",
                IdNote = "Định danh bằng UUID 0x82 = 0x030000000538 trước khi ghi",
                Ids = { (0x258A, 0x02DE, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "YUNZII C98 Cake Meow ISO-FR", Layout = "98% · 102 phím",
                ProtocolStatus = "Driver Pack 1 (kb_by_v2) — chưa kiểm chứng phần cứng", StatusKind = 1,
                HasScreen = false, HasKnob = true, Family = "kb_by_v2",
                IdNote = "Định danh bằng UUID 0x82 = 0x030000000539 trước khi ghi",
                Ids = { (0x258A, 0x02DF, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "Keychron V1", Layout = "65% · 84 phím",
                ProtocolStatus = "VIA chính thức — sẽ tích hợp", StatusKind = 1,
                HasScreen = false, HasKnob = true,
                Ids = { (0x3434, -1, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "Wooting 60HE", Layout = "60% · 61 phím",
                ProtocolStatus = "SDK chính thức — sẽ tích hợp", StatusKind = 1,
                HasScreen = false, HasKnob = false,
            },
            new KeyboardModel
            {
                Name = "MADLIONS MAD60 HE", Layout = "60% · 61 phím",
                ProtocolStatus = "Đã có giao thức mở — sẽ tích hợp", StatusKind = 1,
                HasScreen = false, HasKnob = false,
                Ids = { (0x373B, -1, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "Royal Kludge RK84", Layout = "75% · 84 phím",
                ProtocolStatus = "Đã có giao thức mở — sẽ tích hợp", StatusKind = 1,
                HasScreen = false, HasKnob = true,
            },
            new KeyboardModel
            {
                Name = "DrunkDeer A75", Layout = "75%",
                ProtocolStatus = "Đã có giao thức mở — sẽ tích hợp", StatusKind = 1,
                HasScreen = false, HasKnob = false,
            },
            new KeyboardModel
            {
                Name = "AULA S75 Pro", Layout = "75% · 80 phím",
                ProtocolStatus = "Giao thức kín — để sau", StatusKind = 2,
                HasScreen = true, ScreenSize = 128, HasKnob = true,
            },
            new KeyboardModel
            {
                Name = "AULA Nova75", Layout = "75%",
                ProtocolStatus = "Giao thức kín — để sau", StatusKind = 2,
                HasScreen = true, ScreenSize = 128, HasKnob = true,
            },
            new KeyboardModel
            {
                Name = "AULA WIN60 HE", Layout = "60% · 61 phím",
                ProtocolStatus = "Giao thức kín — để sau", StatusKind = 2,
                HasScreen = false, HasKnob = false,
            },
            new KeyboardModel
            {
                Name = "AULA WIN68 HE", Layout = "65% · 68 phím",
                ProtocolStatus = "Giao thức kín — để sau", StatusKind = 2,
                HasScreen = false, HasKnob = false,
            },
            new KeyboardModel
            {
                Name = "AULA HERO68 HE", Layout = "65% · 68 phím",
                ProtocolStatus = "Giao thức kín — để sau", StatusKind = 2,
                HasScreen = false, HasKnob = false,
            },
            new KeyboardModel
            {
                Name = "Akko 5075B", Layout = "75% · núm xoay",
                ProtocolStatus = "Driver Pack 2 (RongYuan) — chỉ xem (chưa có dev_id)", StatusKind = 1,
                HasScreen = false, HasKnob = true, Family = "rongyuan", Brand = "Akko",
                IdNote = "Thuộc nền tảng RongYuan (Driver Pack 2) nhưng chưa có dev_id đã kiểm chứng cho 5075B trong tài liệu đã trích — app chỉ cho xem, không cho ghi",
            },
            new KeyboardModel
            {
                Name = "GravaStar Mercury K1", Layout = "75% · 79 phím",
                ProtocolStatus = "Driver Pack 1 (kb_by_v2) — chưa kiểm chứng phần cứng", StatusKind = 1,
                HasScreen = false, HasKnob = true, Family = "kb_by_v2",
                IdNote = "Định danh bằng chữ ký 0x82 [3,0,0,0,1,180] trước khi ghi (PID 0294 hoặc 010C)",
                Ids = { (0x258A, 0x0294, "USB có dây"), (0x258A, 0x010C, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "MonsGeek M1", Layout = "75% · núm xoay",
                ProtocolStatus = "Driver Pack 2 (RongYuan) — chỉ xem (chưa có dev_id)", StatusKind = 1,
                HasScreen = false, HasKnob = true, Family = "rongyuan", Brand = "MonsGeek",
                IdNote = "M1 đời đầu (họ yc3121) thuộc nền tảng RongYuan nhưng chưa có dev_id đã kiểm chứng trong tài liệu đã trích — app chỉ cho xem, không cho ghi",
                Ids = { (0x3151, 0x4015, "USB có dây") },
            },
        };

        // v3.5 Driver Pack 2: nap cac model RongYuan (Akko + MonsGeek +
        // EPOMAKER) tu registry da trich (RongYuanRegistryData — 246 muc,
        // gop cac bien the trung ten hien thi thanh mot model, giu union
        // VID:PID va toan bo dev_id da biet). Muc trung ten voi danh sach
        // viet tay o tren thi bo qua.
        static ModelDatabase()
        {
            foreach (var m in BuildRongYuanModels())
                if (!All.Any(x => string.Equals(x.Name, m.Name, StringComparison.OrdinalIgnoreCase)))
                    All.Add(m);
            foreach (var m in BuildMchoseModels())
                if (!All.Any(x => string.Equals(x.Name, m.Name, StringComparison.OrdinalIgnoreCase)))
                    All.Add(m);
        }

        private static string MchoseLayoutGuess(string name)
        {
            if (name.Contains("98") || name.Contains("99")) return "98%";
            if (name.Contains("87")) return "TKL";
            if (name.Contains("75")) return "75%";
            if (name.Contains("68")) return "65%";
            if (name.Contains("60")) return "60%";
            return "Bàn phím";
        }

        // v3.6 Driver Pack 3: nap cac model MCHOSE tu registry da trich
        // (MchoseRegistryData — 31 muc CZ + 18 muc BY). Ho CZ -> Family
        // "mchose" (trang rieng, dinh danh bang PID registry + probe doc);
        // ho BY -> Family "kb_by_v2" (dung chung Driver Pack 1) nhung
        // CHI XEM: tai lieu cua hang phan biet cac model BY bang
        // productName USB, registry kb_by_v2 khong co UUID/chu ky cho
        // bat ky model MCHOSE BY nao — khong suy doan, nhu Leobog.
        // Khong model MCHOSE nao co man hinh (HasScreen luon false);
        // "GIF" cua QHW la hieu ung LED toan phim, khong phai man hinh.
        private static List<KeyboardModel> BuildMchoseModels()
        {
            var order = new List<KeyboardModel>();
            foreach (var e in MchoseRegistryData.CzEntries)
            {
                var m = new KeyboardModel
                {
                    Name = e.DisplayName,
                    Brand = "MCHOSE",
                    Family = "mchose",
                    Stack = e.Stack,
                    StatusKind = 1,
                    HasScreen = false,
                    HasHE = e.IsGlw,   // chi stack GLW co lenh trigger/HE; QHW khong co lenh HE trong kenh vendor
                    HasKnob = false,
                    Layout = MchoseLayoutGuess(e.Name) + (e.IsGlw ? " · Hall-effect" : " · cơ"),
                    ProtocolStatus = e.IsGlw
                        ? "Driver Pack 3 (MCHOSE GLW/CZ) — chưa kiểm chứng phần cứng"
                        : "Driver Pack 3 (MCHOSE QHW) — chưa kiểm chứng phần cứng",
                    IdNote = e.IsGlw
                        ? "Định danh bằng VID:PID theo registry CZ + probe đọc getInfo (cmd 3, đáp 0xAA) trước khi ghi. Checksum GLW là giả thuyết sum-mod-256 (hàm gốc chưa thu hồi được) — xem BUILD-INFO."
                        : "Định danh bằng VID:PID theo registry CZ + probe đọc getBaseInfo (0x0900) — VID/PID thiết bị tự khai phải khớp registry trước khi ghi.",
                };
                m.Ids.Add((e.Vid, e.PidMain, "USB có dây"));
                if (e.PidVendor != e.PidMain)
                    m.Ids.Add((e.Vid, e.PidVendor, "Kênh vendor/boot"));
                order.Add(m);
            }
            foreach (var e in MchoseRegistryData.ByEntries)
            {
                var m = new KeyboardModel
                {
                    Name = e.DisplayName,
                    Brand = "MCHOSE",
                    Family = "kb_by_v2",
                    Stack = "BY",
                    StatusKind = 1,
                    HasScreen = false,
                    HasHE = false,
                    Layout = MchoseLayoutGuess(e.Name) + " · cơ",
                    ProtocolStatus = "Driver Pack 3 (MCHOSE BY qua kb_by_v2) — chỉ xem (chưa có UUID)",
                    IdNote = "Model MCHOSE họ BY dùng chung họ kb_by_v2 (Driver Pack 1). Tài liệu của hãng phân biệt các model này bằng productName USB (nhiều model dùng chung 258A:010C); registry kb_by_v2 không có UUID/chữ ký cho model MCHOSE BY nào nên app chỉ cho xem — không suy đoán, giống Leobog.",
                };
                foreach (var (vid, pid) in e.Ids)
                    if (!m.Ids.Any(t => t.Vid == vid && t.Pid == pid))
                        m.Ids.Add((vid, pid, "USB có dây"));
                order.Add(m);
            }
            return order;
        }

        private static string LayoutGuess(int keys)
        {
            if (keys >= 104) return "Full-size";
            if (keys >= 96) return "98%";
            if (keys >= 87) return "TKL";
            if (keys >= 75) return "75%";
            if (keys >= 65) return "65%";
            if (keys >= 60) return "60%";
            return "";
        }

        private static List<KeyboardModel> BuildRongYuanModels()
        {
            var byName = new Dictionary<string, KeyboardModel>(StringComparer.OrdinalIgnoreCase);
            var order = new List<KeyboardModel>();
            foreach (var e in RongYuanRegistryData.Entries)
            {
                if (e.ChipFamily == "mouse") continue; // 3 muc chuot EPOMAKER — ngoai pham vi phim
                string display = e.DisplayName;
                if (!byName.TryGetValue(display, out var m))
                {
                    m = new KeyboardModel
                    {
                        Name = display,
                        Brand = e.Brand,
                        Family = "rongyuan",
                        StatusKind = 1,
                    };
                    byName[display] = m;
                    order.Add(m);
                }
                foreach (var pid in e.Pids)
                    if (!m.Ids.Any(t => t.Vid == e.Vid && t.Pid == pid))
                        m.Ids.Add((e.Vid, pid, "USB có dây"));
                if (e.DevId.HasValue && !m.DevIds.Contains(e.DevId.Value)) m.DevIds.Add(e.DevId.Value);
                if (e.HasHE) m.HasHE = true;
                if (e.HasKnob) m.HasKnob = true;
                if (e.HasScreen && !m.HasScreen)
                {
                    m.HasScreen = true;
                    m.ScreenW = e.ScreenW;
                    m.ScreenH = e.ScreenH;
                    m.ScreenSize = Math.Max(e.ScreenW, e.ScreenH);
                    m.ScreenRgb888 = e.ScreenRgb888;
                }
                if (e.Keys > 0 && string.IsNullOrEmpty(m.Layout))
                {
                    string g = LayoutGuess(e.Keys);
                    m.Layout = (g.Length > 0 ? g + " · " : "") + e.Keys + " phím";
                }
            }
            foreach (var m in order)
            {
                if (string.IsNullOrEmpty(m.Layout)) m.Layout = "Bàn phím";
                if (m.DevIds.Count > 0)
                {
                    m.ProtocolStatus = "Driver Pack 2 (RongYuan) — chưa kiểm chứng phần cứng";
                    m.IdNote = "Định danh bằng dev_id 0x8F (" +
                        string.Join(", ", m.DevIds.Select(d => "0x" + d.ToString("X8"))) +
                        ") trước khi ghi — VID:PID bị dùng chung giữa nhiều model";
                }
                else
                {
                    m.ProtocolStatus = "Driver Pack 2 (RongYuan) — chỉ xem (chưa có dev_id)";
                    m.IdNote = "Chưa có dev_id đã kiểm chứng trong tài liệu đã trích — app chỉ cho xem, không cho ghi (không suy đoán dev_id)";
                }
            }
            return order;
        }

        public static KeyboardModel F75Max => All[0];

        // Tim cac model khop voi mot VID:PID (mot cap co the khop nhieu model
        // — vd 258A:010C dung chung F75 / Hi75C Pro, can probe them moi ghi).
        public static List<KeyboardModel> Match(int vid, int pid)
        {
            var found = new List<KeyboardModel>();
            foreach (var m in All)
                foreach (var (v, p, _) in m.Ids)
                    if (v == vid && (p == -1 || p == pid)) { found.Add(m); break; }
            return found;
        }

        public static string ConnLabel(KeyboardModel m, int vid, int pid)
        {
            foreach (var (v, p, c) in m.Ids)
                if (v == vid && (p == -1 || p == pid)) return c;
            return "USB / HID";
        }
    }

    public sealed class DetectedDevice
    {
        public KeyboardModel Model;
        public int Vid, Pid;
        public string Connection;
        public string ProductName;
        public int InterfaceCount;
        public override string ToString() => $"{Model.Name} — {Connection}";
    }

    // Quet toan bo thiet bi HID, doi chieu voi ModelDatabase.
    public static class DeviceScanner
    {
        public static List<DetectedDevice> Scan()
        {
            var byKey = new Dictionary<string, DetectedDevice>();
            HidDevice[] devices;
            try { devices = DeviceList.Local.GetHidDevices().ToArray(); }
            catch { return new List<DetectedDevice>(); }
            foreach (var d in devices)
            {
                var matches = ModelDatabase.Match(d.VendorID, d.ProductID);
                if (matches.Count == 0) continue;
                string name = "";
                try { name = d.GetProductName() ?? ""; } catch { }
                foreach (var m in matches)
                {
                    string key = $"{m.Name}|{d.VendorID:X4}:{d.ProductID:X4}";
                    if (!byKey.TryGetValue(key, out var det))
                    {
                        det = new DetectedDevice
                        {
                            Model = m,
                            Vid = d.VendorID,
                            Pid = d.ProductID,
                            Connection = ModelDatabase.ConnLabel(m, d.VendorID, d.ProductID),
                            ProductName = name,
                            InterfaceCount = 0,
                        };
                        byKey[key] = det;
                    }
                    det.InterfaceCount++;
                }
            }
            // F75 Max len dau, sau do theo bang chu cai
            return byKey.Values
                .OrderByDescending(x => x.Model.FullSupport)
                .ThenBy(x => x.Model.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
