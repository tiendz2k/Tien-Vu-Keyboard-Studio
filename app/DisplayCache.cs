using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.15 — DAY TFT NHANH HON (phan nam NGOAI F75Device duoc bao ve):
    // (a) Cache chuyen doi: GIF/anh da ma hoa thanh stream man hinh thi
    //     luu lai duoi %AppData%\TienKeyboardStudio\DisplayCache, khoa
    //     bang (duong dan + mtime + kich thuoc file + che do fit) — day
    //     lai cung mot file thi bo qua han giai ma/ma hoa.
    // (b) Tien chuyen doi nen khi chon GIF (MainWindow goi GetOrEncode
    //     tren thread nen ngay luc chon) — bam "Nap len phim" la gui
    //     ngay, khong con buoc loading chuyen doi.
    // (c) Cong day widget: chi day khi noi dung frame thay doi that
    //     (bam SHA-256 cua stream; giong het thi bo qua).
    // Phan (d) — noi nhip cho ACK trong F75Device.UploadDisplay — nam
    // trong file DUOC BAO VE nen khong dung duoc o ban nay (xem
    // BUILD-INFO).
    // =====================================================================
    public sealed class DisplayCacheStore
    {
        private readonly string _cacheDir;

        public DisplayCacheStore(string cacheDir)
        {
            _cacheDir = cacheDir ?? throw new ArgumentNullException(nameof(cacheDir));
        }

        public string CacheDir => _cacheDir;

        public static string DefaultDir => Path.Combine(GifLibraryStore.RootDir, "DisplayCache");

        /// <summary>Khoa cache: SHA-256 cua (path chuan hoa | mtime UTC
        /// ticks | kich thuoc | fit). File doi noi dung (mtime/size doi)
        /// la khoa doi — khong bao gio phuc vu stream cu.</summary>
        public static string CacheKey(string path, long mtimeUtcTicks, long length, FitMode fit)
        {
            string raw = (path ?? "").ToUpperInvariant() + "|" + mtimeUtcTicks + "|" + length + "|" + (int)fit;
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)))
                    .Replace("-", "").ToLowerInvariant();
        }

        public string CachePathFor(string key) => Path.Combine(_cacheDir, key + ".bin");

        /// <summary>So chunk 4096B cua mot stream man hinh (de bao cao).</summary>
        public static int ChunksOf(byte[] stream)
            => stream == null ? 0 : (stream.Length + F75Protocol.ChunkLength - 1) / F75Protocol.ChunkLength;

        public bool TryLoad(string key, out byte[] stream)
        {
            stream = null;
            try
            {
                string p = CachePathFor(key);
                if (!File.Exists(p)) return false;
                var bytes = File.ReadAllBytes(p);
                // Stream hop le: dung boi so chunk va it nhat 1 chunk.
                if (bytes.Length == 0 || bytes.Length % F75Protocol.ChunkLength != 0) return false;
                stream = bytes;
                return true;
            }
            catch { return false; }
        }

        public void Store(string key, byte[] stream)
        {
            try
            {
                Directory.CreateDirectory(_cacheDir);
                string p = CachePathFor(key);
                string tmp = p + ".part";
                File.WriteAllBytes(tmp, stream);
                if (File.Exists(p)) File.Delete(p);
                File.Move(tmp, p);
            }
            catch { /* cache la lop tang toc — loi ghi cache khong duoc lam hong luong day */ }
        }

        /// <summary>
        /// Lay stream man hinh cho (path, fit): co cache thi tra ngay
        /// (FromCache = true, khong goi encoder); chua co thi goi
        /// encoder (DisplayEncoder.Encode o MainWindow) roi luu cache.
        /// </summary>
        public (byte[] Stream, bool FromCache, int Chunks) GetOrEncode(
            string path, FitMode fit, Func<string, FitMode, byte[]> encoder)
        {
            if (encoder == null) throw new ArgumentNullException(nameof(encoder));
            var fi = new FileInfo(path);
            string key = CacheKey(path, fi.Exists ? fi.LastWriteTimeUtc.Ticks : 0, fi.Exists ? fi.Length : 0, fit);
            if (TryLoad(key, out var cached))
                return (cached, true, ChunksOf(cached));
            var stream = encoder(path, fit);
            if (stream != null && stream.Length > 0) Store(key, stream);
            return (stream, false, ChunksOf(stream));
        }
    }

    /// <summary>
    /// Cong day widget len TFT: ghi nho bam cua stream vua day; lan day
    /// ke tiep co stream giong het thi ShouldPush = false (bo qua —
    /// widget dong ho/nhac khong doi thi khong can day lai, tiet kiem
    /// dung luong kenh man hinh).
    /// </summary>
    public sealed class WidgetPushGate
    {
        private string _lastHash = "";

        public static string HashOf(byte[] stream)
        {
            if (stream == null || stream.Length == 0) return "";
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>true neu stream khac lan day truoc (va ghi nho bam
        /// moi); false neu giong het — bo qua lan day nay.</summary>
        public bool ShouldPush(byte[] stream)
        {
            string h = HashOf(stream);
            if (h.Length == 0) return true; // khong co gi de so — cu day nhu cu
            if (h == _lastHash) return false;
            _lastHash = h;
            return true;
        }

        public void Reset() => _lastHash = "";
    }
}
