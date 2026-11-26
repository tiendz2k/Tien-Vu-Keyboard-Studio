using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TienKeyboardStudio3
{
    public enum FitMode { Fill, Fit, Stretch }

    public sealed class GifInfo
    {
        public string Path;
        public int SourceWidth;
        public int SourceHeight;
        public int FrameCount;      // so frame se dung (da cat o 255)
        public int SourceFrames;    // so frame goc trong file
        public int StreamBytes;
        public int ChunkCount;
        public bool Capped;         // file co > 255 frame
    }

    /// <summary>
    /// Ma hoa anh/GIF thanh stream tai len man hinh F75 Max, dung ImageSharp
    /// (thuan managed, khong dung System.Drawing nhu v2).
    /// Dinh dang stream GIAU NGUYEN nhu v2 - da doi chieu mohin7 + VitalyArt:
    ///  - Header 256 byte: [0] = so frame, [1+i] = delay frame i, don vi 2 ms,
    ///    delay kep trong 30..500 ms.
    ///  - Moi frame: 128x128 RGB565 little-endian (32768 byte).
    ///  - Stream dem 0 toi boi so cua chunk 4096 byte.
    /// </summary>
    public static class DisplayEncoder
    {
        public static GifInfo Probe(string path)
        {
            using (var img = Image.Load<Rgba32>(path))
            {
                int frames = img.Frames.Count;
                var info = new GifInfo
                {
                    Path = path,
                    SourceWidth = img.Width,
                    SourceHeight = img.Height,
                    SourceFrames = frames,
                    FrameCount = Math.Min(frames, F75Protocol.MaxFrames),
                    Capped = frames > F75Protocol.MaxFrames,
                };
                int payload = F75Protocol.HeaderLength + info.FrameCount * F75Protocol.FrameBytes;
                info.ChunkCount = (payload + F75Protocol.ChunkLength - 1) / F75Protocol.ChunkLength;
                info.StreamBytes = info.ChunkCount * F75Protocol.ChunkLength;
                return info;
            }
        }

        public static byte[] Encode(string path, FitMode fit, Action<string> log)
        {
            using (var img = Image.Load<Rgba32>(path))
            {
                int srcFrames = img.Frames.Count;
                int frames = Math.Min(srcFrames, F75Protocol.MaxFrames);
                if (srcFrames > frames && log != null)
                    log("Canh bao: file co " + srcFrames + " frame, chi lay " + frames + " frame dau (gioi han firmware).");

                int payload = F75Protocol.HeaderLength + frames * F75Protocol.FrameBytes;
                int chunks = (payload + F75Protocol.ChunkLength - 1) / F75Protocol.ChunkLength;
                var stream = new byte[chunks * F75Protocol.ChunkLength];

                stream[0] = (byte)frames;
                for (int i = 0; i < frames; i++)
                {
                    int ms = FrameDelayMs(img, i);
                    if (ms <= 0) ms = 100;
                    int clamped = Math.Min(500, Math.Max(30, ms));
                    stream[1 + i] = (byte)Math.Round(clamped / 2.0);
                }

                var mode = fit == FitMode.Fill ? ResizeMode.Crop
                         : fit == FitMode.Fit ? ResizeMode.Pad
                         : ResizeMode.Stretch;

                for (int i = 0; i < frames; i++)
                {
                    using (var frame = img.Frames.CloneFrame(i))
                    {
                        frame.Mutate(m => m.Resize(new ResizeOptions
                        {
                            Size = new Size(F75Protocol.ScreenWidth, F75Protocol.ScreenHeight),
                            Mode = mode,
                            PadColor = Color.Black,
                            Sampler = KnownResamplers.Bicubic,
                        }));
                        int offset = F75Protocol.HeaderLength + i * F75Protocol.FrameBytes;
                        WriteRgb565(frame, stream, offset);
                    }
                }
                return stream;
            }
        }

        private static int FrameDelayMs(Image<Rgba32> img, int index)
        {
            try
            {
                var meta = img.Frames[index].Metadata.GetGifMetadata();
                if (meta != null) return meta.FrameDelay * 10; // GIF delay: don vi 1/100 s
            }
            catch { }
            return 100;
        }

        private static void WriteRgb565(Image<Rgba32> frame, byte[] stream, int offset)
        {
            frame.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    int rowOff = offset + y * F75Protocol.ScreenWidth * 2;
                    for (int x = 0; x < row.Length; x++)
                    {
                        var p = row[x];
                        ushort v = (ushort)(((p.R >> 3) << 11) | ((p.G >> 2) << 5) | (p.B >> 3));
                        int o = rowOff + x * 2;
                        stream[o] = (byte)(v & 0xFF);
                        stream[o + 1] = (byte)(v >> 8);
                    }
                }
            });
        }

        /// <summary>Frame dau tien, thu ve size yeu cau - dung lam thumbnail.</summary>
        public static Image<Rgba32> LoadThumbnail(string path, int size)
        {
            using (var img = Image.Load<Rgba32>(path))
            {
                var frame = img.Frames.CloneFrame(0);
                frame.Mutate(m => m.Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Pad,
                    PadColor = Color.Black,
                    Sampler = KnownResamplers.Bicubic,
                }));
                return frame;
            }
        }
    }
}
