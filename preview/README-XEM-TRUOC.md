# Tien Keyboard Studio — BẢN XEM TRƯỚC GIAO DIỆN (v0.9.1)

Đây là **app Windows thật** (không phải mockup web) để bạn mở lên xem và duyệt
**giao diện** của Tien Keyboard Studio bản 3.2 trước khi chốt làm bản đầy đủ.

> **v0.9.1 — sửa lỗi sập app khi mở (bản vá duy nhất so với v0.9):**
> v0.9 bị crash ngay lúc khởi động do hiệu ứng chuyển trang động vào
> thuộc tính biến đổi (RenderTransform) mà Avalonia không có bộ chạy
> hoạt ảnh đăng ký sẵn. v0.9.1 bỏ phần trượt đó, **giữ nguyên hiệu ứng
> mờ dần (fade)** khi chuyển trang — giao diện và tính năng không đổi.
> Bản vá đã được kiểm chứng bằng cách chạy thử đủ 8 trang trên Linux
> (Xvfb) trước khi phát hành.

## Cách mở

Chạy thẳng file **TienKeyboardStudioPreview.exe** — không cần cài thêm gì
(exe đã tự chứa .NET runtime). Cửa sổ mở rộng/thu nhỏ thoải mái
(tối thiểu ~1040×640).

## Bản này CHỈ có giao diện — chưa nối phím thật

- Không chứa bất kỳ mã thiết bị/HID nào, **không ghi được xuống bàn phím**.
- Mọi thao tác liên quan tới phím thật (Áp dụng RGB, Nạp GIF, Đồng bộ đồng hồ,
  Chuyển thiết bị…) đều hiện thông báo:
  *"Bản xem trước — tính năng này chưa nối phím thật"*.
- Không cần mạng, không thu thập dữ liệu. Toàn bộ GIF (21 GIF thư viện +
  16 hiệu ứng cộng đồng) đã nhúng sẵn trong exe.

## Hướng thiết kế đã áp dụng

- **Nền sạch, trung tính** (xám than/navy phẳng — không vũ trụ/sao/tinh vân),
  thẻ/panel kính mờ rất nhẹ, viền mảnh, bo góc lớn, bóng mềm.
- Accent **gradient tím #7C3AED → cyan #22D3EE** dùng tiết chế: nút chính,
  mục đang chọn ở sidebar, thanh trượt, viền thẻ đang chọn.
- **Bàn phím trung tâm**: vỏ nhôm phay xước + bóng đổ xuống "mặt bàn",
  keycap trắng ngà 3D (mặt sáng, hông đậm), quầng LED bloom quanh phím
  chạy theo màu hiệu ứng, núm xoay kim loại có vân tròn.
- Sidebar kính nổi, mục chọn là pill gradient; chuyển trang có hiệu ứng
  fade/trượt nhẹ; công tắc dạng pill trượt; toast kính mờ.
- **Logo Mẫu C** (chữ T tạo hình từ phím spacebar) — đã được bạn duyệt —
  dùng làm icon app, đầu sidebar và mục giới thiệu.

## 8 trang trong bản xem trước

1. **Tổng quan** — khu "Bàn phím đang kết nối": nút **Quét bàn phím**
   (mô phỏng quét ~1 giây) → thẻ AULA F75 Max "Đang sử dụng"
   (USB-C · VID 0C45:800A · Pin 87%), một thiết bị minh hoạ thứ hai
   (AULA F75 Bluetooth) và dòng thiết bị lạ chưa có driver. Bàn phím lớn
   chạy hiệu ứng trực tiếp + 3 thẻ thống kê. Bên dưới là lưới
   **"Tất cả mẫu được hỗ trợ"** (16 mẫu) với nhãn trạng thái theo rà soát
   giao thức: chỉ tích hợp mẫu **đã công khai giao thức** (SDK chính thức /
   VIA-QMK / driver mã nguồn mở); mẫu giao thức kín để sau.
2. **Ánh sáng RGB** — 20 hiệu ứng firmware, bấm là bàn phím chạy thử ngay;
   chọn màu (9 màu + chế độ nhiều màu), trượt độ sáng/tốc độ, đổi hướng chạy.
3. **Theo nhạc** — mô phỏng: các cột phím nhảy như equalizer theo nhịp
   tổng hợp (bản thật sẽ bắt nhạc thật từ máy tính qua WASAPI loopback).
   Có chỉnh độ nhạy và chế độ nền.
4. **Màn hình GIF** — 21 GIF thư viện (lọc theo nhóm Ngân hà / Pixel & Game /
   Thư giãn; hover là GIF chạy), GIF chọn phát trong ô màn hình TFT trên
   bàn phím + khung xem trước lớn; chuyển Fill/Fit/Stretch để xem cách
   app chuẩn hoá về 128×128.
5. **Thư viện Online** — 16 hiệu ứng cộng đồng của Hoàng Phúc Keyboard
   (đã chuẩn hoá sẵn cho TFT, nhúng trong app): tìm kiếm, chips hashtag,
   nút "Lấy về máy" (mô phỏng), ghi chú về GIF gốc LED toàn phím.
6. **Widget TFT** — 3 widget vẽ trên máy tính rồi đưa vào màn hình phím:
   **Thông số máy** (CPU/GPU/RAM — giá trị mô phỏng), **Đang phát**
   (bài hát mô phỏng), **Đồng hồ & thời tiết** (giờ thật của máy bạn).
7. **Đồng hồ & Cài đặt** — đồng hồ trực tiếp, các công tắc minh hoạ
   (Dry-run, khởi động cùng Windows, khay hệ thống…), mục giới thiệu
   kèm logo.
8. **Điều khiển từ điện thoại** — trang khái niệm: mã QR mô phỏng +
   mã PIN 4 số, khung điện thoại hiển thị trang điều khiển từ xa mẫu.
   Lưu ý: điện thoại không chỉnh trực tiếp được phím — PC là cầu nối
   bắt buộc và phải đang mở app.

## Cách góp ý để chốt bản thật

Mở app, lướt qua các trang (ưu tiên **Tổng quan, Ánh sáng RGB,
Màn hình GIF**) rồi cho ý kiến, ví dụ:

- Nền/độ sáng tổng thể đã "sạch và hiện đại" chưa?
- Bàn phím đã đủ đẹp/chân thực chưa (vỏ nhôm, keycap, quầng LED)?
- Thẻ, nút, sidebar, tốc độ hiệu ứng chuyển trang có mượt không?
- Trang nào thừa/thiếu, muốn thêm gì?

Duyệt xong, bản 3.2 đầy đủ sẽ được xây trên đúng giao diện này và
nối driver phím thật (Dry-run bật mặc định như các bản trước).
