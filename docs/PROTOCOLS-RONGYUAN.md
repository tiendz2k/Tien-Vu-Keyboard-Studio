# DOSSIER — Akko Cloud Driver v4 & MonsGeek Driver v4: kiểm toán + mổ xẻ giao thức HID

**Ngày:** 27/11/2026 (JST). **Phương pháp:** CHỈ phân tích tĩnh — tải file, giải nén, đọc mã. **Không chạy/không cài bất kỳ binary nào, không kết nối thiết bị.**
**Quy ước:** **[CODE]** = thấy trực tiếp trong mã/file, có dẫn chứng. **[SUY LUẬN]** = suy ra từ nhiều điểm. "Không tìm thấy" = đã tìm và không có — không đoán, không bịa hằng số.
**Chi tiết đầy đủ theo từng app:** `analysis/akko-findings.md` (611 dòng), `analysis/monsgeek-findings.md`; bảng thiết bị gộp: `devices.json` (172 mục). Tài liệu đối chiếu mã nguồn mở: `analysis/ry5088-protocol-upstream.md`, `analysis/ry5088-firmware-upstream.md` (tải từ repo dot-agi/ry5088-flasher).

---

## 0. Kết luận nhanh

1. **Hai app là MỘT nền tảng OEM của RongYuan** (荣源/rongyuan.tech) được gắn nhãn trắng: cùng khung Electron, cùng bộ máy `iot_manager`, cùng bảng lệnh, cùng hạ tầng cloud (`api*.rongyuan.tech`, `iotdriver.gearhub.top`/`qmk.top`). Akko và MonsGeek chỉ khác lớp branding (`company/company_akko` vs `company/company_MonsGeek`) và tập model được bật.
2. **Họ chip chủ lực của cả hai hãng là RongYuan RY5088** (MCU Artery AT32F405) cho dòng Hall-effect — giao thức của nó **đã có dự án mã nguồn mở bao phủ phần lõi** (ry5088-flasher), và mã driver chính hãng **xác nhận độc lập từng chi tiết cốt lõi** (checksum, quy luật GET=SET+0x80, opcode 0x65 magnetism, định danh bằng dev_id). Hai app cộng thêm: bảng model/PID/ID đầy đủ, các họ phụ (RY6609/RY6602/YC312x/PAN108), giao thức màn hình TFT, luồng hiệu chuẩn và các tính năng stream từ PC.
3. **Kiểm toán an toàn: SẠCH trong phạm vi phân tích tĩnh** — installer của cả hai ký **EV Code Signing cho cùng một pháp nhân** (深圳市安圆科技有限公司), không thấy SDK telemetry phổ biến, không dấu hiệu miner/đánh cắp thông tin đăng nhập. Điểm riêng tư duy nhất cần biết: driver có tài khoản cloud đồng bộ profile qua máy chủ RongYuan.

---

## 1. Nguồn tải (chỉ domain chính hãng)

| App | Trang nguồn | URL tải trực tiếp | Kích thước | SHA-256 |
|---|---|---|---|---|
| Akko Cloud Driver v4 (Win) **372.2.40** (29/10/2026) | https://en.akkogear.com/download/ | https://file.akkogear.com/Akko_Cloud_v4_setup_372.2.40_WIN20261029.zip | 85,723,624 B | `0776c6c9d1613806e30a931a511de100e839de5e1121deeeecfd51d2042ad4b0` |
| MonsGeek Driver v4 (Win) **500.3.4** (28/09/2026) | https://www.monsgeek.com/download/ | https://files.monsgeek.com/MonsGeek_V4_Driver_500.3.4_WIN20260928.zip | 82,926,082 B | `46f5dfe61ac334dc57cb22f5afa4a864635c6cad2e0d905187b85334eb14e1e9` |
| MonsGeek Driver (Web) | https://app.monsgeek.com/ | bundle `js/index.1e076e39.js` | 2,572,135 B | — (tài sản web công khai) |

---

## 2. Kiểm toán an toàn

