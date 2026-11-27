using System;
using System.Collections.Generic;
using System.Linq;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Mot muc registry RongYuan (du lieu o RongYuanRegistryData.cs, sinh tu
    // devices.json cua Akko/MonsGeek + EPOMAKER).
    // DevId = null -> chua co anh xa dev_id da kiem chung -> CHI XEM.
    // =====================================================================
    public sealed class RongYuanRegistryEntry
    {
        public string Name;
        public string Brand;
        public string ChipFamily;
        public string InternalName;
        public int Vid;
        public int[] Pids;
        public uint? DevId;            // fw_id doc bang lenh 0x8F; null = chua biet
        public int Keys;               // 0 = chua biet
        public bool HasHE;
        public int ScreenW, ScreenH;   // 0 = khong man hinh
        public bool ScreenRgb888;      // false = RGB565 (mode "16"), true = RGB888 (mode "24")
        public bool HasKnob;
        public double MaxTravelMm;
        public string Source;

        public bool HasScreen => ScreenW > 0 && ScreenH > 0;
        public bool ViewOnly => !DevId.HasValue;
        public string DisplayName => Name.StartsWith(Brand, StringComparison.OrdinalIgnoreCase)
            ? Name : Brand + " " + Name;

        public RongYuanRegistryEntry(string name, string brand, string chipFamily, string internalName,
            int vid, int[] pids, uint? devId, int keys, bool hasHE,
            int screenW, int screenH, bool screenRgb888, bool hasKnob,
            double maxTravelMm, string source)
        {
            Name = name; Brand = brand; ChipFamily = chipFamily; InternalName = internalName;
            Vid = vid; Pids = pids; DevId = devId; Keys = keys; HasHE = hasHE;
            ScreenW = screenW; ScreenH = screenH; ScreenRgb888 = screenRgb888;
            HasKnob = hasKnob; MaxTravelMm = maxTravelMm; Source = source;
        }
    }

    // =====================================================================
    // Giao thuc nen tang RongYuan (RY5088 va cac ho cung nen) — Driver
    // Pack 2, v3.5. Dung chung boi Akko Cloud v4, MonsGeek V4 va EPOMAKER
    // Driver (ca ba deu la nhan trang cua cung mot nen tang OEM).
    //
    // Nguon su that (tai lieu hoa tu ma JS cua driver chinh hang, phan
    // tich tinh — CHUA kiem chung tren phan cung that):
    //  - Feature Report 64 byte, usage page 0xFFFF / usage 2, report ID 0.
    //  - Byte[0] = opcode; doc = ghi + 0x80 (GET = SET | 0x80).
    //  - Checksum Bit7 o byte 7: s[7] = 255 - (sum(s[0..6]) & 0xFF), tuc 8
    //    byte dau cong lai = 0xFF (mod 256). Lenh LED 7/8 dung Bit8 o
    //    byte 8: s[8] = 255 - (sum(s[0..7]) & 0xFF).
    //  - Du lieu lon cat manh 56 byte sau header 8 byte; pacing 10 ms;
    //    ACK = goi dap tra (resp[0] phai bang opcode da gui).
    //  - Dinh danh model: PID 0x5030 (va nhieu PID khac) bi DUNG CHUNG —
    //    model that xac dinh bang dev_id 32-bit LE doc tu lenh 0x8F
    //    (GET_INFOR), nam o byte 1..4 cua dap tra.
    //
    // File nay chi la ham thuan (pure) — khong cham vao thiet bi.
    // =====================================================================
    public static class RongYuanProtocol
    {
        public const int ReportLength = 64;
        public const int HeaderLength = 8;
        public const int ChunkLength = 56;          // du lieu huu ich moi goi sau header
        public const int UsagePage = 0xFFFF;
        public const int Usage = 2;
        public const int PacingMs = 10;             // SEND_TIME/READ_TIME cua driver goc
        public const int ScreenChunkPacingMs = 3;   // nhip goi du lieu man hinh (jm(3))
        public const int MainVid = 0x3151;

        // --- Ma lenh SET (GET = SET + 0x80) ---
        public const byte CmdReset = 1;
        public const byte CmdReportRate = 3;
        public const byte CmdProfile = 4;
        public const byte CmdDebounce = 6;
        public const byte CmdLed = 7;               // checksum Bit8
        public const byte CmdSideLed = 8;           // checksum Bit8
        public const byte CmdKbOption = 9;
        public const byte CmdKeymatrix = 10;
        public const byte CmdMacro = 11;
        public const byte CmdUserPic = 12;
        public const byte CmdFnLayer = 16;
        public const byte CmdSleep = 17;
        public const byte CmdUserGif = 18;
        public const byte CmdCalibrationReport = 27;
        public const byte CmdCalibrationMin = 28;
        public const byte CmdCalibrationMax = 30;
        public const byte CmdTftData = 37;          // RGB565
        public const byte CmdOledClock = 40;
        public const byte CmdScreen24Data = 41;     // RGB888
        public const byte CmdFlashErase = 44;       // GET (172) tra ACK magic
        public const byte CmdHeMulti = 101;         // 0x65 lenh gop Hall-effect
        public const byte CmdGetInfor = 0x8F;       // 143, chi doc: dev_id + fw
        public const byte CmdScreenPrepare565 = 165;  // = GET cua 37
        public const byte CmdScreenPrepare888 = 169;  // = GET cua 41
        public const byte CmdFlashEraseAck = 172;     // = GET cua 44
        public const byte CmdGetFeatureList = 230;    // 0xE6, chi doc

        public static byte GetOpcode(byte setOpcode) => (byte)(setOpcode | 0x80);

        // ------------------------------------------------------------------
        // Checksum
        // ------------------------------------------------------------------
        public static byte ComputeBit7(byte[] packet)
        {
            int sum = 0;
            for (int i = 0; i < 7; i++) sum += packet[i];
            return (byte)(255 - (sum & 0xFF));
        }

        public static byte ComputeBit8(byte[] packet)
        {
            int sum = 0;
            for (int i = 0; i < 8; i++) sum += packet[i];
            return (byte)(255 - (sum & 0xFF));
        }

        /// <summary>Dien checksum Bit7 vao byte 7 (mac dinh cho hau het lenh).</summary>
        public static byte[] SealBit7(byte[] packet) { packet[7] = ComputeBit7(packet); return packet; }

        /// <summary>Dien checksum Bit8 vao byte 8 (chi lenh LED 7/8 va ban doc cua chung).</summary>
        public static byte[] SealBit8(byte[] packet) { packet[8] = ComputeBit8(packet); return packet; }

        public static bool VerifyBit7(byte[] packet)
        {
            if (packet == null || packet.Length < 8) return false;
            int sum = 0;
            for (int i = 0; i < 8; i++) sum += packet[i];
            return (sum & 0xFF) == 0xFF;
        }

        public static bool VerifyBit8(byte[] packet)
        {
            if (packet == null || packet.Length < 9) return false;
            int sum = 0;
            for (int i = 0; i < 9; i++) sum += packet[i];
            return (sum & 0xFF) == 0xFF;
        }

        /// <summary>Goi lenh chi co header (khong du lieu sau byte 8), da dien Bit7.</summary>
        public static byte[] BuildCommand(byte opcode, params byte[] args)
        {
            var p = new byte[ReportLength];
            p[0] = opcode;
            if (args != null) Array.Copy(args, 0, p, 1, Math.Min(args.Length, 6));
            return SealBit7(p);
        }

        /// <summary>ACK chuan cua nen tang: dap tra bat dau bang dung opcode da gui.</summary>
        public static bool CheckAck(byte[] request, byte[] reply)
            => request != null && reply != null && reply.Length > 0 && reply[0] == request[0];

        // ------------------------------------------------------------------
        // Dinh danh bang dev_id (lenh 0x8F)
        // ------------------------------------------------------------------
        public static byte[] BuildGetInfor() => BuildCommand(CmdGetInfor);

        /// <summary>dev_id 32-bit little-endian o byte 1..4 cua dap tra 0x8F.</summary>
        public static uint? ParseDevId(byte[] reply)
        {
            if (reply == null || reply.Length < 5 || reply[0] != CmdGetInfor) return null;
            return (uint)(reply[1] | (reply[2] << 8) | (reply[3] << 16) | (reply[4] << 24));
        }

        /// <summary>Tra registry theo dev_id. null = la/chua co trong registry.</summary>
        public static RongYuanRegistryEntry MatchDevId(uint devId)
        {
            foreach (var e in RongYuanRegistryData.Entries)
                if (e.DevId.HasValue && e.DevId.Value == devId) return e;
            return null;
        }

        // ------------------------------------------------------------------
        // Lenh co ban
        // ------------------------------------------------------------------
        /// <summary>Chon profile 0..3 (opcode 4).</summary>
        public static byte[] BuildSetProfile(int profile)
        {
            if (profile < 0 || profile > 3) throw new ArgumentOutOfRangeException(nameof(profile));
            return BuildCommand(CmdProfile, (byte)profile);
        }
        public static byte[] BuildGetProfile() => BuildCommand(GetOpcode(CmdProfile));

        /// <summary>Tan so bao cao (opcode 3): 0=8K, 1=4K, 2=2K, 3=1K, 4=500, 5=250, 6=125 Hz.</summary>
        public static byte[] BuildSetReportRate(int rateCode)
        {
            if (rateCode < 0 || rateCode > 6) throw new ArgumentOutOfRangeException(nameof(rateCode));
            return BuildCommand(CmdReportRate, (byte)rateCode);
        }

        /// <summary>
        /// LED chinh (opcode 7, checksum Bit8). Layout sau opcode:
        /// [mode][speed][brightness][option&lt;&lt;4 | co mau][R][G][B], checksum o byte 8.
        /// </summary>
        public static byte[] BuildSetLed(byte mode, byte speed, byte brightness, byte option, bool colorful, byte r, byte g, byte b)
            => BuildLedPacket(CmdLed, mode, speed, brightness, option, colorful, r, g, b);

        /// <summary>LED vien (opcode 8, checksum Bit8) — cung layout voi LED chinh.</summary>
        public static byte[] BuildSetSideLed(byte mode, byte speed, byte brightness, byte option, bool colorful, byte r, byte g, byte b)
            => BuildLedPacket(CmdSideLed, mode, speed, brightness, option, colorful, r, g, b);

        private static byte[] BuildLedPacket(byte opcode, byte mode, byte speed, byte brightness, byte option, bool colorful, byte r, byte g, byte b)
        {
            var p = new byte[ReportLength];
            p[0] = opcode;
            p[1] = mode;
            p[2] = speed;
            p[3] = brightness;
            p[4] = (byte)(((option & 0x0F) << 4) | (colorful ? 1 : 0));
            p[5] = r; p[6] = g; p[7] = b;
            return SealBit8(p);
        }

        // ------------------------------------------------------------------
        // Cat manh 56 byte sau header 8 byte.
        // Header keymatrix/userpic (theo JS driver goc):
        //   [opcode, profile, 0xFF, chunkIdx, lenChunk, isLast, 0, Bit7]
        // ------------------------------------------------------------------
        private static List<byte[]> ChunkData(byte opcode, byte arg1, bool slotMarker, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int chunks = Math.Max(1, (data.Length + ChunkLength - 1) / ChunkLength);
            var list = new List<byte[]>();
            for (int i = 0; i < chunks; i++)
            {
                int len = Math.Min(ChunkLength, data.Length - i * ChunkLength);
                var p = new byte[ReportLength];
                p[0] = opcode;
                p[1] = arg1;
                p[2] = slotMarker ? (byte)0xFF : (byte)i;
                p[3] = slotMarker ? (byte)i : (byte)ChunkLength;
                if (slotMarker) { p[4] = (byte)len; p[5] = (byte)(i == chunks - 1 ? 1 : 0); }
                else p[4] = (byte)(i == chunks - 1 ? 1 : 0);
                if (len > 0) Array.Copy(data, i * ChunkLength, p, HeaderLength, len);
                list.Add(SealBit7(p));
            }
            return list;
        }

        /// <summary>Ghi keymap (opcode 10): du lieu 4 byte/phim, cat 56 byte/goi.</summary>
        public static List<byte[]> BuildKeymatrixWrite(byte profile, byte[] keymapData)
            => ChunkData(CmdKeymatrix, profile, true, keymapData);

        /// <summary>Goi yeu cau doc keymap (opcode 138) cho mot manh.</summary>
        public static byte[] BuildKeymatrixReadChunk(byte profile, int chunkIdx)
        {
            var p = new byte[ReportLength];
            p[0] = GetOpcode(CmdKeymatrix);
            p[1] = profile;
            p[2] = 0xFF;
            p[3] = (byte)chunkIdx;
            return SealBit7(p);
        }

        /// <summary>Ghi anh den tung phim (opcode 12): du lieu RGB phang 3 byte/phim (126 o -> 378 byte -> 7 goi).</summary>
        public static List<byte[]> BuildUserPicWrite(byte profile, byte[] rgbData)
            => ChunkData(CmdUserPic, profile, true, rgbData);

        /// <summary>Goi yeu cau doc anh den tung phim (opcode 140) cho mot manh.</summary>
        public static byte[] BuildUserPicReadChunk(byte profile, int chunkIdx)
        {
            var p = new byte[ReportLength];
            p[0] = GetOpcode(CmdUserPic);
            p[1] = profile;
            p[2] = 0xFF;
            p[3] = (byte)chunkIdx;
            return SealBit7(p);
        }

        /// <summary>Trai phang bang mau theo o phim thanh chuoi RGB lien tuc (thu tu o cua ban phim).</summary>
        public static byte[] FlattenRgb(IReadOnlyList<(byte R, byte G, byte B)> slots)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            var data = new byte[slots.Count * 3];
            for (int i = 0; i < slots.Count; i++)
            {
                data[i * 3] = slots[i].R;
                data[i * 3 + 1] = slots[i].G;
                data[i * 3 + 2] = slots[i].B;
            }
            return data;
        }

        // ------------------------------------------------------------------
        // Macro (opcode 11): buffer 256 byte = [repeatCount u16-LE][su kien
        // 4 byte]...; ghi theo manh 56 byte, header rieng cua macro:
        //   [11, macroIndex, chunkIdx, 56, isLast, 0, 0, Bit7]
        // (du lieu duoc dem 0 toi boi cua 56 nhu driver goc).
        // ------------------------------------------------------------------
        public const int MacroBufferLength = 256;

        public static byte[] BuildMacroBuffer(ushort repeatCount, byte[] events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (events.Length % 4 != 0) throw new ArgumentException("Su kien macro phai la boi cua 4 byte.");
            if (2 + events.Length > MacroBufferLength) throw new ArgumentException("Macro vuot qua buffer 256 byte.");
            var buf = new byte[MacroBufferLength];
            buf[0] = (byte)(repeatCount & 0xFF);
            buf[1] = (byte)(repeatCount >> 8);
            Array.Copy(events, 0, buf, 2, events.Length);
            return buf;
        }

        public static List<byte[]> BuildMacroWrite(byte macroIndex, ushort repeatCount, byte[] events)
        {
            var buf = BuildMacroBuffer(repeatCount, events);
            int chunks = (MacroBufferLength + ChunkLength - 1) / ChunkLength; // 5
            var list = new List<byte[]>();
            for (int i = 0; i < chunks; i++)
            {
                var p = new byte[ReportLength];
                p[0] = CmdMacro;
                p[1] = macroIndex;
                // header macro khong co o danh dau 0xFF: byte[2]=chunkIdx, byte[3]=56, byte[4]=isLast
                if (i * ChunkLength < MacroBufferLength)
                    Array.Copy(buf, i * ChunkLength, p, HeaderLength, Math.Min(ChunkLength, MacroBufferLength - i * ChunkLength));
                p[2] = (byte)i;
                p[3] = ChunkLength;
                p[4] = (byte)(i == chunks - 1 ? 1 : 0);
                list.Add(SealBit7(p));
            }
            return list;
        }

        /// <summary>Goi yeu cau doc macro (opcode 139) cho mot manh.</summary>
        public static byte[] BuildMacroReadChunk(byte macroIndex, int chunkIdx)
        {
            var p = new byte[ReportLength];
            p[0] = GetOpcode(CmdMacro);
            p[1] = macroIndex;
            p[2] = (byte)chunkIdx;
            p[3] = ChunkLength;
            return SealBit7(p);
        }

        // ------------------------------------------------------------------
        // Danh sach tinh nang (opcode 230, chi doc): dap tra hop le khi
        // resp[1] == 0xAA.
        // ------------------------------------------------------------------
        public static byte[] BuildGetFeatureList() => BuildCommand(CmdGetFeatureList);
        public static bool CheckFeatureListAck(byte[] reply)
            => reply != null && reply.Length > 1 && reply[0] == CmdGetFeatureList && reply[1] == 0xAA;

        // ------------------------------------------------------------------
        // Xoa flash (SET 44): dap tra cua GET 172 chua magic AA AA 55 55 o
        // byte 1..4 (ban EPOMAKER ghi dap tra bat dau [44, AA, AA, 55, 55]
        // — chap nhan ca hai dang, magic o byte 1..4 la phan chung).
        // ------------------------------------------------------------------
        public static byte[] BuildFlashErase() => BuildCommand(CmdFlashErase);
        public static bool CheckFlashEraseAck(byte[] reply)
        {
            if (reply == null || reply.Length < 5) return false;
            if (reply[0] != CmdFlashErase && reply[0] != CmdFlashEraseAck) return false;
            return reply[1] == 0xAA && reply[2] == 0xAA && reply[3] == 0x55 && reply[4] == 0x55;
        }
    }

    // =====================================================================
    // Khoi Hall-effect: mot lenh gop SET 101 (0x65) / GET 229 (0xE5),
    // sub-op o byte[1] chon mang du lieu theo tung phim.
    //  - Dang bulk:  byte[2]=1, byte[3]=chunkIdx, byte[4]=isLast, du lieu
    //    cat 56 byte sau header 8 byte.
    //  - Dang simple (1 phim): byte[2]=0, byte[3]=slot, byte[4]=isLast,
    //    gia tri nam o offset 8.
    // Gia tri hanh trinh = uint16-LE x he so theo firmware (10/100/200/1000).
    // =====================================================================
    public static class RongYuanHe
    {
        public const byte SubActuation = 0;
        public const byte SubLift = 1;
        public const byte SubRtPress = 2;
        public const byte SubRtLift = 3;
        public const byte SubDksStart = 4;
        public const byte SubMtTime = 5;
        public const byte SubDeadZone = 6;
        public const byte SubMode = 7;
        public const byte SubDksThresholds = 8;
        public const byte SubSnapPair = 9;
        public const byte SubDksTable = 10;
        public const byte SubTopDeadZone = 251;
        public const byte SubSwitchType = 252;
        public const byte SubRealtimeTravel = 254;
        public const byte SubRealtimeFlux = 255;

        // Gia tri dem mac dinh khi ghi bulk (don vi tho, tai lieu driver goc)
        public const ushort DefaultTravelRaw = 200;      // 2.00 mm o he so 100
        public const ushort DefaultLiftRaw = 280;        // 2.80 mm
        public const ushort DefaultDeadZoneRaw = 30;     // 0.30 mm
        public const ushort DefaultTopDeadZoneRaw = 30;  // 0.30 mm

        /// <summary>He so quy doi mm theo lop firmware: 10 (0.1mm), 100 (0.01mm), 200 (0.005mm), 1000 (0.001mm).</summary>
        public static readonly int[] ScaleOptions = { 10, 100, 200, 1000 };
        public const int DefaultScale = 100;

        public static ushort MmToRaw(double mm, int scale)
        {
            double raw = Math.Round(mm * scale);
            if (raw < 0) raw = 0;
            if (raw > 0xFFFF) raw = 0xFFFF;
            return (ushort)raw;
        }
        public static double RawToMm(ushort raw, int scale) => scale <= 0 ? 0 : raw / (double)scale;

        /// <summary>Mode byte (sub-op 7): bit7 = Rapid Trigger; 7 bit thap = che do.</summary>
        public const byte ModeNormal = 0;
        public const byte ModeDks = 2;
        public const byte ModeMt = 3;
        public const byte ModeTglHold = 4;
        public const byte ModeTglDots = 5;
        public const byte ModeSnap = 7;

        public static byte EncodeModeByte(byte mode, bool rapidTrigger)
            => (byte)((mode & 0x7F) | (rapidTrigger ? 0x80 : 0));
        public static (byte Mode, bool RapidTrigger) DecodeModeByte(byte value)
            => ((byte)(value & 0x7F), (value & 0x80) != 0);

        /// <summary>Thoi gian Mod-Tap luu theo luong tu 10 ms (sub-op 5, 1 byte/phim).</summary>
        public static byte MtMsToByte(int ms)
        {
            int q = ms / 10;
            if (q < 0) q = 0;
            if (q > 255) q = 255;
            return (byte)q;
        }
        public static int MtByteToMs(byte value) => value * 10;

        /// <summary>Noi tiep danh sach uint16 thanh byte little-endian.</summary>
        public static byte[] SerializeU16(IReadOnlyList<ushort> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var data = new byte[values.Count * 2];
            for (int i = 0; i < values.Count; i++)
            {
                data[i * 2] = (byte)(values[i] & 0xFF);
                data[i * 2 + 1] = (byte)(values[i] >> 8);
            }
            return data;
        }

        public static ushort[] ParseU16(byte[] data, int count)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var values = new ushort[count];
            for (int i = 0; i < count && i * 2 + 1 < data.Length; i++)
                values[i] = (ushort)(data[i * 2] | (data[i * 2 + 1] << 8));
            return values;
        }

        private static List<byte[]> ChunkHe(byte opcode, byte subOp, byte[] data)
        {
            int chunks = Math.Max(1, (data.Length + RongYuanProtocol.ChunkLength - 1) / RongYuanProtocol.ChunkLength);
            var list = new List<byte[]>();
            for (int i = 0; i < chunks; i++)
            {
                int len = Math.Min(RongYuanProtocol.ChunkLength, data.Length - i * RongYuanProtocol.ChunkLength);
                var p = new byte[RongYuanProtocol.ReportLength];
                p[0] = opcode;
                p[1] = subOp;
                p[2] = 1; // bulk
                p[3] = (byte)i;
                p[4] = (byte)(i == chunks - 1 ? 1 : 0);
                if (len > 0) Array.Copy(data, i * RongYuanProtocol.ChunkLength, p, RongYuanProtocol.HeaderLength, len);
                list.Add(RongYuanProtocol.SealBit7(p));
            }
            return list;
        }

        /// <summary>Ghi bulk mot mang HE (SET 101): data da noi tiep (uint16-LE hoac 1 byte/phim tuy sub-op).</summary>
        public static List<byte[]> BuildBulkWrite(byte subOp, byte[] data) => ChunkHe(RongYuanProtocol.CmdHeMulti, subOp, data);

        /// <summary>Goi yeu cau doc bulk (GET 229) cho mot manh cua mot sub-op.</summary>
        public static byte[] BuildBulkReadChunk(byte subOp, int chunkIdx)
        {
            var p = new byte[RongYuanProtocol.ReportLength];
            p[0] = RongYuanProtocol.GetOpcode(RongYuanProtocol.CmdHeMulti);
            p[1] = subOp;
            p[2] = 1; // bulk
            p[3] = (byte)chunkIdx;
            return RongYuanProtocol.SealBit7(p);
        }

        /// <summary>So manh can de doc mot mang co n byte huu ich.</summary>
        public static int ChunksForBytes(int dataBytes)
            => Math.Max(1, (dataBytes + RongYuanProtocol.ChunkLength - 1) / RongYuanProtocol.ChunkLength);

        /// <summary>Ghi 1 phim (SET 101 dang simple): gia tri nam o offset 8.</summary>
        public static byte[] BuildSimpleWrite(byte subOp, byte slot, bool isLast, byte[] value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length > RongYuanProtocol.ChunkLength) throw new ArgumentException("Gia tri simple vuot qua 56 byte.");
            var p = new byte[RongYuanProtocol.ReportLength];
            p[0] = RongYuanProtocol.CmdHeMulti;
            p[1] = subOp;
            p[2] = 0; // simple
            p[3] = slot;
            p[4] = (byte)(isLast ? 1 : 0);
            Array.Copy(value, 0, p, RongYuanProtocol.HeaderLength, value.Length);
            return RongYuanProtocol.SealBit7(p);
        }

        /// <summary>Ghi 1 phim cho sub-op dang uint16 (actuation/lift/RT/dead zone...).</summary>
        public static byte[] BuildSimpleWriteU16(byte subOp, byte slot, bool isLast, ushort raw)
            => BuildSimpleWrite(subOp, slot, isLast, new[] { (byte)(raw & 0xFF), (byte)(raw >> 8) });

        // --- Hieu chuan (chi khi cam day, theo luong cua driver goc) ---
        /// <summary>Bat/tat luong bao cao hieu chuan (opcode 27).</summary>
        public static byte[] BuildSetCalibrationReport(bool on)
            => RongYuanProtocol.BuildCommand(RongYuanProtocol.CmdCalibrationReport, (byte)(on ? 1 : 0));
        /// <summary>Mo/tat che do ghi moc MIN (opcode 28).</summary>
        public static byte[] BuildSetCalibrationMin(bool on)
            => RongYuanProtocol.BuildCommand(RongYuanProtocol.CmdCalibrationMin, (byte)(on ? 1 : 0));
        /// <summary>Mo/tat che do ghi moc MAX (opcode 30).</summary>
        public static byte[] BuildSetCalibrationMax(bool on)
            => RongYuanProtocol.BuildCommand(RongYuanProtocol.CmdCalibrationMax, (byte)(on ? 1 : 0));

        /// <summary>
        /// Cac buoc cua luong hieu chuan dang wizard: bao cao 27 bat ->
        /// moc MIN 28 bat -> (cho ~2 giay, nguoi dung nha het phim) -> 28 tat
        /// -> moc MAX 30 bat -> (nguoi dung nhan het co) -> 30 tat -> 27 tat.
        /// Trong luc hieu chuan, doc sub-op 254 de xem hanh trinh realtime.
        /// </summary>
        public static List<(string Label, byte[] Packet)> BuildCalibrationSteps()
            => new List<(string, byte[])>
            {
                ("Bật luồng báo cáo hiệu chuẩn (27)", BuildSetCalibrationReport(true)),
                ("Bắt đầu ghi mốc MIN — thả hết phím (28)", BuildSetCalibrationMin(true)),
                ("Kết thúc mốc MIN (28)", BuildSetCalibrationMin(false)),
                ("Bắt đầu ghi mốc MAX — nhấn hết cỡ từng phím (30)", BuildSetCalibrationMax(true)),
                ("Kết thúc mốc MAX (30)", BuildSetCalibrationMax(false)),
                ("Tắt luồng báo cáo hiệu chuẩn (27)", BuildSetCalibrationReport(false)),
            };
    }

    // =====================================================================
    // Duong man hinh TFT (theo ho so EPOMAKER — muc byte):
    //  1) (Tuy chon) xoa flash: SET 44, ACK magic AA AA 55 55.
    //  2) Goi chuan bi gui bang opcode GET: 165 (RGB565) / 169 (RGB888),
    //     header 18 byte:
    //       [0] opcode, [1] currentFrame, [2] frameNum, [3] frameDelay,
    //       [4..5] 2 byte thap cua tong do dai anh, [6] 0, [7] Bit7,
    //       [8..11] left/top/right/bottom (byte thap),
    //       [12..15] left/top/right/bottom (byte cao),
    //       [16..17] 2 byte cao cua tong do dai anh (do dai 32-bit tach).
    //     ACK thanh cong khi resp[1] == 1 (gui lai toi da 10 lan x 100 ms).
    //  3) Du lieu: opcode 37 (RGB565) / 41 (RGB888), header 8 byte
    //       [op, frame, frameNum, delay, chunkIdx u16-LE, lenChunk, Bit7]
    //     + 56 byte du lieu, nhip ~3 ms.
    // Dong goi pixel: RGB565 big-endian (che do "16"); thu tu quet
    // hang/cot cua tung firmware CHUA tai dung dut khoat trong ho so —
    // ban nay dung row-major va ghi ro gioi han trong BUILD-INFO.
    // =====================================================================
    public static class RongYuanScreen
    {
        public static byte PrepareOpcode(bool rgb888)
            => rgb888 ? RongYuanProtocol.CmdScreenPrepare888 : RongYuanProtocol.CmdScreenPrepare565;
        public static byte DataOpcode(bool rgb888)
            => rgb888 ? RongYuanProtocol.CmdScreen24Data : RongYuanProtocol.CmdTftData;

        public static byte[] BuildPrepare(bool rgb888, byte currentFrame, byte frameNum, byte frameDelay,
            int left, int top, int right, int bottom, int totalLength)
        {
            var p = new byte[RongYuanProtocol.ReportLength];
            p[0] = PrepareOpcode(rgb888);
            p[1] = currentFrame;
            p[2] = frameNum;
            p[3] = frameDelay;
            p[4] = (byte)(totalLength & 0xFF);
            p[5] = (byte)((totalLength >> 8) & 0xFF);
            p[6] = 0;
            p[8] = (byte)(left & 0xFF);
            p[9] = (byte)(top & 0xFF);
            p[10] = (byte)(right & 0xFF);
            p[11] = (byte)(bottom & 0xFF);
            p[12] = (byte)((left >> 8) & 0xFF);
            p[13] = (byte)((top >> 8) & 0xFF);
            p[14] = (byte)((right >> 8) & 0xFF);
            p[15] = (byte)((bottom >> 8) & 0xFF);
            p[16] = (byte)((totalLength >> 16) & 0xFF);
            p[17] = (byte)((totalLength >> 24) & 0xFF);
            return RongYuanProtocol.SealBit7(p);
        }

        public static bool CheckPrepareAck(byte[] reply)
            => reply != null && reply.Length > 1 && reply[1] == 1;

        public static List<byte[]> BuildDataChunks(bool rgb888, byte currentFrame, byte frameNum, byte frameDelay, byte[] imageData)
        {
            if (imageData == null) throw new ArgumentNullException(nameof(imageData));
            byte opcode = DataOpcode(rgb888);
            int chunks = Math.Max(1, (imageData.Length + RongYuanProtocol.ChunkLength - 1) / RongYuanProtocol.ChunkLength);
            var list = new List<byte[]>();
            for (int i = 0; i < chunks; i++)
            {
                int len = Math.Min(RongYuanProtocol.ChunkLength, imageData.Length - i * RongYuanProtocol.ChunkLength);
                var p = new byte[RongYuanProtocol.ReportLength];
                p[0] = opcode;
                p[1] = currentFrame;
                p[2] = frameNum;
                p[3] = frameDelay;
                p[4] = (byte)(i & 0xFF);
                p[5] = (byte)((i >> 8) & 0xFF);
                p[6] = (byte)len;
                if (len > 0) Array.Copy(imageData, i * RongYuanProtocol.ChunkLength, p, RongYuanProtocol.HeaderLength, len);
                list.Add(RongYuanProtocol.SealBit7(p));
            }
            return list;
        }

        /// <summary>Dong goi anh RGB (3 byte/pixel, row-major) thanh RGB565 big-endian.</summary>
        public static byte[] PackRgb565BE(byte[] rgbPixels)
        {
            if (rgbPixels == null || rgbPixels.Length % 3 != 0)
                throw new ArgumentException("Du lieu pixel phai la boi cua 3 byte (RGB).");
            var outp = new byte[rgbPixels.Length / 3 * 2];
            for (int i = 0, j = 0; i < rgbPixels.Length; i += 3, j += 2)
            {
                int v = ((rgbPixels[i] >> 3) << 11) | ((rgbPixels[i + 1] >> 2) << 5) | (rgbPixels[i + 2] >> 3);
                outp[j] = (byte)(v >> 8);
                outp[j + 1] = (byte)(v & 0xFF);
            }
            return outp;
        }

        /// <summary>Anh mau kiem tra: gradient ngang do->xanh la theo cot, them vien trang.</summary>
        public static byte[] BuildTestPatternRgb(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            var rgb = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int o = (y * width + x) * 3;
                    bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    rgb[o] = border ? (byte)255 : (byte)(x * 255 / Math.Max(1, width - 1));
                    rgb[o + 1] = border ? (byte)255 : (byte)(y * 255 / Math.Max(1, height - 1));
                    rgb[o + 2] = border ? (byte)255 : (byte)128;
                }
            return rgb;
        }

        /// <summary>Dong ho OLED (opcode 40): nam 2 byte big-endian o byte 8..9, roi thang/ngay/gio/phut/giay.</summary>
        public static byte[] BuildOledClock(DateTime now)
        {
            var p = new byte[RongYuanProtocol.ReportLength];
            p[0] = RongYuanProtocol.CmdOledClock;
            p[8] = (byte)((now.Year >> 8) & 0xFF);
            p[9] = (byte)(now.Year & 0xFF);
            p[10] = (byte)now.Month;
            p[11] = (byte)now.Day;
            p[12] = (byte)now.Hour;
            p[13] = (byte)now.Minute;
            p[14] = (byte)now.Second;
            return RongYuanProtocol.SealBit7(p);
        }
    }
}
