using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TienKeyboardStudio3
{
    public enum GifFitMode { Fill, Fit, Stretch }

    public sealed class GifEntry
    {
        public string Name;        // ten hien thi
        public string Group;       // nhom: NganHa / PixelGame / ThuGian / Effects
        public string GroupLabel;  // nhan tieng Viet
        public Uri AssetUri;       // avares://...
        public int FrameCount;     // so frame trong file (doc khi can)
    }

    /// <summary>
    /// Doc GIF nhung san trong exe (AvaloniaResource) va giai ma frame sang
    /// Avalonia Bitmap bang ImageSharp. Khong dung mang, khong dung HID.
    /// </summary>
    public static class GifTools
    {
        public const string Asm = "TienKeyboardStudio";

        public static Uri Asset(string rel) => new Uri("avares://" + Asm + "/" + rel);

        private static readonly Dictionary<string, byte[]> ByteCache = new Dictionary<string, byte[]>();

        public static byte[] LoadBytes(Uri uri)
        {
            string key = uri.ToString();
            lock (ByteCache)
            {
                if (ByteCache.TryGetValue(key, out var cached)) return cached;
            }
            using (var s = AssetLoader.Open(uri))
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                var bytes = ms.ToArray();
                lock (ByteCache) ByteCache[key] = bytes;
                return bytes;
            }
        }

        public static int ProbeFrameCount(Uri uri)
        {
            try
            {
                var bytes = LoadBytes(uri);
                using (var img = Image.Load<Rgba32>(bytes)) return img.Frames.Count;
            }
            catch { return 0; }
        }

        /// <summary>Frame dau tien (hoac frame chi dinh) o kich thuoc size×size, che do Pad.</summary>
        public static Bitmap LoadFrame(Uri uri, int size, int frameIndex = 0)
        {
            var bytes = LoadBytes(uri);
            using (var img = Image.Load<Rgba32>(bytes))
            {
                int idx = Math.Max(0, Math.Min(frameIndex, img.Frames.Count - 1));
                using (var f = img.Frames.CloneFrame(idx))
                {
                    f.Mutate(m => m.Resize(new ResizeOptions
                    {
                        Size = new Size(size, size),
                        Mode = ResizeMode.Pad,
                        PadColor = Color.Black,
                        Sampler = KnownResamplers.Bicubic,
                    }));
                    return ToBitmap(f);
                }
            }
        }

        /// <summary>
        /// Giai ma toi da maxFrames frame ve size×size theo che do Fill/Fit/Stretch
        /// (dung pipeline chuan hoa 128×128 giong ban that se dung cho man hinh TFT).
        /// </summary>
        public static List<Bitmap> LoadFrames(Uri uri, int size, GifFitMode fit, int maxFrames)
        {
            var list = new List<Bitmap>();
            var bytes = LoadBytes(uri);
            var mode = fit == GifFitMode.Fill ? ResizeMode.Crop
                     : fit == GifFitMode.Fit ? ResizeMode.Pad
                     : ResizeMode.Stretch;
            using (var img = Image.Load<Rgba32>(bytes))
            {
                int n = Math.Min(img.Frames.Count, maxFrames);
                // Neu GIF dai, lay mau deu de vong lap dai hon ma van nhe
                int step = 1;
                if (img.Frames.Count > maxFrames) step = Math.Max(1, img.Frames.Count / maxFrames);
                for (int i = 0; i < img.Frames.Count && list.Count < maxFrames; i += step)
                {
                    using (var f = img.Frames.CloneFrame(i))
                    {
                        f.Mutate(m => m.Resize(new ResizeOptions
                        {
                            Size = new Size(size, size),
                            Mode = mode,
                            PadColor = Color.Black,
                            Sampler = KnownResamplers.Bicubic,
                        }));
                        list.Add(ToBitmap(f));
                    }
                }
            }
            return list;
        }

        public static Bitmap ToBitmap(Image<Rgba32> frame)
        {
            int w = frame.Width, h = frame.Height;
            var wb = new WriteableBitmap(new Avalonia.PixelSize(w, h), new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Opaque);
            using (var fb = wb.Lock())
            {
                int stride = fb.RowBytes;
                var buffer = new byte[stride * h];
                frame.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x = 0; x < row.Length; x++)
                        {
                            var p = row[x];
                            int o = y * stride + x * 4;
                            buffer[o] = p.B; buffer[o + 1] = p.G; buffer[o + 2] = p.R; buffer[o + 3] = 255;
                        }
                    }
                });
                System.Runtime.InteropServices.Marshal.Copy(buffer, 0, fb.Address, buffer.Length);
            }
            return wb;
        }

        public static void DisposeFrames(IEnumerable<Bitmap> frames)
        {
            if (frames == null) return;
            foreach (var f in frames) { try { f?.Dispose(); } catch { } }
        }

        // ---- Ban file (v3.2): nap frame tu file tren dia (thu vien nguoi dung,
        // cache online) — cung pipeline chuan hoa nhu ban avares. ----
        public static int ProbeFrameCountFile(string path)
        {
            try { using (var img = Image.Load<Rgba32>(path)) return img.Frames.Count; }
            catch { return 0; }
        }

        public static Bitmap LoadFrameFile(string path, int size)
        {
            using (var img = Image.Load<Rgba32>(path))
            {
                using (var f = img.Frames.CloneFrame(0))
                {
                    f.Mutate(m => m.Resize(new ResizeOptions
                    {
                        Size = new Size(size, size),
                        Mode = ResizeMode.Pad,
                        PadColor = Color.Black,
                        Sampler = KnownResamplers.Bicubic,
                    }));
                    return ToBitmap(f);
                }
            }
        }

        public static List<Bitmap> LoadFramesFile(string path, int size, GifFitMode fit, int maxFrames)
        {
            var list = new List<Bitmap>();
            var mode = fit == GifFitMode.Fill ? ResizeMode.Crop
                     : fit == GifFitMode.Fit ? ResizeMode.Pad
                     : ResizeMode.Stretch;
            using (var img = Image.Load<Rgba32>(path))
            {
                int step = 1;
                if (img.Frames.Count > maxFrames) step = Math.Max(1, img.Frames.Count / maxFrames);
                for (int i = 0; i < img.Frames.Count && list.Count < maxFrames; i += step)
                {
                    using (var f = img.Frames.CloneFrame(i))
                    {
                        f.Mutate(m => m.Resize(new ResizeOptions
                        {
                            Size = new Size(size, size),
                            Mode = mode,
                            PadColor = Color.Black,
                            Sampler = KnownResamplers.Bicubic,
                        }));
                        list.Add(ToBitmap(f));
                    }
                }
            }
            return list;
        }
    }

    /// <summary>Danh muc GIF tich hop: 21 GIF thu vien + 16 hieu ung cong dong.</summary>
    public static class GifCatalog
    {
        public static readonly (string Group, string Label, string[] Files)[] LibraryGroups =
        {
            ("NganHa", "Ngân hà", new[]
            {
                "Galaxy-Loading-128.gif", "Tien-Galaxy-Pulse-128.gif", "Tien-Galaxy-Spiral-128.gif",
                "Tien-Galaxy-Zoom-128.gif", "Tien-Nebula-Wave-128.gif", "Tien-Orbit-Planet-128.gif",
                "Tien-Star-Warp-128.gif",
            }),
            ("PixelGame", "Pixel & Game", new[]
            {
                "GG-Game-128.gif", "Matrix-Rain-128.gif", "Pixel-Heart-128.gif", "Pixel-Invader-128.gif",
            }),
            ("ThuGian", "Thư giãn", new[]
            {
                "Coffee-Time-128.gif", "Fire-Flame-128.gif", "Moon-Night-128.gif", "Ocean-Sunset-128.gif",
                "Rain-Night-128.gif", "Rocket-Launch-128.gif", "Tien-Galaxy-Tien-128.gif",
                "Tien-Music-EQ-128.gif", "Tien-Name-Bounce-128.gif", "Vinyl-Spin-128.gif",
            }),
        };

        public static List<GifEntry> Library()
        {
            var list = new List<GifEntry>();
            foreach (var (group, label, files) in LibraryGroups)
            {
                foreach (var f in files)
                {
                    list.Add(new GifEntry
                    {
                        Name = PrettyName(f),
                        Group = group,
                        GroupLabel = label,
                        AssetUri = GifTools.Asset("Assets/Gifs/Library/" + group + "/" + f),
                    });
                }
            }
            return list;
        }

        public static string PrettyName(string file)
        {
            string n = Path.GetFileNameWithoutExtension(file);
            if (n.EndsWith("-128")) n = n.Substring(0, n.Length - 4);
            return n.Replace('-', ' ');
        }
    }
}
