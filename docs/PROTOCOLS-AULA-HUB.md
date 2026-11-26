# PROTOCOLS — Toàn bộ giao thức bàn phím trong AULA Hub chính hãng

**Ngày:** 27/11/2026 (JST). **Phương pháp:** CHỈ phân tích tĩnh — đọc mã đã trích xuất sẵn trong thư mục này (client Windows 1.1.22 + web hub). **Không chạy app, không kết nối thiết bị.**
**Nguồn:** `client/asar/dist/assets/*.js` (frontend + SDK chip của client), `web/assets/*.js` (bản web hub.aulacn.com), `DOSSIER.md` (hồ sơ mổ app trước đó — chỉ để định hướng).
**Quy ước tin cậy:** **[CODE]** = thấy trực tiếp trong mã, kèm file + đoạn trích ngắn. **[SUY LUẬN]** = suy ra từ nhiều điểm trong mã. "Không tìm thấy" = đã tìm và không có — không đoán, không bịa hằng số.
**File kèm theo:** `devices.json` — bảng thiết bị dạng máy đọc (114 mục bàn phím, gộp registry client + bảng web đã giải mã).

---

## 1. Tổng quan: một hub — 7 họ giao thức bàn phím

Client AULA Hub không có một giao thức chung; mỗi họ chip có SDK, khung gói và bảng lệnh riêng. Bản web dùng cùng frontend và **bổ sung thêm dòng cơ/SONiX mà client không có** (F75 Max). Tổng hợp 7 họ bàn phím trích được + 1 họ chuột:

| # | Họ | VID:PID / kênh nhận diện | Transport | Khung gói | Checksum | Stream từng phím |
|---|---|---|---|---|---|---|
| 1 | **SparkLink V1** ("KB2") | 0x1CA5 / 0x1CA2 · page 0xFFA0 | Output report id 0, 64 B | `[0x5C, len, cmd, cksum, payload…]` | Cộng 1 byte (53+92+len+cmd+byte cuối) | Không (ghi bền từng phím) |
| 2 | **SparkLink V2** | 0x38A6 · page 0xFFB0 | Output report id 0, 64 B | `[category, sub, payload…]` | Không | Có — DirectDrive + GIF stream (5,5) |
| 3 | **HFD** (`hfd` + `hfdMechanical`) | 0x0C45 (có dây MINI/dongle) + 0x38A6 · page 0xFF68 (+ interface phụ 0xFF67) | Output report id 0, kích thước theo descriptor (thường 32 B; nhánh màn hình ~4104) | Req `[0xAA, cmd, len, addrLE16, hdr, payload@8]` · Resp `[0x55, …]` | Không (trừ gói nhạc cũ: tổng byte) | Có — animation/music (65/66/53), không có opcode stream realtime riêng |
| 4 | **RLW** | 0x2E3C:0xC365 · page 0xFF33 | Output report id 1, payload 63 B | `[op, sub, pageBE16, len, payload@5]` | Không | Không (ghi bền từng phím, op 9) |
| 5 | **Bytech HE** (class `Sc`/codec `RV`) | 0x372E:0x103E · page 0xFF60 | **Feature report** (reportId lấy từ descriptor; biến thể wireless 8K riêng) | `[cmd, param, 0, total, index, len, payload@6, …, cksum@62]` — gói 63 B | Byte 62: `(reportId + Σ) ≡ 0xFF (mod 256)` | Không (ghi bền từng phím, cmd 6) |
| 6 | **Bytech cơ** (BPTK) | 0x258A:0x010C · page 0xFF00 | **Feature report, payload 519 B** | `[cmd, param, 0, 1, 0, lenLE16, …]` | Có ở nhánh ảnh/TFT (tổng byte) | Không |
| 7 | **VIA/Vial mở rộng** (Neos75) | 0x1EA7:0x4884 · page 0xFF60 | Output report id 0, 32 B | VIA/Vial chuẩn + lệnh custom 17/19/21/29 | CRC-16 (poly 0x1021) ở byte 6–7 cho gói custom; gói VIA không CRC | Không |
| — | Infortech | Chuột: VID 0x384C / 0x38A6 · page 0xFF78 | WebHID, filter đăng ký lúc chạy | Bị obfuscate — không khôi phục được | — | Ngoài phạm vi (chuột) |

**Evison:** DOSSIER trước ghi `evison-hidsdk` được tải từ server của hãng; trong **client 1.1.22 không có mặt** (0 kết quả tìm kiếm trong toàn bộ assets) — chỉ còn ở bề mặt web/remote, không trích được gì từ bộ file này. [CODE]

**Bài học kiến trúc (khớp kết luận DOSSIER):** muốn mở rộng hỗ trợ thì tổ chức driver **theo họ chip**, không theo model — cùng một họ dùng chung gần như toàn bộ mã lệnh, model chỉ khác layout/số phím và vài PID.

---

## 2. Bảng thiết bị bàn phím đầy đủ

Gộp từ registry của client (`index-npIkQlTa.js`, 75 mục bàn phím gồm dongle) và bảng thiết bị của web hub đã giải mã hoàn toàn (95 mục bàn phím; bản web là tập cha — thêm nhiều biến thể PID và các model chỉ có trên web như F75 Max, F75 V2, NOVA75 Pro, NOVA87). Cột "Nguồn": client = có trong client Windows; web = có trong web hub. HE = Hall-effect/từ tính; "Từng phím" = ghi màu từng phím (bền hoặc stream — xem mục họ tương ứng).

