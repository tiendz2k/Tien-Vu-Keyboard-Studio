# T Keyboard Studio — v3.2.2

Ứng dụng điều khiển bàn phím cơ cho Windows, làm riêng cho Tiến.
**AULA F75 Max: hỗ trợ đầy đủ** qua giao thức HID đã kiểm chứng chéo từ hai driver mã nguồn mở độc lập.
Các mẫu khác (AULA F75, Leobog Hi75C Pro, Keychron VIA, Wooting 60HE, MADLIONS MAD60 HE, RK84, DrunkDeer A75, AULA S75 Pro/Nova75/WIN60/WIN68/HERO68, Akko 5075B, GravaStar K1, MonsGeek M1): app nhận diện được khi cắm vào, cho xem trước giao diện/hiệu ứng — tính năng ghi xuống phím được khoá kèm giải thích cho tới khi driver của mẫu đó được tích hợp (chỉ tích hợp mẫu đã công khai giao thức).

Giao diện là bản đã được duyệt ở bản xem trước (Preview v0.9.1): nền tối trung tính sạch (không nền vũ trụ), panel kính, điểm nhấn gradient tím → cyan, font Be Vietnam Pro, logo chữ T từ phím spacebar.

## Hai bản build

| Bản | File | Yêu cầu |
|---|---|---|
| **Light** | `TienKeyboardStudio.exe` trong `T-Keyboard-Studio-v3.2.2-Light.zip` | Cần **.NET 8 Runtime** (Microsoft.NETCore.App 8.0) x64 đã cài trên máy |
| **Compressed** | `TienKeyboardStudio.exe` trong `T-Keyboard-Studio-v3.2.2-Compressed.zip` | Tự chứa toàn bộ — **không cần cài gì thêm** |

Giải nén zip ra một thư mục (giữ nguyên hai thư mục `Library` và `OnlineLibrary` nằm cạnh file exe), rồi chạy `TienKeyboardStudio.exe`. Windows có thể hiện SmartScreen vì app không ký số — chọn *More info → Run anyway*.

## Nguyên tắc an toàn

- **Dry-run BẬT mặc định.** Khi Dry-run bật, mọi thao tác ghi chỉ **ghi log** đúng các gói sẽ gửi — không có gì được ghi xuống phím. Công tắc Dry-run nằm ở thanh bên trái và ở trang Cài đặt.
- **Không telemetry.** App không tự gọi mạng, ngoại trừ đúng 3 việc bạn chủ động bấm: tải GIF từ link bạn tự dán ở trang Màn hình GIF, tải thời tiết khi mở trang Widget, và server điều khiển điện thoại trong LAN khi bạn bật. Thư viện Online dùng hoàn toàn offline (GIF đóng gói sẵn trong máy).
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
- **Màn hình GIF** — 21 GIF tích hợp + GIF của bạn (thêm thư mục nguồn, tải từ URL, kéo-thả vào cửa sổ app — tự chuẩn hoá 128×128, tối đa 255 frame, lưu vào `%AppData%\TienKeyboardStudio\MyGIFs`), 3 chế độ Fill/Fit/Stretch, nạp lên TFT qua kênh màn hình đã kiểm chứng. **Trang này chỉ hiện khi mẫu đang chọn có màn hình** (F75 Max có; F75 thường/Hi75C Pro không — trang tự ẩn).
- **Thư viện Online** — 16 hiệu ứng cộng đồng **đóng gói sẵn** trong thư mục `OnlineLibrary` cạnh exe (từ v3.2.2): dùng hoàn toàn offline, không tải gì từ mạng, không còn link bên thứ ba nào. Bấm chọn một hiệu ứng là dùng ngay: phát lên **đèn từng phím** (GIF→LED), hoặc nạp lên **màn hình TFT**. Các hiệu ứng này cũng xuất hiện ở trang Màn hình GIF trong nhóm "Thư viện Online".
- **Per-key (thử nghiệm)** — xem mục riêng bên dưới. Chỉ hiện khi đang chọn F75 Max.
- **Widget TFT** — widget vẽ trên máy rồi nạp lên màn hình phím: **Thông số máy** (CPU/RAM thật + GPU NVIDIA qua nvidia-smi nếu có), **Đồng hồ & thời tiết** (giờ thật + thời tiết Hà Nội từ Open-Meteo, không cần API key), **Đang phát** (đang phát triển — chưa tích hợp SMTC, xem BUILD-INFO). Có nút đẩy thủ công và tự động đẩy mỗi 5 giây. Trang này cũng chỉ hiện khi mẫu có màn hình.
- **Đồng hồ & Cài đặt** — đồng bộ đồng hồ; khối cài đặt thiết bị 04 17 (khoá Win, khoá Alt+F4, khoá Alt+Tab, chuyển Fn, giờ tự ngủ, độ phản hồi phím 1–5); cài đặt app (Dry-run, khởi động cùng Windows, thu nhỏ xuống khay); và 3 tính năng Stage 2 bên dưới.
- **Điều khiển từ điện thoại** — bật server LAN ngay trong app: hiện **mã QR thật** + PIN 4 số (PIN nằm sẵn trong link QR). Điện thoại cùng Wi-Fi quét QR → mở trang điều khiển: chọn nhanh hiệu ứng, nạp GIF, đẩy widget. Mọi lệnh từ điện thoại đều đi qua PC và **tôn trọng Dry-run** như thao tác trên PC. Server tắt mặc định; Windows Firewall có thể hỏi lần đầu — cho phép trong mạng Private/Home.

### Stage 2 (đã có trong bản này, ở trang Cài đặt)

- **Hồ sơ theo ứng dụng** — bật theo dõi cửa sổ đang active; gán cho mỗi app một hiệu ứng RGB (+ GIF TFT) và app tự áp khi bạn chuyển sang app đó.
- **Macro trên máy** — ghi/phát chuỗi phím bằng hook + SendInput, lưu JSON tại máy; phím tắt toàn cục **Ctrl+Alt+R** (ghi/dừng) và **Ctrl+Alt+P** (phát). Macro chạy trên máy, không ghi xuống bộ nhớ phím.
- **Thống kê gõ phím** — chỉ **đếm** số lần nhấn theo vị trí phím (heatmap + WPM). Không ghi nội dung, không ghi chuỗi phím, không lưu lại khi tắt app.

## ⚠️ MỤC THỬ NGHIỆM — per-key / stream / nhạc (đọc kỹ trước khi mở khoá)

Các lệnh **0x23** (ghi màu từng phím), **0x20** (stream đèn theo thời gian thực ~17fps) và **0x78** (feed phổ nhạc) được tài liệu hoá công khai bởi dự án **AULA L99** của Gavin Graham (github.com/Revisory676/aula-l99-linux). L99 dùng **chung nền tảng SONiX 0C45:800A** với F75 Max: trùng VID:PID khi cắm dây, trùng khung feature report 64 byte và trùng khối hiệu ứng 04 13. Các builder trong app được port nguyên văn logic của `protocol.py` và đã **kiểm chứng byte-for-byte bằng unit test** (67 test, đạt 100% — xem `tests/`).

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
- Hiệu ứng ở Thư viện Online: thư viện hiệu ứng cộng đồng.
- Font Be Vietnam Pro: SIL Open Font License 1.1.
- Bàn phím trong app là hình vẽ vector tự vẽ — không dùng tài sản của AULA.
