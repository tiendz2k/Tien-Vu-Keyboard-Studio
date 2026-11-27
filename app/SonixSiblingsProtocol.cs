using System;
using System.Collections.Generic;
using System.Linq;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Driver Pack 4 (v3.7) — cac ho "anh em SONiX" trich tu ung dung tham
    // chieu da giai phau (docs/PROTOCOLS-REFERENCE-APP.md). File nay THUAN
    // LOGIC (khong HID/UI) de unit test duoc:
    //  - Ho B (SonixBProtocol): S75 Pro + Nova75 — cung nen tang F75 Max
    //    (0C45:800A, usagePage 0xFF13, feature report 64 byte).
    //  - Ho C (Mad60Protocol): MADLIONS MAD60 HE (V2/thuong/RGB) —
    //    373B:xxxx, usagePage 0xFF60, output report 32 byte.
    //  - Ho D (Hero68Protocol): AULA HERO68 HE — 372E:103E, reportId 9,
    //    goi 63 byte co checksum o byte 62.
    //  - Ho E (Win60HeProtocol): AULA WIN60 HE — 2E3C:C365, usagePage
    //    0xFF33, output reportId 1, goi 63 byte, khung luoi 132 o.
    //  - Ho F2 (Win60SeProtocol): AULA WIN60 SE — 38A6:291A, byte phien
    //    ngau nhien; CHI builder + log (hang tu danh dau "thu nghiem").
    // Moi so lieu deu lay tu ho so giai phau (bang chung [JS]); chua cai
    // nao duoc kiem chung tren phan cung that.
    // =====================================================================

    // ------------------------- Ho B: SONiX 64B feature -------------------------
    public static class SonixBProtocol
    {
        public const int VendorId = 0x0C45;
        public const int ProductId = 0x800A;
        public const int UsagePage = 0xFF13;
        public const int PacketLen = 64;

        public const byte OpEnd = 0x02;    // commit/END
        public const byte OpStream = 0x20; // bat dau frame stream
        public const byte OpProbe = 0xF5;  // doc trang thai per-key (9 khoi)

        public const int StateBlockCount = 9;
        public const int FrameBlockCount = 8;
        public const int FrameBytes = 512; // 128 quad [id, R, G, B]
        public const int PacerMs = 2;      // pacer truoc moi khoi frame
        public const int EndRetryDelayMs = 35;
        public const int EndRetries = 2;

        // Thang cho ACK toan 0 cua lenh 0x20 (ho so S75 Pro): tong thoi
        // gian doi toi da truoc khi gui 8 khoi frame.
        public static readonly int[] ZeroAckPollLadderMs = { 20, 40, 80, 160, 200, 200, 300 };

        /// <summary>Dung lenh 64 byte: [0]=0x04, [1]=opcode, byte[8] = 9 neu
        /// opcode 0xF5, 8 neu 0x20, con lai 0 (builder oy() trong ho so).</summary>
        public static byte[] BuildCommand(byte opcode)
        {
            var p = new byte[PacketLen];
            p[0] = 0x04;
            p[1] = opcode;
            p[8] = opcode == OpProbe ? (byte)9 : opcode == OpStream ? (byte)8 : (byte)0;
            return p;
        }

        public enum Ack { Invalid, Ready, Busy }

        /// <summary>Kiem tra ACK (validator h5): reply[1] phai bang opcode;
        /// reply[3]==1 -> Ready, ==0xFF -> Busy, con lai Invalid.</summary>
        public static Ack CheckAck(byte[] reply, byte opcode)
        {
            if (reply == null || reply.Length < 4) return Ack.Invalid;
            if (reply[1] != opcode) return Ack.Invalid;
            if (reply[3] == 0x01) return Ack.Ready;
            if (reply[3] == 0xFF) return Ack.Busy;
            return Ack.Invalid;
        }

        /// <summary>Giai 9 khoi trang thai doc bang 0xF5 -> id -> (R,G,B).
        /// Moi khoi 64 byte = 16 quad [id, R, G, B]; bo quad id == 0.</summary>
        public static Dictionary<byte, (byte R, byte G, byte B)> ParseStateBlocks(IEnumerable<byte[]> blocks)
        {
            var map = new Dictionary<byte, (byte, byte, byte)>();
            foreach (var b in blocks)
            {
                if (b == null) continue;
                for (int i = 0; i + 3 < b.Length; i += 4)
                {
                    byte id = b[i];
                    if (id == 0) continue;
                    map[id] = (b[i + 1], b[i + 2], b[i + 3]);
                }
            }
            return map;
        }

        /// <summary>Dung frame 512 byte: quad tai slot*4 = [id, R, G, B];
        /// mau tra theo id (thieu -> den). Khung phang nay la thu Nova75
        /// gui bang mot loi goi native duy nhat trong app goc.</summary>
        public static byte[] BuildFrame512(IReadOnlyList<byte> slotIds,
            IReadOnlyDictionary<byte, (byte R, byte G, byte B)> colors)
        {
            var frame = new byte[FrameBytes];
            int n = Math.Min(slotIds.Count, FrameBytes / 4);
            for (int slot = 0; slot < n; slot++)
            {
                byte id = slotIds[slot];
                frame[slot * 4] = id;
                if (colors != null && colors.TryGetValue(id, out var c))
                {
                    frame[slot * 4 + 1] = c.R;
                    frame[slot * 4 + 2] = c.G;
                    frame[slot * 4 + 3] = c.B;
                }
            }
            return frame;
        }

        /// <summary>Chia frame 512 byte thanh 8 khoi 64 byte (duong gui cua S75 Pro).</summary>
        public static List<byte[]> BuildStreamBlocks(IReadOnlyList<byte> slotIds,
            IReadOnlyDictionary<byte, (byte R, byte G, byte B)> colors)
        {
            var frame = BuildFrame512(slotIds, colors);
            var blocks = new List<byte[]>(FrameBlockCount);
            for (int i = 0; i < FrameBlockCount; i++)
            {
                var b = new byte[PacketLen];
                Array.Copy(frame, i * PacketLen, b, 0, PacketLen);
                blocks.Add(b);
            }
            return blocks;
        }

        // Bang phim S75 Pro (zl trong ho so): 80 id theo thu tu slot,
        // trich tu layout chinh thuc aula-s75-pro-layout-900x341.json.
        public static readonly byte[] S75ProKeyIds =
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 119,
            19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 103, 117,
            37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 67, 120,
            55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 85,
            73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 101,
            91, 92, 93, 94, 95, 96, 98, 99, 100, 102,
        };

        // Nova75 co bang phim rieng (Oo) trong app goc nhung chua thu hoi
        // duoc day du tu ma nguon da giai phau — cong them viec trinh ghi
        // frame native (hid_write_nova_frame) khong ro cach chia goi, nen
        // Nova75 chi o muc builder + log (xem SonixSiblingsDevice).
    }

    // ------------------------- Ho C: MAD60 -------------------------
    public static class Mad60Protocol
    {
        public const int VendorId = 0x373B;
        public const int PidV2 = 0x1120;
        public const int PidOriginal = 0x105D;
        public const int PidRgb = 0x1054;
        public const int UsagePage = 0xFF60;
        public const int Usage = 0x61;
        public const int PacketLen = 32;
        public const int LedSlots = 70;
        public const int ColorBytes = 210; // 70 slot x 3
        public const int SupportedProtocolVersion = 9;

        public static byte[] BuildPacket(params byte[] head)
        {
            var p = new byte[PacketLen];
            Array.Copy(head, 0, p, 0, Math.Min(head.Length, PacketLen));
            return p;
        }

        /// <summary>Probe phien ban giao thuc firmware: goi [1].</summary>
        public static byte[] BuildVersionProbe() => BuildPacket(1);

        /// <summary>Doc thong tin den: goi [8, 65].</summary>
        public static byte[] BuildGetLightingInfo() => BuildPacket(8, 65);

        /// <summary>Do dai tien to echo ma dap tra phai khop: lenh [1] -> 1
        /// byte; [8,65] -> 2 byte; con lai 5 byte.</summary>
        public static int EchoPrefixLength(byte[] sent)
        {
            if (sent == null || sent.Length == 0) return 0;
            if (sent[0] == 1 && (sent.Length < 2 || sent[1] == 0)) return 1;
            if (sent[0] == 8 && sent.Length >= 2 && sent[1] == 65) return 2;
            return 5;
        }

        public static bool MatchEcho(byte[] sent, byte[] reply)
        {
            if (sent == null || reply == null) return false;
            int n = EchoPrefixLength(sent);
            if (reply.Length < n) return false;
            for (int i = 0; i < n; i++) if (reply[i] != sent[i]) return false;
            return true;
        }

        /// <summary>Dap tra probe hop le: d[1]==0, d[2]==9 (fw protocol v9 —
        /// app goc bao loi "chi ho tro giao thuc 9" neu khac).</summary>
        public static bool CheckVersionReply(byte[] reply)
            => reply != null && reply.Length >= 3 && reply[1] == 0 && reply[2] == SupportedProtocolVersion;

        /// <summary>Header doc mau goc: [8, 69, page, half*7, 7] — 10 goi
        /// (5 page x 2 nua), moi dap tra mang 21 byte du lieu tai offset 5.
        /// (Truong byte[3] doc tu ho so la "half×7": offset LED trong page.)</summary>
        public static byte[] BuildReadColorsHeader(int page, int half)
            => BuildPacket(8, 69, (byte)page, (byte)(half * 7), 7);

        public static byte[] ParseColorsReply(byte[] reply)
        {
            var data = new byte[21];
            if (reply != null)
                Array.Copy(reply, 5, data, 0, Math.Min(21, Math.Max(0, reply.Length - 5)));
            return data;
        }

        /// <summary>Ghep 10 dap tra (5 page x 2 nua) thanh buffer mau goc 210 byte.</summary>
        public static byte[] AssembleColors(IEnumerable<byte[]> replies21)
        {
            var buf = new byte[ColorBytes];
            int i = 0;
            foreach (var r in replies21)
            {
                if (i >= 10) break;
                Array.Copy(r, 0, buf, i * 21, Math.Min(21, r.Length));
                i++;
            }
            return buf;
        }

        /// <summary>Vao che do custom/stream: ban sao goi info voi [0]=7,
        /// [2]=1, [8]=200. Thoat: gui lai goi info goc (enter=false tra
        /// ban sao nguyen trang de ghi lai).</summary>
        public static byte[] BuildEnterCustom(byte[] infoPacket, bool enter)
        {
            var p = new byte[PacketLen];
            if (infoPacket != null) Array.Copy(infoPacket, 0, p, 0, Math.Min(PacketLen, infoPacket.Length));
            if (enter)
            {
                p[0] = 7;
                p[2] = 1;
                p[8] = 200;
            }
            return p;
        }

        /// <summary>10 goi frame: [7, 66, page, offset, 7] + 21 byte du lieu
        /// (7 LED x 3) tai offset 5. Mau vao: 210 byte theo thu tu slot.</summary>
        public static List<byte[]> BuildFramePackets(byte[] colors210)
        {
            var list = new List<byte[]>(10);
            for (int k = 0; k < 10; k++)
            {
                var p = new byte[PacketLen];
                p[0] = 7; p[1] = 66;
                p[2] = (byte)(k / 2);
                p[3] = (byte)((k % 2) * 21);
                p[4] = 7;
                if (colors210 != null)
                    Array.Copy(colors210, k * 21, p, 5, Math.Min(21, Math.Max(0, colors210.Length - k * 21)));
                list.Add(p);
            }
            return list;
        }
    }

    // ------------------------- Ho D: HERO68 HE -------------------------
    public static class Hero68Protocol
    {
        public const int VendorId = 0x372E;
        public const int ProductId = 0x103E;
        public const int UsagePage = 0xFF60;
        public const int Usage = 0x61;
        public const int ReportId = 9;
        public const int PacketLen = 63;
        public const int ChecksumIndex = 62;
        public const int KeyCount = 68;
        public const int MinFrameIntervalMs = 34;
        public const int MaxIdsPerFramePacket = 52;

        // Dinh danh ky vong trong dap tra cmd 130: payload tai [6..11].
        public static readonly byte[] IdentityExpected = { 17, 0, 0, 0, 0, 3 };

        /// <summary>Builder Nn(): [0]=cmd, [1]=sub, [2]=0, [3]=total,
        /// [4]=index, [5]=do dai payload, payload tai byte 6, checksum tai
        /// byte 62 sao cho (9 + tong 63 byte) &amp; 0xFF == 0xFF (9 = reportId).</summary>
        public static byte[] BuildPacket(byte cmd, byte sub, byte[] payload, byte total = 1, byte index = 0)
        {
            var p = new byte[PacketLen];
            p[0] = cmd; p[1] = sub; p[2] = 0; p[3] = total; p[4] = index;
            int len = payload == null ? 0 : Math.Min(payload.Length, PacketLen - 6 - 1);
            p[5] = (byte)len;
            if (len > 0) Array.Copy(payload, 0, p, 6, len);
            p[ChecksumIndex] = ComputeChecksum(p);
            return p;
        }

        /// <summary>Byte checksum can dien vao byte 62 (cac byte con lai da co).</summary>
        public static byte ComputeChecksum(byte[] packet)
        {
            int sum = ReportId;
            for (int i = 0; i < PacketLen; i++)
            {
                if (i == ChecksumIndex) continue;
                sum += packet[i];
            }
            return (byte)((0xFF - (sum & 0xFF)) & 0xFF);
        }

        /// <summary>Kiem tra quy tac checksum tren mot goi day du.</summary>
        public static bool VerifyChecksum(byte[] packet)
        {
            if (packet == null || packet.Length < PacketLen) return false;
            int sum = ReportId;
            for (int i = 0; i < PacketLen; i++) sum += packet[i];
            return (sum & 0xFF) == 0xFF;
        }

        /// <summary>Validator dap tra cua ho so: checksum dung + [2]==0,
        /// [3]==1, [4]==0, [5]&lt;=56.</summary>
        public static bool ValidateReply(byte[] reply)
        {
            if (reply == null || reply.Length < PacketLen) return false;
            return VerifyChecksum(reply) && reply[2] == 0 && reply[3] == 1 && reply[4] == 0 && reply[5] <= 56;
        }

        public static byte[] BuildIdentity() => BuildPacket(130, 1, new byte[6]);

        /// <summary>Dap tra dinh danh hop le: [5]==6 va [6..11] == [17,0,0,0,0,3].</summary>
        public static bool CheckIdentityReply(byte[] reply)
        {
            if (reply == null || reply.Length < 12) return false;
            if (reply[5] != 6) return false;
            for (int i = 0; i < IdentityExpected.Length; i++)
                if (reply[6 + i] != IdentityExpected[i]) return false;
            return true;
        }

        public static byte[] BuildReadLighting() => BuildPacket(132, 1, null);

        /// <summary>Trang thai den: [5]==7, 7 byte trang thai tai [6..12].</summary>
        public static byte[] ParseLightingState(byte[] reply)
        {
            if (reply == null || reply.Length < 13 || reply[5] != 7) return null;
            var s = new byte[7];
            Array.Copy(reply, 6, s, 0, 7);
            return s;
        }

        /// <summary>Cac goi doc mau tung phim: moi goi Nn(134, 0, ...) mang
        /// 11 cap [0, id]; tong 7 goi cho 68 phim.</summary>
        public static List<byte[]> BuildReadColorPackets(IReadOnlyList<byte> keyIds)
        {
            var list = new List<byte[]>();
            int total = (keyIds.Count + 10) / 11;
            for (int g = 0; g < total; g++)
            {
                var payload = new List<byte>();
                for (int i = g * 11; i < Math.Min(keyIds.Count, g * 11 + 11); i++)
                {
                    payload.Add(0);
                    payload.Add(keyIds[i]);
                }
                list.Add(BuildPacket(134, 0, payload.ToArray(), (byte)total, (byte)g));
            }
            return list;
        }

        /// <summary>Giai dap tra doc mau: cac nhom 5 byte [0, id, R, G, B] tu offset 6.</summary>
        public static Dictionary<byte, (byte R, byte G, byte B)> ParseColorReply(byte[] reply)
        {
            var map = new Dictionary<byte, (byte, byte, byte)>();
            if (reply == null) return map;
            int payloadLen = reply.Length > 5 ? reply[5] : 0;
            for (int off = 6; off + 4 < reply.Length && off + 4 < 6 + payloadLen + 1; off += 5)
            {
                byte id = reply[off + 1];
                map[id] = (reply[off + 2], reply[off + 3], reply[off + 4]);
            }
            return map;
        }

        public static byte[] BuildEnterStream1() => BuildPacket(1, 128, new byte[] { 16 });
        public static byte[] BuildEnterStream2() => BuildPacket(4, 2, new byte[] { 19, 0 });
        public static byte[] BuildEnterStream3() => BuildPacket(4, 4, new byte[] { 100, 4 });
        public static byte[] BuildFrameTerminator() => BuildPacket(1, 0, null);

        /// <summary>Dung cac goi frame: gom cac phim trung mau RGB thanh
        /// goi Nn(8, 1, [R, G, B, so id, ids...]) toi da 52 id/goi.
        /// Tra ve danh sach goi CHUA gom goi ket thuc (1, 0).</summary>
        public static List<byte[]> BuildFramePackets(IReadOnlyList<(byte Id, byte R, byte G, byte B)> keys)
        {
            var groups = new Dictionary<(byte, byte, byte), List<byte>>();
            var order = new List<(byte, byte, byte)>();
            foreach (var k in keys)
            {
                var key = (k.R, k.G, k.B);
                if (!groups.TryGetValue(key, out var ids))
                {
                    ids = new List<byte>();
                    groups[key] = ids;
                    order.Add(key);
                }
                ids.Add(k.Id);
            }
            var raw = new List<(byte R, byte G, byte B, List<byte> Ids)>();
            foreach (var g in order)
                for (int i = 0; i < groups[g].Count; i += MaxIdsPerFramePacket)
                    raw.Add((g.Item1, g.Item2, g.Item3,
                        groups[g].Skip(i).Take(MaxIdsPerFramePacket).ToList()));
            var list = new List<byte[]>();
            for (int i = 0; i < raw.Count; i++)
            {
                var payload = new List<byte> { raw[i].R, raw[i].G, raw[i].B, (byte)raw[i].Ids.Count };
                payload.AddRange(raw[i].Ids);
                list.Add(BuildPacket(8, 1, payload.ToArray(), (byte)raw.Count, (byte)i));
            }
            return list;
        }

        /// <summary>Cac goi khoi phuc mau goc khi dung stream: 7 goi
        /// Nn(6, 0, [0, id, R, G, B] x 11), sau do Nn(4, 1, trang thai den
        /// goc) va Nn(1, 0).</summary>
        public static List<byte[]> BuildRestorePackets(
            IReadOnlyList<(byte Id, byte R, byte G, byte B)> keys, byte[] lightingState)
        {
            var list = new List<byte[]>();
            int total = (keys.Count + 10) / 11;
            for (int g = 0; g < total; g++)
            {
                var payload = new List<byte>();
                for (int i = g * 11; i < Math.Min(keys.Count, g * 11 + 11); i++)
                {
                    payload.Add(0);
                    payload.Add(keys[i].Id);
                    payload.Add(keys[i].R);
                    payload.Add(keys[i].G);
                    payload.Add(keys[i].B);
                }
                list.Add(BuildPacket(6, 0, payload.ToArray(), (byte)total, (byte)g));
            }
            list.Add(BuildPacket(4, 1, lightingState));
            list.Add(BuildPacket(1, 0, null));
            return list;
        }

        // Bang phim HERO68 (va trong ho so): 68 id theo thu tu slot, trich
        // tu hero68-he-layout-900x281.json.
        public static readonly byte[] KeyIds =
        {
            1, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 98,
            28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 99,
            42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 102,
            55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 74, 103,
            67, 68, 69, 70, 71, 72, 73, 76, 75, 77,
        };
    }

    // ------------------------- Ho E: WIN60 HE -------------------------
    public static class Win60HeProtocol
    {
        public const int VendorId = 0x2E3C;
        public const int ProductId = 0xC365;
        public const int UsagePage = 0xFF33;
        public const int Usage = 0x91;
        public const int ReportId = 1;
        public const int PacketLen = 63;
        public const int GridSlots = 132;      // 6 hang x 22 cot
        public const int FrameBytes = 396;     // 132 x 3
        public const int ChunkData = 54;
        public const int ChunkCount = 8;
        public const int PacerMs = 2;
        public const int HeartbeatMs = 1000;

        public static byte[] BuildReadLighting()
        {
            var p = new byte[PacketLen];
            p[0] = 7; p[1] = 1;
            return p;
        }

        /// <summary>Dap tra doc den hop le: [0]==7 va [4] &gt;= 12; 12 byte
        /// trang thai nam tai offset 5 (ngu nghia tung byte chua giai ma
        /// trong app goc — giu nguyen trang).</summary>
        public static bool CheckLightingReply(byte[] reply)
            => reply != null && reply.Length >= 17 && reply[0] == 7 && reply[4] >= 12;

        public static byte[] ParseLightingState(byte[] reply)
        {
            if (!CheckLightingReply(reply)) return null;
            var s = new byte[12];
            Array.Copy(reply, 5, s, 0, 12);
            return s;
        }

        /// <summary>Goi bat/tat stream (lenh 23): [0]=23, [4]=2, [5]=[6]=bool.</summary>
        public static byte[] BuildStreamEnable(bool on)
        {
            var p = new byte[PacketLen];
            p[0] = 23; p[4] = 2;
            p[5] = on ? (byte)1 : (byte)0;
            p[6] = p[5];
            return p;
        }

        public static byte[] BuildHeartbeat()
        {
            var p = new byte[PacketLen];
            p[0] = 1;
            return p;
        }

        /// <summary>Thiet bi yeu cau dong bo lai khi: goi vao [0]==1,
        /// [4] &gt;= 17 va [21]==0.</summary>
        public static bool CheckNeedsSync(byte[] inputReport)
            => inputReport != null && inputReport.Length >= 22
               && inputReport[0] == 1 && inputReport[4] >= 17 && inputReport[21] == 0;

        /// <summary>8 goi frame (lenh 15): header [0]=15, [2..3]=chi so
        /// manh BIG-ENDIAN, [4]=do dai manh, du lieu tai offset 5.</summary>
        public static List<byte[]> BuildFramePackets(byte[] rgb396)
        {
            var list = new List<byte[]>(ChunkCount);
            for (int i = 0; i < ChunkCount; i++)
            {
                int len = Math.Min(ChunkData, Math.Max(0, FrameBytes - i * ChunkData));
                var p = new byte[PacketLen];
                p[0] = 15;
                p[2] = (byte)(i >> 8);
                p[3] = (byte)(i & 0xFF);
                p[4] = (byte)len;
                if (rgb396 != null && len > 0)
                    Array.Copy(rgb396, i * ChunkData, p, 5, Math.Min(len, Math.Max(0, rgb396.Length - i * ChunkData)));
                list.Add(p);
            }
            return list;
        }
    }

    // ------------------------- Ho F2: WIN60 SE (chi builder + log) -------------------------
    public static class Win60SeProtocol
    {
        public const int VendorId = 0x38A6;
        public const int ProductId = 0x291A;
        public const int UsagePage = 0xFF00;
        public const int ReportId = 0;
        public const int PacketLen = 64;
        public const int StreamEntries = 65; // 61 phim + 4 LED phu cua spacebar
        public const int FramePacerMs = 34;

        // Dap tra exchange(1,1) phai co bytes [3..10] bang hang so nay va [43]==1.
        public static readonly byte[] InfoSignature = { 2, 4, 1, 4, 64, 0, 0, 0 };

        /// <summary>Byte phien ngau nhien 1..254, chon mot lan khi ket noi.</summary>
        public static byte NewSessionByte(Random rng) => (byte)(rng.Next(1, 255));

        /// <summary>Builder C5(): [session, cmd, sub, ...params] trong goi 64 byte.</summary>
        public static byte[] BuildPacket(byte session, byte cmd, byte sub, params byte[] parms)
        {
            var p = new byte[PacketLen];
            p[0] = session; p[1] = cmd; p[2] = sub;
            if (parms != null) Array.Copy(parms, 0, p, 3, Math.Min(parms.Length, PacketLen - 3));
            return p;
        }

        /// <summary>Dap tra info hop le: 64 byte, echo [session, 1, 1],
        /// [3..10] == chu ky, [43]==1 (che do ban phim, khong phai bootloader).</summary>
        public static bool CheckInfoReply(byte[] reply, byte session)
        {
            if (reply == null || reply.Length < 44) return false;
            if (reply[0] != session || reply[1] != 1 || reply[2] != 1) return false;
            for (int i = 0; i < InfoSignature.Length; i++)
                if (reply[3 + i] != InfoSignature[i]) return false;
            return reply[43] == 1;
        }

        /// <summary>Cac goi frame N5(): [session, 6, 6, 0, 1, tong goi,
        /// index+1], 19 bo ba RGB tai offset 7; 65 muc -> 4 goi.</summary>
        public static List<byte[]> BuildFramePackets(byte session, byte[] rgbEntries)
        {
            int total = (StreamEntries + 18) / 19;
            var list = new List<byte[]>(total);
            for (int i = 0; i < total; i++)
            {
                var p = new byte[PacketLen];
                p[0] = session; p[1] = 6; p[2] = 6; p[3] = 0; p[4] = 1;
                p[5] = (byte)total; p[6] = (byte)(i + 1);
                int bytes = Math.Min(19 * 3, Math.Max(0, StreamEntries * 3 - i * 19 * 3));
                if (rgbEntries != null && bytes > 0)
                    Array.Copy(rgbEntries, i * 19 * 3, p, 7, Math.Min(bytes, Math.Max(0, rgbEntries.Length - i * 19 * 3)));
                list.Add(p);
            }
            return list;
        }
    }
}
