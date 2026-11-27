using System;
using System.Collections.Generic;
using System.Linq;
using HidSharp;

namespace TienKeyboardStudio3
{
    /// <summary>
    /// Mot giao dien HID cua dau thu 2.4G (mo ta THUAN du lieu de test):
    /// usage page/usage doc tu report descriptor + do dai report toi da.
    /// </summary>
    public sealed class DongleInterfaceInfo
    {
        public int UsagePage = -1;   // -1 = khong doc duoc
        public int Usage = -1;
        public int MaxInput;
        public int MaxOutput;
        public int MaxFeature;
        public string Path = "";
    }

    /// <summary>
    /// Logic phan loai + ket luan cua chan doan dau thu 2.4G (thuan, test
    /// duoc bang descriptor tong hop). Boi canh (nghien cuu cua du an,
    /// wireless-screen-research/REPORT.md): kenh MAN HINH TFT cua F75 Max
    /// la usage page 0xFF68 voi output report 4096 byte/chunk va CHI ton
    /// tai qua cap USB-C; kenh vendor cua dongle 05AC:024F chi gom
    /// pin/den/muc phan hoi/che do game (0xFF59, report 32 byte; registry
    /// web hub AULA con ghi mot page phu 0xFF60). Chan doan nay kiem chung
    /// lai dieu do tren dung chiec dongle cua nguoi dung — CHI DOC.
    /// </summary>
    public static class DongleClassifier
    {
        public const int DongleVid = 0x05AC;
        public const int DonglePid = 0x024F;
        public const int DisplayUsagePage = 0xFF68;
        public const int LargeOutputThreshold = 4096; // >= nguong nay = output report rat lon

        public static bool LooksLikeDisplay(DongleInterfaceInfo i)
        {
            if (i == null) return false;
            return i.UsagePage == DisplayUsagePage || i.MaxOutput >= LargeOutputThreshold;
        }

        public static bool HasDisplayInterface(IEnumerable<DongleInterfaceInfo> ifs)
        {
            return ifs != null && ifs.Any(LooksLikeDisplay);
        }

        /// <summary>Nhan vai tro tieng Viet cho mot giao dien.</summary>
        public static string RoleOf(DongleInterfaceInfo i)
        {
            if (i == null) return "Giao diện HID khác";
            if (LooksLikeDisplay(i))
                return "GIỐNG KÊNH MÀN HÌNH (bất thường trên đầu thu — xem kết luận!)";
            switch (i.UsagePage)
            {
                case 0xFF59:
                    return "Kênh vendor của đầu thu (theo tài liệu giao thức: pin / đèn / mức phản hồi / chế độ game)";
                case 0xFF60:
                    return "Kênh vendor phụ của đầu thu (theo registry web hub AULA)";
                case 0xFF13:
                    return "Kênh lệnh kiểu có dây (0xFF13) — lạ trên đầu thu";
                case 0x0001:
                    if (i.Usage == 0x0006) return "Giao diện bàn phím chuẩn (gõ phím)";
                    if (i.Usage == 0x0002) return "Giao diện chuột chuẩn";
                    return "Giao diện Generic Desktop chuẩn";
                case 0x000C:
                    return "Giao diện media/consumer chuẩn (phím đa phương tiện)";
                case 0x000D:
                    return "Giao diện pin/battery chuẩn";
                default:
                    return i.UsagePage < 0 ? "Không đọc được usage page" : "Giao diện HID khác";
            }
        }

        public static string DescribeLine(DongleInterfaceInfo i)
        {
            string page = i.UsagePage >= 0 ? "0x" + i.UsagePage.ToString("X4") : "?";
            string usage = i.Usage >= 0 ? "0x" + i.Usage.ToString("X4") : "?";
            return string.Format("• Usage page {0} / usage {1} — vào {2} B · ra {3} B · feature {4} B — {5}",
                page, usage, i.MaxInput, i.MaxOutput, i.MaxFeature, RoleOf(i));
        }

        /// <summary>
        /// Dong ket luan ro rang. ifs rong/null = khong thay dongle (caller
        /// tu hien thong bao than thien rieng).
        /// </summary>
        public static string BuildConclusion(IReadOnlyList<DongleInterfaceInfo> ifs)
        {
            if (ifs == null || ifs.Count == 0) return "";
            if (HasDisplayInterface(ifs))
            {
                return "⚠ PHÁT HIỆN giao diện giống kênh MÀN HÌNH trên đầu thu (usage page 0xFF68 hoặc output report ≥ 4096 byte). " +
                    "Điều này TRÁI với tài liệu giao thức đã kiểm chứng của dự án — kết luận “màn hình chỉ qua cáp” cần được xét lại trên đúng phần cứng này. " +
                    "Chẩn đoán này vẫn chỉ đọc; đừng gửi lệnh màn hình qua đầu thu khi chưa có nghiên cứu tiếp.";
            }
            return "Kết luận: KHÔNG có giao diện màn hình nào trên đầu thu — khớp với nghiên cứu của dự án: kênh màn hình TFT " +
                "(usage page 0xFF68, output report 4096 byte) chỉ tồn tại khi cắm cáp USB-C. Qua đầu thu 2.4G, firmware chỉ mở các kênh ở trên " +
                "(gõ phím + kênh vendor cho pin / đèn / mức phản hồi / chế độ game). Muốn đổi GIF/ảnh màn hình TFT thì cắm cáp USB-C.";
        }

        public static List<string> BuildReportLines(IReadOnlyList<DongleInterfaceInfo> ifs)
        {
            var lines = new List<string>();
            if (ifs == null) return lines;
            foreach (var i in ifs) lines.Add(DescribeLine(i));
            var c = BuildConclusion(ifs);
            if (c.Length > 0) { lines.Add(""); lines.Add(c); }
            return lines;
        }
    }

    /// <summary>
    /// Liet ke HID cua dongle — CHI DOC TUYET DOI: chi goi
    /// GetHidDevices + doc report descriptor + do dai report toi da.
    /// Khong mo stream, khong gui feature/output report nao.
    /// </summary>
    public static class DongleEnumerator
    {
        public static List<DongleInterfaceInfo> Enumerate()
        {
            var list = new List<DongleInterfaceInfo>();
            IEnumerable<HidDevice> devices;
            try { devices = DeviceList.Local.GetHidDevices(DongleClassifier.DongleVid, DongleClassifier.DonglePid); }
            catch { return list; }
            foreach (var d in devices)
            {
                var info = new DongleInterfaceInfo
                {
                    Path = SafeStr(() => d.DevicePath),
                    MaxInput = SafeInt(() => d.GetMaxInputReportLength()),
                    MaxOutput = SafeInt(() => d.GetMaxOutputReportLength()),
                    MaxFeature = SafeInt(() => d.GetMaxFeatureReportLength()),
                };
                try
                {
                    var desc = d.GetReportDescriptor();
                    foreach (var item in desc.DeviceItems)
                    {
                        uint first = 0; bool found = false;
                        try
                        {
                            foreach (var u in item.Usages.GetAllValues()) { first = u; found = true; break; }
                        }
                        catch { }
                        if (found)
                        {
                            info.UsagePage = (int)(first >> 16);
                            info.Usage = (int)(first & 0xFFFF);
                            break;
                        }
                    }
                }
                catch { /* khong doc duoc descriptor: giu -1 */ }
                list.Add(info);
            }
            return list;
        }

        private static int SafeInt(Func<int> f) { try { return f(); } catch { return -1; } }
        private static string SafeStr(Func<string> f) { try { return f() ?? ""; } catch { return ""; } }
    }
}
