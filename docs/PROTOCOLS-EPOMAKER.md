# Hồ sơ mổ xẻ: EPOMAKER Driver 2.1.92 (Windows) — giao thức bàn phím & màn hình

> Phân tích **tĩnh 100%**: không chạy/không cài binary, không kết nối thiết bị. Mọi khẳng định gắn nhãn
> **[CODE]** (thấy trực tiếp trong mã/dữ liệu đã tải) hoặc **[SUY LUẬN]** (suy ra từ bằng chứng gián tiếp).
> Ngày trích xuất: 2026-11-27. Thư mục làm việc: `~/workspace/hot-keyboards/epomaker/`.

---

## 0. Kết luận nhanh (TL;DR)

1. **EPOMAKER Driver bản hợp nhất KHÔNG phải nền tảng riêng** — nó là một bản gắn nhãn trắng (white-label)
   nữa của **nền tảng driver IoT của RongYuan (Thâm Quyến)**, cùng hệ với Akko Cloud v4 và MonsGeek V4
   đã mổ trước đó. Hệ quả thực tế cho T Keyboard: **giao thức lõi đã có sẵn trong kho** (hồ sơ RongYuan);
   phần mới của đợt này là **bảng thiết bị EPOMAKER** và **đường ghi màn hình TFT/OLED ở mức byte**.
2. Mẫu màn hình hot nhất trong driver này là **RT100** (mini-TV): chip họ **yc3121/yc500**, VID `0x3151`,
   màn hình cấu hình trong driver là **240×173, RGB565** [CODE]. Mẫu màn hình khác: **DynaTab 75/75X**,
   **KF850 UK** (họ chip kf850).
3. **Đường tải ảnh lên màn hình đã trích được ở mức byte** (chuẩn bị bằng lệnh 165/169 + ACK, dữ liệu
   opcode 37 = RGB565 / 41 = RGB888, cắt mảnh 56 byte, checksum BIT7) — xem mục 5.
4. **TH80 Pro và Galaxy100 KHÔNG nằm trong driver này.** Galaxy100 xuất hiện trong EPOMAKER Hub với
   `deviceModel: VENDOR_QMK` [CODE] — tức họ QMK/VIA, **bỏ qua theo tiêu chí** (đã công khai). TH80 Pro
   cũng thuộc dòng VIA (trang via-json của hãng), không có mục nào trong registry của driver hợp nhất [CODE về sự vắng mặt].
5. Kiểm toán an toàn: **SẠCH ở mức tĩnh, kèm lưu ý** (mục 2).

---

## 1. Nguồn tải (chính hãng, ghi đúng URL)

| Thành phần | URL | Ghi chú |
|---|---|---|
| Trang phần mềm | `https://epomaker.com/pages/software-downloads` | Trang chính thức; bài driver hợp nhất tại `/blogs/software/epomaker-driver` (bài RT100 cũ redirect về đây) |
| Windows | `https://orders.epomaker.com/software/EPOMAKER_setup_2.1.92.zip` | 80,148,859 B; SHA-256 `33199f62498ba9d671443d3defb2df5801bf31447bd1cd332e6ad22aff7bd84d` |
| Bên trong zip | `EPOMAKER_setup_2.1.92.exe` (NSIS) | 80,168,264 B; FileVersion 2.1.92 |
| Web driver | `https://hub.epomaker.com` | Hệ sinh thái WebHID riêng (họ "OuYu"), xem mục 8 |

Cấu trúc sau bung nén [CODE]: NSIS → `app-32.7z` → app **Electron** (`EPOMAKER Driver.exe` +
`resources/app/` gồm `dist/static/js/main_c38c7efc.js` — bundle renderer 17.5 MB chứa toàn bộ ngữ nghĩa
giao thức, `main_dist/main.js`, và `iot_driver.exe` — tiến trình native vận chuyển HID).

---

## 2. Kiểm toán an toàn — verdict: SẠCH (tĩnh), có lưu ý

- **Chữ ký số** [CODE, đọc từ PE security directory]:
  - `EPOMAKER_setup_2.1.92.exe` (installer NSIS): **có ký** (cert 13,576 B).
  - `iot_driver.exe` (native): **có ký**, chủ thể trong chứng thư: **Shenzhen Rongyuan Technology Co., Ltd.**
  - `EPOMAKER Driver.exe` (vỏ Electron): **không ký** (cert size 0) — giống hệt mẫu ở Akko (installer ký bao ngoài, exe Electron bên trong không ký).
