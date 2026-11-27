# Hồ sơ Xinmeng X98 Pro — KẾT QUẢ: DỪNG Ở BƯỚC NGUỒN FILE (không tải, không mổ)

> Ngày: 27/11/2026. Nhiệm vụ yêu cầu: chỉ tải driver `x98_pro.zip` nếu quy được về kênh **chính hãng hoặc được uỷ quyền rõ ràng**; nếu không thì dừng và báo cáo trung thực. Kết quả: **không tìm thấy kênh như vậy → không tải file, không phân tích**. Hồ sơ này ghi lại toàn bộ quá trình truy nguồn để parent quyết định bước tiếp theo.

## 1. Hãng và sản phẩm — xác minh được

- Xinmeng (新盟) là thương hiệu của **深圳市福彩鑫电子有限公司** (Thâm Quyến), thành lập 2003, chủ lực chuột/bàn phím/phụ kiện. [Bằng chứng: dữ liệu thương hiệu trên smzdm.com, khớp nhau ở nhiều trang sản phẩm]
- X98 Pro / X98 Pro V2: 99 phím (98%), ba chế độ (Bluetooth / 2.4G / USB-C), hot-swap, RGB + đèn viền RGB, pin 4.600 mAh (V2: 5.000–10.000 mAh). Manual chính hãng (bản mirror tại manuals.plus) xác nhận có "custom driver" cho Windows + 2.4G: chỉnh đèn, macro, remap.
- **Đính chính một giả định trong đề bài:** X98 Pro **không có màn hình** — manual và thông số hãng không hề có display; phần "màn hình" thuộc về các mẫu Xinmeng khác (vd M75 bản có màn hình). Nếu sau này lấy được driver, trọng tâm sẽ là đèn/keymap/macro, không có giao thức upload màn hình cho riêng mẫu này.

## 2. Các kênh đã kiểm tra

| Kênh | Kết quả |
|---|---|
| Trang tải chính hãng của nhà sản xuất | **Không tồn tại/không tìm thấy.** Không có download center nào của 深圳市福彩鑫电子有限公司 xuất hiện qua tìm kiếm web (tiếng Trung + tiếng Anh + tiếng Việt). |
| Manual chính hãng (mirror manuals.plus, 3 bản: X98 Pro, X98 Pro V2, X98PRO) | Chỉ ghi: driver *"can be downloaded from the manufacturer's official website or provided by the seller"* — **không in URL, không QR trỏ tới trang tải cụ thể** trong nội dung trích xuất được. |
| `thexinmeng.com` ("XINMENG Website") | Trang storefront tiếng Anh dạng SEO/bán lẻ, không có mục tải driver cho X98 Pro, không có dấu hiệu thuộc nhà sản xuất Thâm Quyến. **Không đủ căn cứ quy thuộc chính hãng.** |
| `xinmengs.com` ("XINMENG Official Website") | Tự xưng official nhưng ghi địa chỉ Chicago, IL và số điện thoại dạng 555 (số giả theo quy ước Mỹ) — đặc trưng của site dựng SEO. **Không đáng tin là kênh của nhà sản xuất.** |
| Shop VN — MKShop (bài hướng dẫn X98 Pro, mkshop.vn) | Link tải `x98_pro.zip` trỏ tới **`1drv.ms` — OneDrive cá nhân của shop**. Đây là bản shop tự upload lại, đúng mô tả "phân phối chủ yếu qua shop VN" trong đề bài. |
| Shop/reseller nước ngoài (BD/Thái…) | Chỉ bán hàng, không phát hành driver. |

## 3. Vì sao dừng (theo đúng điều kiện của nhiệm vụ)

- File duy nhất truy được (`x98_pro.zip` trên OneDrive của MKShop) **không quy thuộc được** về hãng hay kênh được uỷ quyền: không hash công bố, không chữ ký kiểm chứng chéo, không trang chính hãng nào liên kết tới nó. Tải về từ đây thuộc đúng nhóm "random re-upload" mà nhiệm vụ cấm.
- Không có file → không thể kiểm toán an toàn (hash/chữ ký/endpoint) và không thể mổ giao thức (VID:PID, framing, lệnh đèn…). Mọi con số nếu ghi ra lúc này đều là bịa — hồ sơ này không làm vậy.
- Không tạo `devices.json` (không có dữ liệu nguồn đạt chuẩn bằng chứng).

## 4. Gợi ý hướng đi tiếp (để parent/người dùng quyết)

1. **Xin file qua kênh có kiểm chứng:** người dùng (hoặc shop VN quen) cung cấp file kèm hash; hoặc hỏi trực tiếp bộ phận hỗ trợ của Xinmeng trên Tmall/JD flagship (kênh bán chính hãng tại TQ) xin link tải driver X98 Pro — nếu họ trả link thuộc domain/CDN của hãng thì đủ điều kiện tải.
2. **Đường vòng qua dòng HE đời mới của hãng:** manual của Xinmeng Beat68 / Beat68 HE Ultra / Beat75 ghi rõ web driver chính thức tại `https://qmk.top` — đó là kênh chính hãng xác minh được, nhưng là **nền tảng khác** (dòng Hall-effect), không thay thế cho driver X98 Pro.
3. Nếu chấp nhận nới điều kiện nguồn (vd cho phép bản shop kèm kiểm chứng hash từ ≥2 shop độc lập trùng nhau), cần parent xác nhận rõ trước khi tải.

## 5. Tệp trong thư mục này

- Chỉ có hồ sơ này. Không có file driver nào được tải về.
