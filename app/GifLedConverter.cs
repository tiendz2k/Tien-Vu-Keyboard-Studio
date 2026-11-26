using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // GIF -> LED: doc tung frame GIF (bat ky kich thuoc, ke ca GIF dai
    // 900x281 cua shop) va lay mau tai TAM RECT cua tung phim theo hinh hoc
    // XML cua F75 Max (MaxKeyData.StreamSlots). Ket qua moi frame:
    // - Slots: 80 o stream 0x20 (id + RGB) de gui/log qua L99Protocol.
    // - ViewColors: mau theo index phim cua KeyboardView de phat trong app.
    // =====================================================================
    public sealed class GifLedAnimation
    {
        public string Name;
        public int SourceWidth, SourceHeight, SourceFrames;
        public List<L99Protocol.StreamQuad[]> Frames = new List<L99Protocol.StreamQuad[]>();
        public List<Dictionary<int, (byte R, byte G, byte B)>> ViewFrames = new List<Dictionary<int, (byte, byte, byte)>>();
        public List<int> DelaysMs = new List<int>();
    }

    public static class GifLedConverter
    {
        public const int MaxLedFrames = 300;

        public static GifLedAnimation Convert(string path)
        {
            var anim = new GifLedAnimation { Name = Path.GetFileNameWithoutExtension(path) };
            using (var img = Image.Load<Rgba32>(path))
            {
                anim.SourceWidth = img.Width;
                anim.SourceHeight = img.Height;
                anim.SourceFrames = img.Frames.Count;
                int take = Math.Min(img.Frames.Count, MaxLedFrames);
                // Neu GIF qua dai: lay mau deu de khong qua MaxLedFrames frame.
                double step = img.Frames.Count > MaxLedFrames ? (double)img.Frames.Count / MaxLedFrames : 1.0;
                for (int f = 0; f < take; f++)
                {
                    int srcIdx = (int)(f * step);
                    var frame = img.Frames[srcIdx];
                    var quads = new L99Protocol.StreamQuad[MaxKeyData.StreamSlots.Length];
                    var view = new Dictionary<int, (byte, byte, byte)>();
                    for (int s = 0; s < MaxKeyData.StreamSlots.Length; s++)
                    {
                        var slot = MaxKeyData.StreamSlots[s];
                        int x = Math.Min(frame.Width - 1, Math.Max(0, (int)Math.Round(slot.CX * (frame.Width - 1))));
                        int y = Math.Min(frame.Height - 1, Math.Max(0, (int)Math.Round(slot.CY * (frame.Height - 1))));
                        var px = frame[x, y];
                        quads[s] = new L99Protocol.StreamQuad(slot.Id, px.R, px.G, px.B);
                        view[slot.ViewIndex] = (px.R, px.G, px.B);
                    }
                    anim.Frames.Add(quads);
                    anim.ViewFrames.Add(view);
                    int delay = 100;
                    try
                    {
                        var meta = frame.Metadata.GetGifMetadata();
                        if (meta != null && meta.FrameDelay > 0) delay = meta.FrameDelay * 10; // don vi 10ms
                    }
                    catch { }
                    anim.DelaysMs.Add(Math.Max(30, Math.Min(1000, delay)));
                }
            }
            return anim;
        }
    }
}
