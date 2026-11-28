using System;
using System.Collections.Generic;
using System.Linq;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.15 — LICH TRINH DEN THEO GIO: logic THUAN (test duoc). Moi luat
    // co gio bat dau (phut trong ngay 0..1439) va mot hanh dong; luat
    // dang hieu luc la luat co gio bat dau <= hien tai muon nhat (khong
    // co luat nao som hon -> luat muon nhat cua HOM QUA, bao quanh
    // ngay). Nguoi dung doi den bang tay thi thang cho den BIEN luat
    // tiep theo (manualOverrideUntil do MainWindow dat bang
    // NextBoundary). Danh gia tra ve mot quyet dinh duy nhat; MainWindow
    // chi ap dung khi ShouldApply = true va ghi nho Key da ap de khong
    // gui lai cung mot luat moi tick.
    // =====================================================================

    /// <summary>Mot luat lich trinh (luu trong LightingStateData.ScheduleRules).</summary>
    public sealed class ScheduleRuleData
    {
        public const int KindBrightness = 0; // chi doi do sang
        public const int KindEffect = 1;     // doi hieu ung firmware
        public const int KindOff = 2;        // tat den (hieu ung 0)

        public int StartMinutes { get; set; } = 480;   // 0..1439 (480 = 08:00)
        public int Kind { get; set; } = KindBrightness;
        public int BrightnessPercent { get; set; } = 60; // 0..100 (Kind = Brightness)
        public int EffectId { get; set; } = 10;          // 0..19 (Kind = Effect)
    }

    public sealed class ScheduleDecision
    {
        public bool ShouldApply;
        public int RuleIndex = -1;      // index trong danh sach DA SAP XEP
        public string Key = "";         // dinh danh lan ap (ngay + luat)
        public ScheduleRuleData Rule;
    }

    public static class LightingSchedule
    {
        /// <summary>Danh sach luat da sap xep theo gio bat dau (on dinh).</summary>
        public static List<ScheduleRuleData> Sorted(IReadOnlyList<ScheduleRuleData> rules)
        {
            var list = (rules ?? new List<ScheduleRuleData>())
                .Where(r => r != null)
                .Select(r => new ScheduleRuleData
                {
                    StartMinutes = Math.Max(0, Math.Min(1439, r.StartMinutes)),
                    Kind = Math.Max(0, Math.Min(2, r.Kind)),
                    BrightnessPercent = Math.Max(0, Math.Min(100, r.BrightnessPercent)),
                    EffectId = Math.Max(0, Math.Min(19, r.EffectId)),
                })
                .OrderBy(r => r.StartMinutes)
                .ToList();
            return list;
        }

        /// <summary>Index (trong danh sach da sap xep) cua luat dang
        /// hieu luc tai "now"; -1 khi khong co luat. Bao quanh ngay:
        /// chua toi luat dau tien thi luat cuoi cua hom qua hieu luc.</summary>
        public static int ActiveRuleIndex(IReadOnlyList<ScheduleRuleData> rules, DateTime now)
        {
            var sorted = Sorted(rules);
            if (sorted.Count == 0) return -1;
            int mins = now.Hour * 60 + now.Minute;
            int idx = -1;
            for (int i = 0; i < sorted.Count; i++)
                if (sorted[i].StartMinutes <= mins) idx = i;
            return idx >= 0 ? idx : sorted.Count - 1;
        }

        /// <summary>Key dinh danh "lan ap" cua luat dang hieu luc: kem
        /// ngay hieu luc (luat bao quanh tu hom qua thi gan ngay hom
        /// qua) de nua dem khong ap lai luat cu, va sang luat moi thi
        /// ap dung ngay.</summary>
        public static string ActiveKey(IReadOnlyList<ScheduleRuleData> rules, DateTime now)
        {
            var sorted = Sorted(rules);
            int idx = ActiveRuleIndex(sorted, now);
            if (idx < 0) return "";
            int mins = now.Hour * 60 + now.Minute;
            var day = sorted[idx].StartMinutes <= mins ? now.Date : now.Date.AddDays(-1);
            return day.ToString("yyyy-MM-dd") + "#" + idx + "@" + sorted[idx].StartMinutes;
        }

        /// <summary>
        /// Quyet dinh ap dung tai thoi diem now:
        /// - Khong co luat -> khong ap.
        /// - Dang trong cua so thang-tay (manualOverrideUntil > now) -> khong ap.
        /// - Key cua luat hien tai == lastAppliedKey -> khong ap (da ap roi).
        /// </summary>
        public static ScheduleDecision Evaluate(
            IReadOnlyList<ScheduleRuleData> rules, DateTime now,
            string lastAppliedKey, DateTime manualOverrideUntil)
        {
            var d = new ScheduleDecision();
            var sorted = Sorted(rules);
            int idx = ActiveRuleIndex(sorted, now);
            if (idx < 0) return d;
            d.RuleIndex = idx;
            d.Rule = sorted[idx];
            d.Key = ActiveKey(sorted, now);
            if (manualOverrideUntil > now) return d;
            if (string.Equals(lastAppliedKey ?? "", d.Key, StringComparison.Ordinal)) return d;
            d.ShouldApply = true;
            return d;
        }

        /// <summary>Thoi diem bien luat TIEP THEO sau now (khi doi tay
        /// thang toi do). Khong co luat -> DateTime.MaxValue.</summary>
        public static DateTime NextBoundary(IReadOnlyList<ScheduleRuleData> rules, DateTime now)
        {
            var sorted = Sorted(rules);
            if (sorted.Count == 0) return DateTime.MaxValue;
            int mins = now.Hour * 60 + now.Minute;
            foreach (var r in sorted)
                if (r.StartMinutes > mins)
                    return now.Date.AddMinutes(r.StartMinutes);
            return now.Date.AddDays(1).AddMinutes(sorted[0].StartMinutes);
        }

        /// <summary>Do sang UI cua firmware (BrightnessUi 0..4, cong 1
        /// thanh muc 1..5 khi gui) tu phan tram 0..100.</summary>
        public static int BrightnessUiFromPercent(int percent)
        {
            int p = Math.Max(0, Math.Min(100, percent));
            return Math.Max(0, Math.Min(4, (int)Math.Round(p / 100.0 * 4)));
        }

        /// <summary>"HH:mm" -> phut trong ngay; loi -> fallback.</summary>
        public static int ParseTimeToMinutes(string text, int fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            var parts = text.Trim().Split(':');
            if (parts.Length != 2) return fallback;
            if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m)) return fallback;
            if (h < 0 || h > 23 || m < 0 || m > 59) return fallback;
            return h * 60 + m;
        }

        public static string FormatMinutes(int minutes)
        {
            int m = Math.Max(0, Math.Min(1439, minutes));
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }
    }
}
