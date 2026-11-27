using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.3 — GameSense: loi THUAN cua tinh nang CS2 Game State Integration.
    //
    // CS2 (qua file cfg gamestate_integration_*.cfg ma app tao) tu gui JSON
    // trang thai tran dau ve http://127.0.0.1:<port> — chuan mo cua Valve.
    // App CHI NHAN du lieu game tu gui; khong doc bo nho game, khong inject.
    //
    // File nay khong chua socket/UI: chi co (1) bo phat hien su kien theo
    // canh (edge detection) tren chuoi payload JSON, va (2) bo sinh noi
    // dung file cfg. Ca hai deu unit-test duoc.
    // =====================================================================

    public enum GsiEventKind
    {
        Death,       // mau tu >0 ve 0
        FlashStart,  // do flash vuot nguong (bat dau bi loa)
        FlashEnd,    // do flash xuong duoi nguong (het loa)
        RoundWin,    // round ket thuc va doi cua minh thang
        BombPlanted, // bom vua duoc dat
    }

    /// <summary>
    /// Nhan tung payload JSON cua GSI va tra ve cac su kien vua XAY RA
    /// (phat hien theo canh so voi payload truoc). Payload loi/khong du
    /// truong -> khong su kien, khong nem exception.
    /// </summary>
    public sealed class GsiEventDetector
    {
        /// <summary>Nguong flashed (0..255) coi la "dang bi loa manh".</summary>
        public int FlashThreshold { get; set; } = 60;

        private int _prevHealth = -1;   // -1 = chua biet
        private int _prevFlashed = 0;
        private string _prevPhase = "";
        private string _prevBomb = "";

        public void Reset()
        {
            _prevHealth = -1;
            _prevFlashed = 0;
            _prevPhase = "";
            _prevBomb = "";
        }

        public List<GsiEventKind> Feed(string json)
        {
            var events = new List<GsiEventKind>();
            if (string.IsNullOrWhiteSpace(json)) return events;
            JsonDocument doc;
            try { doc = JsonDocument.Parse(json); }
            catch { return events; }
            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return events;

                // --- player.state ---
                int? health = null;
                int? flashed = null;
                string team = null;
                if (root.TryGetProperty("player", out var player) && player.ValueKind == JsonValueKind.Object)
                {
                    if (player.TryGetProperty("team", out var t) && t.ValueKind == JsonValueKind.String)
                        team = t.GetString();
                    if (player.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Object)
                    {
                        if (state.TryGetProperty("health", out var h) && h.ValueKind == JsonValueKind.Number && h.TryGetInt32(out var hv))
                            health = hv;
                        if (state.TryGetProperty("flashed", out var f) && f.ValueKind == JsonValueKind.Number && f.TryGetInt32(out var fv))
                            flashed = fv;
                    }
                }

                // --- round ---
                string phase = null, winTeam = null, bomb = null;
                if (root.TryGetProperty("round", out var round) && round.ValueKind == JsonValueKind.Object)
                {
                    if (round.TryGetProperty("phase", out var p) && p.ValueKind == JsonValueKind.String)
                        phase = p.GetString();
                    if (round.TryGetProperty("win_team", out var w) && w.ValueKind == JsonValueKind.String)
                        winTeam = w.GetString();
                    if (round.TryGetProperty("bomb", out var b) && b.ValueKind == JsonValueKind.String)
                        bomb = b.GetString();
                }

                // Death: chi khi da tung thay mau > 0 (tranh bao gia luc moi vao tran/spectate)
                if (health.HasValue)
                {
                    if (_prevHealth > 0 && health.Value == 0)
                        events.Add(GsiEventKind.Death);
                    _prevHealth = health.Value;
                }

                // Flash: canh len / canh xuong qua nguong
                if (flashed.HasValue)
                {
                    if (_prevFlashed < FlashThreshold && flashed.Value >= FlashThreshold)
                        events.Add(GsiEventKind.FlashStart);
                    else if (_prevFlashed >= FlashThreshold && flashed.Value < FlashThreshold)
                        events.Add(GsiEventKind.FlashEnd);
                    _prevFlashed = flashed.Value;
                }

                // RoundWin: phase chuyen sang "over" va win_team la doi cua minh
                if (phase != null)
                {
                    if (_prevPhase != "over" && phase == "over" &&
                        !string.IsNullOrEmpty(team) && !string.IsNullOrEmpty(winTeam) &&
                        string.Equals(team, winTeam, StringComparison.OrdinalIgnoreCase))
                        events.Add(GsiEventKind.RoundWin);
                    _prevPhase = phase;
                }

                // BombPlanted: trang thai bom chuyen sang "planted"
                if (bomb != null)
                {
                    if (_prevBomb != "planted" && bomb == "planted")
                        events.Add(GsiEventKind.BombPlanted);
                    _prevBomb = bomb;
                }
            }
            return events;
        }
    }

    /// <summary>Sinh noi dung file cfg GSI cho CS2.</summary>
    public static class GsiConfig
    {
        public const string CfgFileName = "gamestate_integration_tkeyboard.cfg";
        public const string Token = "tk-gsi-33";

        public static string BuildCfg(int port, string token)
        {
            if (port <= 0 || port > 65535) port = 13650;
            if (string.IsNullOrEmpty(token)) token = Token;
            return "\"Tien Vu Keyboard Studio — CS2 GSI\"\n" +
                   "{\n" +
                   $"  \"uri\"           \"http://127.0.0.1:{port}\"\n" +
                   "  \"timeout\"       \"5.0\"\n" +
                   "  \"buffer\"        \"0.1\"\n" +
                   "  \"throttle\"      \"0.1\"\n" +
                   "  \"heartbeat\"     \"15.0\"\n" +
                   $"  \"auth\"          {{ \"token\" \"{token}\" }}\n" +
                   "  \"data\"\n" +
                   "  {\n" +
                   "    \"player_state\"  \"1\"\n" +
                   "    \"player_weapons\" \"1\"\n" +
                   "    \"round\"         \"1\"\n" +
                   "    \"player_id\"     \"1\"\n" +
                   "  }\n" +
                   "}\n";
        }
    }
}
