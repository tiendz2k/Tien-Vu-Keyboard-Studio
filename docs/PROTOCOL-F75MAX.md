# Giao thức HID — AULA F75 Max

Tổng hợp kỹ thuật về giao thức điều khiển của bàn phím **AULA F75 Max** (USB VID:PID `0C45:800A` có dây / `05AC:024F` dongle 2.4G), phục vụ phát triển T Keyboard Studio.

Tài liệu gồm 2 phần mức độ tin cậy khác nhau — **đọc kỹ nhãn của từng phần**:

- ✅ **Đã kiểm chứng**: đối chiếu byte-for-byte giữa hai driver mã nguồn mở độc lập (mohin7/aula-f75max và VitalyArt/Aula-F75-Max-Driver).
- 🔬 **Đã có tài liệu trên nền tảng anh em, chờ kiểm chứng trên Max**: trích từ dự án AULA L99 của Gavin Graham — L99 dùng **cùng nền tảng phần cứng** (trùng VID:PID `0C45:800A`, trùng khung giao dịch 64 byte, trùng khối lệnh đèn). Chưa từng chạy trên chính F75 Max.

---

## 1. Kênh truyền & khung giao dịch ✅

| Hạng mục | Giá trị |
|---|---|
| Kênh lệnh | Usage page `0xFF13`, feature report **64 byte** |
| Kênh hiển thị (màn hình TFT) | Usage page `0xFF68`, output report **4096 byte**/chunk |
| Xác nhận (ACK) | Gói trả về có `byte[3] == 0x01` |
| Độ trễ giữa các gói | ~35 ms (theo cấu hình driver hãng: `cmd_delaytime="35"`) |

Mọi lần ghi cấu hình đều theo khung giao dịch:

```
04 18 (bắt đầu) → 04 XX (chọn khối, byte[8] = số trang) → các trang dữ liệu → 04 02 (áp dụng) → 04 F0 (kết thúc)
```

Cấu trúc gói lệnh tổng quát: `04 <opcode> 00 00 00 00 00 00 <số khối> 00 …`

## 2. Các khối đã kiểm chứng ✅

### 2.1 Khối đèn — `04 13`
Payload (1 trang):
- `[0]` = id hiệu ứng (0–19)
- `[1..3]` = R, G, B
- `[8]` = cờ "colourful" (đa sắc)
- `[9]` = độ sáng, thang **1–5**
- `[10]` = tốc độ, thang **1–5**
- `[11]` = hướng chạy (0–3)
- `[14..15]` = trailer `AA 55`

20 hiệu ứng firmware: Off, Static, SingleOn, SingleOff, Glittering, Falling, Colourful, Breath, Spectrum, Outward, Scrolling, Rolling, Rotating, Explode, Launch, Ripples, Flowing, Pulsating, Tilt, Shuttle. **Không có hiệu ứng nhạc và không có hiệu ứng "custom" trong danh sách này** — hai tính năng đó đi qua đường riêng (mục 3).

### 2.2 Khối cài đặt — `04 17`
Payload (1 trang):
- `[1]` = `0x01`
- `[2]` = khoá phím Win
- `[3]` = khoá Alt+F4
- `[4]` = khoá Alt+Tab
- `[5]` = chuyển đổi Fn
- `[6]` = hẹn giờ ngủ (0 = không, 1 = 1 phút, 2 = 5 phút, 3 = 30 phút)
- `[8]` = mức phản hồi phím (key response) 1–5
- `[62..63]` = trailer `AA 55`

Giá trị mặc định nhà máy (prefix): `00 01 00 00 00 00 02 00 02`.

### 2.3 Khối đồng hồ — `04 28`
Giao dịch: `04 18 → 04 28 → payload thời gian → 04 02`. Đồng bộ giờ hệ thống xuống đồng hồ trên màn hình phím.

### 2.4 Kênh hiển thị TFT — `04 72` / usage page `0xFF68`
- Ảnh/GIF 128×128 được mã hoá thành luồng: header 256 byte (số khung hình, độ trễ — đơn vị 2 ms) + các khung hình **RGB565 little-endian**, mỗi khung 32 768 byte.
- Truyền qua kênh `0xFF68` theo chunk 4096 byte (8 chunk/khung hình), mỗi chunk chờ ACK.
- Tối đa 255 khung hình. Chỉ hoạt động qua **USB có dây**.

### 2.5 Các khối khác đã biết
| Khối | Số trang | Ghi chú |
|---|---|---|
| `04 11` | 9 | Bảng keymap/macro — bố cục chưa rõ |
| `04 27` | 9 | **Gần như chắc chắn là bảng keymap lớp Fn** (cùng khuôn với `04 11`) — nhãn "lưu trữ đèn" trong tài liệu cũ là nhận định sai |
| `04 15` / `04 19` | — | Khe ảnh hiển thị / reset cấu hình |