- **Endpoint ra ngoài** [CODE, grep bundle renderer]: `api.rongyuan.tech`, `api2.rongyuan.tech`,
  `api3.rongyuan.tech`, `api2.qmk.top`, `api3.qmk.top` (cloud profile của nền tảng RongYuan — cùng endpoint
  với Akko/MonsGeek), `webapi.amap.com` (AMap — phục vụ widget thời tiết cho màn hình). Tiến trình native
  mở gRPC tại localhost (`127.0.0.1:3814`, `127.0.0.1:34254` thấy trong strings của `iot_driver.exe`).
- **Không thấy**: miner, credential-stealer, webhook lạ, SDK telemetry phổ biến (sentry/bugly/umeng… 0 hit).
  `package.json` có khai `universal-analytics` và `node-machine-id` trong dependencies, **nhưng grep cả hai
  bundle đã đóng gói cho 0 dấu vết sử dụng** (không ID đo lường, không lời gọi) [CODE] — ghi nhận là khai báo thừa, không phải bằng chứng thu thập.
- **Lưu ý riêng tư**: giống Akko/MonsGeek, profile có thể đồng bộ qua cloud RongYuan; đó là tính năng công khai của nền tảng, không phải kênh lén.

---

## 3. Kiến trúc & quan hệ nền tảng

- Electron (React + MobX) → gRPC (`proto.driver.*`: `SendMsg/ReadMsg`, `sendRawFeature/readRawFeature`,
  `watchSystemInfo`, `getWeather`, `OTAUpgrade`…) → `iot_driver.exe` (Rust/tonic, ký bởi RongYuan) → HID.
  [CODE: strings trong `iot_driver.exe` + định nghĩa protobuf trong bundle]
- Bằng chứng cùng nền tảng với Akko Cloud v4/MonsGeek V4 [CODE]:
  - Registry thiết bị chứa trường `company:"rongyuan"` cho nhiều mục và ảnh thiết bị đặt tên theo chip
    `ry5088_*`, `yc3121_*`, `yc500_*`, `yc300_*`, `pan108*` (thư mục `company/company_EPOMAKER/dev/`).
  - Bảng hằng số lệnh `FEA_CMD_*` trùng khớp hệ RongYuan đã trích (mục 4), kể cả lệnh gộp Hall-effect 101/229.
  - Checksum BIT7/BIT8 và quy ước GET = SET + 0x80 giống hệt.
- Toàn bộ registry của nền tảng chứa **1.694 thiết bị** của nhiều hãng gắn nhãn trắng; phần mang nhãn
  `company:"EPOMAKER"` là **74 mục** (bàn phím + 3 chuột) — trích vào `devices.json` kèm theo.

---

## 4. Transport & khung lệnh (họ RongYuan áp dụng cho EPOMAKER)

[CODE trừ khi ghi khác]

- **Định danh USB**: VID chính `0x3151` (RongYuan); `usagePage 0xFFFF`, `usage 2`;
  `featureReportByteLength 65` → Feature Report **64 byte + report ID**.
- **Model phân biệt bằng cặp VID:PID + mục registry**, không dùng một PID chung duy nhất như RY5088 thuần:
  các PID hay gặp: `0x4010` (biến thể "dm"), `0x4011`/`0x4015` (biến thể "soc"), `0x5029–0x502F` (họ ry5088 8K),
  `0x402A/0x402D` (chuột). [SUY LUẬN] hậu tố `dm/soc` là hai biến thể firmware/kênh kết nối của cùng một mẫu.
  Một số họ cũ dùng VID khác: `0x0461` (yzw), `0x25A7` (yc300 một phần), `0x05AC` (dongle giả Apple — TH98/TK68).
- **Khung lệnh**: header **8 byte** + payload cắt mảnh **56 byte** (đệm 0), tổng gói 64 byte.
  - Byte 0 = opcode; byte 7 = **checksum BIT7** = `255 − (sum(byte0..6) & 0xFF)` — thấy trực tiếp trong
    `_setFnKeyConfig`, `_setKeyConfig`… (hàm `writeFeatureCmd` và wrapper native `CheckSumType.BIT7`).
  - Một số lệnh dùng **BIT8**: checksum đặt tại byte 8 trên payload LED 7/8 (theo hồ sơ RongYuan).
  - **GET = SET + 0x80** (ví dụ SET_USERPIC 12 ↔ GET_USERPIC 140).
  - Nhịp gửi (pacing): các hàm dùng trễ 1/3/15/50/100 ms tuỳ luồng; luồng keymatrix có bước chờ 50 ms sau khi gửi hết các mảnh.