### 2.1 Chữ ký số [CODE — trích PKCS#7 từ PE security directory, đọc bằng openssl]
- Cả hai installer NSIS và `iot_manager_rs.exe` đều ký bằng chứng thư **Certum Extended Validation Code Signing 2021 CA** (Asseco), chủ thể: **深圳市安圆科技有限公司 (Shenzhen Anyuan Technology Co., Ltd.)**, Quảng Đông/Thâm Quyến, mã doanh nghiệp 91440300MA5FNF428F, có đóng dấu thời gian DigiCert. → Hai app dùng **chung một pháp nhân ký** — bằng chứng cứng cho việc chúng là sản phẩm anh em cùng nhà phát triển nền tảng.
- Exe chính của Electron (`Akko Cloud Driver v4.exe`, `MonsGeek Driver v4.exe`) **không ký** — thông lệ phổ biến của app Electron (installer đã ký EV bao ngoài).

### 2.2 Endpoint trong mã [CODE]
- Cloud của nền tảng: `https://api.rongyuan.tech`, `https://api2.rongyuan.tech`, `https://api3.rongyuan.tech` (đăng nhập/đồng bộ profile, firmware); hạ tầng IoT driver: `https://iotdriver.gearhub.top`, `https://iotdriver.qmk.top`, `https://api2.qmk.top`, `https://aidshk.qmk.top`.
- `http://192.168.6.155` — IP LAN nội bộ bị sót trong mã (môi trường phát triển của hãng).
- `update.leigod.com` — tham chiếu thư viện bên thứ ba; các domain AI (chat.deepseek.com, chat.openai.com, fotor…) chỉ là danh mục liên kết của trang "AI assistant" và cả hai bản đều đặt `"noAIAssistant": true` trong `company/*/CONFIG.json`.

### 2.3 Telemetry & nguy cơ [CODE]
- Quét `dist-electron/main.js` và các bundle: **0 kết quả** cho sentry, bugly, umeng, mixpanel, google-analytics, firebase, crashlytics, appcenter, posthog, amplitude.
- Không thấy dấu hiệu đào coin, keylogger ngoài mục đích driver, hay đánh cắp credential trong phạm vi strings tĩnh. `elevate.exe` trong `resources/` là thành phần chuẩn của Electron.
- Các tiến trình phụ có mục đích rõ ràng: `rhythm_service.exe` (bắt âm thanh → đèn theo nhạc), `screen_capture_service.exe` (chụp màn hình → đèn ambient), `keyboard_monitor_service.exe`, `system_info_hid_rs.exe` + LibreHardwareMonitor (thông số hệ thống cho widget/màn hình).
- **Verdict: cả hai app SẠCH ở mức phân tích tĩnh.** Lưu ý duy nhất: tính năng cloud yêu cầu tài khoản và đồng bộ dữ liệu profile qua máy chủ RongYuan (api.rongyuan.tech) — người dùng không muốn phụ thuộc cloud có thể dùng offline, driver vẫn điều khiển phím qua USB.

---

## 3. Kiến trúc / stack [CODE]

| Lớp | Chi tiết |
|---|---|
| Vỏ | NSIS installer → `$PLUGINSDIR/app-32.7z` → app hoàn chỉnh (khung NSIS của hai app giống hệt nhau, kể cả bộ plugin DLL) |
| App | **Electron 22.3.15 + React 18**, KHÔNG đóng gói asar — `resources/app` là thư mục trần; frontend webpack ~714 chunk JS (Akko) / ~470 (MonsGeek), bundle chính `index.157e489d.js` / `index.f9e8588e.js` |
| Cầu HID | Electron ↔ **gRPC** (protobuf-ts ở JS; tonic ở Rust) tới `iot_manager_rs.exe` trên **127.0.0.1:3838/3839/3840/3850** (+6015 quản lý), service `hid.HidService`: SendFeatureReport / GetFeatureReport / Write / ListenToReports / LockDevice |
| Native | `common_hid_rs.exe` (Rust + hidapi, tự parse HID report descriptor), `HidSharp.dll`; toàn bộ **ngữ nghĩa giao thức nằm ở JS**, native chỉ vận chuyển thô — strings của exe Rust không chứa opcode/checksum nào |
| Phiên bản nền | `APPVersion.json`: IOTVersion 222, IotManagerVersion 0.1.8 |
| Bản web | app.monsgeek.com dùng **cùng mã nguồn giao thức** (bảng thiết bị và số đếm từ khoá khớp bản desktop; chỉ khác bootstrap/hash chunk). WebHID: `requestDevice({filters:[{vendorId, productId}…]})` theo từng model rồi khớp collection **usagePage 0xFFFF / usage 2** |

