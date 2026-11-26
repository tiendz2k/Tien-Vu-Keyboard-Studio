# HỒ SƠ GIẢI PHẪU: YUNZII Cake (C98 Cake Meow / C75 Cake Meow) — hệ sinh thái driver chính hãng

> Phương pháp: **phân tích tĩnh 100%** — không cài đặt, không chạy file, không kết nối thiết bị.
> Ngày trích xuất: 2026-11-26 (UTC). Mọi khẳng định đều gắn nhãn **[CODE]** (thấy trực tiếp trong mã/dữ liệu, kèm file + trích đoạn) hoặc **[SUY LUẬN]** (suy ra từ nhiều điểm trong mã). "Không tìm thấy" = đã tìm và không có — không đoán, không bịa hằng số.
> File kèm theo: `devices.json` — bảng thiết bị dạng máy đọc.

---

## 1. Nguồn chính thức đã dùng (URL chính xác)

| Nguồn | URL | Nội dung |
|---|---|---|
| Trang phần mềm YUNZII | `https://www.yunzii.com/en-ca/pages/software` | Mục C75 (Driver Version **20260515**) và C98 (Driver Version **20260113**) đều trỏ web driver `https://YUNZII.hubx.pro/` |
| Driver Windows C98 | `https://cdn.shopify.com/s/files/1/0509/3713/5273/files/YUNZII_C98_Keyboard_Setup_v2.0_20260113.exe?v=1771989809` | File EXE 2.233.025 byte (lưu tại `win/`) |
| Driver Windows C75 | `https://mega.nz/folder/XQl20DLB#zkp-vOzshQzmvstKLcDuww` (+ folder MEGA thứ hai `bUsShbaT#cTC1rS5gLguDwga1o5jpdA`, khả năng là firmware) | **Chỉ** phân phối qua MEGA — xem mục 8 (khoảng trống) |
| Web driver | `https://yunzii.hubx.pro/` → bundle `app-a0yy2ZsF.js` (4.492.366 B) + `chunk-DHIAZ2cm.js`, `chunk-D1DtcWCl.js`, `chunk-Cqq0tayn.js`, `app-ChT2Owkd.css` (lưu tại `web/`) | SPA Vue 3 + Element Plus, toàn bộ logic WebHID nằm trong `app-a0yy2ZsF.js` |

**Nhận định tổng quan [CODE]:** Dòng Cake **không** dùng VIA/QMK (khác nhiều dòng AL/B của chính YUNZII). C75/C98 chạy nền tảng OEM "hub" dùng chung cho nhiều hãng (trong bundle còn layout của MCHOSE, AULA, HUAMI…) với họ giao thức đặt tên trong mã là **`kb_by_v2`** — cùng dòng nền tảng firmware "BYK916" mà YUNZII dùng cho nhiều mẫu (tên file firmware `YUNZII_*_Firmware_BYK916_*` trên cùng trang software) **[SUY LUẬN — dựa trên tên giao thức `kb_by_v2` + quy ước đặt tên BYK916]**.

---

## 2. Kiểm toán an toàn

### 2.1. Ứng dụng Windows — `YUNZII_C98_Keyboard_Setup_v2.0_20260113.exe`

