# DOSSIER — Ứng dụng tham chiếu (keyboard app 0.1.7): Protocol Extraction

**Target:** `keyboard-studio.exe` (36,056,064 B, PE32+ x86-64), Tauri 2.12.7 + Rust (hidapi 2.6.7) backend, embedded web frontend.
**Method:** static analysis only — the app was never executed and no device was contacted. The frontend was carved from the binary (Tauri asset map in `.rdata`, brotli-compressed values), decompressed, and read as source. Public config files were fetched read-only from the reference app's public config server.
**Date:** 2026-11-27 (overnight run).

**Confidence legend**
- **[JS]** = read directly in the recovered frontend source (`assets/assets/index-CYgHsvd1.js`; line numbers below refer to the beautified copy `assets/assets/index-CYgHsvd1.pretty.js`). Source-level truth, but *not* hardware-verified by us.
- **[SRV]** = from the shop's public server config files (`server-config/`, identical content to the embedded copies where both exist).
- **[STR]** = strings/native evidence only (Rust side not decompiled).
- **[INF]** = inference — labelled as such, never presented as fact.

---

## 1. Headline findings

1. **The app contains TWELVE keyboard models in SIX protocol families** — far more than its store page advertises. Full device table in §3.
2. **All protocol logic lives in the JavaScript frontend.** The Rust backend is a generic HID transport (10 commands, §2). Whoever reads the JS owns the protocols — which is what this dossier does.
3. **AULA S75 Pro runs on the F75 Max's own platform**: same VID:PID `0C45:800A`, same usage page `0xFF13`, same 64-byte feature-report framing, and the same stream opcodes (`0xF5` probe → `0x20` + 8 blocks → `0x02` commit) that were independently recovered for the L99/F75 Max. The S75 Pro protocol is therefore *effectively public now* (source-level), pending only on-hardware verification.
4. **There is NO dedicated AULA F75 Max support in this build.** The string "F75 Max" appears nowhere in the frontend; both matchers that could claim a `0C45:800A` or `258A:010C` device explicitly reject product names containing "MAX". [JS] (See §5.2 note.)
5. **The app is a pure LED streamer.** No key remapping, no macros, no firmware update, no TFT/screen upload code exists in the frontend — for any model. Effects are volatile: every driver backs up the keyboard's current colours in RAM and restores them on stop. Nothing is written persistently. [JS]
6. **2.4G dongles are explicitly unsupported** — the app's own error text: only wired mode; "đầu thu 2.4G chưa được hỗ trợ". [JS]

---

## 2. Architecture

### 2.1 Two transports, one driver layer
The same frontend runs in a browser (WebHID) and in the Tauri shell (native bridge):

- **WebHID path**: `navigator.hid.requestDevice()` with per-family filters; drivers use `device.sendReport / sendFeatureReport / receiveFeatureReport` and `inputreport` events directly.
- **Tauri path**: class `B5` (bridge provider) + class `L5` (device wrapper) expose the *same* WebHID-shaped API on top of Tauri `invoke()` calls [JS ~19790–19960]. The connection manager `U5` (~19620–19800) picks a driver adapter by trying matchers in this order:
  **Nova75 → NewStar → WIN60 HE → WIN60 SE → F75 → S75 Pro → MAD60 (orig) → MAD60 V2 → HERO68 → K1**. [JS]

### 2.2 Native (Rust) command surface — the whole of it
From the Tauri ACL/capability strings and the JS bridge calls [JS/STR]:

| Command | Args (from JS) | Purpose |
|---|---|---|
| `hid_list` | — | enumerate HID devices (returns vendorId/productId/productName/serial + collections with usagePage/usage/report sizes) |
| `hid_open` | `{ device_id }` | open a session |
| `hid_close` | `{ session_id }` | close |
| `hid_write` | `{ session_id, report_id, data[] }` | send output report |
| `hid_write_feature` | `{ session_id, report_id, data[] }` | send feature report |
| `hid_read_feature` | `{ session_id, report_id, size }` | get feature report (ACK reads) |
| `hid_write_nova_frame` | `{ session_id, data[512] }` | **only model-specific native command** — sends one Nova75/S75-class frame natively; returns the write duration in ms (JS validates 0–100) [JS] |
| `hid_stop_stream` | `{ session_id }` | stop a native stream loop |
| `hid_diagnostic_list` | — | device list for the built-in diagnostics page |
| `hid_diagnostic_descriptor` | `{ device_id }` | raw report-descriptor dump (diagnostics page, ~22181) |