Hệ quả quan trọng: vì giao thức nằm ở JS đọc được, **mọi hằng số trong hồ sơ này đều trích từ mã thật đang chạy** của hãng, không phải suy đoán từ capture.

---

## 4. Bảng thiết bị & họ chip

### 4.1 Cách driver định danh model [CODE]
- VID chủ đạo **12625 = 0x3151**; thêm 14574 (0x38EE), 14763 (0x39AB), và lác đác 1452 (0x05AC), 1121, 17235.
- **PID không định danh model:** PID 20528 (**0x5030**) dùng chung cho gần như toàn bộ dòng RY5088 của cả hai hãng; 20521 (0x5029) cũng dùng chung cho nhiều model.
- Model thật được xác định bằng **lệnh 0x8F (143) GET_INFOR**: firmware trả một **ID 32-bit little-endian** (đọc ở byte 1 của response, thử tối đa 3 lần — hàm `whoAmI`/`___getNormalID`), khớp trường `id` trong bảng thiết bị của bundle MonsGeek (52 model có sẵn `fw_id` trong `devices.json`).
- ⚠️ **Khoảng trống bằng chứng lớn nhất:** trong bundle Akko, bảng ánh xạ ID→model đầy đủ không nằm ở JS (bảng `Ct` có 1.081 object vid/pid, 922 `type:"keyboard"` của ~90 nhãn hãng, nhưng việc nối ID↔model cho từng SKU Akko một phần dựa vào dữ liệu tải thêm/không trích trọn được). Với MonsGeek, 52 model trích được kèm `id` đầy đủ.

### 4.2 Các họ chip (tiền tố tên nội bộ = platform) [CODE]
| Họ | Vai trò | Ghi chú |
|---|---|---|
| **ry5088_** | Hall-effect chủ lực (Akko TAC75 HE/TAC87, MOD007 HE, MOD68; MonsGeek FUN60/FUN68, M1 V5 TMR…) | MCU AT32F405; biến thể MonsGeek mới còn nhúng mã `ry1033` |
| **ry6609_ / ry6602_** | Họ RongYuan khác (cơ/HE đời khác) | Có mặt trong bảng Akko; opcode có dịch chuyển theo họ |
| **yc3121_ / yc3123_** | Họ cũ (MonsGeek M-series đời trước) | Giao thức rút gọn hơn |
| **pan108*_** | Chip radio PAN1080 (kênh 2.4G/BT đi qua chip này) | Khớp ghi chú của repo mở: ảnh firmware ZIP gồm cả ảnh radio PAN1080 |
| hitbox | Nhánh tay cầm/hitbox với SOCD riêng (opcode 104/105) | Ngoài phạm vi bàn phím, ghi nhận để đầy đủ |

### 4.3 Model tiêu biểu (chi tiết đầy đủ trong `devices.json`)
- **Akko:** TAC75 HE, TAC87, MOD007 V5 HE (màn hình 128×128), MOD007 HE Dragon V2, MOD68, Air 01, 5098B (màn hình 172×320), 5098 V5 (420×116), 3108 V3…
- **MonsGeek:** FUN60 Ultra TMR (VID 0x38EE PID 1 — kênh riêng), FUN60 Ultra/Pro/Max HE, FUN68 HE, M1 V5 TMR, M1 V5, M2/M3 V5, M5/M6/M7…
- Cờ RY5088 trong dữ liệu: tiền tố `ry5088_` ở tên nội bộ, ví dụ `ry5088_akko_fun60_ultra_tmr_3m_8k_8k` (FUN60 Ultra TMR, id 3853).