| Model | VID:PID | Họ | Page / Usage | Kết nối | HE | Từng phím | Màn hình | Nguồn |
|---|---|---|---|---|---|---|---|---|
| 8K Wireless Receiver | `0x372E:0x106C` | Bytech HE (Sc/RV) | 0xFF60 / 97 | 2.4G dongle | có | có | không | client+web |
| AULA HERO 75 HE | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | client+web |
| AULA HERO108 HE | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | web |
| AULA HERO75 AIR | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | web |
| HERO 108 | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | web |
| HERO 68 Air | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | client+web |
| HERO 68 HE | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | client+web |
| HERO 68 HE JIS | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | web |
| HERO 68 MINI | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | client+web |
| HERO 68 XS | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | client+web |
| HERO 84 HE | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | client+web |
| HERO 84 HE JIS | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | web |
| HERO 99 | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | client |
| HERO 99 HE | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | client |
| HERO68 MINI Air | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | client+web |
| WIN68HE Ultra | `0x372E:0x103E` | Bytech HE (Sc/RV) | 0xFF60 / 97 | wired | có | có | không | client+web |
| 2.4G Wireless Receiver | `0x3554:0xFA09` | Bytech co (BPTK) | 0xFF02 / 2 | 2.4G dongle | không | có | — | client |
| AULA-F68 | `0x258A:0x019D` | Bytech co (BPTK) | 0xFF00 / 1 | wired | không | có | — | web |
| Game-1002-0202 | `0x258A:0x0150` | Bytech co (BPTK) | 0xFF02 / 2 | 2.4G dongle | không | có | — | web |
| Gaming Keyboard | `0x258A:0x010C` | Bytech co (BPTK) | 0xFF00 / 1 | wired | không | có | — | client |
| 8K Wireless Receiver | `0x0C45:0xFEF9` | HFD | 0xFF60 / 97 | 2.4G dongle | có | có | — | client+web |
| AULA F108Pro V2 | `0x38A6:0x271A` | HFD | 0xFF68 / 97 | wired | có | có | — | web |
| AULA F108Pro V2 Dongle | `0x0C45:0xFEFE` | HFD | 0xFF68 / 97 | 2.4G dongle | có | có | — | web |
| AULA F108ProV2 | `0x38A6:0x2739` | HFD | 0xFF68 / 97 | wired | có | có | dot-matrix | client+web |
| AULA F108ProV2C | `0x38A6:0x273A` | HFD | 0xFF68 / 97 | wired | có | có | dot-matrix | client+web |
| AULA F108ProV2C | `0x38A6:0x2749` | HFD | 0xFF68 / 97 | wired | có | có | dot-matrix | client+web |
| AULA F108ProV2C Dongle | `0x0C45:0xFEFC` | HFD | 0xFF68 / 97 | 2.4G dongle | có | có | — | client+web |
| AULA F75 HE | `0x38A6:0x2729` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| AULA F75 HE | `0x38A6:0x2742` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| AULA F75 HE Dongle | `0x0C45:0xFEFE` | HFD | 0xFF60 / 97 | 2.4G dongle | có | có | — | client+web |
| AULA F75 V2 | `0x38A6:0x2768` | HFD | 0xFF68 / 97 | wired | có | có | — | web |
| AULA F75 V2 Dongle | `0x0C45:0xFEFC` | HFD | 0xFF68 / 97 | 2.4G dongle | có | có | — | web |
| AULA F87ProV2C | `0x38A6:0x2712` | HFD | 0xFF68 / 97 | wired | có | có | — | client |
| AULA F87ProV2C | `0x38A6:0x2738` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| AULA F87ProV2C | `0x38A6:0x2747` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| AULA F87ProV2C Dongle | `0x0C45:0xFEFC` | HFD | 0xFF68 / 97 | 2.4G dongle | có | có | — | client+web |
| AULA F87ProV2D | `0x38A6:0x2711` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| AULA F87ProV2D | `0x38A6:0x2737` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| AULA F87ProV2D | `0x38A6:0x2746` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| AULA F87ProV2D  Dongle | `0x0C45:0xFEFE` | HFD | 0xFF68 / 97 | 2.4G dongle | có | có | — | web |
| AULA F87ProV2D Dongle | `0x0C45:0xFEFE` | HFD | 0xFF68 / 97 | 2.4G dongle | có | có | — | client+web |
| AULA F87ProV2Z | `0x38A6:0x2710` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| AULA F87ProV2Z | `0x38A6:0x2736` | HFD | 0xFF68 / 97 | wired | có | có | — | web |
| AULA F87ProV2Z | `0x38A6:0x2748` | HFD | 0xFF68 / 97 | wired | có | có | — | web |
| AULA F87ProV2Z Dongle | `0x0C45:0xFEFE` | HFD | 0xFF68 / 97 | 2.4G dongle | có | có | — | web |
| AULA F87S 8K | `0x38A6:0x2908` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| AULA NOVA75 Pro | `0x38A6:0x275C` | HFD | 0xFF68 / 97 | wired | có | có | — | web |
| AULA NOVA75 Pro | `0x0C45:0xFEFC` | HFD | 0xFF68 / 97 | 2.4G dongle | có | có | — | web |
| AULA NOVA87 | `0x38A6:0x275A` | HFD | 0xFF68 / 97 | wired | có | có | — | web |
| AULA NOVA87 | `0x38A6:0x275E` | HFD | 0xFF68 / 97 | wired | có | có | — | web |
| AULA NOVA87 Dongle | `0x0C45:0xFEFC` | HFD | 0xFF68 / 97 | 2.4G dongle | có | có | — | web |
| AULA NOVA98 | `0x38A6:0x273B` | HFD | 0xFF68 / 97 | wired | có | có | TFT 240x135 | client+web |
| AULA NOVA98 | `0x38A6:0x275D` | HFD | 0xFF68 / 97 | wired | có | có | TFT 240x135 | client+web |
| AULA NOVA98 Dongle | `0x0C45:0xFEFC` | HFD | 0xFF80 / 113 | 2.4G dongle | có | có | — | client |
| Epomaker x Aula F75 V2 | `0x38A6:0x273F` | HFD | 0xFF68 / 97 | wired | có | có | — | web |
| F75 Master | `0x38A6:0x2758` | HFD | 0xFF68 / 97 | wired | có | có | — | web |
| F75 Master Dongle | `0x0C45:0xFEF9` | HFD | 0xFF60 / 97 | 2.4G dongle | có | có | — | web |
| F75 V2 Dongle | `0x0C45:0xFEFC` | HFD | 0xFF68 / 97 | 2.4G dongle | có | có | — | web |
| MINI 60 HE | `0x0C45:0x8032` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| MINI 60 HE PRO | `0x0C45:0x80A2` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| MINI 60 HE PRO | `0x0C45:0x80B2` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| MINI 60 HE PRO Dongle | `0x0C45:0xFEFE` | HFD | 0xFF60 / 97 | 2.4G dongle | có | có | — | client+web |
| MINI 68 HE | `0x38A6:0x2722` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| MINI 68 HE PRO | `0x38A6:0x2723` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| MINI 68 HE PRO Dongle | `0x0C45:0xFEFE` | HFD | 0xFF60 / 97 | 2.4G dongle | có | có | — | client+web |
| MINI60HE MAX | `0x0C45:0x80A1` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| MINI60HE MAX Dongle | `0x0C45:0xFEFC` | HFD | 0xFF80 / 1 | 2.4G dongle | có | có | — | client+web |
| MINI68 HE MAX | `0x38A6:0x2724` | HFD | 0xFF68 / 97 | wired | có | có | — | client+web |
| MINI68HE MAX Dongle | `0x0C45:0xFEFC` | HFD | 0xFF60 / 97 | 2.4G dongle | có | có | — | client+web |
| WIN 60 HE | `0x2E3C:0xC365` | RLW | 0xFF1B / 145 | wired | có | có | không | client+web |
| WIN 68 HE | `0x2E3C:0xC365` | RLW | 0xFF1B / 145 | wired | có | có | không | client+web |
| 2.4G Dongle | `0x05AC:0x024F` | SONiX F75 Max (web hub) | 0xFF60 / 97 | 2.4G dongle | không | có | TFT 128x128 | web |
| AULA F108Pro | `0x0C45:0x800A` | SONiX F75 Max (web hub) | 0xFF13 / 1 | wired | không | có | TFT 128x128 | web |
| AULA F75Max | `0x0C45:0x800A` | SONiX F75 Max (web hub) | 0xFF13 / 1 | wired | không | có | TFT 128x128 | web |
| F108Pro Dongle | `0x05AC:0x024F` | SONiX F75 Max (web hub) | 0xFF60 / 97 | 2.4G dongle | không | có | TFT 128x128 | web |
| HERO 68 HE PRO | `0x1CA5:0x0409` | SparkLink V1 | 0xFFA0 / 1 | wired | có | có | — | client+web |
| HERO 68 HE ULTRA | `0x1CA5:0x0407` | SparkLink V1 | 0xFFA0 / 1 | wired | có | có | — | client+web |
| WIN 60 HE MAX | `0x1CA2:0x1902` | SparkLink V1 | 0xFFA0 / 1 | wired | có | có | — | client+web |
| WIN 60 HE PRO | `0x1CA2:0x1902` | SparkLink V1 | 0xFFA0 / 1 | wired | có | có | — | client+web |
| WIN 68 HE MAX | `0x1CA2:0x1901` | SparkLink V1 | 0xFFA0 / 1 | wired | có | có | — | client+web |
| WIN 68 HE PRO | `0x1CA2:0x1901` | SparkLink V1 | 0xFFA0 / 1 | wired | có | có | — | client+web |
| AG60 | `0x38A6:0x28D0` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG60 MAX | `0x38A6:0x28D2` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG60 MAX | `0x38A6:0x28DD` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG60 PRO | `0x38A6:0x28D1` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG60 PRO | `0x38A6:0x28DC` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG63 | `0x38A6:0x272D` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG63 PRO | `0x38A6:0x272C` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG63 PRO | `0x1A86:0xFE81` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG63 pro | `0x38A6:0x272C` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG63 pro | `0x1A86:0xFE81` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG75 | `0x38A6:0x28DF` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client |
| AG75 | `0x38A6:0x28F0` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG75 MAX | `0x38A6:0x28E1` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG75 MAX | `0x38A6:0x28F2` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG75 PRO | `0x38A6:0x28E0` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG75 PRO | `0x38A6:0x28F1` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG75 Ultra | `0x38A6:0x28E1` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AG75 Ultra | `0x38A6:0x28F2` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AULA 60 | `0x38A6:0x2902` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AULA 66 | `0x38A6:0x2903` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| AULA60 | `0x38A6:0x2902` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| BOX 63 | `0x38A6:0x2730` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | dot-matrix (Decorate1) | client |
| BOX 63 | `0x38A6:0x2731` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | dot-matrix (Decorate1) | web |
| BOX 63MAX | `0x38A6:0x2731` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | dot-matrix (Decorate1) | client+web |
| BOX 63MAX | `0x38A6:0x2767` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | dot-matrix (Decorate1) | web |
| F68RX | `0x38A6:0x272E` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| F68rx | `0x38A6:0x272E` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| GALAXY 600 | `0x38A6:0x2904` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| WIN 60 EVO | `0x38A6:0x290A` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client+web |
| WIN 68 EVO | `0x38A6:0x2909` | SparkLink V2 | 0xFFB0 / 1 | wired | có | có | — | client |
| AULA-Neos75 | `0x1EA7:0x4884` | VIA/Vial (Neos75) | 0xFF60 / 97 | wired | có | không | không | client+web |
| AULA-Neos75 dongle | `0x6D66:0x8572` | VIA/Vial (Neos75) | 0xFF60 / 97 | 2.4G dongle | có | không | không | web |
| WIN 60 SE | `0x38A6:0x291A` | WIN60 SE (shop F2) | 0xFF00 / 1 | wired | có | có | không | web |
**Ghi chú bảng thiết bị:**
- **PRO/MAX dùng chung PID** ở nhiều dòng (SparkLink V1: WIN 68 HE PRO = WIN 68 HE MAX = 0x1CA2:0x1901) — không phân biệt model bằng VID:PID được; app phân biệt bằng tên sản phẩm HID/bảng layout. [CODE]
- **Họ Bytech HE (0x372E:0x103E):** một VID:PID duy nhất cho cả dòng HERO (68/MINI/XS/Air/84/99/108/75) — phân biệt bằng UUID đọc từ thiết bị. [CODE]
- **Họ HFD:** cùng một model có **nhiều PID theo đời/vùng** (client và web lệch nhau, vd F87ProV2C: client 0x2712/0x2748/0x2757, web 0x2738/0x2747 + biến thể 0x2712) — khi làm driver phải chấp nhận danh sách PID mở. [CODE]
- **F108Pro (web) dùng chung VID:PID với F75 Max** (0x0C45:0x800A) — xem mục 9. [CODE]
- Ngoài bàn phím, registry client còn 41 mục chuột + 1 tai nghe (USB Audio 0x3302:0x12C2) — ngoài phạm vi trích giao thức của đợt này, chỉ đếm số lượng trong `devices.json`. [CODE]
- Đính chính registry client: PID của AULA F87ProV2Z ghi trong mã là `1e4` = **10000 (0x2710)**, không phải 1. [CODE]

---

## 3. Họ SparkLink V1 — giao thức "KB2"

**Thiết bị:** HERO 68 HE ULTRA (0x1CA5:0x0407), HERO 68 HE PRO (0x1CA5:0x0409), WIN 68 HE PRO/MAX (0x1CA2:0x1901), WIN 60 HE PRO/MAX (0x1CA2:0x1902). 68 phím (HERO/WIN 68), 61 phím (WIN 60). Không núm, không màn hình; có vùng LED logo riêng.
**File:** `info-B2zKVp8F.js` (lớp thiết bị + giao thức), layout `SparkLinkV1-t4aSMPxg.js`, các trang `light-Cp389CIy.js`, `trigger-VHOIH7VE.js`, `key-BkmYRml7.js`, `high-CLmP8232.js`, `perf-Dx-QLAFx.js`, `version-CZpI-VxG.js`.

### 3.1 Transport & khung gói [CODE]
- WebHID, **output report id 0**, gói **64 byte**; filter `{vendorId, productId, usage:1, usagePage:65440}` (0xFFA0). Không dùng feature report.
  > `async sendReport(r) { return this.device.sendReport(0, r) }` — `info-B2zKVp8F.js`
