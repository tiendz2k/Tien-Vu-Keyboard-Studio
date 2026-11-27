# Tien Vu Keyboard Studio — v3.10.0

Ứng dụng điều khiển bàn phím cơ cho Windows.
**AULA F75 Max: hỗ trợ đầy đủ** qua giao thức HID đã kiểm chứng chéo từ hai driver mã nguồn mở độc lập.
Các mẫu khác (AULA F75, Leobog Hi75C Pro, Keychron VIA, Wooting 60HE, RK84, DrunkDeer A75, AULA WIN68, GravaStar K1): app nhận diện được khi cắm vào, cho xem trước giao diện/hiệu ứng — tính năng ghi xuống phím được khoá kèm giải thích cho tới khi driver của mẫu đó được tích hợp (chỉ tích hợp mẫu đã công khai giao thức hoặc đã trích xuất được từ ứng dụng chính hãng). Dòng Akko / MonsGeek / EPOMAKER thuộc nền tảng RongYuan đã có Driver Pack 2 (mục bên dưới). Dòng MCHOSE (Ace 68, Jet 75, G87 V3…) đã có Driver Pack 3. Các họ SONiX anh em (S75 Pro/Nova75, MAD60, HERO68 HE, WIN60 HE/SE) và nền WitMod (Redragon K617 HE) đã có Driver Pack 4. **Driver Pack 5 (v3.9.0, pack cuối của lộ trình)** thêm họ DareU (EK75/EK75 RT và toàn bộ catalog All-in-One của hãng) và các họ AULA Hub còn lại (HFD, SparkLink V1/V2) — mục ngay bên dưới.

Giao diện là bản đã được duyệt ở bản xem trước (Preview v0.9.1 + các bổ sung của Preview v0.10.1): nền tối trung tính sạch, panel kính **Liquid Glass** (panel mờ cho nền xuyên qua, viền sáng ở cạnh trên, bóng mềm), điểm nhấn gradient tím → cyan đổi được 4 màu, font Be Vietnam Pro, logo mới: chữ TV cách điệu trên nền keycap (v3.8).

## Mới ở v3.10.0 — 4 theme màn hình chào mới trong Chợ plugin

Bản này chỉ thêm theme chào (không đổi giao diện, không đổi giao thức):

1. **550C Boot + Thẻ ID** (nâng cấp tại chỗ từ "550C Boot"): boot terminal phosphor hổ phách có scanline → đăng nhập UEG → **THẺ TRUY CẬP cá nhân** gồm tên + mã định danh của bạn, serial tự suy ra từ định danh, và **mã QR THẬT** (QRCoder) chứa nội dung bạn tự nhập trong cấu hình của theme (để trống thì QR chứa tên + mã). Ghi công: Ziyang Song (yannicksong0106) + intro gốc của Voidpocket, MIT.
2. **DeepSeek Harness — Rhine Lab (bản đầy đủ)** (nâng cấp tại chỗ): màn chào cấp quyền **6 hồi** như bản gốc — biển tiêu đề → trắng xoá → thẻ hồ sơ tông ấm + vòng reticle → checklist cấp quyền 5 bước → lộ diện sắc độ → ACCESS GRANTED. Ghi công: 6shenhonghong9, MIT.
3. **MOSS · Ignition** (theme mới): **tái hiện phong cách, vẽ thủ tục 100%** bằng Avalonia — liên kết thần kinh → mắt đỏ khoá mục tiêu → tách đôi → hành tinh gradient + vòng quỹ đạo + 420 hạt lửa → lộ tên app. Không dùng bất kỳ video/hình ảnh/trích đoạn phim nào của bản gốc (video gốc là asset Wallpaper Engine, quyền không rõ). Tiết tấu học từ theme cộng đồng của QIFANAI (MIT).
4. **Hạt Bụi TV** (theme mới): **3.200 hạt** vẽ bằng DrawingContext xoay xoắn ốc rồi **biến hình thành đúng logo Mẫu F** — điểm đích lấy mẫu trực tiếp từ ảnh logo đóng gói trong app, hạt mang màu của logo — rồi lắng xuống và lộ tên app. Ý tưởng hạt biến hình từ INJU Particle Boot của inju0524 (MIT).

- Cả 4 theme chạy trong đúng luồng chào hiện có: plugin đang bật = màn chào khi mở app, click/Esc bỏ qua, công tắc "bỏ qua màn chào" vẫn hiệu lực; thời lượng từng theme do `GreetingSequence` quyết định (5,6–7 giây). Đọc bằng giọng nói vẫn là cấu hình-chỉ-lưu (chưa có TTS) như trước.
- **Khối ghi công (Credits)** ở cuối trang Chợ plugin + mục Giới thiệu trong Cài đặt liệt kê đủ 4 theme bên thứ ba: tác giả + link repo + giấy phép MIT.
- Hai control hoạt hình mới đo thời gian bằng Stopwatch (không đếm tick) nên nhịp theme đúng cả trên máy chậm.
- 552/552 test đạt (487 test cũ + 65 test mới: registry/ghi công theme, ánh xạ style + Resolve, QR payload, serial thẻ, lấy mẫu điểm logo xác định, máy trạng thái trình tự chào). Toàn bộ 21 file giao thức được bảo vệ giữ nguyên từng byte so với v3.9.2. Mọi cảnh báo cũ vẫn nguyên: chưa kiểm chứng trên F75 Max thật, dry-run bật mặc định.

## Mới ở v3.10.0 — sửa đường stream GIF→LED của F75 Max theo cơ chế ứng dụng tham chiếu

Bản này sửa đúng một đường: **stream GIF→LED (lệnh 0x20) của lớp thử nghiệm F75 Max**, sau khi bản v3.9.1 chạy trên máy thật cho thấy đèn phím không phản ứng. Toàn bộ thay đổi dựa trên báo cáo nghiên cứu cơ chế của **ứng dụng tham chiếu trên cùng nền tảng SONiX** (0C45:800A, usage page 0xFF13) — một bản cài đặt thương mại đang chạy thật trên phần cứng cùng họ. **Nói thẳng: cơ chế mới vẫn CHƯA được kiểm chứng trên F75 Max thật — v3.10.0 chính là bản để người dùng kiểm chứng.** Nếu vẫn không sáng, hãy bật Cài đặt → “Hiện nhật ký thao tác”, chạy lại stream và đối chiếu log (log nay ghi rõ từng bước bắt tay/ACK).

