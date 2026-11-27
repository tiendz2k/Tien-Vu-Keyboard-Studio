# T Keyboard Studio

Ứng dụng Windows điều khiển đèn RGB, màn hình TFT và macro cho các mẫu bàn phím phổ biến — đa hãng, xây dựng trên các giao thức đã được cộng đồng mã nguồn mở công bố.

T Keyboard Studio được phát triển với triết lý: **chỉ tích hợp những giao thức đã công khai** (SDK chính thức, VIA/QMK, hoặc driver mã nguồn mở đã công bố tài liệu), kiểm chứng cẩn thận từng bước trước khi ghi xuống phần cứng thật, và tuyệt đối không thu thập dữ liệu người dùng.

---

## Tính năng hiện có

### Bản chính (dòng v3.1 — cho AULA F75 Max)
- **Quét & nhận diện thiết bị** qua USB VID/PID, hiển thị trạng thái kết nối
- **Ánh sáng RGB**: 20 hiệu ứng đèn tích hợp trong firmware, chỉnh màu / độ sáng / tốc độ
- **Màn hình TFT 128×128**: nạp ảnh/GIF xuống màn hình phím (chuẩn hoá tự động về 128×128, tối đa 255 khung hình, 3 chế độ Fill / Fit / Stretch)
- **Thư viện GIF tích hợp**: 21 GIF đi kèm theo 3 nhóm chủ đề, cộng thêm khả năng tự thêm GIF từ thư mục, dán link tải về, hoặc kéo–thả
- **Thư viện hiệu ứng cộng đồng**: duyệt và lấy về các hiệu ứng LED được cộng đồng chia sẻ
- **Đồng bộ đồng hồ** xuống bàn phím
- **Dry-run mặc định BẬT**: mọi lệnh ghi đều được mô phỏng và ghi log trước; chỉ ghi thật khi người dùng chủ động tắt Dry-run

### Bản xem trước giao diện (thư mục `preview/`)
Bản dựng Avalonia (.NET 8) thể hiện giao diện thế hệ mới của app — sạch, hiện đại, đa thiết bị — gồm 8 trang: Tổng quan (quét thiết bị + thư viện mẫu phím), Ánh sáng RGB, Theo nhạc (mô phỏng), Màn hình GIF, Thư viện Online, Widget TFT, Đồng hồ & Cài đặt, và Điều khiển từ điện thoại. Đây là bản **UI-only** (không chứa mã giao tiếp phần cứng) để duyệt giao diện trước khi tích hợp.

## Lộ trình phát triển (Roadmap)

- [ ] **RGB từng phím (per-key)** và **stream GIF → đèn LED toàn phím** cho F75 Max — giao thức đã có tài liệu (xem `docs/PROTOCOL-F75MAX.md`), đang ở giai đoạn kiểm chứng an toàn trên phần cứng
- [ ] **Chế độ nháy đèn theo nhạc** (phân tích âm thanh trên PC, stream xuống phím)
- [ ] **Widget trên màn hình TFT**: thông số máy (CPU/GPU/RAM/nhiệt độ), nhạc đang phát, đồng hồ & thời tiết tuỳ biến
- [ ] **Điều khiển từ điện thoại**: PC cắm phím hiện mã QR, điện thoại quét (cùng Wi-Fi nhà) để đổi hiệu ứng / nạp GIF từ xa
- [ ] **Driver đa hãng**: AULA F75, Leobog Hi75C Pro, họ Keychron VIA/QMK, Wooting (SDK chính thức), MADLIONS MAD60 HE…
- [ ] **Giao diện theo khả năng phần cứng**: phím có màn hình mới hiện mục Màn hình GIF / Widget TFT; phím không có thì ẩn hẳn

## Thiết bị được hỗ trợ

| Mẫu phím | Trạng thái | Ghi chú giao thức |
|---|---|---|
| AULA F75 Max | ✅ Hỗ trợ đầy đủ | Giao thức đã kiểm chứng chéo từ 2 driver mã nguồn mở độc lập |
| AULA F75 (bản thường) | 🔜 Kế hoạch | Giao thức Sinowealth đã được nhiều driver mã nguồn mở công bố chi tiết |
| Leobog Hi75 / Hi75C Pro | 🔜 Kế hoạch | Chung nền tảng giao thức với AULA F75 |
| Keychron họ VIA/QMK (V/Q/K Pro/K Max…) | 🔜 Kế hoạch | Một driver VIA chung mở khoá nhiều mẫu |
| Wooting 60HE / 80HE | 🔜 Kế hoạch | SDK chính thức mã nguồn mở của hãng |
| MADLIONS MAD60 HE / MAD68 HE | 🔜 Kế hoạch | Giao thức đã được dự án OpenMAD công bố |
| Royal Kludge (dòng classic), DrunkDeer, AJAZZ AK820 | 🔜 Hàng chờ | Đã có cộng đồng công bố giao thức |
| AULA S75 Pro / Nova75 / dòng WIN HE, Akko, EPOMAKER, GravaStar, MelGeek… | ⏸ Để sau | Giao thức kín, chưa có tài liệu công khai — app chỉ hiển thị "đang phát triển" |

