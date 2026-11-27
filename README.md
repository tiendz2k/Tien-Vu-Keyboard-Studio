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
5. **Giao thức không công bố** — tài liệu giao thức chi tiết là tài sản riêng của dự án và không được công khai; repo chỉ công bố mã nguồn của app để kiểm chứng tính an toàn.

## Tài liệu

Tài liệu giao thức chi tiết không được công khai trong repo này. Thư mục `docs/` chỉ chứa các bảng thiết bị dạng máy đọc phục vụ app và cộng đồng người dùng:

- [`docs/devices.json`](docs/devices.json) — Bảng 114 mẫu bàn phím (VID:PID, họ chip, khả năng) dạng máy đọc, tổng hợp từ các hồ sơ trên.
- [`docs/devices-registry.json`](docs/devices-registry.json) — Bảng thiết bị cộng đồng mà app tải về khi người dùng bấm kiểm tra cập nhật (định dạng ở mục "Bảng thiết bị cộng đồng" phía trên); sinh từ cơ sở dữ liệu mẫu hiện tại của app.
- [`docs/devices-rongyuan.json`](docs/devices-rongyuan.json) — Bảng 172 thiết bị Akko + MonsGeek (gồm 52 mục MonsGeek có sẵn firmware-id).
- [`docs/devices-yunzii.json`](docs/devices-yunzii.json) — Bảng thiết bị dòng YUNZII Cake (5 mục, kèm UUID).
- [`docs/devices-dareu.json`](docs/devices-dareu.json) — Bảng 187 thiết bị DareU.
- [`docs/devices-redragon.json`](docs/devices-redragon.json) — Bảng thiết bị WitMod/Redragon trích từ app.
- [`docs/devices-mchose.json`](docs/devices-mchose.json) — Bảng 49 bàn phím MCHOSE (31 cấu hình CZ có đủ VID:PID).
- [`docs/devices-epomaker.json`](docs/devices-epomaker.json) — Bảng 74 thiết bị EPOMAKER.
- [`preview/README-XEM-TRUOC.md`](preview/README-XEM-TRUOC.md) — Giới thiệu bản xem trước giao diện.

Font chữ Be Vietnam Pro (SIL Open Font License 1.1) được dùng trong giao diện.

## Giấy phép

**Công khai mã nguồn để kiểm chứng — không phải mã nguồn mở tự do.**
© 2026 Vũ Minh Tiến (tiendz2k) — giữ mọi quyền.

Mã nguồn được công khai để bất kỳ ai cũng có thể đọc và tự kiểm chứng phần mềm không chứa mã độc. Bạn được tải và dùng bản phát hành chính thức cho mục đích cá nhân, phi thương mại, và tuỳ biến thoải mái bằng các tính năng có sẵn trong app. Bạn **không** được sao chép/phân phối lại, phát hành bản sửa đổi dưới tên khác, dùng vào mục đích thương mại, hay xoá tên/chữ ký tác giả — trừ khi được tác giả đồng ý bằng văn bản.

Chi tiết: xem file [`LICENSE`](LICENSE). Các thư viện, font và nghiên cứu giao thức của bên thứ ba giữ nguyên giấy phép riêng — xem file [`NOTICE`](NOTICE).

---

*Dự án cá nhân, không liên kết với AULA, Leobog, Keychron, Wooting hay bất kỳ hãng bàn phím nào. Tên các hãng/mẫu phím thuộc về chủ sở hữu tương ứng.*

## Mã băm các bản phát hành chính thức (SHA-256)

- v3.9.2 Light: `9186a2684e839460e61ee0966d352dfb861840e1eeae64aaaf4ddb6d44ba1778`
- v3.9.2 Compressed: `384951ce944f384adf359caa00ac9a8614c13eade8e1d9da2f99cc41bc037330`

- v3.9.1 Light: `bffb25e9caf22c7b315b745cf78a9ed7e1a5bca0ff167985a43837feb5a8f20f`
- v3.9.1 Compressed: `232c9c7c3df4c14fdb33e3a1a4082559d96194afbdd8f80a4aa3e570a0c24337`

- v3.9.0 Light: `66975835a251a274de06b9867e33148abfae2e9a1d7d0d3df59d69742560d36d`
- v3.9.0 Compressed: `9e708c4ed22343a70c6cc962513deba167797f5fa84b883b7041c1869b6fb697`

- v3.8.0 Light: `41d9a4a15daf00c150ef259d5803887f3a43931cc4e27ea80ad4e0aa0341a7d3`
- v3.8.0 Compressed: `02ad4c647041c45f28e5c57a7d8d913e2dc7c857ef5c8330721759229ce85556`
- v3.7.0 Light: `458f454b3b5de922c3beaa294c4b52fd117872932e9e72f2cf031399b0b7f5ef`
- v3.7.0 Compressed: `bacfea28a5c254a5808a2ce9fff19f701d6f50ab958d23fbda96ca3458719359`