- **Bắt tay khi mở phiên stream (mới):** đúng trình tự connect của ứng dụng tham chiếu — gửi **0xF5** (byte[8]=9, bắt buộc ACK “sẵn sàng”) → **đọc 9 khối trạng thái rồi bỏ đi** (không sao lưu/khôi phục — ứng dụng tham chiếu cũng không làm) → gửi **0x02** (bắt buộc sẵn sàng). Chạy một lần khi phiên stream armed bắt đầu; chưa armed thì các bước này chỉ được ghi log như gói sẽ gửi.
- **Kỷ luật ACK viết lại (port từ `SonixCommander` mới, tách thuần logic và có unit test bằng kênh giả lập):** gửi lệnh → chờ **5 ms** → đọc ACK; reply **toàn 0** (firmware đang xử lý) không còn bị coi là lỗi — tiếp tục poll theo thang **20/40/80/160/200/200/300 ms**; header **0x20 báo bận (0xFF)** thì **gửi lại tối đa 3 lần** (cách 20 ms), vẫn bận thì **bỏ frame đó và stream tiếp** — stream không còn chết vì một frame bận; lệnh kết thúc **0x02 bận** thì **gửi lại sau 35 ms, tối đa 2 lần**; **pacer 2 ms trước mỗi khối dữ liệu** (bản cũ: ngủ 10 ms sau mỗi khối); ngân sách mỗi thao tác ~1600 ms. Trước đây bộ chờ ACK ngủ cố định 30 ms, chỉ chấp nhận một dạng ACK và **ném exception huỷ cả stream** ở frame đầu tiên nếu gặp reply toàn 0 hoặc bận — đó là nghi phạm chính của lỗi “bấm phát mà đèn không phản ứng”.
- **Chọn đúng interface 0xFF13:** kênh thử nghiệm nay **xếp hạng các interface theo usage page** đọc từ report descriptor (ưu tiên interface đã xác minh **0xFF13 / usage 1**; HidSharp không đọc được descriptor thì lui về heuristic cũ và **ghi log rõ interface đã chọn** kèm lý do) — thay cho cách cũ lấy interface đầu tiên có feature report ≥ 64 byte, có nguy cơ mở nhầm kênh.
- **Sửa lấy mẫu màu GIF→LED:** chuyển từ lấy **đúng 1 pixel tại tâm** sang **median theo từng kênh trong vùng quanh tâm LED** (±0,35 bề rộng phím / ±0,35 chiều cao hàng) rồi chọn pixel thật gần màu trung vị nhất — đúng hàm lấy mẫu của ứng dụng tham chiếu; thêm **ngưỡng đen blackLevel = 24** (max(R,G,B) ≤ 24 → đen hẳn, hết nhiễu sáng mờ) và hệ số sáng. Đồng thời **sửa lỗi hệ toạ độ**: toạ độ tâm phím trong bảng dữ liệu được chuẩn hoá theo *vùng phím con* của XML driver (left 90..707, top 28..267) nhưng bản cũ nhân thẳng với toàn khung GIF — điểm lấy mẫu bị co về góc trên-trái; bản này chuyển về hệ toàn canvas 900×341 trước khi lấy mẫu.
- **Phạm vi:** chỉ lớp thử nghiệm của F75 Max thay đổi (`ExperimentalDevice.cs`, file mới `ExperimentalTransport.cs`, `GifLedConverter.cs`). Đường ghi 0x23 và feed nhạc 0x78 đi qua cùng transport mới; visualizer nhạc và đèn GameSense dùng chung đường stream nên hưởng lợi tự động. Mọi file giao thức/thiết bị/registry được bảo vệ giữ **byte-identical** với v3.9.1 (đối chiếu SHA-256 trong BUILD-INFO.md). Dry-run vẫn bật mặc định, cổng ba lớp giữ nguyên.

## Mới ở v3.9.1 — bản vá giao diện: ẩn nhật ký thao tác mặc định

- Theo yêu cầu của người dùng, **panel “Nhật ký thao tác” ở Trang chủ và panel “Nhật ký gói tin” ở trang Per-key không còn hiện sẵn** — giao diện gọn hơn, khoảng trống được trả lại cho nội dung chính.
- Muốn xem lại (khi kiểm tra/gỡ lỗi): **Cài đặt → Cài đặt app → “Hiện nhật ký thao tác”**. Công tắc áp dụng **ngay, không cần khởi động lại app**, và được nhớ trong `settings.json`.
- Đây **chỉ là thay đổi hiển thị**: nhật ký vẫn được ghi ngầm bên trong như trước (kể cả log gói tin của Dry-run), nên bật công tắc lên là thấy ngay lịch sử gần đây. Không thay đổi giao thức, tính năng hay cổng an toàn nào.

## Mới ở v3.9.0 — Driver Pack 5 (pack CUỐI): họ DareU + các họ AULA Hub còn lại

- **Trang mới "Họ DareU"** — giao thức trích byte-level từ **web driver chính hãng dr.dareu.com** (190 file JSON cấu hình theo PID; tải được 187 mục: 94 bàn phím, 69 chuột, 24 dongle). Định danh đúng cơ chế của hãng: VID duy nhất `0x260D`, **PID → mục registry → họ giao thức theo FwType**; PID lạ không có trong registry → **chỉ xem, cấm ghi**. (Nói thẳng: **bản cài desktop của DareU chưa bao giờ được kiểm toán** — CDN của hãng trả 403 cho mọi lần tải thử; mọi kết luận ở đây lấy từ bản web chính hãng cùng thế hệ.)
  - **TG / TG-RT (FwType 0/1/6/7):** feature report 64 byte, header 6 byte `[TargetId, Size, Class, Cmd (|0x80 = GET), Profile, ?]`, không checksum, ACK = poll đáp trả 10 ms × 20 tới khi nibble trạng thái == 2. Đủ 14 lớp lệnh đã tài liệu hoá; gán phím 8 byte/phím + BULK_ASSIGN; truyền đa gói 48 byte/gói. **Lớp 13 Magnetic Axis đầy đủ** cho các mẫu Hall-effect (61 mục TG-RT): GLOBAL_KEY_TRAVEL, SINGLE_TRIGGER, RAPID_TRIGGER, DEAD_ZONE, MT/TGL/DKS, SOCD, GAME_MODE + **hiệu chuẩn hành trình** theo đúng luồng của hãng. Tâm điểm **EK75 RT** (6 PID của EK75: 2 bản cơ TG + 4 bản RT): hành trình mã hoá uint16 BE theo bước **0.02 mm**, dải **0.3–3.3 mm**, mặc định 1.5 mm (TravelVersion 1). ⚠️ Lệnh LED FRAME (4) / CUSTOM (8) chỉ có mã lệnh trong enum của hãng — bản web **không có mã gửi** → app không dựng gói stream cho hai lệnh này.
  - **OHID (FwType 4/5):** khung `[0xAA, Len, Cmd (|0x80 khi ghi), Checksum = Σ(byte+index), Data…]` 64 byte; bảng lệnh ORDER/SYNC/KEY/MT/TGL/DKS/trigger/RGB…; bản ghi SINGLE 4 byte/phim (travel LE16), RT 5 byte/phim (LE24); hầu hết lệnh SET phải gọi tiếp **SAVE_USER**. ⚠️ Định dạng bản ghi RGB_KEY/RGB_MATRIX không có trong hồ sơ web → chỉ ghi log, không đoán byte.
  - **JM / QF (FwType 2/3):** report 16 byte (ReportId 8), checksum **nguyên văn từ mã hãng** `s < 55 ? 85 − s : 341 − s` (dạng rút gọn `(0x55 − Σ) mod 256` chỉ là suy luận toán học — app dùng công thức gốc), truyền buffer 10 byte/gói tới địa chỉ BE, count +128 cho gói chưa cuối.
  - Họ phụ **UniHub** (6 mục trong cùng JSON) mới ở mức registry — chưa mổ giao thức, app chỉ hiển thị thông tin.
