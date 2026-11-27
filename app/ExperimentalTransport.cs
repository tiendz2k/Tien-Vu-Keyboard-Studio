using System;

namespace TienKeyboardStudio3
{
    // =====================================================================
    // Transport THUAN LOGIC cho kenh feature-report SONiX (lop thu nghiem
    // F75 Max) — tach khoi HidSharp de unit test bang kenh gia lap duoc.
    //
    // Ky luat o day port theo dung co che dang chay that cua ung dung
    // tham chieu tren CUNG nen tang SONiX 0C45:800A / 0xFF13 (driver ho
    // SONiX cua ho — xem bao cao nghien cuu noi bo): moi lenh gui xong
    // cho 5 ms roi doc ACK; reply toan 0 (firmware dang xu ly) KHONG phai
    // loi — tiep tuc poll theo thang 20/40/80/160/200/200/300 ms; header
    // stream 0x20 bao "ban" (byte[3]=0xFF) thi gui lai toi da 3 lan, qua
    // so lan thi BO frame do va tiep tuc stream (khong nem loi, khong
    // giet stream); lenh ket thuc 0x02 ban thi gui lai sau 35 ms, toi da
    // 2 lan; truoc moi khoi du lieu (khong ACK) co pacer 2 ms; moi thao
    // tac co gioi han ~1600 ms.
    //
    // Khac biet co chu dich so voi tham chieu: reply khong khop opcode
    // (reply tre cua lenh truoc) hoac byte trang thai la (khong phai
    // 0x01/0xFF) duoc coi la "chua san sang" va tiep tuc poll trong ngan
    // sach thoi gian, thay vi bao loi giao thuc ngay — firmware Max chua
    // duoc kiem chung nen uu tien chiu dung.
    // =====================================================================

    /// <summary>Kenh feature-report toi thieu: gui 1 goi 64 byte va doc
    /// thu 1 reply 64 byte (null = chua co reply / timeout doc).</summary>
    public interface IFeatureReportChannel
    {
        void Send(byte[] packet64);
        byte[] TryReceive();
    }

    public enum SonixReplyClass
    {
        NotReady, // reply toan 0, reply tre/sai opcode, byte trang thai la, hoac chua co reply
        Ready,    // reply[1] == opcode va reply[3] == 0x01
        Busy,     // reply[1] == opcode va reply[3] == 0xFF
    }

    public enum SonixAckOutcome
    {
        Ready,      // da nhan ACK ready
        DroppedBusy, // het luot gui lai ma van ban (chi xay ra voi 0x20 / 0x02)
        Failed,     // het thang poll + ngan sach thoi gian ma khong co ACK hop le
    }

    public static class SonixAckClassifier
    {
        /// <summary>Reply "rong": moi byte = 0, tru byte[0] co the = 0x04.
        /// null cung duoc coi la rong (chua co gi de doc).</summary>
        public static bool IsZeroReply(byte[] reply)
        {
            if (reply == null) return true;
            for (int i = 0; i < reply.Length; i++)
            {
                if (i == 0 && reply[i] == 0x04) continue;
                if (reply[i] != 0) return false;
            }
            return true;
        }

        public static SonixReplyClass Classify(byte[] reply, byte opcode)
        {
            if (reply == null || reply.Length < 4) return SonixReplyClass.NotReady;
            if (IsZeroReply(reply)) return SonixReplyClass.NotReady;
            if (reply[1] != opcode) return SonixReplyClass.NotReady;
            if (reply[3] == 0x01) return SonixReplyClass.Ready;
            if (reply[3] == 0xFF) return SonixReplyClass.Busy;
            return SonixReplyClass.NotReady;
        }
    }