| Hạng mục | Kết quả |
|---|---|
| SHA-256 | `93c507f80c714293f28b5a97cfe68d8078d54f10bab40b3de146003c5a271d35` |
| MD5 | `324ace1172ef216e6b826a06a719ca18` |
| Kích thước | 2.233.025 byte |
| Chữ ký số (Authenticode) | **KHÔNG có** — Security Directory trong PE header trống (offset/size = 0) **[CODE]** |
| Kiến trúc | PE32 (32-bit) GUI, 9 section (`.text .itext .data .bss .idata .tls .rdata .reloc .rsrc`) — dấu hiệu bộ máy **Delphi** (section `.itext`/`.bss` đặc trưng) **[CODE]** |
| Cấu trúc file | Stub Delphi ~232 KB + **overlay ~1.995.457 byte** bắt đầu bằng magic `zlb\x1a` rồi tới các khối nén kiểu LZMA (props `0x5D`) — đây là gói tự giải nén (SFX) độc quyền; 7-Zip không mở được, LZMA-Alone thô giải nén thất bại → **chưa trích được payload bên trong** (xem mục 8) **[CODE]** |
| Version info | Chỉ có `CompanyName = "YUNZII"`; FileVersion/ProductName/Copyright để trống **[CODE]** |
| Endpoint mạng trong stub | **Không có URL nào** (cả ANSI lẫn UTF-16LE, ngoài XML manifest của Windows) **[CODE]** |
| API đáng ngờ | Không thấy chuỗi API kiểu injection/keylogging/download (CreateRemoteThread, WriteProcessMemory, SetWindowsHookEx, URLDownloadToFile, InternetOpen…) trong stub; DLL import thấy qua strings chỉ gồm advapi32/comctl32/kernel32/oleaut32/user32 **[CODE — phạm vi stub, payload chưa trích được nên không kết luận cho payload]** |
| Telemetry/miner/credential | Không có dấu hiệu trong phần đã kiểm tra. **Phán quyết: stub SẠCH ở mức phân tích tĩnh; payload trong overlay chưa kiểm chứng được — mức tin cậy TRUNG BÌNH.** Không có bằng chứng mã độc, nhưng file **không chữ ký** là điểm trừ khách quan. |

### 2.2. Web driver (chỉ ghi nhận endpoint)

- Toàn bộ asset phục vụ từ chính `yunzii.hubx.pro`. **[CODE]**
- Endpoint ngoài duy nhất trong mã: `https://download.hpgthub.com/GT87.exe` — nằm trong `window.$config.downLoad` của trang (`downLoadShow:!1` — đang tắt hiển thị), là bản Windows của cùng nền tảng hub cho một mẫu khác (GT87). **[CODE]**
- Không có Google Analytics / Sentry / Mixpanel / Segment, không có WebSocket (`wss://`) trong các bundle. **[CODE]**
- `window.$config = {authorize:"Copyright © 2025 YUNZII", title:"YUNZII", …}` — bản hub được đóng gói riêng cho YUNZII. **[CODE]**

---

## 3. Bảng thiết bị của nền tảng (registry trong mã)

Registry thiết bị nằm nguyên văn trong `web/app-a0yy2ZsF.js` (biến `Pt`, trích nguyên trạng) **[CODE]**:

| Model | Tên HID (product string) | Layout dùng chung | VID | PID | Report TX/RX/IO | Payload | Giao thức | Kênh | UUID (48-bit) | Loại | Số phím/lớp |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **C75 Cake Meow** | `C75` | `MCHOSE_X75` | 0x258A (9610) | 0x010C (268) | 6 / 6 / 6 | 519 B | `kb_by_v2_wired` | feature_report | 3298534884215 (0x030000000377) | K1 | 85 × 3 lớp |
| **C98 Cake Meow (ANSI)** | `YUNZII C98 Keyboard` | `MCHOSE_GX87` | 0x258A | 0x010C (268) | 6 / 6 / 6 | 519 B | `kb_by_v2_wired` | feature_report | 3298534884346 (0x0300000003FA) | K2 | 101 × 3 lớp |
| **C98 Cake Meow ISO-DE** | `YUNZII C98 ISO-DE` | `YUNZII_C98_DE` | 0x258A | 0x02DE (734) | 6 / 6 / 6 | 519 B | `kb_by_v2_wired` | feature_report | 3298534884664 (0x030000000538) | K3 | 102 (lớp 0) / 101 |
| **C98 Cake Meow ISO-FR** | `YUNZII C98 ISO-FR` | `YUNZII_C98_FR` | 0x258A | 0x02DF (735) | 6 / 6 / 6 | 519 B | `kb_by_v2_wired` | feature_report | 3298534884665 (0x030000000539) | K4 | 102 (lớp 0) / 101 |
| **Dongle 2.4G (dùng chung)** | `2.4G wireless` | — | 0x3554 (13652) | 0xFA09 (64009) | 19 / 19 / 19 | 19 B | `kb_by_v2_wireless` | report (output/input) | 0xFFFFFFFFFFFF (wildcard) | K1 | — |

Điểm quan trọng **[CODE]**:

- **C75 và C98 dùng chung VID:PID 0x258A:0x010C khi cắm dây** → không thể phân biệt bằng VID/PID. Driver phân biệt bằng **UUID đọc từ chính thiết bị** (lệnh 0x82, xem mục 5.4) đối chiếu với registry, kèm product string.
- VID **0x258A là của Sinowealth** — nền tảng chip/BY dùng VID này cho rất nhiều bàn phím hãng khác; trùng VID:PID với các mẫu không liên quan là chuyện thường (đúng cảnh báo đã biết từ các đợt trích xuất trước).
- Layout của C75/C98-ANSI **dùng lại nguyên layout của MCHOSE X75 / GX87** trong cùng nền tảng hub → các mẫu này chung board/platform với MCHOSE **[CODE]**.
- Kết nối: `navigator.hid.requestDevice({filters})` với filters dựng từ registry (chỉ VID/PID); sau khi mở thiết bị, driver gọi `get_uuid()` để xác thực đúng model trước khi cho cấu hình. **[CODE]**
- **Usage page: KHÔNG xuất hiện trong mã** (0 lần `usagePage` trong toàn bundle) — driver không lọc theo usage page; nó gọi thẳng `sendFeatureReport(6, …)`/`receiveFeatureReport(6)` lên HIDDevice. Collection vendor tương ứng (thường là 0xFF00/0xFF13 trên các board Sinowealth) do firmware trình diện — **[SUY LUẬN]**, mã web không khẳng định.

---

## 4. Kiến trúc dữ liệu cấu hình (chung cho cả hai đường truyền)

Driver tổ chức cấu hình thành các **khối dữ liệu logic** có độ dài cố định; đường dây và đường 2.4G chỉ khác cách đóng gói truyền tải (mục 5 & 6):

| Khối | Độ dài | Nội dung |
|---|---|---|
| Basic info | 128 B | Chế độ Mac/Win, polling, low-latency, win-lock, sleep, mode đèn hiện tại + bảng tham số đèn (bright/speed cho 34 mode) |
| Keys (keymap) | 504 B dữ liệu (3 lớp × 126 ô × 4 B) | Mỗi phím 4 byte mã 32-bit big-endian; cuối khối có trailer `5A A5` |
| Custom light (đèn từng phím) | 384 B | 3 mặt phẳng R/G/B × 128 ô (xem mục 7.2) |
| Light color (màu theo mode) | 512 B | Bản ghi 21 byte/mode (xem mục 7.1) |
| Macro | chia khối 512 B (dây) | Sự kiện 4 byte/sự kiện (xem mục 7.3) |

Layout phím **[CODE]** (từ dữ liệu layout trong bundle): mỗi phím có `pos` = **offset byte** trong khối keymap (Esc `pos:8`, bước nhảy 4), `light_pos` (vị trí LED cho custom light), `effect_pos`. Giá trị phím là mã 32-bit: phím cơ bản dùng mã HID usage (Esc = `0x29`), phím media dùng mã mở rộng (volume-up = `0x20000e9`). Núm xoay (knob) của C75/C98 **không có thực thể riêng** trong layout — thao tác núm xuất hiện dưới dạng phím thường (ví dụ volume-up/down là một "phím" trong keymap) **[CODE]**. Không có lớp (layer) thứ 4: đúng 3 lớp `default / fn1 / fn2`.

---

## 5. Giao thức có dây — `kb_by_v2_wired`

Bằng chứng: class điều khiển (biến `Fo`, alias `n3`) trong `app-a0yy2ZsF.js` (~offset 1.160.906–1.172.097) **[CODE]**.

### 5.1. Transport & khung gói

- Kênh: **Feature Report**, report ID **6**; mỗi lần gửi đúng **519 byte payload** (`sendFeatureReport(6, Uint8Array(519))`), nhận bằng `receiveFeatureReport(6)`; byte đầu của đáp trả bị loại bỏ (`a.shift()`), tiếp theo là header 7 byte được lặp lại (echo). **[CODE]**
- **Header 7 byte** ở đầu mọi gói: `[cmd, param, 0x00, 0x01, 0x00, len_lo, len_hi]` — trường len là **little-endian 16-bit** = độ dài khối dữ liệu đi kèm. Dữ liệu bắt đầu tại offset 7. **[CODE]**
- Quy ước lệnh: **lệnh ĐỌC = lệnh GHI + 0x80**. **[CODE]**
- Không có checksum trên đường dây; khối keymap/basic-info dùng trailer **`5A A5`** ở 2 byte cuối payload (offset 517, 518). **[CODE]**
- Mọi thao tác được tuần tự hoá bằng mutex (`lock_send`); sau `set_keys` driver chờ **100 ms** rồi mới đồng bộ macro. **[CODE]**