- **Trang mới "Họ AULA Hub"** — CHỈ các họ chưa có ở các pack trước (không lặp: RLW = WIN60 HE và Bytech HE = HERO68 HE đã ở Pack 4, BPTK/Sinowealth ở Pack 1, nhóm web của F75 Max là lõi app). Registry 85 mục trích từ `devices.json` của AULA Hub; định danh bằng **VID:PID + usage page**:
  - **HFD (49 mục):** khung `[0xAA, cmd, len, addrLE16, hdr, payload@8]` / đáp trả `[0x55, cmd…]`, không checksum trên đường chính (ngoại lệ: lệnh nhạc legacy 53), giao diện phụ **0xFF67** cho khối LED/TFT lớn. Đủ các khối đã tài liệu hoá: thông tin thiết bị 56 byte, keymap/Fn, LED effect 16 byte, per-key 512 byte, macro, RT 1024 byte (giá trị ÷100), DKS, SOCD, dot-matrix 9 byte. **NOVA98: TFT 240×135 RGB565** với bảng trễ 256 byte + header lệnh 80 riêng và đáp trả chờ ở responseCmd 65 (điểm lạ ghi nguyên văn từ mã hãng; tầng thiết bị đã có, bản này chưa nối nguồn khung từ trang GIF). **Dongle 2.4G dùng bộ lệnh riêng** (55/59/60, màu RGB565) — không forward giao thức có dây.
  - **SparkLink V1 "KB2" (6 mục — HERO 68 HE ULTRA/PRO, WIN 68/60 HE PRO/MAX):** khung 64 byte `[0x5C, len, cmd, checksum, payload]`, checksum 1 byte chỉ phụ thuộc byte payload cuối; per-key **bền** (KRGB từng phím + gói lưu key 254), khối PRGB thứ tự BGR, hành trình mm × 1000 qua khối DB. **Không có stream realtime** ở họ này. ACK kiểu FIFO, 1000 ms, tối đa 3 lần.
  - **SparkLink V2 (30 mục — dòng AG, BOX 63…):** khung `[category, sub, payload]` không magic/checksum; đèn nền + palette BGR + per-key bền (chunk 15 ô, flag 255), **stream DirectDrive** (cùng khung, byte[1]=5, fire-and-forget) + **GIF stream** (nhịp `max(1, round(300/speed))` ms), khung trigger 27 byte (mm × 1000), HigherKey (DKS/MPT/MT/TGL/SOCD/RS), **dot-matrix BOX 63** qua category 10.
- **Bảng thiết bị tích hợp nâng lên 331 mẫu** (224 + 47 mẫu DareU + 60 mẫu AULA Hub); file `devices-registry.json` trong repo cũng đã được sinh lại tương ứng.
- Mọi lệnh ghi của Pack 5 đều qua **cổng ba lớp** như các pack trước: đã định danh + công tắc Thử nghiệm bật + tắt Dry-run; chưa đủ điều kiện thì chỉ ghi log gói. **Chưa kiểm chứng trên phần cứng thật** — hãy chạy probe đọc trước và đối chiếu log.

## Mới ở v3.8.0 — Bản GỌN + đổi tên Tien Vu Keyboard Studio

- **Đổi tên app:** “T Keyboard Studio” → **Tien Vu Keyboard Studio** (tiêu đề cửa sổ, màn hình chào, Giới thiệu, metadata file exe: Company/Authors `tiendz2k`, Copyright © 2026 Vũ Minh Tiến). Tên file exe giữ nguyên `TienKeyboardStudio.exe` và thư mục dữ liệu giữ nguyên `%AppData%\TienKeyboardStudio` nên cài đặt/hình nền/cache của bạn không bị mất. **Logo mới** thay cho logo cũ ở icon app, thanh bên và màn hình chào.
- **Chữ ký tác giả + kiểm tra toàn vẹn:** góc dưới cửa sổ luôn có dòng nhỏ **“cre tiendz2k”**; mục Cài đặt → Giới thiệu ghi rõ tác giả và link repo. Lúc build, app nhúng một **bảng băm SHA-256** của các assembly đi kèm (exe apphost + các DLL phụ thuộc, không gồm runtime .NET và không gồm assembly chính — một file không thể tự chứa băm của chính nó); khi mở, app tự đối chiếu các file nằm cạnh exe và hiện cảnh báo ở Trang chủ nếu phát hiện file lệch băm (“Bản này đã bị chỉnh sửa so với bản gốc…”). Lớp này **ngăn sửa đổi thông thường** (hex-edit, thay DLL, đóng gói lại kèm bảng băm cũ) — không phải khoá tuyệt đối trước người cố tình biên dịch lại cả app lẫn bảng băm; bằng chứng gốc bền nhất là lịch sử repo GitHub công khai này.
- **Thư viện Online chuyển sang tải theo nhu cầu (bản cài nhẹ đi ~62 MB):** 16 GIF cộng đồng **không còn đóng gói trong bản cài** nữa. Chúng được lưu trên **Google Drive công khai của dự án**; app chỉ hiển thị ảnh xem trước (thumb PNG nhỏ vẫn đóng gói sẵn), ghi rõ dung lượng từng GIF, và chỉ tải về khi bạn bấm **“Tải về”**. File tải được kiểm tra đúng dung lượng theo catalog rồi lưu vào `%AppData%\TienKeyboardStudio\OnlineLibrary` — tải một lần, dùng mãi, xoá được bằng nút “Xoá bản đã tải”. Các luồng dùng (phát lên đèn phím, nạp TFT) không đổi. Bản Light vì thế chỉ còn khoảng 11–13 MB.
- **Thêm GIF từ link TikTok (Thử nghiệm):** ở trang Màn hình GIF → thẻ “Thêm GIF của bạn”, dán link video TikTok → app phân giải qua dịch vụ công khai tikwm.com, tải video không watermark, chuyển thành GIF (tối đa 12 giây đầu, 15 fps; chọn **dải LED rộng 900px** hoặc **128×128 cho TFT**) rồi lưu vào “Của tôi”. Nói thẳng: TikTok **không có kênh tải chính thức cho bên thứ ba** — đây là đường vòng best-effort, một số video sẽ không lấy được và app sẽ báo lỗi rõ ràng. Việc chuyển đổi cần **ffmpeg**: app tự tìm trong `tools` cạnh exe → `%AppData%\TienKeyboardStudio\Tools` → PATH; chưa có thì bấm nút **“Tải công cụ chuyển đổi”** trong app (tải bản ffmpeg essentials của gyan.dev, ~30 MB, chỉ khi bạn bấm).
- **Sửa ngốn RAM ở lưới GIF:** lưới thư viện GIF nay chỉ đọc **ảnh thu nhỏ PNG đã cache** (`%AppData%\TienKeyboardStudio\ThumbCache`), không giải mã GIF đầy đủ để hiển thị nữa; giải mã đầy đủ chỉ khi bạn mở/phát một GIF.

## Mới ở v3.7.0 — Driver Pack 4: họ SONiX anh em + WitMod + tính năng cộng đồng

