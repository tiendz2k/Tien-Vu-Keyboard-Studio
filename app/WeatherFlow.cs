using System;
using System.Threading.Tasks;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.11.1 — Luong lay thoi tiet widget TFT tach thanh logic THUAN de
    // test duoc bang HTTP gia (ban v3.11.0 de logic nay inline trong
    // MainWindow, moi loi deu nuot vao nhat ky thao tac — panel dang AN
    // mac dinh — nen nguoi dung khong thay gi ca, widget dung yen o muc
    // "Ha Noi · chua co du lieu").
    //
    // Ket qua tra ve WeatherOutcome co san dong trang thai TIENG VIET de
    // UI hien thang: thanh cong (noi + nhiet do), hoac LOI CO THE LOAI
    // (mang/API, khong tim thay thanh pho, du lieu API la). UI khong con
    // duoc phep that bai trong im lang.
    //
    // Thu tu phan giai giu nguyen v3.11: chon tay -> geocoding; tu dong
    // -> cache IP con tuoi -> do IP moi (ipapi.co, du phong ip-api.com —
    // ban 3.11.0 CHI dung ipapi.co ma dich vu nay gioi han tan suat rat
    // gat: ban build da tai hien duoc {"error":true,"reason":"RateLimited"}
    // ngay tren may build) -> du phong cua WeatherLocationLogic
    // (cache cu -> chon tay -> Ha Noi).
    // =====================================================================

    public enum WeatherFailKind
    {
        None,
        Network,      // loi mang / HTTP / API khong phan hoi
        CityNotFound, // geocoding khong co ket qua cho ten thanh pho chon tay
        BadData,      // API tra ve nhung khong doc duoc
    }

    public sealed class WeatherOutcome
    {
        public bool Ok;
        public WeatherInfo Weather;          // chi co khi Ok
        public string Place = "";            // noi dung se hien (khi Ok)
        public string Via = "";              // "chon tay" / "tu dong theo mang" / ...
        public WeatherFailKind Fail = WeatherFailKind.None;
        public string UserMessage = "";      // dong trang thai hien cho nguoi dung
        public GeoLocation NewGeoCache;      // khac null = UI can luu cache IP moi
    }

    public static class WeatherFlow
    {
        public const string NetworkErrorMessage =
            "Không tải được thời tiết (mạng/API) — bấm Cập nhật để thử lại";
        public const string BadDataMessage =
            "Không tải được thời tiết (dữ liệu API lạ) — bấm Cập nhật để thử lại";

        /// <summary>
        /// Chay mot lan lay thoi tiet. <paramref name="get"/>: url -> body,
        /// NEM exception khi loi mang/HTTP (WeatherService.HttpGetString
        /// dung HttpClient.GetStringAsync dung nhu vay).
        /// </summary>
        public static async Task<WeatherOutcome> RunAsync(
            Func<string, Task<string>> get, LibrarySettings ui, DateTime nowUtc)
        {
            if (get == null) throw new ArgumentNullException(nameof(get));
            var cache = WeatherLocationLogic.FromSettings(ui);
            var plan = WeatherLocationLogic.Plan(ui?.WeatherMode, cache, nowUtc);
            double lat = WeatherLocationLogic.HanoiLat, lon = WeatherLocationLogic.HanoiLon;
            string place = WeatherLocationLogic.HanoiName, via = "mặc định";
            GeoLocation newCache = null;

            if (plan == WeatherSource.Manual)
            {
                via = "chọn tay";
                string city = (ui?.WeatherCity ?? "").Trim();
                if (city.Length > 0)
                {
                    var g = await GeocodeOrFail(get, city);
                    if (g.Outcome != null) return g.Outcome;
                    lat = g.Lat; lon = g.Lon; place = g.Name;
                }
            }
            else if (plan == WeatherSource.GeoCache)
            {
                lat = cache.Lat; lon = cache.Lon; place = cache.DisplayName; via = "tự động theo mạng";
            }
            else // GeoFetch
            {
                var geo = await FetchGeoAsync(get, nowUtc);
                if (geo != null)
                {
                    newCache = geo;
                    lat = geo.Lat; lon = geo.Lon; place = geo.DisplayName; via = "tự động theo mạng";
                }
                else
                {
                    var fb = WeatherLocationLogic.FallbackAfterGeoFailure(cache,
                        !string.IsNullOrWhiteSpace(ui?.WeatherCity));
                    if (fb == WeatherSource.StaleGeoCache)
                    {
                        lat = cache.Lat; lon = cache.Lon; place = cache.DisplayName; via = "tự động (cache cũ)";
                    }
                    else if (fb == WeatherSource.Manual)
                    {
                        via = "chọn tay (dự phòng)";
                        var g = await GeocodeOrFail(get, ui.WeatherCity.Trim());
                        if (g.Outcome != null) { g.Outcome.NewGeoCache = newCache; return g.Outcome; }
                        lat = g.Lat; lon = g.Lon; place = g.Name;
                    }
                }
            }

            string fcJson;
            try { fcJson = await get(WeatherService.BuildForecastUrl(lat, lon)); }
            catch { return WithCache(Fail(WeatherFailKind.Network, NetworkErrorMessage), newCache); }
            var w = WeatherService.ParseForecast(fcJson, place);
            if (w == null)
                return WithCache(Fail(WeatherFailKind.BadData, BadDataMessage), newCache);
            return new WeatherOutcome
            {
                Ok = true,
                Weather = w,
                Place = place,
                Via = via,
                NewGeoCache = newCache,
                UserMessage = place + ": " + w.TempC + "°C · " + w.Description + " · độ ẩm " + w.Humidity + "%",
            };
        }

        /// <summary>Do vi tri qua IP: ipapi.co truoc; loi/bi gioi han tan
        /// suat thi thu ip-api.com (parser da co tu v3.11 nhung ban cu
        /// khong bao gio goi).</summary>
        private static async Task<GeoLocation> FetchGeoAsync(Func<string, Task<string>> get, DateTime nowUtc)
        {
            try
            {
                var g = WeatherLocationLogic.ParseIpApiCo(await get("https://ipapi.co/json/"), nowUtc);
                if (g != null) return g;
            }
            catch { }
            try
            {
                return WeatherLocationLogic.ParseIpApiCom(
                    await get("http://ip-api.com/json/?fields=status,city,country,lat,lon"), nowUtc);
            }
            catch { return null; }
        }

        private struct GeoResult
        {
            public double Lat, Lon;
            public string Name;
            public WeatherOutcome Outcome; // khac null = that bai, dung luong
        }

        private static async Task<GeoResult> GeocodeOrFail(Func<string, Task<string>> get, string city)
        {
            string json;
            try { json = await get(WeatherService.BuildGeocodeUrl(city)); }
            catch { return new GeoResult { Outcome = Fail(WeatherFailKind.Network, NetworkErrorMessage) }; }
            var g = WeatherService.ParseGeocode(json, city);
            if (!g.HasValue)
                return new GeoResult
                {
                    Outcome = Fail(WeatherFailKind.CityNotFound,
                        "Không tìm thấy thành phố “" + city + "” — kiểm tra lại tên rồi bấm Cập nhật để thử lại"),
                };
            return new GeoResult { Lat = g.Value.Lat, Lon = g.Value.Lon, Name = g.Value.Name };
        }

        private static WeatherOutcome Fail(WeatherFailKind kind, string message)
            => new WeatherOutcome { Ok = false, Fail = kind, UserMessage = message };

        private static WeatherOutcome WithCache(WeatherOutcome o, GeoLocation cache)
        {
            if (cache != null) o.NewGeoCache = cache;
            return o;
        }
    }
}
