using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // v3.15 — NOW PLAYING THAT qua GSMTC (Windows.Media.Control).
    // File nay CHI chay tren Windows (WinRT) — khong vao project test,
    // khong duoc khoi tao luc mo app: MainWindow chi EnsureStarted()
    // khi nguoi dung mo widget Dang phat (lazy), moi loi WinRT deu suy
    // thoai thanh Snapshot.Available = false (widget ve khung Idle).
    // Du lieu doc ra chuyen thanh MediaSessionInfo thuan
    // (NowPlayingCore.cs) — chon session + noi suy tien trinh nam o do.
    // HAN CHE DA BIET: hanh vi GSMTC that (dac biet thumbnail + timeline
    // cua tung app phat nhac) chi kiem chung duoc tren Windows that.
    // =====================================================================
    public sealed class NowPlayingService : IDisposable
    {
        private CancellationTokenSource _cts;
        private int _started;
        private string _artKey = "";

        /// <summary>Ban chup moi nhat (doc tu bat ky thread nao).</summary>
        public MediaSessionInfo Snapshot { get; private set; } = MediaSessionInfo.None;

        /// <summary>Thumbnail bai hat dang chon (PNG 46x46) hoac null.</summary>
        public byte[] ArtPng { get; private set; }

        public void EnsureStarted()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0) return;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try { await PollOnceAsync(); }
                    catch { Snapshot = MediaSessionInfo.None; ArtPng = null; _artKey = ""; }
                    try { await Task.Delay(1000, ct); } catch { break; }
                }
            });
        }

        private async Task PollOnceAsync()
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var sessions = manager.GetSessions();
            // v3.16: danh dau "session hien tai" (giu phim media) theo
            // AUMID de NowPlayingLogic pha hoa — session nay dang Paused
            // KHONG duoc thang session dang Playing (xem NowPlayingCore).
            string currentAumid = null;
            try { currentAumid = manager.GetCurrentSession()?.SourceAppUserModelId; } catch { }
            var infos = new List<MediaSessionInfo>();
            var byInfo = new Dictionary<MediaSessionInfo, GlobalSystemMediaTransportControlsSession>();

            foreach (var session in sessions)
            {
                try
                {
                    var props = await session.TryGetMediaPropertiesAsync();
                    var timeline = session.GetTimelineProperties();
                    var playback = session.GetPlaybackInfo();
                    var info = new MediaSessionInfo
                    {
                        Available = true,
                        Title = props?.Title ?? "",
                        Artist = props?.Artist ?? "",
                        Album = props?.AlbumTitle ?? "",
                        PositionSec = timeline != null ? timeline.Position.TotalSeconds : 0,
                        DurationSec = timeline != null
                            ? Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds) : 0,
                        IsPlaying = playback != null &&
                            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                        IsCurrentSession = currentAumid != null &&
                            session.SourceAppUserModelId == currentAumid,
                        SourceName = FriendlySource(session.SourceAppUserModelId),
                        LastUpdatedUtc = timeline != null ? timeline.LastUpdatedTime.UtcDateTime : DateTime.UtcNow,
                    };
                    infos.Add(info);
                    byInfo[info] = session;
                }
                catch { /* mot session loi khong duoc lam hong vong poll */ }
            }

            var chosen = NowPlayingLogic.ChooseSession(infos);
            if (chosen == null)
            {
                Snapshot = MediaSessionInfo.None;
                ArtPng = null;
                _artKey = "";
                return;
            }
            Snapshot = chosen;
            if (byInfo.TryGetValue(chosen, out var chosenSession))
                await UpdateArtAsync(chosenSession, chosen);
        }

        private async Task UpdateArtAsync(GlobalSystemMediaTransportControlsSession session, MediaSessionInfo info)
        {
            string key = (info.Title ?? "") + "|" + (info.Artist ?? "");
            if (key == _artKey) return;
            _artKey = key;
            ArtPng = null;
            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                var thumb = props?.Thumbnail;
                if (thumb == null) return;
                using (IRandomAccessStreamWithContentType stream = await thumb.OpenReadAsync())
                using (var ms = new MemoryStream())
                {
                    await stream.AsStreamForRead().CopyToAsync(ms);
                    ms.Position = 0;
                    using (var img = Image.Load<Rgba32>(ms))
                    {
                        img.Mutate(m => m.Resize(new ResizeOptions
                        {
                            Size = new Size(46, 46),
                            Mode = ResizeMode.Crop,
                            Sampler = KnownResamplers.Bicubic,
                        }));
                        using (var outMs = new MemoryStream())
                        {
                            img.SaveAsPng(outMs);
                            ArtPng = outMs.ToArray();
                        }
                    }
                }
            }
            catch { ArtPng = null; }
        }

        private static string FriendlySource(string aumid)
        {
            if (string.IsNullOrWhiteSpace(aumid)) return "";
            string s = aumid;
            int bang = s.IndexOf('!');
            if (bang > 0) s = s.Substring(0, bang);
            if (s.Length > 28) s = s.Substring(0, 28);
            return s;
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
        }
    }
}