---

## 5. Giao thức họ RY5088 (họ chính, dùng chung cả hai app)

### 5.1 Transport [CODE]
- **Feature Report 64 byte**, report ID lấy từ descriptor do native trả về (JS không hard-code usage page; phía thiết bị là usage page **0xFFFF** — khớp descriptor của repo mở: `06 ff ff 09 02 … 95 40 b1 02`). Gói tin = reportId + 64 byte payload đệm 0; `reportCount` mặc định 64. Kênh Bluetooth có tiền tố byte 0x55.
- **Byte[0] = opcode. Đọc = Ghi + 0x80** (GET = SET | 0x80) — áp dụng nhất quán toàn bảng lệnh.
- **Checksum trích nguyên văn từ JS** (lớp base, chunk `7376c8db.js` bản MonsGeek / hàm `___encodeCmd` bản Akko):
  - Bit7 (mặc định): `s[7] = 255 - (sum(s[0..6]) & 0xFF)` — tức 8 byte đầu cộng lại ≡ 0xFF (mod 256).
  - Bit8 (chỉ lệnh LED 7/8 và bản đọc của chúng): `s[8] = 255 - (sum(s[0..7]) & 0xFF)`.
- **ACK:** không có gói ACK riêng — response của GetFeatureReport chính là ACK; JS kiểm tra `resp[0] == opcode` gửi đi. Ngoại lệ: lệnh xoá flash (172) có ACK mẫu `AA AA 55 55` (resp 172,170,170,85,85); lệnh đọc feature list (230) yêu cầu `resp[1] == 0xAA`.
- **Pacing:** SEND_TIME/READ_TIME 10 ms; `vendorSleep` sau mỗi SET (mặc định ~100 ms ở lớp Akko); reset chờ 2000 ms; kênh BT chậm hơn (100/200 ms). Dongle 2.4G được poll trạng thái bằng nhóm lệnh 0xF6 (chọn kênh: kb=10/mouse=5), 0xF7 (trạng thái), 0xFC (đọc notice) — bản MonsGeek ghi nhận thêm biến thể poll 0xFB/0xFC/0xFD với đáp án chứa `0xAA 0x55`.

### 5.2 Bảng lệnh chính (SET; GET = SET+128) [CODE — chunk `4fbc5cb1.js` / `e40a68bc.js`, class lệnh FEA_CMD_*]
| Opcode | Tên | Nội dung |
|---|---|---|
| 1 | RESET | khôi phục mặc định |
| 3 | REPORT RATE | byte mã hoá: 0=8K, 1=4K, 2=2K, 3=1K, 4=500, 5=250, 6=125 Hz |
| 4 | PROFILE | chọn profile 0–3 |
| 6 | DEBOUNCE | chống dội phím |
| 7 | LED | chế độ đèn chính — **checksum Bit8**; layout [mode][speed][brightness][option<<4 \| cờ màu][R][G][B] |
| 8 | SIDE LED (SLED) | đèn viền — **checksum Bit8** |
| 9 | KB OPTION | bit tuỳ chọn / bank lớp Fn |
| 10 | KEYMATRIX | keymap, ghi theo chunk 56 byte/gói (4 byte/phím) hoặc theo slot |
| 11 | MACRO | buffer 256 byte, repeatCount uint16-LE, sự kiện 4 byte |
| 12 | USERPIC | màu từng phím: 7 gói × 56 byte (staging + commit) |
| 16 | FN | dữ liệu lớp Fn |
| 17 | SLEEP | hẹn giờ ngủ/tắt đèn |
| 18 | USER GIF | GIF người dùng cho đèn phím |
| 27/28/30 | CALIBRATION | 27 bật/tắt báo cáo hiệu chuẩn, 28 mốc min, 30 mốc max (xem §6.3) |
| 37 | TFT | nạp ảnh màn hình TFT (chunk 56 byte) |
| 40 | OLED CLOCK | đồng hồ OLED |
| 41 | SCREEN 24BIT | nạp ảnh màn hình màu 24-bit (chunk 56 byte) |
| 101 (0x65) | MULTI_MAGNETISM | toàn bộ cấu hình Hall-effect — xem §6 |
| 143 (0x8F) | GET_INFOR (chỉ đọc; = 15+128) | **dev_id 32-bit LE** + phiên bản firmware |
| 157 | GAMEPAD MODE | chế độ tay cầm |
| 165/169 | SCREEN PREPARE | chuẩn bị nạp màn hình, poll tới khi `resp[1]==1` (tối đa 10 × 100 ms) |
| 172 | FLASH ERASE | xoá flash, ACK `AA AA 55 55` |
| 230 (0xE6) | GET_FEATURE_LIST (chỉ đọc) | bitmap tính năng, `resp[1]` phải = 0xAA |

