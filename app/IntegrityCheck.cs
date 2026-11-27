using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace TienKeyboardStudio3
{
    /// <summary>
    /// Bang bam SHA-256 cua cac assembly cua app, sinh luc build va nhung
    /// vao ban bien dich (resource "integrity-manifest.json").
    /// </summary>
    public sealed class IntegrityManifest
    {
        public string App { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public Dictionary<string, string> Files { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Kiem tra toan ven ban phat hanh (v3.8) — THUAN LOGIC, unit test duoc.
    ///
    /// Cach lam: luc build, build.sh bam SHA-256 cac DLL phu thuoc do app
    /// quan ly (KHONG bam file runtime cua .NET) trong thu muc bien dich
    /// va nhung bang bam vao tai nguyen cua app. Assembly chinh
    /// (TienKeyboardStudio.dll) KHONG nam trong bang bam vi mot file khong
    /// the chua bam cua chinh no (nhung bang bam vao la bam thay doi) — no
    /// duoc bao ve gian tiep qua bam cua goi phat hanh cong bo tren repo.
    ///
    /// v3.11.2 — SUA LOI CANH BAO GIA tren ban chinh thuc: ban cu dua ca
    /// "TienKeyboardStudio.exe" cua ban build ROI (apphost) vao bang bam,
    /// trong khi ban phat hanh la exe SINGLE-FILE (bundle) — mot file exe
    /// hoan toan khac, bam khac. Verify thay exe co mat canh minh ma bam
    /// lech nen banner do hien tren MOI ban chinh thuc (Tien gap tren may
    /// that). Nay: (1) build.sh khong bam exe nua (exe bundle cung theo
    /// nguyen tac "file khong tu chua bam cua chinh no", bao ve qua bam
    /// zip cong bo); (2) Verify TU BO QUA muc trung ten file exe dang
    /// chay, phong thu ngay ca khi bang bam cu/con sot muc exe.
    ///
    /// Khi chay, app doi chieu cac file assembly THUC SU NAM CANH file
    /// exe (ban dong goi roi / ban bi giai nen - dong goi lai) voi bang
    /// bam: file nao co mat ma bam khac -> canh bao "ban da bi chinh sua"
    /// tren Trang chu (khong chan tinh nang).
    ///
    /// Gioi han trung thuc: day la lop RAN DE sua doi thong thuong (hex-edit,
    /// thay DLL, dong goi lai kem bang bam cu). Nguoi du kien tri van co the
    /// bien dich lai ca app lan bang bam — khi do bang chung goc la lich su
    /// repo GitHub cong khai + bang bam cong bo kem moi ban phat hanh.
    /// </summary>
    public static class IntegrityCheck
    {
        public const string ResourceName = "TienKeyboardStudio38.integrity-manifest.json";
        public const string TamperWarning =
            "Bản này đã bị chỉnh sửa so với bản gốc — tải bản chính thức tại github.com/tiendz2k/Tien-Vu-Keyboard-Studio";

        public static IntegrityManifest ParseManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var m = JsonSerializer.Deserialize<IntegrityManifest>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (m == null || m.Files == null) return null;
                return m;
            }
            catch { return null; }
        }

        public static string ComputeSha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(data);
                return BitConverter.ToString(h).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string ComputeFileSha256Hex(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(fs);
                return BitConverter.ToString(h).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Doi chieu thu muc app voi bang bam. Tra ve danh sach mo ta cac file
        /// lech bam (rong = sach). File trong bang bam nhung KHONG co mat trong
        /// thu muc thi BO QUA (ban single-file nen cac DLL nam trong exe, khong
        /// phai file roi) — chi file CO MAT ma sai bam moi bi coi la chinh sua.
        /// </summary>
        public static List<string> Verify(string baseDir, IntegrityManifest manifest)
        {
            var bad = new List<string>();
            if (manifest == null || manifest.Files == null || string.IsNullOrEmpty(baseDir)) return bad;
            // v3.11.2: bo qua chinh file exe dang chay — exe single-file
            // khong bao gio khop bam apphost trong bang bam (xem ghi chu
            // lop), dua vao la canh bao gia tren ban chinh thuc.
            string selfName = null;
            try { selfName = Path.GetFileName(Environment.ProcessPath); } catch { }
            foreach (var kv in manifest.Files)
            {
                if (!string.IsNullOrEmpty(selfName) &&
                    string.Equals(kv.Key, selfName, StringComparison.OrdinalIgnoreCase))
                    continue;
                string path = Path.Combine(baseDir, kv.Key);
                if (!File.Exists(path)) continue;
                string actual;
                try { actual = ComputeFileSha256Hex(path); }
                catch { continue; }
                if (!string.Equals(actual, kv.Value, StringComparison.OrdinalIgnoreCase))
                    bad.Add(kv.Key);
            }
            return bad;
        }

        /// <summary>Doc bang bam nhung trong assembly dang chay (null neu khong co).</summary>
        public static IntegrityManifest LoadEmbedded()
        {
            try
            {
                var asm = typeof(IntegrityCheck).Assembly;
                using (var s = asm.GetManifestResourceStream(ResourceName))
                {
                    if (s == null) return null;
                    using (var r = new StreamReader(s))
                        return ParseManifest(r.ReadToEnd());
                }
            }
            catch { return null; }
        }
    }
}
