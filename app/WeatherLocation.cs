using System;
using System.Text.Json;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.11 — Vi tri thoi tiet cho widget TFT: che do TU DONG THEO MANG
    // (mac dinh moi) do vi tri qua IP (ipapi.co, mien phi, khong key),
    // cache 24 gio; che do CHON TAY la ghi de. Thu tu du phong khi do IP
    // that bai: cache cu (du het han) -> thanh pho chon tay -> Ha Noi.
    // File nay THUAN LOGIC (ke hoach phan giai + parse JSON) de unit
    // test duoc bang phan hoi gia; loi goi mang nam o WeatherService.
    // =====================================================================

    /// <summary>Mot vi tri da do duoc qua IP.</summary>
    public sealed class GeoLocation
    {
        public string City = "";
        public string Country = "";
        public double Lat;
        public double Lon;
        public DateTime FetchedUtc;

        public string DisplayName
            => string.IsNullOrEmpty(Country) ? City : (string.IsNullOrEmpty(City) ? Country : City + ", " + Country);
    }

    /// <summary>Nguon vi tri se dung cho mot lan lay thoi tiet.</summary>
    public enum WeatherSource
    {
        Manual,          // che do chon tay: dung thanh pho nguoi dung chon
        GeoCache,        // cache IP con tuoi (< 24h)
        GeoFetch,        // can do IP moi
        StaleGeoCache,   // du phong: cache IP da het han nhung con dung duoc
        DefaultHanoi,    // du phong cuoi: Ha Noi
    }

    public static class WeatherLocationLogic
    {
        public const string ModeAuto = "auto";
        public const string ModeManual = "manual";
        public const int CacheFreshHours = 24;
        public const double HanoiLat = 21.0285;
        public const double HanoiLon = 105.8542;
        public const string HanoiName = "Hà Nội";

        public static string NormalizeMode(string mode)
            => string.Equals(mode, ModeManual, StringComparison.OrdinalIgnoreCase) ? ModeManual : ModeAuto;

        public static bool IsCacheFresh(GeoLocation g, DateTime nowUtc)
            => g != null && g.FetchedUtc != default && (nowUtc - g.FetchedUtc).TotalHours < CacheFreshHours
               && (g.Lat != 0 || g.Lon != 0);

        /// <summary>
        /// Ke hoach phan giai cho mot lan lay thoi tiet: chon tay thi luon
        /// Manual; tu dong thi cache con tuoi dung ngay, khong thi do IP moi.
        /// </summary>
        public static WeatherSource Plan(string mode, GeoLocation cache, DateTime nowUtc)
        {
            if (NormalizeMode(mode) == ModeManual) return WeatherSource.Manual;
            return IsCacheFresh(cache, nowUtc) ? WeatherSource.GeoCache : WeatherSource.GeoFetch;
        }

        /// <summary>
        /// Thu tu du phong SAU KHI do IP that bai: cache cu (neu co) ->
        /// thanh pho chon tay (neu da dat) -> Ha Noi mac dinh.
        /// </summary>
        public static WeatherSource FallbackAfterGeoFailure(GeoLocation cache, bool hasManualCity)
        {
            if (cache != null && (cache.Lat != 0 || cache.Lon != 0)) return WeatherSource.StaleGeoCache;
            if (hasManualCity) return WeatherSource.Manual;
            return WeatherSource.DefaultHanoi;
        }

        /// <summary>Doc GeoLocation da luu trong settings (null neu chua co).</summary>
        public static GeoLocation FromSettings(LibrarySettings s)
        {
            if (s == null) return null;
            if (string.IsNullOrEmpty(s.GeoCity) && s.GeoLat == 0 && s.GeoLon == 0) return null;
            DateTime fetched = default;
            if (!string.IsNullOrEmpty(s.GeoFetchedUtc))
                DateTime.TryParse(s.GeoFetchedUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out fetched);
            return new GeoLocation
            {
                City = s.GeoCity ?? "",
                Country = s.GeoCountry ?? "",
                Lat = s.GeoLat,
                Lon = s.GeoLon,
                FetchedUtc = fetched,
            };
        }

        /// <summary>Parse JSON cua ipapi.co/json/ ({city, country_name, latitude, longitude, error}).</summary>
        public static GeoLocation ParseIpApiCo(string json, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    var r = doc.RootElement;
                    if (r.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.True) return null;
                    if (!r.TryGetProperty("latitude", out var la) || !r.TryGetProperty("longitude", out var lo)) return null;
                    return new GeoLocation
                    {
                        City = GetStr(r, "city"),
                        Country = GetStr(r, "country_name"),
                        Lat = la.GetDouble(),
                        Lon = lo.GetDouble(),
                        FetchedUtc = nowUtc,
                    };
                }
            }
            catch { return null; }
        }

        /// <summary>Parse JSON cua ip-api.com/json ({status, city, country, lat, lon}).</summary>
        public static GeoLocation ParseIpApiCom(string json, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    var r = doc.RootElement;
                    if (GetStr(r, "status") != "success") return null;
                    if (!r.TryGetProperty("lat", out var la) || !r.TryGetProperty("lon", out var lo)) return null;
                    return new GeoLocation
                    {
                        City = GetStr(r, "city"),
                        Country = GetStr(r, "country"),
                        Lat = la.GetDouble(),
                        Lon = lo.GetDouble(),
                        FetchedUtc = nowUtc,
                    };
                }
            }
            catch { return null; }
        }

        private static string GetStr(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    }
}
