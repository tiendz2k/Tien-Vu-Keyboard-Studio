# Hồ sơ mổ xẻ Redragon K617 HE / nền tảng WitMod — PHẦN 2: GIAO THỨC MỨC BYTE

> Tiếp nối `DOSSIER.md` (Phần 1: kiểm toán + bản đồ nền tảng; ở Phần 1 bảng opcode bị đánh dấu [GAP]). Phần 2 này **lấp được khoảng trống đó** bằng cách deobfuscate toàn bộ app web chính hãng (illumipc.com).
> Phương pháp: giải mã XOR lớp ngoài (magic `WMJ1`) → giải javascript-obfuscator (mảng chuỗi base64; trạng thái xoay đúng của mảng được brute-force bằng 6 cặp chỉ số↔chuỗi biết trước từ ngữ cảnh code — lệch +140 vị trí so với trạng thái mặc định) → thay 35.896 lời gọi bộ giải mã bằng chuỗi rõ. Bằng chứng chính: **`web/app.deob.js`** (bản đã giải mã hoàn toàn, nằm cùng thư mục `web/` với bản gốc `app.decoded.js`).
> Nhãn: **[CODE]** = đọc trực tiếp từ `app.deob.js`; **[SUY LUẬN]** = suy luận có căn cứ; **[GAP]** = vẫn chưa có.
> Phạm vi: đây là giao thức của **web driver**. App desktop (Qt) dùng chung lõi nền tảng nhưng code native của nó chưa được disassembly — xem mục 10.

## 1. Transport & khung gói tin — [CODE]

- WebHID: `device.sendReport(0x1, data)` — **report ID = 1**; nhận qua sự kiện `inputreport` với `reportId === 1` mới được xử lý. (Lớp transport = class `adI` trong app.deob.js: `send()` gọi `sendReport(0x1, aGM)`; `_onInputReport` lọc reportId 1 rồi bọc DataView thành Uint8Array.)
- **Data của mỗi report dài đúng 63 byte** — mọi gói lệnh đều được dựng bằng `new Uint8Array(0x3f)` (55 vị trí trong code). Cộng report ID → report HID đủ là 64 byte. [CODE]
- Bố cục khung (data 63 B):

  | Offset | Ý nghĩa |
  |---|---|
  | 0 | **Opcode** (byte lệnh chính) |
  | 1 | Subcommand / cờ (thường: 0 = ghi, 1 = đọc; hoặc mã lớp/trang đặc thù từng lệnh) |
  | 2–3 | **Page index, big-endian** (chỉ dùng ở các lệnh truyền theo trang) |
  | 4 | **Độ dài payload** (số byte hợp lệ bắt đầu từ offset 5) |
  | 5…62 | Payload (tối đa 58 byte) |

- **Checksum: KHÔNG CÓ.** Toàn bộ 55 vị trí dựng gói + lớp transport + lớp phiên đều không tính tổng/CRC ở bất kỳ byte nào; gói được gửi nguyên trạng. [CODE — khẳng định dựa trên việc đã đọc toàn bộ đường gửi]
- Usage page 0xFF1B / usage 0x91 và bảng filter 36 cặp VID:PID: giữ nguyên kết quả Phần 1. **VID:PID riêng của K617 HE vẫn [GAP]** — code web không chứa nó (app generic, nhận diện bằng chuỗi product-info, xem mục 3); mã `7153` trong productName HID không phải PID (Phần 1 đã chứng minh).

## 2. Lớp phiên: ACK, timeout, nhịp gửi (pacing) — [CODE]

Lớp `adK` (scheduler) + lớp cơ sở `aeb` của các controller:

- **Ghép cặp đáp hồi:** đáp hồi hợp lệ phải có **byte 0 bằng opcode của gói yêu cầu** (matcher mặc định), kèm matcher riêng từng lệnh (thường kiểm thêm byte 1 / trường page).
- **Timeout mặc định 300 ms** (`defaultTimeout = 0x12c`); lệnh đọc mặc định **thử lại 2 lần**; `retryBackoff = 300 ms`; sau timeout còn có `recoveryDelay = 20 ms` trước tác vụ kế tiếp.
- **Pacing:** `writeDelay = 5 ms` — nghỉ 5 ms sau mỗi tác vụ có chờ đáp hồi trước khi chạy tác vụ tiếp theo trong hàng đợi. Các gói trong cùng một tác vụ (truyền nhiều trang) được gửi liên tiếp không nghỉ thêm, trừ khi dùng ACK từng gói (dưới đây).
- **ACK cho lệnh ghi (`writePacketWithAck`):** đáp hồi ACK = gói **echo lại opcode (byte 0) + subcommand (byte 1)**. Chế độ "strict": chờ ACK tối đa **100 ms** cho gói đầu tiên của mỗi lệnh; nếu không có ACK thì ghi nhận "thiết bị không đáp ACK" và các gói sau của lệnh đó chuyển sang **bắn-và-quên** (không chờ nữa). Chế độ thường: ghi là bắn-và-quên ngay từ đầu.
- **Thu thập nhiều trang (`collectPages`):** với các lệnh đọc dữ liệu dài, app gom mọi gói đáp có opcode khớp cho tới khi gặp gói "trang cuối" theo matcher của lệnh, hoặc hết **idle timeout 300 ms** không có gói mới.
- **Chờ ghi xong (storing):** sau các lần ghi keymap, app poll lệnh 0x01 cho tới khi bit "đang lưu" trong gói trạng thái tắt (settle 250 ms, poll mỗi 60 ms, timeout 2.000 ms).
- **Khử gói trễ (stale):** gói đáp đến sau khi tác vụ đã timeout trong cửa sổ 600 ms bị bỏ qua/đẩy xuống dispatcher sự kiện.

## 3. "Handshake" — bản web của bước ShakeHands — [CODE]

Web driver **không có gói bắt tay riêng**. Trình tự nhận diện khi kết nối (tương đương vai trò của `ShakeHands` + đọc productName bên desktop):

1. Mở thiết bị WebHID; kiểm tra准入 (admission) theo danh sách VID:PID cho phép / hàm identify được cấu hình.
2. **Lệnh 0x0D — đọc thông tin sản phẩm** (`_readProductInfo` / `initProduct`, thử tối đa 3 lần): đáp hồi chứa **chuỗi ASCII phân tách bằng dấu phẩy** tại offset 5, độ dài ở byte 4; **trường thứ 5 (index 4) là định danh product** dùng để đối chiếu layout/KeyInfo. `initProduct` kiểm tra byte 3 của đáp hồi = mã trạng thái (0 = OK).
3. **Lệnh 0x15 — đọc số serial**: độ dài ở byte 4 (mặc định 0x20), ASCII tại offset 5.
4. **Lệnh 0x01 — đọc trạng thái + bảng khả năng** (mục 4.1) để biết thiết bị hỗ trợ module nào (HE, SOCD, deadband, custom light…).
5. Sau đó mới nạp layout (KeyInfo theo product), keymap, đèn… Heartbeat định kỳ = gửi lại lệnh 0x01 (không chờ đáp hồi; đáp hồi đến được dispatcher xử lý như sự kiện `deviceStatus`).

## 4. Bảng lệnh đầy đủ (yêu cầu → đáp hồi) — [CODE]

Quy ước: "sub" = byte 1; "page" = byte 2–3 BE; "len" = byte 4; payload bắt đầu ở byte 5. Độ dài gói luôn 63 B (phần thừa = 0).

### 4.1 Nhóm trạng thái / hệ thống