- Khung: `[0]=0x5C (magic), [1]=độ dài payload, [2]=lệnh, [3]=checksum, [4…]=payload`, đệm 0 tới 64 byte.
  > `vn = (e, t, r) => [92, e, t, r]` — header `[0x5C, length, command, checksum]`
- **Checksum 1 byte:** `checksum = 53 + 92 + length + command + (byte payload cuối nếu 0 < length ≤ 252)`, cắt 8 bit.
  > `_n = (e, t, r) => { let s = 53; return s += 92, s += e, s += t, e > 0 && e <= 252 && (s += r[r.length - 1]), s }`
- Payload byte 0 của hầu hết lệnh = cờ đọc/ghi: **0 = đọc, 1 = ghi**. Số 16-bit luôn little-endian.
- Ghi lớn (firmware/ảnh): nối header+payload rồi cắt thành từng gói 64 byte; byte độ dài mô tả **toàn bộ luồng**.

### 3.2 Bảng lệnh (byte 2) [CODE]
| Byte | Tên | Chức năng |
|---:|---|---|
| 0 | KB2_CMD | Lệnh thông tin chung; payload[0] = ORDER_TYPE (bảng dưới) |
| 1 | KB2_CMD_SYNC | Đồng bộ định danh (BoardID LE32, SN 16 ký tự, phiên bản, ngày build) |
| 8–14 | KB2_BL_* | Bootloader: SIGN(8) ERASE(9) REBOOT(10) TOAPP(11) WRITE(12) READ(13) RCRC(14) |
| 18 | KB2_CMD_RM6X21 | Đọc khối hành trình/hiệu chuẩn (đa phản hồi, chờ 3 gói) |
| 24 / 25 | KB2_CMD_PRGB / LOGORGB | Khối RGB chính / khối RGB logo (đọc+ghi) |
| 32 / 33 | KB2_CMD_MACRO / MACRO_MODE | Thân macro (9 sự kiện/gói) / header macro |
| 35 | KB2_CMD_KEY | Keymap + slot layout từng phím (bảng Layout dưới) |
| 36–40 | MT / TGL / DKS / MPT / END | Phím nâng cao: Mod-Tap, Toggle, DKS, đa điểm, END |
| 41 | KB2_CMD_DB | Dead-band + hành trình toàn cục |
| 42 | KB2_CMD_KRGB | Màu từng phím (xem 3.4) |
| 43 | KB2_CMD_DEFKEY | Đọc keymap mặc định (theo cặp hàng) |
| 44 / 45 | KB2_CMD_SOCD / RS | Cặp SOCD / cặp RS |
| 48 / 49 | KB2_CMD_PIC / PIC_WRITE | Nạp ảnh — **chỉ có ở lớp giao thức, không có nơi gọi trong họ này (mã chết)** |
| 255 | KB2_CMD_FAIL | Đánh dấu lỗi |

**ORDER_TYPE** (payload[0] khi lệnh = 0) — các giá trị chính [CODE]: 1 phiên bản giao thức · 2 lưu tham số · 3 nạp lại tham số · 4 xoá hiệu chuẩn · 5/6 mở/đóng DKS · 7 đảo ngược · 8/9 bật/tắt remap · 10/11 bật/tắt relative trigger · 12/13 bắt đầu/kết thúc hiệu chuẩn · 14/15 demo · 16 xoá kho macro · 17 khôi phục gốc · 32 khoá Win · 33/34 truy vấn mode Win/Mac · 37 độ chính xác hành trình · 38 tên phím · 48/49 đặt mode Win/Mac · 52 công tắc dead-zone trên · 64–67 RGB1–4 · 68 truy vấn màu cố định (đọc {r,g,b} ở payload[2..4]; cũng là lệnh ghi độ bão hoà) · 71 RGBNACK · 80 **polling rate** (giá trị 0–6 = 8K/4K/2K/1K/500/250/125 Hz) · 96 web · 97 SOCD · 112 **chọn profile onboard** (0–3, 4 profile) · 117/118 trục switch hiện tại / danh sách trục.

**Layout slot** (payload[1] khi lệnh = 35) [CODE]: 0–3 = keymap Fn0–Fn3 · 4 = điểm kích hoạt từng phím (DB0) · 5–7 = điểm hành trình DKS/DB · 8 = chế độ trigger `(touchMode<<4)|advancedMode` · 9–12 = điểm DKS đọc ngược · 13–16 = cờ TRPS · 19 = độ trễ MT · 20/21 = hành trình RT nhấn/nhả · 22/23 = dead-zone nhấn/nhả từng phím · 25 = loại trục từng phím · (17/18/24/32 chỉ có trong enum, không thấy nơi gọi).

### 3.3 Đèn [CODE]
- Khối PRGB (24): payload `[cờ, 0,0,0,0, 7 màu × (B,G,R,0xFF), 0,0,0,0, cờ(bit0 mở, bit1 hướng, bit4 super-response), độ sáng, mode, tốc độ, sleep, chỉ số màu tĩnh, (màu động — chỉ khi version ≥ 1.0.9)]`. **Thứ tự màu trong khối là BGR.**
- Mode: 0 = tĩnh · 1–20 = hiệu ứng động · 21 = tuỳ chỉnh (từng phím). Tên VI đầy đủ có trong locale của app (`kLight.sparkLinkV1.mode*`): 1 Gợn sóng, 2 Lên xuống, 3 Gợn nhẹ, 4 Bão xoay, 5 Cầu vồng may mắn, 6 Cầu vồng lấp lánh, 7 Rực rỡ, 8 Ô di chuyển, 9 Biến đổi dạng sóng, 10 Bóng di động, 11 Mây trôi nước chảy, 12 Đường cong sin, 13 Trăm hoa đua nở, 14 Khảm sắc màu, 15 Mưa như trút, 16 Chuyển động không ngừng, 17 Dấu chân trên tuyết, 18 Tìm hoa trong tuyết, 19 Laser xuyên mây, 20 Sóng nước. Logo: mode tĩnh + 1–4.
- Độ sáng/tốc độ UI 1–5 (wire = UI−1 ở tầng trang).

### 3.4 Từng phím & stream [CODE]
- Từng phím **bền**: lệnh 42, payload `[cờ ghi, key, R, G, B]` — **mỗi phím một gói** (thứ tự RGB, khác khối BGR ở trên); kết thúc bằng gói "lưu" đặc biệt: key 254, màu 254/254/254.
  > `async saveCustomLighting() { const t = nt.cmdSingleRGB(!1, { key: 254, r: 254, g: 254, b: 254 }); …`
- Builder ghi nhiều phím/gói (`cmdKRGB`) tồn tại nhưng **không có nơi gọi** [SUY LUẬN từ grep]. **Không có stream realtime** trong họ này.

### 3.5 ACK / nhịp gửi [CODE]
- Không đối chiếu echo lệnh: hàng đợi FIFO — gói phản hồi đầu tiên về sẽ giải quyết lệnh chờ đầu tiên; phản hồi cũ bị loại theo timestamp. Timeout mặc định **1000 ms**, thử lại tối đa **3 lần** (`MAX_RETRIES = 3`), hàng đợi lệnh nối tiếp (1 lệnh tại một thời điểm).
- Gói tự phát (không do lệnh) được định tuyến theo byte 2 thô = **lệnh gốc + 0x80** (128=getCmd, 163=getKey, 171=defKey, 152/153=RGB đặc biệt/logo) — cơ chế app nhận biết khi người dùng đổi profile/mode ngay trên phím.
- Nhịp gửi thường: **không có delay cố định** (chỉ nối tiếp theo hàng đợi). Riêng firmware: writeDelay 30 ms, chờ 4000 ms khi vào/ra boot.

### 3.6 Cài đặt & firmware [CODE]
- Mọi giá trị hành trình = **mm × 1000, LE16**. UI: hành trình 0.1–3.4 mm, dead-zone 0–1, RT 0.005–3.4.
- Dead-band toàn cục (41): `[cờ, 0, 0, hành trình LE16, dead-nhấn LE16, dead-nhả LE16, 0×6]`.
- DKS/MPT/MT/TGL/END/SOCD/RS: payload `[cờ, key, …trường LE16]`; SOCD = `[cờ, pos1, pos2, key1 LE16, key2 LE16, type, mode, (delay LE16 nếu version ≥1.0.7)]`; một số mã hoá **thay đổi theo phiên bản giao thức thiết bị** (1.0.5/1.0.7) — driver phải đọc version trước.
- Macro thân: gói 9 sự kiện, địa chỉ bắt đầu 256; mỗi sự kiện = keycode LE16 + từ 32-bit (bit trạng thái <<24 | hiệu thời gian 24 bit).
- Firmware: mở khoá bằng chữ ký kiểu TEA (16 vòng, delta 0x9E3779B9, khoá nhúng trong bundle) trên SN thiết bị → erase → ghi từng khúc 244 byte → kiểm tra CRC-16 phản xạ poly **0x8408** (tham số kiểu KERMIT) → về app. Đây là rào cản lớn nhất nếu muốn tự nâng firmware cho họ này.

---

## 4. Họ SparkLink V2

**Thiết bị:** dòng AG (AG60/PRO/MAX, AG75/PRO/Ultra/MAX, AG63/PRO), AULA60, AULA 66, GALAXY 600, F68RX, WIN 60/68 EVO, BOX 63/63MAX — VID 0x38A6, page 0xFFB0 (biến thể AG63 PRO có VID 0x1A86:0xFE81, cùng layoutClass). Số phím theo layout: AG60/WIN 60 EVO 61 · AG75 81 · AULA 66/AG63 66 · AULA60 63 · F68RX/WIN 68 EVO 68 · BOX 63 64.
**File:** `info-Doo-kgMx.js` (Info + toàn bộ SDK "sparkLink", `sdkModuleName="sparkLinkV2"`), layout `SparkLinkV2-JhtdFxGV.js`, trang `light-DgD-ZbRU.js`, `gifLight-m6jnq9pa.js`, `matrixLight-yTbYaoY9.js` (chỉ BOX 63), `trigger-DRkAPZlM.js`, `key-CHLv1Ywi.js`, `high-DmqlPLvU.js`, `perf-Bubj2mym.js`, `version-BaxvFQW7.js`.

