# Hồ sơ mổ xẻ driver DAREU All-in-One / "A+" — họ EK75 / EK75 RT

> Phân tích tĩnh 100% (static analysis). Không chạy/cài bất kỳ binary nào, không kết nối thiết bị thật.
> Ngày thực hiện: 2026-11-27. Ký hiệu: **[CODE]** = trích trực tiếp từ mã nguồn/bằng chứng file; **[SUY LUẬN]** = suy luận từ bằng chứng, chưa chắc chắn tuyệt đối.

---

## 1. Nguồn tải (chỉ domain chính hãng DAREU)

| Hạng mục | URL chính xác |
|---|---|
| Trang driver chính thức (CN) | `https://cn.dareu.com/drivers/` |
| Trang driver chính thức (global) | `https://dareu.com/drivers/` |
| Gói nền tảng 1 (zip, đặt tên theo SHA-256) | `https://assets.dareu.com/dareu/drivers/sha256/95/954ef4f32fbd8143a950e9c875f73cc126d4bd570c49f7216aefaecf314ff5bb.zip` |
| Gói nền tảng 2 (zip, đặt tên theo SHA-256) | `https://assets.dareu.com/dareu/drivers/sha256/ef/ef4bb854e931a2e6adda9f90c8f498da90462fb5600e53e9492dc58528cb6faa.zip` |
| Gói riêng EK75 RT | `https://assets.dareu.com/products/%E9%94%AE%E7%9B%98/EK75%20RT/zip/EK75%20RT_%E9%A9%B1%E5%8A%A8.zip` |
| Gói riêng EK75 | `https://assets.dareu.com/products/%E9%94%AE%E7%9B%98/EK75/zip/EK75_%E9%A9%B1%E5%8A%A8.zip` |
| **All in One WEB driver** (nguồn giao thức chính của hồ sơ này) | `https://dr.dareu.com/` — tiêu đề trang: "DAREU All in one WEB" |
| Cấu hình từng thiết bị (JSON theo PID) | `https://dr.dareu.com/products/<PID-HEX>/<PID-HEX>.json` |
| UniHub (driver web thế hệ mới, họ riêng) | `https://unihub.dareu.com/` + registry `https://unihub.dareu.com/explore/api/explore/root-registry/standard` |

Trên trang CN, hai thẻ driver nền tảng được mô tả nguyên văn: thẻ **"ALL IN ONE 驱动"** — "打开网页驱动，连接并设置设备" (mở web driver, kết nối và cài đặt thiết bị); thẻ thứ hai — "下载驱动安装包，安装后使用" (tải gói cài đặt driver, cài xong dùng) **[CODE — cn-drivers.html]**.

**Lưu ý nguồn:** CDN `assets.dareu.com` (Tencent CDN) trả **HTTP 403** cho mọi yêu cầu từ môi trường phân tích này (đã thử kèm Referer/Sec-Fetch/range/http), nên **binary cài đặt desktop chưa tải được** — xem mục 7. Toàn bộ giao thức dưới đây được trích từ **All in One WEB driver chính thức tại dr.dareu.com** (JS không nén sang tên biến nhưng đọc được), là cùng thế hệ driver "All in One/A+" mà manual EK75 RT yêu cầu **[CODE — manuals.plus bản scan manual EK75RT: "download and install the DAREU A+ driver"]**.

## 2. Kiểm toán an toàn

**Đối tượng kiểm toán được:** toàn bộ tài sản web chính thức (dr.dareu.com: HTML + 16 file JS + 190 JSON cấu hình; unihub.dareu.com: bundle chính + registry). **Chưa kiểm toán được:** binary installer desktop (không tải được, mục 7).

