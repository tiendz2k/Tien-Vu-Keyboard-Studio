using System;
using System.Collections.Generic;
using System.Linq;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Giao thuc kb_by_v2 (ho Sinowealth, VID 0x258A) — Driver Pack 1, v3.4.
    //
    // Nguon su that (tai lieu hoa tu ma nguon driver chinh hang, phan tich
    // tinh — CHUA kiem chung tren phan cung that):
    //  - Web driver YUNZII (hubx.pro), class kb_by_v2_wired / kb_by_v2_wireless:
    //    feature report ID 6, payload 519 byte, header 7 byte
    //    [cmd, param, 0, 1, 0, len_lo, len_hi], doc = ghi + 0x80.
    //  - App tham chieu (Family A): AULA F75 + GravaStar Mercury K1 dung
    //    chung kenh 519 byte / report 6 / usage page 0xFF00; F75 va K1 phan
    //    biet bang chu ky tra ve cua lenh 0x82.
    //
    // NGUYEN TAC AN TOAN cua ho nay:
    //  - VID:PID 258A:010C bi DUNG CHUNG boi nhieu board khong lien quan.
    //    Truoc khi cho phep BAT KY lenh ghi nao, app PHAI doc dinh danh tu
    //    chinh thiet bi (lenh 0x82) va doi chieu registry duoi day.
    //  - Dinh danh khong khop / khong doc duoc -> thiet bi chi duoc XEM.
    //
    // File nay chi la ham thuan (pure) — khong cham vao thiet bi.
    // =====================================================================
    public static class KbByV2Protocol
    {
        public const int VendorId = 0x258A;
        public const int PidShared = 0x010C;        // F75 / Hi75 / C75 / C98 ANSI / K1 (mot phan)
        public const int PidK1Alt = 0x0294;         // GravaStar Mercury K1 (rieng)
        public const int PidC98IsoDe = 0x02DE;
        public const int PidC98IsoFr = 0x02DF;
        public const int UsagePage = 0xFF00;
        public const byte ReportId = 6;
        public const int PayloadLength = 519;
        public const int HeaderLength = 7;

        // Ma lenh (byte[0] cua payload)
        public const byte CmdSetKeys = 0x03;
        public const byte CmdGetKeys = 0x83;
        public const byte CmdSetBasicInfo = 0x04;
        public const byte CmdGetBasicInfo = 0x84;
        public const byte CmdMacro = 0x05;
        public const byte CmdSetCustomLight = 0x06;
        public const byte CmdGetCustomLight = 0x86;
        public const byte CmdSetLightColor = 0x0A;
        public const byte CmdGetLightColor = 0x8A;
        public const byte CmdGetUuid = 0x82;
        public const byte CmdGetBattery = 0x87;
        public const byte CmdFactoryReset = 0x11;

        public const int BasicInfoLength = 128;
        public const int KeymapLength = 504;        // 126 o x 4 byte big-endian
        public const int CustomLightLength = 384;   // 3 mat phang R/G/B x 128 o
        public const int LightColorLength = 512;
        public const int MacroChunkLength = 512;
        public const int SetKeysSettleMs = 100;     // cho sau set_keys (tai lieu goc)

        // ------------------------------------------------------------------
        // Registry dinh danh: 6 byte tra ve cua lenh 0x82 (offset 7..12
        // trong payload dap tra, big-endian) -> model.
        //  - Yunzii: UUID 48-bit nguyen van tu registry cua hang.
        //  - AULA F75 / GravaStar K1: chu ky [3,0,0,0,x,code] — code 0xCD
        //    (205) = F75, 0xB4 (180) kem byte[4]=1 = K1 (app tham chieu).
        //  - Leobog Hi75 / Hi75C Pro: CHUA co UUID/chu ky trong tai lieu
        //    cong khai da trich -> Id = null -> khong the dinh danh duong
        //    tinh -> app chi cho XEM (view-only). Ghi ro, khong doan.
        // ------------------------------------------------------------------
        public sealed class IdentityEntry
        {
            public string ModelName;
            public byte[] Id;              // 6 byte; null = chua co tai lieu
            public string Method;          // mo ta cach dinh danh (hien UI)
            public int Pid;                // PID du kien khi cam day (de doi chieu)
            public IdentityEntry(string name, byte[] id, string method, int pid)
            { ModelName = name; Id = id; Method = method; Pid = pid; }
        }

        public static readonly List<IdentityEntry> Registry = new List<IdentityEntry>
        {
            new IdentityEntry("YUNZII C75 Cake Meow",
                new byte[] { 0x03, 0x00, 0x00, 0x00, 0x03, 0x77 },
                "UUID 0x82 = 0x030000000377 (registry YUNZII)", PidShared),
            new IdentityEntry("YUNZII C98 Cake Meow (ANSI)",
                new byte[] { 0x03, 0x00, 0x00, 0x00, 0x03, 0xFA },
                "UUID 0x82 = 0x0300000003FA (registry YUNZII)", PidShared),
            new IdentityEntry("YUNZII C98 Cake Meow ISO-DE",
                new byte[] { 0x03, 0x00, 0x00, 0x00, 0x05, 0x38 },
                "UUID 0x82 = 0x030000000538 (registry YUNZII)", PidC98IsoDe),
            new IdentityEntry("YUNZII C98 Cake Meow ISO-FR",
                new byte[] { 0x03, 0x00, 0x00, 0x00, 0x05, 0x39 },
                "UUID 0x82 = 0x030000000539 (registry YUNZII)", PidC98IsoFr),
            new IdentityEntry("AULA F75",
                new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00, 0xCD },
                "Chu ky 0x82 [3,0,0,0,0,205] (app tham chieu, Family A)", PidShared),
            new IdentityEntry("GravaStar Mercury K1",
                new byte[] { 0x03, 0x00, 0x00, 0x00, 0x01, 0xB4 },
                "Chu ky 0x82 [3,0,0,0,1,180] (app tham chieu, Family A)", PidShared),
            new IdentityEntry("Leobog Hi75", null,
                "Chua co UUID/chu ky trong tai lieu da trich — chi xem", PidShared),
            new IdentityEntry("Leobog Hi75C Pro", null,
                "Chua co UUID/chu ky trong tai lieu da trich — chi xem", PidShared),
        };

        /// <summary>Doi chieu 6 byte dinh danh voi registry. null = khong khop (la).</summary>
        public static IdentityEntry MatchIdentity(byte[] id6)
        {
            if (id6 == null || id6.Length != 6) return null;
            foreach (var e in Registry)
                if (e.Id != null && e.Id.SequenceEqual(id6)) return e;
            return null;
        }

        // ------------------------------------------------------------------
        // Khung goi co day: payload 519 byte, header 7 byte o dau.
        // ------------------------------------------------------------------
        public static byte[] BuildPacket(byte cmd, byte param, int declaredLength, byte[] data = null)
        {
            var p = new byte[PayloadLength];
            p[0] = cmd;
            p[1] = param;
            p[2] = 0x00;
            p[3] = 0x01;
            p[4] = 0x00;
            p[5] = (byte)(declaredLength & 0xFF);
            p[6] = (byte)((declaredLength >> 8) & 0xFF);
            if (data != null) Array.Copy(data, 0, p, HeaderLength, Math.Min(data.Length, PayloadLength - HeaderLength));
            return p;
        }

        /// <summary>ACK cua duong day = thiet bi echo lai nguyen 7 byte header.</summary>
        public static bool CheckHeaderEcho(byte[] request, byte[] reply)
        {
            if (request == null || reply == null) return false;
            if (request.Length < HeaderLength || reply.Length < HeaderLength) return false;
            for (int i = 0; i < HeaderLength; i++) if (request[i] != reply[i]) return false;
            return true;
        }

        // --- Dinh danh ---
        /// <summary>Probe chuan kb_by_v2: [0x82, 1, 0, 1, 0, 6, 0], doc UUID 48-bit.</summary>
        public static byte[] BuildUuidProbe() => BuildPacket(CmdGetUuid, 1, 6);

        /// <summary>Probe dinh danh F75/K1 cua app tham chieu: [0x82, 1, 0, 1, 0, 10, 0].</summary>
        public static byte[] BuildIdentityProbe() => BuildPacket(CmdGetUuid, 1, 10);

        /// <summary>Doc 6 byte dinh danh tai offset 7..12 cua payload dap tra.</summary>
        public static byte[] ParseIdentityBytes(byte[] replyPayload)
        {
            if (replyPayload == null || replyPayload.Length < HeaderLength + 6) return null;
            if (replyPayload[0] != CmdGetUuid) return null;
            var id = new byte[6];
            Array.Copy(replyPayload, HeaderLength, id, 0, 6);
            return id;
        }

        /// <summary>UUID 48-bit big-endian tu 6 byte dinh danh.</summary>
        public static ulong IdentityToUuid(byte[] id6)
        {
            if (id6 == null || id6.Length != 6) return 0;
            ulong v = 0;
            foreach (var b in id6) v = (v << 8) | b;
            return v;
        }

        // --- Basic info (128 byte) ---
        public static byte[] BuildGetBasicInfo() => BuildPacket(CmdGetBasicInfo, 0, BasicInfoLength);
        public static byte[] BuildSetBasicInfo(byte[] block128)
        {
            if (block128 == null || block128.Length != BasicInfoLength)
                throw new ArgumentException("Basic info phai dung 128 byte.");
            return BuildPacket(CmdSetBasicInfo, 0, BasicInfoLength, block128);
        }

        // --- Keymap (moi lop 504 byte = 126 o x 4 byte BE; trailer 5A A5 o 517/518) ---
        public static byte[] BuildGetKeys(byte layer) => BuildPacket(CmdGetKeys, layer, KeymapLength);
        public static byte[] BuildSetKeys(byte layer, byte[] keymap504)
        {
            if (keymap504 == null || keymap504.Length != KeymapLength)
                throw new ArgumentException("Keymap moi lop phai dung 504 byte.");
            var p = BuildPacket(CmdSetKeys, layer, KeymapLength, keymap504);
            p[517] = 0x5A;
            p[518] = 0xA5;
            return p;
        }

        /// <summary>Ma phim 32-bit big-endian tai o slot (0..125) trong khoi keymap 504 byte.</summary>
        public static void SetKeyCode(byte[] keymap504, int slot, uint code)
        {
            if (keymap504 == null || keymap504.Length != KeymapLength)
                throw new ArgumentException("Keymap moi lop phai dung 504 byte.");
            if (slot < 0 || slot >= 126) throw new ArgumentOutOfRangeException(nameof(slot));
            int o = slot * 4;
            keymap504[o] = (byte)(code >> 24);
            keymap504[o + 1] = (byte)(code >> 16);
            keymap504[o + 2] = (byte)(code >> 8);
            keymap504[o + 3] = (byte)code;
        }

        // --- Den tung phim (custom light): 384 byte = 3 mat phang R/G/B x 128 o ---
        public static byte[] BuildGetCustomLight() => BuildPacket(CmdGetCustomLight, 0, CustomLightLength);
        public static byte[] BuildSetCustomLight(byte[] planes384)
        {
            if (planes384 == null || planes384.Length != CustomLightLength)
                throw new ArgumentException("Custom light phai dung 384 byte.");
            return BuildPacket(CmdSetCustomLight, 0, CustomLightLength, planes384);
        }

        /// <summary>Dung 3 mat phang tu bang mau theo o LED (slot 0..127): R o [slot], G o [128+slot], B o [256+slot].</summary>
        public static byte[] BuildCustomLightPlanes(IReadOnlyList<(byte R, byte G, byte B)> slots128)
        {
            if (slots128 == null || slots128.Count != 128)
                throw new ArgumentException("Can dung 128 o LED.");
            var planes = new byte[CustomLightLength];
            for (int i = 0; i < 128; i++)
            {
                planes[i] = slots128[i].R;
                planes[128 + i] = slots128[i].G;
                planes[256 + i] = slots128[i].B;
            }
            return planes;
        }

        // --- Mau theo mode (light color): ghi 512 byte; doc khai len 640 (nguyen van tai lieu) ---
        public static byte[] BuildSetLightColor(byte[] block512)
        {
            if (block512 == null || block512.Length != LightColorLength)
                throw new ArgumentException("Light color phai dung 512 byte.");
            return BuildPacket(CmdSetLightColor, 0, LightColorLength, block512);
        }
        public static byte[] BuildGetLightColor() => BuildPacket(CmdGetLightColor, 0, 640);

        // --- Macro: cat blob thanh cac khoi 512 byte; header byte[3]=tong khoi, [4]=chi so khoi, [5..6]=len LE ---
        public static List<byte[]> BuildMacroChunks(byte[] blob)
        {
            if (blob == null) throw new ArgumentNullException(nameof(blob));
            int chunks = Math.Max(1, (blob.Length + MacroChunkLength - 1) / MacroChunkLength);
            if (chunks > 255) throw new ArgumentException("Blob macro qua lon (>255 khoi).");
            var list = new List<byte[]>();
            for (int i = 0; i < chunks; i++)
            {
                int len = Math.Min(MacroChunkLength, blob.Length - i * MacroChunkLength);
                var p = new byte[PayloadLength];
                p[0] = CmdMacro;
                p[1] = 0x00;
                p[2] = 0x00;
                p[3] = (byte)chunks;
                p[4] = (byte)i;
                p[5] = (byte)(len & 0xFF);
                p[6] = (byte)((len >> 8) & 0xFF);
                if (len > 0) Array.Copy(blob, i * MacroChunkLength, p, HeaderLength, len);
                list.Add(p);
            }
            return list;
        }

        // --- Pin + factory reset ---
        public static byte[] BuildGetBattery() => BuildPacket(CmdGetBattery, 0, 2);

        /// <summary>2 byte pin nam ngay sau header trong dap tra (offset 7, 8).</summary>
        public static (byte B0, byte B1) ParseBattery(byte[] replyPayload)
        {
            if (replyPayload == null || replyPayload.Length < HeaderLength + 2)
                throw new ArgumentException("Dap tra pin khong hop le.");
            return (replyPayload[HeaderLength], replyPayload[HeaderLength + 1]);
        }

        public static byte[] BuildFactoryReset(byte param = 0) => BuildPacket(CmdFactoryReset, param, 1);
    }

    // =====================================================================
    // Duong 2.4G qua dongle (kb_by_v2_wireless): goi 19 byte, report ID 19,
    // checksum byte cuoi = (19 + tong byte[0..17]) % 256.
    // Ghi khoi lon bang send_batch: manh 14 byte, moi manh cho ACK rieng.
    // (Ban v3.4 chi co builder + test; transport dongle that chi o che do
    //  dry-run/log — xem KbByV2Device va BUILD-INFO.)
    // =====================================================================
    public static class KbByV2Wireless
    {
        public const int DongleVendorId = 0x3554;
        public const int DongleProductId = 0xFA09;
        public const byte ReportId = 19;
        public const int PacketLength = 19;
        public const int FragmentLength = 14;

        public static byte ComputeChecksum(byte[] packet19)
        {
            if (packet19 == null || packet19.Length != PacketLength)
                throw new ArgumentException("Goi 2.4G phai dung 19 byte.");
            int sum = ReportId;
            for (int i = 0; i < PacketLength - 1; i++) sum += packet19[i];
            return (byte)(sum % 256);
        }

        /// <summary>Dien checksum vao byte[18] va tra lai goi.</summary>
        public static byte[] Seal(byte[] packet19)
        {
            packet19[PacketLength - 1] = ComputeChecksum(packet19);
            return packet19;
        }

        private static byte[] Cmd(byte cmd, byte b1, byte b2, byte b3)
        {
            var p = new byte[PacketLength];
            p[0] = cmd; p[1] = b1; p[2] = b2; p[3] = b3;
            return Seal(p);
        }

        public static byte[] BuildGetUuid() => Cmd(0x05, 1, 0, 0);
        public static byte[] BuildFactoryReset() => Cmd(0x06, 1, 0, 0);
        public static byte[] BuildGetKeys(byte layer) => Cmd(0x41, 1, 0, layer);
        public static byte[] BuildGetCustomLight() => Cmd(0x42, 1, 0, 0);
        public static byte[] BuildGetBasicInfo() => Cmd(0x44, 1, 0, 16);
        public static byte[] BuildGetLightColor() => Cmd(0x49, 1, 0, 16);
        public static byte[] BuildGetBattery() => Cmd(0x4A, 0, 0, 0);

        /// <summary>
        /// send_batch: cat data thanh manh 14 byte. Moi goi:
        /// [0]=loai du lieu, [1]=tong manh, [2]=thu tu manh,
        /// [3]=do dai manh + (layer &lt;&lt; 4), [4..17]=du lieu, [18]=checksum.
        /// </summary>
        public static List<byte[]> BuildBatch(byte dataType, byte layer, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int total = Math.Max(1, (data.Length + FragmentLength - 1) / FragmentLength);
            if (total > 255) throw new ArgumentException("Du lieu qua lon (>255 manh).");
            var list = new List<byte[]>();
            for (int i = 0; i < total; i++)
            {
                int len = Math.Min(FragmentLength, data.Length - i * FragmentLength);
                var p = new byte[PacketLength];
                p[0] = dataType;
                p[1] = (byte)total;
                p[2] = (byte)i;
                p[3] = (byte)(len + ((layer & 0x0F) << 4));
                if (len > 0) Array.Copy(data, i * FragmentLength, p, 4, len);
                list.Add(Seal(p));
            }
            return list;
        }
    }
}
