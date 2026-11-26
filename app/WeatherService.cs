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
        {
            const string url = "https://api.open-meteo.com/v1/forecast?latitude=21.0285&longitude=105.8542&current=temperature_2m,relative_humidity_2m,weather_code";
            try
            {
                string json = await Http.GetStringAsync(url);
                using (var doc = JsonDocument.Parse(json))
                {
                    var cur = doc.RootElement.GetProperty("current");
                    int temp = (int)Math.Round(cur.GetProperty("temperature_2m").GetDouble());
                    int hum = cur.GetProperty("relative_humidity_2m").GetInt32();
                    int code = cur.GetProperty("weather_code").GetInt32();
                    return new WeatherInfo { TempC = temp, Humidity = hum, Description = Describe(code) };
                }
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