SHA-256 (trích):
```
16bc6048a2cc8c2930748e6e9b2ed03fa27ced7e7d1e830b836f4b416f30a442  unihub/index-registry.js
14cf8b449bbd1fe11c5316697624d3368026226c0397d125a4ff8c07ad299cee  drweb/js/device/tgdevice.js
6218fceeae29ea04dfa923a1ab1bd3fbec82f5d039a9fd04050fe5d172ba1eb4  drweb/js/device/ohiddevice.js
38e7374ae10107134000b12c455732ea4cbf804e2fe7f54cffadebbf04f400b8  drweb/js/device/jmdevice.js
7667a603f78a98de4bf3e0dd3303ecdaade5b15dda75ba43239fae70b48d4b65  drweb/js/global.js
```
(bản đầy đủ: `analysis/hashes.txt`)

Endpoint ngoài duy nhất tìm thấy trong toàn bộ mã web **[CODE]**:
- `www.googletagmanager.com` — trong `drweb/index.html` với ID dạng placeholder nguyên văn `GA_TRACKING_ID` (không phải ID thật → tag không hoạt động) **[CODE]**.
- `beian.miit.gov.cn` — link chân trang (số đăng ký ICP của site Trung Quốc).
- `localhost` / `127.0.0.1` — cầu nối tới Windows service cục bộ (mục 3), cổng mặc định **8081**.

Quét chỉ dấu độc hại (miner — coinhive/cryptonight, webhook Discord/Telegram, pastebin, rút gọn link, credential stealer): **0 kết quả** trong cả drweb lẫn bundle UniHub **[CODE]**. Bundle UniHub không chứa SDK analytics nào (các hit "segment" chỉ là mã hình học) **[CODE]**.

**Kết luận kiểm toán: SẠCH ở phạm vi tài sản web chính thức.** Không có bằng chứng telemetry bên thứ ba đang hoạt động, không miner, không đánh cắp credential. Phần binary desktop chưa có kết luận (chưa tải được) — không được suy diễn là sạch hay bẩn.

## 3. Kiến trúc driver

- Bản desktop "A+" dùng **chung giao diện web** với bản WEB: `webservice.js` cho thấy UI nói chuyện với một **Windows service cục bộ** qua WebSocket `ws://localhost:8081` và HTTP `http://localhost:8081/api/<cmd>` (các lệnh: `reset`, `setkeeprhythm`, `setpause`, `stopaudiocaptrue`) — service này là tầng vận chuyển HID native + thu âm cho Music Rhythm **[CODE — webservice.js]**.
- Bản WEB thuần dùng **WebHID** trực tiếp: bộ lọc thiết bị chỉ gồm **VID 0x260D (9741)** với các usagePage 0xFF02/usage 2, 0xFF01/usage 0, 0x000C/usage 1, 0xFFA0/usage 1, 0xFF04/usage 2 **[CODE — global.js `DEVICE_FILTERS`]**.
- Danh sách PID hợp lệ (`AVAILABLE_PID_LIST`) có **190 PID**, tất cả cùng VID 0x260D **[CODE]**. Mỗi PID có một JSON cấu hình riêng trên dr.dareu.com chứa: tên model, `FwType` (họ firmware/giao thức), layout phím, vùng LED, thông số hành trình (KeyTravel)… **[CODE]**.
- `FwType` quyết định class giao thức và thông số report **[CODE — device.js `CreateHidDevice` + `HidUsbDeviceBase`]**:

| FwType | Họ | ReportId | Cỡ report | Transport |
|---|---|---|---|---|
| 0 | **TG** | 0 (dongle: 7) | 64 B | Feature report |
| 1 | HS (biến thể TG) | 10 | 64 B | Feature report |
| 2 | **JM** | 8 | 16 B | Output report + input report |
| 3 | QF (con cháu JM) | 8 | 16 B | như JM |
| 4 | **OHID** (không dây) | 0 | 64 B (logic) | Feature report |
| 5 | **OHID** (có dây) | 0 | 64 B (logic) | Output report + input report |
| 6 | **TG-RT** (Hall-effect) | 0 (dongle: 7) | 64 B | Feature report |
| 7 | Dongle TG-OHID | — | 64 B | Feature report |