- **Trang mới "Họ SONiX (S75/MAD60/HERO)"** — chỉ hiện khi mẫu đang chọn thuộc một trong 5 stack dưới đây; mỗi stack một thẻ riêng, thẻ khác tự ẩn (quy tắc capability). Toàn bộ giao thức trích byte-level từ một ứng dụng driver đã giải phẫu — hồ sơ đầy đủ ở `docs/PROTOCOLS-REFERENCE-APP.md` trong repo GitHub.
  - **Họ B — AULA S75 Pro / Nova75:** cùng nền tảng với F75 Max (0C45:800A, usage page 0xFF13, feature report 64 byte). Probe **0xF5** (đọc, chạy thật ở mọi chế độ) đọc 9 khối trạng thái màu phím; stream = lệnh **0x20** (byte[8]=8) → 8 khối frame `[id,R,G,B]` → lệnh **0x02** commit; ACK: `reply[1]=opcode`, `reply[3]=1` sẵn sàng / `0xFF` bận. ⚠️ **Nói thẳng: probe 0xF5 KHÔNG phân biệt được S75 Pro/Nova75 với chính F75 Max** (chung VID:PID, chung giao thức) — app chỉ gợi ý qua productName (Nova75 phải đúng `AULA-NOVA75`) + mẫu bạn chọn, và stream luôn khoá sau công tắc Thử nghiệm + tắt Dry-run kèm cảnh báo trên trang. **Nova75 stream chỉ ghi log**: app gốc dùng một hàm native tăng tốc (`hid_write_nova_frame`) mà cách chia gói chưa rõ, app này không đoán.
  - **Họ C — MADLIONS MAD60 HE / V2 / RGB** (373B:1120/105D/1054, output report 32 byte, usage page 0xFF60): nhận diện bằng bắt tay — gửi `[1]`, firmware phải trả **protocol v9** mới kết nối được (khác v9 thì từ chối, đúng như app gốc). Stream: sao lưu màu gốc → vào chế độ custom từ gói info `[8,65]` → 10 gói frame (70 LED × RGB) → khôi phục màu gốc.
  - **Họ D — AULA HERO68 HE** (372E:103E, report ID 9, gói 63 byte): checksum ở byte 62 theo quy tắc `(9 + tổng 63 byte) & 0xFF == 0xFF`. Nhận diện bằng chữ ký của lệnh 130 (`[17,0,0,0,0,3]`); chỉ bản có dây — Air/Mini/Pro/Ultra không thuộc giao thức này. Stream gom phím trùng màu (tối đa 52 id/gói) + khôi phục khi xong.
  - **Họ E — AULA WIN60 HE** (2E3C:C365, usage page 0xFF33, report 1): đọc đèn lệnh 7 (12 byte trạng thái, ngữ nghĩa chưa giải mã trong app gốc — giữ nguyên trạng), stream lệnh 23 (bật/tắt) + lệnh 15 (frame lưới 132 ô, 8 gói, pacer 2 ms), heartbeat lệnh 1.
  - **Họ F2 — AULA WIN60 SE** (38A6:291A): giao thức byte phiên ngẫu nhiên. Ngay giao diện của chính nhà sản xuất cũng ghi mẫu này là thử nghiệm/chưa kiểm chứng → app này **chỉ dựng gói + ghi log**, không ghi thật.
- **Trang mới "Họ WitMod (Redragon)"** — nền tảng WitMod (hồ sơ `docs/PROTOCOLS-WITMOD-FULL.md`): khung gói report 1 + 63 byte `[opcode, sub, page big-endian, len, payload]`, **không checksum**. Nhận diện: quét bảng filter **36 cặp VID:PID** (usage page 0xFF1B), probe quyết định là lệnh **0x0D** (chuỗi sản phẩm CSV, trường thứ 5) + khối support của lệnh **0x01**; không qua probe → chỉ xem. Đã tích hợp: đèn chính 0x07 (đọc/ghi), đọc keymap 0x18, đọc trigger/poll-rate 0x21, reset deadband 0x26; builder đầy đủ cho họ lệnh 0x01–0x26 (SOCD 0x24, phím nâng cao 0x22, macro 0x19…) đã có ở tầng giao thức + unit test. Quy tắc phiên theo driver gốc: timeout 300 ms, ACK ghi = echo opcode+sub (gói đầu chờ nghiêm ngặt 100 ms), sau ghi keymap phải poll bit `isStoringData` của lệnh 0x01 tới khi tắt. Mẫu đã biết: **Redragon K617 HE** (app desktop của hãng nhận diện bằng tiền tố productName `7153`, 8 biến thể layout; VID:PID riêng của K617 HE không có trong code web — app không đoán).
- **Nút "Sao chép thông tin thiết bị"** ở trang Tổng quan và mọi trang họ driver: sao chép một đoạn text gồm VID:PID, tên sản phẩm, usage page/usage, kích thước report, kết quả probe (UUID / dev_id / chuỗi 0x0D / tóm tắt 0xF5…), mẫu đang chọn và phiên bản app — dán thẳng vào GitHub issue khi cần hỗ trợ hoặc muốn đóng góp thông tin mẫu phím mới (xem mục "Đóng góp thông tin thiết bị" bên dưới).
- **Cập nhật bảng thiết bị không cần phát hành app mới:** trang Cài đặt → thẻ **Bảng thiết bị cộng đồng** → nút **"Kiểm tra cập nhật bảng thiết bị"**. App chỉ tải khi bạn bấm (không tự gọi mạng nền), kiểm tra định dạng + số mục hợp lệ, lưu vào `%AppData%\TienKeyboardStudio\devices-registry.json` và áp dụng ở lần mở app sau (chỉ **bổ sung** mẫu mới, không ghi đè dữ liệu tích hợp; bảng tích hợp luôn là dự phòng). Định dạng file được mô tả ở mục "Bảng thiết bị (devices-registry.json)" bên dưới; file khởi đầu nằm tại `docs/devices-registry.json` trong repo.

## Bảng thiết bị (devices-registry.json)

File JSON dùng để bổ sung danh sách mẫu phím mà không cần build lại app. Cấu trúc:

```json
{
  "version": 1,
  "updated": "2026-11-27",
  "devices": [
    {
      "name": "Tên mẫu (duy nhất, không trùng)",
      "brand": "Hãng",
      "layout": "75% · 80 phím",
      "family": "sonix_sib",
      "stack": "B",
      "hasScreen": true, "screenW": 128, "screenH": 128,
      "hasHE": false, "hasKnob": true,
      "statusKind": 1,
      "protocolStatus": "Mô tả trạng thái giao thức",
      "idNote": "Ghi chú cách nhận diện",
      "devIds": [],
      "ids": [[3141, 32778]]
    }
  ]
}
```

- `ids`: mảng các cặp `[vid, pid]` dạng **số thập phân** (pid = `-1` nghĩa là khớp mọi PID của VID đó). Mục để `ids` rỗng là mục chỉ hiển thị (chưa biết VID:PID).
- `family`: `kb_by_v2` | `rongyuan` | `mchose` | `sonix_sib` | `witmod` | `dareu` | `aulahub` | để trống. `stack`: stack trong họ (ví dụ `B/C/D/E/F2` của họ SONiX, `GLW/QHW` của MCHOSE, `TG/TG-RT/OHID/JM/QF` của DareU, `HFD/SLV1/SLV2` của AULA Hub).
- App từ chối file nếu: không đọc được JSON, số mục ngoài khoảng 10–5000, trùng tên, hoặc VID/PID ngoài 0–0xFFFF.
- Bảng tải về chỉ **bổ sung** mẫu chưa có trong app; mẫu đã tích hợp sẵn không bị ghi đè.

## Đóng góp thông tin thiết bị (cho cộng đồng)

Bạn có một mẫu phím chưa được hỗ trợ (hoặc hỗ trợ chưa đầy đủ)? Hãy giúp cộng đồng bằng cách:

1. Cắm bàn phím bằng **USB có dây**, mở app, bấm **Quét bàn phím** ở trang Tổng quan.
2. Nếu mẫu thuộc một họ driver đã có trang riêng (Sinowealth, RongYuan, MCHOSE, SONiX, WitMod), mở trang họ đó và chạy **Nhận diện / Probe** (các probe đều là lệnh đọc, an toàn).
3. Bấm nút **"Sao chép thông tin thiết bị"** (có ở trang Tổng quan và mọi trang họ).
4. Mở một **GitHub issue** trong repo của dự án, dán đoạn vừa sao chép vào, kèm: tên mẫu chính xác, link sản phẩm (nếu có), và ghi rõ probe nào chạy được / không chạy được.

Thông tin này giúp xác định VID:PID và định danh thật của mẫu để đưa vào bảng thiết bị hoặc viết driver ở bản sau. **Không** gửi kèm bất kỳ thông tin cá nhân nào — đoạn sao chép chỉ chứa thông tin phần cứng của bàn phím.

## Mới ở v3.4.0 — Driver Pack 1: họ Sinowealth kb_by_v2

- **Trang mới "Họ Sinowealth (kb_by_v2)"** — chỉ hiện khi mẫu đang chọn thuộc họ này: AULA F75, Leobog Hi75 / Hi75C Pro, YUNZII C75 Cake Meow, YUNZII C98 Cake Meow (ANSI / ISO-DE / ISO-FR), GravaStar Mercury K1. Các mẫu này **không có màn hình** nên trang Màn hình GIF / Widget TFT vẫn ẩn theo quy tắc capability như cũ; không mẫu nào có Hall-effect.
- **Nhận diện bằng định danh đọc từ thiết bị (lệnh 0x82) là BẮT BUỘC trước khi ghi.** VID:PID 258A:010C bị dùng chung bởi nhiều board khác nhau, nên app đọc UUID/chữ ký từ chính bàn phím và đối chiếu registry (YUNZII C75/C98 theo UUID; F75/K1 theo chữ ký của app tham chiếu). **Không khớp hoặc không đọc được → chỉ xem, khoá ghi.** Leobog Hi75/Hi75C Pro chưa có UUID trong tài liệu đã trích xuất nên hiện **chỉ xem**.
- **Lệnh đã tích hợp** (giao thức kb_by_v2 có dây: feature report 6, gói 519 byte): đọc/ghi basic info (0x84/0x04), đọc/ghi keymap từng lớp (0x83/0x03, trailer 5A A5, chờ 100 ms sau khi ghi), đèn từng phím 3 mặt phẳng RGB (0x86/0x06), màu theo mode (0x8A/0x0A), macro theo khối 512 byte (0x05), đọc pin (0x87), khôi phục cài đặt gốc (0x11). ACK = thiết bị echo lại 7 byte header.
- **Khoá ghi 3 lớp** như lớp thử nghiệm của F75 Max: đã định danh khớp registry + công tắc Thử nghiệm BẬT + Dry-run TẮT. Ngoài ra chỉ ghi log gói sẽ gửi.
- **Đường dongle 2.4G** (gói 19 byte, checksum (19 + tổng) % 256, send_batch manh 14 byte): đã có builder + unit test, nhưng bản này **chỉ ghi log gói — chưa mở transport thật lên dongle**.
- ⚠️ **Chưa kiểm chứng trên phần cứng thật**: không ai trong đội sở hữu các mẫu phím này; toàn bộ giao thức lấy từ driver chính hãng đã giải phẫu. Hãy thử đọc (0x82/0x84/0x87) trước, và chỉ mở khoá ghi khi đã định danh đúng mẫu của mình.

## Mới ở v3.6.0 — Driver Pack 3: MCHOSE (4 stack)

- **Trang mới "Họ MCHOSE"** — chỉ hiện khi mẫu đang chọn là MCHOSE họ CZ: **31 cấu hình** trích từ M HUB chính hãng (28 stack GLW + 3 stack QHW). **Không model MCHOSE nào có màn hình** nên trang Màn hình GIF / Widget TFT luôn ẩn với họ này; mục Hall-effect chỉ hiện với stack GLW, mục QHW chỉ hiện với 3 model V3.
- **Stack GLW/CZ** (dòng Ace 68/V2/GT/Air, Jet 75…): kênh vendor usagePage 0xFF00, gói 64 byte `[0x55, cmd, 0, checksum, len, offset LE16, 0, payload ≤ 56B]`, đáp trả 0xAA khớp cmd/offset. Đã tích hợp: đọc FW (cmd 3), thứ tự profile (4), FuncConfig (5 — gồm % pin), vùng trigger Hall-effect (160/161: 128 phím × 8 byte, hành trình/RT 9-bit lưu −1, vùng chết 7-bit/phim; builder DKS 162/163, Mod-Tap 164/165, Toggle 166/167 đã có ở mức giao thức), hiệu chuẩn (168/169/177), khôi phục cài đặt gốc (238). Giá trị hành trình hiển thị ở **đơn vị thô** — bảng quy đổi sang mm nằm ở tầng giao diện của hãng và chưa thu hồi được.
- **⚠️ Checksum GLW là GIẢ THUYẾT sum-mod-256** — hàm checksum gốc của SDK nằm trong một module không có trong các file đã giải phẫu; bản này cài đặt giả thuyết tổng-byte-mod-256 và ghi rõ ở trang họ lẫn BUILD-INFO. Nếu sai, firmware từ chối mọi gói GLW (kể cả đọc) — hãy thử Nhận diện/Đọc trước; ghi vẫn khoá 3 lớp như các pack khác.
- **Stack QHW** (G87 V3 / G98 V3 / K87 V3): usagePage 0xFF80, report ID 77, opcode 16-bit (đọc 0x02xx / ghi 0x03xx / điều khiển 0x09xx), checksum XOR. Đã tích hợp: getBaseInfo (0x0900), FW (0x0901), pin (0x0904), màu từng phím realtime (0x0306), khôi phục gốc (0x030C). Kênh QHW **không có lệnh Hall-effect nào** trong tài liệu đã trích. "GIF" của QHW là **hiệu ứng LED toàn phím** (khung delta + CRC32, tối đa 36.800 byte) — encoder + meta đã có và qua unit test, phần gửi nội dung theo mảnh mới chỉ ghi log vì định dạng offset từng gói chưa tái dựng dứt khoát.
- **Stack BY dùng chung Driver Pack 1 (kb_by_v2)** — 18 model MCHOSE BY (G87, K99, G98, X75…) đăng ký vào cùng họ Sinowealth. Hãng phân biệt các model này bằng productName USB (8 model dùng chung 258A:010C) và registry kb_by_v2 không có UUID/chữ ký cho chúng, nên tất cả **chỉ xem** — giống Leobog, app không suy đoán.
- **Stack legacy (0x11/0x12, XOR 0xFF)**: chỉ có builder + giải mã đã qua unit test; transport chưa mở — trang họ chỉ cho xem trước gói (ghi log), ghi rõ dry-run-only.
- **Định danh trước khi ghi** như các pack trước: GLW probe getInfo (cmd 3), QHW probe getBaseInfo (0x0900) đối chiếu VID/PID thiết bị tự khai với registry. Khoá ghi 3 lớp: định danh khớp + công tắc Thử nghiệm BẬT + Dry-run TẮT; ngoài ra chỉ ghi log gói.
- ⚠️ **Chưa kiểm chứng trên phần cứng thật**: không ai trong đội sở hữu phím MCHOSE; toàn bộ giao thức lấy từ app chính hãng đã giải phẫu. Thử Nhận diện → Đọc trước khi nghĩ tới ghi.

