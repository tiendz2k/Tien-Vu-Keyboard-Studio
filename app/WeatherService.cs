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

        public static async Task<WeatherInfo> FetchHanoiAsync()
            => await FetchAsync(WeatherLocationLogic.HanoiLat, WeatherLocationLogic.HanoiLon, WeatherLocationLogic.HanoiName);

        /// <summary>v3.11: lay thoi tiet tai mot toa do bat ky (Open-Meteo).</summary>
        public static async Task<WeatherInfo> FetchAsync(double lat, double lon, string place)
        {
            string url = "https://api.open-meteo.com/v1/forecast?latitude="
                + lat.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "&longitude=" + lon.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "&current=temperature_2m,relative_humidity_2m,weather_code";
            try
            {
                string json = await Http.GetStringAsync(url);
                using (var doc = JsonDocument.Parse(json))
                {
                    var cur = doc.RootElement.GetProperty("current");
                    int temp = (int)Math.Round(cur.GetProperty("temperature_2m").GetDouble());
                    int hum = cur.GetProperty("relative_humidity_2m").GetInt32();
                    int code = cur.GetProperty("weather_code").GetInt32();
                    return new WeatherInfo { TempC = temp, Humidity = hum, Description = Describe(code), Place = place ?? WeatherLocationLogic.HanoiName };
                }
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
                string url = "https://geocoding-api.open-meteo.com/v1/search?name="
                    + Uri.EscapeDataString(city.Trim()) + "&count=1&language=vi&format=json";
                string json = await Http.GetStringAsync(url);
                using (var doc = JsonDocument.Parse(json))
                {
                    if (!doc.RootElement.TryGetProperty("results", out var res) || res.GetArrayLength() == 0) return null;
                    var first = res[0];
                    double la = first.GetProperty("latitude").GetDouble();
                    double lo = first.GetProperty("longitude").GetDouble();
                    string name = first.TryGetProperty("name", out var n) ? n.GetString() : city;
                    string country = first.TryGetProperty("country", out var c) ? c.GetString() : "";
                    return (la, lo, string.IsNullOrEmpty(country) ? name : name + ", " + country);
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// v3.11: do vi tri qua IP (ipapi.co — mien phi, khong key, https).
        /// That bai (mang/VPN chan) -> null, tang goi tu ap thu tu du phong.
        /// </summary>
        public static async Task<GeoLocation> FetchIpGeoAsync()
        {
            try
            {
                string json = await Http.GetStringAsync("https://ipapi.co/json/");
                return WeatherLocationLogic.ParseIpApiCo(json, DateTime.UtcNow);
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
