using System;
using System.Collections.Generic;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // LOP THU NGHIEM (experimental) — giao thuc per-key/stream/nhac cua nen
    // tang SONiX 0C45:800A, tai lieu hoa cong khai boi Gavin Graham cho
    // AULA L99 (cung nen tang voi F75 Max: trung VID:PID day 0C45:800A,
    // trung khung feature report 64B va khoi hieu ung 04 13).
    // Nguon: github.com/Revisory676/aula-l99-linux, tools/aula_l99_hacky/protocol.py
    //
    // CAC BUILDER O DAY DUOC PORT NGUYEN VAN LOGIC CUA protocol.py va co
    // unit test doi chieu byte-for-byte voi chinh protocol.py (xem tests/).
    //
    // - Doc 0xF5: chi doc (read-only) — chay live.
    // - Ghi 0x23 / stream 0x20 / feed nhac 0x78: gui truc tiep khi nguoi
    //   dung bam nut (v3.14: khong con cong tac thu nghiem / Dry-run).
    // File F75Protocol.cs / F75Device.cs (duong verified) KHONG bi dong vao.
    // =====================================================================
    public static class L99Protocol
    {
        public const byte CmdPrefix = 0x04;
        public const byte OpBegin = 0x18;
        public const byte OpCommit = 0x02;
        public const byte OpEnd = 0xF0;
        public const byte OpEffect = 0x13;
        public const byte OpColorSet = 0x23;    // ghi per-key ben vung (9 block)
        public const byte OpColorStream = 0x20; // stream thoi gian thuc (~17fps)
        public const byte OpColorQuery = 0xF5;  // doc lai mau per-key (chi doc)
        public const byte OpSettings = 0x17;    // khoi cai dat thiet bi (04 17)
        public const byte OpAudio = 0x78;       // feed pho nhac (1 block / frame)
        public const byte EffectCustom = 0x80;  // hieu ung "custom" hien mau 0x23

        public const int PacketSize = 64;
        public const int KeyRows = 8;           // id = (row<<4)|col
        public const int KeysPerRow = 16;
        public const int ColorBlockCount = 9;   // 8 hang + 1 terminator
        public const int StreamBlockCount = 8;  // 8 x 64B = 512B = 128 o
        public const int StreamSlotCount = 128;
        public const int AudioBandCount = 23;

        // --- lenh co ban: [0]=0x04 [1]=opcode [2]=byte2 [8]=block_count -----
        public static byte[] BuildCommand(byte opcode, int blockCount = 0, byte byte2 = 0)
        {
            var p = new byte[PacketSize];
            p[0] = CmdPrefix;
            p[1] = opcode;
            p[2] = byte2;
            p[8] = (byte)blockCount;
            return p;
        }

        private static F75Protocol.Step Step(string name, byte[] bytes, bool expectAck = true)
            => new F75Protocol.Step(name, bytes, expectAck);

        private static F75Protocol.Step BeginStep() => Step("Mo phien (04 18)", BuildCommand(OpBegin));
        private static F75Protocol.Step CommitStep() => Step("Commit (04 02)", BuildCommand(OpCommit));
        private static F75Protocol.Step EndStep() => Step("Dong phien (04 F0)", BuildCommand(OpEnd));

        // =====================================================================
        // 0x23 — ghi mau per-key ben vung (port cua build_color_blocks +
        // build_transfer trong protocol.py)
        // 8 block hang (row = id>>4): moi block 16 o x [key_id, R, G, B],
        // vi tri o = col (id & 0x0F); phim khong co mau de trong (toan 0).
        // Block thu 9 la terminator: toan 0, AA 55 o byte 62..63.
        // =====================================================================
        public static List<byte[]> BuildColorBlocks(IReadOnlyDictionary<byte, (byte R, byte G, byte B)> colors)
        {
            var blocks = new List<byte[]>();
            for (int row = 0; row < KeyRows; row++)
            {
                var block = new byte[PacketSize];
                for (int col = 0; col < KeysPerRow; col++)
                {
                    byte id = (byte)((row << 4) | col);
                    if (colors.TryGetValue(id, out var c))
                    {
                        int off = col * 4;
                        block[off] = id;
                        block[off + 1] = c.R;
                        block[off + 2] = c.G;
                        block[off + 3] = c.B;
                    }
                }
                blocks.Add(block);
            }
            var term = new byte[PacketSize];
            term[62] = 0xAA; term[63] = 0x55;
            blocks.Add(term);
            return blocks;
        }

        // Toan bo phien ghi 0x23 dang Step (de log / gui qua ExperimentalDevice):
        // begin -> header 0x23 ([8]=9) -> 9 block -> commit -> end.
        // Sau do can chon hieu ung 0x80 (BuildCustomColorTrigger) de hien thi.
        public static List<F75Protocol.Step> BuildColorWriteSteps(IReadOnlyDictionary<byte, (byte R, byte G, byte B)> colors)
        {
            var blocks = BuildColorBlocks(colors);
            var steps = new List<F75Protocol.Step> { BeginStep(), Step("Header ghi mau (04 23)", BuildCommand(OpColorSet, blocks.Count)) };
            for (int i = 0; i < blocks.Count; i++)
                steps.Add(Step($"Khoi mau 0x23 #{i + 1}/{blocks.Count}", blocks[i], expectAck: false));
            steps.Add(CommitStep());
            steps.Add(EndStep());
            return steps;
        }

        // Chon hieu ung Custom 0x80 qua duong 04 13 verified (dung lai
        // F75Protocol.BuildLighting — cung layout khoi hieu ung cua protocol.py:
        // [0]=id [1..3]=RGB [8]=co che do [9]=sang [10]=toc do [14..15]=AA 55).
        // Byte [8] = 1: khop ban ghi cua Graham cho 0x80 (mode_flag mac dinh 1
        // voi moi hieu ung ngoai 0x04/0x07) va hop nghia "dung mau tung phim".
        public static List<F75Protocol.Step> BuildCustomColorTrigger(byte brightness = 5, byte speed = 3)
            => F75Protocol.BuildLighting(EffectCustom, 255, 255, 255, true, brightness, speed, 0);

        // =====================================================================
        // 0xF5 — doc lai mau per-key (CHI DOC). Framing cua protocol.py:
        // commit (04 02) TRUOC, roi lenh query (04 F5, [8]=9); sau do doc 9
        // block bang GET_FEATURE tho (khong framing, KHONG terminator —
        // ca 9 block deu la du lieu hang, id trai 0x00..0x8F).
        // =====================================================================
        public static List<F75Protocol.Step> BuildColorQuerySteps() => new List<F75Protocol.Step>
        {
            CommitStep(),
            Step("Truy van mau (04 F5)", BuildCommand(OpColorQuery, ColorBlockCount)),
        };

        // Giai ma 9 block doc ve -> id -> (R,G,B). Bo qua o trong (id byte = 0).
        // (protocol.py chi giu id nam trong KEY_IDS cua L99; ban nay giu moi id
        // khac 0 — tren Max, MainWindow tu loc theo MaxKeyData.KeyIds neu can.)
        public static Dictionary<byte, (byte R, byte G, byte B)> ParseColorBlocks(IEnumerable<byte[]> blocks)
        {
            var result = new Dictionary<byte, (byte R, byte G, byte B)>();
            foreach (var block in blocks)
            {
                if (block == null || block.Length < PacketSize) continue;
                for (int slot = 0; slot < KeysPerRow; slot++)
                {
                    int off = slot * 4;
                    byte id = block[off];
                    if (id == 0) continue;
                    result[id] = (block[off + 1], block[off + 2], block[off + 3]);
                }
            }
            return result;
        }

        // =====================================================================
        // 0x20 — stream thoi gian thuc (port cua build_stream_blocks /
        // build_stream_frame). Moi frame: header (04 20, [8]=8) -> 8 block ->
        // commit (04 02). KHONG begin, KHONG end, KHONG trailer AA 55.
        // 8 block = 1 mang phang 512B = 128 o [key_id, R, G, B]; cac o dau la
        // phim vat ly theo thu tu stream (MaxKeyData.StreamSlots), moi o mang
        // id cua chinh no ke ca khi tat den (0,0,0) — thieu o se lam lech toan
        // bo phim phia sau. O thua (128 - so phim) de 0.
        // =====================================================================
        public struct StreamQuad
        {
            public byte Id, R, G, B;
            public StreamQuad(byte id, byte r, byte g, byte b) { Id = id; R = r; G = g; B = b; }
        }

        public static List<byte[]> BuildStreamBlocks(IReadOnlyList<StreamQuad> slots)
        {
            if (slots.Count > StreamSlotCount)
                throw new ArgumentException($"Stream chi chua toi da {StreamSlotCount} o, nhan {slots.Count}.");
            var payload = new byte[StreamBlockCount * PacketSize];
            for (int i = 0; i < slots.Count; i++)
            {
                int off = i * 4;
                payload[off] = slots[i].Id;
                payload[off + 1] = slots[i].R;
                payload[off + 2] = slots[i].G;
                payload[off + 3] = slots[i].B;
            }
            var blocks = new List<byte[]>();
            for (int i = 0; i < StreamBlockCount; i++)
            {
                var b = new byte[PacketSize];
                Array.Copy(payload, i * PacketSize, b, 0, PacketSize);
                blocks.Add(b);
            }
            return blocks;
        }

        // Ban tien loi: slots tu bang mau theo id + thu tu stream cho truoc
        // (dung cho test doi chieu protocol.py va cac hieu ung theo id).
        public static List<byte[]> BuildStreamBlocks(IReadOnlyList<byte> streamOrder, IReadOnlyDictionary<byte, (byte R, byte G, byte B)> colors)
        {
            var slots = new List<StreamQuad>();
            foreach (var id in streamOrder)
            {
                colors.TryGetValue(id, out var c);
                slots.Add(new StreamQuad(id, c.R, c.G, c.B));
            }
            return BuildStreamBlocks(slots);
        }

        public static List<F75Protocol.Step> BuildStreamFrameSteps(IReadOnlyList<StreamQuad> slots)
        {
            var blocks = BuildStreamBlocks(slots);
            var steps = new List<F75Protocol.Step> { Step("Header stream (04 20)", BuildCommand(OpColorStream, StreamBlockCount)) };
            for (int i = 0; i < blocks.Count; i++)
                steps.Add(Step($"Khoi stream #{i + 1}/{blocks.Count}", blocks[i], expectAck: false));
            steps.Add(CommitStep());
            return steps;
        }

        // =====================================================================
        // 0x78 — feed pho nhac (port cua build_audio_blocks / build_audio_frame).
        // Framing: commit (04 02) TRUOC -> header (04 78, [8]=1) -> 1 block.
        // Block: [0]=kieu nhip (rhythm) [1]=che do nen [2]=bien do (scale,
        // mac dinh 100; moi level phai <= scale) [3..25]=23 muc pho thap->cao
        // [26]=do sang nen, con lai 0. Block nay KHONG co trailer AA 55.
        // =====================================================================
        public static byte[] BuildAudioBlock(IReadOnlyList<int> levels, int scale = 100, byte rhythm = 0x04, byte backgroundMode = 0x08, byte backgroundBrightness = 0)
        {
            if (levels.Count != AudioBandCount)
                throw new ArgumentException($"Can dung {AudioBandCount} muc pho, nhan {levels.Count}.");
            if (scale < 0 || scale > 255) throw new ArgumentOutOfRangeException(nameof(scale));
            var block = new byte[PacketSize];
            block[0] = rhythm;
            block[1] = backgroundMode;
            block[2] = (byte)scale;
            for (int i = 0; i < AudioBandCount; i++)
            {
                int v = levels[i];
                if (v < 0 || v > scale)
                    throw new ArgumentOutOfRangeException(nameof(levels), $"Muc pho {v} ngoai khoang 0..{scale}.");
                block[3 + i] = (byte)v;
            }
            block[26] = backgroundBrightness;
            return block;
        }

        public static List<F75Protocol.Step> BuildAudioFrameSteps(IReadOnlyList<int> levels, int scale = 100, byte rhythm = 0x04, byte backgroundMode = 0x08, byte backgroundBrightness = 0)
        {
            return new List<F75Protocol.Step>
            {
                CommitStep(),
                Step("Header nhac (04 78)", BuildCommand(OpAudio, 1)),
                Step("Khoi pho nhac 0x78", BuildAudioBlock(levels, scale, rhythm, backgroundMode, backgroundBrightness), expectAck: false),
            };
        }

        public static readonly string[] AudioStyleNames =
        {
            "Tắt", "Xanh lá/Vàng/Đỏ", "Cầu vồng", "Cầu vồng ngược", "Gradient",
            "Xoay phổ", "Trắng", "Đỏ", "Cam", "Vàng", "Xanh lá", "Xanh lơ",
            "Xanh dương", "Tím", "Ambilight",
        };

        // =====================================================================
        // 04 17 — khoi cai dat thiet bi (tai lieu mohin7 + VitalyArt, doi chieu
        // offset voi protocol.py cua Graham: TAG1=1, SLEEP=6, RESPONSE=8,
        // trailer 62..63). Framing giong khoi sang: begin -> lenh chon (04 17,
        // byte[2]=0x01 dac biet, [8]=1) -> payload -> commit -> end.
        // Payload: [1]=0x01 [2]=khoa Win [3]=khoa Alt+F4 [4]=khoa Alt+Tab
        // [5]=chuyen Fn [6]=ngu (0=khong, 1=1 phut, 2=5 phut, 3=30 phut)
        // [8]=phan hoi phim 1..5, [62..63]=AA 55.
        // Mac dinh xuat xuong (payload prefix): 00 01 00 00 00 00 02 00 02.
        // =====================================================================
        public static byte[] BuildSettingsBlock(bool winLock, bool altF4Lock, bool altTabLock, bool fnSwitch, int sleep, int keyResponse)
        {
            if (sleep < 0 || sleep > 3) throw new ArgumentOutOfRangeException(nameof(sleep), "Ngu: 0..3.");
            if (keyResponse < 1 || keyResponse > 5) throw new ArgumentOutOfRangeException(nameof(keyResponse), "Phan hoi phim: 1..5.");
            var block = new byte[PacketSize];
            block[1] = 0x01;
            block[2] = (byte)(winLock ? 1 : 0);
            block[3] = (byte)(altF4Lock ? 1 : 0);
            block[4] = (byte)(altTabLock ? 1 : 0);
            block[5] = (byte)(fnSwitch ? 1 : 0);
            block[6] = (byte)sleep;
            block[8] = (byte)keyResponse;
            block[62] = 0xAA; block[63] = 0x55;
            return block;
        }

        public static List<F75Protocol.Step> BuildSettingsSteps(bool winLock, bool altF4Lock, bool altTabLock, bool fnSwitch, int sleep, int keyResponse)
        {
            return new List<F75Protocol.Step>
            {
                BeginStep(),
                Step("Chon khoi cai dat (04 17)", BuildCommand(OpSettings, 1, byte2: 0x01)),
                Step("Khoi cai dat 04 17", BuildSettingsBlock(winLock, altF4Lock, altTabLock, fnSwitch, sleep, keyResponse), expectAck: false),
                CommitStep(),
                EndStep(),
            };
        }
    }
}