## Mới ở v3.5.0 — Driver Pack 2: nền tảng RongYuan (Akko · MonsGeek · EPOMAKER)

- **Trang mới "Họ RongYuan"** — chỉ hiện khi mẫu đang chọn thuộc nền tảng này. Cơ sở dữ liệu mẫu giờ gồm **246 mục** trích từ driver chính hãng Akko Cloud v4, MonsGeek V4 và EPOMAKER Driver (cả ba là cùng một nền tảng OEM RongYuan): cờ khả năng (Hall-effect, màn hình + kích thước, núm xoay) lấy từ hồ sơ giải phẫu của từng driver. Màn hình tuỳ mẫu: EPOMAKER RT100 240×173 RGB565, Akko MOD007 V5 HE 128×128, 5098B 172×320, 5098 V5 420×116, KF105/KF106 240×240.
- **Nhận diện bằng dev_id (lệnh 0x8F, 32-bit LE) là BẮT BUỘC trước khi ghi.** VID 0x3151 / PID 0x5030 bị dùng chung cho gần như toàn bộ dòng RY5088 của cả Akko lẫn MonsGeek. App đọc dev_id từ chính thiết bị và đối chiếu registry: hiện chỉ có **52 mục MonsGeek có sẵn dev_id đã kiểm chứng** (ví dụ FUN60 Ultra TMR = 3853). **Mục nào chưa có dev_id đã kiểm chứng — toàn bộ EPOMAKER và phần lớn Akko — luôn ở chế độ CHỈ XEM**, app không suy đoán. Giao thức: feature report 64 byte, byte 0 = opcode, GET = SET + 0x80, checksum Bit7 ở byte 7 (và Bit8 ở byte 8 riêng cho lệnh LED), nhịp 10 ms.
- **Lệnh đã tích hợp:** đổi profile (4), keymap theo mảnh 56 byte (10), macro (11), đèn từng phím phẳng (12), đèn chính (7), danh sách tính năng (230), xoá flash (44, ACK magic AA AA 55 55). **Hall-effect** đi qua lệnh gộp 101/229 với đủ sub-op đã tài liệu hoá: điểm kích hoạt/nhả, Rapid Trigger, DKS, Mod-Tap (lượng tử 10 ms), vùng chết trên/dưới, mode byte (bit7 = Rapid Trigger, 7 = Snap/SOCD), cặp snap, loại switch, đọc realtime 254/255; quy đổi mm ×{10, 100, 200, 1000} theo lớp firmware; dải 0.1–3.3 mm (Ultra/TMR 3.4 mm). **Hiệu chuẩn** có wizard 3 bước theo đúng luồng driver gốc (bật báo cáo 27 → mốc MIN 28 → mốc MAX 30, chỉ khi cắm dây). **Màn hình:** chuẩn bị 165 (RGB565) / 169 (RGB888) → dữ liệu 37/41 theo mảnh 56 byte, nhịp ~3 ms, kèm đồng bộ đồng hồ (40).
- **Khoá ghi 3 lớp** giống Pack 1: đã định danh dev_id khớp registry + công tắc Thử nghiệm BẬT + Dry-run TẮT. Ngoài ra chỉ ghi log gói sẽ gửi; chưa định danh thì app từ chối hẳn.
- ⚠️ **Chưa kiểm chứng trên phần cứng thật** và hai điểm cần nói thẳng: (1) registry trích từ driver gốc cho thấy nhiều mẫu chia sẻ cùng VID:PID nên việc định danh dev_id là không thể bỏ qua; (2) thứ tự quét pixel và chiều byte RGB565 của đường nạp màn hình chưa được tái dựng dứt khoát trong hồ sơ gốc — bản này đóng gói RGB565 big-endian theo hàng (row-major), ảnh nạp lên có thể lệch/xoay trên phím thật cho tới khi kiểm chứng bằng thiết bị thật.

## Mới ở v3.3.0 (các bổ sung đã duyệt ở Preview v0.10.1 — đều là tính năng thật)

- **Liquid Glass + hình nền app thật** — trang Cài đặt → thẻ **Giao diện**: nền Mặc định + 4 skin có sẵn (Cực quang, Hoàng hôn, Rừng sâu, Vũ trụ sâu) + **Ảnh của bạn** (chọn ảnh bằng hộp chọn file thật; ảnh được nén về cạnh dài nhất 1920px và lưu trong `%AppData%\TienKeyboardStudio\Backgrounds`). 4 màu chủ đề đổi trực tiếp. Chế độ **Sáng**: chưa làm — thẻ Sáng ghi rõ "sắp có ở bản sau".
- **Màn hình chào thật** — khi mở app: logo + vệt sáng LED quét qua, bấm chuột hoặc Esc để bỏ qua; có công tắc "Bỏ qua màn hình chào mừng" và nút xem lại trong thẻ Giao diện.
- **Chợ plugin** — trang mới với thẻ bìa 2 cột, tab danh mục, panel chi tiết (bật/tắt + cấu hình + xem trước). 3 plugin khởi động (DeepSeek Harness, 550C Boot, Tien Galaxy Boot) là **phong cách màn hình chào thật**: bật plugin nào thì lần mở app sau dùng phong cách đó. Hai mục **Wallpaper Engine Sync** và **Aurora Wave Pack** là **ý tưởng đang phát triển** — khoá bật, không có chức năng, không gọi mạng.
- **GameSense (CS2) thật** — trang mới: app nghe Game State Integration của CS2 trên **127.0.0.1:13650** (đổi cổng được), có nút **tạo file cfg** thả thẳng vào thư mục `game\csgo\cfg` của CS2 (tự tìm qua Steam). Ánh xạ sự kiện → hành động: chết → mở trang web / chạy chương trình / nháy đèn phím; bị flashbang → lớp phủ chống loá toàn màn hình (đen/xám/ảnh của bạn + độ mờ) hoặc làm tối đèn; thắng round / đặt bom → nháy đèn ăn mừng. Phần đèn đi qua đúng lớp thử nghiệm (cùng khoá Thử nghiệm + Dry-run như per-key). Chưa kiểm chứng với CS2 thật — xem BUILD-INFO.

## Hai bản build

| Bản | File | Yêu cầu |
|---|---|---|
| **Light** | `TienKeyboardStudio.exe` trong `T-Keyboard-Studio-v3.5-Light.zip` | Cần **.NET 8 Runtime** (Microsoft.NETCore.App 8.0) x64 đã cài trên máy |
| **Compressed** | `TienKeyboardStudio.exe` trong `T-Keyboard-Studio-v3.5-Compressed.zip` | Tự chứa toàn bộ — **không cần cài gì thêm** |

Giải nén zip ra một thư mục (giữ nguyên hai thư mục `Library` và `OnlineLibrary` nằm cạnh file exe — `OnlineLibrary` giờ chỉ chứa ảnh xem trước nhỏ), rồi chạy `TienKeyboardStudio.exe`. Windows có thể hiện SmartScreen vì app không ký số — chọn *More info → Run anyway*.

## Nguyên tắc an toàn

