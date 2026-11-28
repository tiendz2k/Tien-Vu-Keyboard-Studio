using System;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.17 (scope B) — TACH NHOM GIF: man hinh TFT vs den phim LED.
    //
    // Bao loi cua Tien: trang Man hinh GIF va trang den phim dung CHUNG
    // mot danh sach tron — GIF dai rong (cho LED) lot vao luoi TFT, GIF
    // vuong 128x128 (cho TFT) lot vao bo chon GIF->LED.
    //
    // Quy tac phan loai THUAN LOGIC o day:
    // - Tin hieu chinh: ti le khung (rong / cao). Gan vuong (< 1.5) ->
    //   nhom TFT (man 128x128); dai rong (>= 1.5) -> nhom LED (cac dai
    //   GIF cua thu vien online la 900x281..900x352, ti le 2.6–3.2).
    // - Nguoi dung ghi de bang the luu trong settings (GifGroupOverrides,
    //   khoa = duong dan file, gia tri "tft"/"led") qua nut "Chuyen
    //   nhom" o tung be mat — the thang kich thuoc.
    // - Khong doc duoc kich thuoc (file loi/khong phai anh) -> mac dinh
    //   TFT: duong TFT chuan hoa moi anh ve 128x128 nen an toan, con
    //   converter LED gia dinh dai rong.
    // =====================================================================
    public enum GifKind
    {
        Tft = 0,
        Led = 1,
    }

    public static class GifGroups
    {
        public const string TagTft = "tft";
        public const string TagLed = "led";

        /// <summary>Nguong ti le rong/cao de coi la "dai rong" (LED).</summary>
        public const double LedAspectThreshold = 1.5;

        /// <summary>Phan loai theo kich thuoc goc.</summary>
        public static GifKind Classify(int width, int height)
        {
            if (width <= 0 || height <= 0) return GifKind.Tft;
            double aspect = (double)width / height;
            return aspect >= LedAspectThreshold ? GifKind.Led : GifKind.Tft;
        }

        public static string TagOf(GifKind kind) => kind == GifKind.Led ? TagLed : TagTft;

        public static bool TryParseTag(string tag, out GifKind kind)
        {
            kind = GifKind.Tft;
            if (string.IsNullOrWhiteSpace(tag)) return false;
            string t = tag.Trim().ToLowerInvariant();
            if (t == TagLed) { kind = GifKind.Led; return true; }
            if (t == TagTft) { kind = GifKind.Tft; return true; }
            return false;
        }

        /// <summary>Nhom hieu luc cua mot item: the nguoi dung (neu hop
        /// le) thang phan loai theo kich thuoc.</summary>
        public static GifKind Resolve(string tag, int width, int height)
            => TryParseTag(tag, out var k) ? k : Classify(width, height);
    }
}