> ⚠️ Lưu ý nhận diện: một số mã VID/PID (ví dụ `258A:010C`) được **nhiều mẫu phím không liên quan dùng chung**. App luôn dò mã model của thiết bị trước khi cho phép ghi — không bao giờ dựa vào VID/PID đơn thuần.

## Bảng thiết bị cộng đồng (devices-registry.json)

Danh sách mẫu phím của app có thể được bổ sung mà **không cần phát hành bản app mới**: file [`docs/devices-registry.json`](docs/devices-registry.json) trong repo này là nguồn bảng mới nhất. Trong app: **Cài đặt → Bảng thiết bị cộng đồng → "Kiểm tra cập nhật bảng thiết bị"** (app chỉ tải khi bạn bấm — không tự gọi mạng nền). File tải về được kiểm tra định dạng (JSON hợp lệ, 10–5000 mục, không trùng tên, VID/PID trong 0–0xFFFF), lưu vào `%AppData%\TienKeyboardStudio\devices-registry.json` và áp dụng ở lần mở app sau — chỉ **bổ sung** mẫu mới, không ghi đè dữ liệu tích hợp; bảng tích hợp luôn là dự phòng.

Cấu trúc mỗi mục trong mảng `devices`:

```json
{
  "name": "Tên mẫu (duy nhất)",
  "brand": "Hãng",
  "layout": "75% · 80 phím",
  "family": "sonix_sib",
  "stack": "B",
  "hasScreen": true, "screenW": 128, "screenH": 128,
  "hasHE": false, "hasKnob": true,
  "statusKind": 1,
  "protocolStatus": "Mô tả trạng thái giao thức",
  "idNote": "Ghi chú cách nhận diện",
  "devIds": [],
  "ids": [[3141, 32778]]
}
```

- `ids`: các cặp `[vid, pid]` dạng **số thập phân**; `pid = -1` = khớp mọi PID của VID đó. `ids` rỗng = mục chỉ hiển thị (chưa biết VID:PID).
- `family`: `kb_by_v2` | `rongyuan` | `mchose` | `sonix_sib` | `witmod` | trống. `stack`: stack trong họ (B/C/D/E/F2 của họ SONiX; GLW/QHW của MCHOSE…).

## Đóng góp thông tin thiết bị

Bạn có mẫu phím chưa được hỗ trợ (hoặc hỗ trợ chưa đầy đủ)? Hãy giúp cộng đồng:

1. Cắm bàn phím bằng **USB có dây**, mở app, bấm **Quét bàn phím** ở trang Tổng quan.
2. Nếu mẫu thuộc một họ driver đã có trang riêng (Sinowealth, RongYuan, MCHOSE, SONiX, WitMod), mở trang họ đó và chạy **Nhận diện / Probe** (các probe đều là lệnh đọc, an toàn).
3. Bấm nút **"Sao chép thông tin thiết bị"** (có ở trang Tổng quan và mọi trang họ) — đoạn text gồm VID:PID, tên sản phẩm, usage page/usage, kích thước report, kết quả probe (UUID / dev_id / chuỗi sản phẩm 0x0D / tóm tắt 0xF5…), mẫu đang chọn và phiên bản app.
4. Mở một **GitHub issue** trong repo này, dán đoạn vừa sao chép, kèm tên mẫu chính xác, link sản phẩm (nếu có) và ghi rõ probe nào chạy được / không chạy được.

Thông tin này giúp xác định VID:PID và định danh thật của mẫu để đưa vào bảng thiết bị hoặc viết driver ở bản sau. Đoạn sao chép chỉ chứa thông tin phần cứng của bàn phím — **không** kèm thông tin cá nhân nào.

## Nguyên tắc an toàn

1. **Dry-run là mặc định** — không lệnh ghi nào xuống phím khi chưa được người dùng chủ động cho phép.
2. **Kiểm chứng trước khi ghi thật** — mọi giao thức mới đều đi qua thang kiểm chứng: mô phỏng → đọc thử (read-only) → ghi tối thiểu → ghi đầy đủ.
3. **Factory reset trước tiên** — trước bất kỳ thử nghiệm ghi nào, quy trình khôi phục cài đặt gốc phải được xác nhận hoạt động.
4. **Không telemetry** — app không thu thập, không gửi dữ liệu đi đâu. Mạng chỉ dùng khi người dùng chủ động tải GIF.
5. **Mã nguồn mở về giao thức** — tài liệu giao thức tổng hợp được công bố trong thư mục `docs/` để cộng đồng cùng kiểm chứng.