Phân bố trong 187 JSON tải được: FwType 2 (JM) — 76; 6 (TG-RT) — 61; 0 (TG) — 26; 1 (HS) — 13; 4 (OHID) — 10; 7 — 1 **[CODE]**.

## 4. Bảng thiết bị & tâm điểm EK75

File `devices.json` kèm theo chứa toàn bộ **187 thiết bị** (94 bàn phím, 69 chuột, 24 dongle) trích từ JSON chính hãng. 3 PID trong danh sách (0x0082, 0x0215, 0x1092) trả 404 — có trong `AVAILABLE_PID_LIST` nhưng không có JSON cấu hình **[CODE]**.

**EK75 xuất hiện dưới một tên sản phẩm duy nhất trong driver, gồm 6 PID** (model nội bộ chung `TK51G`, 83 phím + núm) **[CODE]**:

| PID | FwType / Họ | Pin | Bản chất |
|---|---|---|---|
| 0x0045 | 0 / TG | Có | EK75 cơ **ba chế độ** (tri-mode) |
| 0x0101 | 0 / TG | Có | EK75 cơ ba chế độ (biến thể) |
| 0x0107 | 6 / TG-RT | Không | **EK75 RT** (Hall-effect, có dây) |
| 0x0115 | 6 / TG-RT | Không | EK75 RT (biến thể) |
| 0x0131 | 6 / TG-RT | Không | EK75 RT (biến thể) |
| 0x0132 | 6 / TG-RT | Không | EK75 RT (biến thể) |

- EK75 RT **dùng chính driver All-in-One/A+ này** (FwType 6 = class `TgRtKeyboard`) — trả lời trực tiếp câu hỏi của đề bài **[CODE]**.
- Thông số hành trình EK75 RT từ JSON chính hãng: dải 0.3–3.3 mm, mặc định 1.5 mm, bước 0.02 mm; `TravelVersion` không khai báo → mặc định **1** **[CODE — 0107.json + tgdevice.js `InitParameters`]**.
- Núm xoay (knob) của EK75 được ánh xạ thành 2 KeyID trong keymap: **171 = Volume +**, **172 = Volume −** (function multimedia, usage 233/234) **[CODE — 0107.json]**.
- Không có model nào trong 187 JSON có `HasLcd: true` → thế hệ All-in-One WEB **không có bàn phím màn hình LCD**; lớp lệnh LCD của họ TG chỉ có độ sáng + ngôn ngữ (mục 5.2) **[CODE]**.

## 5. Giao thức

### 5.1 Họ TG / TG-RT (FwType 0/1/6/7) — họ chính của EK75 & EK75 RT

**Transport [CODE — tgdevice.js `TgHidDevice.CommandProcess`]:** Feature report 64 B (`sendFeatureReport`/`receiveFeatureReport`, ReportId theo bảng mục 3). Sau khi gửi, phía host **poll** `receiveFeatureReport` tối đa 20 lần, nghỉ 10 ms; qua dongle thì chờ thêm 50 ms trước khi poll. **Không có checksum** trong khung TG **[CODE]**. ACK = gói trả lời có **nibble thấp của byte trạng thái == 2**.

**Khung (header 6 B + payload từ byte 6) [CODE — `DATA_INDEX`]:**
```
[0] HDR_STATUS   = TargetId (qua dongle: target << 4 — xem GetWirelessConnectState)
[1] HDR_SIZE     = số byte ý nghĩa (header+payload, tuỳ lệnh)
[2] HDR_CLASS    = lớp lệnh
[3] HDR_COMMAND  = mã lệnh | 0x80 nếu là GET (GET_CMD=128), SET = lệnh gốc
[4] HDR_PROFILE  = profile id
[5] (không dùng trong các lệnh đã trích)
[6..] payload
```