### 5.3 Các họ phụ [CODE]
- **pan108:** bảng opcode dịch chuyển riêng (radio/2.4G), không dùng chung số với ry5088.
- **Họ rút gọn (yc312x):** tập lệnh con của ry5088.
- **OLED bootloader:** opcode 48 với magic `55 AA 55 AA` (= 0x55AA55AA — cùng hằng số mailbox vào bootloader mà repo mở ghi nhận), nạp flash TFT qua opcode 50.
- **Hitbox:** SOCD qua opcode 104/105.

---

## 6. Khối Hall-effect (RY5088) — phần quan trọng nhất

Toàn bộ cấu hình từ tính đi qua **một lệnh gộp: SET 101 (0x65) / GET 229 (0xE5)**, với **sub-opcode ở byte[1]** chọn mảng dữ liệu (structure-of-arrays theo từng phím/slot). [CODE — hai bundle độc lập khớp nhau]

| Sub-op | Tên | Nội dung / mã hoá |
|---|---|---|
| 0 | Actuation (press travel) | điểm kích hoạt từng phím, uint16-LE × hệ số |
| 1 | Lift (release travel) | hành trình nhả |
| 2 / 3 | RT press / RT lift | độ nhạy Rapid Trigger khi nhấn / khi nhả |
| 4 | DKS start | điểm bắt đầu Dynamic Keystroke |
| 5 | MT | Mod-Tap: thời gian lưu theo **lượng tử 10 ms** (ms/10) |
| 6 | Dead zone (đáy) | vùng chết cuối hành trình |
| 7 | **Mode byte** | **bit7 = cờ Rapid Trigger bật/tắt**; `mode & 0x7F`: 0=normal, 2=DKS, 3=MT, 4=TGL hold, 5=TGL dots, **7=Snap (SOCD)** |
| 8 | DKS thresholds | 4 điểm ngưỡng DKS cho từng phím |
| 9 | Snap pair | cặp phím SOCD — ghi **đối xứng** cho cả hai phím trong cặp |
| 10 | DKS table | bảng DKS toàn bàn |
| 251 | Top dead zone | vùng chết đầu hành trình (chỉ firmware ≥ 1024 ở bản Akko) |
| 252 | Switch type | loại switch đang cắm (để chọn đường cong hiệu chuẩn) |
| 254 / 255 | Realtime read | đọc giá trị hành trình thời gian thực (dùng khi hiệu chuẩn/xem trước) |

**Scaling hành trình [CODE]:** giá trị lưu = mm × hệ số, trong đó hệ số phụ thuộc phiên bản firmware: **10, 100 (0.01 mm — phổ biến nhất), 200 (0.005 mm), 1000 (0.001 mm)**; uint16-LE. Dải actuation theo model: đa số **0.1–3.3 mm**; FUN60 Ultra TMR và FUN60 MAX: **0.1–3.4 mm**. Preset UI "gaming" của Akko: actuation 0.5 mm + RT 0.5/0.5 mm.