| Opcode | Tên trong code | Cấu trúc yêu cầu | Đáp hồi / ghi chú |
|---|---|---|---|
| 0x01 | getStatus / heartbeat / initSupport | chỉ opcode | Gói trạng thái: byte1 = mode; byte8 = batteryStatus; byte9 = batteryPercent; byte10 = triMode; **khối support tại byte 5…0x16 (18 B)**: [5]=lockKeyEnable; [14]=isCalibrated; [16]=isMusicRhythmOn; các cờ tính năng đóng gói bit tại byte 17 (0x11, tương đối support+0x0C): bit0 calibration, bit1 customLight, bit2 moreSwitch, bit3 pollRate, bit4 deadBand, bit5 musicRhythm, bit6 reportKeySettings, bit7 all/6key; byte 18 (support+0x0D): bit0 highPrecision, bit1 lightPositionSwitch, bit2 gamePad, bit3 RS, bit4 RKRT, bit5 DKSMouse, bit6 MDS; byte 20 (support+0x0F): bit0 lockWin, bit1 lockShiftTab, bit2 lockAltF4, bit3 lockAltTab; byte 21 (support+0x10): bit0 superPerformance supported… (isSupportSuperPerformance đọc tại support+0x0A = byte 15, isSuperPerformanceMode tại support+0x0B = byte 16); byte 22 (support+0x11): bit0 6-key mode, **bit1 isStoringData** |
| 0x0B | enterBootloader | chỉ opcode, không chờ đáp | Thiết bị nhảy bootloader (nạp firmware) |
| 0x0D | identifyProduct | chỉ opcode | Chuỗi CSV (mục 3) |
| 0x15 | identifySerialNumber | chỉ opcode | ASCII serial |
| 0x17 | setMusicRhythmState | len=2, payload=[v,v] (v=1 bật/0 tắt) | bắn-và-quên |
| 0x20 | — (thiết bị → host) | — | Sự kiện không đồng bộ `appFunctionTrigger`: functionType = [5]<<8\|[6], keyState tại byte 9 (1 = nhấn) |

### 4.2 Đèn (lighting)

| Opcode | Tên | Cấu trúc | Ghi chú |
|---|---|---|---|
| 0x0A | getLightingList (đèn chính) | sub=0 | Đáp: payload = danh sách mode + 2 byte cuối: maxSpeed tại [len−2], maxBrightness tại [len−1] (tương đối payload); dispatcher sự kiện `lightModes` |
| 0x02 | getLightingList (đèn viền/side) | sub=0 | Như 0x0A nhưng cho side light (sự kiện `sideLightModes`) |
| 0x07 | get/set **main** light config | đọc: sub=1; ghi: sub=0, len=0x0E | Struct config 14 B tại payload: mode, brightness, speed, FG R,G,B, BG R,G,B, direction, fullColor, power, timer |
| 0x08 | get/set **side** light config | như 0x07 | Struct 14 B giống hệt |
| 0x09 | custom light (đèn từng phím theo layer) | đọc layer hiện tại: sub=0x20 → đáp byte1 = layer; đọc dữ liệu: sub=0x80 (layer 0) / 0x81 (layer 1), page BE; ghi: sub=layer (0/1), page BE | Dữ liệu layer = **132 phím × RGB = 396 B (0x18C)**, chia **8 trang × 54 B (0x36)**, mỗi trang chứa 18 phím × 3 B, index phím = 18×page + i. Ghi có ACK echo (0x09 + sub). Đọc dùng collectPages, matcher trang cuối = 7 |

### 4.3 Keymap & Fn

| Opcode | Tên | Cấu trúc | Ghi chú |
|---|---|---|---|
| 0x18 | get/set key mapping | đọc: sub=0x80 (lớp default) / 0x82 (lớp Fn); ghi: sub=0x00 (default) / 0x02 (Fn) [cặp sub ghi suy ra từ `_sendKeyData` + điều kiện đặc biệt trong `_onPacket` cho 0x18 với sub 0/2], page BE | Mỗi bản ghi phím **4 B: {code1, hidCode, code3, code4}**. Đọc: trang chứa 14 phím (56 B), index = 14×page + i, matcher trang cuối = 9 → 10 trang = 140 slot. Ghi: cắt **56 B (0x38)/gói**, ACK từng gói (đáp echo 0x18 với sub 0 hoặc sub ghi), sau đó chờ bit isStoringData tắt (mục 2) |
| 0x0E | getFnKey (Fn có sửa được không) | chỉ opcode | Đáp: len tại byte 4; bitmap tại byte 5…: phím index = 22×bit + byteOffset là Fn-editable |

### 4.4 Cài đặt thiết bị — opcode 0x14 (đa lệnh con)

