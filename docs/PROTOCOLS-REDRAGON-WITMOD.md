# Hồ sơ mổ xẻ: Redragon K617 HE (FIZZ Magnetic) — phần mềm chính hãng

> Phân tích **tĩnh 100%** (chỉ tải file + đọc code/strings; không cài đặt, không chạy, không tiếp xúc thiết bị). Nguồn: duy nhất các domain chính hãng của Redragon. Ngày phân tích: 27/11/2026.
> Nhãn bằng chứng: **[CODE]** = đọc trực tiếp từ code/file; **[SUY LUẬN]** = suy luận có căn cứ; **[GAP]** = chưa trích xuất được, ghi rõ thay vì đoán.

## 1. Nguồn tải (chính hãng, ghi nguyên văn)

| Hạng mục | URL / vị trí |
|---|---|
| Trang sản phẩm K617 HE | `https://redragonshop.com/products/fizz-k617-he-magnetic-hall-effect-keyboard` |
| Trang mục lục phần mềm | `https://redragonshop.com/pages/software` |
| Bài tải K617 HE | `https://redragonshop.com/blogs/product-download/fizz-k617-he-magnetic-hall-effect-keyboard` (có 3 mục: User Guide, "Download for Windows: Software", "Web Driver") |
| File cài Windows | `https://cdn.shopify.com/s/files/1/0012/4957/4961/files/Redragon_K617RGB-M_Installer_V2.06.04.exe?v=1761102116` |
| Web driver | `https://www.illumipc.com/` (đúng link "Web Driver" trong bài tải chính hãng; cũng là địa chỉ web-driver in trong manual Redragon, vd K721) |

## 2. Kiểm toán an toàn — KẾT LUẬN: SẠCH ở mức phân tích tĩnh

| File | Kích thước | SHA-256 | Chữ ký số |
|---|---|---|---|
| `Redragon_K617RGB-M_Installer_V2.06.04.exe` | 16,626,160 B | `fa368df22a89ca1482c60ec825b0f295b50b462f78c35de89baee62ef7664c16` | Có — GlobalSign EV CodeSigning |
| `Redragon K617RGB-M.exe` (app chính, Qt) | 9,802,832 B | `a3b4ec61c9595825c3a4abb5201632b97390c089fb46a0a9aea379684240c7a8` | Có |
| `UpgradeAppTool.exe` (nạp firmware, "Upgrade Runing Tool V_1.0.2") | 98,416 B | `bda1c7f48e67b5a61de0633d7875a0b03185a0a5dd628e1c366e81432051cbad` | Có |

- **Chủ thể ký:** Ruilian Micro Technology (Shenzhen) Co., Ltd. (thâm quyến, Quảng Đông, CN; trong exe còn thấy biến thể tên "Ruilianwei Technology (Shenzhen) Co.,LTD") — tức **đơn vị ODM/nền tảng**, không phải Redragon ký trực tiếp. [CODE — bảng chứng thư PE + strings]
- **Endpoint lạ:** ngoài các URL hạ tầng chứng thư (CA/CRL/OCSP của GlobalSign), trong app chính chỉ thấy `http://witmod.com`, `http://www.Witmod.com`, `http://K617RGB-M.com` (trang chủ sản phẩm/nền tảng). [CODE — strings]
- **Không phát hiện:** domain telemetry/analytics, SDK theo dõi, dấu hiệu đào coin, đánh cắp credential trình duyệt, webhook Discord/Telegram. [CODE — quét strings toàn bộ]
- **Lưu ý trung tính:** app có chuỗi registry `HKCU\...\CurrentVersion\Run` — khả năng tự khởi động cùng Windows (thường là tuỳ chọn khay hệ thống của driver). Không phải bằng chứng độc hại, nhưng người dùng nên biết. [CODE]
- Giới hạn thường lệ: phân tích tĩnh không chứng minh được 100% hành vi lúc chạy; kết luận "sạch" là ở mức bằng chứng tĩnh.

## 3. Phát hiện lớn: đây là nền tảng white-label "WitMod", không phải code riêng của Redragon