### 2.6 Kênh dongle 2.4G ✅
Usage page `0xFF59`, report 32 byte, checksum = 8 bit thấp của tổng các byte, lưu ở byte 31. Kênh này lo pin / đèn / mức phản hồi / chế độ game; màn hình, đồng hồ và reset chỉ có ở kênh có dây.

---

## 3. Phát hiện từ nền tảng anh em L99 🔬

> **Nguồn**: Gavin Graham, dự án [aula-l99-linux](https://github.com/Revisory676/aula-l99-linux) (`tools/aula_l99_hacky/protocol.py`, `re_notes/`) — đã kiểm chứng trên phần cứng L99 (ghi + đọc ngược per-key, stream giải mã từ bản bắt gói USB của driver hãng). **Chưa kiểm chứng trên F75 Max.**

### 3.1 Ghi màu từng phím (lưu bền) — opcode `0x23`, kích hoạt bằng hiệu ứng `0x80`
Giao dịch theo khung chuẩn: `04 18 → header "04 23 …" (byte[8] = 9) → 9 khối dữ liệu → 04 02 → 04 F0`.

- 9 khối: khối 1–8 là các hàng ma trận 0–7; khối 9 là khối kết thúc (toàn 0, mang trailer `AA 55` ở byte 62..63).
- Mỗi khối chứa **16 entry × 4 byte: `[key_id, R, G, B]`**. Vị trí entry trong khối = nibble thấp của key_id (cột); chỉ số khối = nibble cao (hàng): `row = id >> 4`, `col = id & 0x0F`.
- Thứ tự màu là R, G, B nguyên bản. Vị trí ma trận không có phím vật lý → gửi 4 byte 0.
- **Không có checksum** trên đường truyền; `AA 55` chỉ là trailer kết thúc bản ghi.
- Hiển thị: chọn hiệu ứng **`0x80`** qua khối `04 13` thông thường (đây là chế độ custom per-key, không nằm trong danh sách 20 hiệu ứng firmware). Hiệu ứng Static (`0x01`) cũng dùng bảng màu đã nạp làm nguồn màu.

### 3.2 Đọc ngược màu từng phím — opcode `0xF5` (chỉ đọc)
- Không bọc trong phiên giao dịch. Driver hãng gọi liên tục ~27 lần/giây để phản chiếu trạng thái LED: `commit → truy vấn (04 F5, byte[8] = 9) → nhận 9 khối`.
- Các khối trả về dùng cùng bố cục `[key_id, R, G, B]` × 16 nhưng **không có khối kết thúc** — cả 9 khối đều là dữ liệu thật, bao phủ không gian id `0x00`–`0x8F`.
- Đây là phép thử đầu tiên lý tưởng trên Max: **read-only, không thể làm hỏng cấu hình**.

### 3.3 Stream thời gian thực — opcode `0x20` (đường GIF → LED và hiệu ứng host)
Giải mã từ bản bắt gói `save_to_gif_18.pcapng` (driver hãng chạy animation từ máy tính):

```
mỗi khung hình: header "04 20 …" (byte[8] = 8) → ACK → 8 khối dữ liệu → 04 02 (commit) → ACK
```

- **Không** có `04 18` / `04 F0` — khác mọi đường ghi có phiên.
- 8 khối = một mảng phẳng **512 byte = 128 quad `[key_id, R, G, B]`** cắt thành report 64 byte. Các quad đầu là phím vật lý **đóng gói không khoảng trống, theo thứ tự đọc trực quan** (hàng trên→dưới, trái→phải); phần còn lại là đệm 0 (kể cả byte id).
- Không có trailer `AA 55`. Chỉ header và commit cuối được ACK; các khối dữ liệu không ACK riêng.
- Tốc độ đo được: khoảng cách header trung vị 58,7 ms ≈ **17 fps**; trong một khung hình các khối cách nhau ~2,8 ms.
- Tính bền vững: chưa kiểm chứng, nhưng nhịp ghi này gợi ý dữ liệu chỉ nằm ở **RAM** (khớp với cơ chế app của shop Hoàng Phúc: phải mở app liên tục, GIF không lưu trong phím).
- Lưu ý trung thực từ tác giả: bản bắt gói chỉ animate 1 phím, nên việc render nhiều phím/khung là **suy luận**, chưa phải chứng minh; stream có ghi đè hiệu ứng tích hợp đang chạy hay không cũng chưa kiểm chứng. Đường `0x20` **không** cần chọn hiệu ứng `0x80` trước.

### 3.4 Kênh nhạc (Music Rhythm) — opcode `0x78`
Trên nền tảng này, Music Rhythm **không phải hiệu ứng firmware**: app trên PC phân tích FFT âm thanh hệ thống rồi gửi xuống ~21 fps, mỗi khung 1 khối 64 byte:

```
04 08 64 <23 mức dải tần, thấp → cao> <38 byte 0>
```

- Byte 0 = kiểu nhịp (Rhythm style), byte 1 = chế độ nền (Background Mode), byte 2 = biên độ (Amplitude, mẫu bắt được = 100), byte 26 = độ sáng nền (Background Brightness) — khớp chính xác 4 điều khiển trong tab Music Rhythm của driver hãng.
- Khung truyền: `commit → header 0x78 → khối dữ liệu` (commit đi **trước**).
- Trên L99, luồng này điều khiển bộ phân tích phổ trên màn hình phụ. Việc firmware F75 Max có ánh xạ `0x78` xuống đèn phím hay không: **hợp lý nhưng chưa kiểm chứng** — để dành cho giai đoạn bắt gói/thử nghiệm.

### 3.5 Bảng mã phím của F75 Max
Trích từ `layouts/rgb-keyboard.xml` của driver hãng F75 Max: 80 entry `<key>`, `light_index == key_index` ở mọi phím, **79 giá trị duy nhất** nằm trong cùng không gian id ma trận `(row << 4) | col` như L99:

```
0x01–0x0D, 0x13–0x1F, 0x25–0x31, 0x37–0x43, 0x49–0x55,
0x5B–0x5E, 0x60, 0x63–0x67, 0x76–0x79
```

- Giá trị lớn nhất `0x79` — nằm gọn trong không gian `0x00`–`0x8F` mà định dạng 9 khối địa chỉ hoá.
- Một điểm bất thường trong XML hãng: `Shift_R` và `Alt_R` cùng mang giá trị 84 — gần như chắc chắn là lỗi file của hãng, cần phân xử khi thử trên phần cứng (thử từng phím riêng lẻ).
- **Thứ tự stream suy luận cho Max**: sắp xếp các rect trong XML theo `(top, left)` — cùng phương pháp Graham đã kiểm chứng trên L99. Vẫn là giả thuyết cho đến khi được xác nhận bằng phép thử `0xF5` hoặc stream trên phím thật.

### 3.6 Ghi chú vận hành
- Các gói không được gửi sát nhau tuyệt đối (khối thứ hai lỗi timeout khi khoảng cách = 0); 2 ms là đủ trên máy của Graham, mặc định của anh là 10 ms, driver hãng dùng ~35 ms.
- Không chạy thử khi driver hãng đang mở — nó poll `0xF5` liên tục và sẽ làm lệch các gói trả về.
- Bước `04 F0` có thể khiến bàn phím khởi động lại giữa chừng và rớt kết nối HID — mã nguồn cần có xử lý reconnect/retry quanh bước này.

---

## 4. Thang kiểm chứng an toàn (áp dụng cho mọi tính năng mới)

1. **Dry-run**: cài đặt bộ dựng gói (builder) cho `0x23` / `0xF5` / `0x20` sau logger dry-run hiện có; unit-test byte-for-byte với builder tham chiếu của Graham (dùng đầu vào L99).
2. **Probe chỉ đọc**: gọi truy vấn `0xF5` trên Max thật. Nếu Max trả 9 khối có byte id khớp tập id trích từ XML → nền tảng được chứng minh trên chính thiết bị, thứ tự phím được xác nhận — trước mọi lần ghi.
3. **Ghi tối thiểu**: xác nhận factory reset (Fn+Esc giữ ~5 giây) hoạt động **trước**; đóng driver hãng; USB-C only; nạp `0x23` với đúng 1 phím khác màu đen + chọn hiệu ứng `0x80`; kiểm chứng bằng mắt và bằng đọc ngược `0xF5`. Phân xử luôn điểm trùng `light_index = 84` ở bước này.
4. **Stream**: phát khung `0x20` dựng từ thứ tự stream suy luận ở ~17 fps; kiểm chứng tính RAM-only (rút cắm lại) và render nhiều phím.

Điều kiện dừng ở mọi giai đoạn: byte trả về bất thường, mất ACK (`byte[3] != 0x01`), hoặc rớt kết nối → dừng và factory reset.

---

## 5. Nguồn

- mohin7 — [aula-f75max](https://github.com/mohin7/aula-f75max) (`Docs/Hardware-Protocol-Notes.md`, `Sources/AulaKit/Protocol/AulaWiredProtocol.swift`) và [aula.uxatom.com](https://aula.uxatom.com/).
- VitalyArt — [Aula-F75-Max-Driver](https://github.com/VitalyArt/Aula-F75-Max-Driver) (`Sources/AulaF75MaxDriver/AulaDevice.swift`, `docs/DEVELOPMENT.md`).
- Gavin Graham — [aula-l99-linux](https://github.com/Revisory676/aula-l99-linux) (`tools/aula_l99_hacky/protocol.py`, `tools/aula_l99_hacky/README.md`, `tools/aula_l99_hacky/re_notes/color_stream.md`, `tools/aula_l99_hacky/re_notes/key_remap_macros.md`); trang dự án: [gavingraham.com/aula-l99-linux-keyboard-app-tools](https://gavingraham.com/aula-l99-linux-keyboard-app-tools/).
- File của driver hãng AULA F75 Max (`config.xml`, `layouts/rgb-keyboard.xml`, `language/1033.lan`) — chỉ dùng để trích thông số thiết bị và bảng mã phím.