| Sub/trường | Tên | Cấu trúc |
|---|---|---|
| sub=2 | getSleepTimer | Đáp: sleepSeconds = BE tại byte 8–9; standbySeconds = BE tại byte 10–11 |
| sub=1 | setSleepTimer | byte 8–9 = sleep BE; byte 10–11 = standby BE; không chờ đáp |
| (ghi settings) | setDeviceSettings / winLock / superPerformance | byte4=1; các cờ tại: **[12]=lockWin, [13]=lockShiftTab, [14]=lockAltF4, [15]=lockAltTab, [17]=superPerformance, [18]=6-key mode** (offset tuyệt đối trong gói) |
| (reset) | resetKeyboard | byte4=1, byte5=1 |
| sub=2 | factoryReset | chỉ opcode+sub=2 — **lưu ý: trùng sub với getSleepTimer trong chính code web** (phân biệt bằng ngữ cảnh/lệnh gọi; ghi lại đúng như code, không suy đoán thêm) |
| sub=3 | setGamePadMode | byte4=1, byte5=mode |
| — | Đáp hồi ACK của nhóm 0x14 | dispatcher sự kiện `deviceAck`: resultCode tại byte 5, lightPosition tại byte 6 |

### 4.5 Macro — opcode 0x19

| Hướng | Cấu trúc |
|---|---|
| Đọc 1 slot | sub = 0x80 + slot. Đáp (nhiều trang, page BE): header payload — byte5: bit7 = isCopy, 7 bit thấp = index; byte6 = type; byte8 = số byte dữ liệu bước (≤ 0x3A); byte 9–10 = tổng số bước (mcCount) BE; **bước tại offset 13, mỗi bước 4 B: {keyCode, (type thấp 4 bit \| state cao 4 bit), delay BE 16-bit}** |
| Ghi 1 slot | Ảnh slot **256 B (0x100)**, cắt **58 B (0x3A)/gói** → 5 gói; sub = slot, page BE, len từng gói. Trong ảnh slot: bản ghi bước 4 B tại offset 8 + 4×i: {hidCode, ?, delay BE} |
| Xoá slot | sub=3, byte4 = slot |
| ACK ghi | **Thiết bị tự phát** gói 0x19 với sub < 0x80 → sự kiện `macroWriteFinish` (host chờ sự kiện này, timeout mặc định 100 ms; một số firmware không hỗ trợ thì app tự bỏ qua sau lần đầu) |

### 4.6 Hall-effect: trigger & hiệu chuẩn — opcode 0x21

Byte 4 = 0x18 ở hầu hết lệnh con trigger (riêng poll-rate đọc đặt byte4=1); **byte 5 = lệnh con**:

| Lệnh con (byte5) | Tên | Cấu trúc / đáp hồi |
|---|---|---|
| 0x00 | setTrigger (ghi cấu hình RT) | Payload (từ byte 5) là struct: byte cấu trúc [col+1] \|= 1<<row cho từng phím được chọn (bitmap 22 cột × hàng); travel (uint16) ghi lặp tại offset cấu trúc 0x17–0x18 (low) và 0x1B–0x1C (high)… chính xác theo code: struct[0x17]=struct[0x18]=travel&0xFF; struct[0x19]=interval1&0xFF; struct[0x1A]=interval2&0xFF; struct[0x1B]=struct[0x1C]=travel>>8; struct[0x1D]=interval1>>8; struct[0x1E]=interval2>>8. Đơn vị travel/interval theo `triggerUnitRaw` của thiết bị (0 → 10) |
| 0x01 | (thiết bị → host) triggerData | Khi đang test: row tại byte 6, col tại byte 7, travel = [9]<<8 \| [8] |
| 0x02 | openTriggerTest | Payload từ byte 6: **bitmap 22 B** — byte[col] \|= 1<<row cho mỗi phím cần test |
| 0x03 | closeTriggerTest | — |
| 0x04 | getTriggerConfig | Đáp: maxTriggerTravel = [9]<<8 \| [6] (đúng như code), triggerUnitRaw tại byte 7 (0 → 10), minTriggerTravelRaw tại byte 10 |
| 0x05 | readTriggerData (từng phím) | Yêu cầu: byte6 = nhóm hàng = index div 22, byte7 = cột = index mod 22. Đáp: mode tại byte 6; travel = [0x0B]<<8 \| [7]; interval1 = [0x0D]<<8 \| [9]; interval2 = [0x0E]<<8 \| [0x0A]; row/col tại byte 0x0F/0x10 nếu gói ≥ 17 B |
| 0x06 | resetTrigger | — |
| 0x07 | reviseKey (hiệu chuẩn 1 phím) | byte6 = row, byte7 = col. Đáp sự kiện calibration: trạng thái tại byte 6 (0=success, 1=progress kèm bitmap phím đã chuẩn tại byte 7…0x1C, 2/4=failed) |
| 0x08 | reviseKeys all-key | byte6: 0 = bắt đầu, 1 = dừng; đáp như 0x07 |
| 0x0F / 0x10 | startAnyKey / stopAnyKey | Hiệu chuẩn "phím bất kỳ"; byte6 = 0 |
| 0x09 | poll rate | Đọc: byte4=1, byte5=9 → đáp rate tại byte 6. Ghi: byte5=9, byte6=1, byte7=rate. **rate ∈ {1,2,4,8} ↔ {1000, 2000, 4000, 8000} Hz** |
| 0x11 | getPresetIndex | Đáp: index tại byte 6 (sự kiện `presetIndex`; cũng có thể đến không đồng bộ) |
| 0x12 | setPresetIndex | Yêu cầu: index tại byte 6. Đáp: byte6 = 1 là thành công (`presetSetResult`) |