## Tài liệu

- [`docs/PROTOCOL-F75MAX.md`](docs/PROTOCOL-F75MAX.md) — Tổng hợp giao thức HID của AULA F75 Max: các khối lệnh đã kiểm chứng và các phát hiện per-key / streaming / music từ nền tảng anh em L99.
- [`docs/PROTOCOLS-REFERENCE-APP.md`](docs/PROTOCOLS-REFERENCE-APP.md) — Hồ sơ trích xuất giao thức từ một app tham chiếu: 12 mẫu phím thuộc 6 họ giao thức (gồm S75 Pro / Nova75 dùng chung nền tảng F75 Max, AULA F75, GravaStar K1, MAD60 HE, HERO68 HE, WIN60 HE/SE…).
- [`docs/PROTOCOLS-AULA-HUB.md`](docs/PROTOCOLS-AULA-HUB.md) — Toàn bộ giao thức trong app AULA Hub chính hãng: 7 họ chip (SparkLink V1/V2, HFD, RLW, Bytech HE, Bytech cơ…) kèm bảng lệnh và bằng chứng mã nguồn.
- [`docs/devices.json`](docs/devices.json) — Bảng 114 mẫu bàn phím (VID:PID, họ chip, khả năng) dạng máy đọc, tổng hợp từ các hồ sơ trên.
- [`docs/devices-registry.json`](docs/devices-registry.json) — Bảng thiết bị cộng đồng mà app tải về khi người dùng bấm kiểm tra cập nhật (định dạng ở mục "Bảng thiết bị cộng đồng" phía trên); sinh từ cơ sở dữ liệu mẫu hiện tại của app.
- [`docs/PROTOCOLS-RONGYUAN.md`](docs/PROTOCOLS-RONGYUAN.md) — Giao thức nền tảng **RongYuan RY5088** trích từ Akko Cloud Driver v4 + MonsGeek V4 Driver: hai hãng dùng chung một nền tảng OEM (cùng pháp nhân ký EV, cùng giao thức) — bảng ~25 opcode, lệnh Hall-effect gộp (actuation/RT/DKS/SOCD), giao thức màn hình, hiệu chuẩn.
- [`docs/devices-rongyuan.json`](docs/devices-rongyuan.json) — Bảng 172 thiết bị Akko + MonsGeek (gồm 52 mục MonsGeek có sẵn firmware-id).
- [`docs/PROTOCOLS-YUNZII-CAKE.md`](docs/PROTOCOLS-YUNZII-CAKE.md) — Giao thức dòng **YUNZII Cake (C75/C98)**: hoá ra chạy chung nền tảng Sinowealth 258A:010C với AULA F75 (họ `kb_by_v2`) — đủ 12 lệnh có dây + giao thức dongle 2.4G, và quan trọng nhất là cơ chế **phân biệt model bằng UUID đọc từ thiết bị (lệnh 0x82)** vì C75/C98 trùng VID:PID.
- [`docs/devices-yunzii.json`](docs/devices-yunzii.json) — Bảng thiết bị dòng YUNZII Cake (5 mục, kèm UUID).
- [`docs/PROTOCOLS-REDRAGON-WITMOD.md`](docs/PROTOCOLS-REDRAGON-WITMOD.md) — **Redragon K617 HE** và nền tảng white-label **WitMod** đứng sau nó (cùng lõi dùng cho nhiều hãng: Fantech, Machenike, Keycool, KZZI, Cidoo…): transport (usage page 0xFF1B), bảng 36 cặp VID:PID của WebHID, cấu trúc KeyInfo.
- [`docs/PROTOCOLS-WITMOD-FULL.md`](docs/PROTOCOLS-WITMOD-FULL.md) — **WitMod mức byte (đợt 2)**: giải mã toàn bộ web app bị làm rối → khôi phục **bảng lệnh đầy đủ 0x01–0x26** (trạng thái, đèn, keymap, macro, họ trigger/hiệu chuẩn 0x21, phím nâng cao DKS/MT/TGL 0x22, SOCD 0x24, deadband 0x26), khung trang big-endian, quy tắc ACK/pacing — không checksum trên đường gửi. Đủ để triển khai lại driver.
- [`docs/NOTES-XINMENG-X98PRO.md`](docs/NOTES-XINMENG-X98PRO.md) — Ghi chú Xinmeng X98 Pro: **chưa trích được** vì hãng không có kênh tải công khai kiểm chứng được (link duy nhất là OneDrive cá nhân của shop) — hồ sơ ghi rõ tình trạng nguồn và đính chính: X98 Pro không có màn hình.
- [`docs/PROTOCOLS-DAREU.md`](docs/PROTOCOLS-DAREU.md) — **DareU All-in-One/A+** (trích từ web driver chính thức dr.dareu.com; CDN desktop chặn tải nên bản cài chưa kết luận): một VID 0x260D cho 190 PID, họ giao thức theo `FwType` — **TG/TG-RT** (EK75/EK75 RT; lớp lệnh Magnetic Axis đầy đủ: rapid trigger 3 giá trị, dead zone, hiệu chuẩn, DKS/SOCD), **OHID**, **JM/QF**. Không mẫu nào trong catalog có màn hình LCD.
- [`docs/devices-dareu.json`](docs/devices-dareu.json) — Bảng 187 thiết bị DareU.
- [`docs/devices-redragon.json`](docs/devices-redragon.json) — Bảng thiết bị WitMod/Redragon trích từ app.
- [`docs/PROTOCOLS-MCHOSE.md`](docs/PROTOCOLS-MCHOSE.md) — **MCHOSE M HUB**: app là vỏ Electron cho web driver WebHID, bên trong có **4 stack giao thức** — BY (cơ, cùng họ `kb_by_v2` với YUNZII Cake), legacy XOR 0xFF, **GLW/CZ cho dòng Ace 68/Jet75** (khối Hall-effect giải mã ở mức byte: actuation/RT/DKS/MT/TGL/hiệu chuẩn), và QHW (V3). Không mẫu nào của MCHOSE có màn hình thật.
- [`docs/devices-mchose.json`](docs/devices-mchose.json) — Bảng 49 bàn phím MCHOSE (31 cấu hình CZ có đủ VID:PID).
- [`docs/PROTOCOLS-EPOMAKER.md`](docs/PROTOCOLS-EPOMAKER.md) — **EPOMAKER Driver hợp nhất v2.1.92**: hoá ra cũng là một bản white-label của nền tảng **RongYuan** (cùng họ Akko/MonsGeek — giao thức lõi đã có trong `PROTOCOLS-RONGYUAN.md`); giá trị mới là bảng thiết bị EPOMAKER và **giao thức màn hình mức byte của RT100** (240×173 RGB565: xoá flash → gói chuẩn bị 165/169 → dữ liệu 37/41, gói 56 B) cùng nhóm lệnh màn hình 32–53. TH80 Pro/Galaxy100 là QMK/VIA (đã công khai) nên bỏ qua.
- [`docs/devices-epomaker.json`](docs/devices-epomaker.json) — Bảng 74 thiết bị EPOMAKER.
- [`preview/README-XEM-TRUOC.md`](preview/README-XEM-TRUOC.md) — Giới thiệu bản xem trước giao diện.

