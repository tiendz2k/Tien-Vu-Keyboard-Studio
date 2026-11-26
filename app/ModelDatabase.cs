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
        public bool HasKnob;
        public bool FullSupport;       // ghi duoc that (chi F75 Max hien tai)
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
                ProtocolStatus = "Đã có giao thức mở — sẽ tích hợp", StatusKind = 1,
                HasScreen = false, HasKnob = true,
                Ids = { (0x258A, 0x010C, "USB có dây") },
            },
            new KeyboardModel
            {
                Name = "Leobog Hi75C Pro", Layout = "75% · 81 phím",
                ProtocolStatus = "Chung giao thức F75 — sẽ tích hợp", StatusKind = 1,
                HasScreen = false, HasKnob = true,
                Ids = { (0x258A, 0x010C, "USB có dây") },
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
                ProtocolStatus = "Giao thức kín — để sau", StatusKind = 2,
                HasScreen = false, HasKnob = true,
            },
            new KeyboardModel
            {
                Name = "GravaStar Mercury K1", Layout = "75% · 79 phím",
                ProtocolStatus = "Giao thức kín — để sau", StatusKind = 2,
                HasScreen = false, HasKnob = true,
            },
            new KeyboardModel
            {
                Name = "MonsGeek M1", Layout = "75% · núm xoay",
                ProtocolStatus = "Giao thức kín — để sau", StatusKind = 2,
                HasScreen = false, HasKnob = true,
            },
        };

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