### 4.7 ADC stream — opcode 0x23

- Mở stream: byte5=1; đóng: byte5=0 (`openADC`/`closeADC`).
- Dữ liệu thiết bị tự phát: gói ≥ 0x31 B; từ offset 5 là **22 giá trị uint16 BE** = 11 phím × (mainValue, auxValue) — dùng cho màn hình test/hiệu chuẩn trực tiếp. [CODE]

### 4.8 SOCD (PRCS) — opcode 0x24

| Sub | Tên | Cấu trúc |
|---|---|---|
| 0 | ghi danh sách | 2 trang × 10 mục; page BE; len=0x28 (40 B); mỗi mục 4 B: {state, model, keyCode1, keyCode2}; tối đa 20 slot (`maxSocdSlots = 0x14`); ACK echo |
| 1 | đọc danh sách | collectPages; mỗi trang 10 mục 4 B như trên (mục rỗng: state=0 hoặc mẫu {7, 0xFF, 0xFF, 0xFF}) |
| 2 | đọc power | Đáp payload: power tại [0], keyCode1 tại [4], keyCode2 tại [5] |
| 3 | set power | len=1, payload=[0/1] |

### 4.9 Advanced keys (DKS/MT/TGL/RS/RKRT/MDS) — opcode 0x22

- Đọc toàn bộ: sub=0x80, collectPages tới page=0x27 (39) → tối đa 40 slot (`maxSlots = 0x28`). Mỗi mục: **type tại byte 5**: 1=DKS, 2=MT, 3=TGL, 4=RS, 5=RKRT, 6=MDS. Bố cục theo type (từ dispatcher): type 1 (DKS): routes tại byte 6–9, sau đó 4 key × 8 B (key = {code1, hidCode, code3, code4} tại +0…+3, steps tại +4…+7, bước sóng 8 B/phím); type 2 (MT): key1 tại byte 6–9, key2 tại 10–13, duration tại byte 14; type 3 (TGL): 1 key tại byte 6–9; type 4/5 (RS/RKRT): key1 tại 6–9, key2 tại 12–15; type 6 (MDS): duration BE tại byte 6–7, mode tại byte 8, số key tại byte 9, danh sách hidCode từ byte 10 (tối đa 6).
- Ghi: sub = số slot (0…0x27), len=0x32 (50 B), payload: type code tại byte 5 rồi các trường như trên. [CODE]

### 4.10 Switch từng phím (axis) — opcode 0x25