- **Dry-run BẬT mặc định.** Khi Dry-run bật, mọi thao tác ghi chỉ **ghi log** đúng các gói sẽ gửi — không có gì được ghi xuống phím. Công tắc Dry-run nằm ở thanh bên trái và ở trang Cài đặt.
- **Không telemetry.** App không tự gọi mạng, ngoại trừ đúng các việc bạn chủ động bật/bấm: tải GIF từ link bạn tự dán ở trang Màn hình GIF, tải thời tiết khi mở trang Widget, server điều khiển điện thoại trong LAN khi bạn bật, và cổng nghe GameSense CS2 — cổng này chỉ **nghe trên 127.0.0.1** (máy của bạn tự gửi cho app), không kết nối ra ngoài. Chợ plugin **không gọi mạng**; Thư viện Online hiển thị sẵn offline nhưng GIF gốc chỉ tải khi bạn bấm “Tải về” (từ Google Drive công khai của dự án); thêm GIF từ TikTok và tải ffmpeg cũng chỉ chạy khi bạn bấm.
- Các tính năng ghi thật cho F75 Max (RGB, đồng hồ, cài đặt 04 17, nạp GIF TFT) đều qua **cáp USB-C**. Bluetooth/đầu thu 2.4G không nhận cài đặt và không nạp được màn hình.
- Lớp per-key/stream/nhạc là **THỬ NGHIỆM** có khoá riêng — đọc kỹ mục riêng bên dưới trước khi mở khoá.

## LỘ TRÌNH AN TOÀN CHO LẦN CHẠY ĐẦU TIÊN (làm đúng thứ tự này)

1. **Cắm F75 Max bằng cáp USB-C.** Đóng app chính hãng AULA và app của shop nếu đang mở (tránh tranh kênh HID).
2. Mở app → trang **Tổng quan** → bấm **Quét bàn phím**. App liệt kê thiết bị HID thật; F75 Max hiện thẻ "Đang sử dụng".
3. Sang trang **Ánh sáng RGB**, chọn một hiệu ứng, bấm **Áp dụng xuống phím** — đang Dry-run nên app chỉ ghi log (xem ở Nhật ký thao tác, trang Tổng quan). Kiểm tra log thấy đủ 5 bước: mở phiên → chọn hiệu ứng → khối thông số → áp dụng → đóng phiên.
4. **Tắt Dry-run** ở thanh bên trái (app sẽ cảnh báo "ĐANG GHI THẬT").
5. Bấm **Áp dụng xuống phím** lại — lần này đèn phím đổi thật. Thử thêm đổi màu, độ sáng, tốc độ, hướng.
6. Trang **Đồng hồ & Cài đặt**: bấm **Đồng bộ đồng hồ** (gửi giờ máy tính xuống màn hình phím). Sau đó thử khối **Cài đặt thiết bị (04 17)**: chỉnh vài công tắc (ví dụ Khoá phím Windows), bấm **Gửi cài đặt xuống phím**.
7. Trang **Màn hình GIF**: chọn **một GIF nhỏ** trong thư viện, bấm **Nạp lên phím**, chờ thanh tiến trình chạy hết — GIF hiện trên màn hình TFT của phím.
8. Trang **Per-key (thử nghiệm)**: bấm **Đọc thử màu phím (0xF5)** — thao tác **chỉ đọc**, chạy live kể cả khi Dry-run bật. Nếu màu đọc về hiện đúng lên hình vẽ bàn phím (khớp đèn thật) thì kênh per-key của F75 Max đã thông — đó là mốc kiểm chứng quan trọng nhất của lớp thử nghiệm.
9. Chỉ sau bước 8 mới cân nhắc mở khoá lớp thử nghiệm (xem mục dưới).

Nếu có gì lạ (đèn không đổi, app báo không ACK): cắm lại cáp, đóng mọi app khác đang mở cùng phím, và bật lại Dry-run. Khôi phục cài đặt gốc của phím: giữ **Fn+Esc khoảng 5 giây**.

## Các trang chính

- **Tổng quan** — quét thiết bị thật (HID), chọn mẫu phím, 3 thẻ trạng thái, nhật ký thao tác.
- **Ánh sáng RGB** — 20 hiệu ứng firmware, màu chính/nhiều màu, độ sáng/tốc độ/hướng, áp dụng qua khối 04 13 đã kiểm chứng.
- **Theo nhạc** — bắt âm thanh hệ thống thật qua WASAPI loopback (NAudio) → FFT 23 dải phổ. Ba chế độ gửi: *Visualizer stream 0x20* (thử nghiệm), *Nhịp firmware* (pulse độ sáng theo nhịp bass bằng hiệu ứng 04 13 đã kiểm chứng — chạy live theo Dry-run thường), *Feed phổ 0x78* (thử nghiệm). Không bắt được âm thanh thì app tự dùng phổ mô phỏng để xem trước.
- **GameSense** — (mới ở v3.3.0) nghe sự kiện CS2 qua GSI trên localhost, ánh xạ sang hành động trên máy + đèn phím; có nút tạo file cfg cho CS2. Xem mục "Mới ở v3.3.0" ở đầu trang.
- **Màn hình GIF** — 21 GIF tích hợp + GIF của bạn (thêm thư mục nguồn, tải từ URL, kéo-thả vào cửa sổ app — tự chuẩn hoá 128×128, tối đa 255 frame, lưu vào `%AppData%\TienKeyboardStudio\MyGIFs`), 3 chế độ Fill/Fit/Stretch, nạp lên TFT qua kênh màn hình đã kiểm chứng. **Trang này chỉ hiện khi mẫu đang chọn có màn hình** (F75 Max có; F75 thường/Hi75C Pro không — trang tự ẩn).
- **Thư viện Online** — 16 hiệu ứng cộng đồng lưu trên **Google Drive công khai của dự án** (v3.8): app hiển thị ảnh xem trước + dung lượng, bấm **“Tải về”** mới tải GIF gốc về cache trong máy (kiểm tra đúng dung lượng), sau đó dùng ngay offline: phát lên **đèn từng phím** (GIF→LED) hoặc nạp lên **màn hình TFT**. GIF đã tải cũng xuất hiện ở trang Màn hình GIF trong nhóm "Thư viện Online".
- **Per-key (thử nghiệm)** — xem mục riêng bên dưới. Chỉ hiện khi đang chọn F75 Max.
- **Họ Sinowealth / Họ RongYuan / Họ MCHOSE** — trang riêng cho Driver Pack 1/2/3, chỉ hiện khi mẫu đang chọn thuộc họ tương ứng (xem các mục "Mới ở…" tương ứng).
- **Họ SONiX (S75/MAD60/HERO)** — (mới ở v3.7.0) trang Driver Pack 4 cho 5 stack B/C/D/E/F2, chỉ hiện khi mẫu đang chọn thuộc họ SONiX anh em.
- **Họ WitMod (Redragon)** — (mới ở v3.7.0) trang Driver Pack 4 cho nền WitMod, chỉ hiện khi mẫu đang chọn thuộc nền này.
- **Họ DareU** — (mới ở v3.9.0) trang Driver Pack 5: nhận diện bằng registry PID, thẻ Magnetic Axis chỉ hiện với mẫu TG-RT, thẻ OHID/JM chỉ hiện với đúng họ của mẫu.
- **Họ AULA Hub** — (mới ở v3.9.0) trang Driver Pack 5 cho HFD / SparkLink V1 / SparkLink V2, mỗi họ một thẻ riêng tự ẩn hiện theo mẫu đang chọn.
- **Chợ plugin** — (mới ở v3.3.0) kho plugin cộng đồng: 3 phong cách màn hình chào dùng thật + 2 ý tưởng đang phát triển (khoá, ghi rõ nhãn). Bật plugin khởi động nào thì màn hình chào lần mở sau dùng phong cách đó; nút Xem trước mở đúng màn hình chào thật.
- **Widget TFT** — widget vẽ trên máy rồi nạp lên màn hình phím: **Thông số máy** (CPU/RAM thật + GPU NVIDIA qua nvidia-smi nếu có), **Đồng hồ & thời tiết** (giờ thật + thời tiết Hà Nội từ Open-Meteo, không cần API key), **Đang phát** (đang phát triển — chưa tích hợp SMTC, xem BUILD-INFO). Có nút đẩy thủ công và tự động đẩy mỗi 5 giây. Trang này cũng chỉ hiện khi mẫu có màn hình.
- **Đồng hồ & Cài đặt** — thẻ **Giao diện** mới ở v3.3.0 (hình nền app + ảnh của bạn, 4 màu chủ đề, chế độ Tối/Sáng, màn hình chào: xem lại + bỏ qua); đồng bộ đồng hồ; khối cài đặt thiết bị 04 17 (khoá Win, khoá Alt+F4, khoá Alt+Tab, chuyển Fn, giờ tự ngủ, độ phản hồi phím 1–5); cài đặt app (Dry-run, khởi động cùng Windows, thu nhỏ xuống khay); và 3 tính năng Stage 2 bên dưới.
- **Điều khiển từ điện thoại** — bật server LAN ngay trong app: hiện **mã QR thật** + PIN 4 số (PIN nằm sẵn trong link QR). Điện thoại cùng Wi-Fi quét QR → mở trang điều khiển: chọn nhanh hiệu ứng, nạp GIF, đẩy widget. Mọi lệnh từ điện thoại đều đi qua PC và **tôn trọng Dry-run** như thao tác trên PC. Server tắt mặc định; Windows Firewall có thể hỏi lần đầu — cho phép trong mạng Private/Home.

