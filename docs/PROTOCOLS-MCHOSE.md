# DOSSIER — MCHOSE "M HUB": mổ xẻ & trích xuất giao thức bàn phím

**Ngày:** 27/11/2026 (JST). **Phương pháp:** CHỈ phân tích tĩnh — không chạy/cài app, không kết nối thiết bị.
**Quy ước:** **[CODE]** = thấy trực tiếp trong mã/file, kèm nguồn. **[SUY LUẬN]** = suy ra từ nhiều điểm. "Không tìm thấy" = đã tìm và không có.
**Phụ lục chi tiết:** `analysis/protocol-notes.md` (stack BY + legacy + pin, ~44KB), `analysis/cz-protocol-notes.md` (stack từ tính GLW + QHW), `analysis/devices-raw.md` (bảng thiết bị thô), `devices.json` (49 bàn phím, dạng máy đọc).

---

## 1. Nguồn tải — chính hãng, đã xác minh

- Trang chính thức: `https://www.mchose.store/pages/mchose-hub` → nút tải trỏ về CDN của hãng: **`https://cdn.mchose.com.cn/MCHOSE_HUB_installer.zip`** (Aliyun OSS `mchosesoft.oss-cn-shenzhen.aliyuncs.com`) [CODE]
- File: `MCHOSE_HUB_installer.zip` 160.910.506 B, SHA-256 `dfb0b3e86a8714bd7aac9d0ceb4ba159dfc412ea59b78e22c483de81723aa2e7`; bên trong là `M HUB installer.exe` (NSIS, 161.059.240 B), SHA-256 `73fa4498599fb1d71377e0794138c03fa45a38fd6ee13e6274e7ecd36ad2b8fc`.
- Cùng một frontend driver chạy trên web: `https://www.mchose.com.cn/` (SPA, `#/connectDevice`) — bản desktop chỉ là vỏ chứa nó.

## 2. Kiểm toán an toàn — SẠCH (ở mức tĩnh)

