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