### 4.1 Transport & khung [CODE]
- WebHID, **output report id 0**, gói **64 byte** đệm 0. Filter `{vendorId, productId, usagePage:65456, usage:1}`.
  > `async sendReport(i){return this.device.sendReport(0,i)}` — `info-Doo-kgMx.js`
- Khung: `[0]=category, [1]=subcommand, [2…]=payload`. **Không magic, không trường độ dài, không checksum** ở khung thường (checksum chỉ xuất hiện như *mã trạng thái thiết bị* 252 trong enum Voice).
- LE16/LE32 cho mọi số nhiều byte.

### 4.2 Bảng category (byte 0) & lệnh chính [CODE]
| Byte | Category | Lệnh tiêu biểu |
|---:|---|---|
| 1 | Device | 1=phiên bản giao thức · 2=thông tin thiết bị (boardId, appVersion, SN…) · 3=năng lực thiết bị (bitfield: loại trục; kết nối; cơ bản {RGB, **núm**}; mở rộng {màn hình nhỏ, màn hình lớn, haptic, voice, gamepad, **dot-matrix**}) |
| 2 | Global | 1=reset gốc (theo phạm vi) · 2=lưu tham số (giá trị 15 = reset high-polling) · 3=profile (4 profile, tên UTF-8 ≤32 B) · 4=mode Win/Mac · 5=**polling** (0–6 = 8K→125 Hz) · 6=hiệu chuẩn mở/đóng · 7=thư viện trục · 8=vùng hiệu ứng đèn · 9=trục mặc định · 12=độ chính xác RT · 13=thời gian ngủ đèn (u16LE phút) · 14=thông tin kho macro · 16=chống rung · 18=**USB mode** (frame đặc biệt bắt đầu `[18,…]`: USB2.0/3.0-8K/16K/32K) |
| 3 | LayoutAndKey | 1/2=đọc/ghi hàng keymap (30 keycode LE16) · 3/4=đọc/ghi 1 phím · 5=kiểu layout (ô có ratio 12 = **núm**, 13/14/15 = **màn hình**) · 6=layout mặc định. Layer 0–3 |
| 4 | Performance | 1/2=đọc/ghi trigger từng phím (khung 27 B, §4.4) · 3=AxisData (0=ADC, 1=hành trình, 2=hiệu chuẩn, 3=trạng thái phím) |
| 5 | Lighting | §4.3 |
| 6 | HigherKey | §4.5 |
| 7 | Macro | 1/2=mode macro · 3/4=đọc/ghi dữ liệu macro (15 action/gói) |
| 8 | FirmwareUpgrade | 1=AppToBoot · 2=Flash (56 B/gói) · 3=Validate · 4=BootToApp |
| 10 | CustomCommand | Animation dot-matrix (§4.6) + blackout `[10,1,…]` + màu vỏ phím |
| 12 | Displayer | §4.6 — **có trong SDK nhưng họ này không gắn trang TFT nào** |
| 13 | ThreeMode | Pin/sạc/ngủ cho bản không dây — SDK có, các model họ này đăng ký wired nên không dùng |
| 14/15/16/17 | Voice / Touch / Handle / ThreeD | Chỉ có trong SDK đóng gói, không trang nào của họ này gọi |

### 4.3 Đèn [CODE]
Vùng đèn (area): 0=Keyboard, 1..5=Decorate1..5. Ma trận điện luôn là **6 hàng × 21 cột = 126 ô** bất kể số phím thật.
- **Base:** `[5, 1|2, area, 0, open, mode, luminance, speed, direction, selectStaticColor]` (1=đọc, 2=ghi theo enum Lt: ReadList=0/Read=1/Write=2 — ở category Lighting byte[1] là 1|2 = Read|Write của `vr`). UI gửi `luminance = lightly−1` (0–100), `speed = speed−1`; mode 18 + speed 0 bị ép thành 25.
- **Bảng mode (wire = giá trị UI):** 0–19 cho vùng chính (nhãn i18n `kLight.sparkLinkV1.mode*` dùng lại; SDK đặt tên 1=ripple, 2=EbbAndFlow, 3=RippleGently, 4=RotatingStorm, 5=LuckyRainbow, 6=ShiningRainbow, 7=Shining, 8=MovingWindow, 9=WaveTransformation, 10=Shift, 11=SineCurve, 12=CloudFlow, 13=Blooming, 14=Rainbow, 15=Rainfall, 16=Jump); bản EVO có danh sách riêng 0–20; vùng logo/Decorate: 0–4. BOX 63 matrix: −1, 0–17 (thiếu 3).
- **Palette:** `[5, 1|2, area, 1, (B,G,R,H)×8]` — 8 màu tĩnh, thứ tự **B,G,R,H**.
- **Từng phím bền (Custom):** `[5, 4, area, chunkIdx, (B,G,R,flag)×15]` — 15 phím/gói, flag 255 = có tuỳ chỉnh; đọc ngược bằng `[5, 3, area, chunkIdx]`. Hiệu chỉnh màu: `[5, 1|2, area, 2, R, G, B]` (thứ tự RGB ở lệnh này). Màu Caps: `[5, 6, B, G, R]`.
- **Stream (DirectDrive):** cùng khung với Custom nhưng byte[1]=5 — gửi **không chờ phản hồi**; đây là đường stream thật của họ này.
- **GIF stream (trang gifLight):** trang bỏ qua SDK, ghi thẳng HID: `[0]=5,[1]=5,[2]=0,[3]=chunkIdx`, 15 ô × u32LE `(255<<24|R<<16|G<<8|B)`, nhịp mỗi gói `max(1, round(300/speed))` ms. [CODE — `gifLight-m6jnq9pa.js`]

### 4.4 Trigger / HE [CODE]
Khung SetPerformance (27 B): `[4, 2, row, col, mode, normalPress LE16, normalRelease LE16, rtFirstTouch LE16, rtPress LE16, rtRelease LE16, pressDead LE16, releaseDead LE16, axis, calibrate, axisV2Id LE16, axisRangeMax LE16, axisCoefficient LE16]` — mọi hành trình = mm × 1000. mode 0 = thường, 1 = RT. UI: hành trình 0.001–3.4 mm, dead-zone 0–1.

### 4.5 Phím nâng cao (category 6) [CODE]
Đầu khung `[6, Read=1|Write=2, row, col, mode]`, mode: 0 NONE · 1 DKS · 2 MPT · 3 MT · 4 TGL · 5 END · 6 SOCD · 7 RS. SOCD/RS ghi bằng **2 gói** (mỗi phím một gói, byte cặp SOCD theo bảng Cover/Ưu tiên A/Ưu tiên B/Trung lập). Macro: ID macro chính là keycode 0xF500–0xF50F (16 macro); dữ liệu macro đóng gói u32LE `(status<<31)|(delay<<16)|keycode`, delay tối đa 32767.

### 4.6 Màn hình / dot-matrix [CODE]
- **Họ này không có model TFT.** Displayer (category 12: 1=Info, 2=ImageResources, 3=UpgradeFlash, 4=GIF, 5=bootAnimation) có trong SDK nhưng không trang nào của họ gọi — **nằm im**. Nếu sau này dùng: nạp GIF = ghi flash từng khúc 56 B tại địa chỉ chạy, trường size = số byte thật ở khúc cuối.
- **BOX 63/63MAX có dot-matrix** ở vùng Decorate1 (126 ô, hiển thị 99 ô) và kho animation riêng qua CustomCommand (category 10): `[10,1, animIdx, totalFrame, targetFrame, packIdx, dataLenDiv4, 0, data 56 B]`, `[10,3]` chọn animation, `[10,5]` đặt tên UTF-8; bị khóa bởi `band:"magnegic"`.

### 4.7 ACK / nhịp [CODE]
- FIFO giống SparkLink V1 (không đối chiếu opcode), timeout **1000 ms**, thử lại **3 lần**. Sự kiện tự phát tra theo khóa tạo từ byte đầu (vd `"2-3-1"` = đổi profile → chờ 300 ms rồi nạp lại layout Fn).
- Nhịp: lệnh thường không delay; GIF stream như §4.3; poll màu realtime của trang đèn 16 ms; hiệu chuẩn trigger poll 60 ms; nhập cấu hình: chờ 500 ms sau khi ghi macro; firmware: chờ vào boot 6000 ms / về app 3000 ms.

---

---

## 5. Họ HFD (`hfd` + `hfdMechanical`) — họ lớn nhất

**Thiết bị:** hai wrapper dùng chung một SDK:
- `info-k_lmxSek.js` (`sdkModuleName="hfd"`, model key "Ka"): MINI 60 HE (0x0C45:0x8032/0x80A1/0x80A2/0x80B2, page 0xFF68), MINI 68 HE/PRO/MAX (0x38A6:0x2722/0x2723/0x2724), F75 HE (0x38A6:0x2729/0x2742), và **toàn bộ dongle 2.4G** của các dòng trên (0x0C45:0xFEF9–0xFEFE).
- `info-DZvc7M4P.js` (`sdkModuleName="hfdMechanical"`, model key "Pi"): F75 HE (10025/10050), F87S 8K (0x38A6:0x2908), F87ProV2C/D/Z, F108ProV2/C, NOVA98 (0x38A6:0x273B/0x275D).
**File:** SDK `hfd-sdk.es-CwIM3CGY.js` (chứa cả hai thế hệ — các hàm có hậu tố `_2` là thế hệ mới).