    /// <summary>Bo dieu khien lenh SONiX: gui lenh/khoi qua IFeatureReportChannel
    /// theo ky luat ACK cua ung dung tham chieu. Sleep duoc tiem vao
    /// (Thread.Sleep o ban that, no-op ghi lai o test) nen toan bo may
    /// trang thai chay duoc trong unit test khong can phan cung.</summary>
    public sealed class SonixCommander
    {
        public const int PacerMs = 2;                 // cho truoc moi khoi du lieu
        public const int PostSendWaitMs = 5;          // cho sau khi gui lenh, truoc khi doc ACK
        public const int TimeoutMs = 1600;            // ngan sach moi thao tac
        public static readonly int[] ZeroPollLadderMs = { 20, 40, 80, 160, 200, 200, 300 };
        public const int StreamHeaderBusyRetries = 3; // header 0x20: gui lai toi da 3 lan khi ban
        public const int StreamHeaderBusyDelayMs = 20;
        public const int EndBusyRetries = 2;          // lenh 0x02: gui lai toi da 2 lan khi ban
        public const int EndBusyDelayMs = 35;

        private readonly IFeatureReportChannel _channel;
        private readonly Action<int> _sleep;
        private readonly Action<string> _log;

        public SonixCommander(IFeatureReportChannel channel, Action<int> sleep = null, Action<string> log = null)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _sleep = sleep ?? (_ => { });
            _log = log ?? (_ => { });
        }

        /// <summary>Gui mot khoi du lieu khong cho ACK: pacer 2 ms TRUOC goi
        /// (tham chieu cho truoc moi khoi, khong phai sau).</summary>
        public void SendBlock(byte[] packet64)
        {
            _sleep(PacerMs);
            _channel.Send(packet64);
        }

        /// <summary>Gui mot lenh 64 byte va chay ky luat ACK. Khong nem
        /// exception cho truong hop ban/timeout — tra ve outcome de ben
        /// goi quyet dinh (bo frame hay dung).</summary>
        public SonixAckOutcome SendCommand(byte[] packet64)
        {
            if (packet64 == null || packet64.Length < 2)
                throw new ArgumentException("Goi lenh phai co it nhat 2 byte (prefix + opcode).", nameof(packet64));
            byte opcode = packet64[1];
            int maxSends = opcode == L99Protocol.OpColorStream ? 1 + StreamHeaderBusyRetries
                         : opcode == L99Protocol.OpCommit ? 1 + EndBusyRetries
                         : 1;
            int busyDelay = opcode == L99Protocol.OpColorStream ? StreamHeaderBusyDelayMs : EndBusyDelayMs;
            for (int send = 0; send < maxSends; send++)
            {
                if (send > 0)
                {
                    _log($"[SONiX] Opcode 0x{opcode:X2} bao ban — gui lai lan {send}/{maxSends - 1} sau {busyDelay} ms.");
                    _sleep(busyDelay);
                }
                _channel.Send(packet64);
                var cls = PollAck(opcode);
                if (cls == SonixReplyClass.Ready) return SonixAckOutcome.Ready;
                if (cls == SonixReplyClass.Busy) continue;
                return SonixAckOutcome.Failed;
            }
            return SonixAckOutcome.DroppedBusy;
        }

        // Doc ACK sau khi gui: cho 5 ms, doc; khi reply thuoc lop NotReady
        // thi leo thang poll 20/40/80/160/200/200/300 ms doc lai. Tong thoi
        // gian cho bi chan boi TimeoutMs.
        private SonixReplyClass PollAck(byte opcode)
        {
            int elapsed = 0;
            _sleep(PostSendWaitMs);
            elapsed += PostSendWaitMs;
            var cls = SonixAckClassifier.Classify(_channel.TryReceive(), opcode);
            int rung = 0;
            while (cls == SonixReplyClass.NotReady && rung < ZeroPollLadderMs.Length && elapsed < TimeoutMs)
            {
                int d = ZeroPollLadderMs[rung++];
                _sleep(d);
                elapsed += d;
                cls = SonixAckClassifier.Classify(_channel.TryReceive(), opcode);
            }
            return cls;
        }
    }
}
