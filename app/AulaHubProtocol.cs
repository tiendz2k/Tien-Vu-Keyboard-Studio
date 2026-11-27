using System;
using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Driver Pack 5 (v3.9) — cac ho AULA Hub CHUA co o cac pack truoc
    // (docs/PROTOCOLS-AULA-HUB.md, trich tu client AULA Hub 1.1.22 +
    // web hub.aulacn.com). File nay THUAN LOGIC:
    //  - HfdProtocol: ho HFD — khung [0xAA, cmd, len, addrLE16, hdr(3B),
    //    payload@8], dap tra [0x55, cmd, ...], khong checksum tren duong
    //    chinh; giao dien phu 0xFF67 cho LED/TFT khoi lon.
    //  - SparkLinkV1Protocol ("KB2"): khung [0x5C, len, cmd, checksum,
    //    payload], checksum 1 byte dac biet cua hang.
    //  - SparkLinkV2Protocol: khung [category, sub, payload], khong
    //    magic/length/checksum; DirectDrive stream + GIF stream.
    // Chi cac fact muc [CODE] trong ho so moi co builder; muc [SUY LUẬN]
    // ghi ro tai cho va chi o lop thiet bi duoi dang ghi log.
    // =====================================================================

    // ------------------------------ HFD ------------------------------
    public static class HfdProtocol
    {
        public const byte ReqHead = 0xAA;
        public const byte RespHead = 0x55;
        public const int HeaderLen = 8;
        public const int MainReportLen = 32;      // ban phim co day thong thuong
        public const int SubChunkFallback = 4104; // 0xFF67: reportCount - 8, du phong 4104
        public const int TimeoutMs = 500;
        public const int MaxRetries = 3;
        public const int LedSettleMs = 10;        // nghi sau goi LED/animation cuoi

        // Bang lenh HFD [CODE]
        public const byte CmdGetDeviceInfo = 16;
        public const byte CmdReadGameMode = 17, CmdWriteGameMode = 33;
        public const byte CmdReadKeymap = 18, CmdWriteKeymap = 34;
        public const byte CmdReadLedEffect = 19, CmdWriteLedEffect = 35;
        public const byte CmdReadPerKey = 20, CmdWritePerKey = 36;
        public const byte CmdReadMacro = 21, CmdWriteMacro = 37;
        public const byte CmdReadFnLayer = 22, CmdWriteFnLayer = 38;
        public const byte CmdReadRt = 23, CmdWriteRt = 39;
        public const byte CmdReadDks = 24, CmdWriteDks = 40;
        public const byte CmdReadTriggerMode = 25, CmdWriteTriggerMode = 41;
        public const byte CmdReadLightBox = 26, CmdWriteLightBox = 42;
        public const byte CmdReadSocd = 28, CmdWriteSocd = 44;
        public const byte CmdReadSideLight = 29, CmdWriteSideLight = 45;
        public const byte CmdRs = 30, CmdMt = 31, CmdTgl = 32;
        public const byte CmdEnd = 47, CmdMpt = 48;
        public const byte CmdGetAllLightsRgb = 51;
        public const byte CmdTemp = 52;      // payload dong ho / sysinfo
        public const byte CmdMusicLegacy = 53; // 32B, checksum byte 31
        public const byte CmdMusic = 60;
        public const byte CmdDotMatrixConfig = 61; // cap doc/ghi 42/61
        public const byte CmdLedAnimUpload = 65, CmdLedAnimSelect = 66; // qua 0xFF67
        public const byte CmdFirmwareDownload = 79;
        public const byte CmdTftAnimUpload = 80, CmdTftPreset = 81;     // qua 0xFF67
        public const byte CmdStatusUpdate = 82;
        public const byte CmdCalibStart = 96;
        public const byte CmdCalibWriteData = 100, CmdCalibStatus = 101,
            CmdCalibSave = 102, CmdCalibClear = 103, CmdCalibGetData = 104,
            CmdCalibApplyRange = 105, CmdCalibReset = 106;
        public const byte CmdDongleInfo = 55, CmdDongleLight = 59, CmdDongleMusic = 60;

        /// <summary>Dung mot goi yeu cau HFD co do dai report cho truoc:
        /// [0]=0xAA, [1]=cmd, [2]=so byte payload cua goi nay,
        /// [3..4]=dia chi LE16, [5..7]=otherHeader (mac dinh 0; byte 6 =
        /// co "goi cuoi" — giu 0 o goi cuoi theo ma hang),
        /// [8..]=payload.</summary>
        public static byte[] BuildRequest(byte cmd, ushort address, byte[] payload, int reportLen,
            byte hdr5 = 0, byte hdr6 = 0, byte hdr7 = 0)
        {
            int len = payload == null ? 0 : Math.Min(payload.Length, Math.Max(0, reportLen - HeaderLen));
            var p = new byte[Math.Max(reportLen, HeaderLen + len)];
            p[0] = ReqHead;
            p[1] = cmd;
            p[2] = (byte)len;
            p[3] = (byte)(address & 0xFF);
            p[4] = (byte)(address >> 8);
            p[5] = hdr5; p[6] = hdr6; p[7] = hdr7;
            if (len > 0) Array.Copy(payload, 0, p, HeaderLen, len);
            return p;
        }

        /// <summary>Chia mot khoi du lieu thanh cac goi HFD lien tiep:
        /// moi goi giu nguyen header 0xAA, dia chi tang dan.</summary>
        public static List<byte[]> BuildBlockRequests(byte cmd, byte[] data, int reportLen)
        {
            var list = new List<byte[]>();
            data = data ?? Array.Empty<byte>();
            int chunk = Math.Max(1, reportLen - HeaderLen);
            int chunks = Math.Max(1, (data.Length + chunk - 1) / chunk);
            for (int i = 0; i < chunks; i++)
            {
                int len = Math.Min(chunk, data.Length - i * chunk);
                var part = new byte[len];
                if (len > 0) Array.Copy(data, i * chunk, part, 0, len);
                list.Add(BuildRequest(cmd, (ushort)(i * chunk), part, reportLen));
            }
            return list;
        }

        /// <summary>Kiem tra dap tra: [0]=0x55, [1]=responseCmd khop lenh
        /// da gui (rieng upload TFT NOVA98: cho responseCmd 65 — xem
        /// BuildNova98TftHeader).</summary>
        public static bool IsResponseFor(byte[] reply, byte expectedCmd)
            => reply != null && reply.Length >= 2 && reply[0] == RespHead && reply[1] == expectedCmd;

        /// <summary>Payload huu ich cua dap tra nam o offset 8 (mot so
        /// lenh legacy o offset 4).</summary>
        public static byte[] ResponsePayload(byte[] reply, int offset = 8)
        {
            if (reply == null || reply.Length <= offset) return Array.Empty<byte>();
            var r = new byte[reply.Length - offset];
            Array.Copy(reply, offset, r, 0, r.Length);
            return r;
        }

        // ---------- Khoi thong tin thiet bi (lenh 16, 56 byte) ----------
        public sealed class DeviceInfo
        {
            public int Vid, Pid, LedRecordCount, Battery, TftMaxFrames, GifMaxFrames,
                LedMaxFrames, RtPrecision;
            public bool MusicFlag1, MusicFlag2;
        }

        public static DeviceInfo ParseDeviceInfo(byte[] payload56)
        {
            if (payload56 == null || payload56.Length < 56) return null;
            int Le16(int o) => payload56[o] | (payload56[o + 1] << 8);
            return new DeviceInfo
            {
                Vid = Le16(0), Pid = Le16(2), LedRecordCount = Le16(4),
                Battery = payload56[6], TftMaxFrames = payload56[10],
                GifMaxFrames = payload56[12], LedMaxFrames = payload56[13],
                RtPrecision = payload56[19],
                MusicFlag1 = payload56[20] != 0, MusicFlag2 = payload56[21] != 0,
            };
        }

        // ---------- Khoi LED effect (16 byte) ----------
        /// <summary>[0]=mode (0 tat, 1 tinh, 2–19 dong, 20 tuy chinh),
        /// [1]=do sang, [2]=toc do, [3..5]=RGB, [6]=type, [7]=customFlag,
        /// [8..9]=sleep LE16 phut, [10]=direction, [11]=saturation,
        /// [12]=fullColor, [13]=music-effect, [14..15]=0xAA,0x55.</summary>
        public static byte[] BuildLedEffectBlock(byte mode, byte brightness, byte speed,
            byte r, byte g, byte b, byte type = 0, byte customFlag = 0, ushort sleepMinutes = 0,
            byte direction = 0, byte saturation = 0, byte fullColor = 0, byte musicEffect = 0)
        {
            var blk = new byte[16];
            blk[0] = mode; blk[1] = brightness; blk[2] = speed;
            blk[3] = r; blk[4] = g; blk[5] = b;
            blk[6] = type; blk[7] = customFlag;
            blk[8] = (byte)(sleepMinutes & 0xFF); blk[9] = (byte)(sleepMinutes >> 8);
            blk[10] = direction; blk[11] = saturation; blk[12] = fullColor; blk[13] = musicEffect;
            blk[14] = 0xAA; blk[15] = 0x55;
            return blk;
        }

        // ---------- Per-key: 512 byte = 128 o [ledId, R, G, B] ----------
        public static byte[] BuildPerKeyBlock(IReadOnlyList<(byte R, byte G, byte B)> colors128)
        {
            var blk = new byte[512];
            for (int i = 0; i < 128 && colors128 != null && i < colors128.Count; i++)
            {
                blk[i * 4] = (byte)i;
                blk[i * 4 + 1] = colors128[i].R;
                blk[i * 4 + 2] = colors128[i].G;
                blk[i * 4 + 3] = colors128[i].B;
            }
            return blk;
        }

        // ---------- Khoi RT (1024 byte = 128 x 8 byte) ----------
        /// <summary>Ban ghi RT 8 byte: [0]=rtSwitch, [2..3]=travel LE16,
        /// [4..5]=rtPress LE16, [6..7]=rtRelease LE16 (gia tri = mm x 100).</summary>
        public static byte[] BuildRtRecord(bool rtSwitch, double travelMm, double rtPressMm, double rtReleaseMm)
        {
            var rec = new byte[8];
            rec[0] = rtSwitch ? (byte)1 : (byte)0;
            void Put(int off, double mm)
            {
                int v = (int)Math.Round(mm * 100);
                rec[off] = (byte)(v & 0xFF); rec[off + 1] = (byte)(v >> 8);
            }
            Put(2, travelMm); Put(4, rtPressMm); Put(6, rtReleaseMm);
            return rec;
        }

        // ---------- Nhac legacy (lenh 53, goi 32 byte, checksum byte 31) ----------
        /// <summary>Goi nhac legacy: 32 byte, byte 31 = Σ byte 0–30 &amp;
        /// 0xFF; mau RGB565. Day la ngoai le DUY NHAT co checksum tren
        /// duong chinh HFD.</summary>
        public static byte[] BuildMusicLegacyPacket(byte[] payload30)
        {
            var p = new byte[32];
            p[0] = ReqHead; p[1] = CmdMusicLegacy;
            if (payload30 != null) Array.Copy(payload30, 0, p, 2, Math.Min(payload30.Length, 29));
            int sum = 0;
            for (int i = 0; i < 31; i++) sum += p[i];
            p[31] = (byte)(sum & 0xFF);
            return p;
        }

        public static ushort Rgb565(byte r, byte g, byte b)
            => (ushort)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));

        // ---------- NOVA98 TFT 240x135 RGB565 LE ----------
        /// <summary>Bang tre 256 byte tien to file TFT: [0]=so khung,
        /// [1..n]=delay x 5 cua tung khung, sau do 0, dem 0xFF toi 256.</summary>
        public static byte[] BuildTftDelayTable(IReadOnlyList<int> frameDelaysMs)
        {
            var t = new byte[256];
            int n = frameDelaysMs?.Count ?? 0;
            t[0] = (byte)n;
            for (int i = 0; i < n && i < 253; i++)
                t[1 + i] = (byte)Math.Min(255, (frameDelaysMs[i] / 5));
            // t[1 + n] giu 0 (byte ket thuc danh sach delay), dem 0xFF tu sau no
            for (int i = 2 + n; i < 256; i++) t[i] = 0xFF;
            return t;
        }

        /// <summary>Header dac biet cua lenh 80 cho NOVA98:
        /// [0xAA, 80, chunkIdxLE16, totalChunksLE16, 80, 6]. Dap tra cua
        /// lenh nay duoc cho o responseCmd 65 (diem la ghi nguyen van tu
        /// ma hang).</summary>
        public static byte[] BuildNova98TftHeader(ushort chunkIndex, ushort totalChunks)
            => new[]
            {
                ReqHead, CmdTftAnimUpload,
                (byte)(chunkIndex & 0xFF), (byte)(chunkIndex >> 8),
                (byte)(totalChunks & 0xFF), (byte)(totalChunks >> 8),
                CmdTftAnimUpload, (byte)6,
            };

        public const byte Nova98TftExpectedResponseCmd = 65;

        // ---------- Dongle 2.4G HFD (bo lenh RIENG, khong forward) ----------
        /// <summary>Header dongle 4 byte: [0xAA, cmd, lenLE16]; mau dongle
        /// dung RGB565 2 byte/phim; byte 3 cua dap tra = pin.</summary>
        public static byte[] BuildDonglePacket(byte cmd, byte[] payload)
        {
            int len = payload?.Length ?? 0;
            var p = new byte[4 + len];
            p[0] = ReqHead; p[1] = cmd;
            p[2] = (byte)(len & 0xFF); p[3] = (byte)(len >> 8);
            if (len > 0) Array.Copy(payload, 0, p, 4, len);
            return p;
        }

        public static byte? DongleBatteryOf(byte[] reply)
            => reply != null && reply.Length >= 4 ? reply[3] : (byte?)null;

        // ---------- Cau hinh dot-matrix (lenh ghi 61 / doc 42) ----------
        /// <summary>Khoi cau hinh dot-matrix 9 byte: [effect, brightness,
        /// speed, R, G, B, count, direction, bgSwitch].</summary>
        public static byte[] BuildDotMatrixConfig(byte effect, byte brightness, byte speed,
            byte r, byte g, byte b, byte count, byte direction, byte bgSwitch)
            => new[] { effect, brightness, speed, r, g, b, count, direction, bgSwitch };
    }

    // --------------------- SparkLink V1 ("KB2") ---------------------
    public static class SparkLinkV1Protocol
    {
        public const int PacketLen = 64;
        public const byte Magic = 0x5C;
        public const int TimeoutMs = 1000;
        public const int MaxRetries = 3;

        public const byte CmdOrder = 0, CmdSync = 1, CmdRm6x21 = 18, CmdPrgb = 24,
            CmdLogoRgb = 25, CmdMacro = 32, CmdMacroMode = 33, CmdKey = 35,
            CmdMt = 36, CmdTgl = 37, CmdDks = 38, CmdMpt = 39, CmdEnd = 40,
            CmdDb = 41, CmdKrgb = 42, CmdDefKey = 43, CmdSocd = 44, CmdRs = 45,
            CmdFail = 255;

        // ORDER_TYPE cua lenh 0 (payload[0])
        public const byte OrderProtocolVersion = 1, OrderSaveParams = 2, OrderReload = 3,
            OrderClearCalibration = 4, OrderCalibStart = 12, OrderCalibEnd = 13,
            OrderFactoryRestore = 17, OrderWinLock = 32, OrderTravelPrecision = 37,
            OrderFixedColor = 68, OrderPollingRate = 80, OrderSocdSwitch = 97,
            OrderOnboardProfile = 112;

        /// <summary>Checksum SparkLink V1: (53 + 92 + len + cmd + (byte
        /// payload CUOI neu 0 &lt; len ≤ 252)) &amp; 0xFF — chi phu thuoc
        /// byte payload cuoi, nguyen van tu ma hang.</summary>
        public static byte ComputeChecksum(int payloadLen, byte cmd, byte[] payload)
        {
            int sum = 53 + 92 + payloadLen + cmd;
            if (payloadLen > 0 && payloadLen <= 252 && payload != null && payload.Length >= payloadLen)
                sum += payload[payloadLen - 1];
            return (byte)(sum & 0xFF);
        }

        /// <summary>Dung khung 64 byte: [0]=0x5C, [1]=do dai payload,
        /// [2]=cmd, [3]=checksum, [4..]=payload, dem 0.</summary>
        public static byte[] BuildFrame(byte cmd, byte[] payload)
        {
            var p = new byte[PacketLen];
            int len = payload == null ? 0 : Math.Min(payload.Length, PacketLen - 4);
            p[0] = Magic;
            p[1] = (byte)len;
            p[2] = cmd;
            if (len > 0) Array.Copy(payload, 0, p, 4, len);
            var eff = new byte[len];
            if (len > 0) Array.Copy(p, 4, eff, 0, len);
            p[3] = ComputeChecksum(len, cmd, eff);
            return p;
        }

        public static bool VerifyFrame(byte[] frame)
        {
            if (frame == null || frame.Length < 4 || frame[0] != Magic) return false;
            int len = frame[1];
            if (4 + len > frame.Length) return false;
            var payload = new byte[len];
            Array.Copy(frame, 4, payload, 0, len);
            return ComputeChecksum(len, frame[2], payload) == frame[3];
        }

        public static byte[] FramePayload(byte[] frame)
        {
            if (frame == null || frame.Length < 4) return Array.Empty<byte>();
            int len = Math.Min(frame[1], frame.Length - 4);
            var r = new byte[len];
            Array.Copy(frame, 4, r, 0, len);
            return r;
        }

        /// <summary>Lay payload kem co doc/ghi o payload[0] (0 doc, 1 ghi).</summary>
        public static byte[] WithRwFlag(bool write, params byte[] rest)
        {
            var payload = new byte[1 + (rest?.Length ?? 0)];
            payload[0] = write ? (byte)1 : (byte)0;
            if (rest != null) Array.Copy(rest, 0, payload, 1, rest.Length);
            return payload;
        }

        /// <summary>Lệnh 0 (KB2_CMD): payload[0] = ORDER_TYPE truc tiep
        /// (khong co co doc/ghi rieng cho lenh nay — bang ORDER_TYPE
        /// trong ho so).</summary>
        public static byte[] BuildOrder(byte orderType, params byte[] rest)
        {
            var payload = new byte[1 + (rest?.Length ?? 0)];
            payload[0] = orderType;
            if (rest != null) Array.Copy(rest, 0, payload, 1, rest.Length);
            return BuildFrame(CmdOrder, payload);
        }

        /// <summary>PRGB: khoi den nen. [0]=co ghi, [1..4]=0, 7 mau x
        /// (B,G,R,0xFF) — thu tu BGR, [33..36]=0, [37]=co (bit0 bat, bit1
        /// huong, bit4 super-response), [38]=do sang, [39]=mode (0 tinh,
        /// 1–20 dong, 21 tuy chinh), [40]=toc do, [41]=sleep,
        /// [42]=chi so mau tinh. UI 1–5 -> day = UI − 1.</summary>
        public static byte[] BuildPrgb(IReadOnlyList<(byte R, byte G, byte B)> colors7,
            byte flags, int brightnessUi1To5, byte mode, int speedUi1To5, byte sleep, byte staticColorIndex)
        {
            var payload = new byte[48];
            payload[0] = 1;
            for (int i = 0; i < 7; i++)
            {
                var c = colors7 != null && i < colors7.Count ? colors7[i] : (R: (byte)0, G: (byte)0, B: (byte)0);
                int o = 5 + i * 4;
                payload[o] = c.B; payload[o + 1] = c.G; payload[o + 2] = c.R; payload[o + 3] = 0xFF;
            }
            payload[37] = flags;
            payload[38] = (byte)Math.Max(0, brightnessUi1To5 - 1);
            payload[39] = mode;
            payload[40] = (byte)Math.Max(0, speedUi1To5 - 1);
            payload[41] = sleep;
            payload[42] = staticColorIndex;
            return BuildFrame(CmdPrgb, payload);
        }

        /// <summary>KRGB (42): mot phim/goi [co ghi, key, R, G, B] (thu tu
        /// RGB); goi LUU dac biet: key 254, mau 254/254/254.</summary>
        public static byte[] BuildKrgb(byte key, byte r, byte g, byte b)
            => BuildFrame(CmdKrgb, new[] { (byte)1, key, r, g, b });

        public static byte[] BuildKrgbSave()
            => BuildFrame(CmdKrgb, new[] { (byte)1, (byte)254, (byte)254, (byte)254, (byte)254 });

        /// <summary>DB (41): dead-band + hanh trinh toan cuc:
        /// [co, 0, 0, travelLE16, deadPressLE16, deadReleaseLE16, 0 x 6].
        /// Hanh trinh = mm x 1000.</summary>
        public static byte[] BuildDb(double travelMm, double deadPressMm, double deadReleaseMm)
        {
            var payload = new byte[14];
            payload[0] = 1;
            void Put(int off, double mm)
            {
                int v = (int)Math.Round(mm * 1000);
                payload[off] = (byte)(v & 0xFF); payload[off + 1] = (byte)(v >> 8);
            }
            Put(3, travelMm); Put(5, deadPressMm); Put(7, deadReleaseMm);
            return BuildFrame(CmdDb, payload);
        }

        /// <summary>SOCD (44): [co, pos1, pos2, key1LE16, key2LE16, type,
        /// mode, (delayLE16 neu protocol ≥ 1.0.7)].</summary>
        public static byte[] BuildSocd(byte pos1, byte pos2, ushort key1, ushort key2,
            byte type, byte mode, ushort? delay = null)
        {
            var payload = new List<byte>
            {
                1, pos1, pos2,
                (byte)(key1 & 0xFF), (byte)(key1 >> 8),
                (byte)(key2 & 0xFF), (byte)(key2 >> 8),
                type, mode,
            };
            if (delay.HasValue)
            {
                payload.Add((byte)(delay.Value & 0xFF));
                payload.Add((byte)(delay.Value >> 8));
            }
            return BuildFrame(CmdSocd, payload.ToArray());
        }

        /// <summary>KEY (35) o actuation: payload[1] = slot bo cuc
        /// (0–3 keymap Fn0–Fn3, 4 actuation, 8 trigger mode, 20/21 RT
        /// press/release, 22/23 dead-zone, 25 axis type...).</summary>
        public const byte KeySlotActuation = 4, KeySlotTriggerMode = 8,
            KeySlotRtPress = 20, KeySlotRtRelease = 21, KeySlotDeadPress = 22,
            KeySlotDeadRelease = 23, KeySlotAxisType = 25;

        /// <summary>Gia tri hanh trinh SparkLink: mm x 1000, LE16.</summary>
        public static byte[] TravelLe16(double mm)
        {
            int v = (int)Math.Round(mm * 1000);
            return new[] { (byte)(v & 0xFF), (byte)(v >> 8) };
        }
    }

    // ------------------------- SparkLink V2 -------------------------
    public static class SparkLinkV2Protocol
    {
        public const int PacketLen = 64;
        public const int TimeoutMs = 1000;
        public const int MaxRetries = 3;
        public const int MatrixRows = 6, MatrixCols = 21; // ma tran dien 6x21 = 126 o

        public const byte CatDevice = 1, CatGlobal = 2, CatLayoutKey = 3,
            CatPerformance = 4, CatLighting = 5, CatHigherKey = 6, CatMacro = 7,
            CatFirmware = 8, CatCustom = 10, CatDisplayer = 12, CatThreeMode = 13;

        /// <summary>Dung khung V2 64 byte dem 0: [0]=category, [1]=sub,
        /// [2..]=payload. Khong magic/length/checksum.</summary>
        public static byte[] BuildFrame(byte category, byte sub, byte[] payload)
        {
            var p = new byte[PacketLen];
            p[0] = category; p[1] = sub;
            if (payload != null) Array.Copy(payload, 0, p, 2, Math.Min(payload.Length, PacketLen - 2));
            return p;
        }

        // ---------- Lighting (category 5) ----------
        /// <summary>Base: [5, 1 doc|2 ghi, area, 0, open, mode, luminance,
        /// speed, direction, selectStaticColor]. UI gui luminance−1
        /// (0–100) va speed−1.</summary>
        public static byte[] BuildLightingBase(bool write, byte area, byte open, byte mode,
            int luminanceUi, int speedUi, byte direction, byte staticColorIndex)
        {
            return BuildFrame(CatLighting, write ? (byte)2 : (byte)1, new[]
            {
                area, (byte)0, open, mode,
                (byte)Math.Max(0, luminanceUi - 1),
                (byte)Math.Max(0, speedUi - 1),
                direction, staticColorIndex,
            });
        }

        /// <summary>Palette: [5, 1|2, area, 1, (B,G,R,H) x 8].</summary>
        public static byte[] BuildLightingPalette(bool write, byte area,
            IReadOnlyList<(byte R, byte G, byte B, byte H)> colors8)
        {
            var payload = new List<byte> { area, 1 };
            for (int i = 0; i < 8; i++)
            {
                var c = colors8 != null && i < colors8.Count ? colors8[i] : (R: (byte)0, G: (byte)0, B: (byte)0, H: (byte)0);
                payload.Add(c.B); payload.Add(c.G); payload.Add(c.R); payload.Add(c.H);
            }
            return BuildFrame(CatLighting, write ? (byte)2 : (byte)1, payload.ToArray());
        }

        /// <summary>Per-key ben vung (Custom): [5, 4, area, chunkIdx,
        /// (B,G,R,flag) x 15] — flag 255 = da tuy chinh. Doc lai: [5, 3,
        /// area, chunkIdx].</summary>
        public static byte[] BuildLightingCustomChunk(byte area, byte chunkIndex,
            IReadOnlyList<(byte R, byte G, byte B, bool Customised)> cells15)
        {
            var payload = new List<byte> { area, chunkIndex };
            for (int i = 0; i < 15; i++)
            {
                var c = cells15 != null && i < cells15.Count ? cells15[i] : (R: (byte)0, G: (byte)0, B: (byte)0, Customised: false);
                payload.Add(c.B); payload.Add(c.G); payload.Add(c.R);
                payload.Add(c.Customised ? (byte)255 : (byte)0);
            }
            return BuildFrame(CatLighting, 4, payload.ToArray());
        }

        public static byte[] BuildLightingCustomRead(byte area, byte chunkIndex)
            => BuildFrame(CatLighting, 3, new[] { area, chunkIndex });

        /// <summary>Stream DirectDrive: CUNG khung Custom nhung byte[1]=5,
        /// fire-and-forget (khong cho ACK).</summary>
        public static byte[] BuildDirectDriveChunk(byte area, byte chunkIndex,
            IReadOnlyList<(byte R, byte G, byte B, bool Customised)> cells15)
        {
            var f = BuildLightingCustomChunk(area, chunkIndex, cells15);
            f[1] = 5;
            return f;
        }

        /// <summary>GIF stream (trang GIF ghi thang HID): [0]=5, [1]=5,
        /// [2]=0, [3]=chunkIdx, 15 o x u32LE (255&lt;&lt;24|R&lt;&lt;16|
        /// G&lt;&lt;8|B); nhip goi = max(1, round(300/speed)) ms.</summary>
        public static byte[] BuildGifStreamChunk(byte chunkIndex,
            IReadOnlyList<(byte R, byte G, byte B)> cells15)
        {
            var p = new byte[PacketLen];
            p[0] = 5; p[1] = 5; p[2] = 0; p[3] = chunkIndex;
            for (int i = 0; i < 15; i++)
            {
                var c = cells15 != null && i < cells15.Count ? cells15[i] : (R: (byte)0, G: (byte)0, B: (byte)0);
                uint v = (255u << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                int o = 4 + i * 4;
                p[o] = (byte)(v & 0xFF); p[o + 1] = (byte)((v >> 8) & 0xFF);
                p[o + 2] = (byte)((v >> 16) & 0xFF); p[o + 3] = (byte)((v >> 24) & 0xFF);
            }
            return p;
        }

        public static int GifStreamIntervalMs(int speed) => Math.Max(1, (int)Math.Round(300.0 / Math.Max(1, speed)));

        /// <summary>Hieu chinh mau: [5, 1|2, area, 2, R, G, B] — o day thu
        /// tu RGB (khac Palette/Custom dung BGR).</summary>
        public static byte[] BuildColorCorrection(bool write, byte area, byte r, byte g, byte b)
            => BuildFrame(CatLighting, write ? (byte)2 : (byte)1, new[] { area, (byte)2, r, g, b });

        /// <summary>Mau Caps: [5, 6, B, G, R].</summary>
        public static byte[] BuildCapsColor(byte r, byte g, byte b)
            => BuildFrame(CatLighting, 6, new[] { b, g, r });

        // ---------- Performance (category 4): khung trigger 27 byte ----------
        /// <summary>[4, 2 ghi, row, col, mode (0 thuong, 1 RT),
        /// normalPressLE16, normalReleaseLE16, rtFirstTouchLE16,
        /// rtPressLE16, rtReleaseLE16, pressDeadLE16, releaseDeadLE16,
        /// axis, calibrate, axisV2IdLE16, axisRangeMaxLE16,
        /// axisCoefficientLE16] — hanh trinh mm x 1000. Tong khung 27
        /// byte y nghia (dem 0 toi 64 khi gui).</summary>
        public static byte[] BuildTriggerFrame(byte row, byte col, byte mode,
            double normalPressMm, double normalReleaseMm, double rtFirstTouchMm,
            double rtPressMm, double rtReleaseMm, double pressDeadMm, double releaseDeadMm,
            byte axis, byte calibrate, ushort axisV2Id, ushort axisRangeMax, ushort axisCoefficient)
        {
            var payload = new List<byte> { row, col, mode };
            void PutMm(double mm)
            {
                int v = (int)Math.Round(mm * 1000);
                payload.Add((byte)(v & 0xFF)); payload.Add((byte)(v >> 8));
            }
            void Put16(ushort v) { payload.Add((byte)(v & 0xFF)); payload.Add((byte)(v >> 8)); }
            PutMm(normalPressMm); PutMm(normalReleaseMm); PutMm(rtFirstTouchMm);
            PutMm(rtPressMm); PutMm(rtReleaseMm); PutMm(pressDeadMm); PutMm(releaseDeadMm);
            payload.Add(axis); payload.Add(calibrate);
            Put16(axisV2Id); Put16(axisRangeMax); Put16(axisCoefficient);
            return BuildFrame(CatPerformance, 2, payload.ToArray());
        }

        public static byte[] BuildTriggerRead(byte row, byte col)
            => BuildFrame(CatPerformance, 1, new[] { row, col });

        // ---------- HigherKey (category 6) ----------
        public const byte HkNone = 0, HkDks = 1, HkMpt = 2, HkMt = 3, HkTgl = 4,
            HkEnd = 5, HkSocd = 6, HkRs = 7;

        /// <summary>Header HigherKey: [6, 1 doc|2 ghi, row, col, mode].</summary>
        public static byte[] BuildHigherKeyHeader(bool write, byte row, byte col, byte mode, byte[] rest = null)
        {
            var payload = new List<byte> { row, col, mode };
            if (rest != null) payload.AddRange(rest);
            return BuildFrame(CatHigherKey, write ? (byte)2 : (byte)1, payload.ToArray());
        }

        // ---------- Global (category 2) ----------
        public static byte[] BuildGlobalSave() => BuildFrame(CatGlobal, 2, null);
        public static byte[] BuildGlobalReset() => BuildFrame(CatGlobal, 1, null);

        /// <summary>Polling (Global sub 5): 0–6 (8K/4K/2K/1K/500/250/125 Hz).</summary>
        public static byte[] BuildGlobalPolling(byte rate0To6) => BuildFrame(CatGlobal, 5, new[] { rate0To6 });

        /// <summary>LED sleep (Global sub 13): u16LE phut.</summary>
        public static byte[] BuildGlobalLedSleep(ushort minutes)
            => BuildFrame(CatGlobal, 13, new[] { (byte)(minutes & 0xFF), (byte)(minutes >> 8) });

        // ---------- CustomCommand (category 10): dot-matrix BOX 63 ----------
        /// <summary>Goi animation dot-matrix: [10, 1, animIdx, totalFrame,
        /// targetFrame, packIdx, dataLenDiv4, 0, data 56B].</summary>
        public static byte[] BuildDotMatrixPacket(byte animIndex, byte totalFrames, byte targetFrame,
            byte packIndex, byte[] data56)
        {
            var payload = new List<byte>
            {
                animIndex, totalFrames, targetFrame, packIndex,
                (byte)((data56?.Length ?? 0) / 4), 0,
            };
            if (data56 != null) payload.AddRange(data56);
            return BuildFrame(CatCustom, 1, payload.ToArray());
        }

        public static byte[] BuildDotMatrixSelect(byte animIndex)
            => BuildFrame(CatCustom, 3, new[] { animIndex });

        /// <summary>Macro (category 7): du lieu u32LE
        /// (status&lt;&lt;31)|(delay&lt;&lt;16)|keycode; 15 action/goi khi ghi (sub 4).</summary>
        public static uint MacroActionWord(bool keyDown, int delay, ushort keycode)
            => ((keyDown ? 1u : 0u) << 31) | ((uint)(delay & 0x7FFF) << 16) | keycode;
    }
}