- **Bảng opcode cốt lõi** (trích từ hằng số `FEA_CMD_*` trong `main_c38c7efc.js`; giá trị thập phân):

  | SET | GET | Ý nghĩa |
  |---|---|---|
  | 0/128 | — | REV / thông tin bản |
  | 2 | 130 | Profile |
  | 4 | 132 | Tham số LED chính |
  | 8 | 136 | Tham số LED phụ (SLED) |
  | 9/10 | 137/138 | Keymatrix (hai biến thể theo họ chip) |
  | 11 | 139 | Macro |
  | 12 | 140 | **UserPic — ảnh đèn từng phím** |
  | 13/14 | — | Audio / music follow (SET_AUDIO) |
  | 16 | 144 | Lớp Fn |
  | 17/18 | 145/146 | Debounce / Sleep (biến thể theo họ) |
  | 24/25 | — | UserGif start / UserGif (đèn) |
  | 28/30/31 | 156/158/159 | Hiệu chuẩn Hall-effect |
  | 101 | 229 | **Lệnh gộp Hall-effect (magnetism by array)** — cùng 16 sub-op như hồ sơ RongYuan |
  | 44 | 172 | Xoá flash (flash chip erase) |
  | 127 | — | Vào bootloader |
  | 173/174 | — | Phiên bản OLED / MLED |

---

## 5. MÀN HÌNH — phần trọng tâm

Driver phân hai đường màn hình: **OLED đơn sắc/nhỏ** (nhóm lệnh 32–43) và **TFT LCD màu** (lệnh 37/41/50).
RT100 dùng đường TFT. Mọi trích dẫn dưới đây từ các hàm `setTFTLCDDataRGBImg`, `getTFTLCDDataRGBImg`,
`setTFTLCDData`, `setOLEDClock`, `setFlashChipErase` trong `main_c38c7efc.js` [CODE].

### 5.1 Nhóm lệnh màn hình (opcode thập phân)

| SET | GET | Tên hằng | Ý nghĩa |
|---|---|---|---|
| 32 | 160 | OLEDPICINDEX | Chọn trang/ảnh OLED |
| 33 | 161 | OLEDPICDATA | Dữ liệu ảnh OLED |
| 34 | 162 | OLEDOPTION | Tuỳ chọn OLED |
| 36 | 164 | OLEDGIFDATA | Dữ liệu GIF cho màn hình |
| 38 | 166 | OLEDGIFINDEX | Chọn GIF màn hình |
| 39 | — | OLEDLUANGAGE | Ngôn ngữ màn hình |
| 40 | — | OLEDCLOCK | Đồng bộ đồng hồ |
| 42 | — | OLEDWEATHER | Đẩy thời tiết |
| 43 | 171 | OLEDEFFECT | Hiệu ứng màn hình |
| **37** | **165** | **TFTLCDDATA** | **Dữ liệu TFT 16-bit (RGB565)** |
| **41** | **169** | **SCREEN_24BITDATA** | **Dữ liệu TFT 24-bit (RGB888)** |
| 50 | 178 | SETTFTFLASHDATA | Ghi flash của TFT |
| 51/52/53 | 179/180 | TOUCHVALUE / TFTTOUCHDATA / CLEARTOUCH | Dữ liệu cảm ứng (mẫu có touch) |
| 48/49 | 176/177 | OLEDBOOTLOADER / BOOTSTART | Bootloader cho MCU màn hình |

### 5.2 Quy trình tải một khung hình TFT (RGB)

1. **(Tuỳ chọn) Xoá flash** — `setFlashChipErase`: gói 64 B, byte0 = **44**, checksum BIT7.
   ACK hợp lệ = phản hồi bắt đầu bằng `[44, 0xAA, 0xAA, 0x55, 0x55]` (magic `AA AA 55 55` — trùng khớp hồ sơ RongYuan).
2. **Gói "chuẩn bị" (prepare)** — gửi bằng chính opcode GET tương ứng (**165** cho 16-bit, **169** cho 24-bit),
   header **18 byte**:
   - `[0]` opcode (165/169), `[1]` currentFrame, `[2]` frameNum, `[3]` frameDelay
   - `[4..5]` = 2 byte thấp của tổng độ dài dữ liệu ảnh (LE từng phần)
   - `[6]` = 0
   - `[8..11]` = left, top, right, bottom (byte thấp, &0xFF); `[12..15]` = byte cao (>>8) của left/top/right/bottom
     → hình chữ nhật đích trên màn hình, toạ độ 16-bit tách thấp/cao thành hai nhóm.
   - `[16..17]` = 2 byte cao của tổng độ dài (độ dài 32-bit bị tách: thấp ở [4..5], cao ở [16..17] — bố cục lạ nhưng đọc rõ trong mã).
   Sau khi gửi: đọc phản hồi Feature Report; nếu `resp[1]==0` thì chờ 100 ms và gửi lại, tối đa 10 lần;
   **ACK thành công khi `resp[1]==1`**.