**Bảng lớp lệnh (`CMD_CLASS`) [CODE]:** 0 Device, 1 Key, 2 Button, 3 Lighting, 4 Sensor, 5 Profile, 6 Macro, 7 Power, 8 Audio, 9 DFU, 10 Test, 11 LCD, 12 Flash, 13 **Magnetic Axis**, 255 End.

**Lệnh tiêu biểu [CODE]:**
- Device: 0 FW_VER, 1 HW_VER, 2 SN, 3 DEVICE_MODE, 4 POLLING_PERIOD, 5 EDITION, 6 MID, 7 NAME, 8 RESET, 16–19 Dock FW/SN/Edition/MID, 32 WIRELESS_CONNECT_STATUS, 33 RSSI, 34 RF_PROTOCOL_VER, 35 WIRELESS_PAIR.
- Key: 0 ID_LIST, 1 ATTRIBUTE, 2 DEBOUNCE, 3 **ASSIGN**, 4 ANALOG_ACTUATION_POINT, 5 FN_LOCK, 6 WIN_LOCK_MAC_STATUS, 7 PERFORMANCE, 8 **BULK_ASSIGN**, 9 SOCD_STATUS.
  - ASSIGN: payload `[keyId, layer, functionId, data0..data4]` (bản ghi 8 B/phím); BULK_ASSIGN đóng gói nhiều bản ghi 8 B kèm count ở payload[0] **[CODE]**.
- Lighting: 0 ID_LIST, 1 ATTRIBUTE, 2 **EFFECT**, 3 BRIGHTNESS, 4 FRAME, 5 CAL_DATA, 6 CHARGE_CTRL, 7 DPI_STAGE_INDICATOR_COLOR, 8 CUSTOM.
  - EFFECT (SET): payload `[regionId, effect, flag, speed, colorCount, RGB×n…]` **[CODE]**. EFFECT/ATTRIBUTE/ID_LIST dùng cơ chế đa gói (dưới đây). Hai lệnh FRAME (4) và CUSTOM (8) **có trong enum nhưng bản WEB không có mã gửi** — stream từng phím theo thời gian thực không thấy trong bản web **[CODE — vắng mặt trong tgdevice.js]**; Music Rhythm đi qua service cục bộ (`setkeeprhythm`) thay vì WebHID **[CODE — webservice.js]**.
- Profile: 0 ID_LIST, 1 CREATE, 2 DELETE, 3 ACTIVE, 4 NAME, 5 RESET.
- Macro: 0 STORAGE_INFO, 1 ID_LIST, 2 CREATE (payload `[id, sizeBE16]`), 3 DELETE, 4 NAME, 5 MEMORY, 6 MINI_DELAY — dữ liệu macro truyền bằng cơ chế đa gói, địa chỉ/tổng dạng big-endian **[CODE]**.
- Power: 0 BAT_STATUS (payload trả `[status, level, maxLevel, critical]`), 1 TIME_2_DIM, 2 TIME_2_SLEEP (`[control, secondsBE16]`), 3 LOW_INDICATOR_CTRL, 4 MAX_LED_BRIGHTNESS, 5 ADC, 6 USB_TIME_2_SLEEP **[CODE]**.
- DFU: 0 ATTRIBUTE, 1 PREPARE, 2 FILE, 3 CRC, 4 EXIT, 5 ENTRY. Test: 0 USB_VID_PID, 3 USB_RESET, 64 FACTORY_DEFAULT_SETTING… Flash: 0 ATTRIBUTE, 1 TABLE_ACCESS, 2 MEMORY_ACCESS. LCD: 1 BRIGHTNESS, 3 LANGUAGE **[CODE]**.

**Cơ chế đa gói (multi-packet) [CODE — `GetMultiPacketCmd`/`SetMultiPacketCmd`]:** dữ liệu lớn chia块 **48 B/gói** (`TG_BLOCK_SIZE = 48`); đầu mỗi lần truyền có trường tổng độ dài + offset dạng big-endian với độ rộng 1/2/3/4 byte (tham số `a`); `HDR_SIZE = chunk + header phụ + 2*a`.