## Nguồn giao thức & lời cảm ơn

T Keyboard Studio đứng trên vai các dự án mã nguồn mở sau — phần giao thức của app được xây dựng và kiểm chứng dựa trên tài liệu/mã nguồn công khai của họ:

- **[mohin7/aula-f75max](https://github.com/mohin7/aula-f75max)** (AULA Studio) — driver F75 Max mã nguồn mở, nguồn kiểm chứng chính cho các khối lệnh đèn / cài đặt / đồng hồ / màn hình.
- **[VitalyArt/Aula-F75-Max-Driver](https://github.com/VitalyArt/Aula-F75-Max-Driver)** — driver F75 Max độc lập thứ hai, dùng để đối chiếu byte-for-byte.
- **[Gavin Graham / Revisory676 — aula-l99-linux](https://github.com/Revisory676/aula-l99-linux)** — dự án reverse-engineering AULA L99 (cùng nền tảng phần cứng SONiX `0C45:800A`); nguồn tài liệu cho giao thức **per-key (0x23), đọc ngược (0xF5), streaming thời gian thực (0x20) và kênh nhạc (0x78)**.
- **[keylux](https://github.com/SibteProf/keylux)** — tham chiếu UX và giao thức cho AULA F75 bản thường (nền tảng Sinowealth).

Font chữ Be Vietnam Pro (SIL Open Font License 1.1) được dùng trong giao diện.

## Giấy phép

MIT — xem file [`LICENSE`](LICENSE).

---

*Dự án cá nhân, không liên kết với AULA, Leobog, Keychron, Wooting hay bất kỳ hãng bàn phím nào. Tên các hãng/mẫu phím thuộc về chủ sở hữu tương ứng.*

## Mã băm các bản phát hành chính thức (SHA-256)

- v3.8.0 Light: `41d9a4a15daf00c150ef259d5803887f3a43931cc4e27ea80ad4e0aa0341a7d3`
- v3.8.0 Compressed: `02ad4c647041c45f28e5c57a7d8d913e2dc7c857ef5c8330721759229ce85556`
- v3.7.0 Light: `458f454b3b5de922c3beaa294c4b52fd117872932e9e72f2cf031399b0b7f5ef`
- v3.7.0 Compressed: `bacfea28a5c254a5808a2ce9fff19f701d6f50ab958d23fbda96ca3458719359`