### 5.2. Bảng lệnh (header nguyên văn từ các hàm đóng gói `q0t/K0t/G0t/Y0t/X0t/J0t/Q0t/Z0t/eot/tot/aot/oot`)

| Cmd | Tên trong mã | Header (7 byte) | Len | Ý nghĩa |
|---|---|---|---|---|
| 0x03 | `set_keys` (G0t) | `[3, layer, 0, 1, 0, 248, 1]` | 504 | Ghi keymap 1 lớp (126 ô × 4 B) + trailer 5A A5 |
| 0x83 | `get_keys` (Y0t) | `[131, layer, 0, 1, 0, 248, 1]` | 504 | Đọc keymap 1 lớp (đáp trả bỏ 7 byte header) |
| 0x04 | `set_basic_info` (q0t) | `[4, 0, 0, 1, 0, 128, 0]` | 128 | Ghi khối basic info |
| 0x84 | `get_basic_info` (K0t) | `[132, 0, 0, 1, 0, 128, 0]` | 128 | Đọc basic info |
| 0x06 | `set_custom_light` (X0t) | `[6, 0, 0, 1, 0, 128, 1]` | 384 | Ghi đèn từng phím (3 mặt phẳng RGB) |
| 0x86 | `get_custom_light` (J0t) | `[134, 0, 0, 1, 0, 128, 1]` | 384 | Đọc đèn từng phím |
| 0x0A | `set_light_color` (Q0t) | `[10, 0, 0, 1, 0, 0, 2]` | 512 | Ghi khối màu theo mode đèn |
| 0x8A | `get_light_color` (Z0t) | `[138, 0, 0, 1, 0, 128, 2]` | 640* | Đọc khối màu theo mode |
| 0x05 | macro send (eot) | `[5, 0, 0, n_chunks, chunk_idx, len_lo, len_hi]` | ≤512/khối | Gửi dữ liệu macro theo khối 512 B (byte 3–6 bị ghi đè khi gửi) |
| 0x82 | `get_uuid` (tot) | `[130, 1, 0, 1, 0, 6, 0]` | 6 | Đọc UUID 48-bit (đáp trả: byte 7..12, big-endian) |
| 0x87 | `get_battery` (aot) | `[135, 0, 0, 1, 0, 2, 0]` | 2 | Đọc pin (2 byte sau header) |
| 0x11 | `reset_factory` (oot) | `[17, param, 0, 1, 0, 1, 0]` | 1 | Khôi phục cài đặt gốc (param ở byte 1) |

\* Trường len của 0x8A là 0x0280 = 640 trong khi khối ghi là 512 — ghi nhận đúng như mã, không suy đoán thêm. **[CODE]**

### 5.3. ACK / xác nhận

Sau `set_basic_info`, driver đọc đáp trả và **so khớp 7 byte header echo** với mẫu `[4,0,0,1,0,128,0]` (`_check_set_basic_info_recv`) — đây là cơ chế ACK của đường dây: thiết bị trả lại feature report với header giống hệt. **[CODE]** Các lệnh đọc dùng chính đáp trả feature report làm dữ liệu. **[CODE]**

### 5.4. Nhận diện thiết bị bằng UUID

`get_uuid` (0x82) trả 6 byte tại offset 7..12 của đáp trả (sau khi bỏ header), cộng dồn big-endian thành số 48-bit, đối chiếu với `uuid` trong registry (bảng mục 3). Đây là cơ chế phân biệt C75 ↔ C98 khi trùng VID:PID. **[CODE]**

### 5.5. Layout khối Basic Info (offset tính trong payload 519 B, dữ liệu từ offset 7)

Trích từ `set_basic_info` **[CODE]**:

| Offset | Giá trị |
|---|---|
| 7 | mac_mode (1 = Mac, 0 = Win) |
| 8 | polling_level |
| 9 | hằng số 3 |
| 10 | low_latency: 3 = bật, 1 = tắt |
| 13, 14 | hằng số 4, 4 |
| 15 | hằng số 7 |
| 16–17 | light_mode hiện tại (big-endian 16-bit) |
| 18, 19 | hằng số 32, 1 |
| 22 | win_lock (0/1) |
| 25, 27, 28 | hằng số 1, 4, 1 |
| 30 | hằng số 255 |
| 31 | sleep_level |
| 35 | hằng số 1 |
| 36 | 6 nếu loại K2/K3/K4 (C98), 0 nếu K1 (C75) |
| 37 | 4 nếu K2/K3/K4, 1 nếu K1 |
| 38 | hằng số 1 |
| 63, 64 | marker `FF FF` |
| 65 + 2·k | bright của mode đèn k; 66 + 2·k = speed (bảng 34 mode, xem mục 7.1) |

### 5.6. Keymap

`set_keys(layer)`: mỗi phím ghi 4 byte big-endian tại `payload[pos-1 … pos+2]` (pos từ layout, Esc pos=8 → offset 7, ngay sau header). Phím gán macro được mã hoá thành 4 byte `[3, macro_mode, repeat_count, macro_index]` với macro_mode: 2 = nhấn một lần (BUTTON), 1 = vòng lặp (CIRCLE), 4 = lặp theo số lần (REPEAT). Cuối payload: `payload[517]=0x5A, payload[518]=0xA5`. Sau khi gửi: chờ 100 ms → `_sync_macro`. **[CODE]**

---

## 6. Giao thức không dây 2.4G — `kb_by_v2_wireless` (qua dongle)

Bằng chứng: class điều khiển (biến `t1`, alias `fl`) trong `app-a0yy2ZsF.js` (~offset 1.141.661–1.152.883) **[CODE]**.

### 6.1. Transport & khung gói

- Dongle USB **0x3554:0xFA09**, kênh **Output/Input Report** thường (không phải feature report), report ID **19**, gói cố định **19 byte**. **[CODE]**
- **Checksum ở byte cuối (byte 18)**: `crc_sum` = `(report_id_tx + tổng mọi byte trước) mod 256` — tức `(19 + Σ byte[0..17]) % 256`. **[CODE]** (Hàm: `crc_sum(t){let a=this.report_id_tx; for(o<t.length-1) a+=t[o]; return t[t.length-1]=a%256}`.)
- Đáp trả đến qua sự kiện `oninputreport` (report 19), được xếp hàng đợi (`receive_queue`, tối đa 1024) và hàm `fetch_timeout` chờ/ghép đáp trả theo gói yêu cầu; thông số tĩnh của lớp: `fetch_retry_count = 5`, `max_timeout = 3000 ms`. **[CODE]**

### 6.2. Bảng lệnh (byte 0 của gói 19 B)

| Cmd | Tên trong mã | Gói mẫu (trước khi tính CRC) | Ý nghĩa |
|---|---|---|---|
| 0x05 | `get_uuid` (U0t) | `[5, 1, 0, 0, 0, 0, 0, …]` | Đọc UUID (đáp trả của lớp r1 tương tự cho thấy UUID tại byte 9..14 — với lớp t1, vị trí chính xác trong đáp trả **chưa tách riêng được**, xem mục 8) |
| 0x06 | `reset_factory` (W0t) | `[6, 1, 0, 0, …]` | Khôi phục cài đặt gốc |
| 0x41 | `get_keys` (O0t) | `[65, 1, 0, layer]` | Đọc keymap (đáp trả ghép theo tham số (65, 36) trong fetch_timeout) |
| 0x42 | `get_custom_light` (B0t) | `[66, 1, 0, 0]` | Đọc đèn từng phím |
| 0x44 | `get_basic_info` (D0t) | `[68, 1, 0, 16]` | Đọc basic info |
| 0x49 | `get_light_color` (E0t) | `[73, 1, 0, 16]` | Đọc màu theo mode |
| 0x4A | `get_battery` (j0t) | `[74, 0, 0, 0]` | Đọc pin |