- Web driver tại illumipc.com là SPA dùng chung nhiều hãng: blob cấu hình `HOST_DATA` bị XOR-mã hoá (tiền tố `WMX1:`, base64, khoá XOR `witmod-config-0x5a`) giải ra chính sách đa nhãn — bản đồ tên miền → thương hiệu gồm `aulacn.com` (AULA), `fantechworld.com`, `machenike.com`, `keycool.vip`, `kzzi.com`, Cidoo, Wraith, QMK center, SilverMonkey... [CODE — `web/host-data-decoded.json`]
- Mã web chính `app.js` cũng XOR-mã hoá (magic `WMJ1`, khoá `witmod-app-0x5a`); bản giải mã 2,892,942 B tại `web/app.decoded.js`, phía trên còn phủ javascript-obfuscator (mảng chuỗi đã giải toàn bộ: `web/web-strings-decoded.txt`, 6,059 chuỗi). [CODE]
- App desktop chứa `WitmodDefaultConfig.ini` và các theme INI của nền tảng. [CODE]

Hệ quả thực tế: **giao thức K617 HE = giao thức chung của dòng Hall-effect WitMod**, nhiều hãng khác (AULA bản web, Fantech, Machenike...) dùng cùng lõi.

## 4. Định danh K617 HE

- **App desktop nhận diện thiết bị bằng chuỗi productName HID**, không phải bằng bảng VID:PID. Họ K617 HE = tiền tố **`7153`**, gồm 8 biến thể layout/vùng: `7153PORXYXNHJHERGB`, `7153_AJPJHERGB`, `7153_APORJHERGB`, `7153_AUSHERGB`, `7153_AUSJAKHERGB`, `7153_AUSXHERGB`, `7153_AUSXYXNCYHERGB`, `7153_AUSXYXNHJHERGB`. [CODE — strings trong exe; lưu tại `desktop-device-strings.txt`]
- Chỉ 2 ảnh thiết bị đóng gói kèm: `images/hidImages/Keyboard_7153USHEXYXACPARGB.png`, `Keyboard_7153USHEXYXCPARGB.png`. [CODE]
- **VID:PID của K617 HE: [GAP — cố ý không đoán].** Mã `7153` là mã model trong productName, **không** phải PID: giá trị `0x7153` không xuất hiện ở bất kỳ đâu trong bảng filter WebHID (36 cặp, mục 5). Phân tích tĩnh không cho ra VID:PID thật ở mức bằng chứng; muốn chắc chắn phải đọc USB descriptor trên thiết bị thật (ngoài phạm vi nhiệm vụ này). Mọi nguồn ghi VID:PID cho K617 HE mà không kèm descriptor/capture đều không đáng tin.
- Web driver **không chứa cơ sở dữ liệu model**: giải mã toàn bộ 6,059 chuỗi của app web không có chuỗi nào chứa "K617", "7153" hay "Redragon" — app web là bản generic cho mọi thiết bị 0xFF1B, đọc productName từ descriptor rồi đối chiếu (hàm `matchDeviceEntry`). [CODE]

## 5. Transport

- Giao diện cấu hình = HID vendor-defined: **usagePage 0xFF1B, usage 0x91** (một filter dùng usage 0x1). [CODE — `web/webhid-filters.json`, trích từ hằng số `abx` dạng rõ trong app web]
- Bảng filter WebHID đầy đủ (36 cặp) — các silicon vendor gặp lại ở nhiều nền tảng khác:

| VID | PID |
|---|---|
| 0x0416 (Winbond/Nuvoton) | 7000, 7372, 7402, 7410, 7472, 7474, 7500–7503, 7600, 9258, C345, C375 |
| 0x117F | 4006, 4007 |
| 0x17EF (Lenovo) | 61D4, 61D6 |
| 0x2E3C | 7500–7505, 7508–750B, C365–C371 |
| 0x34D3 | 5301 |

  Ghi chú: `0x2E3C:0xC365` chính là định danh WIN60 HE đã gặp trong các lần mổ xẻ app khác trước đây — cùng vendor silicon, nhưng **khác nền tảng giao thức** (ở đó usage page 0xFF33; ở WitMod là 0xFF1B). [CODE + đối chiếu hồ sơ cũ]
