using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Dung "stream" day len man hinh TFT (cung dinh dang voi DisplayEncoder
    // cua duong verified): header 256B ([0]=so frame, [1+i]=delay theo don
    // vi 2ms, kep 30..500ms) + tung frame RGB565 little-endian 32768B.
    // Dung cho Widget TFT (frame render boi host) — day qua
    // F75Device.UploadDisplay nhu GIF binh thuong.
    // =====================================================================
    public static class DisplayStreamBuilder
    {
        public static byte[] Build(IReadOnlyList<Image<Rgba32>> frames, int delayMsEach = 1000)
        {
            if (frames == null || frames.Count == 0) throw new ArgumentException("Can it nhat 1 frame.");
            if (frames.Count > F75Protocol.MaxFrames) throw new ArgumentException($"Toi da {F75Protocol.MaxFrames} frame.");
            int chunks = (int)Math.Ceiling((F75Protocol.HeaderLength + (long)frames.Count * F75Protocol.FrameBytes) / (double)F75Protocol.ChunkLength);
            var stream = new byte[chunks * F75Protocol.ChunkLength];
            stream[0] = (byte)frames.Count;
            int delayUnits = Math.Max(15, Math.Min(250, delayMsEach / 2)); // 30..500ms
            for (int i = 0; i < frames.Count; i++) stream[1 + i] = (byte)delayUnits;
            for (int f = 0; f < frames.Count; f++)
            {
                var img = frames[f];
                if (img.Width != 128 || img.Height != 128)
                    throw new ArgumentException("Frame widget phai dung 128x128.");
                int baseOff = F75Protocol.HeaderLength + f * F75Protocol.FrameBytes;
                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < 128; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < 128; x++)
                        {
                            var px = row[x];
                            int v = ((px.R >> 3) << 11) | ((px.G >> 2) << 5) | (px.B >> 3);
                            int off = baseOff + (y * 128 + x) * 2;
                            stream[off] = (byte)(v & 0xFF);
                            stream[off + 1] = (byte)((v >> 8) & 0xFF);
                        }
                    }
                });
            }
            return stream;
        }
    }
}