Native source files visible in panic paths [STR]: `src\main.rs`, `src\hid.rs`, `src\hid-descriptor.rs`, **`src\hid-profiles.rs`** (native-side device profiles — probably mirrors the JS matcher filters for `hid_list`). Native error names [STR]: `HID_FRAME_DEADLINE`, `HID_BUSY`, `HID_COMMAND_NOT_ALLOWED`, `HID_DESCRIPTOR_UNSUPPORTED`, `HID_INVALID_REPORT`, `HID_STALE_SESSION`, `HID_WRITE_TIMEOUT`, `HID_QUEUE_UNAVAILABLE`, `HID_DEVICE_UNAVAILABLE`.

**What remains locked in native code:** only (a) how `hid_write_nova_frame` chunks/paces the 512-byte block (presumably 8× 64-byte feature reports, possibly with native pacing — [INF]), (b) `hid_stop_stream` internals, (c) `hid-profiles.rs` matching details. **No packet construction happens natively** — every opcode, checksum and frame layout below is built in JS. [JS]

### 2.3 Timing infrastructure
- **hid-pacer** (`assets/keyboard-config/hid-pacer.js`, also fetched from server): a Web Worker giving precise `setTimeout` pacing; request = `{id, delay}` with delay clamped 1–1000 ms, **default 2 ms**. Drivers `await pacer.wait(n)` between packets. Worker name in code: `win60he-usb-pacer`. [JS]
- **playback-clock** (`playback-clock.js`): worker posting a tick every **50 ms** (20 fps playback clock; `setInterval` fallback). Frame choice is elapsed-time based, honouring each GIF frame's own duration × user speed. [JS]

### 2.4 GIF → LED pipeline (shared by all models) [JS]
1. GIF decoded with WebCodecs `ImageDecoder` (fallback: `createImageBitmap` per frame) (~20100–20160).
2. Each frame is drawn onto a 600-px-wide canvas (height from model aspect) with a placement transform (fit/fill-style) and a canvas `filter: brightness() saturate()` (~20281).
3. Sampling (`P5`, ~20289): for every key, take the pixel region around the key's LED centre; mode `point` = centre pixel, mode `median` = per-channel median of the box, then the actual pixel closest to that median colour. Colours with max(R,G,B) ≤ `blackLevel` (0–96) become black; result scaled by `brightness`.
4. Mapping (`zo`, ~17664): per-key colours → slot-ordered RGB buffer of `ledSlots × 3` bytes via the model's `mapping` (user-overridable per model, persisted in localStorage under `hpk:<model>-config:v1`). For WIN60 HE only, unmapped grid slots are filled with the nearest mapped slot's colour within their row (its frame is a full 6×22 grid, not per-key).
5. Rendered frames are cached (1 MB LRU, class `h3`); the loop calls `connection.pushFrame(buffer)`; each driver streams it with its own framing/pacing (§4–§5).
6. A **calibration mode** lights one slot at a time with RGB(160,160,160) (`s5`) so users can fix their mapping. [JS]

Layout geometry for authoring GIFs is published per model as `*-layout-900x*.json` [SRV]: `{canvas:{width,height}, keyCount, keys:[{key, id, rectangle{x,y,width,height}, ledCenter{x,y}}]}` plus a grid PNG/SVG and an `*-ai-prompt.txt` describing the geometry for AI GIF generation.

---

## 3. Device table — every model in the app [JS registry ~17100–17340]