**Hiệu chuẩn (calibration) [CODE]:** chỉ khi cắm dây — opcode 28 (ghi mốc min) → chờ ~2 s → opcode 30 (ghi mốc max), trong lúc đó poll sub-op 254 để hiển thị hành trình realtime; opcode 27 bật/tắt luồng báo cáo hiệu chuẩn. Dữ liệu hiệu chuẩn Hall nằm ở vùng flash riêng của phím (0x08032000 theo repo mở) và **không bị xoá khi flash lại firmware**.

**Ghi chú trung thực:** "Astrolink" trong tài liệu MonsGeek chỉ là tên switch (星引力) ở lớp i18n — không phải giao thức riêng. Chuỗi `royuan_tool` cho **0 kết quả** trong toàn bộ JS của driver: công cụ flash `royuan_tool.exe`/`ry_upgrade.exe` là tiện ích riêng hãng phát kèm trang firmware, không nằm trong driver.

---

## 7. Giao thức màn hình [CODE]

| Model | Màn hình | Đường nạp |
|---|---|---|
| Akko MOD007 V5 HE | 128×128 | lệnh 37/41 |
| Akko 5098B | 172×320, mode "16" | lệnh 41 |
| Akko 5098 V5 | 420×116 | lệnh 41 |
| KF105/KF106 (bảng Akko) | 240×240 | lệnh 37/41 |

- Pipeline: ảnh/GIF → canvas → cắt theo bounding-box các pixel khác đen → sắp xếp **column-major** → mã hoá **RGB565 big-endian** (mode "16") hoặc **RGB888** (mode "24"). Không thấy nén trên đường này.
- Trình tự: lệnh prepare **165/169** (layout byte của trường độ dài/toạ độ khá lạ, đã ghi nguyên trạng trong findings) → poll tới khi `resp[1]==1` → gửi dữ liệu bằng SET 37/41 theo **chunk 56 byte**.
- Phần hoạt ảnh/nhạc/ambient do các service native đảm nhận (rhythm/screen-capture chạy ở PC rồi stream xuống qua cùng kênh lệnh đèn/userpic) — driver không có opcode "stream video" riêng cho màn hình ngoài cơ chế chunk trên.

---

## 8. Đối chiếu với mã nguồn mở ry5088 (dot-agi/ry5088-flasher, MIT)

Repo mở tái dựng độc lập giao thức RY5088 từ trước; đối chiếu từng điểm với mã driver chính hãng:

| Nội dung | Repo mở đã có | Driver chính hãng xác nhận / cộng thêm |
|---|---|---|
| Định danh USB | 3151:5030 (0xFFFF, 64B Feature), bootloader 3151:502A (0xFF01) | **Khớp** — và cộng thêm: PID 0x5030 dùng chung, định danh model bằng dev_id qua 0x8F (repo mở cũng kết luận y hệt: "PID is not a model discriminator") |
| Checksum | Bit7 ở byte 7, Bit8 ở byte 8 cho LED 0x07/0x08, tổng ≡ 0xFF | **Khớp từng chữ** — công thức JS `255-(sum&0xFF)` tương đương |
| Quy luật đọc/ghi | GET = SET \| 0x80 | **Khớp** |
| Bảng opcode application | 0x8F infor, 0x04 profile, 0x03 polling, 0x06 debounce, 0x09 option, 0x05/0x07/0x08 LED, 0x0A keymap, 0x0B macro, 0x0C userpic, 0x11 sleep/Fn, **0x65 magnetism** | **Khớp** — driver cộng thêm: 165/169 screen-prepare, 172 flash-erase + ACK magic, 230 feature list, 157 gamepad, 18 user GIF, 40 OLED clock, dongle F6/F7/FC, họ phụ ry6609/ry6602/yc312x/pan108, hitbox 104/105 |
| Magnetism 0x65 | "structure-of-arrays theo sub-command; đơn vị centi-mm (200 = 2.00 mm)" | **Khớp và chi tiết hoá hẳn:** đủ 16 sub-op (0–10, 251, 252, 254, 255), mode byte bit7 = RT, giá trị snap = 7, hệ số tỉ lệ mở rộng 10/100/200/1000 theo firmware, dải 0.1–3.3/3.4 mm theo model |
| Vào bootloader / DFU | 0xC5 0x3A → 0x7F + `55 AA 55 AA`; DFU frame 0xBA 0xC0/0xC2, checksum 24-bit, ACK 0xAB (0x55 OK/0xAA FAIL); flash dest 0x08005000, header `AT32F405 8KMKB` | Driver không tự flash qua đường này (dùng tool royuan_tool/ry_upgrade riêng) nhưng hằng số magic `55 AA 55 AA` xuất hiện trong nhánh OLED bootloader của driver — **nhất quán** |
| Cloud firmware API | POST `api2.rongyuan.tech:3816/api/v2/get_fw_version` {dev_id}; GET `/download/<file_path>` (ZIP chứa `firmwareFile.bin`) | **Khớp domain** — driver dùng chính api*.rongyuan.tech |
| Firmware tuỳ biến AT32F405 | Có (profile-driven, **chưa bring-up xong trên phần cứng thật**) | — |