### 5.1 Transport & khung [CODE]
- WebHID, **output report id 0**; filter `{vendorId, productId, usagePage:65384, usage:97}` (0xFF68/0x61). **Kích thước gói đọc từ descriptor của thiết bị** (`outputReports[0].items[0].reportCount`, trừ 1 khi có report-id byte) — không phải hằng số: wired thường 32 B; nhánh dongle/màn hình tính riêng (xem 5.5).
- Khung yêu cầu (gói đầu tiên): `[0]=0xAA, [1]=cmd, [2]=độ dài payload của gói này, [3..4]=địa chỉ LE16, [5]=otherHeader0, [6]=otherHeader1, [7]=otherHeader2 (byte 6 = cờ "gói cuối"), [8…]=payload`. Các gói tiếp nối vẫn giữ header 0xAA; gói **cuối** được đánh dấu `[6]=0`.
  > `t[0] = 170; t[1] = r; t[2] = d.length; t[3] = e & 255; t[4] = e >> 8; … t[8 + m] = d[m]` — `hfd-sdk.es-CwIM3CGY.js`
- Phản hồi: `[0]=0x55, [1]=cmd, …` và wrapper chờ đúng `responseCmd` đã đăng ký; payload hữu ích nằm ở **offset 8**, ngoại trừ vài lệnh cũ (info ở offset 4).
- **Không checksum** ở đường dữ liệu chính (xác nhận bằng grep `checksum|& 255` trên toàn SDK — không có tổng kiểm). Ngoại lệ: lệnh nhạc 53 (xem 5.4) và đường dongle dùng cờ `sumCheck`.
- **Interface phụ:** mọi thao tác LED/TFT dung lượng lớn (animation, màn hình, ảnh) được chuyển sang **một thiết bị WebHID thứ hai** có `usagePage 65383` (**0xFF67**); kích thước khúc = `reportCount − 8` (fallback 4104). Chính vì vậy registry ghi `filterAlias` 0xFF67 cho các model này. [CODE]

### 5.2 Bảng opcode chính (byte 1) [CODE]
| Cmd | Chức năng |
|---:|---|
| 16 | GET_DEVICE_INFO — khối 56 B: vid/pid, số bản ghi LED, pin, `tftMaxFrames`, `gifMaxFrames`, `ledMaxFrames`, `rtPrecision`, cờ nhạc/nhạc cụ |
| 17 / 33 | Đọc / ghi mode game: `[0]=reportRate (0=125Hz…6=8KHz), [8]/[9]=dead-zone ÷100` + khối trạng thái 128 B (mỗi phím 1 B: 0x80=macro, 0x40=đã remap) |
| 18 / 34 | Đọc / ghi keymap lớp thường (128 slot × 4 B `[pageType, param1, param2, param3]`) |
| 22 / 38 | Keymap lớp Fn (cùng định dạng) |
| 19 / 35 | Đọc / ghi khối hiệu ứng LED 16 B (§5.3) |
| 20 / 36 | Đọc / ghi màu từng phím — 512 B = 128 × `[ledId, R, G, B]` |
| 51 | GET_ALL_LIGHTS_RGB — toàn bộ màu (đọc bằng id 51, **ghi bằng id 20/36**) |
| 21 / 37 | Macro: 100 macro; bảng mục lục 400 B (offset LE32, 4 B/macro) tại địa chỉ 0, bản ghi tại địa chỉ 400; event 4 B `[state, param1, param2, param3]`, tối đa 248 event/macro |
| 23 / 39 | Khối RT 1024 B = 128 × 8 B `[rtSwitch, travelLE16@2, rtPressLE16@4, rtRelLE16@6]` + cờ dead-zone; đơn vị = giá trị ÷ 100 |
| 24 / 40 | DKS 1024 B = 64 × 16 B (4 điểm hành trình LE16 + keycode LE32 ở 8–11) |
| 25 / 41 | Trigger/mode từng phím 1024 B |
| 26 / 42 | Light box (đèn hộp/logo) |
| 28 / 44 | SOCD (4 B/cặp) · 29 / 45 side light · 30 RS · 31 MT · 32 TGL · 47 END · 48 MPT (khối 1024 B = 128 × 8 B) |
| 42 / 61 | Cấu hình dot-matrix 9 B: `[effect, brightness, speed, R, G, B, count, direction, bgSwitch]` |
| 52 | Lệnh tạm: đồng hồ (payload 10 B bắt đầu `90,1,90`) và thông tin hệ thống (24 B) |
| 53 / 60 | Nhạc (§5.4) |
| 64 / 65 / 66 | Animation LED mở máy / animation LED người dùng / dữ liệu LED animation — **qua interface 0xFF67**; cmd 64 không thấy nơi gọi |
| 79 | Tải firmware xuống flash (địa chỉ đích 0x00190000 khi nâng cấp) |
| 80 / 81 | Animation TFT người dùng / chọn animation TFT có sẵn (§5.5) |
| 82 | Cập nhật trạng thái (4 B: mode, pin, sạc, trạng thái) |
| 96 / 100–106 | Hiệu chuẩn (96 = đặt 1 phím `[row, col, type]`; 100–106 = trạng thái/tiến trình toàn cục) |
| 250–253 | Thông báo từ thiết bị (250 = phím Fn nhấn; 252 sub 4 = ngắt kết nối, sub 6 = ngủ; 253 = sự kiện phím) |

**pageType** của ô keymap [CODE]: 0 DEFAULT · 1 MOUSE · 2 KEYBOARD · 3 CONSUMER · 4 SYSTEM · 5 EXTRA (dải nhạc/nhạc cụ) · 6 MACRO · 7 CB (combo: p1=modifier, p2=key) · 8 DKS · 9 MT · 10 TGL · 11 SOCD · 12 RS · 13 FUNC · 14 END · 15 MPT. Keycode nội bộ dạng HID usage (A=4 …) + mã đặc biệt (Win trái 227, macro 1024+id…).

### 5.3 Đèn [CODE]
- Khối hiệu ứng 16 B: `[0]=mode, [1]=brightness, [2]=speed, [3..5]=R,G,B, [6]=type, [7]=customFlag, [8..9]=thời gian ngủ LE16 (phút), [10]=hướng, [11]=độ bão hoà, [12]=fullColor, [13]=hiệu ứng theo nhạc, [14..15]=0xAA,0x55 (trailer)`.
- Mode: 1 = tĩnh · 2–19 = động (nhãn lấy từ locale: 1 Tĩnh, 2 Nhấp nháy, 3 Hít thở, 4 Cầu vồng, 5 Sóng ngang, 6 Sóng dọc, 7 Mưa, 8 Tuyết, 9 Mây, 10 Laser, 11 Bong bóng, 12 Mưa sao băng, 13 Nhạc sóng nước, 14 Lửa, 15 Thuỷ triều, 16 Ánh sáng sao, 17 Phản ứng nhấn, 18 Xoay, 19 Biến đổi) · 20 = tuỳ chỉnh · 0 = tắt.
- Màu từng phím **bền** qua cmd 20/36/51 như bảng trên — không cần commit riêng.
- GIF→LED: ghi animation người dùng (65/66) qua 0xFF67, không phải stream từng frame.

### 5.4 Nhạc / music rhythm [CODE]
- Cmd 53 (thế hệ cũ): gói 32 B, **byte 31 = tổng bytes 0–30 & 0xFF** (checksum duy nhất của họ); mỗi frame chứa phổ đã nén + độ sáng + mode + màu, màu chuyển qua **RGB565**; nhịp mặc định 50 ms (có thể ghi đè). Cmd 60 là biến thể mới không checksum.

### 5.5 Màn hình [CODE]
- **NOVA98 — TFT 240×135:** định dạng RGB565 LE. Stream animation = **bảng trễ 256 B** (`[0]=số frame, [1..n]=delay×5, [n]=0, đệm 0xFF`) nối trước các frame; header cmd 80 đặc biệt `[0xAA, 80, chunkIdxLE16, totalChunksLE16, 80, 6]` (không dùng khung địa chỉ thường) và phản hồi được chờ ở **responseCmd 65** (điểm lạ trong mã — ghi lại nguyên văn). Số frame tối đa lấy từ `tftMaxFrames` trong info (cmd 16); app tự co ảnh về đúng 240×135.
- **F108ProV2/C — dot-matrix, không phải TFT:** trang `pointScreen` dùng khối màu theo vùng 512 B (cmd 20) và lưới **7×7** suy ra từ layout [SUY LUẬN về số ô].
- **F87S 8K:** route bundle không có lớp màn hình (dù SDK có khối TFT chung) → coi là **không có màn hình** trong bản client này. Nhóm F87ProV2 có trang `pointScreen` trong bundle nhưng mức lộ ra phần cứng chưa xác nhận.
- TFT boot/logo tĩnh: không có đường riêng — logo đi qua cùng cmd 80/81 hoặc khối cấu hình màn hình.

### 5.6 Dongle 2.4G [CODE — điểm quan trọng]
Dongle **không** chuyển tiếp giao thức có dây: có bộ lệnh riêng (cmd 55 đọc thông tin, 59/60 đèn/nhạc), header 4 byte `[0xAA, cmd, lenLE16]`, màu theo **RGB565 2 B/phím** (không phải RGB888), byte 3 của phản hồi = pin. Định danh thiết bị sau dongle bằng bộ ba VID:PID:**tên sản phẩm HID**.

### 5.7 ACK / nhịp [CODE]
- Đối chiếu phản hồi theo cmd (+ địa chỉ khi ghi macro); timeout mặc định **500 ms**, thử lại **3 lần** (tham số mặc định của `sendHidCommand`); hàng đợi lệnh tuỳ chọn theo model.
- Nhịp: ghi LED/animation chờ 10 ms sau gói cuối; nạp firmware chờ 200 ms/frame; dongle chờ 100 ms.

---

## 6. Họ RLW — WIN 60/68 HE (0x2E3C:0xC365)