### 5.2 Khối Hall-effect / Rapid Trigger (lớp 13 — Magnetic Axis) — EK75 RT

Bảng lệnh lớp 13 **[CODE — `CLASS_MAGNETIC_AXIS_CMD_LIST`]**: 0 KEY_FUNCTION, 1 NORMAL_TRIGGER, 2 SINGLE_TRIGGER, 3 RAPID_TRIGGER, 4 MT_FUNCTION, 5 TGL_FUNCTION, 6 DKS_FUNCTION, 7 GLOBAL_KEY_TRAVEL, 8 ACD_TEST, 9 TRAVEL_TEST, 10 TRAVEL_CALIBRATION, 11 ST_FUNCTION (SOCD), 12 DEAD_ZONE_ADJUST, 13 MIXED_AXIS, 14 RT_ADAPTIVE, 16 GAME_MODE, 17 FUNC_AND_SOCD.

Đơn vị hành trình: giá trị thô × hệ số trong JSON (`GlobalTravelRange[5]`, với EK75 RT = **0.02 mm**). Ba biến thể mã hoá theo `TravelVersion` **[CODE — TgRtKeyboardHidDevice]**:
- v0: 1 byte/giá trị.
- v1 (EK75 RT): **uint16 big-endian**/giá trị. (v2: uint16 BE nhưng trường đầu chia đôi hệ số.)

Chi tiết payload **[CODE]**:
- **GLOBAL_KEY_TRAVEL (7):** GET/SET 1 giá trị toàn cục (v1: 2 B BE ở payload[0..1]).
- **KEY_FUNCTION (0):** GET với payload `[count, keyId…]` (mỗi phím chiếm 2 B trong khung), trả về functionId từng phím (0 Normal, 1 Single, 2 Rapid, 3 MT, 4 TGL, 5 DKS, 6 ST/SOCD — theo `MA_KEY_FUNCTION_ID`).
- **SINGLE_TRIGGER (2):** mỗi phím: `[keyId, travel]` — v1: bản ghi 3 B (keyId + uint16 BE).
- **RAPID_TRIGGER (3):** mỗi phím 3 giá trị **[initTravel, pressedTravel, releasedTravel]** — v1: bản ghi 7 B (keyId + 3×uint16 BE); v0: bản ghi 4 B.
- **DEAD_ZONE_ADJUST (12):** payload `[keyId, valueBE16]`; v2 tự nhân/chia 2 khi chuyển đổi.
- **TRAVEL_CALIBRATION (10):** SET payload `[1]` = vào chế độ hiệu chuẩn, `[0]` = thoát.
- **MT_FUNCTION (4):** `[1, keyId, trigger1, trigger2, delay]` — delay LE ở v0, BE ở v1+.
- **TGL_FUNCTION (5):** `[1, keyId, trigger1, delay(2B)]`.
- **DKS_FUNCTION (6):** bản ghi 10 B: `[1, keyId, trigger×4, condition×4]`.
- **ST_FUNCTION / SOCD (11):** `[1, keyId, groupId]`; xoá liên kết SOCD ghi groupId = 255. Trạng thái SOCD tổng còn có lệnh lớp Key #9 (SOCD_STATUS, payload 1 B).
- **MIXED_AXIS (13):** `[count, (keyId, axisType)…]`. **RT_ADAPTIVE (14):** 1 B 0/1. **GAME_MODE (16):** theo profile, 1 B.

### 5.3 Họ OHID (FwType 4/5)

**Transport [CODE — ohiddevice.js]:** report 64 B; có dây dùng output/input report (`sendReport`), không dây dùng feature report kèm nhịp chờ theo lệnh (25/100/200/250 ms). Đáp ứng thu qua hàng đợi input report, poll 20 ms × tối đa 20 lần.