Các thao tác **ghi** qua đường 2.4G không dùng lệnh đơn: dữ liệu được đóng gói bằng `send_batch` (mục 6.3). Độ dài khối logic giữ nguyên như đường dây: basic_info = 128 B, keys = 512 B (gồm trailer 5A A5 ở 2 byte cuối), light_color = 512 B (hằng số lớp `payload_length_*`). **[CODE]**

### 6.3. `send_batch` — truyền khối theo mảnh 14 byte **[CODE]**

- Dữ liệu được cắt thành các mảnh **14 byte** (`F1(t,14)`).
- Mỗi gói 19 B: `[0]=loại dữ liệu (=1 khi ghi keymap), [1]=tổng số mảnh, [2]=số thứ tự mảnh, [3]=độ dài mảnh + (layer << 4), [4..17]=dữ liệu mảnh, [18]=CRC`.
- Sau mỗi mảnh, driver chờ ACK của đúng mảnh đó (`fetch_timeout_one`); sai/hỏng → gửi lại, tối đa **10 lần**, nghỉ **30 ms** giữa các lần; quá 10 lần → huỷ toàn bộ.
- Khối basic info khi truyền qua 2.4G dùng **cùng layout** như đường dây nhưng bỏ header 7 byte: bảng bright/speed bắt đầu tại offset 58 (= 65 − 7), marker `FF FF` tại 56–57. **[CODE]**

---

## 7. Đèn, macro, và các chức năng khác

### 7.1. Mô hình đèn theo mode **[CODE]**

- Bảng tham số mặc định `iot` trong mã có **34 mode đèn** (light_mode 1–34). Mỗi mode có: `offset` (= 2·(mode−1), vị trí cặp bright/speed trong basic info), `offset_color` (vị trí màu trong khối light-color 512 B), `color` mặc định.
- `offset_color` tuân theo công thức **7 + 21·mode** (mode 1 → 28, mode 2 → 49, mode 4 → 91, mode 5 → 112, mode 7 → 154); mode có `offset_color = 0` (mode 3, 6, …) là mode **không cho chọn màu** (hiệu ứng cầu vồng/tự đổi màu). Tức mỗi mode chiếm một bản ghi 21 byte trong khối light-color, màu RGB ghi tại đầu bản ghi (tính theo offset payload tuyệt đối).
- Danh sách hiệu ứng phía UI (`sot`) bắt đầu bằng `light_mode_close` (mode 0 = tắt đèn) rồi tới các mode có tên kiểu `mode_1`…`mode_18` + mode tuỳ chỉnh — UI chỉ phơi một tập con của 34 mode firmware. **[CODE]**
- `set_light_color` (0x0A): payload = header + **bản mẫu 512 byte `l3`** (hằng số trong mã — bảng màu mặc định của nhà sản xuất) rồi ghi đè RGB tại `payload[offset_color]` cho từng mode có màu tuỳ chỉnh. **[CODE]**
- Dải điều khiển phía UI cho họ này: bright 1–4, speed 1–4 (`br1()`), dù giá trị speed lưu trong basic info có thể là các mã đặc biệt (ví dụ 256) cho một số mode. **[CODE]**

### 7.2. Đèn từng phím (custom light) **[CODE]**

Khối 384 B = **3 mặt phẳng liên tiếp, mỗi mặt 128 ô**: R tại `light_pos − 1`, G tại `light_pos + 126 − 1` (tức +126), B tại `light_pos + 252 − 1` (+252) — tính theo offset trong vùng dữ liệu sau header (hàm `set_custom_light`). Chỉ phím thuộc lớp 0 được ghi trong lần ghi này. Không có cơ chế "stream" đèn theo thời gian thực trong `kb_by_v2`: đèn từng phím là **ghi tĩnh vào bộ nhớ thiết bị**, không phải streaming như họ SONiX 0x20. **[CODE]**

### 7.3. Macro **[CODE]**