**Thiết bị:** WIN 68 HE (68 phím) và WIN 60 HE (61 phím) — cùng VID:PID, phân biệt bằng trường `modelKey`. Grid điện 6×22 = **132 slot**; slot có LED thật: 22…126.
**File:** `info-NelRB_nl.js` (lớp + toàn bộ giao thức), layout `RLW-C4jZkZ7B.js`, trang `light-BnVjQKVE.js`, `trigger-Cw6s5S0f.js`, `key-CdC4J3rR.js`, `high-DKqz1C8O.js`, `perf-Bj2tqz5c.js`.

### 6.1 Transport & khung [CODE]
- WebHID, **sendReport reportId 1**, gói **64 byte** (1 byte report-id + 63 byte payload). Filter `{vendorId:11836, productId:50245, usage:145, usagePage:65331}` (0xFF33/0x91).
- Khung: `[0]=opcode, [1]=sub/hướng, [2..3]=chỉ số trang BE16, [4]=độ dài, [5…]=dữ liệu`. **Không checksum, không magic.**
- Mọi khối lớn đều ghi theo trang; độ lệch trang tính bằng byte. Riêng custom colour dùng **từng trang 54 B (18 phím)**, keymap 56 B, macro 58 B, deadband 58 B, trigger 40/50 B.

### 6.2 Bảng opcode (byte 0) [CODE]
| Op | Chức năng |
|---:|---|
| 1 | Đọc khối hỗ trợ/thông tin 18 B (byte 5…): bit cờ lock, SOCD, nhạc, deadband…; byte 21 = nhạc đang bật |
| 2 / 10 | Đọc danh sách hiệu ứng đèn (ghi chú mã nguồn: "7 = ghi, 2 hoặc 10 = đọc") |
| 7 / 8 | Đọc / ghi cấu hình đèn 12 trường: mode, độ sáng, tốc độ, màu chính RGB, màu nền RGB, hướng, fullColor, công tắc nguồn |
| 9 | Màu tuỳ chỉnh 2 lớp × 396 B = 132 × RGB (truy vấn b1=32; đọc b1=128/129; ghi 8 trang × 54 B) |
| 11 | Bootloader · 13 = thông tin sản phẩm (chuỗi ASCII phân tách bằng dấu phẩy, trường [5] = version firmware) |
| 14 | Bitmap lớp Fn (528 B) |
| 20 | Lệnh phụ: khoá Win (byte 12–15), reset (b4=1, b5=1), khôi phục gốc (b1=2), thời gian ngủ (b1=2), gamepad (b1=3) |
| 24 | Keymap 132 × 4 B `[code1, hidCode, code3, code4]` (528 B; code1: 0 chuẩn, 7 consumer, 11 lock, 12 chuột, 13 combo, 16 macro, 17 đèn, 23 nâng cao, 24/25 gamepad) |
| 25 | Macro 10 slot × 256 B |
| 33 | Khối trigger (§6.3) |
| 34 | Phím nâng cao: **40 slot, ghi lại toàn bộ mỗi lần lưu** (gói b4=50; type: 1 DKS, 2 MT, 3 TGL, 4 RS, 5 RKRT) |
| 35 | Giám sát ADC: 22 giá trị BE16/hàng × 6 hàng |
| 36 | SOCD: 20 slot × mục 4 B `[en, key1, key2, mode]` |
| 37 | Trục: danh sách + dữ liệu từng phím theo trang |
| 38 | Deadband 132 × 2 B |

### 6.3 Trigger [CODE]
Khối con chọn bằng byte 5: 4 = đọc cấu hình toàn cục, 5 = đọc 1 phím, 6 = reset, 8 = bắt đầu/dừng hiệu chuẩn, 9 = lưu + **polling rate** (khi b4=1: mã 1/2/4/8 = 1K/2K/4K/8K), 15/16 = hiệu chuẩn bất kỳ phím, 17/18 = truy vấn/đặt profile. Bản ghi từng phím **31 B**: mode (0 thường, 13 RT), bitmap 22 cột của hàng, các giá trị hành trình ×100.

### 6.4 ACK / nhịp [CODE]
- Đối chiếu phản hồi theo opcode; timeout **3000 ms** (5000 ms với thao tác lưu/nặng); **writeDelay 5 ms** giữa các lệnh ghi (đây là nhịp gửi duy nhất của họ — rất thấp); cửa sổ loại phản hồi cũ 200 ms; trang rảnh 300 ms thì tự hủy bộ hẹn giờ.
- **Không có stream từng phím** — màu tuỳ chỉnh là ghi bền (op 9). Xem đối chiếu mục 12 về điểm này.

---

---

## 7. Họ Bytech HE — dòng HERO (0x372E:0x103E)

**Thiết bị:** HERO 68 HE / MINI / XS / Air / MINI Air, HERO 84 HE, HERO 99 HE, HERO 108, AULA HERO 75 HE, WIN68HE Ultra — tất cả dùng **chung VID:PID 0x372E:0x103E**, page 0xFF60, usage 0x61; phân biệt model bằng UUID + `layoutClass` (mỗi model một lớp layout riêng). Receiver 8K: PID 0x106C.
**File:** lớp thiết bị `Sc` + codec `RV` nằm **ngay trong bundle chính** `index-npIkQlTa.js` (không phải file SDK riêng); wrapper `info-BcGqMv9r.js` (`sdkModuleName="bytechHe"`); trang `light-Kx6D4dL6.js`, `key-DbP3U5cA.js`, `trigger-BtJk3m3u.js`, `high-BiTBx3yq.js`, `perf-CMGHwU7E.js`, `version-nQ4tW4fY.js`.

### 7.1 Transport & khung [CODE]
- Có dây: **feature report** — `sendFeatureReport(reportId, data)`; reportId lấy từ feature report đầu tiên của descriptor (bản ghi cấu hình dùng 9).
- Không dây: lớp `Wireless8K` riêng (timeout 2000 ms, có khung ACK; kích thước payload tối đa mỗi gói **không khôi phục được** từ phần đã đọc).
- Cấu hình codec:
  > `TV = { totalPacketSize: 63, headerLength: 6, tailLength: 1, commandIdIndex: 0, commandParamIndex: 1, totalPacketLengthIndex: 3, currentPacketIndex: 4, lengthIndex: 5, checksumIndex: 62 }` — `index-npIkQlTa.js`
- Khung: `[0]=cmd, [1]=param, [2]=0, [3]=tổng số gói, [4]=chỉ số gói, [5]=độ dài payload, [6…]=payload, [62]=checksum`.
- **Checksum (byte 62):** `255 − ((reportId + Σ mọi byte của gói, với byte checksum tạm = 0) % 256)` — tức tổng gồm reportId phải ≡ 0xFF (mod 256).
  > `calculateCrc(e){ … n = (n + (e[t]||0)) % 256 …; return 255 - n }` — codec `RV`, `index-npIkQlTa.js`
  **Đính chính quan trọng:** con số "9" trong ghi chép cũ về họ này chính là **reportId của thiết bị**, không phải hằng số của công thức (xem đối chiếu mục 12).
- ACK: đối chiếu theo cmd; timeout mặc định **1000 ms**, thử lại 1 lần; độ trễ nhận gói có thể cấu hình (`receiveDelay`).

### 7.2 Bảng lệnh chính [CODE]
| Cmd | Chức năng |
|---:|---|
| 132 (param = trường) | Đọc cấu hình: 21 = khoá Win · 23 = polling · 24/29/30/32/33 = các khối cấu hình khác |
| 4 (param như trên) | Ghi cấu hình tương ứng |
| 135 | Đọc pin |
| 130 (param 9) | Đọc năng lực: cờ ở payload[17] {musicMain, musicSpectrum, musicSide} · [18] sideLight · [19] logoLight · [20] số đèn cạnh |
| 18 | Keymap |
| 6 | Màu từng phím — mục 5 B `[idHi, idLo, R, G, B]` (đọc ngược bằng cmd 134) |
| 8 (param 1) | Màu theo nhóm: khúc **48 mục** `[R, G, B, count, keyId…]` |
| 8 (param 2) | Màu đơn: `[R, G, B, idHi, idLo]` |
| 146 / 150 / 152 / 154 / 161 / 165 | SOCD / trigger / phím nâng cao (bảng lệnh đầy đủ trong báo cáo trích xuất) |
| 254 | Sự kiện từ dongle: param 2 = kết nối · 5 = pin · 7/9 = profile/hệ thống · 11 = đèn |

### 7.3 Đèn & stream [CODE]
- Hiệu ứng đèn ghi qua cmd 4 với các trường tương ứng; trang đèn chính (`light-Kx6D4dL6.js`) có danh sách 18 chế độ + custom. Đường gửi nhanh không dây cho đèn dùng `sendReport` trực tiếp với gói bắt đầu `[9, 8, total, index, …]` — số 9 ở đầu chính là reportId.
- **Không có stream khung hình realtime**; từng phím là ghi bền qua cmd 6/8.

---

## 8. Họ Bytech cơ (BPTK) — nền tảng 0x258A:0x010C

**Thiết bị:** mục registry "Gaming Keyboard" 0x258A:0x010C, page 0xFF00, usage 0x01 — layoutClass gồm **F75, F87, F87Pro** (bản web còn thêm F99 và AULA-F68 0x258A:0x019D — xem mục 9). Đây là **cùng nền tảng Sinowealth** với AULA F75 bản thường đã có trong kho giao thức của T Keyboard.
**File:** `index-BPTK-vUa.js` (các lớp F/P/X), wrapper `info-ChgNybWD.js` (`sdkModuleName="bytechMechanical"`), trang `light-CsR8K0qR.js`, `key-CyPWS2bV.js`, `macroSet-Dc0jH6fV.js`, `version-BaxvFQW7.js`. (File `Bytech-DuNSRtja.js` chỉ chứa dữ liệu layout, không có giao thức.)

### 8.1 Transport & khung [CODE]
- **Feature report**, đệm payload cố định **519 byte**; reportId lấy từ feature report đầu tiên của descriptor.
  > `payloadBuffer: new ArrayBuffer(519)` — `index-BPTK-vUa.js`
