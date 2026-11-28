using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.15 — KHO SCHEME DEN CONG DONG: logic THUAN.
    // - Catalog docs/schemes-catalog.json tren repo (chep khuon catalog
    //   GIF online): moi muc tro toi mot file scheme JSON cua app (dinh
    //   dang LightingScheme) — tai ve bang UpdateCore.DownloadAndVerify
    //   (kiem SHA-256) roi ghi thang vao thu muc LightingSchemes nen tu
    //   xuat hien trong "Bo den cua toi", khong can code hien thi moi.
    // - Ma chia se TVKS1: + base64url cua chinh JSON scheme — nguoi
    //   nhan dan ma vao app la co scheme, khong can file/link.
    // =====================================================================

    /// <summary>Mot muc trong catalog scheme cong dong.</summary>
    public sealed class SchemeCatalogItem
    {
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string File { get; set; } = "";
        public string Url { get; set; } = "";
        public string DriveId { get; set; } = "";
        public long Size { get; set; }
        public string Sha256 { get; set; } = "";
    }

    /// <summary>File catalog dang { version, items: [...] }.</summary>
    public sealed class SchemeCatalogDoc
    {
        public int Version { get; set; }
        public List<SchemeCatalogItem> Items { get; set; } = new List<SchemeCatalogItem>();
    }

    public static class SchemeCatalogCore
    {
        public const string CatalogUrl =
            "https://raw.githubusercontent.com/tiendz2k/Tien-Vu-Keyboard-Studio/main/docs/schemes-catalog.json";

        /// <summary>Parse catalog; bo cac muc thieu ten/file hoac khong
        /// co duong tai (Url va DriveId deu rong).</summary>
        public static List<SchemeCatalogItem> ParseCatalog(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<SchemeCatalogItem>();
            try
            {
                var doc = JsonSerializer.Deserialize<SchemeCatalogDoc>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var items = doc?.Items ?? new List<SchemeCatalogItem>();
                items.RemoveAll(i => i == null
                    || string.IsNullOrWhiteSpace(i.Name) || string.IsNullOrWhiteSpace(i.File)
                    || (string.IsNullOrWhiteSpace(i.Url) && string.IsNullOrWhiteSpace(i.DriveId)));
                return items;
            }
            catch { return new List<SchemeCatalogItem>(); }
        }

        /// <summary>URL tai file scheme: Url truc tiep neu co, khong thi
        /// qua link tai Drive cong khai (cung mau voi Thu vien Online).</summary>
        public static string DownloadUrlOf(SchemeCatalogItem item)
        {
            if (item == null) return null;
            if (!string.IsNullOrWhiteSpace(item.Url)) return item.Url;
            if (!string.IsNullOrWhiteSpace(item.DriveId))
                return "https://drive.google.com/uc?export=download&id=" + item.DriveId;
            return null;
        }
    }

    /// <summary>Ma chia se scheme dang "TVKS1:" + base64url(JSON).</summary>
    public static class SchemeShare
    {
        public const string Prefix = "TVKS1:";

        public static string Encode(LightingScheme scheme)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            string json = JsonSerializer.Serialize(scheme);
            return Prefix + Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        }

        /// <summary>
        /// Giai ma + kiem hop le: JSON phai parse thanh LightingScheme,
        /// co it nhat 1 phim, moi khoa dung dang "k&lt;0..79&gt;" va gia
        /// tri la mau hex doc duoc (kiem bang ToPainted +
        /// MaxKeyData.ViewKeyWireIds/KeyIds). Scheme tra ve da duoc
        /// loc chi giu cac phim hop le.
        /// </summary>
        public static bool TryDecode(string code, out LightingScheme scheme, out string error)
        {
            scheme = null; error = null;
            if (string.IsNullOrWhiteSpace(code)) { error = "Mã trống."; return false; }
            string s = code.Trim();
            if (!s.StartsWith(Prefix, StringComparison.Ordinal)) { error = "Mã phải bắt đầu bằng " + Prefix; return false; }
            byte[] bytes;
            try { bytes = Base64UrlDecode(s.Substring(Prefix.Length)); }
            catch { error = "Mã không phải base64 hợp lệ."; return false; }
            LightingScheme parsed;
            try { parsed = JsonSerializer.Deserialize<LightingScheme>(Encoding.UTF8.GetString(bytes)); }
            catch { error = "Nội dung mã không phải scheme hợp lệ."; return false; }
            if (parsed == null || parsed.Keys == null || parsed.Keys.Count == 0)
            { error = "Scheme trong mã không có phím nào."; return false; }

            var clean = new LightingScheme
            {
                Name = string.IsNullOrWhiteSpace(parsed.Name) ? "Scheme chia sẻ" : parsed.Name.Trim(),
                Speed = Math.Max(1, Math.Min(5, parsed.Speed)),
                LightMode = parsed.LightMode,
            };
            foreach (var kv in parsed.Keys)
            {
                if (kv.Key == null || kv.Key.Length < 2 || kv.Key[0] != 'k') continue;
                if (!int.TryParse(kv.Key.Substring(1), out int view) || view < 0 || view >= 80) continue;
                if (!MaxKeyData.KeyIds.Contains(MaxKeyData.ViewKeyWireIds[view])) continue;
                if (!ColorMath.TryParseHex(kv.Value, out var c)) continue;
                clean.Keys["k" + view] = ColorMath.ToHex(c.R, c.G, c.B);
            }
            if (clean.Keys.Count == 0) { error = "Scheme trong mã không có phím hợp lệ nào."; return false; }
            scheme = clean;
            return true;
        }

        /// <summary>Kiem mot scheme tai tu catalog: dung lai dung quy
        /// tac cua ma chia se (co ten + it nhat 1 phim hop le).</summary>
        public static bool ValidateDownloaded(string json, out LightingScheme scheme, out string error)
        {
            scheme = null; error = null;
            LightingScheme parsed;
            try { parsed = JsonSerializer.Deserialize<LightingScheme>(json ?? ""); }
            catch { error = "File scheme không phải JSON hợp lệ."; return false; }
            if (parsed == null || parsed.Keys == null || parsed.Keys.Count == 0)
            { error = "File scheme không có phím nào."; return false; }
            // Dung lai duong kiem cua TryDecode bang cach boc ma tam.
            return TryDecode(Encode(parsed), out scheme, out error);
        }

        private static string Base64UrlEncode(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] Base64UrlDecode(string text)
        {
            string s = text.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
                case 1: throw new FormatException("base64url length");
            }
            return Convert.FromBase64String(s);
        }
    }
}