**Kết luận đối chiếu:** với họ RY5088, phần giao thức lõi **không còn là bí mật** — repo mở + mã driver rò rỉ qua bundle JS bổ sung cho nhau gần như hoàn chỉnh. Giá trị cộng thêm lớn nhất của đợt mổ này là: (1) bảng model/PID/fw-id để nhận diện đúng SKU, (2) chi tiết sub-op magnetism và scaling theo firmware, (3) giao thức màn hình, (4) các họ phụ ngoài RY5088.

---

## 9. Khoảng trống & giới hạn (nói thẳng)

- Bảng ánh xạ **fw-id → model đầy đủ của Akko** chưa trích trọn từ JS (MonsGeek đủ 52 model kèm id; Akko mới chắc chắn ở mức VID/PID/layout — xem `devices.json`, các mục Akko thiếu `fw_id` là thiếu thật, không điền đoán).
- Ý nghĩa từng bit của 4 byte `triggerModes` trong DKS: JS coi là opaque — chưa đào sâu.
- Cột `connection` trong devices.json một phần suy từ hậu tố tên nội bộ (`_1m`/`_3m`) — đã gắn nhãn [SUY LUẬN] trong findings gốc.
- Report ID / usage page không hard-code trong JS (native suy từ descriptor) — giá trị 0xFFFF lấy từ descriptor mà repo mở công bố cho cùng nền tảng; phía driver chỉ xác nhận gián tiếp qua WebHID filter (usagePage 0xFFFF, usage 2).
- Phân tích tĩnh không chứng minh được hành vi lúc chạy (điều kiện kích hoạt gửi cloud, nội dung chính xác của payload đăng nhập) — ngoài phạm vi đợt này.
- Chưa mổ sâu: nhánh chuột (cùng nền tảng, ngoài yêu cầu), giao thức BT chi tiết ngoài tiền tố 0x55 và pacing, định dạng file firmware tải từ cloud.

---

## 10. Phụ lục: cây file bằng chứng

- `raw/` — hai ZIP gốc nguyên trạng + hash trong `analysis/hashes.txt`.
- `work/akko/`, `work/monsgeek/` — installer đã giải nén (NSIS → app-32.7z → Electron app); `work/monsgeek-web/` — bundle web driver.
- `analysis/akko-findings.md`, `analysis/monsgeek-findings.md` — báo cáo chi tiết kèm snippet cho từng claim.
- `analysis/akko-devices.json`, `analysis/monsgeek-devices.json` — bảng thiết bị theo từng app; `devices.json` (thư mục gốc gói này) — bản gộp 172 mục.
- `analysis/ry5088-protocol-upstream.md`, `analysis/ry5088-firmware-upstream.md` — tài liệu của repo mở dot-agi/ry5088-flasher dùng để đối chiếu.