| # | Model id | Display name | VID:PID (wired) | Family (§) | LED slots | Keys in layout | Native lighting read |
|---|---|---|---|---|---|---|---|
| 1 | `win60he` | AULA WIN60 HE | `2E3C:C365` | E | 132 (grid) | 61 | yes |
| 2 | `win60-se` | AULA WIN60 SE | `38A6:291A` | F2 | 61 (+4 space) | 61 | no |
| 3 | `aula-f75` | AULA F75 | `258A:010C` | A | 122 | 80 | no |
| 4 | `aula-s75-pro` | AULA S75 Pro | `0C45:800A` | B | 80 | 80 | no |
| 5 | `aula-nova75` | AULA Nova75 | `0C45:800A` (inherited) | B+ | 80 (inherited) | ~80 (`Oo` table) | no |
| 6 | `mad60-he-v2` | MADLIONS MAD60 HE V2 | `373B:1120` | C | 70 | 61 | no |
| 7 | `mad60-he` | MADLIONS MAD60 HE thường | `373B:105D` | C | 70 | 61 | no |
| 8 | `mad60-he` (1054) | MADLIONS MAD60 HE RGB | `373B:1054` | C | 70 | 61 | no |
| 9 | `hero68-he` | AULA HERO68 HE | `372E:103E` | D | 68 | 68 | no |
| 10 | `gravastar-k1` | GravaStar Mercury K1 | `258A:0294` (also `258A:010C`, §5.8) | A | 126 | 79 | no |
| 11 | `mad60-v2-newstar` | MAD 60 V2 (New Star) | `28E9:32BD` | F1 | 61 (+4 space) | 61 | no |
| 12 | `mad60-v2-newstar` (3256) | MAD 60 V2 (New Star · 3256) | `28E9:3256` | F1 | 61 (+4 space) | 61 | no |

No Bluetooth or 2.4G PIDs appear anywhere in the frontend; all matchers require the wired device. [JS]
Key tables (id, slot, label, x, y, width) live in the same registry region: WIN60 HE `Zr` (~16118) + mapping `Lv` (~16546); F75 `wd` (~16564); S75 Pro `zl` (~16730, from array `Vx`); Nova75 `Oo` (~16899, from `qx`); MAD60 `Sd` (~16910, derived from the WIN60 HE rows); HERO68 `va` (~16998, from `Qx`); K1 `Ed` (~17095, from `Zx`). Full tables are in the recovered JS — they are data, and they are complete.

---

## 4. Protocol families

### Family A — Sinowealth-style, 519-byte feature reports [JS]
Channel: usagePage `0xFF00` (65280), **feature reportId 6**, report body **519 bytes**. Used by F75 (§5.3) and GravaStar K1 (§5.8) — same silicon platform, distinguished by an identity probe.

### Family B — SONiX, 64-byte feature reports [JS]
Channel: VID `0x0C45`, PID `0x800A`, usagePage `0xFF13` (65299), usage 1, **feature reportId 0, 64-byte reports**.
Command header builder `oy(opcode)`: byte[0]=`0x04`, byte[1]=opcode, **byte[8] = 9 if opcode==0xF5, 8 if opcode==0x20, else 0**.
ACK: after `sendFeatureReport(0, cmd)`, read `receiveFeatureReport(0)` (a 65-byte reply with a leading 0 is stripped by helper `Hr`); validator `h5`: reply[1] must equal the opcode; **reply[3]==1 → "ready", ==0xFF → "busy"**.
This is byte-for-byte the framing previously recovered for the AULA L99 / F75 Max (parent project docs) — the S75 Pro and Nova75 are the same platform. Used by S75 Pro (§5.4) and Nova75 (§5.5).

### Family C — MAD60, 32-byte output reports [JS]
Channel: VID `0x373B`, usagePage `0xFF60` (65376), usage `0x61`, **output reportId 0, 32-byte reports**. Request/response: send report, await an `inputreport` whose prefix echoes the request (echo length: 1 byte for cmd `[1]`, 2 bytes for `[8,65]`, else 5 bytes). Pacer wait before every exchange. Used by all three MAD60 variants (§5.6).

### Family D — HERO68 HE, 63-byte reports with checksum [JS]
Channel: VID `0x372E`, PID `0x103E`, usagePage `0xFF60`, usage `0x61`, **reportId 9, 63-byte reports**.
Packet builder `Nn(cmd, sub, payload, total=1, index=0)`: `[0]=cmd, [1]=sub, [2]=0, [3]=total, [4]=index, [5]=payload length`, payload at byte 6, **checksum at byte [62] chosen so `(9 + sum of all 63 bytes) & 0xFF == 0xFF`** (the 9 is the reportId). Replies are validated with the same rule plus header `[2]==0, [3]==1, [4]==0, [5]≤56`. (§5.7)

### Family E — WIN60 HE, 63-byte output reports [JS]
Channel: VID `0x2E3C`, PID `0xC365`, usagePage `0xFF33` (65307), usage `0x91`, **output reportId 1, 63-byte reports** via `sendReport(1, …)`; command byte is byte[0]. (§5.1)

