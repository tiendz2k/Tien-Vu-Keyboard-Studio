using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Thoi tiet cho widget TFT: Open-Meteo (mien phi, khong can API key).
    // CHI goi mang khi nguoi dung mo trang Widget / bam lam moi — ngoai
    // truong hop nay app khong tu dong goi mang (nguyen tac cua du an).
    // Mac dinh Ha Noi (như ban Preview); loi mang -> tra null, widget chi
    // hien dong ho.
    // =====================================================================
    public sealed class WeatherInfo
    {
        public int TempC;
        public int Humidity;
        public string Description;
        public string Place = "Hà Nội";
    }

    public static class WeatherService
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        /// <summary>GET chuoi qua HttpClient dung chung (nem exception khi
        /// loi mang/HTTP — dung lam getter cho WeatherFlow).</summary>
        public static async Task<string> HttpGetString(string url)
            => await Http.GetStringAsync(url);

        // ---------- URL builders (thuan, test duoc) ----------

        public static string BuildForecastUrl(double lat, double lon)
            => "https://api.open-meteo.com/v1/forecast?latitude="
                + lat.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "&longitude=" + lon.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "&current=temperature_2m,relative_humidity_2m,weather_code";

        public static string BuildGeocodeUrl(string city)
            => "https://geocoding-api.open-meteo.com/v1/search?name="
                + Uri.EscapeDataString((city ?? "").Trim()) + "&count=1&language=vi&format=json";

        // ---------- Parsers (thuan, test duoc bang payload that) ----------

        /// <summary>
        /// Doc JSON forecast cua Open-Meteo -> WeatherInfo. So doc LINH
        /// HOAT (GetDouble + lam tron): ban 3.11.0 doc relative_humidity_2m
        /// va weather_code bang GetInt32 — API doi tra so thap phan la
        /// nem exception va toan bo lan lay thoi tiet chet trong im lang.
        /// Thieu truong "current" / JSON hong -> null.
        /// </summary>
        public static WeatherInfo ParseForecast(string json, string place)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    if (!doc.RootElement.TryGetProperty("current", out var cur)) return null;
                    int temp = (int)Math.Round(GetNum(cur, "temperature_2m"));
                    int hum = (int)Math.Round(GetNum(cur, "relative_humidity_2m"));
                    int code = (int)Math.Round(GetNum(cur, "weather_code"));
                    return new WeatherInfo
                    {
                        TempC = temp,
                        Humidity = hum,
                        Description = Describe(code),
                        Place = place ?? WeatherLocationLogic.HanoiName,
                    };
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Doc JSON geocoding cua Open-Meteo -> (lat, lon, "Ten, Quoc gia").
        /// Khong co "results" / mang rong -> null (thanh pho khong ton tai).
        /// </summary>
        public static (double Lat, double Lon, string Name)? ParseGeocode(string json, string fallbackCity)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    if (!doc.RootElement.TryGetProperty("results", out var res) || res.GetArrayLength() == 0) return null;
                    var first = res[0];
                    double la = first.GetProperty("latitude").GetDouble();
                    double lo = first.GetProperty("longitude").GetDouble();
                    string name = first.TryGetProperty("name", out var n) ? n.GetString() : fallbackCity;
                    string country = first.TryGetProperty("country", out var c) ? c.GetString() : "";
                    return (la, lo, string.IsNullOrEmpty(country) ? name : name + ", " + country);
                }
            }
            catch { return null; }
        }

        private static double GetNum(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

        // ---------- Goi mang (mong, dung cac ham thuan o tren) ----------

        public static async Task<WeatherInfo> FetchHanoiAsync()
            => await FetchAsync(WeatherLocationLogic.HanoiLat, WeatherLocationLogic.HanoiLon, WeatherLocationLogic.HanoiName);

        /// <summary>v3.11: lay thoi tiet tai mot toa do bat ky (Open-Meteo).</summary>
        public static async Task<WeatherInfo> FetchAsync(double lat, double lon, string place)
        {
            try
            {
                string json = await Http.GetStringAsync(BuildForecastUrl(lat, lon));
                return ParseForecast(json, place);
            }
            catch { return null; }
        }

        /// <summary>
        /// v3.11: tim toa do theo ten thanh pho (Open-Meteo Geocoding, mien
        /// phi, khong key) — phuc vu che do chon tay. Null neu khong thay.
        /// </summary>
        public static async Task<(double Lat, double Lon, string Name)?> GeocodeAsync(string city)
        {
            if (string.IsNullOrWhiteSpace(city)) return null;
            try
            {
                string json = await Http.GetStringAsync(BuildGeocodeUrl(city));
                return ParseGeocode(json, city.Trim());
            }
            catch { return null; }
        }

        /// <summary>
        /// v3.11.1: do vi tri qua IP bang HAI nha cung cap: ipapi.co truoc,
        /// ip-api.com du phong (ipapi.co ban mien phi gioi han tan suat rat
        /// gat — da tai hien duoc RateLimited tren may build). That bai ca
        /// hai -> null, tang goi tu ap thu tu du phong.
        /// </summary>
        public static async Task<GeoLocation> FetchIpGeoAsync()
        {
            try
            {
                var g = WeatherLocationLogic.ParseIpApiCo(
                    await Http.GetStringAsync("https://ipapi.co/json/"), DateTime.UtcNow);
                if (g != null) return g;
            }
            catch { }
            try
            {
                return WeatherLocationLogic.ParseIpApiCom(
                    await Http.GetStringAsync("http://ip-api.com/json/?fields=status,city,country,lat,lon"),
                    DateTime.UtcNow);
            }
            catch { return null; }
        }

        private static string Describe(int code) => code switch
        {
            0 => "Trời quang",
            1 => "Ít mây",
            2 => "Mây rải rác",
            3 => "Nhiều mây",
            45 or 48 => "Sương mù",
            51 or 53 or 55 => "Mưa phùn",
            61 => "Mưa nhỏ",
            63 => "Mưa vừa",
            65 => "Mưa to",
            71 or 73 or 75 => "Tuyết",
            80 or 81 or 82 => "Mưa rào",
            95 => "Giông",
            96 or 99 => "Giông + mưa đá",
            _ => "—",
        };
    }
}
