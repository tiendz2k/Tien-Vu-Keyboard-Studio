using System;
using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Driver Pack 5 (v3.9) — ho DareU All-in-One, trich byte-level tu WEB
    // driver chinh hang dr.dareu.com (docs/PROTOCOLS-DAREU.md; toan bo
    // bang chung [CODE] trong ho so, tru cong thuc checksum JM rut gon la
    // [SUY LUẬN] toan hoc — ghi ro tai cho). File nay THUAN LOGIC:
    //  - TgProtocol: ho TG / TG-RT (FwType 0/1/6/7) — feature report 64B,
    //    header 6 byte, khong checksum, ACK = nibble thap cua byte trang
    //    thai == 2 (poll 10 ms x 20).
    //  - OhidProtocol: ho OHID (FwType 4/5) — khung [0xAA, Len, Cmd,
    //    Checksum = Σ(byte+index), Data...], 64 byte.
    //  - JmProtocol: ho JM / QF (FwType 2/3) — report 16 byte, checksum
    //    (0x55 − Σ), truyen buffer 10 byte/goi toi dia chi BE.
    // File cai dat desktop cua hang CHUA BAO GIO duoc kiem toan (CDN chan
    // tai) — giao thuc o day lay tu ban WEB chinh hang cung the he.
    // =====================================================================

    // ------------------------- Ho TG / TG-RT -------------------------
    public static class TgProtocol
    {
        public const int VendorId = 0x260D;
        public const int PacketLen = 64;
        public const int HeaderLen = 6;
        public const int BlockSize = 48;          // TG_BLOCK_SIZE: du lieu moi goi da goi
        public const int AckPollIntervalMs = 10;  // poll receiveFeatureReport
        public const int AckPollMax = 20;
        public const int DongleExtraWaitMs = 50;  // qua dongle: cho them truoc poll

        // Lop lenh (CMD_CLASS trong ho so)
        public const byte ClassDevice = 0;
        public const byte ClassKey = 1;
        public const byte ClassButton = 2;
        public const byte ClassLighting = 3;
        public const byte ClassSensor = 4;
        public const byte ClassProfile = 5;
        public const byte ClassMacro = 6;
        public const byte ClassPower = 7;
        public const byte ClassAudio = 8;
        public const byte ClassDfu = 9;
        public const byte ClassTest = 10;
        public const byte ClassLcd = 11;
        public const byte ClassFlash = 12;
        public const byte ClassMagneticAxis = 13;
        public const byte GetFlag = 0x80; // GET_CMD: cmd | 0x80 = GET

        // Lenh lop Device
        public const byte DevFwVer = 0, DevHwVer = 1, DevSn = 2, DevMode = 3,
            DevPollingPeriod = 4, DevEdition = 5, DevMid = 6, DevName = 7, DevReset = 8,
            DevWirelessStatus = 32, DevRssi = 33;

        // Lenh lop Key
        public const byte KeyIdList = 0, KeyAttribute = 1, KeyDebounce = 2, KeyAssign = 3,
            KeyAnalogActuation = 4, KeyFnLock = 5, KeyWinLockMac = 6, KeyPerformance = 7,
            KeyBulkAssign = 8, KeySocdStatus = 9;

        // Lenh lop Lighting. FRAME (4) va CUSTOM (8) CHI co trong enum cua
        // hang — ban WEB khong co ma gui (stream tung phim cua TG chua trich
        // duoc) nen app nay KHONG co builder gui cho hai lenh nay.
        public const byte LedIdList = 0, LedAttribute = 1, LedEffect = 2, LedBrightness = 3,
            LedFrameNoSendCode = 4, LedCalData = 5, LedChargeCtrl = 6,
            LedDpiStageColor = 7, LedCustomNoSendCode = 8;

        // Lenh lop Profile / Macro / Power / LCD
        public const byte ProfileIdList = 0, ProfileCreate = 1, ProfileDelete = 2,
            ProfileActive = 3, ProfileName = 4, ProfileReset = 5;
        public const byte MacroStorageInfo = 0, MacroIdList = 1, MacroCreate = 2,
            MacroDelete = 3, MacroName = 4, MacroMemory = 5, MacroMiniDelay = 6;
        public const byte PowerBatStatus = 0, PowerTime2Dim = 1, PowerTime2Sleep = 2,
            PowerLowIndicator = 3, PowerMaxLedBrightness = 4, PowerAdc = 5, PowerUsbTime2Sleep = 6;
        public const byte LcdBrightness = 1, LcdLanguage = 3;

        // Lop 13 — Magnetic Axis (CLASS_MAGNETIC_AXIS_CMD_LIST)
        public const byte MaKeyFunction = 0, MaNormalTrigger = 1, MaSingleTrigger = 2,
            MaRapidTrigger = 3, MaMtFunction = 4, MaTglFunction = 5, MaDksFunction = 6,
            MaGlobalKeyTravel = 7, MaAcdTest = 8, MaTravelTest = 9, MaTravelCalibration = 10,
            MaStFunction = 11, MaDeadZoneAdjust = 12, MaMixedAxis = 13, MaRtAdaptive = 14,
            MaGameMode = 16, MaFuncAndSocd = 17;

        // MA_KEY_FUNCTION_ID
        public const byte KeyFnNormal = 0, KeyFnSingle = 1, KeyFnRapid = 2, KeyFnMt = 3,
            KeyFnTgl = 4, KeyFnDks = 5, KeyFnStSocd = 6;

        /// <summary>Dung goi TG 64 byte: [0]=TargetId, [1]=Size (so byte y
        /// nghia = 6 + payload), [2]=Class, [3]=Cmd (|0x80 neu GET),
        /// [4]=Profile, [5]=0 (ho so: chua thay gan — muc dich chua ro),
        /// payload tu byte 6.</summary>
        public static byte[] BuildPacket(byte cls, byte cmd, bool get, byte profile, byte[] payload, byte targetId = 0)
        {
            var p = new byte[PacketLen];
            int len = payload == null ? 0 : Math.Min(payload.Length, PacketLen - HeaderLen);
            p[0] = targetId;
            p[1] = (byte)(HeaderLen + len);
            p[2] = cls;
            p[3] = get ? (byte)(cmd | GetFlag) : cmd;
            p[4] = profile;
            if (len > 0) Array.Copy(payload, 0, p, HeaderLen, len);
            return p;
        }

        /// <summary>ACK cua TG: nibble thap cua byte trang thai (byte 0 cua
        /// dap tra) == 2.</summary>
        public static bool IsAck(byte[] reply)
            => reply != null && reply.Length >= 1 && (reply[0] & 0x0F) == 2;

        /// <summary>TargetId khi di qua dongle: (vi tri + 1) &lt;&lt; 4
        /// (suy tu GetWirelessConnectState trong ho so).</summary>
        public static byte DongleTargetId(int slotIndex) => (byte)((slotIndex + 1) << 4);

        // ---------- Lop Device / Key / Lighting / Power: builder tieu bieu ----------
        public static byte[] BuildGetFwVersion(byte profile = 0) => BuildPacket(ClassDevice, DevFwVer, true, profile, null);
        public static byte[] BuildGetName(byte profile = 0) => BuildPacket(ClassDevice, DevName, true, profile, null);
        public static byte[] BuildGetBattery(byte profile = 0) => BuildPacket(ClassPower, PowerBatStatus, true, profile, null);

        /// <summary>Giai dap tra BAT_STATUS: [status, level, maxLevel, critical] tai payload.</summary>
        public static (byte Status, byte Level, byte MaxLevel, byte Critical)? ParseBattery(byte[] reply)
        {
            if (reply == null || reply.Length < HeaderLen + 4) return null;
            return (reply[HeaderLen], reply[HeaderLen + 1], reply[HeaderLen + 2], reply[HeaderLen + 3]);
        }

        /// <summary>Ban ghi gan phim ASSIGN: dung 8 byte [keyId, layer,
        /// functionId, data0..data4].</summary>
        public static byte[] BuildKeyAssignRecord(byte keyId, byte layer, byte functionId, byte[] data5)
        {
            var r = new byte[8];
            r[0] = keyId; r[1] = layer; r[2] = functionId;
            if (data5 != null) Array.Copy(data5, 0, r, 3, Math.Min(5, data5.Length));
            return r;
        }

        /// <summary>Goi ASSIGN mot phim (lop Key, lenh 3).</summary>
        public static byte[] BuildKeyAssign(byte keyId, byte layer, byte functionId, byte[] data5, byte profile = 0)
            => BuildPacket(ClassKey, KeyAssign, false, profile, BuildKeyAssignRecord(keyId, layer, functionId, data5));

        /// <summary>Goi BULK_ASSIGN: payload[0] = so ban ghi, tiep theo la
        /// cac ban ghi 8 byte. Toi da 6 ban ghi/goi (48 byte du lieu).</summary>
        public static byte[] BuildKeyBulkAssign(IReadOnlyList<byte[]> records, byte profile = 0)
        {
            var payload = new List<byte> { (byte)Math.Min(records?.Count ?? 0, 6) };
            if (records != null)
                for (int i = 0; i < records.Count && i < 6; i++)
                    payload.AddRange(records[i]);
            return BuildPacket(ClassKey, KeyBulkAssign, false, profile, payload.ToArray());
        }

        /// <summary>Hieu ung den EFFECT (SET): payload [regionId, effect,
        /// flag, speed, colorCount, RGB x n...].</summary>
        public static byte[] BuildLedEffect(byte regionId, byte effect, byte flag, byte speed,
            IReadOnlyList<(byte R, byte G, byte B)> colors, byte profile = 0)
        {
            var payload = new List<byte> { regionId, effect, flag, speed, (byte)(colors?.Count ?? 0) };
            if (colors != null)
                foreach (var c in colors) { payload.Add(c.R); payload.Add(c.G); payload.Add(c.B); }
            return BuildPacket(ClassLighting, LedEffect, false, profile, payload.ToArray());
        }

        public static byte[] BuildGetLedEffect(byte regionId, byte profile = 0)
            => BuildPacket(ClassLighting, LedEffect, true, profile, new[] { regionId });

        /// <summary>Ngu 2 sleep: payload [control, secondsBE16].</summary>
        public static byte[] BuildSetTime2Sleep(byte control, ushort seconds, byte profile = 0)
            => BuildPacket(ClassPower, PowerTime2Sleep, false, profile,
                new[] { control, (byte)(seconds >> 8), (byte)(seconds & 0xFF) });

        // ---------- Co che da goi (multi-packet) ----------
        /// <summary>Chia du lieu thanh cac goi TG: moi goi mang toi da 48
        /// byte du lieu; HDR_SIZE = chunk + header phu + 2*a, trong do cac
        /// truong tong do dai + offset big-endian rong a byte (1..4) nam
        /// ngay sau header 6 byte (GetMultiPacketCmd/SetMultiPacketCmd).</summary>
        public static List<byte[]> BuildMultiPackets(byte cls, byte cmd, bool get, byte profile,
            byte[] data, int addrWidth = 2)
        {
            var list = new List<byte[]>();
            data = data ?? Array.Empty<byte>();
            int total = data.Length;
            int chunks = Math.Max(1, (total + BlockSize - 1) / BlockSize);
            for (int i = 0; i < chunks; i++)
            {
                int off = i * BlockSize;
                int len = Math.Min(BlockSize, total - off);
                var payload = new List<byte>();
                WriteBe(payload, total, addrWidth);
                WriteBe(payload, off, addrWidth);
                for (int k = 0; k < len; k++) payload.Add(data[off + k]);
                var p = BuildPacket(cls, cmd, get, profile, payload.ToArray());
                p[1] = (byte)(len + 2 * addrWidth + HeaderLen);
                list.Add(p);
            }
            return list;
        }

        private static void WriteBe(List<byte> dst, int value, int width)
        {
            for (int i = width - 1; i >= 0; i--) dst.Add((byte)(value >> (8 * i)));
        }

        // ---------- Lop 13: Magnetic Axis (chi TG-RT / FwType 6) ----------
        // Ma hoa hanh trinh theo TravelVersion: v0 = 1 byte/gia tri;
        // v1 (EK75 RT) = uint16 BE; v2 = uint16 BE nhung truong dau chia
        // doi he so khi chuyen doi (chi anh huong DEAD_ZONE — xem duoi).
        public static byte[] EncodeTravel(double mm, double stepMm, int travelVersion)
        {
            int raw = stepMm > 0 ? (int)Math.Round(mm / stepMm) : 0;
            if (travelVersion == 0) return new[] { (byte)raw };
            return new[] { (byte)(raw >> 8), (byte)(raw & 0xFF) };
        }

        public static double DecodeTravel(byte[] bytes, int offset, double stepMm, int travelVersion)
        {
            if (bytes == null || offset >= bytes.Length) return 0;
            int raw = travelVersion == 0 ? bytes[offset]
                : offset + 1 < bytes.Length ? (bytes[offset] << 8) | bytes[offset + 1] : bytes[offset];
            return raw * stepMm;
        }

        /// <summary>GLOBAL_KEY_TRAVEL (7): 1 gia tri toan cuc
        /// (v1: 2 byte BE o payload[0..1]).</summary>
        public static byte[] BuildSetGlobalTravel(double mm, double stepMm, int travelVersion, byte profile = 0)
            => BuildPacket(ClassMagneticAxis, MaGlobalKeyTravel, false, profile,
                EncodeTravel(mm, stepMm, travelVersion));

        public static byte[] BuildGetGlobalTravel(byte profile = 0)
            => BuildPacket(ClassMagneticAxis, MaGlobalKeyTravel, true, profile, null);

        /// <summary>KEY_FUNCTION (0) GET: payload [count, keyId...].</summary>
        public static byte[] BuildGetKeyFunctions(IReadOnlyList<byte> keyIds, byte profile = 0)
        {
            var payload = new List<byte> { (byte)(keyIds?.Count ?? 0) };
            if (keyIds != null) payload.AddRange(keyIds);
            return BuildPacket(ClassMagneticAxis, MaKeyFunction, true, profile, payload.ToArray());
        }

        /// <summary>SINGLE_TRIGGER (2): ban ghi [keyId, travel] — v1 = 3 byte.</summary>
        public static byte[] BuildSetSingleTrigger(byte keyId, double mm, double stepMm, int travelVersion, byte profile = 0)
        {
            var payload = new List<byte> { keyId };
            payload.AddRange(EncodeTravel(mm, stepMm, travelVersion));
            return BuildPacket(ClassMagneticAxis, MaSingleTrigger, false, profile, payload.ToArray());
        }

        /// <summary>RAPID_TRIGGER (3): ban ghi [keyId, init, pressed,
        /// released] — v1 = 7 byte, v0 = 4 byte.</summary>
        public static byte[] BuildSetRapidTrigger(byte keyId, double initMm, double pressMm, double releaseMm,
            double stepMm, int travelVersion, byte profile = 0)
        {
            var payload = new List<byte> { keyId };
            payload.AddRange(EncodeTravel(initMm, stepMm, travelVersion));
            payload.AddRange(EncodeTravel(pressMm, stepMm, travelVersion));
            payload.AddRange(EncodeTravel(releaseMm, stepMm, travelVersion));
            return BuildPacket(ClassMagneticAxis, MaRapidTrigger, false, profile, payload.ToArray());
        }

        /// <summary>DEAD_ZONE_ADJUST (12): payload [keyId, valueBE16]
        /// (luon 2 byte BE ke ca v0 theo ho so; v2 tu nhan/chia 2 khi
        /// chuyen doi noi bo — builder nay ghi gia tri tho da quy doi).</summary>
        public static byte[] BuildSetDeadZone(byte keyId, ushort rawValue, byte profile = 0)
            => BuildPacket(ClassMagneticAxis, MaDeadZoneAdjust, false, profile,
                new[] { keyId, (byte)(rawValue >> 8), (byte)(rawValue & 0xFF) });

        /// <summary>TRAVEL_CALIBRATION (10): payload [1] = vao hieu chuan, [0] = thoat.</summary>
        public static byte[] BuildTravelCalibration(bool enter, byte profile = 0)
            => BuildPacket(ClassMagneticAxis, MaTravelCalibration, false, profile,
                new[] { enter ? (byte)1 : (byte)0 });

        /// <summary>MT_FUNCTION (4): [1, keyId, trigger1, trigger2, delay] —
        /// delay LE o v0, BE o v1+.</summary>
        public static byte[] BuildSetMt(byte keyId, byte trigger1, byte trigger2, ushort delay,
            int travelVersion, byte profile = 0)
        {
            byte d0 = travelVersion == 0 ? (byte)(delay & 0xFF) : (byte)(delay >> 8);
            byte d1 = travelVersion == 0 ? (byte)(delay >> 8) : (byte)(delay & 0xFF);
            return BuildPacket(ClassMagneticAxis, MaMtFunction, false, profile,
                new[] { (byte)1, keyId, trigger1, trigger2, d0, d1 });
        }

        /// <summary>TGL_FUNCTION (5): [1, keyId, trigger1, delay(2B)].</summary>
        public static byte[] BuildSetTgl(byte keyId, byte trigger1, ushort delay, byte profile = 0)
            => BuildPacket(ClassMagneticAxis, MaTglFunction, false, profile,
                new[] { (byte)1, keyId, trigger1, (byte)(delay >> 8), (byte)(delay & 0xFF) });

        /// <summary>DKS_FUNCTION (6): ban ghi 10 byte [1, keyId, 4 trigger, 4 condition].</summary>
        public static byte[] BuildSetDks(byte keyId, byte[] triggers4, byte[] conditions4, byte profile = 0)
        {
            var payload = new List<byte> { 1, keyId };
            if (triggers4 != null) payload.AddRange(triggers4);
            while (payload.Count < 6) payload.Add(0);
            if (conditions4 != null) payload.AddRange(conditions4);
            while (payload.Count < 10) payload.Add(0);
            return BuildPacket(ClassMagneticAxis, MaDksFunction, false, profile, payload.ToArray());
        }

        /// <summary>ST_FUNCTION / SOCD (11): [1, keyId, groupId]; xoa lien ket = groupId 255.</summary>
        public static byte[] BuildSetSocd(byte keyId, byte groupId, byte profile = 0)
            => BuildPacket(ClassMagneticAxis, MaStFunction, false, profile,
                new[] { (byte)1, keyId, groupId });

        /// <summary>GAME_MODE (16): 1 byte theo profile.</summary>
        public static byte[] BuildSetGameMode(bool on, byte profile = 0)
            => BuildPacket(ClassMagneticAxis, MaGameMode, false, profile,
                new[] { on ? (byte)1 : (byte)0 });

        /// <summary>RT_ADAPTIVE (14): 1 byte 0/1.</summary>
        public static byte[] BuildSetRtAdaptive(bool on, byte profile = 0)
            => BuildPacket(ClassMagneticAxis, MaRtAdaptive, false, profile,
                new[] { on ? (byte)1 : (byte)0 });

        /// <summary>MIXED_AXIS (13): [count, (keyId, axisType)...].</summary>
        public static byte[] BuildSetMixedAxis(IReadOnlyList<(byte KeyId, byte AxisType)> pairs, byte profile = 0)
        {
            var payload = new List<byte> { (byte)(pairs?.Count ?? 0) };
            if (pairs != null) foreach (var p in pairs) { payload.Add(p.KeyId); payload.Add(p.AxisType); }
            return BuildPacket(ClassMagneticAxis, MaMixedAxis, false, profile, payload.ToArray());
        }
    }

    // ------------------------- Ho OHID -------------------------
    public static class OhidProtocol
    {
        public const int VendorId = 0x260D;
        public const int PacketLen = 64;
        public const byte Head = 0xAA;
        public const int MaxData = 60;
        public const int ResponsePollIntervalMs = 20;
        public const int ResponsePollMax = 20;

        // CommandIndex
        public const byte CmdOrder = 0, CmdSync = 1, CmdKey = 16, CmdMt = 17, CmdTgl = 18,
            CmdDks = 19, CmdAks = 20, CmdTrigger = 21, CmdLedEffect = 22, CmdRgbKey = 23,
            CmdRgbMatrix = 24, CmdMacroData = 25, CmdKeyDataReport = 26, CmdCurve = 27,
            CmdNormal = 28, CmdSingle = 29, CmdRt = 30, CmdRtSimple = 31, CmdMacro = 32,
            CmdMtp = 33, CmdQuick = 35, CmdFail = 255;

        // OrderIndex (cmd 0, data[0] = order id)
        public const byte OrderSaveUser = 2, OrderReload = 3, OrderFactory = 5,
            OrderQueryVersionHex = 7, OrderQuerySn = 9, OrderQuickMode = 16,
            OrderReportRate = 17, OrderQsLockWin = 32, OrderQsLinear = 36,
            OrderTrigger = 49, OrderProfileTab = 50;

        /// <summary>Checksum OHID: Σ (byte[i] + i) tren [head, len, cmd,
        /// data...], mod 256 (GetCheckSum trong ho so).</summary>
        public static byte ComputeChecksum(IReadOnlyList<byte> frameHeadAndData)
        {
            int sum = 0;
            for (int i = 0; i < frameHeadAndData.Count; i++) sum += frameHeadAndData[i] + i;
            return (byte)(sum & 0xFF);
        }

        /// <summary>Dung khung OHID 64 byte: [0]=0xAA, [1]=Len, [2]=Cmd
        /// (|0x80 neu la lenh ghi — CmdWrite), [3]=Checksum, [4..]=Data.</summary>
        public static byte[] BuildPacket(byte cmd, bool write, byte[] data)
        {
            var p = new byte[PacketLen];
            int len = data == null ? 0 : Math.Min(data.Length, MaxData);
            p[0] = Head;
            p[1] = (byte)len;
            p[2] = write ? (byte)(cmd | 0x80) : cmd;
            if (len > 0) Array.Copy(data, 0, p, 4, len);
            var head = new List<byte> { p[0], p[1], p[2] };
            for (int i = 0; i < len; i++) head.Add(p[4 + i]);
            p[3] = ComputeChecksum(head);
            return p;
        }

        public static bool VerifyChecksum(byte[] packet)
        {
            if (packet == null || packet.Length < 4 || packet[0] != Head) return false;
            int len = Math.Min((int)packet[1], MaxData);
            var head = new List<byte> { packet[0], packet[1], packet[2] };
            for (int i = 0; i < len && 4 + i < packet.Length; i++) head.Add(packet[4 + i]);
            return ComputeChecksum(head) == packet[3];
        }

        /// <summary>Lay phan Data cua mot khung (theo Len).</summary>
        public static byte[] DataOf(byte[] packet)
        {
            if (packet == null || packet.Length < 4) return Array.Empty<byte>();
            int len = Math.Min((int)packet[1], Math.Max(0, packet.Length - 4));
            var d = new byte[len];
            Array.Copy(packet, 4, d, 0, len);
            return d;
        }

        public static byte[] BuildOrder(byte orderId, byte[] extra = null, bool write = true)
        {
            var data = new List<byte> { orderId };
            if (extra != null) data.AddRange(extra);
            return BuildPacket(CmdOrder, write, data.ToArray());
        }

        /// <summary>SAVE_USER: lenh luu cau hinh — hau het lenh SET cua OHID
        /// deu goi tiep lenh nay.</summary>
        public static byte[] BuildSaveUser() => BuildOrder(OrderSaveUser);

        /// <summary>Giai FW tu dap tra QUERY_VERSION_HEX: FW = Data[5]<<16|Data[6]<<8|Data[7].</summary>
        public static int? ParseVersionHex(byte[] reply)
        {
            var d = DataOf(reply);
            if (d.Length < 8) return null;
            return (d[5] << 16) | (d[6] << 8) | d[7];
        }

        /// <summary>Profile dang chon tu dap tra PROFILE_TAB: Data[5] + 1.</summary>
        public static int? ParseProfileTab(byte[] reply)
        {
            var d = DataOf(reply);
            if (d.Length < 6) return null;
            return d[5] + 1;
        }

        // ---------- Khoi trigger OHID (ghi xong phai SAVE_USER) ----------
        /// <summary>NORMAL (28): 2 byte/phim [keyId, 0], toi da 30 phim/goi.</summary>
        public static List<byte[]> BuildNormalRecords(IReadOnlyList<byte> keyIds)
        {
            var list = new List<byte[]>();
            for (int i = 0; i < (keyIds?.Count ?? 0); i += 30)
            {
                var data = new List<byte>();
                for (int k = i; k < Math.Min(keyIds.Count, i + 30); k++) { data.Add(keyIds[k]); data.Add(0); }
                list.Add(BuildPacket(CmdNormal, true, data.ToArray()));
            }
            return list;
        }

        /// <summary>SINGLE (29): 4 byte/phim [keyId, 0, travelLE16], toi da 15 phim/goi.</summary>
        public static List<byte[]> BuildSingleRecords(IReadOnlyList<(byte KeyId, ushort TravelLe)> records)
        {
            var list = new List<byte[]>();
            for (int i = 0; i < (records?.Count ?? 0); i += 15)
            {
                var data = new List<byte>();
                for (int k = i; k < Math.Min(records.Count, i + 15); k++)
                {
                    data.Add(records[k].KeyId); data.Add(0);
                    data.Add((byte)(records[k].TravelLe & 0xFF));
                    data.Add((byte)(records[k].TravelLe >> 8));
                }
                list.Add(BuildPacket(CmdSingle, true, data.ToArray()));
            }
            return list;
        }

        /// <summary>RT (30): 5 byte/phim [keyId, 0, valueLE24], toi da 12 phim/goi.</summary>
        public static List<byte[]> BuildRtRecords(IReadOnlyList<(byte KeyId, int ValueLe24)> records)
        {
            var list = new List<byte[]>();
            for (int i = 0; i < (records?.Count ?? 0); i += 12)
            {
                var data = new List<byte>();
                for (int k = i; k < Math.Min(records.Count, i + 12); k++)
                {
                    int v = records[k].ValueLe24;
                    data.Add(records[k].KeyId); data.Add(0);
                    data.Add((byte)(v & 0xFF)); data.Add((byte)((v >> 8) & 0xFF)); data.Add((byte)((v >> 16) & 0xFF));
                }
                list.Add(BuildPacket(CmdRt, true, data.ToArray()));
            }
            return list;
        }

        /// <summary>RT_SIMPLE (31): 4 byte/phim [keyId, 0, valueLE16].</summary>
        public static List<byte[]> BuildRtSimpleRecords(IReadOnlyList<(byte KeyId, ushort ValueLe)> records)
        {
            var list = new List<byte[]>();
            for (int i = 0; i < (records?.Count ?? 0); i += 15)
            {
                var data = new List<byte>();
                for (int k = i; k < Math.Min(records.Count, i + 15); k++)
                {
                    data.Add(records[k].KeyId); data.Add(0);
                    data.Add((byte)(records[k].ValueLe & 0xFF));
                    data.Add((byte)(records[k].ValueLe >> 8));
                }
                list.Add(BuildPacket(CmdRtSimple, true, data.ToArray()));
            }
            return list;
        }

        /// <summary>KEY (16) gan phim: [keyIdLE16, functionId, valueLE16].</summary>
        public static byte[] BuildKeyAssign(ushort keyId, byte functionId, ushort value)
            => BuildPacket(CmdKey, true, new[]
            {
                (byte)(keyId & 0xFF), (byte)(keyId >> 8), functionId,
                (byte)(value & 0xFF), (byte)(value >> 8),
            });

        /// <summary>Cau truc khoi LED_EFFECT (22) doc tu dap tra: mau tinh
        /// o Data[8..39], Brightness Data[40], Effect Data[41], Speed
        /// Data[42], SleepTime Data[43], Open Data[44], RgbSleep Data[45],
        /// Flag Data[46].</summary>
        public sealed class LedEffectState
        {
            public byte[] StaticColors32;
            public byte Brightness, Effect, Speed, SleepTime, Open, RgbSleep, Flag;
        }

        public static LedEffectState ParseLedEffect(byte[] reply)
        {
            var d = DataOf(reply);
            if (d.Length < 47) return null;
            var s = new LedEffectState
            {
                Brightness = d[40], Effect = d[41], Speed = d[42], SleepTime = d[43],
                Open = d[44], RgbSleep = d[45], Flag = d[46],
                StaticColors32 = new byte[32],
            };
            Array.Copy(d, 8, s.StaticColors32, 0, 32);
            return s;
        }

        // RGB_KEY (23) / RGB_MATRIX (24): ten lenh co trong bang lenh cua
        // hang nhung DINH DANG BAN GHI khong co trong ho so web driver —
        // app khong co builder cho hai lenh nay (chi ghi log o lop thiet
        // bi), tranh doan byte.
    }

    // ------------------------- Ho JM / QF -------------------------
    public static class JmProtocol
    {
        public const int VendorId = 0x260D;
        public const int ReportId = 8;
        // Goi JM: hang xay mang 17 byte roi bo byte report-id dau khi gui;
        // dinh dang con lai [0]=0x08 nhom, [1]=lenh con, [3..4]=dia chi BE,
        // [5]=so luong/co, [6..15]=du lieu (dung 10 byte — khop quy tac
        // "10 B du lieu/goi" cua truyen buffer), [16]=checksum. Tong 17
        // byte tren duong truyen (report 16 B + byte nhom/report).
        public const int PacketLen = 17;
        public const int DataCapacity = 10;
        public const int BufferChunk = 10;   // 10 byte du lieu/goi khi truyen buffer
        public const int ResponsePollIntervalMs = 20;
        public const int ResponsePollMax = 5;

        public const byte CmdGetDeviceInfo = 1, CmdSetDriverMode = 2, CmdCheckActive = 3,
            CmdWriteBuffer = 7, CmdReadBuffer = 8;

        /// <summary>Checksum JM nguyen van tu ma nguon hang: s = Σ cac byte
        /// truoc; tra ve s &lt; 55 ? 85 − s : 341 − s. (Dang rut gon
        /// (0x55 − Σ) mod 256 la SUY LUAN toan hoc — ham nay giu nguyen
        /// cong thuc goc de khoi lech.)</summary>
        public static byte ComputeChecksum(IReadOnlyList<byte> bytesBefore)
        {
            int s = 0;
            foreach (var b in bytesBefore) s += b;
            return (byte)(s < 55 ? 85 - s : 341 - s);
        }

        /// <summary>Dung goi JM: [0]=0x08 nhom, [1]=lenh con, [2]=0,
        /// [3..4]=dia chi BE, [5]=so luong/co, [6..]=du lieu (toi da 10
        /// byte), byte cuoi = checksum tren cac byte truoc.</summary>
        public static byte[] BuildPacket(byte subCmd, ushort address, byte count, byte[] data)
        {
            var p = new byte[PacketLen];
            p[0] = ReportId;
            p[1] = subCmd;
            p[3] = (byte)(address >> 8);
            p[4] = (byte)(address & 0xFF);
            p[5] = count;
            if (data != null) Array.Copy(data, 0, p, 6, Math.Min(data.Length, DataCapacity));
            var before = new byte[PacketLen - 1];
            Array.Copy(p, before, PacketLen - 1);
            p[PacketLen - 1] = ComputeChecksum(before);
            return p;
        }

        public static bool VerifyChecksum(byte[] packet)
        {
            if (packet == null || packet.Length < PacketLen) return false;
            var before = new byte[PacketLen - 1];
            Array.Copy(packet, before, PacketLen - 1);
            return ComputeChecksum(before) == packet[PacketLen - 1];
        }

        /// <summary>GET_DEVICE_INFO (1): request chua 4 byte ngau nhien tai
        /// data (hang gui de tranh trung goi cu).</summary>
        public static byte[] BuildGetDeviceInfo(byte[] random4)
        {
            var data = new byte[4];
            if (random4 != null) Array.Copy(random4, data, Math.Min(4, random4.Length));
            return BuildPacket(CmdGetDeviceInfo, 0, 0, data);
        }

        /// <summary>Giai dap tra GET_DEVICE_INFO: CID/MID/JmType tai Data[9..11]
        /// (offset tinh tren goi dap tra 16 byte).</summary>
        public static (byte Cid, byte Mid, byte JmType)? ParseDeviceInfo(byte[] reply)
        {
            if (reply == null || reply.Length < 12) return null;
            return (reply[9], reply[10], reply[11]);
        }

        /// <summary>CHECK_ACTIVE (3): dap tra hop le khi Data[5] == 1.</summary>
        public static bool CheckActiveReply(byte[] reply)
            => reply != null && reply.Length >= 6 && reply[5] == 1;

        /// <summary>Truyen buffer ghi: chia 10 byte/goi, dia chi BE tu
        /// tang; count cong 128 cho cac goi khong phai goi cuoi (co
        /// "con tiep").</summary>
        public static List<byte[]> BuildWriteBuffer(ushort startAddress, byte[] data)
        {
            var list = new List<byte[]>();
            data = data ?? Array.Empty<byte>();
            int chunks = Math.Max(1, (data.Length + BufferChunk - 1) / BufferChunk);
            for (int i = 0; i < chunks; i++)
            {
                int len = Math.Min(BufferChunk, data.Length - i * BufferChunk);
                var chunk = new byte[len];
                if (len > 0) Array.Copy(data, i * BufferChunk, chunk, 0, len);
                byte count = (byte)(len + (i < chunks - 1 ? 128 : 0));
                list.Add(BuildPacket(CmdWriteBuffer, (ushort)(startAddress + i * BufferChunk), count, chunk));
            }
            return list;
        }

        /// <summary>Truyen buffer doc: cac goi READ_BUFFER (8) theo cung
        /// quy tac chia 10 byte/dia chi BE.</summary>
        public static List<byte[]> BuildReadBuffer(ushort startAddress, int totalBytes)
        {
            var list = new List<byte[]>();
            int chunks = Math.Max(1, (totalBytes + BufferChunk - 1) / BufferChunk);
            for (int i = 0; i < chunks; i++)
            {
                int len = Math.Min(BufferChunk, totalBytes - i * BufferChunk);
                byte count = (byte)(len + (i < chunks - 1 ? 128 : 0));
                list.Add(BuildPacket(CmdReadBuffer, (ushort)(startAddress + i * BufferChunk), count, null));
            }
            return list;
        }
    }
}