3. **Các gói dữ liệu** — header 8 byte + 56 byte dữ liệu (mảnh cuối đệm 0, trường độ dài ghi số byte thật):
   - `[0]` = **37** (RGB565) hoặc **41** (RGB888), `[1]` currentFrame, `[2]` frameNum, `[3]` frameDelay
   - `[4..5]` = chỉ số mảnh (chunk index) **UInt16 LE**
   - `[6]` = độ dài payload thật của mảnh này (≤56)
   - `[7]` = checksum BIT7 (do lớp gửi native điền theo `CheckSumType.BIT7`)
   - Nhịp: chờ ~3 ms trước mỗi gói (`jm(3)`).
4. Ảnh động: lặp lại cho từng frame; `frameDelay` nằm trong header mọi gói. Phía app giải mã GIF bằng
   `gifwrap` (desktop) trước khi cắt frame [CODE: package.json + luồng `setTFTLCDDataRGBImg`].

### 5.3 Đường OLED/GIF (màn hình nhỏ, opcode 36)

`setTFTLCDData` (tên hàm dùng chung cho OLED): header 8 byte —
`[0]=36, [1]= frameNum−1 nếu là GIF (0 nếu ảnh tĩnh), [2]= chỉ số mảnh, [3]= currentFrame, [4]= frameDelay, [5]= layer`,
payload 56 byte/mảnh đệm 0. Nhánh gửi thay đổi theo loại thiết bị: thiết bị 2.4G đi qua đường native thô
(`writeRawFeatureCmd`), thiết bị thường qua `writeFeatureCmd`; nhịp 1 ms (GIF thường) / 15 ms (biến thể `isblue`).

### 5.4 Đồng bộ đồng hồ (opcode 40)

Gói 64 B: `[0]=40`; thời gian đặt tại byte 8–14: **năm 2 byte (byte 8 = cao, byte 9 = thấp)**, tháng, ngày,
giờ, phút, giây. App lấy giờ hệ thống qua PowerShell `get-date` (Windows) / `date` (macOS) [CODE].

### 5.5 Kích thước màn hình theo cấu hình trong registry [CODE]

| Mẫu | Cấu hình (`otherSetting.LED`) | Ghi chú |
|---|---|---|
| **RT100** (yc3121_cy_rt100_soc, biến thể chính) | `kbW 240 × kbH 173`, isRgb "16" (RGB565), size 1 | biến Js |
| RT100 UK / bản yc500 | `162 × 173`, RGB565, có `weather`, `isTemp` | biến Qs — vùng hiển thị khả dụng hẹp hơn [SUY LUẬN: do firmware vùng] |
| Biến thể có cảm ứng (họ kf850/RT100) | `240 × 173` + `Touch 80 × 80` | biến qs |
| **DynaTab 75X** (yc500_kf850_screen_soc), **KF850 UK** | LED `60 × 9`, isRgb "24", chấm (dot-matrix) | biến qc — đây là dải dot-matrix; xem khoảng trống mục 7 |
| DynaTab 75 (yc3121_kf850p_soc) | `otherSetting` (Iu) **không có block LED/screen** trong bản driver này | màn hình của DynaTab 75 có thể do MCU phụ/đường khác quản [SUY LUẬN] |

> Lưu ý: `kbW/kbH` là kích thước vùng ảnh mà driver xuất ra cho màn hình, lấy nguyên văn từ registry —
> không phải độ phân giải panel vật lý tra từ datasheet.

---

## 6. Đèn từng phím & tính năng khác

- **Ảnh đèn từng phím (UserPic)**: SET **12** / GET **140**; biến thể "simple" 20/148. Dữ liệu đi theo cùng
  khung 8 + 56 byte như keymatrix [CODE cho opcode + khung; chi tiết layout pixel theo hồ sơ RongYuan].
- **Keymatrix**: SET 9 hoặc 10 tuỳ họ chip (bản "simple" 19/147), 9–11 mảnh × 56 byte, header có
  `[1]=profile, [2]=248, [3]=1, [4]=chỉ số mảnh` [CODE từ `_setKeyConfig`].