**Khung [CODE — `OhidPackage`]:**
```
[0] Head = 0xAA
[1] Len  (0..60)
[2] Cmd  (ghi = cmd | 0x80 qua CmdWrite(); đọc = cmd gốc)
[3] Checksum = Σ (byte[i] + i) trên [head, len, cmd, data...] (mod 256 tự nhiên của byte đích — xem GetCheckSum)
[4..] Data (tối đa 60 B)
```
**Bảng lệnh (`CommandIndex`) [CODE]:** 0 ORDER, 1 SYNC, 16 KEY, 17 MT, 18 TGL, 19 DKS, 20 AKS, 21 TRIGGER, 22 LED_EFFECT, 23 RGB_KEY (từng phím), 24 RGB_MATRIX, 25 MACRO_DATA, 26 KEY_DATA_REPORT, 27 CURVE, 28 NORMAL, 29 SINGLE, 30 RT, 31 RT_SIMPLE, 32 MACRO, 33 MTP, 35 QUICK, 255 FAIL.
**ORDER (cmd 0, data[0] = order id) [CODE — `OrderIndex`]:** 2 SAVE_USER (lưu cấu hình — hầu hết lệnh SET đều gọi tiếp lệnh này), 3 RELOAD, 5 FACTORY, 7 QUERY_VERSION_HEX (FW = `Data[5]<<16|Data[6]<<8|Data[7]`), 9 QUERY_SN, 16 QUICK_MODE, 17 REPORT_RATE, 18–25 các lệnh RGB phụ, 32 QS_LOCKWIN, 36 QS_LINEAR (chế độ hành trình), 49 TRIGGER, 50 PROFILE_TAB (profile đang chọn = `Data[5]+1`).

**Khối trigger OHID [CODE]:** bản ghi từng phím, ghi xong phải SAVE_USER:
- NORMAL (28): 2 B/phím `[keyId, 0]`, tối đa 30 phím/gói.
- SINGLE (29): 4 B/phím `[keyId, 0, travelLE16]`, tối đa 15 phím/gói.
- RT (30): 5 B/phím `[keyId, 0, valueLE24]` (3 byte giá trị), tối đa 12 phím/gói.
- RT_SIMPLE (31): 4 B/phím `[keyId, 0, valueLE16]`.
- KEY (16) gán phím: `[keyIdLE16, functionId, valueLE16]`.
- LED_EFFECT (22): khối hiệu ứng đọc từ offset 4: màu tĩnh theo bảng màu tại `Data[8..39]`, Brightness `Data[40]`, Effect `Data[41]`, Speed `Data[42]`, SleepTime `Data[43]`, Open `Data[44]`, RgbSleep `Data[45]`, Flag `Data[46]` **[CODE]**.

### 5.4 Họ JM / QF (FwType 2/3)

**Transport [CODE — jmdevice.js `JmHidDevice`]:** output report 16 B (ReportId 8), đáp ứng qua input report 16 B, khớp theo byte lệnh đầu; poll 20 ms × tối đa 5 lần. Gói lệnh xây dạng mảng 17 B rồi bỏ byte đầu (report id): `[0]=0x08 nhóm, [1]=lệnh con, [3..4]=địa chỉ BE, [5]=số lượng/cờ, [6..]=dữ liệu, [cuối]=checksum`.
- **Checksum [CODE — `CheckSum`]**: `s = Σ các byte trước; trả về s < 55 ? 85 − s : 341 − s` (tương đương `(0x55 − s) mod 256` trong miền giá trị thường gặp **[SUY LUẬN]**).
- Lệnh con đã thấy: 1 GET_DEVICE_INFO (trả CID/MID/JmType ở `Data[9..11]`; request chứa 4 byte ngẫu nhiên), 2 SET_DRIVER_MODE, 3 CHECK_ACTIVE (đáp `Data[5]==1`), 7 WRITE_BUFFER, 8 READ_BUFFER.
- **Truyền buffer (keymap/macro…) [CODE]:** chia块 **10 B dữ liệu/gói**, địa chỉ BE tự tăng; khi ghi, count cộng 128 cho các gói không phải gói cuối (cờ "còn tiếp").
- QF (`QfHidDevice`) kế thừa nguyên JM, chỉ thêm cờ không dây **[CODE — qfdevice.js]**. HY (`HyKeyboard`) chỉ là subclass rỗng của KeyboardBase, không có tầng HID riêng trong bản web **[CODE — hydevice.js]**.

