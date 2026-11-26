using System;
using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    /// <summary>
    /// Bo xay dung goi lenh HID cho AULA F75 Max (kenh lenh co day USB-C).
    ///
    /// Giao thuc duoc doi chieu tu HAI ma nguon mo doc lap, ca hai deu da
    /// xac thuc tren phan cung that:
    ///  1) mohin7/aula-f75max ("AULA Studio", MIT) - AulaWiredProtocol.swift,
    ///     ghi chu "Verified on hardware 2026-09-15".
    ///  2) VitalyArt/Aula-F75-Max-Driver (MIT) - AulaDevice.swift,
    ///     WirelessAulaDevice.swift (applyWiredStandardRGB), DisplayEncoder.swift.
    /// Hai nguon khop nhau tung byte cho: kenh lenh (usage page 0xFF13,
    /// feature report 64 byte, report ID 0), lenh RGB, dong bo gio,
    /// va giao thuc tai GIF (chunk 4096 byte qua usage page 0xFF68).
    ///
    /// Day chi la ham thuan (pure) - khong cham vao thiet bi.
    /// </summary>
    public static class F75Protocol
    {
        public const int VendorId = 0x0C45;
        public const int ProductId = 0x800A;

        public const int CommandUsagePage = 0xFF13;   // kenh lenh: feature report 64 byte
        public const int DisplayUsagePage = 0xFF68;   // kenh man hinh: output report 4096 byte
        public const int PacketLength = 64;
        public const int ChunkLength = 4096;
        public const int HeaderLength = 256;
        public const int ScreenWidth = 128;
        public const int ScreenHeight = 128;
        public const int FrameBytes = ScreenWidth * ScreenHeight * 2; // RGB565
        public const int MaxFrames = 255;

        // Ma lenh (byte[1], sau byte[0] = 0x04)
        public const byte CmdBegin = 0x18;
        public const byte CmdApply = 0x02;
        public const byte CmdFinish = 0xF0;
        public const byte CmdSelectLighting = 0x13;
        public const byte CmdSelectClock = 0x28;
        public const byte CmdSelectDisplay = 0x72;

        /// <summary>Mot buoc trong chuoi lenh: goi 64 byte + cach cho phan hoi.</summary>
        public sealed class Step
        {
            public string Name;
            public byte[] Bytes;
            /// <summary>true: doc GET_REPORT cho co ACK (byte[3]==0x01). false: chi gui.</summary>
            public bool ExpectAck;
            /// <summary>true: doc phan hoi nhung khong bat buoc co ACK (goi du lieu, firmware phan hoi nguyen van).</summary>
            public bool ReadEcho;

            public Step(string name, byte[] bytes, bool expectAck, bool readEcho = false)
            {
                Name = name;
                Bytes = bytes;
                ExpectAck = expectAck;
                ReadEcho = readEcho;
            }
        }

        private static byte[] Command(byte code, IDictionary<int, byte> fields = null)
        {
            var p = new byte[PacketLength];
            p[0] = 0x04;
            p[1] = code;
            if (fields != null)
                foreach (var kv in fields) p[kv.Key] = kv.Value;
            return p;
        }

        // ------------------------------------------------------------------
        // RGB LIGHTING
        // Chuoi: begin -> select(0x13,[8]=1) -> payload -> apply(0x02) -> finish(0xF0)
        // Payload: [0]=effect, [1..3]=R,G,B, [8]=multicolor, [9]=brightness,
        //          [10]=speed, [11]=direction, [14]=0xAA, [15]=0x55
        // Firmware nhan brightness/speed trong khoang 1..5 (ca hai nguon deu
        // clamp 1..5). Giao dien nguoi dung dung thang 0..4, nen tang gui
        // xuong day se cong +1 khi goi ham nay (xem MainForm).
        // ------------------------------------------------------------------
        public static List<Step> BuildLighting(int effect, int r, int g, int b,
            bool multicolor, int brightnessWire, int speedWire, int direction)
        {
            var payload = new byte[PacketLength];
            payload[0] = (byte)effect;
            if (effect != 0)
            {
                payload[1] = (byte)r;
                payload[2] = (byte)g;
                payload[3] = (byte)b;
                payload[8] = (byte)(multicolor ? 1 : 0);
                payload[9] = (byte)brightnessWire;
                payload[10] = (byte)speedWire;
                payload[11] = (byte)direction;
            }
            payload[14] = 0xAA;
            payload[15] = 0x55;

            return new List<Step>
            {
                new Step("RGB begin (0x04 0x18)", Command(CmdBegin), expectAck: true),
                new Step("RGB select lighting (0x04 0x13)", Command(CmdSelectLighting, new Dictionary<int, byte> { { 8, 0x01 } }), expectAck: true),
                new Step("RGB payload", payload, expectAck: false),
                new Step("RGB apply (0x04 0x02)", Command(CmdApply), expectAck: true),
                new Step("RGB finish/save (0x04 0xF0)", Command(CmdFinish), expectAck: false),
            };
        }

        // ------------------------------------------------------------------
        // CLOCK SYNC
        // Chuoi: begin -> select(0x28,[8]=1) -> time payload -> apply(0x02)
        // Time payload: [0]=0x00 [1]=0x01 [2]=0x5A [3]=nam-2000 [4]=thang
        //   [5]=ngay [6]=gio [7]=phut [8]=giay [10]=thu (0=Chu nhat)
        //   [62]=0xAA [63]=0x55
        // Luu y: ban VitalyArt hardcode byte[3]=0x1A (2026); ban mohin7 tinh
        // dung nam-2000 va da xac thuc phan cung - ta lam theo mohin7.
        // ------------------------------------------------------------------
        public static List<Step> BuildClockSync(DateTime now)
        {
            var t = new byte[PacketLength];
            t[0] = 0x00;
            t[1] = 0x01;
            t[2] = 0x5A;
            t[3] = (byte)((now.Year - 2000) % 100);
            t[4] = (byte)now.Month;
            t[5] = (byte)now.Day;
            t[6] = (byte)now.Hour;
            t[7] = (byte)now.Minute;
            t[8] = (byte)now.Second;
            t[10] = (byte)now.DayOfWeek; // .NET: Sunday=0 - khop firmware (0=Chu nhat)
            t[62] = 0xAA;
            t[63] = 0x55;

            return new List<Step>
            {
                new Step("Clock begin (0x04 0x18)", Command(CmdBegin), expectAck: true),
                new Step("Clock select (0x04 0x28)", Command(CmdSelectClock, new Dictionary<int, byte> { { 8, 0x01 } }), expectAck: true),
                new Step("Clock time payload", t, expectAck: false, readEcho: true),
                new Step("Clock apply (0x04 0x02)", Command(CmdApply), expectAck: true),
            };
        }

        // ------------------------------------------------------------------
        // DISPLAY UPLOAD
        // Mo dau (kenh lenh): begin -> metadata(0x72, [2]=slot,
        //   [8..9]=so chunk little-endian)
        // Sau do gui tung chunk 4096 byte bang OUTPUT report len kenh 0xFF68,
        // moi chunk cho mot input report ACK tu phim (toi da ~400 ms).
        // Ket thuc: apply (0x04 0x02) tren kenh lenh.
        // ------------------------------------------------------------------
        public static List<Step> BuildDisplayBegin(int slot, int chunkCount)
        {
            return new List<Step>
            {
                new Step("Display begin (0x04 0x18)", Command(CmdBegin), expectAck: true),
                new Step("Display metadata (0x04 0x72)",
                    Command(CmdSelectDisplay, new Dictionary<int, byte>
                    {
                        { 2, (byte)slot },
                        { 8, (byte)(chunkCount & 0xFF) },
                        { 9, (byte)((chunkCount >> 8) & 0xFF) },
                    }), expectAck: true),
            };
        }

        public static Step BuildDisplayFinish()
        {
            return new Step("Display apply (0x04 0x02)", Command(CmdApply), expectAck: true);
        }

        /// <summary>Ten 20 hieu ung firmware (id 0..19) - khop ca hai nguon.</summary>
        public static readonly string[] EffectNames =
        {
            "0 - Tat (LED Off)",
            "1 - Tinh (Static)",
            "2 - Reactive sang khi go (Single On)",
            "3 - Reactive mo dan (Single Off)",
            "4 - Long lanh (Glittering)",
            "5 - Mua roi (Falling)",
            "6 - Nhieu mau (Colourful)",
            "7 - Tho (Breathing)",
            "8 - Quang pho (Spectrum)",
            "9 - Lan ra ngoai (Outward)",
            "10 - Song (Wave / Scrolling)",
            "11 - Cuon (Rolling)",
            "12 - Xoay (Rotating)",
            "13 - Bung no (Explode)",
            "14 - Phong (Launch)",
            "15 - Gon song (Ripples)",
            "16 - Chay (Flowing)",
            "17 - Nhip (Pulsating)",
            "18 - Nghieng (Tilt)",
            "19 - Con thoi (Shuttle)",
        };

        /// <summary>Huong chay hieu ung: 0=Phai, 1=Xuong, 2=Trai, 3=Len.</summary>
        public static readonly string[] DirectionNames =
        {
            "Phải", "Xuống", "Trái", "Lên",
        };
    }
}