- **Hall-effect** (HE65 Mag, HE68 Lite/Mag, HE75 Mag, Cypher 81 bản ry5088…): dùng lệnh gộp **101/229** và
  nhóm hiệu chuẩn 28/30/31 — **giống hệt hồ sơ RongYuan**, không trích lại ở đây.
- **Music follow**: SET_AUDIO 13/14, cửa sổ 32 mẫu; khi thiết bị ở chế độ 2.4G có bước đặt trước độ dài gói tiếp theo.
- **Thời tiết/nhiệt độ CPU cho màn hình**: qua `WeatherReq/WeatherRes` (gRPC, nguồn AMap) và `watchSystemInfo`
  của tiến trình native — khớp với tính năng "sync CPU/TEMP" trong hướng dẫn RT100 của hãng.

---

## 7. Khoảng trống trung thực (chưa trích được / chưa chắc)

1. **Đóng gói pixel chi tiết của TFT**: thứ tự byte RGB565 (BE/LE), chiều quét (hàng/cột — hồ sơ RongYuan ghi
   column-major cho màn hình 5098), và bước tiền xử lý ảnh (resize/crop về kbW×kbH) nằm rải trong renderer +
   tiến trình native; đợt này mới xác định được khung truyền, **chưa tái dựng được hàm đóng gói pixel hoàn chỉnh**.
2. **Checksum BIT8**: biết tồn tại (payload LED 7/8) từ hồ sơ RongYuan; chưa thấy vị trí gọi cụ thể trong bản EPOMAKER.
3. **DynaTab 75X**: registry gắn config dot-matrix 60×9 cho mục `kf850_screen` — chưa khớp dứt điểm với màn
   hình lớn của máy thật; có cấu hình 240×173 + touch trong cùng registry nhưng gán cho biến thể khác.
4. Ý nghĩa chính xác của hậu tố `dm`/`soc` và của từng PID (0x4010/0x4011/0x4015) theo kênh kết nối: [SUY LUẬN], chưa có tài liệu đối chứng.
5. Các mẫu EPOMAKER dùng **driver riêng** trên blog hãng (HE60, HE30 Pro, EM3, GX1…) nằm ngoài bản hợp nhất
   này — chưa mổ; một số có thể là họ khác (HE = Hall-effect dùng app riêng).
6. Chưa kiểm chứng trên phần cứng thật (không có thiết bị trong tay) — mọi giao thức ở trạng thái "trích từ mã hãng, chờ kiểm chứng thiết bị".

---

## 8. EPOMAKER Hub (hub.epomaker.com) — ghi nhận, ngoài phạm vi trích sâu

- Là một hệ sinh thái WebHID **khác** (Vue + WebHID), chứa họ giao thức "OuYu" (ví dụ EPOMAKER GX2:
  vendorId 14513, usagePage 65280, reportId 2), tính năng màn hình cho **receiver/dock** (`ReceiverScreenView`,
  `setScreenPlayback`, tải GIF với width/height/maxFrames) và các layout QMK.
- **Galaxy100** trong Hub khai `deviceModel: VENDOR_QMK` → QMK/VIA → **bỏ qua** theo tiêu chí nhiệm vụ.
- Hub liệt kê nhiều vendorId (12625 = 0x3151 RongYuan chiếm đa số mục, cùng các VID 0x2E3C, 0x3554, 0x28E9…)
  cho thấy Hub là cổng hợp nhất đa nền tảng; việc trích xuất đầy đủ Hub là một đợt riêng nếu cần.

---

## 9. Tệp bằng chứng chính

- `raw/EPOMAKER_setup_2.1.92.zip` — bản tải chính hãng (hash ở mục 1).
- `extract/app/resources/app/dist/static/js/main_c38c7efc.js` — bundle chứa: bảng hằng `FEA_CMD_*`,
  registry thiết bị, các hàm `setTFTLCDDataRGBImg` / `getTFTLCDDataRGBImg` / `setTFTLCDData` /
  `setOLEDClock` / `setFlashChipErase` / `_setKeyConfig` / `_setFnKeyConfig`.
- `extract/app/resources/app/iot_driver.exe` — native gRPC (strings: `sendRawFeature`, `readRawFeature`,
  chứng thư RongYuan).
- `analysis/devices-raw.json` — registry thô 1.694 thiết bị toàn nền tảng; `devices.json` (kèm hồ sơ này) — 74 mục EPOMAKER đã làm sạch.
