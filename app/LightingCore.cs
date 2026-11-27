using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.12 — LOGIC THUAN cho nang cap anh sang (khong UI/HID, unit test
    // duoc): toan hoc mau (HSV/hex/mau gan day), trang thai den luu ben
    // (settings.json), scheme den tuy chinh (JSON cua app + XML theo dinh
    // dang app chinh chu), anh xa tham so nhac -> khoi 0x78, va cac
    // generator hieu ung thoi gian thuc (stream 0x20).
    // =====================================================================

    /// <summary>Toan hoc mau tren byte RGB (khong phu thuoc Avalonia).</summary>
    public static class ColorMath
    {
        /// <summary>RGB -> HSV: H 0..360, S/V 0..1.</summary>
        public static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
        {
            double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
            double max = Math.Max(rf, Math.Max(gf, bf));
            double min = Math.Min(rf, Math.Min(gf, bf));
            double d = max - min;
            double h = 0;
            if (d > 1e-9)
            {
                if (max == rf) h = 60 * (((gf - bf) / d) % 6);
                else if (max == gf) h = 60 * ((bf - rf) / d + 2);
                else h = 60 * ((rf - gf) / d + 4);
            }
            if (h < 0) h += 360;
            double s = max <= 1e-9 ? 0 : d / max;
            return (h, s, max);
        }

        /// <summary>HSV -> RGB. H tu dong boc ve 0..360, S/V kep 0..1.</summary>
        public static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            s = Math.Max(0, Math.Min(1, s));
            v = Math.Max(0, Math.Min(1, v));
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;
            double rf, gf, bf;
            if (h < 60) { rf = c; gf = x; bf = 0; }
            else if (h < 120) { rf = x; gf = c; bf = 0; }
            else if (h < 180) { rf = 0; gf = c; bf = x; }
            else if (h < 240) { rf = 0; gf = x; bf = c; }
            else if (h < 300) { rf = x; gf = 0; bf = c; }
            else { rf = c; gf = 0; bf = x; }
            return (ToByte((rf + m) * 255), ToByte((gf + m) * 255), ToByte((bf + m) * 255));
        }

        public static byte ToByte(double v) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v)));

        /// <summary>(R,G,B) -> "RRGGBB" (khong #).</summary>
        public static string ToHex(byte r, byte g, byte b) => $"{r:X2}{g:X2}{b:X2}";

        /// <summary>Doc hex linh hoat: "RRGGBB", "#RRGGBB", "0xRRGGBB",
        /// hoac so thap phan (dang rgbvalue cua mot so file scheme).</summary>
        public static bool TryParseHex(string text, out (byte R, byte G, byte B) rgb)
        {
            rgb = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            else if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            if (s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hv))
            {
                rgb = ((byte)(hv >> 16), (byte)(hv >> 8), (byte)hv);
                return true;
            }
            if (s.Length > 0 && s.All(char.IsDigit) &&
                int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dv) &&
                dv >= 0 && dv <= 0xFFFFFF)
            {
                rgb = ((byte)(dv >> 16), (byte)(dv >> 8), (byte)dv);
                return true;
            }
            return false;
        }

        /// <summary>Day mot mau vao dau danh sach "mau gan day": trung thi
        /// dua len dau, gioi han cap mau (mac dinh 12).</summary>
        public static List<string> PushRecent(IEnumerable<string> recents, string hex, int cap = 12)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(hex)) list.Add(hex.ToUpperInvariant());
            foreach (var h in recents ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrEmpty(h)) continue;
                string u = h.ToUpperInvariant();
                if (!list.Contains(u)) list.Add(u);
                if (list.Count >= cap) break;
            }
            return list.Take(cap).ToList();
        }

        /// <summary>Nhan mau voi he so sang 0..1 (giu nguyen hue).</summary>
        public static (byte R, byte G, byte B) Scale((byte R, byte G, byte B) c, double k)
        {
            k = Math.Max(0, Math.Min(1, k));
            return (ToByte(c.R * k), ToByte(c.G * k), ToByte(c.B * k));
        }
    }

    /// <summary>
    /// Trang thai anh sang day du — luu trong settings.json (LibrarySettings.
    /// Lighting). Moi truong co mac dinh khop hanh vi cu de file settings
    /// cu nap len khong doi giao dien.
    /// </summary>
    public sealed class LightingStateData
    {
        // Hieu ung firmware (khoi 04 13)
        public int EffectId { get; set; } = 10;
        public byte R { get; set; } = 0x7C;
        public byte G { get; set; } = 0x3A;
        public byte B { get; set; } = 0xED;
        public bool Multi { get; set; } = false;
        public int BrightnessUi { get; set; } = 3;   // 0..4
        public int SpeedUi { get; set; } = 2;        // 0..4
        public int Direction { get; set; } = 0;      // 0..3
        // Per-key: ten scheme dang ap (rong = ban do ve tay chua luu)
        public string PaintedSchemeName { get; set; } = "";
        // Nhac 0x78
        public int MusicStyle { get; set; } = 4;          // 0..14 (L99Protocol.AudioStyleNames)
        public int MusicAmplitude { get; set; } = 100;    // scale byte 0x78: 10..100
        public int MusicBgMode { get; set; } = 1;         // 0 Tat, 1 Mo, 2 Day du
        public int MusicBgBrightness { get; set; } = 40;  // 0..100 (%)
        public double MusicGain { get; set; } = 1.0;      // do nhay 0.4..1.8
        // Thoi gian thuc 0x20: hieu ung dang chon (8 = Tat)
        public int RtEffect { get; set; } = 8;
        // GIF -> LED
        public double GifBrightness { get; set; } = 1.0;  // 0.2..1.5
        public int GifBlackLevel { get; set; } = 24;      // 0..60
    }

    /// <summary>Anh xa dieu khien nhac (UI) -> tham so khoi 0x78.</summary>
    public static class MusicRhythmMap
    {
        /// <summary>Byte kieu nhip = chi so trong AudioStyleNames cua app
        /// (0 = Tat ... 14 = Ambilight). Danh sach nay la cua nen tang
        /// L99/app — KHONG anh xa theo thu tu danh sach UI cua hang
        /// (ban hang dat "Off" o giua; xem bao cao nghien cuu).</summary>
        public static byte RhythmByteForStyle(int styleIndex)
            => (byte)Math.Max(0, Math.Min(L99Protocol.AudioStyleNames.Length - 1, styleIndex));

        /// <summary>Byte che do nen: Tat -> 0x00; Mo/Day du -> 0x08 (gia tri
        /// mac dinh da dung tu ban dau cua builder). Khac biet Mo/Day du
        /// nam o byte do sang nen (UI rieng) + preview/stream 0x20.</summary>
        public static byte BgModeByte(int uiMode) => uiMode <= 0 ? (byte)0x00 : (byte)0x08;

        /// <summary>Do sang nen UI 0..100 (%) -> byte 0..255 (byte 26).</summary>
        public static byte BgBrightnessByte(int uiPercent)
            => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(Math.Max(0, Math.Min(100, uiPercent)) * 255.0 / 100.0)));

        /// <summary>
        /// 23 muc pho gui di: bands01 (0..1) x do nhay (gain) -> thang
        /// 0..scale. Moi muc <= scale (dieu kien cua BuildAudioBlock).
        /// </summary>
        public static int[] BuildLevels(IReadOnlyList<float> bands01, double gain, int scale)
        {
            scale = Math.Max(1, Math.Min(255, scale));
            var levels = new int[L99Protocol.AudioBandCount];
            for (int i = 0; i < levels.Length; i++)
            {
                double b = bands01 != null && i < bands01.Count ? bands01[i] : 0;
                double v = Math.Max(0, Math.Min(1, b * gain));
                levels[i] = Math.Min(scale, (int)Math.Round(v * scale));
            }
            return levels;
        }
    }

    /// <summary>
    /// Mot scheme den tuy chinh: ban do mau theo phim HIEN THI ("k0".."k79",
    /// gia tri "RRGGBB") + toc do/mode nen tuy chon (LightMode -1 = khong
    /// co; scheme v1 chi ap ban do tinh — xem bao cao gap, muc 11).
    /// </summary>
    public sealed class LightingScheme
    {
        public string Name { get; set; } = "";
        public int Speed { get; set; } = 3;
        public int LightMode { get; set; } = -1;
        public Dictionary<string, string> Keys { get; set; } = new Dictionary<string, string>();

        public static LightingScheme FromPainted(string name, IDictionary<string, (byte R, byte G, byte B)> painted)
        {
            var s = new LightingScheme { Name = name ?? "" };
            foreach (var kv in painted ?? new Dictionary<string, (byte, byte, byte)>())
                s.Keys[kv.Key] = ColorMath.ToHex(kv.Value.R, kv.Value.G, kv.Value.B);
            return s;
        }

        public Dictionary<string, (byte R, byte G, byte B)> ToPainted()
        {
            var map = new Dictionary<string, (byte, byte, byte)>();
            if (Keys == null) return map;
            foreach (var kv in Keys)
                if (ColorMath.TryParseHex(kv.Value, out var c)) map[kv.Key] = c;
            return map;
        }

        /// <summary>Ban do theo WIRE id (cho xuat XML: keycode = wire id).</summary>
        public Dictionary<byte, (byte R, byte G, byte B)> ToWireMap()
        {
            var map = new Dictionary<byte, (byte, byte, byte)>();
            var painted = ToPainted();
            for (int i = 0; i < MaxKeyData.ViewKeyWireIds.Length; i++)
            {
                if (painted.TryGetValue("k" + i, out var c))
                    map[MaxKeyData.ViewKeyWireIds[i]] = c; // id trung (0x54): phim sau thang, nhu SendPaintedAsync
            }
            return map;
        }

        /// <summary>Dung scheme tu ban do wire id (nhap XML): wire id ->
        /// phim hien thi dau tien mang id do.</summary>
        public static LightingScheme FromWireMap(string name, IDictionary<byte, (byte R, byte G, byte B)> wire)
        {
            var s = new LightingScheme { Name = name ?? "" };
            var firstViewByWire = new Dictionary<byte, int>();
            for (int i = 0; i < MaxKeyData.ViewKeyWireIds.Length; i++)
                if (!firstViewByWire.ContainsKey(MaxKeyData.ViewKeyWireIds[i]))
                    firstViewByWire[MaxKeyData.ViewKeyWireIds[i]] = i;
            foreach (var kv in wire ?? new Dictionary<byte, (byte, byte, byte)>())
                if (firstViewByWire.TryGetValue(kv.Key, out int vi))
                    s.Keys["k" + vi] = ColorMath.ToHex(kv.Value.R, kv.Value.G, kv.Value.B);
            return s;
        }
    }

    /// <summary>Luu scheme dang JSON cua app trong mot thu muc cho truoc
    /// (MainWindow truyen %AppData%\TienKeyboardStudio\LightingSchemes).</summary>
    public sealed class LightingSchemeStore
    {
        private readonly string _dir;
        public LightingSchemeStore(string dir) { _dir = dir; }
        public string Dir => _dir;

        public static string SanitizeFileName(string name)
        {
            string s = (name ?? "").Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            // '\\' hop le trong ten file tren Linux nhung la dau phan cach
            // tren Windows — thay luon de file scheme chia se duoc hai ben.
            s = s.Replace('\\', '_');
            s = s.Trim('.', ' ');
            if (s.Length == 0) s = "scheme";
            if (s.Length > 60) s = s.Substring(0, 60);
            return s;
        }

        private string PathFor(string name) => Path.Combine(_dir, SanitizeFileName(name) + ".json");

        public List<string> ListNames()
        {
            var names = new List<string>();
            try
            {
                if (!Directory.Exists(_dir)) return names;
                foreach (var f in Directory.GetFiles(_dir, "*.json"))
                {
                    try
                    {
                        var s = JsonSerializer.Deserialize<LightingScheme>(File.ReadAllText(f));
                        if (s != null && !string.IsNullOrEmpty(s.Name)) names.Add(s.Name);
                    }
                    catch { }
                }
            }
            catch { }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        public void Save(LightingScheme scheme)
        {
            if (scheme == null || string.IsNullOrWhiteSpace(scheme.Name))
                throw new InvalidOperationException("Scheme cần có tên.");
            Directory.CreateDirectory(_dir);
            File.WriteAllText(PathFor(scheme.Name),
                JsonSerializer.Serialize(scheme, new JsonSerializerOptions { WriteIndented = true }));
        }

        public LightingScheme Load(string name)
        {
            try
            {
                string p = PathFor(name);
                if (!File.Exists(p)) return null;
                return JsonSerializer.Deserialize<LightingScheme>(File.ReadAllText(p));
            }
            catch { return null; }
        }

        public bool Delete(string name)
        {
            try
            {
                string p = PathFor(name);
                if (!File.Exists(p)) return false;
                File.Delete(p);
                return true;
            }
            catch { return false; }
        }

        public void Rename(string oldName, string newName)
        {
            var s = Load(oldName) ?? throw new InvalidOperationException("Không tìm thấy scheme “" + oldName + "”.");
            if (string.IsNullOrWhiteSpace(newName)) throw new InvalidOperationException("Tên mới trống.");
            if (Load(newName) != null && !string.Equals(oldName, newName, StringComparison.CurrentCultureIgnoreCase))
                throw new InvalidOperationException("Đã có scheme tên “" + newName + "”.");
            s.Name = newName.Trim();
            Save(s);
            if (!string.Equals(oldName, newName, StringComparison.CurrentCultureIgnoreCase))
                Delete(oldName);
        }
    }

    /// <summary>
    /// Xuat/nhap XML theo HINH DANG cua app chinh chu (tu bang chung nhi
    /// phan: goc `customlight`, truong `speed`, `lightinfo`, tung phim
    /// `keyinfo` voi `keycode` + `rgbvalue`, kem `lightmode` va `type`).
    /// Cau truc long nhau chinh xac CHUA co file mau that de doi chieu —
    /// day la ban doan tot nhat tu bang chung; ghi ro trong BUILD-INFO.
    /// </summary>
    public static class SchemeXml
    {
        public static string Export(LightingScheme scheme)
        {
            var wire = scheme.ToWireMap();
            var root = new XElement("customlight",
                new XAttribute("type", "tienvu"),
                new XAttribute("speed", scheme.Speed));
            if (scheme.LightMode >= 0) root.Add(new XAttribute("lightmode", scheme.LightMode));
            var info = new XElement("lightinfo");
            foreach (var kv in wire.OrderBy(k => k.Key))
            {
                info.Add(new XElement("keyinfo",
                    new XAttribute("keycode", kv.Key),
                    new XAttribute("rgbvalue", ColorMath.ToHex(kv.Value.R, kv.Value.G, kv.Value.B))));
            }
            root.Add(info);
            return new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString();
        }

        /// <summary>Nhap XML: chap nhan keyinfo o bat ky muc long nao,
        /// keycode dang so (wire id), rgbvalue hex hoac thap phan.</summary>
        public static bool TryParse(string xml, out LightingScheme scheme, out string error)
        {
            scheme = null; error = null;
            try
            {
                var doc = XDocument.Parse(xml);
                var root = doc.Root;
                if (root == null) { error = "File XML trống."; return false; }
                var wire = new Dictionary<byte, (byte, byte, byte)>();
                foreach (var ki in root.Descendants().Where(e => e.Name.LocalName == "keyinfo"))
                {
                    string kc = (string)ki.Attribute("keycode");
                    string rv = (string)ki.Attribute("rgbvalue");
                    if (kc == null || rv == null) continue;
                    if (!int.TryParse(kc.Trim(), out int code) || code < 0 || code > 255) continue;
                    if (ColorMath.TryParseHex(rv, out var c)) wire[(byte)code] = c;
                }
                if (wire.Count == 0) { error = "Không tìm thấy phím nào (keyinfo/keycode/rgbvalue) trong file."; return false; }
                string name = (string)root.Attribute("name") ?? "";
                scheme = LightingScheme.FromWireMap(string.IsNullOrEmpty(name) ? "Scheme nhập (XML)" : name, wire);
                if (int.TryParse((string)root.Attribute("speed"), out int sp)) scheme.Speed = Math.Max(1, Math.Min(5, sp));
                if (int.TryParse((string)root.Attribute("lightmode"), out int lm)) scheme.LightMode = lm;
                return true;
            }
            catch (Exception ex) { error = "XML không hợp lệ: " + ex.Message; return false; }
        }
    }

    /// <summary>
    /// Generator hieu ung thoi gian thuc (stream 0x20) — thu tuc thuan,
    /// xac dinh theo thoi gian t (giay). Moi frame tra du cac o theo
    /// MaxKeyData.StreamSlots (moi o mang id cua chinh no ke ca khi tat —
    /// thieu o se lam lech toan bo phim phia sau, xem L99Protocol).
    /// </summary>
    public static class RealtimeEffects
    {
        public static readonly string[] Names =
        {
            "Tĩnh sáng", "Sao đêm", "Lay động", "Thở động", "Suối màu",
            "Sóng cầu vồng", "Dòng chảy", "Xoay đỉnh", "Tắt",
        };
        public const int OffIndex = 8;

        /// <summary>Khoang cach frame theo toc do UI 0..4: 130ms -> 50ms.</summary>
        public static int FrameIntervalMs(int speedUi)
            => 130 - Math.Max(0, Math.Min(4, speedUi)) * 20;

        // Hash xac dinh theo id phim (cho Sao dem): cung id -> cung pha.
        private static double Hash01(int n)
        {
            uint x = (uint)(n * 2654435761u);
            x ^= x >> 15; x *= 2246822519u; x ^= x >> 13;
            return (x & 0xFFFFFF) / (double)0x1000000;
        }

        public static L99Protocol.StreamQuad[] Generate(int effectId, double t,
            (byte R, byte G, byte B) baseColor, double bright01)
        {
            var slots = MaxKeyData.StreamSlots;
            var quads = new L99Protocol.StreamQuad[slots.Length];
            double b = Math.Max(0, Math.Min(1, bright01));
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                (byte R, byte G, byte B) c = (0, 0, 0);
                switch (effectId)
                {
                    case 0: // Tinh sang
                        c = ColorMath.Scale(baseColor, b);
                        break;
                    case 1: // Sao dem: nhap nhay theo pha rieng tung phim
                        {
                            double ph = Hash01(s.Id) * Math.PI * 2;
                            double sp = 0.6 + Hash01(s.Id * 7 + 3) * 1.8;
                            double tw = Math.Pow(0.5 + 0.5 * Math.Sin(t * sp + ph), 3.0);
                            c = ColorMath.Scale(baseColor, b * tw);
                            break;
                        }
                    case 2: // Lay dong: song sang chay ngang, bien do mem
                        {
                            double w = 0.55 + 0.45 * Math.Sin(t * 2.2 - s.CX * 9.0 + Math.Sin(t * 0.7 + s.CY * 5.0));
                            c = ColorMath.Scale(baseColor, b * w);
                            break;
                        }
                    case 3: // Tho dong: tho toan ban phim
                        {
                            double br = 0.25 + 0.75 * (0.5 + 0.5 * Math.Sin(t * 1.6));
                            c = ColorMath.Scale(baseColor, b * br);
                            break;
                        }
                    case 4: // Suoi mau: phun tu tam duoi len — hue theo khoang cach + thoi gian
                        {
                            double dx = s.CX - 0.5, dy = 1.0 - s.CY;
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            double hue = (d * 300 - t * 90) % 360;
                            var hc = ColorMath.HsvToRgb(hue, 0.85, 1.0);
                            double fall = Math.Max(0.15, 1.0 - d * 0.8);
                            c = ColorMath.Scale(hc, b * fall);
                            break;
                        }
                    case 5: // Song cau vong: hue chay theo truc ngang
                        {
                            var hc = ColorMath.HsvToRgb(s.CX * 360 - t * 70, 0.85, 1.0);
                            c = ColorMath.Scale(hc, b);
                            break;
                        }
                    case 6: // Dong chay: hue chay cheo (ngang + doc)
                        {
                            var hc = ColorMath.HsvToRgb((s.CX + s.CY) * 220 - t * 80, 0.85, 1.0);
                            c = ColorMath.Scale(hc, b);
                            break;
                        }
                    case 7: // Xoay dinh: mot dinh sang quay quanh tam ban phim
                        {
                            double ang = Math.Atan2(s.CY - 0.5, s.CX - 0.5);
                            double peak = t * 1.8 % (Math.PI * 2);
                            double diff = Math.Abs(((ang - peak) % (Math.PI * 2) + Math.PI * 3) % (Math.PI * 2) - Math.PI);
                            double glow = Math.Pow(Math.Max(0, 1.0 - diff / 1.1), 2.0);
                            c = ColorMath.Scale(baseColor, b * (0.08 + 0.92 * glow));
                            break;
                        }
                    default: // Tat (8) va id la
                        c = (0, 0, 0);
                        break;
                }
                quads[i] = new L99Protocol.StreamQuad(s.Id, c.R, c.G, c.B);
            }
            return quads;
        }
    }
}
