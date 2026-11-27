using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Driver Pack 4 (v3.7) — nen tang WitMod (Redragon K617 HE va cac
    // nhan trang trang cung loi). Trich muc byte tu web driver chinh hang
    // da giai ma roi (docs/PROTOCOLS-WITMOD-FULL.md). File THUAN LOGIC.
    //
    // Khung goi: report ID 1, data 63 byte:
    //   [0]=opcode, [1]=sub, [2..3]=page BIG-ENDIAN, [4]=do dai payload,
    //   payload tai offset 5. KHONG checksum o bat ky dau tren duong gui.
    // Nhan dien: quet theo bang filter 36 cap VID:PID + usagePage 0xFF1B,
    // sau do lenh 0x0D (chuoi san pham CSV, truong index 4) + 0x01 (khoi
    // support) la probe quyet dinh. VID:PID rieng cua K617 HE khong co
    // trong code web — app desktop nhan dien bang tien to productName
    // "7153" (8 bien the layout).
    // =====================================================================

    /// <summary>Hang so lop phien WitMod (lop scheduler trong app web goc).</summary>
    public static class WitModSession
    {
        public const int DefaultTimeoutMs = 300;
        public const int ReadRetries = 2;
        public const int RetryBackoffMs = 300;
        public const int RecoveryDelayMs = 20;
        public const int WriteDelayMs = 5;
        public const int StrictAckTimeoutMs = 100;
        public const int CollectIdleTimeoutMs = 300;
        public const int StorePollIntervalMs = 60;
        public const int StoreSettleMs = 250;
        public const int StoreTimeoutMs = 2000;
    }

    public static class WitModProtocol
    {
        public const int UsagePage = 0xFF1B;
        public const int Usage = 0x91;
        public const int ReportId = 1;
        public const int PacketLen = 63;      // data (khong tinh report ID)
        public const int MaxPayload = 58;
        public const int MaxKeys = 132;

        // Opcode
        public const byte OpStatus = 0x01;
        public const byte OpSideList = 0x02;
        public const byte OpMainConfig = 0x07;
        public const byte OpSideConfig = 0x08;
        public const byte OpCustomLight = 0x09;
        public const byte OpMainList = 0x0A;
        public const byte OpBootloader = 0x0B;
        public const byte OpProductInfo = 0x0D;
        public const byte OpFnEditable = 0x0E;
        public const byte OpSettings = 0x14;
        public const byte OpSerial = 0x15;
        public const byte OpMusicRhythm = 0x17;
        public const byte OpKeymap = 0x18;
        public const byte OpMacro = 0x19;
        public const byte OpTrigger = 0x21;
        public const byte OpAdvancedKeys = 0x22;
        public const byte OpAdc = 0x23;
        public const byte OpSocd = 0x24;
        public const byte OpAxis = 0x25;
        public const byte OpDeadband = 0x26;

        /// <summary>Bang filter WebHID cua nen tang (36 cap VID:PID,
        /// usagePage 0xFF1B / usage 0x91) — trich tu webhid-filters.json.</summary>
        public static readonly (int Vid, int[] Pids)[] Filters =
        {
            (0x0416, new[] { 0x7000, 0x7372, 0x7402, 0x7410, 0x7472, 0x7474, 0x7500, 0x7501, 0x7502, 0x7503, 0x7600, 0x9258, 0xC345, 0xC375 }),
            (0x117F, new[] { 0x4006, 0x4007 }),
            (0x17EF, new[] { 0x61D4, 0x61D6 }),
            (0x2E3C, new[] { 0x7500, 0x7501, 0x7502, 0x7503, 0x7504, 0x7505, 0x7508, 0x7509, 0x750A, 0x750B, 0xC365, 0xC366, 0xC367, 0xC368, 0xC369, 0xC370, 0xC371 }),
            (0x34D3, new[] { 0x5301 }),
        };

        public static bool IsFilterPair(int vid, int pid)
        {
            foreach (var (v, pids) in Filters)
                if (v == vid && pids.Contains(pid)) return true;
            return false;
        }

        public static int FilterPairCount => Filters.Sum(f => f.Pids.Length);

        // ------------------------- Khung goi -------------------------
        /// <summary>Dung goi 63 byte: [opcode, sub, pageBE, len, payload...].</summary>
        public static byte[] BuildPacket(byte opcode, byte sub, int page, byte[] payload, int payloadOffset = 0, int payloadLen = -1)
        {
            var p = new byte[PacketLen];
            p[0] = opcode;
            p[1] = sub;
            p[2] = (byte)((page >> 8) & 0xFF);
            p[3] = (byte)(page & 0xFF);
            int len = payloadLen < 0
                ? (payload == null ? 0 : payload.Length - payloadOffset)
                : payloadLen;
            len = Math.Max(0, Math.Min(MaxPayload, len));
            p[4] = (byte)len;
            if (payload != null && len > 0)
                Array.Copy(payload, payloadOffset, p, 5, Math.Min(len, payload.Length - payloadOffset));
            return p;
        }

        public static byte OpcodeOf(byte[] packet) => packet != null && packet.Length > 0 ? packet[0] : (byte)0;
        public static byte SubOf(byte[] packet) => packet != null && packet.Length > 1 ? packet[1] : (byte)0;
        public static int PageOf(byte[] packet)
            => packet != null && packet.Length > 3 ? (packet[2] << 8) | packet[3] : 0;
        public static int PayloadLenOf(byte[] packet)
            => packet != null && packet.Length > 4 ? packet[4] : 0;

        public static byte[] PayloadOf(byte[] packet)
        {
            int len = Math.Min(PayloadLenOf(packet), Math.Max(0, (packet?.Length ?? 0) - 5));
            var data = new byte[len];
            if (len > 0) Array.Copy(packet, 5, data, 0, len);
            return data;
        }

        /// <summary>ACK cua lenh ghi = goi echo opcode (byte 0) + sub (byte 1).</summary>
        public static bool IsAckEcho(byte[] reply, byte opcode, byte sub)
            => reply != null && reply.Length >= 2 && reply[0] == opcode && reply[1] == sub;

        /// <summary>Matcher mac dinh cua lop phien: byte 0 bang opcode yeu cau.</summary>
        public static bool MatchesOpcode(byte[] reply, byte opcode)
            => reply != null && reply.Length >= 1 && reply[0] == opcode;

        // ------------------------- 0x01 trang thai + support -------------------------
        public sealed class StatusInfo
        {
            public byte Mode;
            public byte BatteryStatus;
            public byte BatteryPercent;
            public byte TriMode;
            public bool LockKeyEnable;
            public bool IsCalibrated;
            public bool IsMusicRhythmOn;
            // Khoi support (byte 5..22 cua goi; offset tuong doi support+0x00 = byte 5)
            public bool SupportCalibration, SupportCustomLight, SupportMoreSwitch, SupportPollRate,
                        SupportDeadBand, SupportMusicRhythm, SupportReportKeySettings, SupportAll6Key;
            public bool SupportHighPrecision, SupportLightPositionSwitch, SupportGamePad,
                        SupportRS, SupportRKRT, SupportDKSMouse, SupportMDS;
            public bool LockWin, LockShiftTab, LockAltF4, LockAltTab;
            public bool IsStoringData;
            public bool SixKeyMode;
        }

        /// <summary>Giai goi trang thai 0x01 (khoi support 18 byte tai byte 5..22).</summary>
        public static StatusInfo ParseStatus(byte[] reply)
        {
            if (reply == null || reply.Length < 23 || reply[0] != OpStatus) return null;
            var s = new StatusInfo
            {
                Mode = reply[1],
                BatteryStatus = reply[8],
                BatteryPercent = reply[9],
                TriMode = reply[10],
                LockKeyEnable = reply[5] != 0,
                IsCalibrated = reply[14] != 0,
                IsMusicRhythmOn = reply[16] != 0,
            };
            byte f1 = reply[17]; // support+0x0C
            s.SupportCalibration = (f1 & 0x01) != 0;
            s.SupportCustomLight = (f1 & 0x02) != 0;
            s.SupportMoreSwitch = (f1 & 0x04) != 0;
            s.SupportPollRate = (f1 & 0x08) != 0;
            s.SupportDeadBand = (f1 & 0x10) != 0;
            s.SupportMusicRhythm = (f1 & 0x20) != 0;
            s.SupportReportKeySettings = (f1 & 0x40) != 0;
            s.SupportAll6Key = (f1 & 0x80) != 0;
            byte f2 = reply[18]; // support+0x0D
            s.SupportHighPrecision = (f2 & 0x01) != 0;
            s.SupportLightPositionSwitch = (f2 & 0x02) != 0;
            s.SupportGamePad = (f2 & 0x04) != 0;
            s.SupportRS = (f2 & 0x08) != 0;
            s.SupportRKRT = (f2 & 0x10) != 0;
            s.SupportDKSMouse = (f2 & 0x20) != 0;
            s.SupportMDS = (f2 & 0x40) != 0;
            byte f3 = reply[20]; // support+0x0F
            s.LockWin = (f3 & 0x01) != 0;
            s.LockShiftTab = (f3 & 0x02) != 0;
            s.LockAltF4 = (f3 & 0x04) != 0;
            s.LockAltTab = (f3 & 0x08) != 0;
            byte f4 = reply[22]; // support+0x11
            s.SixKeyMode = (f4 & 0x01) != 0;
            s.IsStoringData = (f4 & 0x02) != 0;
            return s;
        }

        // ------------------------- Dinh danh 0x0D / 0x15 -------------------------
        public static byte[] BuildGetStatus() => BuildPacket(OpStatus, 0, 0, null);
        public static byte[] BuildGetProductInfo() => BuildPacket(OpProductInfo, 0, 0, null);
        public static byte[] BuildGetSerial() => BuildPacket(OpSerial, 0, 0, null);

        /// <summary>Giai dap tra 0x0D: chuoi CSV tai offset 5, do dai tai
        /// byte 4; truong index 4 la dinh danh product. byte 3 = ma trang
        /// thai (0 = OK).</summary>
        public static string ParseProductId(byte[] reply)
        {
            var fields = ParseProductFields(reply);
            return fields != null && fields.Length > 4 ? fields[4] : null;
        }

        public static string[] ParseProductFields(byte[] reply)
        {
            if (reply == null || reply.Length < 5 || reply[0] != OpProductInfo) return null;
            int len = Math.Min(reply[4], reply.Length - 5);
            if (len <= 0) return null;
            string csv = Encoding.ASCII.GetString(reply, 5, len).TrimEnd('\0');
            return csv.Split(',');
        }

        public static string ParseSerial(byte[] reply)
        {
            if (reply == null || reply.Length < 5 || reply[0] != OpSerial) return null;
            int len = Math.Min(reply[4] == 0 ? 0x20 : reply[4], reply.Length - 5);
            if (len <= 0) return null;
            return Encoding.ASCII.GetString(reply, 5, len).TrimEnd('\0');
        }

        // ------------------------- Den 0x07/0x08/0x0A/0x02 -------------------------
        public sealed class LightConfig
        {
            public byte Mode, Brightness, Speed;
            public byte FgR, FgG, FgB, BgR, BgG, BgB;
            public byte Direction, FullColor, Power, Timer;

            public byte[] ToBytes()
                => new[] { Mode, Brightness, Speed, FgR, FgG, FgB, BgR, BgG, BgB, Direction, FullColor, Power, Timer, (byte)0 };

            public static LightConfig FromBytes(byte[] b, int off = 0)
            {
                if (b == null || b.Length < off + 13) return null;
                return new LightConfig
                {
                    Mode = b[off], Brightness = b[off + 1], Speed = b[off + 2],
                    FgR = b[off + 3], FgG = b[off + 4], FgB = b[off + 5],
                    BgR = b[off + 6], BgG = b[off + 7], BgB = b[off + 8],
                    Direction = b[off + 9], FullColor = b[off + 10], Power = b[off + 11], Timer = b[off + 12],
                };
            }
        }

        public static byte[] BuildGetLightConfig(bool side)
            => BuildPacket(side ? OpSideConfig : OpMainConfig, 1, 0, null);
        public static byte[] BuildSetLightConfig(bool side, LightConfig cfg)
            => BuildPacket(side ? OpSideConfig : OpMainConfig, 0, 0, cfg.ToBytes());
        public static byte[] BuildGetLightingList(bool side)
            => BuildPacket(side ? OpSideList : OpMainList, 0, 0, null);

        // ------------------------- Custom light 0x09 -------------------------
        public const int CustomLightBytes = 396; // 132 phim x RGB
        public const int CustomLightPages = 8;
        public const int CustomLightChunk = 54;  // 18 phim x 3

        public static byte[] BuildCustomLightReadLayer() => BuildPacket(OpCustomLight, 0x20, 0, null);
        public static byte[] BuildCustomLightReadPage(int layer, int page)
            => BuildPacket(OpCustomLight, (byte)(0x80 + layer), page, null);
        public static List<byte[]> BuildCustomLightWrite(int layer, byte[] rgb396)
        {
            var list = new List<byte[]>(CustomLightPages);
            for (int page = 0; page < CustomLightPages; page++)
                list.Add(BuildPacket(OpCustomLight, (byte)layer, page, rgb396, page * CustomLightChunk, CustomLightChunk));
            return list;
        }

        // ------------------------- Keymap 0x18 -------------------------
        public const int KeymapChunk = 56;   // 14 phim x 4 byte
        public const int KeymapReadPages = 10;

        public static byte[] BuildKeymapReadPage(bool fnLayer, int page)
            => BuildPacket(OpKeymap, fnLayer ? (byte)0x82 : (byte)0x80, page, null);

        /// <summary>Ghi keymap: ban ghi 4 byte/phim {code1, hidCode, code3,
        /// code4}, cat 56 byte/goi; sub ghi = 0 (default) / 2 (Fn).</summary>
        public static List<byte[]> BuildKeymapWrite(bool fnLayer, byte[] records)
        {
            var list = new List<byte[]>();
            int pages = (records.Length + KeymapChunk - 1) / KeymapChunk;
            for (int page = 0; page < pages; page++)
                list.Add(BuildPacket(OpKeymap, fnLayer ? (byte)2 : (byte)0, page,
                    records, page * KeymapChunk, Math.Min(KeymapChunk, records.Length - page * KeymapChunk)));
            return list;
        }

        // ------------------------- Macro 0x19 -------------------------
        public const int MacroImageBytes = 256;
        public const int MacroChunk = 58;

        /// <summary>Ghi 1 slot macro: anh 256 byte cat 58 byte/goi (5 goi),
        /// sub = slot, page BE.</summary>
        public static List<byte[]> BuildMacroWrite(int slot, byte[] image256)
        {
            var list = new List<byte[]>();
            int pages = (MacroImageBytes + MacroChunk - 1) / MacroChunk;
            for (int page = 0; page < pages; page++)
                list.Add(BuildPacket(OpMacro, (byte)slot, page, image256,
                    page * MacroChunk, Math.Min(MacroChunk, Math.Max(0, MacroImageBytes - page * MacroChunk))));
            return list;
        }

        public static byte[] BuildMacroRead(int slot) => BuildPacket(OpMacro, (byte)(0x80 + slot), 0, null);
        public static byte[] BuildMacroDelete(int slot) => BuildPacket(OpMacro, 3, 0, new[] { (byte)slot }, 0, 1);

        /// <summary>ACK ghi macro la su kien thiet bi tu phat: goi 0x19 voi sub &lt; 0x80.</summary>
        public static bool IsMacroWriteFinish(byte[] packet)
            => packet != null && packet.Length >= 2 && packet[0] == OpMacro && packet[1] < 0x80;

        // ------------------------- Cai dat 0x14 -------------------------
        public static byte[] BuildGetSleepTimer() => BuildPacket(OpSettings, 2, 0, null);

        public static (int SleepSeconds, int StandbySeconds) ParseSleepTimer(byte[] reply)
        {
            if (reply == null || reply.Length < 12) return (0, 0);
            return ((reply[8] << 8) | reply[9], (reply[10] << 8) | reply[11]);
        }

        public static byte[] BuildSetSleepTimer(int sleepSeconds, int standbySeconds)
        {
            var p = BuildPacket(OpSettings, 1, 0, null);
            p[8] = (byte)(sleepSeconds >> 8); p[9] = (byte)sleepSeconds;
            p[10] = (byte)(standbySeconds >> 8); p[11] = (byte)standbySeconds;
            return p;
        }

        /// <summary>Ghi cac co cai dat: byte4=1; [12]=lockWin, [13]=lockShiftTab,
        /// [14]=lockAltF4, [15]=lockAltTab, [17]=superPerformance, [18]=6-key.</summary>
        public static byte[] BuildSetDeviceSettings(bool lockWin, bool lockShiftTab, bool lockAltF4,
            bool lockAltTab, bool superPerformance, bool sixKeyMode)
        {
            var p = BuildPacket(OpSettings, 0, 0, null);
            p[4] = 1;
            p[12] = lockWin ? (byte)1 : (byte)0;
            p[13] = lockShiftTab ? (byte)1 : (byte)0;
            p[14] = lockAltF4 ? (byte)1 : (byte)0;
            p[15] = lockAltTab ? (byte)1 : (byte)0;
            p[17] = superPerformance ? (byte)1 : (byte)0;
            p[18] = sixKeyMode ? (byte)1 : (byte)0;
            return p;
        }

        public static byte[] BuildResetKeyboard()
        {
            var p = BuildPacket(OpSettings, 0, 0, null);
            p[4] = 1; p[5] = 1;
            return p;
        }

        public static byte[] BuildFactoryReset() => BuildPacket(OpSettings, 2, 0, null);
        public static byte[] BuildSetGamePadMode(byte mode)
        {
            var p = BuildPacket(OpSettings, 3, 0, null);
            p[4] = 1; p[5] = mode;
            return p;
        }

        // ------------------------- Trigger / hieu chuan 0x21 -------------------------
        // Lenh con nam tai byte 5 (payload[0]); byte 4 = 0x18 voi hau het
        // lenh con trigger (rieng doc poll-rate dat byte 4 = 1).
        public static byte[] BuildTriggerPacket(byte subCommand, byte[] rest)
        {
            var p = BuildPacket(OpTrigger, 0, 0, null);
            p[4] = 0x18;
            p[5] = subCommand;
            if (rest != null) Array.Copy(rest, 0, p, 6, Math.Min(rest.Length, PacketLen - 6));
            return p;
        }

        /// <summary>Mo test trigger (lenh con 0x02): bitmap 22 byte tai byte 6,
        /// byte[col] |= 1&lt;&lt;row cho moi phim can test.</summary>
        public static byte[] BuildOpenTriggerTest(IEnumerable<(int Row, int Col)> keys)
        {
            var bitmap = new byte[22];
            foreach (var (row, col) in keys)
                if (col >= 0 && col < 22 && row >= 0 && row < 8)
                    bitmap[col] |= (byte)(1 << row);
            var rest = new byte[22];
            Array.Copy(bitmap, rest, 22);
            // bitmap bat dau tai byte 6 cua goi = rest[0] trong BuildTriggerPacket
            return BuildTriggerPacket(0x02, rest);
        }

        public static byte[] BuildCloseTriggerTest() => BuildTriggerPacket(0x03, null);
        public static byte[] BuildGetTriggerConfig() => BuildTriggerPacket(0x04, null);
        public static byte[] BuildResetTrigger() => BuildTriggerPacket(0x06, null);

        /// <summary>Hieu chuan 1 phim (0x07): byte6=row, byte7=col.</summary>
        public static byte[] BuildCalibrateKey(int row, int col)
            => BuildTriggerPacket(0x07, new[] { (byte)row, (byte)col });

        /// <summary>Hieu chuan moi phim (0x08): byte6 = 0 bat dau / 1 dung.</summary>
        public static byte[] BuildCalibrateAll(bool start)
            => BuildTriggerPacket(0x08, new[] { start ? (byte)0 : (byte)1 });

        /// <summary>Doc poll rate (lenh con 0x09, byte4=1): dap tra rate tai byte 6.</summary>
        public static byte[] BuildGetPollRate()
        {
            var p = BuildPacket(OpTrigger, 0, 0, null);
            p[4] = 1; p[5] = 0x09;
            return p;
        }

        /// <summary>Ghi poll rate: byte5=9, byte6=1, byte7=rate (1/2/4/8 &lt;-&gt; 1k/2k/4k/8k Hz).</summary>
        public static byte[] BuildSetPollRate(int rate)
            => BuildTriggerPacket(0x09, new[] { (byte)1, (byte)rate });

        public static int PollRateHz(int rate) => rate >= 1 && rate <= 8 ? rate * 1000 : 0;

        public static byte[] BuildGetPresetIndex() => BuildTriggerPacket(0x11, null);
        public static byte[] BuildSetPresetIndex(int index) => BuildTriggerPacket(0x12, new[] { (byte)index });

        /// <summary>Giai dap tra doc cau hinh trigger (0x04): maxTriggerTravel
        /// = [9]&lt;&lt;8 | [6]; triggerUnitRaw tai byte 7 (0 -&gt; 10);
        /// minTriggerTravelRaw tai byte 10.</summary>
        public static (int MaxTravel, int UnitRaw, int MinTravelRaw) ParseTriggerConfig(byte[] reply)
        {
            if (reply == null || reply.Length < 11) return (0, 10, 0);
            int unit = reply[7] == 0 ? 10 : reply[7];
            return ((reply[9] << 8) | reply[6], unit, reply[10]);
        }

        // ------------------------- Advanced keys 0x22 -------------------------
        public const int AdvancedMaxSlots = 40;

        public static byte[] BuildAdvancedReadPage(int page)
            => BuildPacket(OpAdvancedKeys, 0x80, page, null);

        /// <summary>Ghi 1 slot advanced key: sub = so slot, len = 0x32,
        /// type tai byte 5 (1=DKS, 2=MT, 3=TGL, 4=RS, 5=RKRT, 6=MDS).</summary>
        public static byte[] BuildAdvancedWrite(int slot, byte[] entry50)
            => BuildPacket(OpAdvancedKeys, (byte)slot, 0, entry50, 0, Math.Min(0x32, entry50?.Length ?? 0));

        // ------------------------- SOCD 0x24 -------------------------
        public const int SocdMaxSlots = 20;
        public const int SocdChunk = 40; // 10 muc x 4 byte

        /// <summary>Ghi danh sach SOCD: 2 trang x 10 muc {state, model, keyCode1, keyCode2}.</summary>
        public static List<byte[]> BuildSocdWrite(byte[] entries80)
        {
            var list = new List<byte[]>(2);
            for (int page = 0; page < 2; page++)
                list.Add(BuildPacket(OpSocd, 0, page, entries80, page * SocdChunk, SocdChunk));
            return list;
        }

        public static byte[] BuildSocdReadPage(int page) => BuildPacket(OpSocd, 1, page, null);
        public static byte[] BuildSocdGetPower() => BuildPacket(OpSocd, 2, 0, null);
        public static byte[] BuildSocdSetPower(bool on) => BuildPacket(OpSocd, 3, 0, new[] { on ? (byte)1 : (byte)0 });

        // ------------------------- Switch tung phim 0x25 / Deadband 0x26 -------------------------
        public const int DeadbandImageBytes = 264; // 132 phim x 2 byte
        public const int PageChunk58 = 58;

        public static byte[] BuildAxisGetList() => BuildPacket(OpAxis, 0, 0, null);
        public static byte[] BuildAxisReadPage(int page) => BuildPacket(OpAxis, 2, page, null);

        public static List<byte[]> BuildAxisWrite(byte[] perKeySwitch)
        {
            var list = new List<byte[]>();
            int pages = (perKeySwitch.Length + PageChunk58 - 1) / PageChunk58;
            for (int page = 0; page < pages; page++)
                list.Add(BuildPacket(OpAxis, 1, page, perKeySwitch, page * PageChunk58,
                    Math.Min(PageChunk58, perKeySwitch.Length - page * PageChunk58)));
            return list;
        }

        public static byte[] BuildDeadbandReadPage(int page) => BuildPacket(OpDeadband, 0, page, null);

        /// <summary>Ghi/reset deadband: anh 264 byte, sub=1, cat 58 byte/goi, ACK echo.</summary>
        public static List<byte[]> BuildDeadbandWrite(byte[] image264)
        {
            var list = new List<byte[]>();
            int pages = (DeadbandImageBytes + PageChunk58 - 1) / PageChunk58;
            for (int page = 0; page < pages; page++)
                list.Add(BuildPacket(OpDeadband, 1, page, image264, page * PageChunk58,
                    Math.Min(PageChunk58, Math.Max(0, DeadbandImageBytes - page * PageChunk58))));
            return list;
        }
    }
}
