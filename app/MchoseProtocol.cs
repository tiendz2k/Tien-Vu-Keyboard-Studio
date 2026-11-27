using System;
using System.Collections.Generic;
using System.Text;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Driver Pack 3 — MCHOSE (4 stack), trich tu MCHOSE M HUB
    // (app chinh hang da ky so, giai phau tinh) + SDK CZ/QHW dung chung.
    //
    // Stack GLW/CZ (Ace/Jet/G-series HE): usagePage 0xFF00, usage 1,
    // reportId 0, goi 64 byte qua output/input report:
    //   [0]=0x55 co, [1]=cmd, [2]=0, [3]=checksum, [4]=len,
    //   [5]=offset lo, [6]=offset hi, [7]=0, [8..]=payload toi da 56B.
    //   Doc: payload rong, byte len = so byte muon doc (data arg {size}).
    //   Tra loi: co 0xAA, khop cmd + offset, len_tra <= len_gui.
    //
    // !!! CHECKSUM GLW LA PHAN TU DUY NHAT CHUA KIEM CHUNG !!!
    // Cong thuc goc: byte3 = 255 & b()([len, offLo, offHi, 0, ...payload])
    // trong do b() la mot ham cua module ngoai (lodash) KHONG nam trong
    // cac file da thu thap — gia thuyet manh nhat la TONG cac byte mod 256
    // (lodash sum). Ban nay cai dat sum-mod-256 va danh dau ro o day,
    // trong code device va trong BUILD-INFO. Neu sai, moi goi GLW se bi
    // firmware tu choi — vi vay khoi GLW van nam sau khoa ghi 3 lop.
    // =====================================================================
    public static class GlwProtocol
    {
        public const int PacketLen = 64;
        public const int MaxPayload = 56;
        public const byte SendFlag = 0x55;
        public const byte ReplyFlag = 0xAA;

        // --- opcode (cmd byte 1) ---
        public const byte CmdGetInfo = 3;
        public const byte CmdGetBase = 4;
        public const byte CmdSetBase = 14;
        public const byte CmdGetFuncConfig = 5;
        public const byte CmdSetFuncConfig = 6;
        public const byte CmdGetKeyMatrix = 7;        // lop Fn (IsFn=true)
        public const byte CmdGetKeyMatrixPlain = 8;   // lop thuong
        public const byte CmdSetKeyMatrix = 9;
        public const byte CmdGetCustomLight = 10;
        public const byte CmdSetKeyColor = 11;
        public const byte CmdGetMacro = 12;
        public const byte CmdSetMacro = 13;
        public const byte CmdGetKeyTrigger = 160;     // 0xA0
        public const byte CmdSetKeyTrigger = 161;     // 0xA1
        public const byte CmdGetDks = 162;            // 0xA2
        public const byte CmdSetDks = 163;            // 0xA3
        public const byte CmdGetMt = 164;             // 0xA4
        public const byte CmdSetMt = 165;             // 0xA5
        public const byte CmdGetTgl = 166;            // 0xA6
        public const byte CmdSetTgl = 167;            // 0xA7
        public const byte CmdStartCalibration = 168;  // 0xA8, payload [0]
        public const byte CmdEndCalibration = 169;    // 0xA9, payload [0]
        public const byte CmdRestartCalibration = 177; // 0xB1, payload [0]
        public const byte CmdMusicSide2 = 220;
        public const byte CmdMusicMain = 221;
        public const byte CmdGetRealtimeColor = 222;
        public const byte CmdMusicSide = 223;
        public const byte CmdFactoryReset = 238;      // 0xEE, payload [0xFF]=tat ca
        public const byte CmdGetCustomData = 241;
        public const byte CmdSetCustomData = 242;

        // --- hang so vung nho ---
        public const int MaxProfiles = 4;
        public const int MaxLayers = 4;
        public const int MaxKeys = 128;
        public const int TriggerKeyBytes = 8;
        public const int TriggerRegionBytes = 1024;   // 128 phim x 8B / profile
        public const int DksRegionBytes = 768;        // 32 slot x 24B / profile
        public const int DksSlots = 32;
        public const int MtRegionBytes = 256;         // 32 slot x 8B / profile
        public const int MtSlots = 32;
        public const int TglRegionBytes = 128;        // 32 slot x 4B / profile
        public const int TglSlots = 32;
        public const int FuncConfigBytes = 64;        // / profile, offset 64*profile
        public const int MaxDeadZone = 50;

        /// <summary>
        /// Checksum GLW — GIA THUYET sum-mod-256 (xem ghi chu dau file):
        /// tong cac byte [len, offLo, offHi, 0, ...payload] mod 256.
        /// </summary>
        public static byte Checksum(int len, int offset, byte[] payload, int payloadLen)
        {
            int s = (len & 0xFF) + (offset & 0xFF) + ((offset >> 8) & 0xFF) + 0;
            for (int i = 0; i < payloadLen; i++) s += payload[i];
            return (byte)(s & 0xFF);
        }

        /// <summary>Goi ghi: payload toi da 56 byte tai offset 16-bit.</summary>
        public static byte[] BuildWrite(byte cmd, int offset, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            if (payload.Length > MaxPayload) throw new ArgumentException("GLW payload > 56 byte");
            var p = new byte[PacketLen];
            p[0] = SendFlag; p[1] = cmd; p[2] = 0;
            p[4] = (byte)payload.Length;
            p[5] = (byte)(offset & 0xFF); p[6] = (byte)((offset >> 8) & 0xFF);
            p[7] = 0;
            Array.Copy(payload, 0, p, 8, payload.Length);
            p[3] = Checksum(p[4], offset, payload, payload.Length);
            return p;
        }

        /// <summary>Goi doc: byte len = so byte muon doc (data arg {size}).</summary>
        public static byte[] BuildRead(byte cmd, int offset, int size)
        {
            if (size < 1 || size > 255) throw new ArgumentException("GLW read size 1..255");
            var p = new byte[PacketLen];
            p[0] = SendFlag; p[1] = cmd; p[2] = 0;
            p[4] = (byte)size;
            p[5] = (byte)(offset & 0xFF); p[6] = (byte)((offset >> 8) & 0xFF);
            p[7] = 0;
            p[3] = Checksum(size, offset, Array.Empty<byte>(), 0);
            return p;
        }

        /// <summary>
        /// Kiem tra goi tra loi GLW: co 0xAA, cung cmd, cung offset,
        /// do dai tra ve khong vuot qua do dai da gui.
        /// </summary>
        public static bool MatchReply(byte[] sent, byte[] reply)
        {
            if (sent == null || reply == null || reply.Length < 8) return false;
            return reply[0] == ReplyFlag
                && reply[1] == sent[1]
                && reply[4] <= sent[4]
                && reply[5] == sent[5]
                && reply[6] == sent[6];
        }

        /// <summary>Du lieu huu ich cua goi tra loi (tu byte 8, dai = reply[4]).</summary>
        public static byte[] ReplyData(byte[] reply)
        {
            int n = Math.Min(reply[4], reply.Length - 8);
            var d = new byte[n];
            Array.Copy(reply, 8, d, 0, n);
            return d;
        }

        /// <summary>Cat mot vung byte thanh cac goi ghi 56 byte lien tiep.</summary>
        public static List<byte[]> ChunkWrite(byte cmd, int baseOffset, byte[] data)
        {
            var list = new List<byte[]>();
            for (int off = 0; off < data.Length; off += MaxPayload)
            {
                int n = Math.Min(MaxPayload, data.Length - off);
                var part = new byte[n];
                Array.Copy(data, off, part, 0, n);
                list.Add(BuildWrite(cmd, baseOffset + off, part));
            }
            return list;
        }

        /// <summary>Danh sach goi doc bao phu [baseOffset, baseOffset+total).</summary>
        public static List<byte[]> ChunkRead(byte cmd, int baseOffset, int total)
        {
            var list = new List<byte[]>();
            for (int off = 0; off < total; off += MaxPayload)
            {
                int n = Math.Min(MaxPayload, total - off);
                list.Add(BuildRead(cmd, baseOffset + off, n));
            }
            return list;
        }

        // ---------------- Thong tin thiet bi (cmd 3, 56 byte) ----------------
        public sealed class GlwInfo
        {
            public string FwVer;       // LE16 cua byte 0..1, dang hex
            public string RfFwVer;     // LE16 cua byte 2..3, dang hex
            public bool ReportRateEnhanced;  // byte 6 bit 0
            public bool U3Mode;              // byte 6 bit 1
            public byte[] Raw;
        }

        public static GlwInfo ParseInfo(byte[] d)
        {
            var info = new GlwInfo { Raw = d };
            if (d == null || d.Length < 7) return info;
            info.FwVer = ((d[1] << 8) | d[0]).ToString("X");
            info.RfFwVer = ((d[3] << 8) | d[2]).ToString("X");
            info.ReportRateEnhanced = (d[6] & 1) != 0;
            info.U3Mode = (d[6] & 2) != 0;
            return info;
        }

        // ---------------- Base (cmd 4): thu tu profile ----------------
        public sealed class GlwBase
        {
            public int ProfileIndex;
            public int ProfileLength;
            public byte[] Order;
        }

        public static GlwBase ParseBase(byte[] d)
        {
            var b = new GlwBase();
            if (d == null || d.Length < 2) return b;
            b.ProfileIndex = d[0];
            b.ProfileLength = d[1];
            int n = Math.Min(b.ProfileLength, d.Length - 2);
            b.Order = new byte[n];
            Array.Copy(d, 2, b.Order, 0, n);
            return b;
        }

        // ---------------- FuncConfig (cmd 5, 64B/profile) ----------------
        // Layout trich tu cac setter/getter cua SDK CZ (xem ho so). Chi cac
        // truong da dat ten trong SDK moi duoc giai ma; byte den
        // (den chinh/dem) chua duoc anh xa day du — khong giai ma doan.
        public sealed class GlwFuncConfig
        {
            public bool MacMode;           // byte 1, nibble thap
            public int ReportRateIndex;    // byte 4, nibble thap
            public int TickRate;           // byte 4, nibble cao
            public int GlobalDeadZone;     // byte 5 (0..50)
            public bool LockWin;           // byte 6 bit 0
            public bool LockAltTap;        // byte 6 bit 1
            public bool LockAltF4;         // byte 6 bit 2
            public bool TachyonMode;       // byte 7 bit 0
            public bool BottomRapidTrigger;// byte 7 bit 1
            public bool TravelCheck;       // byte 7 bit 2
            public bool DebugMode;         // byte 7 bit 3
            public int DebounceLevel;      // byte 7, bit 5..7
            public int RtSmart;            // byte 20
            public bool Charging;          // byte 32 bit 7
            public int BatteryPercent;     // byte 32, bit 0..6
            public int RollerType;         // byte 33
            public int WorkMode;           // byte 34
            public int SleepTime;          // byte 35
            public int SleepMode;          // byte 36
            public int RfBtChannel;        // byte 37
            public int ReportRate24G;      // byte 38
            public byte[] Raw;
        }

        public static GlwFuncConfig ParseFuncConfig(byte[] d)
        {
            var c = new GlwFuncConfig { Raw = d };
            if (d == null || d.Length < 39) return c;
            c.MacMode = (d[1] & 0x0F) != 0;
            c.ReportRateIndex = d[4] & 0x0F;
            c.TickRate = (d[4] >> 4) & 0x0F;
            c.GlobalDeadZone = d[5];
            c.LockWin = (d[6] & 1) != 0;
            c.LockAltTap = (d[6] & 2) != 0;
            c.LockAltF4 = (d[6] & 4) != 0;
            c.TachyonMode = (d[7] & 1) != 0;
            c.BottomRapidTrigger = (d[7] & 2) != 0;
            c.TravelCheck = (d[7] & 4) != 0;
            c.DebugMode = (d[7] & 8) != 0;
            c.DebounceLevel = (d[7] >> 5) & 7;
            c.RtSmart = d[20];
            c.Charging = (d[32] & 0x80) != 0;
            c.BatteryPercent = d[32] & 0x7F;
            c.RollerType = d[33];
            c.WorkMode = d[34];
            c.SleepTime = d[35];
            c.SleepMode = d[36];
            c.RfBtChannel = d[37];
            c.ReportRate24G = d[38];
            return c;
        }

        // ---------------- Ban ghi trigger 8 byte/phim (cmd 160/161) --------
        // Trich dung ham dong goi Me / giai ma De cua SDK CZ.
        // Cac gia tri 9-bit (actuation / rtPress / rtRelease) luu tru -1;
        // vung chet 7-bit/phim. Don vi tho — anh xa sang mm nam o tang UI
        // cua hang va CHUA thu hoi duoc, khong trinh bay mm nhu su that.
        public sealed class GlwTrigger
        {
            public int SwitchType;      // 4 bit (nibble thap cua byte 0, OR 0xA0)
            public int Priority;        // nibble cao byte 1 khi != 0
            public int KeyMode;         // byte 1 (hoac nibble thap khi co priority)
            public int Actuation;       // 9 bit, luu -1
            public int PressPrecision;  // 2 bit (byte 3, bit 4..3)
            public int ReleasePrecision;// 2 bit (byte 3, bit 2..1)
            public int RtPress;         // 9 bit, luu -1
            public int PressDeadZone;   // 7 bit (byte 5, bit 7..1)
            public int RtRelease;       // 9 bit, luu -1
            public int ReleaseDeadZone; // 7 bit (byte 7, bit 7..1)

            public byte[] Encode()
            {
                var t = new byte[8];
                t[0] = (byte)(0xA0 | (SwitchType & 0x0F));
                t[1] = Priority != 0
                    ? (byte)(((Priority & 0x0F) << 4) | (KeyMode & 0x0F))
                    : (byte)KeyMode;
                int a = Math.Max(0, Actuation - 1);
                t[2] = (byte)(a & 0xFF);
                t[3] = (byte)(((a >> 8) & 1)
                    | ((ReleasePrecision & 3) << 1)
                    | ((PressPrecision & 3) << 3));
                int rp = Math.Max(0, RtPress - 1);
                t[4] = (byte)(rp & 0xFF);
                t[5] = (byte)(((rp >> 8) & 1) | ((PressDeadZone & 0x7F) << 1));
                int rr = Math.Max(0, RtRelease - 1);
                t[6] = (byte)(rr & 0xFF);
                t[7] = (byte)(((rr >> 8) & 1) | ((ReleaseDeadZone & 0x7F) << 1));
                return t;
            }

            public static GlwTrigger Decode(byte[] t, int off = 0)
            {
                var g = new GlwTrigger();
                g.SwitchType = t[off + 0] & 0x0F;
                int b1 = t[off + 1];
                if ((b1 >> 4) != 0) { g.Priority = (b1 >> 4) & 0x0F; g.KeyMode = b1 & 0x0F; }
                else { g.Priority = 0; g.KeyMode = b1; }
                g.Actuation = (t[off + 2] | ((t[off + 3] & 1) << 8)) + 1;
                g.ReleasePrecision = (t[off + 3] >> 1) & 3;
                g.PressPrecision = (t[off + 3] >> 3) & 3;
                g.RtPress = (t[off + 4] | ((t[off + 5] & 1) << 8)) + 1;
                g.PressDeadZone = (t[off + 5] >> 1) & 0x7F;
                g.RtRelease = (t[off + 6] | ((t[off + 7] & 1) << 8)) + 1;
                g.ReleaseDeadZone = (t[off + 7] >> 1) & 0x7F;
                return g;
            }
        }

        /// <summary>Vung trigger 1024 byte cua mot profile (offset 1024*profile).</summary>
        public static byte[] BuildTriggerRegion(GlwTrigger[] keys128)
        {
            if (keys128 == null || keys128.Length != MaxKeys)
                throw new ArgumentException("Can dung 128 ban ghi trigger");
            var region = new byte[TriggerRegionBytes];
            for (int i = 0; i < MaxKeys; i++)
                Array.Copy(keys128[i].Encode(), 0, region, i * TriggerKeyBytes, TriggerKeyBytes);
            return region;
        }

        public static int TriggerRegionOffset(int profile) => TriggerRegionBytes * profile;
        public static int FuncConfigOffset(int profile) => FuncConfigBytes * profile;
        public static int DksRegionOffset(int profile) => DksRegionBytes * profile;
        public static int MtRegionOffset(int profile) => MtRegionBytes * profile;
        public static int TglRegionOffset(int profile) => TglRegionBytes * profile;

        // ---------------- DKS: slot 24 byte (cmd 162/163) -------------------
        // [0..3] = 4 diem kich hoat (1 byte/diem). Moi hanh dong i chiem
        // 5 byte tai 4+5i: entity phim 3 byte {type, code1, code2} + mat na
        // 10-bit LE tai 2 byte tiep: packed = a0 | (a1&7)<<3 | (a2&7)<<6 |
        // (a3&1)<<9 theo dung cong thuc SDK; y nghia tung bit cua mat na
        // chua kiem chung tren phan cung nen giu dang tho (round-trip).
        public sealed class GlwDksAction
        {
            public byte Type, Code1, Code2;
            public ushort PointMask;
        }

        public sealed class GlwDksSlot
        {
            public byte[] Points = new byte[4];
            public GlwDksAction[] Actions = new GlwDksAction[4]
                { new GlwDksAction(), new GlwDksAction(), new GlwDksAction(), new GlwDksAction() };

            public static ushort PackPointMask(int a0, int a1, int a2, int a3)
                => (ushort)((a0 & 1) | ((a1 & 7) << 3) | ((a2 & 7) << 6) | ((a3 & 1) << 9));

            public byte[] Encode()
            {
                var t = new byte[24];
                Array.Copy(Points, 0, t, 0, 4);
                for (int i = 0; i < 4; i++)
                {
                    int a = 5 * i + 4;
                    t[a] = Actions[i].Type;
                    t[a + 1] = Actions[i].Code1;
                    t[a + 2] = Actions[i].Code2;
                    t[a + 3] = (byte)(Actions[i].PointMask & 0xFF);
                    t[a + 4] = (byte)((Actions[i].PointMask >> 8) & 0xFF);
                }
                return t;
            }

            public static GlwDksSlot Decode(byte[] t, int off = 0)
            {
                var s = new GlwDksSlot();
                Array.Copy(t, off, s.Points, 0, 4);
                for (int i = 0; i < 4; i++)
                {
                    int a = off + 5 * i + 4;
                    s.Actions[i].Type = t[a];
                    s.Actions[i].Code1 = t[a + 1];
                    s.Actions[i].Code2 = t[a + 2];
                    s.Actions[i].PointMask = (ushort)(t[a + 3] | (t[a + 4] << 8));
                }
                return s;
            }
        }

        // ---------------- MT: slot 8 byte (cmd 164/165) ---------------------
        // Parser cua hang chi doc 6 byte dau: entity clickKey tai [0..2],
        // entity downKey tai [3..5]; 2 byte cuoi chua ro — giu tho.
        public sealed class GlwMtSlot
        {
            public byte ClickType, ClickCode1, ClickCode2;
            public byte DownType, DownCode1, DownCode2;
            public byte Unknown6, Unknown7;

            public byte[] Encode() => new byte[]
            {
                ClickType, ClickCode1, ClickCode2,
                DownType, DownCode1, DownCode2,
                Unknown6, Unknown7
            };

            public static GlwMtSlot Decode(byte[] t, int off = 0) => new GlwMtSlot
            {
                ClickType = t[off], ClickCode1 = t[off + 1], ClickCode2 = t[off + 2],
                DownType = t[off + 3], DownCode1 = t[off + 4], DownCode2 = t[off + 5],
                Unknown6 = t[off + 6], Unknown7 = t[off + 7]
            };
        }

        // ---------------- TGL: vung 128 byte = 32 slot x 4 byte ------------
        // Parser cua hang doc entity 3 byte moi slot; byte thu 4 cua slot
        // chua ro vai tro (du tru/co) — giu tho khi dung vung.
        public static byte[] BuildTglRegion(byte[][] slotEntities32, byte[] reserved32 = null)
        {
            if (slotEntities32 == null || slotEntities32.Length != TglSlots)
                throw new ArgumentException("Can dung 32 entity TGL");
            var region = new byte[TglRegionBytes];
            for (int i = 0; i < TglSlots; i++)
            {
                if (slotEntities32[i] == null || slotEntities32[i].Length != 3)
                    throw new ArgumentException("Entity TGL phai dung 3 byte");
                Array.Copy(slotEntities32[i], 0, region, i * 4, 3);
                region[i * 4 + 3] = reserved32 != null ? reserved32[i] : (byte)0;
            }
            return region;
        }

        // ---------------- Lenh dac biet -------------------------------------
        /// <summary>Hieu chuan: 168 bat dau / 169 ket thuc / 177 khoi dong lai, payload [0].</summary>
        public static byte[] BuildCalibration(byte cmd)
            => BuildWrite(cmd, 0, new byte[] { 0 });

        /// <summary>Khoi phuc cai dat goc: payload [0xFF] = tat ca profile.</summary>
        public static byte[] BuildFactoryResetAll()
            => BuildWrite(CmdFactoryReset, 0, new byte[] { 0xFF });
    }

    // =====================================================================
    // CRC32 chuan IEEE (bang tra) — dung cho meta GIF cua stack QHW.
    // Gia tri kiem: CRC32("123456789") = 0xCBF43926.
    // =====================================================================
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[i] = c;
            }
            return t;
        }

        public static uint Compute(byte[] data)
        {
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in data)
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }

    // =====================================================================
    // Stack QHW (G87 V3 / G98 V3 / K87 V3): usagePage 0xFF80, usage 1,
    // reportId 77 (kenh nhac 0xFF81 / reportId 78), goi 31 byte
    // (che do boot 63 byte):
    //   [0]=version, [1]=flags, [2]=do dai payload, [3..4]=cmd LE16,
    //   [5]=bizCode, [6]=sequence, [7..]=payload, byte cuoi = checksum
    //   XOR cua moi byte tu byte 1 tro di (header+payload, tru version
    //   va tru chinh byte checksum).
    // Doc 0x02xx / ghi 0x03xx / dieu khien 0x09xx.
    // "GIF" cua QHW la HIEU UNG LED toan phim (khung delta), KHONG phai
    // man hinh — khong model MCHOSE nao co man hinh.
    // =====================================================================
    public static class QhwProtocol
    {
        public const byte ReportId = 77;
        public const int PacketLen = 31;        // che do thuong
        public const int BootPacketLen = 63;    // che do boot
        public const int MaxPayload = 23;       // 31 - 7 header - 1 checksum
        public const int BootMaxPayload = 55;
        public const int MaxGifContent = 36800;
        public const ushort GifEffectStart = 128;   // chi so hieu ung GIF

        // --- opcode 16-bit ---
        public const ushort CmdBroadcastEvent = 0x0022;
        public const ushort CmdGetLightEffectParams = 0x0200;
        public const ushort CmdGetKeyColors = 0x0201;
        public const ushort CmdGetKeyConfigs = 0x0202;
        public const ushort CmdGetProfileConfig = 0x0205;
        public const ushort CmdGetGifMeta = 0x0206;
        public const ushort CmdGetStillEffect = 0x0207;
        public const ushort CmdSetWorkMode = 0x0300;
        public const ushort CmdSetSleepTime = 0x0301;
        public const ushort CmdSetLightEffectMode = 0x0302;
        public const ushort CmdSetLightEffectParams = 0x0303;
        public const ushort CmdSetKeyColors = 0x0304;        // luu tru
        public const ushort CmdSetKeyColorsRealtime = 0x0306; // thoi gian thuc
        public const ushort CmdSetKeyConfigs = 0x0308;
        public const ushort CmdSetProfileConfig = 0x030B;
        public const ushort CmdRestore = 0x030C;
        public const ushort CmdSetGif = 0x030D;
        public const ushort CmdSetStillEffect = 0x030E;
        public const ushort CmdSetWheelKeyMode = 0x030F;
        public const ushort CmdSetCalibrationRgb = 0x0310;
        public const ushort CmdGetBaseInfo = 0x0900;
        public const ushort CmdGetFwVersion = 0x0901;
        public const ushort CmdGetBattery = 0x0904;
        public const ushort CmdGetProfileName = 0x0905;
        public const ushort CmdSwitchProfile = 0x0906;
        public const ushort CmdGetMacroHeader = 0x090C;
        public const ushort CmdSetProfileName = 0x0910;
        public const ushort CmdHeartbeat = 0x0914;

        /// <summary>Dong goi QHW (khong gom reportId — tang transport them vao).</summary>
        public static byte[] Build(ushort cmd, byte[] payload, byte sequence, byte version = 0, byte bizCode = 0, bool bootMode = false)
        {
            if (payload == null) payload = Array.Empty<byte>();
            int total = bootMode ? BootPacketLen : PacketLen;
            int max = bootMode ? BootMaxPayload : MaxPayload;
            if (payload.Length > max) throw new ArgumentException("QHW payload vuot gioi han goi");
            var p = new byte[total];
            p[0] = version;
            p[1] = 0; // flags
            p[2] = (byte)payload.Length;
            p[3] = (byte)(cmd & 0xFF);
            p[4] = (byte)((cmd >> 8) & 0xFF);
            p[5] = bizCode;
            p[6] = sequence;
            Array.Copy(payload, 0, p, 7, payload.Length);
            byte x = 0;
            for (int i = 1; i < 7 + payload.Length; i++) x ^= p[i];
            p[7 + payload.Length] = x;
            return p;
        }

        /// <summary>Kiem tra checksum XOR cua mot goi QHW da nhan.</summary>
        public static bool VerifyChecksum(byte[] packet)
        {
            if (packet == null || packet.Length < 8) return false;
            int len = packet[2];
            if (7 + len >= packet.Length) return false;
            byte x = 0;
            for (int i = 1; i < 7 + len; i++) x ^= packet[i];
            return packet[7 + len] == x;
        }

        /// <summary>Payload cua goi QHW (bo header 7 byte va byte checksum).</summary>
        public static byte[] PayloadOf(byte[] packet)
        {
            int len = packet[2];
            var d = new byte[len];
            Array.Copy(packet, 7, d, 0, len);
            return d;
        }

        // ---------------- BaseInfo (0x0900, 20 byte) -------------------------
        public sealed class QhwBaseInfo
        {
            public int Vid, Pid;              // LE16 tai [0..1], [2..3]
            public int MaxProfileLength;      // [4]
            public bool MacroShared;          // [5] bit 0 (false = doc lap)
            public bool MacroHeaderHasName;   // [5] bit 4
            public int MacroSizeShared;       // [6..7] LE16
            public int MacroSizeIndependent;  // [8..9] LE16
            public int ConnectStatus;         // [10] 1=co day, 2=khong day
            public int ChargeStatus;          // [11]
            public int BatteryLevel;          // [12]
            public int GameSafeMode;          // [13]
            public int ActiveProfile;         // [14] 2 bit thap
            public int ProfileCount;          // [14] 3 bit ke tiep
            public int WheelKeyMode;          // [16]
            public byte CalibR, CalibG, CalibB; // [17..19]
            public byte[] Raw;
        }

        public static QhwBaseInfo ParseBaseInfo(byte[] d)
        {
            var b = new QhwBaseInfo { Raw = d };
            if (d == null || d.Length < 20) return b;
            b.Vid = d[0] | (d[1] << 8);
            b.Pid = d[2] | (d[3] << 8);
            b.MaxProfileLength = d[4];
            b.MacroShared = (d[5] & 1) == 0;
            b.MacroHeaderHasName = (d[5] & 0x10) != 0;
            b.MacroSizeShared = d[6] | (d[7] << 8);
            b.MacroSizeIndependent = d[8] | (d[9] << 8);
            b.ConnectStatus = d[10];
            b.ChargeStatus = d[11];
            b.BatteryLevel = d[12];
            b.GameSafeMode = d[13];
            b.ActiveProfile = d[14] & 3;
            b.ProfileCount = (d[14] >> 2) & 7;
            b.WheelKeyMode = d[16];
            b.CalibR = d[17]; b.CalibG = d[18]; b.CalibB = d[19];
            return b;
        }

        // ---------------- ProfileConfig (0x0205, 11 byte) --------------------
        public sealed class QhwProfileConfig
        {
            public int ProfileIndex;   // [0]
            public int Mode;           // [1] 2 bit thap
            public int SleepMode;      // [1] 2 bit tiep
            public int SleepTime;      // [2]
            public bool MacLayer;      // [3] == 1
            public int MainKeyRgbMode; // [4] <65 thuong, 65..127 tinh, >=128 GIF
            public int SideEffect;     // [5]
            public int Side2Effect;    // [6]
            public int MaxUserCustomEffect; // [7]
            public int WinLock;        // [8]
            public int EffectTypeValue;// [9]
            public int ReportRateIndex;// [10]
            public byte[] Raw;
        }

        public static QhwProfileConfig ParseProfileConfig(byte[] d)
        {
            var c = new QhwProfileConfig { Raw = d };
            if (d == null || d.Length < 11) return c;
            c.ProfileIndex = d[0];
            c.Mode = d[1] & 3;
            c.SleepMode = (d[1] >> 2) & 3;
            c.SleepTime = d[2];
            c.MacLayer = d[3] == 1;
            c.MainKeyRgbMode = d[4];
            c.SideEffect = d[5];
            c.Side2Effect = d[6];
            c.MaxUserCustomEffect = d[7];
            c.WinLock = d[8];
            c.EffectTypeValue = d[9];
            c.ReportRateIndex = d[10];
            return c;
        }

        // ---------------- Pin (0x0904): [chargeStatus, batteryLevel] ---------
        public static (int ChargeStatus, int BatteryLevel) ParseBattery(byte[] d)
        {
            if (d == null || d.Length < 2) return (0, 0);
            return (d[0], d[1]);
        }

        // ---------------- Mau tung phim (0x0201/0x0304/0x0306) ---------------
        /// <summary>4 byte/phim: [matrixRC, R, G, B]; matrixRC = (hang&lt;&lt;4)|cot.</summary>
        public static byte[] BuildKeyColors(IReadOnlyList<(byte MatrixRC, byte R, byte G, byte B)> colors)
        {
            var d = new byte[colors.Count * 4];
            for (int i = 0; i < colors.Count; i++)
            {
                d[i * 4] = colors[i].MatrixRC;
                d[i * 4 + 1] = colors[i].R;
                d[i * 4 + 2] = colors[i].G;
                d[i * 4 + 3] = colors[i].B;
            }
            return d;
        }

        // ---------------- Khoi phuc (0x030C) ----------------------------------
        /// <summary>Payload [0xFF x5] = khoi phuc tat ca (dung trong SDK).</summary>
        public static byte[] RestoreAllPayload() => new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        /// <summary>
        /// Khoi phuc theo pham vi: [profileMask, scopeMask x4]; scope bit:
        /// base=1, winLayer=2, winFnLayer=4, macLayer=8, macFnLayer=16,
        /// light=32, customData=64.
        /// </summary>
        public static byte[] RestoreScopePayload(byte profileMask, byte scope)
            => new byte[] { profileMask, scope, scope, scope, scope };

        // ---------------- "GIF" LED: khung delta + meta CRC32 -----------------
        /// <summary>
        /// Ma hoa noi dung "GIF" QHW: moi khung = [so phim thay doi (1B),
        /// thoi luong LE16 (mac dinh 30), (matrixRC, R, G, B) x so phim doi].
        /// Khung dau so voi nen den tuyet doi (moi phim deu = 0,0,0).
        /// Day la hieu ung LED toan phim — KHONG phai anh man hinh.
        /// </summary>
        public static byte[] EncodeGifFrames(IReadOnlyList<(int DurationMs, IReadOnlyList<(byte MatrixRC, byte R, byte G, byte B)> Keys)> frames)
        {
            var content = new List<byte>();
            var prev = new Dictionary<byte, (byte R, byte G, byte B)>();
            foreach (var (dur, keys) in frames)
            {
                var cur = new Dictionary<byte, (byte R, byte G, byte B)>(prev);
                foreach (var k in keys) cur[k.MatrixRC] = (k.R, k.G, k.B);
                var diff = new List<(byte MatrixRC, byte R, byte G, byte B)>();
                foreach (var kv in cur)
                {
                    // phim chua tung xuat hien = den (0,0,0) trong khung truoc
                    var old = prev.TryGetValue(kv.Key, out var o) ? o : ((byte)0, (byte)0, (byte)0);
                    if (old != kv.Value)
                        diff.Add((kv.Key, kv.Value.R, kv.Value.G, kv.Value.B));
                }
                if (diff.Count > 255) throw new ArgumentException("Mot khung GIF QHW doi toi da 255 phim");
                content.Add((byte)diff.Count);
                int d = dur <= 0 ? 30 : dur;
                content.Add((byte)(d & 0xFF));
                content.Add((byte)((d >> 8) & 0xFF));
                foreach (var k in diff)
                {
                    content.Add(k.MatrixRC); content.Add(k.R); content.Add(k.G); content.Add(k.B);
                }
                prev = cur;
            }
            if (content.Count > MaxGifContent)
                throw new ArgumentException("Noi dung GIF QHW vuot 36800 byte");
            return content.ToArray();
        }

        /// <summary>
        /// Meta GIF 7 byte (offset 0): [lenLo, lenHi, CRC32 x4 (dao nguoc
        /// thu tu byte — LE), co hoan tat]. Doc lai bang cmd 0x0206.
        /// </summary>
        public static byte[] BuildGifMeta(byte[] content, bool completed)
        {
            uint crc = Crc32.Compute(content);
            return new byte[]
            {
                (byte)(content.Length & 0xFF),
                (byte)((content.Length >> 8) & 0xFF),
                (byte)(crc & 0xFF),
                (byte)((crc >> 8) & 0xFF),
                (byte)((crc >> 16) & 0xFF),
                (byte)((crc >> 24) & 0xFF),
                (byte)(completed ? 1 : 0)
            };
        }

        /// <summary>Giai ma meta GIF doc tu 0x0206.</summary>
        public static (int Length, uint Crc, bool Completed) ParseGifMeta(byte[] meta)
        {
            if (meta == null || meta.Length < 7) return (0, 0, false);
            int len = meta[0] | (meta[1] << 8);
            uint crc = (uint)(meta[2] | (meta[3] << 8) | (meta[4] << 16) | (meta[5] << 24));
            return (len, crc, meta[6] != 0);
        }
    }

    // =====================================================================
    // Stack legacy (chuot + ban phim cu cua hang): feature report,
    // reportId 0x11 (goi 20 byte) / 0x12 (goi 64 byte) — so byte tinh ca
    // reportId. MOI byte du lieu (sau reportId) deu XOR 0xFF; byte dem
    // cung la 0xFF. Kiem tra phan hoi: (~reply[1]) & 0xFF phai bang byte
    // lenh thu hai; payload phan hoi = reply[2..] XOR 0xFF tung byte.
    //
    // BAN NAY: chi builder + giai ma (unit test). Transport THAT khong
    // mo — MchoseDevice chi ghi log goi (dry-run-only), nhan ro trong UI.
    // =====================================================================
    public static class MchoseLegacyProtocol
    {
        public const byte RidShort = 0x11;   // goi 20 byte
        public const byte RidLong = 0x12;    // goi 64 byte
        public const int ShortLen = 20;
        public const int LongLen = 64;

        // --- lenh da biet ---
        public const byte Cmd11ReceiverInfo = 0x03;   // bond, vid, pid, connect, game
        public const byte Cmd11FwVersion = 0x04;      // versionLength + ASCII
        public const byte Cmd11DeviceInfo = 0x06;     // vid/pid/fw/pin (xem parser)
        public const byte Cmd11MouseLight = 0x1B;
        public const byte Cmd12MacroWrite = 0x55;     // ghi macro (ban phim)
        public const byte Cmd12MacroNameWrite = 0x53; // ghi ten macro
        public const byte Cmd12Names = 0x63;          // doc ten phim/macro
        public const byte Cmd12MacroRead = 0x65;      // doc macro
        public const byte Cmd12MouseSettings = 0x67;  // toan bo cai dat chuot
        public const byte Cmd12Onboard = 0x68;        // slot onboard 00/01/02
        public const byte Cmd12MacroReadLong = 0x77;  // doc macro dai

        /// <summary>
        /// Goi logic day du (gom reportId tai [0], lenh tai [1], payload
        /// tu [2], dem 0). Do dai 20 (0x11) hoac 64 (0x12).
        /// </summary>
        public static byte[] BuildLogical(byte reportId, byte cmd, byte[] payload = null)
        {
            int total = reportId == RidShort ? ShortLen : LongLen;
            var p = new byte[total];
            p[0] = reportId;
            p[1] = cmd;
            if (payload != null)
            {
                if (payload.Length > total - 2) throw new ArgumentException("Legacy payload qua dai");
                Array.Copy(payload, 0, p, 2, payload.Length);
            }
            return p;
        }

        /// <summary>
        /// Dang day (wire) gui qua SetFeatureReport: cac byte SAU reportId,
        /// moi byte XOR 0xFF (byte dem 0 thanh 0xFF).
        /// </summary>
        public static byte[] ToWire(byte[] logical)
        {
            var w = new byte[logical.Length - 1];
            for (int i = 1; i < logical.Length; i++) w[i - 1] = (byte)(logical[i] ^ 0xFF);
            return w;
        }

        /// <summary>Kiem tra echo: (~reply[1]) &amp; 0xFF == byte lenh thu hai da gui.</summary>
        public static bool CheckEcho(byte[] fullReply, byte sentCmd)
        {
            if (fullReply == null || fullReply.Length < 2) return false;
            return ((~fullReply[1]) & 0xFF) == sentCmd;
        }

        /// <summary>Payload phan hoi da giai XOR (bo 2 byte dau: reportId + echo).</summary>
        public static byte[] DecodeReplyPayload(byte[] fullReply)
        {
            if (fullReply == null || fullReply.Length <= 2) return Array.Empty<byte>();
            var d = new byte[fullReply.Length - 2];
            for (int i = 2; i < fullReply.Length; i++) d[i - 2] = (byte)(fullReply[i] ^ 0xFF);
            return d;
        }

        // ---------------- 0x11 0x06: thong tin + pin -------------------------
        // Parser cua hang (binary-parser, little-endian, bit doc tu MSB):
        // vid u16, pid u16, fwVersion u32, 1 byte bit: connectMode(3) /
        // connectStatus(1) / reserved(4), batteryLevel u8, chargeStatus u8.
        public sealed class LegacyDeviceInfo
        {
            public int Vid, Pid;
            public uint FwVersion;
            public int ConnectMode, ConnectStatus;
            public int BatteryLevel, ChargeStatus;
        }

        public static LegacyDeviceInfo ParseDeviceInfo(byte[] payloadDeXored)
        {
            var v = new LegacyDeviceInfo();
            if (payloadDeXored == null || payloadDeXored.Length < 11) return v;
            v.Vid = payloadDeXored[0] | (payloadDeXored[1] << 8);
            v.Pid = payloadDeXored[2] | (payloadDeXored[3] << 8);
            v.FwVersion = (uint)(payloadDeXored[4] | (payloadDeXored[5] << 8)
                | (payloadDeXored[6] << 16) | (payloadDeXored[7] << 24));
            byte bits = payloadDeXored[8];
            v.ConnectMode = (bits >> 5) & 7;
            v.ConnectStatus = (bits >> 4) & 1;
            v.BatteryLevel = payloadDeXored[9];
            v.ChargeStatus = payloadDeXored[10];
            return v;
        }

        // ---------------- 0x11 0x04: chuoi phien ban firmware ------------------
        public static string ParseFwVersion(byte[] payloadDeXored)
        {
            if (payloadDeXored == null || payloadDeXored.Length < 1) return "";
            int n = Math.Min(payloadDeXored[0], payloadDeXored.Length - 1);
            return Encoding.ASCII.GetString(payloadDeXored, 1, n);
        }

        // ---------------- Macro (0x12 0x55 ghi / 0x53 ten) --------------------
        public const int MacroHeaderBytes = 58;   // phan dau su kien trong goi dau
        public const int MacroEventDataPerPacket = 52;

        /// <summary>
        /// Goi ghi macro: payload = [buttonIndex, moreData, offLo, offHi,
        /// length, ...du lieu su kien]. (Byte [0] cua goi logic la reportId
        /// 0x12, byte [1] la lenh 0x55 — xem BuildLogical.)
        /// </summary>
        public static byte[] BuildMacroPacket(int buttonIndex, bool moreData, int offset, byte[] eventData)
        {
            if (eventData == null) eventData = Array.Empty<byte>();
            if (eventData.Length > 255) throw new ArgumentException("Du lieu su kien macro > 255 byte/goi");
            var payload = new byte[5 + eventData.Length];
            payload[0] = (byte)buttonIndex;
            payload[1] = (byte)(moreData ? 1 : 0);
            payload[2] = (byte)(offset & 0xFF);
            payload[3] = (byte)((offset >> 8) & 0xFF);
            payload[4] = (byte)eventData.Length;
            Array.Copy(eventData, 0, payload, 5, eventData.Length);
            return BuildLogical(RidLong, Cmd12MacroWrite, payload);
        }

        // Su kien macro 4 byte (gia tri logic — tang day XOR 0xFF khi gui):
        public static byte[] MacroEventKeyDown(byte k0, byte k1) => new byte[] { 0, 129, k0, k1 };
        public static byte[] MacroEventKeyUp(byte k0, byte k1) => new byte[] { 0, 1, k0, k1 };
        public static byte[] MacroEventDelay(int ms) => new byte[] { 0, 15, (byte)(ms & 0xFF), (byte)((ms >> 8) & 0xFF) };
        public static byte[] MacroEventMouse(byte buttonMask, bool down) => new byte[] { 0, (byte)(down ? 136 : 8), buttonMask, 0 };
        public static byte[] MacroEventWheel(int dir) => new byte[] { 0, 5, (byte)dir, 0 };
    }
}