- WebHID chỉ dùng `sendReport` (không có `sendFeatureReport`) → giao thức chạy trên interrupt OUT reports. [CODE]
- App desktop (Qt/C++) có các lớp: `DDeviceHidCmdSend` (gửi lệnh), `MainHidInterFace` (giao diện HID chính), `hidDeviceJoinFrame` (ghép/nối khung — tức **có phân mảnh gói tin**), `HidFirmwareInfoTool` (đọc thông tin firmware). [CODE — RTTI/strings]

## 6. Giao thức — phần trích xuất được và khoảng trống

Đã xác nhận ở mức cấu trúc [CODE]:

1. **Có bước bắt tay (handshake)** khi kết nối: log debug `---------- *** ShakeHands Failed [MainHidInterFace] *** ---------- m_strHidName:` và tín hiệu Qt `onShakeHandsRuning()`. Nội dung byte của handshake: **[GAP]**.
2. **Có cơ chế nối khung (join frame)** cho dữ liệu lớn (màu từng phím / block cấu hình vượt một report) qua lớp `hidDeviceJoinFrame`. Chi tiết header từng mảnh: **[GAP]**.
3. Đường gửi màu từng phím tồn tại trong code desktop: `onDviceUpdateAllKeyColorSendKeyboardHid`. [CODE — chuỗi slot Qt]
4. Cấu hình layout từng phím nằm trong resource dạng JSON `KeyInfo_<productName>.config`: carve được **202 file JSON** từ exe (thư mục `extract/carved/`), cấu trúc: header `{"Keyboard": {"KeyboardIntroduction": "61Key", "hidDeviceType": 4, "supportOnboard": 1, "sideLightHave": 0, ...}}` + mảng `EveryKeyInfo_RowNN` với từng phím gồm `PosX/PosY/Width/Height/Dec/qtDec/Character` — `Dec`/`qtDec` nhiều khả năng là chỉ số phím/LED dùng khi đóng gói lệnh [SUY LUẬN]. Việc carve làm mất tên resource nên **chưa gắn chắc chắn được 1 file JSON cụ thể cho đúng biến thể 7153** [GAP].
5. Các file `<customLight>` XML (2,339–5,407 B) là preset hiệu ứng đèn mẫu đóng gói kèm. [CODE — carved]

**Chưa trích xuất được ở mức byte [GAP — ghi thẳng]:** bảng opcode lệnh, độ dài report chính xác, checksum (nếu có), định dạng ACK và nhịp chờ (pacing) giữa các gói. Lý do: logic đóng gói nằm trong code native đã biên dịch của exe Qt (cần disassembly bằng Ghidra/IDA theo `DDeviceHidCmdSend`) và trong phần thân hàm bị obfuscator che của app web (mảng chuỗi đã giải hết nhưng hằng số lệnh nằm rải trong thân hàm đã đổi tên biến). Đây là bước làm tiếp theo nếu cần giao thức đầy đủ — phân tích tĩnh bề mặt không đủ, và hồ sơ này không bịa số thay cho bằng chứng.

## 7. Tính năng Hall-effect — xác nhận có trong code

Từ chuỗi đã giải mã của app web + cấu trúc app desktop [CODE]:

- **Điểm kích hoạt (actuation)** tuỳ chỉnh từng phím và **Rapid Trigger** — tính năng lõi của dòng (Redragon công bố dải 0.1–3.4 mm, polling 8K trên trang sản phẩm).
- **DKS** (Dynamic Keystroke — gán nhiều hành động theo độ sâu nhấn): có cả tài nguyên giao diện `:/images/DKS/slider.png` phía desktop. [CODE]
- **SOCD** (làm sạch hướng đối nghịch): chuỗi `_readBackSocd`, thông báo lỗi `"SOCD slots full"` → SOCD có **số slot giới hạn** và app có đọc ngược cấu hình SOCD từ thiết bị. [CODE]
- **Hiệu chuẩn (calibration)**: chuỗi `-calibrated.json`, `/layout-calibration`, `_calibrationHandler`, `calibratableSlots` → app lưu file hiệu chuẩn theo layout và có quy trình hiệu chuẩn switch từ. [CODE]
- **Onboard profile**: `supportOnboard: 1`, `showOnboardSelect: 1` trong KeyInfo → cấu hình lưu được trên phím. [CODE]