- **Chữ ký:** installer và `MCHOSE HUB Watcher.exe` được ký **DigiCert Trusted G4 Code Signing** cho **深圳市志远智能科技有限公司** (Shenzhen Zhiyuan Intelligent Technology, Thâm Quyến/Quảng Đông, mã DN 91440300326242237G) [CODE — trích từ PKCS#7]. Riêng `MCHOSE HUB.exe` (Electron chính) và Launcher **không có chữ ký**.
- **Endpoint:** toàn bộ là hạ tầng của hãng — `cdn.mchose.com.cn` (529 tham chiếu), họ `maicong.cn` (www/chat/mall/serverapi/ioss — OSS upload cho ảnh feedback/diễn đàn), `mchose.store`, OSS Aliyun (`mchose-bbs`). **Không có tracker/analytics bên thứ ba** (các hit "amplitude/firebase" là dương tính giả: thuộc tính Lottie và danh sách thuộc tính SVG). Không dấu hiệu miner/đánh cắp credential trong strings và kiểm kê file.
- **Bề mặt native:** driver C-Media APO (tai nghe) có ký số riêng; thư viện nâng cấp firmware JieLi (JL) gọi qua worker; các DLL "executor" cho dongle **không nằm trong gói cài** — app tải/giải nén lúc chạy (`workerAsarExtract`) nên phần này chưa kiểm chứng được [GAP].
- **Rò rỉ môi trường dev trong bytecode:** địa chỉ test nội bộ `192.168.150.14/.85/.157/.206`, `drivertest.maicong.cn`, `localhost:5173` — vô hại nhưng cho thấy bản đóng gói chưa được rà soát kỹ.
- `app-update.yml` là placeholder (`example.com`); cập nhật thật đi qua `updater.ps1` (đọc `pending-install.json`, thay `resources/app`).

## 3. Kiến trúc & tech stack

- **Electron** (electron-builder/NSIS), package `MCHOSEHUB` v1.0.0; main process biên dịch thành **V8 bytecode (.jsc)** — logic main không đọc trực tiếp được, nhưng string literal vẫn trích được. Dependencies đáng chú ý: `koffi` (FFI gọi DLL), `usb` (libusb), `@grpc/grpc-js`, `axios`, `win-audio`.
- **Toàn bộ logic driver bàn phím nằm ở web app** (Vue 3 + Vite, WebHID): bundle chính `main-8Ejovhva.js` 7,6 MB + ~75 chunk (mỗi chunk model chỉ chứa bảng layout phím) + **SDK từ tính "CZ"** nạp riêng từ `./cizhou/CZ_SHARED_DATA/` (loader + `main.*.js` 783KB + manifest) + cấu hình runtime `configCenter/custom/cardList.json` (ZIP nén base64) chứa định danh các model CZ.
- Tiến trình phụ (bytecode): `workerGetDevice` (koffi → DLL executor: `CreateExecutorHandle/runAllTask/read_online_device`, `dongle_client_info_t`), `workerUpgrade` (thư viện **JieLi JL_***: `JL_upgradeDevice`, `JL_queryDevicePidVid`…), `sdk-worker` (C-Media 2025/2026 audio SDK cho tai nghe).
- `libs/` trong gói cài = driver âm thanh C-Media cho tai nghe (G9, K20/K20 GT/Pro, S9 Pro, VT008/009, X9) + `MCHOSE HUB Agent/Notifier.exe`; `native-addons/` = core-audio, game-analysis, input-simulator, raw-input, visual-capture (tính năng kiểu GameSense: bắt hình + giả lập phím), message-notifier.
- **Driver không có đường Bluetooth**: chuỗi "Bluetooth" xuất hiện 0 lần trong bundle chính — chỉ có dây + receiver 2.4G [CODE].

## 4. Thiết bị — 49 bàn phím, 2 họ lớn

Bảng đầy đủ: `devices.json`. Tóm tắt:

- **Họ "BY" (cơ, 18 model)** — G87, G87 V2 (1K), K99/K99 V2, G98/G98 Pro/G98 V2, UT98, X75/X75 V2, GX87/GX87 V2, K87, KX75, G75 Pro, G75 V2 (bản cơ), Z75/Z75S. Nhận diện bằng **(PID × productName)**: nhiều model dùng chung PID — `0x258A:0x010C` (nền tảng Sinowealth quen thuộc) phục vụ tới 8 model, `0x05AC:0x024F` (giả lập Apple, giống chiêu dongle của AULA) cho K99+K87, `0x258A:0x01B9/0x01BB` cho G98 Pro/Z75/Z75S; tên USB là yếu tố phân định (`getDevName()`) [CODE].
- **Họ "CZ" (từ tính/Hall, 31 cấu hình)** — Ace 60/60 Pro/60X, Ace 68 (I/III/Pro/Air II/Air III/Air 2/GT/Turbo 8K/16K/V2 III), Ace 75 8K, Jet 75 I/II, Mix 87 I/III, Zero75X, God 60, GT 75, và các bản từ tính của G75 V2/G87 V2/G87 V3/G98 V3/K87 V3/K87S/K99 V3. Định danh lấy từ `cardList.json` (đã giải nén và trích trọn): mỗi model có cặp identity — kênh phím (usagePage 0x0001) + kênh vendor (0xFF00, hoặc 0xFF80 với nhóm V3). Hai thế hệ chip: **0x41E4** (PID 0x21xx, đời đầu) và **0x3837** (PID 0x30xx, đời mới); Ace 60/Pro có thêm identity phụ **0x19F5**. Receiver 2.4G dùng chung: `0x3837:0x3033`+`0x2010` (nhóm G/K) và `0x3837:0x1015/0x1017` (nhóm V3) — phân biệt model bằng productName [CODE].
- Ngoài bàn phím: chuột (A5/A7/K7/V7/G3 + dock), tai nghe/loa (S9 Pro, G9, V9, X9, K20… chủ yếu C-Media 0x291D:0x385D), và bộ điều khiển LED mainboard ASUS Aura (0x0B05:0x19AF, usagePage 0xFF72) [CODE].
- **Không model bàn phím nào có màn hình/TFT** — "GIF" trong driver là hiệu ứng đèn toàn phím (xem §5.4), `gif-preview.js` chỉ là component phát lại hiệu ứng trên phím ảo [CODE].

## 5. Giao thức — 4 stack, không phải 1

### 5.1 Stack A — "BY" (bàn phím cơ G/K/X/Z) [CODE — chi tiết: protocol-notes.md §1–4]

- **Có dây:** WebHID **feature report, reportId 6** (riêng G98 Pro/G98 V2/UT98/Z75/Z75S/G87 V2 dùng **9** — có một entry bị XOR-che tên "MCHOSE G87 V2 (1K)"); payload **cố định 519 byte, pad 0**; byte đầu của lệnh (`wiredCommand`) là chính reportId và bị cắt khi gửi. Ghi **không chờ ACK**; đọc có retry chọn lọc (Z75: 3 lần, backoff 100ms×n).
- **Không dây (2.4G):** reportId **19**, gói **19 byte** `[cmd, total, seq, YY, …14B dữ liệu…, sum]`; ACK từng gói (timeout 5s, 3 lần thử), đọc ghép mảnh theo seq (timeout 10s).
- **Bảng lệnh** (từ `configManagerMap`, mỗi lệnh có opcode riêng cho wired/wireless): get/set Macro, PassWord, KeySetting (khối 504B/layer), LightColor, Performance, Reset, DiyLight (đèn DIY **126 phím**, 3 mặt phẳng R/G/B), Battery.
- **Performance block:** polling rate (`rateVal`), latency, chế độ đèn, sleep, Win/Mac — bố cục byte trong protocol-notes §3.
- **Realtime lighting** (`keyboard-*.js`): có dây header `08 00 00 01 00 7A 01` + RGB xen kẽ tại `index-8`, throttle 40ms; không dây opcode **0x88**, tối đa 32 nhóm màu, trễ liên gói 2ms/15ms theo model.
- **Dongle:** không có header relay — thiết bị đích được định danh bằng tên `…2.4G Wireless-<Model>` + mật khẩu riêng theo model tra trong bảng hằng số `keyboardPwds` qua lệnh `getPassWord`.

### 5.2 Stack B — legacy `0x11/0x12` (chuột + macro/password của bàn phím) [CODE — protocol-notes.md §6]

- Gói **20 byte (0x11)** / **64 byte (0x12)**; **mọi byte XOR 0xFF, pad 0xFF**; đáp khớp bằng byte echo `(~b[1]) & 0xFF`; timeout 600ms, retry 6 (Entrance); hàng đợi `MessageQueue` (maxTaskTime 200 / maxWatchTime 150; riêng lệnh 0x67/0x63 retry tới 10).
- Macro của bàn phím ghi qua stack này (cmd `0x55/0x53`). Bảng lệnh chuột kèm theo: `0x11 0x03` receiver, `0x11 0x04` firmware version (ASCII), `0x12 0x67` full settings, `0x12 0x68` onboard slots…

### 5.3 Stack C1 — "GLW/CZ" (từ tính, mặc định cho Ace/Jet/Mix…) [CODE — cz-protocol-notes.md]

- WebHID, usagePage **0xFF00**, **reportId 0**, gói **64 byte**: `[0x55, cmd, 0, checksum, len, offLo, offHi, 0, payload ≤56B]`; trả lời bắt đầu **0xAA**, khớp cmd+offset; timeout mặc định 1s, retry 3 lần cách 20ms; truyền theo khối 56B, chỉ ghi khối thay đổi (delta-write).
- **Hall-effect (cmd 0xA0 đọc / 0xA1 ghi):** 1024B/profile = 128 phím × **8 byte**: switchType; priority/keyMode; **actuation 9-bit (lưu giá trị −1)**; **RT press/release 9-bit (lưu −1)**; precision press/release 2-bit; **dead zone trên/dưới 7-bit/phím**; dead zone toàn cục ở funcConfig byte 5. Phần lẻ 0.001mm nằm ở vùng custom-param 256B đóng gói nibble riêng.
- **Phím nâng cao:** DKS = cmd 0xA2/0xA3 (32 slot × 24B: 4 điểm hành trình + 4 action + mặt nạ 10-bit); MT = 0xA4/0xA5; TGL = 0xA6/0xA7; hiệu chuẩn (calibration) = 168/169/177. **Không có opcode SOCD riêng** trong GLW.
- **FuncConfig 64B:** byte 32 = thông tin pin RF, byte 4 (+byte 38 cho 2.4G) = chỉ số polling rate, tachyon mode, debounce.
- PID boot thường = PID thường +1 [SUY LUẬN từ bảng cardList].

### 5.4 Stack C2 — "QHW" (chỉ G87 V3 / G98 V3 / K87 V3) [CODE — cz-protocol-notes.md]

- usagePage **0xFF80** (+0xFF81 kênh nhạc), **reportId 77** (78 = nhạc); header 7 byte `[version, flags, len, cmd LE16, bizCode, sequence]` + **checksum XOR**; payload ≤23B/gói (boot 55B). Opcode 16-bit theo mẫu **0x02xx đọc / 0x03xx ghi / 0x09xx điều khiển** (~35 lệnh đã lập bảng).
- SOCD/RS tồn tại ở đây dưới dạng *loại phím nâng cao* (type 36/147) — không phải opcode riêng.
- **"GIF" thật của QHW:** hiệu ứng LED toàn phím dạng delta-frame `[diffCount, duration LE16, (matrixRC, R, G, B)×n]`, tối đa 36.800B, upload bằng cmd 0x030D tại offset 64, meta 7B + **CRC32 (đảo byte thứ tự)**, bỏ qua upload nếu CRC khớp.

### 5.5 Stack D — `deviceSystem/HidSession`

Kênh cho thiết bị âm thanh đời mới và một số thiết bị hệ thống; không phải kênh bàn phím chính (chi tiết trong protocol-notes §11).

## 6. Pin — leak công khai là ĐÚNG, đã xác minh 2 đường [CODE]

- **Stack A:** `getBattery` — có dây `06 87 …`, không dây `13 4a …`; parser: (có dây: 6 byte, byte đầu là space 0) → `batteryLevel: uint8`, tiếp 1 byte chứa `fullStatus` (bit 4) và `chargeStatus` (bit 4). Hằng số trong bundle phụ: `BLK_BATTERY_REPORT_ID=85 (0x55)`, `BLK_BATTERY_COMMAND_ID=101 (0x65)` thuộc module pin dùng cho thiết bị BLK.
- **Stack B:** lệnh `0x11 0x06` trả cả khối thông tin: VID/PID, `fwVersion: uint32`, connectMode và **pin** — tức pin đọc được ngay cả khi chưa ghép nối đầy đủ, khớp đúng mô tả của leak.
- App còn có cơ chế hiển thị pin theo cấu hình remote (`enableBatteryConfig`, version dongle/headset tối thiểu) — pin là tính năng hiển thị chính thức cho cả tai nghe (X9…).

## 7. Checksum — không có CRC ở các stack chính [CODE]

Chỉ có 3 tổng cộng dồn đơn giản: `crcHandler16` (tổng dạng hex, không mask), `crcHandler10` (tổng thập phân) — cả hai bất chấp tên gọi đều **không phải CRC**; `calculateChecksum` = `sum & 0xFF` (realtime lighting + gói wireless stack A). Ngoại lệ duy nhất có CRC thật: **CRC32** trong upload hiệu ứng GIF của QHW (§5.4), và checksum **XOR** ở header QHW. Thân hàm checksum của GLW nằm ở module không có trong tập file đã tải — giả thuyết `sum mod 256` kiểu lodash, **cần bắt gói thật để xác nhận** [GAP].

## 8. Khoảng trống & độ tin cậy

- Main process dạng bytecode: giao thức firmware-version/bootloader của stack A và đường OTA một phần nằm ở `.jsc` + DLL native (JL/G9OTA/K20ProOTA) — chưa trích được ở mức byte như các stack JS [GAP].
- DLL executor cho dongle (đường desktop ngoài WebHID) được tải lúc chạy, không có trong gói cài [GAP].
- Ánh xạ `rateVal` → Hz cụ thể và bảng keyMode/đổi raw→mm của GLW nằm ở UI driver (ngoài tập file CZ) [GAP].
- Một vùng mã bị obfuscate bằng string-array trong bundle chính (offset ~3918532) chưa giải mã; có thể chứa thêm cấu hình thiết bị phụ.
- Mọi giao thức trên vẫn cần **kiểm chứng trên phần cứng thật** trước khi ghi thật từ app khác (đúng quy trình an toàn thường lệ).

## 9. Đối chiếu với kho giao thức T Keyboard hiện có

- `0x258A:0x010C` ở đây xác nhận độc lập thêm một lần nữa: nền tảng Sinowealth dùng chung (F75 thường, Leobog Hi75…) — MCHOSE dùng cho 8 model và **bắt buộc phân biệt bằng productName**, đúng cảnh báo đã ghi trong roadmap.
- Dongle `0x05AC:0x024F` (giả lập Apple) xuất hiện ở cả AULA lẫn MCHOSE — quy ước chung của dongle Trung Quốc, không phải định danh riêng hãng nào.
- MCHOSE bổ sung 3 họ hoàn toàn mới cho roadmap: **BY** (cơ, feature-report 519B), **GLW** (từ tính, gói 0x55/0xAA 64B) và **QHW** (0xFF80, opcode 16-bit) — trong đó GLW đáng chú ý nhất vì bao phủ dòng Ace 68 đang hot, với khối Hall-effect đã rõ ở mức byte.
