using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HidSharp;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Transport rieng cho LOP THU NGHIEM (0xF5 / 0x23 / 0x20 / 0x78).
    // Tach khoi F75Device (duong verified, giu nguyen 100%) — lop nay mo
    // stream HID rieng toi kenh lenh 0xFF13 cua F75 Max khi can.
    //
    // QUY TAC AN TOAN (hard-coded, khong co ngoai le):
    // - Doc 0xF5 (ReadColors): CHI DOC — duoc phep chay live bat cu luc nao
    //   nguoi dung bam nut, ke ca khi Dry-run toan cuc dang bat.
    // - Ghi (SendSteps): CHI gui that khi ExperimentalEnabled == true VA
    //   DryRun == false. Thieu mot trong hai -> chi ghi log cac goi se gui.
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
    // =====================================================================
    public sealed class ExperimentalDevice : IDisposable
    {
        private bool _dryRun = true;
        private bool _experimentalEnabled;

        public bool DryRun
        {
            get => _dryRun;
            set { if (_dryRun != value) { _dryRun = value; _handshakeDone = false; } }
        }
        public bool ExperimentalEnabled
        {
            get => _experimentalEnabled;
            set { if (_experimentalEnabled != value) { _experimentalEnabled = value; _handshakeDone = false; } }
        }
        public Action<string> Log { get; set; } = _ => { };

        private readonly object _gate = new object();
        private HidStream _cmdStream;
        private HidFeatureChannel _channel;
        private SonixCommander _commander;
        private bool _handshakeDone;
        private bool _disposed;

        public bool WritesArmed => ExperimentalEnabled && !DryRun;

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
                Log("[THU NGHIEM] Khong tim thay kenh lenh F75 Max (0xFF13). Kiem tra cap USB-C.");
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
                Log($"[THU NGHIEM] Da mo kenh lenh rieng cho lop thu nghiem — {cand.Note}. Path: {cand.Info.Path}");
                if (cand.Rank == 2)
                    Log("[THU NGHIEM] CANH BAO: giao dien mo duoc khong phai 0xFF13 theo descriptor — neu stream khong phan hoi, hay xem lai log chon kenh.");
                return true;
            }
            Log("[THU NGHIEM] Khong mo duoc kenh lenh (thiet bi dang ban?).");
            return false;
        }

        // ---------------- Bat tay phien stream (armed) ----------------
        // Dung trinh tu connect cua ung dung tham chieu: 0xF5 bat buoc
        // ready -> doc 9 khoi trang thai ROI BO DI (ho khong luu backup;
        // stop() cua ho cung khong gui gi) -> 0x02 bat buoc ready.
        private bool RunHandshake()
        {
            Log("[THU NGHIEM] Bat tay phien stream: 0xF5 -> doc bo 9 khoi -> 0x02 ...");
            var probe = L99Protocol.BuildCommand(L99Protocol.OpColorQuery, L99Protocol.ColorBlockCount);
            var oc = _commander.SendCommand(probe);
            if (oc != SonixAckOutcome.Ready)
            {
                Log($"[THU NGHIEM] Bat tay that bai: lenh 0xF5 khong ve ready (ket qua: {oc}). Phien stream chua mo; frame nay bi bo, frame sau se thu bat tay lai.");
                return false;
            }
            int got = 0;
            for (int i = 0; i < L99Protocol.ColorBlockCount; i++)
            {
                Thread.Sleep(1); // tham chieu: wait(1) truoc moi lan doc khoi
                if (_channel.TryReceive() != null) got++;
            }
            Log($"[THU NGHIEM] Da doc bo {got}/{L99Protocol.ColorBlockCount} khoi trang thai sau 0xF5.");
            var end = _commander.SendCommand(L99Protocol.BuildCommand(L99Protocol.OpCommit));
            if (end != SonixAckOutcome.Ready)
            {
                Log($"[THU NGHIEM] Bat tay that bai: lenh 0x02 khong ve ready (ket qua: {end}).");
                return false;
            }
            _handshakeDone = true;
            Log("[THU NGHIEM] Bat tay xong — phien stream san sang.");
            return true;
        }

        private void LogHandshakeWouldSend()
        {
            Log($"    -> Bat tay phien: {F75Device.Hex(L99Protocol.BuildCommand(L99Protocol.OpColorQuery, L99Protocol.ColorBlockCount), 12)} (04 F5, [8]=9)");
            Log("    -> Doc bo 9 khoi trang thai (buoc doc, khong co goi gui)");
            Log($"    -> Ket thuc bat tay: {F75Device.Hex(L99Protocol.BuildCommand(L99Protocol.OpCommit), 12)} (04 02)");
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

        // Gui mot chuoi Step. Tra ve true neu da gui that (live), false neu
        // chi log (dry-run / chua bat thu nghiem) hoac frame bi bo vi bat
        // tay chua xong. Voi stream: loi ACK khong nem exception (bo frame,
        // stream song tiep). Voi cac duong ghi khac: het ngan sach ACK van
        // nem exception nhu cu.
        public bool SendSteps(string title, IList<F75Protocol.Step> steps)
        {
            lock (_gate)
            {
                bool isStream = IsStreamSequence(steps);
                if (!WritesArmed)
                {
                    string why = !ExperimentalEnabled
                        ? "cong tac Thu nghiem dang TAT"
                        : "Dry-run toan cuc dang BAT";
                    Log($"[THU NGHIEM - CHI LOG] {title}: se gui {steps.Count} goi (khong gui that — {why}).");
                    if (isStream) LogHandshakeWouldSend();
                    foreach (var s in steps)
                        Log($"    -> {s.Name}: {F75Device.Hex(s.Bytes, 24)}");
                    return false;
                }
                if (!EnsureStream())
                    throw new InvalidOperationException("Khong mo duoc kenh lenh F75 Max.");
                if (isStream && !_handshakeDone && !RunHandshake())
                    return false;
                Log($"[THU NGHIEM - GUI THAT] {title}: {steps.Count} goi.");
                foreach (var s in steps)
                {
                    if (!s.ExpectAck)
                    {
                        _commander.SendBlock(s.Bytes); // pacer 2 ms truoc khoi
                        continue;
                    }
                    var outcome = _commander.SendCommand(s.Bytes);
                    byte opcode = s.Bytes[1];
                    if (outcome == SonixAckOutcome.Ready) continue;
                    if (outcome == SonixAckOutcome.DroppedBusy && opcode == L99Protocol.OpColorStream)
                    {
                        // Header stream ban dai: bo frame nay, stream tiep tuc.
                        LogStreamIssue($"[THU NGHIEM] Header 0x20 van ban sau {SonixCommander.StreamHeaderBusyRetries} lan gui lai — bo frame nay, stream tiep tuc.");
                        return true;
                    }
                    if (outcome == SonixAckOutcome.DroppedBusy && opcode == L99Protocol.OpCommit)
                    {
                        // 0x02 ban dai: da gui lai 2 lan theo ky luat; ghi log va di tiep.
                        Log($"[THU NGHIEM] Lenh 0x02 van ban sau {SonixCommander.EndBusyRetries} lan gui lai — bo qua buoc '{s.Name}', tiep tuc.");
                        continue;
                    }
                    if (isStream)
                    {
                        LogStreamIssue($"[THU NGHIEM] Buoc '{s.Name}' (opcode 0x{opcode:X2}) khong nhan duoc ACK hop le trong ngan sach thoi gian — bo frame nay, stream tiep tuc.");
                        return true;
                    }
                    throw new InvalidOperationException(
                        $"Thiet bi khong ACK buoc '{s.Name}' (opcode 0x{opcode:X2}, ket qua: {outcome}). Dung lai de an toan.");
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
            try { _cmdStream?.Dispose(); } catch { }
            _cmdStream = null;
            _channel = null;
            _commander = null;
            _handshakeDone = false;
        }
    }
}