### Family F — 61-key boards with 65 stream slots (61 keys + spacebar ×5)
Two unrelated transports sharing one key geometry (both model objects inherit the WIN60 SE definition, which inherits WIN60 HE's key layout):
- **F1 NewStar** (§5.9): VID `0x28E9`, usagePage `0xFF87`, reportId 6, 63-byte reports, checksummed request/response packets.
- **F2 WIN60 SE** (§5.2): VID `0x38A6`, usagePage `0xFF00`, reportId 0, 64-byte reports with a random per-session byte.

---

## 5. Per-model protocol details

### 5.1 AULA WIN60 HE — Family E [JS, class `iy` ~17660–17900; profile `an`]
- **Read lighting** (cmd **7**): request `[7, 1, 0…]`; reply must have byte[0]==7 and byte[4] ≥ 12; the 12 state bytes start at offset 5. (Byte semantics not decoded in the frontend — the app only displays them opaquely. Gap, §7.)
- **Stream enable/disable** (cmd **23 / 0x17**): packet `Do(bool)`: `[0]=23, [4]=2, [5]=bool, [6]=bool`. Begin-stream sends `Do(true)`, waits 50 ms.
- **Heartbeat** (cmd **1**): sent every 1 s while streaming. If a device input report arrives with byte[0]==1, byte[4] ≥ 17 and **byte[21]==0**, the app sets `needsSync` and resends the enable packet (device-initiated resync).
- **Frame data** (cmd **15 / 0x0F**): frame = **132 grid slots × 3 = 396 bytes** (6 rows × 22 cols), split into **54-byte chunks** (8 packets). Packet header: `[0]=15, [2..3]=chunk index BIG-ENDIAN, [4]=chunk length`, payload at offset 5. Pacer wait (2 ms) after each packet.
- Matcher `Xv`: VID/PID + a collection with usagePage `0xFF33`, usage `0x91`, and an output reportId 1 of 504 bits.

### 5.2 AULA WIN60 SE — Family F2 [JS, class `wy` ~19121–19470; profile `_l`]
**The vendor's own UI marks this model experimental / not hardware-verified** ("thử nghiệm, chưa kiểm chứng trên thiết bị thật"), sharing the WIN60 HE layout and GIF library. [JS]
- Every packet starts with a **random session byte (1–254)** chosen at connect: builder `C5(session, cmd, sub, params)` = `[session, cmd, sub, …params]` in a 64-byte report (reportId 0). Replies must be 64 bytes echoing `[session, cmd, sub]`.
- **Connect**: exchange(1, 1) → info: reply bytes [3..10] must equal `D5 = [2, 4, 1, 4, 64, 0, 0, 0]` and reply[43]==1 (keyboard mode, not bootloader; HE/Pro/Max variants rejected). exchange(5, 1) → LED region table (1–8 regions, 6-byte descriptors at offset 4; region 0 gives rows ∈ 5–8, cols ∈ 14–32). exchange(5, 2, [0, region]) per region → LED bitmap rows converted by `A5`; row popcounts must equal `Nd = [14, 14, 13, 12, 8]` (61 keys; the last row carries +4 because **the spacebar has 5 LEDs**) → a 65-entry stream table.
- **Read lighting**: exchange(6, 2, [0, 0]); validates reply bytes 5,7 ≤ 1 and 8,9 ≤ 100.
- **Streaming**: *no mode-enter, no backup, no restore* — start only checks lighting is on (readLighting bytes [5] and [8] non-zero). Frames via `N5(session, frame, streamTable)`: packets `[session, 6, 6, 0, 1, totalPackets, index+1]` carrying **19 RGB triples at offset 7** (65 entries → 4 packets). Pacer wait **34 ms** after each full frame. Stop = stop sending; the app notes the keyboard's own effect returns after ~3 s.

### 5.3 AULA F75 (regular) — Family A [JS, class `ry` ~18000–18260; profile `Qa`]
- Matcher `Vd`: VID:PID `258A:010C` + usagePage `0xFF00` collection with feature reportId 6 of 519 bytes (4152 bits); productName must contain "F75" (or be a generic "keyboard gaming"/"gaming keyboard"/"usb keyboard") and must **NOT** contain MAX/PRO/HE.
- **Config read** `c5()`: send 519-byte feature report `[132 (0x84), 0, 0, 1, 0, 128, 0, …]`; reply validated by `u5`: same 7-byte header, then a **128-byte config block whose last two bytes are `0x5A, 0xA5`**. (Block semantics not decoded in the frontend — displayed/stored opaquely. Gap, §7.)
- **Stream control** via `modeReport(reportId, bytes)` (519-byte buffers):
  - begin: report **57 (0x39)** ← `[32, 6, 0, 1, 0]` → wait 5 ms → report **60 (0x3C)** ← `[32, 1, 0]` → report **57** ← `[32, 6, 1, 1, 0]`
  - stop/failure: report **60** ← `[32, 0, 0]`
- **Frame packet** `d5()`: 519-byte report: `[0]=8, [3]=1, [5]=122 (ledSlots), [6]=1`, RGB triples (122 slots × 3) at offset 7. Sent as feature report 6, back-to-back in the drain loop (latest-frame-wins, no explicit pacer).

### 5.4 AULA S75 Pro — Family B [JS, class `Qd` ~18262–18660; profile = base param set]
- Matcher `Xd`: VID:PID `0C45:800A` + `0xFF13` feature channel (reportId 0, 64 B); productName must match "s75 pro" (or be generic) and must **NOT** contain max/he/ultra.
- **Connect sequence**: command **0xF5** (probe; header byte[8]=9) must ACK "ready" → read **9 blocks** of 64 B (the current per-key LED state — the backup used for restore; blocks are `[key_id, R, G, B]` quads per the family framing) → command **0x02** must ACK "ready".
- **Frame flow** (`drain`): command **0x20** (header byte[8]=8), polling through all-zero ACKs with delay ladder `sy = [20, 40, 80, 160, 200, 200, 300]` ms → send the **8 frame blocks** (frame builder `f5`: key-count × 3 RGB in → **512 B = 128 quads of [key_id, R, G, B]** placed at `slot × 4`, split into 8 × 64 B), pacer wait(2) before each block → command **0x02 (END)**; on "busy", the S75 profile resends END after 35 ms, up to 2 retries.
- Key table `zl` = **80 keys** `{id, slot, …}`; `pushFrame` validates frame length == 80 × 3.
- **This is the same stream flow as the L99/F75 Max `0x20` path recovered earlier from open sources** — cross-confirmed by an independent (vendor) implementation. [JS]

### 5.5 AULA Nova75 — Family B, accelerated [JS, class `cy` extends `Qd` ~18660+]
- Matcher `Zd`: same VID:PID/channel; productName must be **exactly** `"AULA-NOVA75"`.
- Same opcodes as S75 Pro, with profile differences: frame blocks are sent via the **native** `hid_write_nova_frame` (one 512-byte call) instead of 8 JS feature writes; `pollZeroLiveAck: true`; END busy-retry ladder `liveEndRetryDelays = [20, 40, 80, 160, 200, 200, 300]` ms; `recoverEndBusy: true`; diagnostics enabled; error strings tagged "Nova75v9".
- Key table `Oo` (its own, distinct from `zl`).

### 5.6 MADLIONS MAD60 HE (V2 `373B:1120`, original `373B:105D`, RGB `373B:1054`) — Family C [JS, class `$v`; `gy`=V2, `yy`=original ~18662–18990; profiles `Tn/Tl/Ai`]
- **Connect**: exchange packet `[1]` (`m5`) → reply must have `d[1]==0, d[2]==9`; firmware protocol version = `d[1]<<8 | d[2]` = **9** (error text: "hiện chỉ hỗ trợ giao thức 9" — only protocol 9 supported). Then exchange `dy()` = `[8, 65]` (get lighting info).
- **beginStream**: read info `[8, 65]`; read current colours via 10 header packets `Wv()` = `[8, 69 (0x45), page 0–4, half×7, 7]` — replies carry 21 data bytes at offset 5 → assemble the **210-byte** original colour buffer; then exchange `fy(info, true)`: the info packet with `[0]` changed to **7**, `[2]=1`, `[8]=200` (enter custom/stream mode).
- **Frames** `hy()`: **70 LED slots × 3 = 210 B** (5 pages × 14), sent as 10 packets `[7, 66 (0x42), page, offset, 7]` + 21 data bytes (7 LEDs × 3).
- **Stop**: rewrite the original colours via the same `hy()` path, then exchange `fy(info, false)` to leave custom mode.
- Note vs public OpenMAD documentation [INF]: OpenMAD documents VID `373B` with PID `1053` for the MAD60 HE; the shop app instead uses `105D`/`1054` (original) and `1120` (V2). Different SKUs or firmware generations — the shop's values are recorded here as observed in source.

### 5.7 AULA HERO68 HE — Family D [JS, class `vy` ~18830–19120; profile `Pa`]
- **Connect**: exchange `v5()` = `Nn(130, 1, [0,0,0,0,0,0])` → reply[5]==6 and reply payload at [6..] must equal `y5 = [17, 0, 0, 0, 0, 3]` (identity; error text: wired HERO68 only — Air/Mini/Pro/Ultra unsupported).
- **Read lighting** `b5()` = `Nn(132, 1)` → reply[5]==7, reply[6] ≤ 19 (effect id); returns 7 state bytes [6..12].
- **Read current colours**: `w5()` packets `Nn(134, 0, …)` carrying 11 key-id pairs `[0, id]` per packet; replies carry 5 bytes per key `[0, id, R, G, B]` starting at offset 6.
- **Enter stream**: send `Nn(1, 128, [16])` → pacer wait(20) → `Nn(4, 2, [19, 0])` → `Nn(4, 4, [100, 4])`.
- **Frames** `E5()`: LEDs grouped by identical RGB (quantization steps tried: [1, 16, 32, 64, 128, 255]) into ≤ **24 packets** of `Nn(8, 1, [R, G, B, idCount, ids…])` with ≤ **52 ids** per packet, followed by terminator `Nn(1, 0)`. Minimum **34 ms** between frames.
- **Stop**: restore colours via `x5()` packets `Nn(6, 0, [0, id, R, G, B] × 11, total = 7 packets)`, then `Nn(4, 1, originalLighting)`, then `Nn(1, 0)`.
- Key table `va` = **68 entries** `{id, slot}`; frame must be 68 × 3 bytes.

### 5.8 GravaStar Mercury K1 — Family A [JS, class `Ey` ~19374–19710; profiles `Sn/jl`]
- Matcher `Jd`: VID `0x258A`, PID `0x010C` **or** `0x0294`, `0xFF00` channel; product-name guards exclude K1 Pro/Lite/JP/DE.
- Because PID `0x010C` collides with the F75, identity is **probed** (`ib()`): send feature report `R5()` = `[130 (0x82), 1, 0, 1, 0, 10, 0, …]`, wait 20 ms, read the reply; bytes at offset 7 decide: **`[3,0,0,0,1,180]` → "gravastar-k1"**, **`[3,0,0,0,0,205]` → "aula-f75"` (180 = 0xB4, 205 = 0xCD — model codes).
- **Frame** `j5()`: 519-byte report, header **`[8, 0, 0, 1, 0, 122, 1]`**, RGB for **126 LED slots × 3 = 378 B** at offset 7. (Observed discrepancy: header byte [5] says 122 while the frame carries 126 slots — recorded as-is. The header itself is byte-identical to the F75's frame header; only the slot count in the payload differs, 126 vs 122.) Drain sends via `sendFeatureReport(6, …)` with **pacer wait(50)** between frames.

### 5.9 MAD 60 V2 "New Star" (`28E9:32BD`, alt `28E9:3256`) — Family F1 [JS, class `xd` ~17403–17660; profiles `Yr/Wx`, list `Eh`]
- Channel: usagePage `0xFF87` (65415), usage `0x20`, **reportId 6**, reports of 504 bits = **63 B** via `sendReport`/`inputreport`.
- Packet builder `$x(cmd, offset, len, payload)`: `[cmd, offsetLE16, len, checksumLE16 @ bytes 4–5, 0, payload @ byte 7]`; the checksum is the **plain sum of all 63 bytes** (computed with the checksum field zeroed, stored LE); payload ≤ 56 B; cmd 29 requires payload length == len.
- Reply validator `Jx`: 63 B, first 3 bytes echo the request header, byte[3] == request len byte, **byte[6] == 85 (0x55)**; payload returned from byte 7.
- Commands: **18** = identity (read 64 B; validated by `t5`: bytes[2..3] = PID LE, **byte[4] = ledSlots (≥ 61)**, byte[6] == 61; for PID `0x3256` an alternate identity form starts `AA BB` with byte[4]==128, byte[9]==0), **33** = read LED routing table (ledSlots × 3 bytes; parsed by `e5` against the 61-entry sequence `ay` — values 0..60 with value 56 repeated 5× → 65 stream positions, the same 61-key + 5-LED-spacebar shape as WIN60 SE), **29** = frame write (`pushFrame` reorders key-order RGB into device-LED order via `n5(frame, targets)`, then chunks; pacer wait(5) after each frame), **30** = release (64 B) sent on stop if any frame was touched.

---

## 6. Cross-model notes

- **One GIF library, many grids**: the server `gifs/metadata.json` tags each GIF with a `models` list drawn from the 10 model ids above (incl. `win60-se`); a GIF authored for one 61-key board is offered to all 61-key boards. [SRV]
- **Backup/restore discipline** is universal except WIN60 SE: F75 Max-class (B) reads 9 state blocks, MAD60 reads 210 B of colours, HERO68 reads per-key colours + lighting state, K1/F75 keep the device in a vendor "custom" mode entered/exited via their mode reports. On any stream error the drivers attempt the same restore path. [JS]
- **No model receives persistent writes.** There is no "save effect to keyboard" command anywhere in the frontend (the WIN60 SE help text says so explicitly: "Chưa hỗ trợ lưu hiệu ứng vào bàn phím"). [JS]
- **Effective frame-rate ceilings** differ by pacing: playback clock 20 fps; HERO68 ≥ 34 ms/frame; WIN60 SE 34 ms/frame; K1 50 ms/frame; WIN60 HE 8 packets × 2 ms; SONiX 8 blocks × 2 ms + ACK round-trips; MAD60 10 echoed exchanges/frame. [JS]

---

## 7. Coverage note — what was recovered, what was not

**Recovered (≈ complete for the frontend):**
- 84 embedded assets carved and brotli-decompressed (`assets/`), including the full application bundle `index-CYgHsvd1.js` (544,655 B decompressed) — 100% of the app's JS logic, minified but readable; a beautified copy is included for reference.
- All 12 model definitions, all key tables, all 10 driver classes, the connection manager, the GIF decode/sample/map pipeline, both worker scripts.
- Server configs (`server-config/`): layout JSONs + ai-prompts for f75, s75-pro, gravastar-k1, hero68-he, mad60-he, mad60-he-v2, win60he; `hid-pacer.js`, `playback-clock.js`, `gifs/catalog.json`, `gifs/metadata.json`. Embedded and server layout JSONs are content-identical where both exist. **404s**: `aula-nova75-layout-900x352.json`, both win60-se layout guesses, both aula-f75-max layout guesses, server `aula-nova75-ai-prompt.txt` (an embedded copy exists and is in `assets/`).

**Not recovered / not in scope tonight:**
- **Rust native code was mapped, not decompiled** (per plan). The only protocol-relevant unknowns inside it are `hid_write_nova_frame`'s chunking/pacing and `hid-profiles.rs`'s matching — both bounded, both listed in §2.2. Everything else native is generic transport (hidapi passthrough).
- **Opaque payload semantics** the frontend itself never decodes: WIN60 HE's 12-byte lighting reply (§5.1), F75's 128-byte config block (§5.3), MAD60's info packet fields beyond the three bytes the app rewrites (`[0]=7, [2]=1, [8]=200`, §5.6), HERO68's setup payloads `[19,0]` / `[100,4]` (§5.7). These are recorded verbatim; decoding them needs hardware experiments or a future native/firmware pass.
- **Nothing here is hardware-verified.** Every protocol claim is source-confirmed [JS] from the vendor's own shipping code — the strongest evidence short of a bus capture — but the first on-device test for each family should still be read-only where the protocol allows it (Family B: the `0xF5` probe; Family A: the `0x84`/`0x82` identity reads; Family D: cmd 130/132 reads; Family C: cmd `[1]`/`[8,65]` reads; Family F1: cmd 18/33 reads; Family F2: (1,1)/(5,x) reads; Family E: cmd 7 read).

**F75 Max status (explicit):** this build contains no F75 Max model, no F75 Max layout, and matchers that reject "MAX" product names on both candidate channels. [INF] If a real F75 Max enumerates with a generic product name (no "MAX"), the SONiX matcher `Xd` could still claim it as an "S75 Pro" — same VID:PID/channel — but its key table (80 keys) would not match the Max's. Treat any F75 Max support in the shop's marketing as belonging to another version until a unit is plugged in and its product string read.
