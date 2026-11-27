namespace TienKeyboardStudio3
{
    /// <summary>
    /// v3.11.2 — Dong mo ta NGAN cho the thiet bi o trang Tong quan
    /// (logic thuan, co test). Ban cu in ca dong dai ky thuat len the:
    /// "USB-C co day · VID 0C45:800A · 8 giao dien HID · Kenh man hinh
    /// (0xFF68?)... · Ho tro day du tinh nang" va voi mau anh em la
    /// nguyen van ProtocolStatus chua noi bo driver pack/dev_id — Tien
    /// phan anh "hien thi nhieu thong so qua". Nay the chi con:
    /// ket noi (rut gon) + muc ho tro bang loi thuong, VD
    /// "USB-C · Hỗ trợ đầy đủ" / "USB · Chỉ xem trước".
    /// Chi tiet ky thuat day du van con o nut "Sao chep thong tin
    /// thiet bi" nhu cu.
    /// </summary>
    public static class DeviceCardLine
    {
        /// <summary>Rut gon chuoi ket noi cua scanner ve dang ngan tren the.</summary>
        public static string ShortConnection(string connection)
        {
            if (string.IsNullOrWhiteSpace(connection)) return "USB";
            string c = connection.Trim();
            if (c.StartsWith("USB-C")) return "USB-C";
            if (c.StartsWith("USB")) return "USB";
            return c; // "Dau thu 2.4G" giu nguyen
        }

        /// <summary>Dong duy nhat tren the thiet bi: "USB-C · Hỗ trợ đầy đủ".</summary>
        public static string Build(string connection, bool fullSupport)
            => ShortConnection(connection) + (fullSupport ? " · Hỗ trợ đầy đủ" : " · Chỉ xem trước");
    }
}