- Khung: `[0]=cmd, [1]=param/layer, [2]=0, [3]=1, [4]=0, [5..6]=độ dài LE16, [7…]=payload`.
- Gói mẫu nguyên văn trong mã [CODE]:
  - Thông tin thiết bị: `[130, 1, 0, 1, 0, 10, 0]`
  - Đọc phím: `[131, …, 248, 1]` (248 = độ dài mỗi trang)
  - Đọc profile: `[132, 0, 0, 1, 0, 128]` · Đọc màu tuỳ chỉnh: `[132, t, 0, 1, 0, 125, 0]`
  - Ghi phím: `[1, s, 0, 1, 1, 248, 0, …]` · Ghi đèn: `[3, 0, 1, 0, 0, 41, …]`
- Bundle chứa **ba thế hệ lớp** cùng lúc: lớp feature-report ở trên; một lớp input/output với gói `[cmd, total, index, 14, payload 14 B, checksum]` và trailer macro/đèn `0x5A 0xA5`; và một **lớp ảnh/TFT** 519 B: gói GIF `[12, 0, total, index, 0, 0, 2, …khúc 512 B]`, delay 30/50 ms, checksum = tổng hai byte header + payload & 0xFF. [CODE]
  > Ghi chú trung thực: lớp ảnh/TFT tồn tại trong mã BPTK nhưng các model cơ của AULA trong registry (F75/F87/F87Pro) không có màn hình — lớp này có thể phục vụ model khác cùng nền tảng hoặc là mã dùng chung chưa gắn thiết bị. [SUY LUẬN]

---

## 9. Web hub — F75 Max và các model chỉ có trên web

Bản web (`web/assets/`) bị obfuscate bằng javascript-obfuscator. Đợt này đã **giải mã toàn bộ**: bảng chuỗi 17.544 mục (mã hoá RC4 + base64 tuỳ biến), 17.538 cặp (index, khoá) giải thành công 100%, bảng thiết bị 153 mục đọc được nguyên văn. Chi tiết phương pháp: báo cáo phân tích web kèm trong bộ trích xuất.

### 9.1 Nhóm F75 Max trên web [CODE]
```
{'pName': "AULA F75Max", 'pId': 0x800a, 'vId': 0xc45, 'wireless': 0,
 'usage': 0x1, 'usagePage': 0xff13, 'filterAlias': {'usagePage': [0xff68]}}
```
- **Khớp hoàn toàn** với nền tảng đã biết: VID:PID **0x0C45:0x800A**, kênh lệnh **0xFF13**, kênh màn hình **0xFF68** xuất hiện ngay trong filter của thiết bị (`filterAlias`). Nhóm này có lớp `TFT_Screen_Class` riêng; chuỗi UI đã giải mã xác nhận màn hình chỉ đổi được qua cáp: *"The screen can only be changed over the cable."*
- **AULA F108Pro dùng chung VID:PID 0x0C45:0x800A / 0xFF13** với F75 Max (cờ `customDevice`) — hai model **không phân biệt được bằng VID:PID**; bundle chính có một gói thăm dò duy nhất `[0x82, 0x01, 0x00, 0x01, 0x00, 0x06, 0x00]` gửi theo từng collection, timeout 519 ms, đáp án được phân tích thành một ID số — [SUY LUẬN] đây là bước định danh phân biệt hai model này (hình dạng gói trùng với lệnh info `[130, 1, 0, 1, 0, len, 0]` của họ Bytech cơ ở mục 8).
- Dongle của nhóm: 0x05AC:0x024F, page 0xFF60 — khớp dongle đã biết của F75 Max.

### 9.2 Giới hạn của đợt trích web [trung thực]
- **Tầng lệnh 04 xx của F75 Max KHÔNG nằm trong các file web hiện có.** Logic từng model được tải lười qua 311 chunk; sáu chunk của nhóm F75 Max (`Mu_Jpyml.js` = Info, `CCpdGkxE.js` = Light, `C8c6Bu5v.js` = Key, `Cw0Gn4la.js` = TFT, `CzTL5VnB.js` = Perf, `Bz1v_BL0.js` = Version) **không có trong thư mục `web/assets/`** (chỉ có 3/311 chunk). Vì vậy đợt này **không xác nhận cũng không phủ nhận** được thêm gì về giao thức 04 18/04 13/04 17/04 28 đã có — phần đó vẫn dựa trên tài liệu `PROTOCOL-F75MAX.md` hiện hữu.
- Những gì web cho thêm: bảng thiết bị đầy đủ hơn client (thêm F75 V2, F75 Master, Epomaker x Aula F75 V2, NOVA75 Pro, NOVA87, F99, F108Pro, HERO 108…), họ cơ Sinowealth trên web dùng **uuid phân biệt model** (F75 `0x300000000cd`, F87 `0x3000000008f`, F87Pro `0x300000003e3`, F99 `0x300000000a4`) vì tên HID chung là "Gaming Keyboard", và WIN 60/68 HE trên web khai báo page khớp là **0xFF1B** (khác 0xFF33 của client/shop — nhiều khả năng là interface phụ; khi làm driver phải quét cả hai).
- **Không có S75 Pro** trong bảng web (tìm "S75" chỉ trúng một blob icon SVG). [CODE]

---

## 10. Họ VIA/Vial mở rộng — AULA-Neos75

**Thiết bị:** AULA-Neos75, VID **0x1EA7**, PID **0x4884** (18564), page 0xFF60, usage 0x61; dongle riêng 0x6D66:0x8572. Đây là họ **mới hoàn toàn** so với kho giao thức hiện có của T Keyboard.
**File:** `info-DRiAKzxv.js` (toàn bộ stack). (`WYFX-SGL1Vmc5.js` chỉ là chunk layout 84 phím dùng chung — không phải giao thức.)

### 10.1 Transport & khung [CODE]
- WebHID, **output report id 0**, gói cố định **32 byte**. Nhịp gửi 2 ms (vòng đệm 1 ms; ghi macro cách 5 ms).
- Lệnh VIA/Vial chuẩn đi nguyên dạng; lệnh **custom** có khung `[cmd, mode, …tham số, CRC ở byte 6–7]`: **CRC-16 bảng, poly 0x1021, init 0xFFFF**, đầu ra bị **đảo byte** rồi lưu big-endian vào gói. Gói bắt đầu bằng 254 (Vial) thì **không có CRC**.
  > `static crc16(i){let t=65535;for(…){t=t<<8^CMD_WYFX.vialTable[…]}…}` · `r[6]=n>>8&255, r[7]=n&255` — `info-DRiAKzxv.js`
- ACK: phản hồi phải trùng cmd+mode và byte trạng thái = 0; timeout 1000 ms, thử lại 2–3 lần; sai CRC khi đọc → thử lại tối đa 3 lần.

### 10.2 Lệnh [CODE]
- **VIA chuẩn:** 0 ghi dữ liệu · 1 version giao thức · 2/3 đọc/ghi giá trị · 4/5 keycode BE16 · 7/8/9 đèn set/get/save · 12–15 buffer macro (28 B/gói) · 17 số layer · 18 buffer keymap · **254 = tiền tố Vial** (id, kích thước, definition, encoder, unlock). Definition Vial được lấy khi protocol ≥ 12 và giải nén LZMA ngay trong trang.
- **Đèn:** qua dataId VIA — 1 độ sáng, 2 hiệu ứng, 3 tốc độ, 4 màu; **màu lưu dạng HSV**; bảng ID hiệu ứng **không có trong mã** (truyền thẳng, 0 = tắt) — muốn liệt kê hiệu ứng phải dựa vào definition của thiết bị.
- **Custom:** 17 = thông tin thiết bị (mode 1–8 + 13 = polling) · 19 = ghi (mode 6 = polling rate, giá trị 1–8 ↔ 1K–8K; mode 3 = khôi phục gốc) · 21 = commit/lưu (mode 1) · **29 = HE**: T=4 khối performance (cờ RT + 11 trường LE16 ×1000 từ byte 9), T=5/6 = DKS, T=7 = RS, T=8 = SOCD, T=0–2 = hiệu chuẩn · 254 mode 13/14 = bản ghi MT/TGL (đi kèm SET_KEYCODE 22272+idx cho MT, 22336+idx cho TGL; macro gán bằng keycode 30464+idx).
- **Không có** ghi màu từng phím, không stream, không TFT trong stack này.

---

## 11. Các họ còn lại: Infortech & Evison

- **Infortech** [CODE một phần]: chỉ phục vụ **chuột** trong bộ file này — lớp thiết bị nằm inline trong bundle chính, bị obfuscate nặng (`"InfortechDevice init."` là chuỗi rõ duy nhất liên quan), page 0xFF78, không có VID cứng (filter đăng ký lúc chạy; registry gắn với chuột VID 0x384C và một phần 0x38A6). **Không khôi phục được nội dung giao thức** trong giới hạn đợt này — ghi rõ là khoảng trống, không đoán.
- **Evison**: vắng mặt hoàn toàn trong client 1.1.22 (0 kết quả). DOSSIER trước cho biết SDK Evison được tải từ server — ngoài phạm vi bộ file tĩnh này.

---

---

## 12. Đối chiếu với kho giao thức đã có của T Keyboard

Tham chiếu: `~/workspace/shop-protocol-extract/DOSSIER.md` (app của shop — 12 model / 6 họ A–F) và `~/workspace/T-Keyboard-Studio-repo/docs/PROTOCOL-F75MAX.md`.

