using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Bang thiet bi cong dong (v3.7): file JSON co the cap nhat rieng ma
    // khong can phat hanh lai app. App chi tai ve khi nguoi dung BAM nut
    // "Kiem tra cap nhat bang thiet bi" trong trang Cai dat (khong tu
    // dong, khong chay nen). File tai ve luu tai
    // %AppData%\TienKeyboardStudio\devices-registry.json va duoc ap dung
    // o lan mo app SAU; bang tich hop san luon la phuong an du phong.
    // Dinh dang JSON duoc mo ta trong README (muc "Bang thiet bi").
    // =====================================================================
    public sealed class DeviceRegistry
    {
        public const string FileName = "devices-registry.json";
        public const string RawUrl =
            "https://raw.githubusercontent.com/tiendz2k/T-Keyboard-Studio/main/docs/devices-registry.json";
        public const int MinEntries = 10;
        public const int MaxEntries = 5000;

        public int Version { get; set; } = 1;
        public string Updated { get; set; } = "";
        public List<DeviceRegistryEntry> Devices { get; set; } = new();

        public static string LocalPath => Path.Combine(GifLibraryStore.RootDir, FileName);

        public static DeviceRegistry Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<DeviceRegistry>(json, JsonOpts);
            }
            catch
            {
                return null;
            }
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>Kiem tra hop le: schema co ban + so muc hop ly + khong
        /// trung ten + moi muc co ten va it nhat mot cap VID/PID hop le.
        /// Tra ve chuoi loi (rong = hop le).</summary>
        public static string Validate(DeviceRegistry reg)
        {
            if (reg == null) return "JSON không đọc được.";
            if (reg.Devices == null) return "Thiếu mảng \"devices\".";
            if (reg.Devices.Count < MinEntries || reg.Devices.Count > MaxEntries)
                return $"Số mục ({reg.Devices.Count}) ngoài khoảng hợp lệ {MinEntries}–{MaxEntries}.";
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in reg.Devices)
            {
                if (d == null || string.IsNullOrWhiteSpace(d.Name))
                    return "Có mục thiếu tên thiết bị.";
                if (!names.Add(d.Name.Trim()))
                    return $"Trùng tên thiết bị: {d.Name}.";
                if (d.Ids == null)
                    return $"Mục \"{d.Name}\" có trường \"ids\" không hợp lệ.";
                // Ids rong duoc chap nhan: muc chi hien thi (chua biet
                // VID:PID, vd cac mau "se tich hop") — khong ghi duoc.
                foreach (var pair in d.Ids)
                {
                    if (pair == null || pair.Count < 1 || pair[0] < 0 || pair[0] > 0xFFFF)
                        return $"Mục \"{d.Name}\" có VID không hợp lệ.";
                    if (pair.Count >= 2 && (pair[1] < -1 || pair[1] > 0xFFFF))
                        return $"Mục \"{d.Name}\" có PID không hợp lệ.";
                }
            }
            return "";
        }

        public static bool TryParseValid(string json, out DeviceRegistry reg, out string error)
        {
            reg = Parse(json);
            error = Validate(reg);
            if (error.Length > 0) reg = null;
            return reg != null;
        }

        /// <summary>Doc bang da tai ve (neu co va hop le). Loi -> null (dung bang tich hop).</summary>
        public static DeviceRegistry LoadDownloaded()
        {
            try
            {
                if (!File.Exists(LocalPath)) return null;
                return TryParseValid(File.ReadAllText(LocalPath), out var reg, out _) ? reg : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Ap dung bang tai ve vao ModelDatabase.All: muc nao trung
        /// ten (khong phan biet hoa thuong) voi bang tich hop thi bo qua —
        /// bang tai ve chi BO SUNG thiet bi moi, khong ghi de du lieu da
        /// kiem chung trong app. Tra ve so muc da them.</summary>
        public static int ApplyTo(List<KeyboardModel> models)
        {
            var reg = LoadDownloaded();
            if (reg == null) return 0;
            var existing = new HashSet<string>(models.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
            int added = 0;
            foreach (var d in reg.Devices)
            {
                if (existing.Contains(d.Name)) continue;
                var model = d.ToModel();
                if (model == null) continue;
                models.Add(model);
                existing.Add(model.Name);
                added++;
            }
            return added;
        }

        /// <summary>Sinh bang JSON tu co so du lieu hien tai (dung cho
        /// viec phat hanh docs/devices-registry.json tren repo).</summary>
        public static DeviceRegistry FromModels(IEnumerable<KeyboardModel> models, int version, string updated)
        {
            var reg = new DeviceRegistry { Version = version, Updated = updated };
            foreach (var m in models)
            {
                var e = new DeviceRegistryEntry
                {
                    Name = m.Name,
                    Brand = m.Brand,
                    Layout = m.Layout,
                    Family = m.Family,
                    Stack = m.Stack,
                    HasScreen = m.HasScreen,
                    ScreenW = m.ScreenW,
                    ScreenH = m.ScreenH,
                    HasHE = m.HasHE,
                    HasKnob = m.HasKnob,
                    StatusKind = m.StatusKind,
                    ProtocolStatus = m.ProtocolStatus,
                    IdNote = m.IdNote,
                    DevIds = m.DevIds.Select(x => (long)x).ToList(),
                    Ids = m.Ids.Select(t => new List<int> { t.Vid, t.Pid }).ToList(),
                };
                reg.Devices.Add(e);
            }
            return reg;
        }

        public string ToJson()
            => JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            });
    }

    public sealed class DeviceRegistryEntry
    {
        public string Name { get; set; } = "";
        public string Brand { get; set; } = "";
        public string Layout { get; set; } = "";
        public string Family { get; set; } = "";
        public string Stack { get; set; } = "";
        public bool HasScreen { get; set; }
        public int ScreenW { get; set; }
        public int ScreenH { get; set; }
        public bool HasHE { get; set; }
        public bool HasKnob { get; set; }
        public int StatusKind { get; set; } = 1;
        public string ProtocolStatus { get; set; } = "";
        public string IdNote { get; set; } = "";
        public List<long> DevIds { get; set; } = new();
        public List<List<int>> Ids { get; set; } = new(); // [vid, pid] (pid -1 = chi theo VID)

        public KeyboardModel ToModel()
        {
            if (string.IsNullOrWhiteSpace(Name)) return null;
            var m = new KeyboardModel
            {
                Name = Name,
                Brand = Brand ?? "",
                Layout = Layout ?? "",
                Family = Family ?? "",
                Stack = Stack ?? "",
                HasScreen = HasScreen,
                ScreenW = ScreenW,
                ScreenH = ScreenH,
                ScreenSize = Math.Max(ScreenW, ScreenH),
                HasHE = HasHE,
                HasKnob = HasKnob,
                StatusKind = StatusKind,
                ProtocolStatus = ProtocolStatus ?? "",
                IdNote = IdNote ?? "",
                DevIds = (DevIds ?? new()).Select(x => (uint)x).ToList(),
                Ids = (Ids ?? new()).Select(p => (p[0], p.Count >= 2 ? p[1] : -1, "Có dây")).ToList(),
            };
            return m;
        }
    }

    // =====================================================================
    // Bao cao thong tin thiet bi (nut "Sao chep thong tin thiet bi"):
    // dinh dang text thuan de dan vao GitHub issue. THUAN LOGIC.
    // =====================================================================
    public sealed class DeviceProbeInfo
    {
        public string VidPid = "";        // "0C45:800A"
        public string ProductName = "";
        public string UsagePageUsage = ""; // "0xFF13 / 0x0001"
        public string ReportSizes = "";    // "in 65 / out 65 / feature 65"
        public string ProbeResults = "";   // tom tat probe (UUID/dev_id/0x0D/0xF5...)
        public string IdentifiedAs = "";   // ket luan nhan dien (neu co)
    }

    public static class DeviceInfoReport
    {
        public static string Build(string appVersion, KeyboardModel selected,
            IEnumerable<DeviceProbeInfo> devices)
        {
            var lines = new List<string>
            {
                "## Thông tin thiết bị (từ T Keyboard Studio)",
                "",
                $"- Phiên bản app: {appVersion}",
                selected != null
                    ? $"- Mẫu đang chọn: {selected.Name} (họ: {(string.IsNullOrEmpty(selected.Family) ? "—" : selected.Family)}, stack: {(string.IsNullOrEmpty(selected.Stack) ? "—" : selected.Stack)})"
                    : "- Mẫu đang chọn: (chưa chọn)",
                "",
            };
            int i = 0;
            foreach (var d in devices ?? Enumerable.Empty<DeviceProbeInfo>())
            {
                i++;
                lines.Add($"### Thiết bị {i}");
                lines.Add($"- VID:PID: {Blank(d.VidPid)}");
                lines.Add($"- Tên sản phẩm (HID): {Blank(d.ProductName)}");
                lines.Add($"- Usage page / usage: {Blank(d.UsagePageUsage)}");
                lines.Add($"- Kích thước report: {Blank(d.ReportSizes)}");
                lines.Add($"- Kết quả probe: {Blank(d.ProbeResults)}");
                if (!string.IsNullOrWhiteSpace(d.IdentifiedAs))
                    lines.Add($"- Nhận diện: {d.IdentifiedAs}");
                lines.Add("");
            }
            if (i == 0)
                lines.Add("(Chưa quét thấy thiết bị nào — hãy cắm bàn phím qua USB rồi bấm Quét lại.)");
            lines.Add("Hệ điều hành: Windows (app chạy trên Windows; thông tin này giúp đối chiếu khi báo lỗi).");
            return string.Join("\n", lines);
        }

        private static string Blank(string s) => string.IsNullOrWhiteSpace(s) ? "(không rõ)" : s.Trim();
    }
}