- Mỗi sự kiện macro = **4 byte**: `[ (mã phím & 0xF0) | ((duration >> 16) & 0x0F), (duration >> 8) & 0xFF, duration & 0xFF, mã phím ]` — duration 20-bit (đơn vị theo mã nguồn của driver; `_transfer_macro_to_raw_data`).
- Gửi macro (đường dây, cmd 0x05): toàn bộ blob macro (gồm bảng mục lục: mỗi macro 4 byte header `[offset_lo, offset_hi, len_lo, len_hi]`, kèm tên macro mã hoá UTF-8 có tiền tố độ dài) được cắt thành khối **512 B**, header từng khối ghi đè byte 3 = tổng số khối, byte 4 = chỉ số khối, byte 5–6 = độ dài khối (LE).
- Keymap tham chiếu macro bằng chỉ số (mục 5.6).

### 7.4. Những thứ KHÔNG có trong `kb_by_v2` **[CODE — đã tìm, không thấy]**

- **Màn hình/TFT:** C75/C98 không có màn hình; trong hai class `kb_by_v2` không có bất kỳ hàm tải ảnh/GIF/màn hình nào.
- **Hall Effect / Rapid Trigger / SOCD:** không có trong hai class của Cake. (Chuỗi `socd`/`rapid_trigger` có xuất hiện trong bundle nhưng thuộc module của các họ thiết bị khác trên cùng nền tảng hub — không thuộc đường điều khiển C75/C98.)
- **Cập nhật firmware qua web driver:** không thấy trong các class `kb_by_v2` của bản hub YUNZII này.

---

## 8. Khoảng trống & giới hạn (trung thực)

1. **Driver Windows của C75 chỉ có trên MEGA** (folder `XQl20DLB#zkp-vOzshQzmvstKLcDuww`) — môi trường phân tích không có công cụ tải MEGA nên **chưa lấy được file**. Tuy nhiên C75 dùng chung nền tảng + chung web driver với C98, và bản Windows C98 (cùng dòng "Keyboard_Setup_v2.0") đã được kiểm toán thay thế.
2. **Payload trong overlay `zlb` của bản C98 chưa trích được** (định dạng SFX độc quyền, không phải 7z/zip/LZMA-Alone chuẩn). Vì vậy: (a) phán quyết an toàn chỉ áp dụng cho stub; (b) chưa đối chiếu được bảng lệnh phía Windows app với web driver. Giao thức trong hồ sơ này lấy từ **web driver chính hãng** — nguồn YUNZII chỉ định cho cả C75 lẫn C98.
3. **Usage page của collection HID không có trong mã web** (driver không tham chiếu `usagePage`); report ID và độ dài report thì chắc chắn (6/519 B và 19/19 B). Việc hệ điều hành chọn đúng interface vendor dựa vào descriptor của firmware — cần thiết bị thật hoặc file firmware để xác nhận usage page cụ thể.
4. Vị trí byte UUID trong **đáp trả** của lệnh 0x05 đường 2.4G chưa tách riêng được cho lớp `t1` (lớp điều khiển khác trên cùng nền tảng đặt UUID tại byte 9..14 của đáp trả; cơ chế ghép đáp trả của `t1` dùng hàng đợi + so khớp gói nên vị trí có thể khác).
5. Trường len 640 của lệnh đọc 0x8A (so với khối ghi 512) được ghi lại đúng nguyên văn từ mã; ý nghĩa thực tế cần kiểm chứng trên thiết bị.
6. Các hằng số "ma thuật" trong basic info (offset 9, 13–15, 18–19…) được chép nguyên văn từ mã `set_basic_info`; ý nghĩa từng trường chưa được nhà sản xuất tài liệu hoá.

---

## 9. Tóm tắt cho lộ trình T Keyboard

- Họ **`kb_by_v2`** (VID 0x258A) bao phủ trọn dòng Cake: **C75, C98 ANSI, C98 ISO-DE, C98 ISO-FR** + dongle 2.4G 0x3554:0xFA09.
- Điều kiện nhận diện an toàn trước khi ghi: khớp VID/PID **và** đọc UUID (0x82) đối chiếu registry — bắt buộc vì 0x258A:0x010C bị dùng chung bởi nhiều board không liên quan (kể cả AULA F75 thường thuộc họ khác).
- Đường dây đã đủ chi tiết để cài đặt driver read/write (bảng lệnh mục 5.2 + layout mục 5.5); đường 2.4G đủ cho read + batch write (mục 6). Tất cả vẫn cần **kiểm chứng trên phần cứng thật** trước khi mở ghi thật, theo nguyên tắc an toàn của dự án.