| Họ trong app shop | Họ tương ứng ở AULA Hub | Kết luận đối chiếu |
|---|---|---|
| **A** — Sinowealth 519 B, 0x258A:0x010C, page 0xFF00 | **Bytech cơ (BPTK)**, mục 8 | **KHỚP nền tảng:** cùng VID:PID, cùng độ dài payload 519 B, cùng hình dạng khung `[cmd, param, 0, 1, 0, lenLE16]`, cùng gói info `[130, 1, 0, 1, 0, len, 0]`. Hai app độc lập dùng chung một giao thức — độ tin cậy cho driver F75 bản thường tăng mạnh. |
| **B** — SONiX 0x0C45:0x800A (S75 Pro / Nova75) | Không có trong client; web chỉ có nhóm F75 Max cùng VID:PID | Client 1.1.22 **không hỗ trợ** S75 Pro/Nova75. Web xác nhận nền tảng 0xFF13 + alias 0xFF68 cho F75 Max/F108Pro. Giao thức stream 0x20 của họ B (từ app shop) vẫn là nguồn duy nhất cho S75 Pro — giữ nguyên trạng thái "đã trích từ app shop, chờ kiểm chứng phần cứng". |
| **C** — MAD60 (0x373B) | Không có | AULA Hub không chứa họ MADLIONS — không thêm được gì. |
| **D** — HERO68 HE, 0x372E:0x103E, checksum ở byte 62 | **Bytech HE (Sc/RV)**, mục 7 | **KHỚP khung gói:** gói 63 B, header 6, checksum byte 62, điều kiện `(9 + Σ) ≡ 0xFF`. **ĐÍNH CHÍNH:** số "9" là **reportId** của thiết bị (công thức thật: `255 − ((reportId + Σ) % 256)`); app shop tình cờ dùng reportId 9 nên hai bên khớp nhau. **KHÔNG KHỚP:** lệnh định danh (130, param 1) + đáp án mẫu `[17,0,0,0,0,3]` của app shop **không tồn tại** trong mã chính hãng — đó là phương ngữ của họ Bytech *cơ* (mục 8), không phải của họ HE này; và nhịp gửi ≥34 ms của app shop cũng không có trong mã chính hãng (đường ảnh của BPTK dùng 30/50 ms). Khi viết driver 372E phải theo mã chính hãng ở mục 7. |
| **E** — WIN60 HE, 0x2E3C:0xC365, page 0xFF33 | **RLW**, mục 6 | **KHỚP vận chuyển:** reportId 1, payload 63 B, opcode ở byte 0, cấu hình đèn op 7 gồm 12 trường, lưới 132 slot, trang màu tuỳ chỉnh 54 B. **MÂU THUẪN LỚN — cần phân xử bằng phần cứng:** app shop ghi nhận stream từng phím bằng cmd 23 (bật) + cmd 15 (dữ liệu frame) + heartbeat cmd 1 + nhịp 2 ms. Trong mã chính hãng: **op 15 và op 23 không tồn tại, op 1 là lệnh đọc thông tin, nhịp ghi là 5 ms** và màu từng phím là **ghi bền** (op 9). Hoặc app shop dùng đường firmware không có trong driver chính hãng, hoặc phần ghi chép về họ E của app shop bị đọc nhầm. **Không được viết driver stream cho WIN60/68 HE dựa trên ghi chép của app shop khi chưa kiểm chứng trên thiết bị thật.** |
| **F** — New Star 0x28E9 + WIN60 SE 0x38A6:0x291A | WIN60 SE có trong bảng web (0x38A6:0x291A, page 0xFF00) | Model có mặt ở cả hai app với cùng VID:PID. Trong AULA Hub, WIN60 SE **không gắn với SDK SparkLink V2** (page khác hẳn: 0xFF00 vs 0xFFB0) — giữ nguyên họ F riêng như hồ sơ shop. New Star 0x28E9: không có trong AULA Hub. |
| — (không có ở app shop) | **SparkLink V1** (mục 3) | **MỚI.** Dòng HERO 68 HE ULTRA/PRO + WIN 60/68 HE PRO/MAX trên chip 0x1CA5/0x1CA2 — khác hoàn toàn WIN HE của họ RLW dù tên gần giống. Cẩn thận khi đặt tên driver: "WIN 68 HE" tồn tại ở **ba** họ khác nhau (RLW 0x2E3C, SparkLink V1 0x1CA2, Bytech HE 0x372E "WIN68HE Ultra"). |
| — | **SparkLink V2** (mục 4) | **MỚI.** Dòng AG/BOX/EVO trên 0x38A6:0xFFB0. Lưu ý **VID 0x38A6 xuất hiện ở cả hai app với giao thức khác nhau**: app shop dùng 0x38A6:0x291A page 0xFF00 (WIN60 SE); AULA Hub dùng 0x38A6 page 0xFFB0 (SparkLink V2) và page 0xFF68 (HFD). **Tuyệt đối không suy ra giao thức từ VID** — phải khớp cả PID + usage page, và thăm dò trước khi ghi. |
| — | **HFD** (mục 5) | **MỚI — giá trị lớn nhất của đợt này.** Một driver HFD bao phủ: MINI 60 HE trên **0x0C45** (cùng VID với F75 Max nhưng PID/page khác hẳn: 0xFF68, khung 0xAA — không liên quan giao thức 04 xx), toàn bộ dongle 0x0C45:0xFEF9–0xFEFE, và dòng HE 0x38A6 đời mới (F75 HE, F87 Pro V2, F108 Pro V2, NOVA98…). Kèm giao thức **TFT 240×135 cho NOVA98** và đường interface phụ 0xFF67. |
| — | **VIA/Vial Neos75** (mục 10) | **MỚI.** Neos75 chạy stack VIA/Vial mở rộng — nghĩa là một phần lớn khả năng tương thích có thể "đi nhờ" hệ sinh thái VIA/Vial công khai, chỉ cần bổ sung lệnh custom 17/19/21/29 và CRC-16. |
| F75 Max (tài liệu riêng) | Nhóm web `fg` (mục 9) | Web **xác nhận độc lập**: VID:PID, kênh 0xFF13, kênh màn hình 0xFF68 nằm trong filter, có lớp TFT riêng, màn hình chỉ qua cáp, dongle 0x05AC:0x024F. Tầng lệnh 04 xx không có trong bộ file web (chunk lười bị thiếu) — tài liệu F75 Max hiện hữu **không đổi**. Thêm cảnh báo mới: **F108Pro trùng VID:PID với F75 Max** — bước quét thiết bị của T Keyboard phải thêm bước thăm dò định danh trước khi áp profile Max. |

### Hệ quả cho lộ trình T Keyboard Studio
1. **Ưu tiên mới cao nhất: driver HFD** — một họ phủ ~49/114 mục thiết bị, gồm nhiều model HE bán chạy (F87 Pro V2, F75 HE, NOVA98 có TFT). Đã có đủ opcode/khung/định dạng để viết driver khô (dry-run) ngay.
2. **Driver Bytech cơ (258A:010C)** giờ có **hai nguồn độc lập khớp nhau** (app shop họ A + BPTK chính hãng) → có thể nâng trạng thái tin cậy của driver F75 bản thường.
3. **Driver 372E (HERO)** viết theo mục 7 (mã chính hãng), bỏ qua phương ngữ định danh của app shop.
4. **WIN 60/68 HE (RLW):** chỉ hỗ trợ ghi bền theo mục 6; tính năng stream theo app shop **đóng băng** cho tới khi kiểm chứng phần cứng.
5. **SparkLink V1/V2:** hai driver mới, độ phủ tốt (dòng AG rất phổ biến); V2 có sẵn đường stream DirectDrive + GIF — gần với tính năng GIF→LED mà T Keyboard đã có.
6. **Neos75/VIA:** cân nhắc một driver VIA/Vial tổng quát (đúng hướng lộ trình Keychron VIA đã có) + lớp custom mỏng cho Neos75.
7. **An toàn nhận diện:** quét thiết bị phải khớp **VID + PID + usage page** và thăm dò định danh ở các nền tảng trùng VID:PID (0x0C45:0x800A = F75 Max/F108Pro; 0x372E:0x103E = cả dòng HERO; 0x258A:0x010C = nhiều board Sinowealth; 0x38A6 = ba họ khác nhau).

---

## 13. Độ bao phủ & giới hạn (nói thẳng)

**Đã trích chắc chắn [CODE]:** SparkLink V1, SparkLink V2, HFD (cả hai wrapper), RLW, Bytech HE, Bytech cơ, VIA/Vial-Neos75 — đủ khung gói, bảng lệnh, định dạng đèn/trigger/macro và nhịp/ACK để viết driver khô cho từng họ. Bảng thiết bị: client 117 mục (75 bàn phím) + web 153 mục đã giải mã 100% chuỗi.

**Chưa/không trích được:**
- **F75 Max tầng lệnh 04 xx từ phía AULA:** không có trong client (client không hỗ trợ Max) và các chunk lười của web bị thiếu trong bộ file — không phải lỗi giải mã, là thiếu file. Muốn bổ sung phải tải được 6 chunk đã nêu ở mục 9.2.
- **Infortech (chuột):** mã bị obfuscate, chỉ xác nhận được sự tồn tại + page 0xFF78.
- **Evison:** không có trong client 1.1.22; SDK của nó nằm ở phía server/web theo DOSSIER cũ.
- **Wireless8K (họ Bytech HE không dây):** chưa khôi phục được kích thước payload tối đa mỗi gói.
- **Vài điểm nhỏ ghi rõ trong từng mục:** công thức giảm sáng của SparkLink V1 khi mode tĩnh; thứ tự trường byte 6–7 của macro BPTK; số ô lưới dot-matrix của F108ProV2 (7×7 là suy luận); việc F87ProV2 có lộ dot-matrix ra phần cứng hay không; ý nghĩa các trường phụ trong đáp án lệnh 130 của họ 372E theo cách hiểu của app shop.
- **Mâu thuẫn RLW/app-shop (mục 12, họ E)** là điểm chưa phân xử được bằng mã — cần thiết bị WIN 60/68 HE thật để kiểm chứng trước khi tin bên nào về stream.

*Mọi phân tích tĩnh, chưa từng chạy app hay chạm vào thiết bị. Các hằng số trong tài liệu này đều trích từ mã nguồn frontend/SDK của AULA Hub 1.1.22 và web hub tại thời điểm trích xuất (27/11/2026).*
