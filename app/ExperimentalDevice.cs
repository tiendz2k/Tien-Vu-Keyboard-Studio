using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HidSharp;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Transport rieng cho cac lenh per-key / stream (0xF5 / 0x23 / 0x20 /
    // 0x78) cua F75 Max. Tach khoi F75Device (duong verified, giu nguyen
    // 100%) — lop nay mo stream HID rieng toi kenh lenh 0xFF13 cua F75 Max
    // khi can.
    //
    // v3.14 (ban hoan thien): khong con Dry-run, khong con cong tac thu
    // nghiem — moi lenh gui deu GUI THAT ngay khi nguoi dung bam nut.
    // Doc 0xF5 (ReadColors) van la thao tac CHI DOC nhu truoc.
    //
    // KY LUAT TRUYEN (v3.9.2 — port theo co che dang chay that cua ung
    // dung tham chieu tren cung nen tang SONiX, qua SonixCommander):
    // - Chon interface: bat buoc usage page 0xFF13 / usage 1 (xac minh qua
    //   report descriptor khi HidSharp doc duoc; khong doc duoc thi lui
    //   ve heuristic cu va ghi log ro interface da chon).
    // - Bat tay phien stream: 0xF5 (ACK ready) -> doc bo 9 khoi trang
    //   thai -> 0x02 (ACK ready), chay mot lan khi phien stream duoc mo
    //   (armed). Khong armed thi chi log cac buoc bat tay nhu goi se gui.
    // - ACK: cho 5 ms roi doc; reply toan 0 thi poll tiep theo thang
    //   20/40/80/160/200/200/300 ms; header 0x20 ban -> gui lai <=3 lan,
    //   qua thi BO frame va stream tiep (khong nem loi); 0x02 ban ->
    //   gui lai sau 35 ms, <=2 lan; pacer 2 ms truoc moi khoi du lieu.
    // - Ngoai stream: het ngan sach ACK van nem exception nhu cu (ghi
    //   0x23 phai that bai la dung ngay — an toan hon la ghi tiep).
    //
    // v3.17 (scope E — Tien bao loi that tren F75 Max: ghi 0x23 chet o
    // buoc 'Commit' (04 02) voi ket qua Failed; xac nhan khong co app
    // nao khac giu phim — loi nam trong chinh duong ghi cua minh):
    // - Phien GHI BEN (0x23) cung chay bat tay 0xF5 -> 9 khoi -> 0x02
    //   truoc khi gui (truoc day chi stream moi co) — dung ky luat
    //   connect cua ung dung tham chieu; bat tay khong ready thi huy
    //   lan ghi truoc khi gui bat ky khoi mau nao.
    // - Commit 0x02 duoc gui lai ca khi het thang poll chi thay reply
    //   toan 0 (firmware ghi flash khong phat byte ban) — sua o
    //   SonixCommander (ExperimentalTransport.cs).
    // - Them nhip nghi 10 ms truoc buoc Commit khi no di ngay sau cac
    //   khoi du lieu (Graham: cac goi khong duoc gui sat nhau).
    // - Buoc ket thuc 04 F0 khong ACK nua thi KHONG lam hong ca lan ghi
    //   (commit da xong): ghi canh bao, danh dau mo lai kenh + bat tay
    //   lai o lan gui ke tiep — F0 co the lam phim khoi dong lai / rot
    //   HID (tai lieu PROTOCOL-F75MAX §3.6); duong verified cua hang
    //   cung dat expectAck: false cho F0.
    // - Nhat ky ghi ket qua ACK cua TUNG buoc (cac phien khong phai
    //   stream) va thong bao loi kem hex reply cuoi thay duoc — phuc vu
    //   chan doan tu xa qua nhat ky nguoi dung gui lai.
    // =====================================================================
    public sealed class ExperimentalDevice : IDisposable
    {
        public Action<string> Log { get; set; } = _ => { };

        // v3.17: nhip nghi truoc buoc Commit khi no di ngay sau khoi du
        // lieu (ms) — Graham ghi ro cac goi khong duoc gui sat nhau,
        // mac dinh cua anh la 10 ms.
        private const int CommitSettleMs = 10;

        private readonly object _gate = new object();
        private HidStream _cmdStream;
        private HidFeatureChannel _channel;
        private SonixCommander _commander;
        private bool _handshakeDone;
        private bool _reopenBeforeNextCall;
        private bool _disposed;

        // Kenh feature-report tren HidStream: goi gui/di la 65 byte
        // ([0]=report id 0 + 64 byte lenh) giong moi duong khac cua app.
        private sealed class HidFeatureChannel : IFeatureReportChannel
        {
            private readonly HidStream _stream;
            public HidFeatureChannel(HidStream stream) { _stream = stream; }

            public void Send(byte[] packet64)
            {
                var buf = new byte[65];
                buf[0] = 0;
                Array.Copy(packet64, 0, buf, 1, 64);
                _stream.SetFeature(buf, 0, buf.Length);
            }

            public byte[] TryReceive()
            {
                var buf = new byte[65];
                buf[0] = 0;
                try
                {
                    _stream.GetFeature(buf, 0, buf.Length);
                    var data = new byte[64];
                    Array.Copy(buf, 1, data, 0, 64);
                    return data;
                }
                catch { return null; }
            }
        }

        // Xep hang giao dien theo usage page: 0 = da xac minh 0xFF13/1,
        // 1 = khong doc duoc descriptor (khong ro), 2 = da xac minh la
        // page KHAC 0xFF13. HidSharp khong lo usage page qua API don gian
        // nen phai di qua cay report descriptor (DeviceItem.Usages chua
        // usage day du page<<16|usage); moi loi doc deu lui ve hang 1.
        private static int UsageRank(HidDevice dev, out string note)
        {
            try
            {
                var desc = dev.GetReportDescriptor();
                var usages = new HashSet<uint>();
                foreach (var item in desc.DeviceItems) CollectUsages(item, usages);
                if (usages.Count == 0)
                {
                    note = "report descriptor khong chua usage nao";
                    return 1;
                }
                var pages = new SortedSet<uint>();
                foreach (var u in usages) pages.Add(u >> 16);
                string pageList = string.Join(", ", pages.Select(p => "0x" + p.ToString("X4")));
                if (usages.Contains(0xFF130001u))
                {
                    note = $"usage page 0xFF13 / usage 1 (xac minh qua report descriptor; cac page thay duoc: {pageList})";
                    return 0;
                }
                if (pages.Contains(0xFF13u))
                {
                    note = $"co usage page 0xFF13 trong descriptor nhung khong thay usage 1 day du (cac page: {pageList})";
                    return 1;
                }
                note = $"descriptor chi thay cac usage page: {pageList} (khong co 0xFF13)";
                return 2;
            }
            catch (Exception ex)
            {
                note = $"khong doc duoc report descriptor ({ex.GetType().Name}: {ex.Message})";
                return 1;
            }
        }

        private static void CollectUsages(HidSharp.Reports.DescriptorItem item, HashSet<uint> into)
        {
            if (item == null) return;
            try { foreach (var u in item.Usages.GetAllValues()) into.Add(u); } catch { }
            try
            {
                if (item.ChildItems != null)
                    foreach (var child in item.ChildItems) CollectUsages(child, into);
            }
            catch { }
        }

        private bool EnsureStream()
        {
            if (_cmdStream != null) return true;
            var candidates = F75Device.Enumerate()
                .Where(i => i.MaxFeature >= 64 &&
                            i.Device.VendorID == F75Protocol.VendorId && i.Device.ProductID == F75Protocol.ProductId)
                .ToList();
            if (candidates.Count == 0)
            {
                Log("[PER-KEY] Khong tim thay kenh lenh F75 Max (0xFF13). Kiem tra cap USB-C.");
                return false;
            }
            var ranked = candidates
                .Select(c => { int rank = UsageRank(c.Device, out string note); return (Info: c, Rank: rank, Note: note); })
                .OrderBy(x => x.Rank)
                .ToList();
            foreach (var cand in ranked)
            {
                if (!cand.Info.Device.TryOpen(out _cmdStream)) continue;
                _cmdStream.ReadTimeout = 300;
                _cmdStream.WriteTimeout = 1000;
                _channel = new HidFeatureChannel(_cmdStream);
                _commander = new SonixCommander(_channel, Thread.Sleep, Log);
                _handshakeDone = false;
                Log($"[PER-KEY] Da mo kenh lenh rieng cho lop thu nghiem — {cand.Note}. Path: {cand.Info.Path}");
                if (cand.Rank == 2)
                    Log("[PER-KEY] CANH BAO: giao dien mo duoc khong phai 0xFF13 theo descriptor — neu stream khong phan hoi, hay xem lai log chon kenh.");
                return true;
            }
            Log("[PER-KEY] Khong mo duoc kenh lenh (thiet bi dang ban?).");
            return false;
        }

        // ---------------- Bat tay phien stream ----------------
        // Dung trinh tu connect cua ung dung tham chieu: 0xF5 bat buoc
        // ready -> doc 9 khoi trang thai ROI BO DI (ho khong luu backup;
        // stop() cua ho cung khong gui gi) -> 0x02 bat buoc ready.
        private bool RunHandshake()
        {
            Log("[PER-KEY] Bat tay phien stream: 0xF5 -> doc bo 9 khoi -> 0x02 ...");
            var probe = L99Protocol.BuildCommand(L99Protocol.OpColorQuery, L99Protocol.ColorBlockCount);
            var oc = _commander.SendCommand(probe);
            if (oc != SonixAckOutcome.Ready)
            {
                Log($"[PER-KEY] Bat tay that bai: lenh 0xF5 khong ve ready (ket qua: {oc}). Phien stream chua mo; frame nay bi bo, frame sau se thu bat tay lai.");
                return false;
            }
            int got = 0;
            for (int i = 0; i < L99Protocol.ColorBlockCount; i++)
            {
                Thread.Sleep(1); // tham chieu: wait(1) truoc moi lan doc khoi
                if (_channel.TryReceive() != null) got++;
            }
            Log($"[PER-KEY] Da doc bo {got}/{L99Protocol.ColorBlockCount} khoi trang thai sau 0xF5.");
            var end = _commander.SendCommand(L99Protocol.BuildCommand(L99Protocol.OpCommit));
            if (end != SonixAckOutcome.Ready)
            {
                Log($"[PER-KEY] Bat tay that bai: lenh 0x02 khong ve ready (ket qua: {end}).");
                return false;
            }
            _handshakeDone = true;
            Log("[PER-KEY] Bat tay xong — phien stream san sang.");
            return true;
        }

        // Log loi stream co tiet che: stream chay ~17fps, loi lap lai tung
        // frame se ngap nhat ky — chi log toi da 1 lan / 5 giay.
        private DateTime _lastStreamIssueLog = DateTime.MinValue;
        private int _suppressedIssues;
        private void LogStreamIssue(string msg)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastStreamIssueLog).TotalSeconds >= 5)
            {
                Log(msg + (_suppressedIssues > 0 ? $" (da tiet che {_suppressedIssues} thong bao loi stream tuong tu)" : ""));
                _suppressedIssues = 0;
                _lastStreamIssueLog = now;
            }
            else _suppressedIssues++;
        }

        private static bool IsStreamSequence(IList<F75Protocol.Step> steps)
            => steps.Any(s => s.Bytes != null && s.Bytes.Length > 1 && s.Bytes[1] == L99Protocol.OpColorStream);

        private static bool IsWriteSequence(IList<F75Protocol.Step> steps)
            => steps.Any(s => s.Bytes != null && s.Bytes.Length > 1 && s.Bytes[1] == L99Protocol.OpColorSet);

        // Dong kenh hien tai (neu co) va quen trang thai bat tay — dung
        // khi buoc 04 F0 lan truoc co the da lam thiet bi khoi dong lai
        // / rot HID, va trong Dispose.
        private void CloseChannel()
        {
            try { _cmdStream?.Dispose(); } catch { }
            _cmdStream = null;
            _channel = null;
            _commander = null;
            _handshakeDone = false;
        }

        private void ReopenIfMarked()
        {
            if (!_reopenBeforeNextCall) return;
            _reopenBeforeNextCall = false;
            CloseChannel();
            Log("[PER-KEY] Mo lai kenh lenh: buoc 04 F0 lan truoc khong ACK (thiet bi co the da khoi dong lai).");
        }

        private string LastReplyHex()
            => _commander?.LastReply != null ? F75Device.Hex(_commander.LastReply, 8) : "(chua doc duoc reply nao)";

        // Gui mot chuoi Step — GUI THAT truc tiep (v3.14: khong con che
        // do chi-log). Tra ve true neu da gui xong; false neu frame stream
        // bi bo vi bat tay chua xong. Voi stream: loi ACK khong nem
        // exception (bo frame, stream song tiep). Voi cac duong ghi khac:
        // het ngan sach ACK van nem exception nhu cu.
        public bool SendSteps(string title, IList<F75Protocol.Step> steps)
        {
            lock (_gate)
            {
                bool isStream = IsStreamSequence(steps);
                bool isWrite = IsWriteSequence(steps);
                ReopenIfMarked();
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh lenh F75 Max.");
                if (isStream && !_handshakeDone && !RunHandshake())
                    return false;
                if (isWrite && !_handshakeDone && !RunHandshake())
                    throw new InvalidOperationException(
                        "Bắt tay phiên (0xF5 → đọc bỏ 9 khối → 0x02) không đạt ready — huỷ lần ghi per-key này trước khi gửi bất kỳ khối màu nào. Chi tiết trong nhật ký thao tác.");
                Log($"[PER-KEY] {title}: {steps.Count} goi.");
                F75Protocol.Step prev = null;
                foreach (var s in steps)
                {
                    if (!s.ExpectAck)
                    {
                        _commander.SendBlock(s.Bytes); // pacer 2 ms truoc khoi
                        prev = s;
                        continue;
                    }
                    byte opcode = s.Bytes[1];
                    // Commit di ngay sau cac khoi du lieu: them nhip nghi
                    // ngan de firmware kip nuot khoi cuoi (Graham).
                    if (opcode == L99Protocol.OpCommit && prev != null && !prev.ExpectAck)
                        Thread.Sleep(CommitSettleMs);
                    var outcome = _commander.SendCommand(s.Bytes);
                    if (!isStream)
                        Log($"[PER-KEY]   <- {s.Name} (opcode 0x{opcode:X2}): ACK {outcome}");
                    prev = s;
                    if (outcome == SonixAckOutcome.Ready) continue;
                    if (outcome == SonixAckOutcome.DroppedBusy && opcode == L99Protocol.OpColorStream)
                    {
                        // Header stream ban dai: bo frame nay, stream tiep tuc.
                        LogStreamIssue($"[PER-KEY] Header 0x20 van ban sau {SonixCommander.StreamHeaderBusyRetries} lan gui lai — bo frame nay, stream tiep tuc.");
                        return true;
                    }
                    if (outcome == SonixAckOutcome.DroppedBusy && opcode == L99Protocol.OpCommit)
                    {
                        // 0x02 ban dai: da gui lai 2 lan theo ky luat; ghi log va di tiep.
                        Log($"[PER-KEY] Lenh 0x02 van ban sau {SonixCommander.EndBusyRetries} lan gui lai — bo qua buoc '{s.Name}', tiep tuc.");
                        continue;
                    }
                    if (opcode == L99Protocol.OpEnd)
                    {
                        // 04 F0 khong ACK: commit da xong truoc do, dung
                        // lam hong ca lan ghi. Thiet bi co the dang khoi
                        // dong lai / rot HID — danh dau mo lai kenh va
                        // bat tay lai o lan gui ke tiep.
                        Log($"[PER-KEY] CANH BAO: buoc ket thuc phien '{s.Name}' (04 F0) khong ACK (ket qua: {outcome}, reply cuoi: {LastReplyHex()}). Phan ghi chinh da xong; lan gui sau se mo lai kenh va bat tay lai.");
                        _reopenBeforeNextCall = true;
                        continue;
                    }
                    if (isStream)
                    {
                        LogStreamIssue($"[PER-KEY] Buoc '{s.Name}' (opcode 0x{opcode:X2}) khong nhan duoc ACK hop le trong ngan sach thoi gian — bo frame nay, stream tiep tuc.");
                        return true;
                    }
                    throw new InvalidOperationException(
                        $"Thiết bị không ACK bước '{s.Name}' (opcode 0x{opcode:X2}, kết quả: {outcome}, reply cuối thấy được: {LastReplyHex()}). Dừng lại để an toàn.");
                }
                return true;
            }
        }

        // =====================================================================
        // 0xF5 — doc mau per-key (CHI DOC, chay live theo yeu cau nguoi dung).
        // Framing theo protocol.py: commit -> query -> 9 lan GET_FEATURE tho.
        // Hai lenh dau di qua SonixCommander (ky luat ACK moi, chiu dung
        // reply cham/ban) nhung loi ACK o day chi canh bao, van doc tiep.
        // =====================================================================
        public Dictionary<byte, (byte R, byte G, byte B)> ReadColors()
        {
            lock (_gate)
            {
                ReopenIfMarked();
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh lenh F75 Max. Kiem tra cap USB-C va dong app chinh hang / app tham chieu neu dang mo.");
                var steps = L99Protocol.BuildColorQuerySteps();
                Log("[0xF5] Bat dau doc mau phim (chi doc)...");
                foreach (var s in steps)
                {
                    var outcome = _commander.SendCommand(s.Bytes);
                    Log($"[0xF5] -> {s.Name}: {F75Device.Hex(s.Bytes, 12)} (ACK: {outcome})");
                    if (outcome != SonixAckOutcome.Ready)
                        Log($"[0xF5] Canh bao: chua thay ACK chuan cho 0x{s.Bytes[1]:X2}, van tiep tuc doc khoi.");
                }
                var blocks = new List<byte[]>();
                int attempts = 0;
                while (blocks.Count < L99Protocol.ColorBlockCount && attempts < 60)
                {
                    attempts++;
                    var data = _channel.TryReceive();
                    if (data == null) { Thread.Sleep(25); continue; }
                    // Bo qua ban ACK echo cua chinh lenh query (04 F5 .. 01, con lai 0)
                    bool isAckEcho = data[0] == 0x04 && data[1] == L99Protocol.OpColorQuery && data[3] == 0x01
                                     && data.Skip(4).Take(56).All(b => b == 0);
                    if (isAckEcho) { Log("[0xF5] (bo qua 1 ban ACK echo)"); continue; }
                    blocks.Add(data);
                    Log($"[0xF5] <- khoi {blocks.Count}/9: {F75Device.Hex(data, 16)}");
                    Thread.Sleep(10);
                }
                if (blocks.Count < L99Protocol.ColorBlockCount)
                    throw new InvalidOperationException(
                        $"Chi doc duoc {blocks.Count}/9 khoi mau. Co the app chinh hang hoac ung dung tham chieu dang mo va chiem kenh — hay dong chung roi thu lai.");
                var colors = L99Protocol.ParseColorBlocks(blocks);
                Log($"[0xF5] Doc xong: {colors.Count} id phim co mau.");
                return colors;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CloseChannel();
        }
    }
}