### 5.5 Họ phụ UniHub (ngoài phạm vi chính, ghi nhận)

UniHub (unihub.dareu.com, version.json = 1.1.0) là driver web thế hệ mới cho COOL68 V2, Ultra75, 雷电75 — dùng registry động: mỗi model một bộ `(vendorId, productId, usagePage=0xFF60, usage=97)`; VID gặp 0x372E, 0x260D, 0x369B (chi tiết trong `devices.json`) **[CODE — root-registry]**. Chưa mổ sâu giao thức UniHub trong đợt này.

## 6. Màn hình & núm xoay

- Núm EK75: không phải kênh riêng — núm là 2 KeyID 171/172 (Volume ±) trong keymap thường, gán lại được như phím **[CODE]**.
- Màn hình: không model nào trong catalog All-in-One WEB có LCD (`HasLcd` đều false); lớp LCD của TG chỉ gồm BRIGHTNESS (1) và LANGUAGE (3) **[CODE]**. (Mẫu có màn LED matrix như Ultra 68 thuộc hệ UniHub/OEM riêng — `oem.dareu.com` — không thuộc driver này.)

## 7. Khoảng trống trung thực (gaps)

1. **Binary installer desktop (A+ bản cài đặt) chưa tải/kiểm toán được**: `assets.dareu.com` trả 403 cho mọi lần thử tải từ môi trường này (kể cả kèm Referer trang drivers). Chưa có hash/chữ ký số của installer; mọi kết luận về desktop app đều suy ra từ bản WEB cùng thế hệ + `webservice.js` (cầu localhost:8081) **[CODE phần cầu nối; SUY LUẬN phần "cùng mã"]**.
2. Hai gói zip nền tảng trên trang CN (954ef4…, ef4bb85…) chưa mở được nên **chưa khẳng định gói nào là "ALL IN ONE" desktop, gói nào là bản offline của web driver**; phần mô tả thẻ trên trang cũng không gắn URL hiển thị. Gói riêng theo model (EK75_驱动.zip, EK75 RT_驱动.zip) nhiều khả năng chứa cùng một installer All-in-One **[SUY LUẬN]**.
3. Byte [5] trong header TG chưa thấy được gán trong các lệnh đã trích — mục đích chưa rõ.
4. LED_CMD_FRAME (4) / LED_CMD_CUSTOM (8) của họ TG: có mã lệnh nhưng không có mã gửi trong bản web → định dạng stream từng phím của TG (nếu firmware hỗ trợ) chưa trích được.
5. `HDR_STATUS`/TargetId qua dongle: suy ra target = `(vị trí+1) << 4` từ `GetWirelessConnectState`; chưa đối chứng với luồng gửi qua dongle thực tế.
6. Họ UniHub mới chỉ trích registry + transport-level (usagePage/usage); bảng lệnh bên trong chưa mổ.
7. Checksum JM: công thức trích nguyên văn từ mã; dạng rút gọn `(0x55 − Σ) mod 256` là suy luận toán học, chưa kiểm chứng bằng thiết bị.
8. Mọi giá trị VID/PID/địa chỉ trong hồ sơ đều từ mã/JSON chính hãng; chưa có kiểm chứng trên phần cứng thật.