Bố cục byte của từng block cấu hình (actuation/RT/DKS/SOCD): **[GAP]** — cùng lý do ở mục 6.

## 8. Các model dùng chung app desktop này

Cùng một exe phục vụ cả dòng HE của nền tảng; các tiền tố mã model thấy trong bảng productName [CODE — `desktop-device-strings.txt`]: 7076, **7153 (K617 HE)**, 7177, 7186, 7187, 7255, 7259, 7272, 7391, 7455, 7465, 7502, 7934 — tương ứng các layout 61/68/82/87/98/104 phím trong 202 file KeyInfo carve được. Tên thương mại của từng tiền tố ngoài 7153 chưa được đối chiếu (không đoán).

## 9. So sánh với reverse-engineering cộng đồng của Redragon đời cũ → HỌ MỚI

| | Kala / Griffin (cộng đồng RE, 2021) | Dòng app INI cũ (vd K500-B61) | **K617 HE (bài này)** |
|---|---|---|---|
| Nguồn | github.com/LeandroSQ/redragon-rgb-controller (`keyboard.js`) | file cấu hình INI của app Redragon cũ lưu hành trên Pastebin | App chính hãng 2.06.04 + web driver |
| VID:PID | 0x0C45:0x5004, manufacturer "sonix" | VID=0x258A, PID=0x0049 ghi thẳng trong INI | Chưa rõ VID:PID [GAP]; nhận diện bằng productName `7153…` |
| Usage page | 0xFF1C | (Sinowealth) | **0xFF1B** |
| Khung lệnh | Report 64 B bắt đầu `04 …`, lệnh áp dụng `04 02 …` — họ framing SONiX "04" | Họ Sinowealth | Bắt tay ShakeHands + nối khung JoinFrame; không thấy dấu vết framing "04" |
| Tính năng | Chỉ LED | LED + macro | LED từng phím + actuation/RT/DKS/SOCD/hiệu chuẩn |

**Kết luận:** K617 HE **không** thuộc họ SONiX đời Kala/Griffin, cũng **không** thuộc họ Sinowealth 0x258A của app đời cũ. Đây là một **họ giao thức mới** của nền tảng WitMod (silicon Winbond/Nuvoton 0x0416 và các VID ở mục 5), ra đời cùng dòng switch từ Hall-effect. Mọi tài liệu RE cũ của Redragon đều không áp dụng được cho K617 HE.

## 10. Tệp bằng chứng trong thư mục này

- `raw/` — installer gốc nguyên vẹn.
- `extract/` — cây file sau innoextract + `extract/carved/` (257 stream zlib carve từ exe: JSON KeyInfo, XML customLight, INI theme).
- `web/` — tài sản web driver: `index.html`, `v2.html`, `boot.js`, `host-data-decoded.json`, `app.decoded.js`, `web-strings-decoded.txt`, `webhid-filters.json`.
- `desktop-device-strings.txt` — bảng productName trích từ exe desktop.
- `devices.json` — bản tóm tắt có cấu trúc của hồ sơ này.

## 11. Hướng làm tiếp (nếu cần giao thức đầy đủ)

1. Disassembly `DDeviceHidCmdSend`/`hidDeviceJoinFrame` trong exe desktop bằng Ghidra → opcode, framing, checksum, pacing.
2. Deobfuscate thân hàm protocol của `web/app.decoded.js` (đã có bản giải mã XOR + giải mảng chuỗi làm nền).
3. Đọc VID:PID + report descriptor từ một chiếc K617 HE thật (cần thiết bị — ngoài phạm vi phân tích tĩnh lần này).