### Stage 2 (đã có trong bản này, ở trang Cài đặt)

- **Hồ sơ theo ứng dụng** — bật theo dõi cửa sổ đang active; gán cho mỗi app một hiệu ứng RGB (+ GIF TFT) và app tự áp khi bạn chuyển sang app đó.
- **Macro trên máy** — ghi/phát chuỗi phím bằng hook + SendInput, lưu JSON tại máy; phím tắt toàn cục **Ctrl+Alt+R** (ghi/dừng) và **Ctrl+Alt+P** (phát). Macro chạy trên máy, không ghi xuống bộ nhớ phím.
- **Thống kê gõ phím** — chỉ **đếm** số lần nhấn theo vị trí phím (heatmap + WPM). Không ghi nội dung, không ghi chuỗi phím, không lưu lại khi tắt app.

## ⚠️ MỤC THỬ NGHIỆM — per-key / stream / nhạc (đọc kỹ trước khi mở khoá)

Các lệnh **0x23** (ghi màu từng phím), **0x20** (stream đèn theo thời gian thực ~17fps) và **0x78** (feed phổ nhạc) được tài liệu hoá công khai bởi dự án **AULA L99** của Gavin Graham (github.com/Revisory676/aula-l99-linux). L99 dùng **chung nền tảng SONiX 0C45:800A** với F75 Max: trùng VID:PID khi cắm dây, trùng khung feature report 64 byte và trùng khối hiệu ứng 04 13. Các builder trong app được port nguyên văn logic của `protocol.py` và đã **kiểm chứng byte-for-byte bằng unit test** (100 test gồm cả logic mới của v3.3 — phát hiện sự kiện GSI, tạo file cfg, chọn phong cách chào, nén ảnh nền — đạt 100%, xem `tests/`).

Tuy vậy: **chưa ai kiểm chứng các lệnh này trên F75 Max thật.** Vì thế app khoá chúng như sau:

- **Đọc 0xF5** (đọc lại màu từng phím): chỉ đọc, được chạy live bằng nút riêng — đây là bước kiểm chứng an toàn đầu tiên bạn nên làm (bước 8 ở lộ trình trên).
- **Mọi lệnh ghi (0x23/0x20/0x78)**: mặc định **chỉ ghi log** đúng các gói sẽ gửi. Muốn gửi thật phải **đồng thời**: (1) bật công tắc **Thử nghiệm** ở trang Per-key, và (2) **tắt Dry-run** toàn cục. Thiếu một trong hai là không có gì được gửi.
- Ghi 0x23 có thể ghi vào bộ nhớ bền của phím — đừng gửi lặp lại liên tục. Trước khi thử gửi thật: đóng app chính hãng/app shop, cắm cáp USB-C, và nhớ cách khôi phục gốc **Fn+Esc ~5 giây**.
- Lưu ý dữ liệu: trong file layout chính hãng, **Shift_R và Alt_R cùng mang light_index 0x54** (gần như chắc chắn là lỗi dữ liệu của hãng) — khi gửi màu per-key/stream, hai phím này nhận cùng một màu cho tới khi kiểm chứng được trên phím thật.
- Thứ tự stream 0x20 dùng giả thuyết đã tài liệu hoá: sắp xếp các rect phím theo (trên → dưới, trái → phải) từ file layout chính hãng — cùng phương pháp Graham đã kiểm chứng khớp trên L99.

## Khắc phục sự cố nhanh

- **Quét không thấy phím**: dùng cáp USB-C (không qua hub), thử cổng USB khác; Bluetooth/2.4G không đủ cho các tính năng ghi.
- **Báo không ACK / nạp GIF dở dang**: đóng app AULA chính hãng và app của shop, rút cáp cắm lại, thử lại khi Dry-run để xem log.
- **App báo đang bận**: một lệnh trước đang chạy — đợi vài giây.
- **Điện thoại không mở được trang điều khiển**: kiểm tra cùng Wi-Fi, server còn bật, và Windows Firewall đã cho phép app trong mạng Private.
- **Bản Light không mở được**: máy chưa có .NET 8 Runtime — cài Microsoft.NETCore.App 8.0 x64, hoặc dùng bản Compressed.

## Tín nhiệm / nguồn giao thức

- Giao thức F75 Max đã kiểm chứng (04 13 / 04 28 / kênh màn hình 0xFF68 / khối 04 17): đối chiếu từ hai driver mã nguồn mở độc lập của mohin7 (AULA Studio) và VitalyArt (Aula-F75-Max-Driver).
- Giao thức per-key/stream/nhạc (lớp thử nghiệm): Gavin Graham — dự án AULA L99 (MIT).
- Driver Pack 1 (kb_by_v2): giải phẫu driver chính hãng đã tài liệu hoá trong kho tài liệu của dự án.
- Driver Pack 2 (RongYuan): giải phẫu ba driver chính hãng Akko Cloud v4, MonsGeek V4 và EPOMAKER Driver (cùng nền tảng OEM) — registry 246 mục và bảng lệnh được trích trực tiếp từ mã nguồn web của các driver này.
- Hiệu ứng ở Thư viện Online: thư viện hiệu ứng cộng đồng.
- Font Be Vietnam Pro: SIL Open Font License 1.1.
- Bàn phím trong app là hình vẽ vector tự vẽ — không dùng tài sản của AULA.