| Sub | Tên | Cấu trúc |
|---|---|---|
| 0 | getAxisList | Đáp payload: số loại switch = BE tại [0..1], danh sách id từ [2] |
| 1 | setKeyAxis | Ghi theo trang, chunk 58 B, ACK echo (0x25 + sub 1) |
| 2 | initKeyAxis (đọc) | collectPages tới page=2; index phím = 58×page + i, 1 B/phím (mã switch) |

### 4.11 Deadband — opcode 0x26

- Đọc: sub=0, collectPages tới page=4; mỗi trang **29 mục × 2 B {deadbandTop, deadbandBottom}**, index = 29×page + i.
- Ghi/reset: ảnh **264 B (0x108) = 132 phím × 2 B**, sub=1, cắt 58 B/gói, ACK echo (0x26 + sub 1). Reset = ghi toàn 0.

## 5. "JoinFrame" phía web = truyền theo trang (fragmentation) — [CODE]

Bản web không có lớp ghép khung riêng như tên `hidDeviceJoinFrame` bên desktop; việc phân mảnh được chuẩn hoá ngay trong từng lệnh bằng trường page (byte 2–3 BE) + len (byte 4):

| Opcode | Kích thước chunk payload | Đơn vị dữ liệu |
|---|---|---|
| 0x09 custom light | 54 B (0x36) | 18 phím × RGB |
| 0x18 keymap | 56 B (0x38) | 14 phím × 4 B |
| 0x19 macro ghi | 58 B (0x3A) | ảnh slot 256 B → 5 gói |
| 0x25 setKeyAxis | 58 B (0x3A) | 1 B/phím |
| 0x26 deadband | 58 B (0x3A) | 2 B/phím |
| 0x24 SOCD | 40 B (0x28) | 10 mục × 4 B |

Phía đọc: cùng trường page; host gom trang bằng `collectPages` (mục 2). Bên nhận (dispatcher `adJ`) xử lý 15 nhóm opcode: 0x01, 0x02, 0x07, 0x08, 0x09, 0x0A, 0x0E, 0x14, 0x18, 0x19, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26 — toàn bộ đã có bố cục byte trong mục 4.

## 6. Đối chiếu với Phần 1 và họ cũ

- Phần 1 [CODE] vẫn đúng: nền tảng WitMod, usagePage 0xFF1B, nhận diện desktop bằng productName tiền tố `7153`, KeyInfo 4 B/phím {code…} — nay khớp với bản ghi keymap 4 B {code1, hidCode, code3, code4} của opcode 0x18 và trường `Dec/qtDec` là chỉ số phím/LED trong cùng không gian index 132 phím (`maxKeys = 0x84`). [CODE]
- Xác nhận lại kết luận Phần 1: **họ mới hoàn toàn** — không dùng framing `04…` của SONiX (Kala/Griffin), không dùng họ Sinowealth 0x258A. Khung WitMod là `[opcode][sub][page BE][len][payload]` trên report 64 B, không checksum.

## 7. Việc còn mở — [GAP] (ghi thẳng, không đoán)

1. **App desktop (Qt) ở mức byte:** nội dung byte của `DDeviceHidCmdSend` / `hidDeviceJoinFrame` / `ShakeHands` trong exe native **chưa** được disassembly. Web và desktop dùng chung một lõi giao thức theo bằng chứng nền tảng, và bản web đã đủ để triển khai lại toàn bộ giao thức; nhưng khẳng định "desktop giống hệt từng byte" cần Ghidra — chưa làm.
2. **VID:PID thật của K617 HE:** vẫn chưa có (code web không chứa; cần đọc descriptor trên thiết bị thật).
3. **Report descriptor đầy đủ:** suy ra từ code: report ID 1, data 63 B. Chiều dài input report tối thiểu thấy qua ADC là 0x31 B; descriptor gốc chưa đọc được từ thiết bị.
4. **Giao thức nạp firmware** (desktop có `jl_firmware_upgrade_x86.dll` — họ JieLi — và `UpgradeAppTool.exe`): chưa phân tích.
5. Một số trường payload còn mờ: byte thứ 2 trong bản ghi bước macro phía ghi (offset 8+4i+1), ngữ nghĩa `code3/code4` trong bản ghi keymap, và trường `triggerUnitRaw` ánh xạ sang đơn vị vật lý (mm) theo từng firmware.
