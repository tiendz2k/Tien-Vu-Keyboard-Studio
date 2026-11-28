using System;
using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.15 — NOW PLAYING THAT: logic THUAN (test duoc) cho widget Dang
    // phat. Phan cham WinRT (GSMTC — Windows.Media.Control) nam o
    // NowPlayingService.cs (chi Windows, khong vao project test); o day
    // chi co POCO + logic chon session + noi suy tien trinh + dinh dang
    // thoi gian.
    // =====================================================================

    /// <summary>Ban chup trang thai mot session media (tu GSMTC).</summary>
    public sealed class MediaSessionInfo
    {
        public bool Available { get; set; }
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public double PositionSec { get; set; }
        public double DurationSec { get; set; }
        public bool IsPlaying { get; set; }
        /// <summary>v3.16: session nay co phai "session hien tai" cua
        /// GSMTC (session dang giu phim media — GetCurrentSession)
        /// khong. CHI dung lam moc pha hoa khi cac moc thoi gian bang
        /// nhau: mot session hien tai dang PAUSED khong bao gio thang
        /// mot session dang PLAYING (bai hoc tu ModernFlyouts /
        /// WebNowPlaying — MIT).</summary>
        public bool IsCurrentSession { get; set; }
        public string SourceName { get; set; } = "";
        /// <summary>Moc thoi gian (UTC) ma PositionSec duoc do — GSMTC
        /// chi cho anh chup, phai noi suy theo moc nay khi dang phat.</summary>
        public DateTime LastUpdatedUtc { get; set; } = DateTime.MinValue;

        public static MediaSessionInfo None => new MediaSessionInfo { Available = false };
    }

    public static class NowPlayingLogic
    {
        /// <summary>
        /// Chon session de hien khi co nhieu nguon cung phat (v3.16 —
        /// mau chon session cua ModernFlyouts/WebNowPlaying, MIT):
        /// 1) Session dang Playing THANG session giu phim media dang
        ///    Paused — GetCurrentSession() co the ket o mot tab trinh
        ///    duyet da dung trong khi Spotify dang phat.
        /// 2) Trong cung nhom Playing (hoac cung nhom Paused): session
        ///    duoc cap nhat GAN NHAT thang; bang moc thoi gian thi
        ///    session hien tai (IsCurrentSession) thang.
        /// Session thieu Title bi bo qua. Khong hop le -> null.
        /// </summary>
        public static MediaSessionInfo ChooseSession(IReadOnlyList<MediaSessionInfo> sessions)
        {
            if (sessions == null) return null;
            MediaSessionInfo bestPlaying = null, bestPaused = null;
            foreach (var s in sessions)
            {
                if (s == null || !s.Available || string.IsNullOrWhiteSpace(s.Title)) continue;
                if (s.IsPlaying) { if (IsBetterCandidate(s, bestPlaying)) bestPlaying = s; }
                else if (IsBetterCandidate(s, bestPaused)) bestPaused = s;
            }
            return bestPlaying ?? bestPaused;
        }

        /// <summary>Ung vien thang ung vien dang giu: moc cap nhat moi
        /// hon, hoac bang moc thi co co session-hien-tai.</summary>
        private static bool IsBetterCandidate(MediaSessionInfo cand, MediaSessionInfo cur)
        {
            if (cur == null) return true;
            if (cand.LastUpdatedUtc != cur.LastUpdatedUtc)
                return cand.LastUpdatedUtc > cur.LastUpdatedUtc;
            return cand.IsCurrentSession && !cur.IsCurrentSession;
        }

        /// <summary>
        /// Vi tri phat tai thoi diem utcNow: dang Playing thi cong them
        /// thoi gian da troi ke tu LastUpdatedUtc (kep trong [0,
        /// Duration]); Paused thi giu nguyen anh chup.
        /// </summary>
        public static double ProgressAt(MediaSessionInfo info, DateTime utcNow)
        {
            if (info == null || !info.Available) return 0;
            double pos = info.PositionSec;
            if (info.IsPlaying && info.LastUpdatedUtc != DateTime.MinValue)
            {
                double dt = (utcNow - info.LastUpdatedUtc).TotalSeconds;
                if (dt > 0) pos += dt;
            }
            if (pos < 0) pos = 0;
            if (info.DurationSec > 0 && pos > info.DurationSec) pos = info.DurationSec;
            return pos;
        }

        /// <summary>Dinh dang giay -> "m:ss" (giong mau WidgetRenderer.Fmt).</summary>
        public static string FormatTime(double sec)
        {
            if (sec < 0 || double.IsNaN(sec) || double.IsInfinity(sec)) sec = 0;
            int s = (int)sec;
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
