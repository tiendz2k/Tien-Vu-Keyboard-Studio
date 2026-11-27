using System;
using System.Collections.Generic;
using System.Linq;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.11.1 — Logic THUAN cho danh sach mau o trang Tong quan: gom theo
    // HANG + trang thai nhi phan "Ho tro / Chua ho tro" (yeu cau cua Tien:
    // danh sach cu qua chi tiet — dong spec + chu trang thai dai cua tung
    // Driver Pack). Du lieu giu nguyen tu ModelDatabase/DeviceRegistry;
    // day chi la lop trinh bay. Chi tiet kha nang cua tung mau van nam o
    // trang rieng cua ho giao thuc.
    //
    // Quy tac nhi phan:
    //  - "Ho tro"  = app dung duoc voi mau nay HOM NAY: ho tro day du
    //    (FullSupport / StatusKind 0), HOAC co trang rieng cua ho giao
    //    thuc (StatusKind 1 + Family khac rong) — ke ca cac mau chi xem /
    //    chi nhan dien, vi trang do la tinh nang that dang dung duoc.
    //  - "Chua ho tro" = muc registry/du kien ma app chua dung duoc:
    //    StatusKind 1 nhung chua co ho giao thuc (mo ta "se tich hop"),
    //    va StatusKind 2 (kin / de sau).
    // =====================================================================
    public static class ModelCatalog
    {
        public sealed class BrandGroup
        {
            public string Brand = "";
            public List<KeyboardModel> Models = new List<KeyboardModel>();
            public int SupportedCount;
        }

        /// <summary>Hang cua mau: uu tien truong Brand; rong thi lay tu
        /// dau tien cua ten (vd "AULA F75 Max" -> "AULA").</summary>
        public static string BrandOf(KeyboardModel m)
        {
            if (m == null) return "Khác";
            if (!string.IsNullOrWhiteSpace(m.Brand)) return m.Brand.Trim();
            string name = (m.Name ?? "").Trim();
            if (name.Length == 0) return "Khác";
            int sp = name.IndexOf(' ');
            return sp > 0 ? name.Substring(0, sp) : name;
        }

        /// <summary>Trang thai nhi phan cho danh sach Tong quan.</summary>
        public static bool IsSupported(KeyboardModel m)
        {
            if (m == null) return false;
            if (m.FullSupport) return true;
            if (m.StatusKind == 0) return true;
            return m.StatusKind == 1 && !string.IsNullOrEmpty(m.Family);
        }

        /// <summary>Gom theo hang (khong phan biet hoa thuong), hang xep
        /// theo alphabet, mau trong hang giu thu tu cua co so du lieu.</summary>
        public static List<BrandGroup> GroupByBrand(IEnumerable<KeyboardModel> models)
        {
            var map = new Dictionary<string, BrandGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in models ?? Enumerable.Empty<KeyboardModel>())
            {
                string b = BrandOf(m);
                if (!map.TryGetValue(b, out var g))
                {
                    g = new BrandGroup { Brand = b };
                    map[b] = g;
                }
                g.Models.Add(m);
                if (IsSupported(m)) g.SupportedCount++;
            }
            var list = map.Values.ToList();
            list.Sort((a, b) => string.Compare(a.Brand, b.Brand, StringComparison.OrdinalIgnoreCase));
            return list;
        }
    }
}
