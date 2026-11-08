# Video playback

The engine plays videos stored in the `.mdecvideo` container: a header, an offset table and one MDEC-compressed picture per frame (see [ADR-0089](../decisions/0089-the-engine-plays-mdec-videos-through-a-video-element.md)). This document is the written specification the decoder follows. It is the first thing written for the feature: no decoder code exists before it.

Sections:

1. [MDEC bitstream](#1-mdec-bitstream) (this document, written by task T1.1).
2. Container, player, `<Video>` element and cut-scene action: added by the tasks that implement them.

## 1. MDEC bitstream

### 1.0 Sources and conventions

Public sources only. Every rule below is written in the engine's own words and carries the section it comes from.

| Tag | Source |
|---|---|
| **[M]** | psx-spx, "Macroblock Decoder (MDEC)": https://psx-spx.consoledev.net/macroblockdecodermdec/ , with the sub-section named after the tag (for example `[M: data input format]`). |
| **[BS-H]** | psx-spx, "CDROM File Formats", section "BS Compression Headers": https://psx-spx.consoledev.net/cdromfileformats/ |
| **[BS-DC]** | same page, section "BS Compression DC Values". |
| **[BS-AC]** | same page, section "BS Compression AC Values" (including its notes). |
| **[BS-V]** | same page, section "BS Compression Versions" and the introduction of "BS Files". |

A rule marked **engine choice** is not given by psx-spx, or psx-spx leaves a point open or says the hardware is not reproduced bit for bit. The engine fixes the behaviour here, and tests prove that formula. A rule marked **engine choice** is never claimed to match any particular disc or reference player; section 1.12 lists the choices a comparison against a reference could contradict.

Notation: `u16` is an unsigned 16-bit value, `s10` is the 10-bit two's-complement value held in the low 10 bits of a word (range -512..+511), `>>` on a signed integer is an arithmetic (flooring) shift, all arithmetic is on 32-bit signed integers unless stated.

### 1.1 Data flow

A frame goes through five stages. Each stage is a pure function of its input, with no state kept between frames.

1. **Bitstream to words** (sections 1.3 to 1.6): the Huffman-coded bitstream becomes 16-bit "MDEC words", the units the hardware decoder consumes: one DC word, zero or more run-length (RLE) words and one end-of-block word per block.
2. **Words to coefficients** (1.7, 1.8): dequantisation and zig-zag un-scan give an 8x8 block of coefficients.
3. **Inverse transform** (1.9): integer 8x8 IDCT.
4. **Colour** (1.10): six blocks of a macroblock become 16x16 RGB pixels.
5. **Placement** (1.11): macroblocks are placed in the picture and cropped to the displayed size.

### 1.2 Frame header

`[BS-H: BS v1/v2/v3 header]`. A frame is a BS picture: an 8-byte header followed by the bitstream. Fields are little-endian.

| Offset | Size | Meaning | Rule |
|---|---|---|---|
| 0 | `u16` | Size of the expanded MDEC data in 32-bit words, rounded up to a multiple of 0x20 words. | **Engine choice**: not interpreted and not validated. The picture size comes from the container, and the end of the frame is found by counting macroblocks (1.6). |
| 2 | `u16` | Identifier, always `0x3800`. | Any other value is an error. |
| 4 | `u16` | Quantisation scale, 0..0x3F. It is the value that the hardware reads in bits 15..10 of every DC word. | A value above 0x3F is an error. |
| 6 | `u16` | Version: 1, 2 or 3. Version 2 is the most common. | Any other value is an error. |
| 8 | - | Bitstream. | |

Versions 1 and 2 are decoded identically; version 1 only differs because its encoder produced a malformed escape code (1.5). Version 3 differs by coding the DC values (1.4). `[BS-V]`

The encrypted variants and the other header layouts listed in `[BS-H]` (versions 0, `iki`, `fraquant`, `ea`, raw MDEC) are **not supported**: a version field of 0 or above 3 is an error. `[BS-H: encrypted v2/v3]` says a value of 3 or less in this field means "not encrypted".

### 1.3 Bit order and reading

`[BS-V: BS Files introduction]`: for versions 1, 2 and 3 the first bit of the stream is bit 15 of the first 16-bit halfword. The stream is read as a sequence of little-endian `u16` halfwords starting at byte 8 of the frame, and bits are consumed from bit 15 down to bit 0 of each halfword before the next halfword is started.

A code that is longer than the bits remaining in the frame is an error that names the frame. **Engine choice**: a trailing odd byte (half a halfword) is ignored; peeking beyond the end for a table lookup reads zero bits, and only consuming them is an error.

### 1.4 DC value of a block

Every block starts with a DC code. A macroblock holds six blocks in the order **Cr, Cb, Y1, Y2, Y3, Y4** (`[BS-V: macroblock ordering]`, `[BS-DC: block order]`, `[M: macroblock layout]`).

The result of the DC code is a **DC word**: `(quantScale << 10) | (dc & 0x3FF)`, where `quantScale` is the header field of 1.2 and `dc` is the 10-bit signed DC value.

#### Versions 1 and 2 `[BS-DC: DC v1/v2/ea]`

* The DC is 10 raw bits, interpreted as `s10` (range -0x200..+0x1FE).
* The pattern `0111111111` (value +0x1FF) is **end of frame** and takes the place of the Cr DC of a macroblock.
* The psx-spx text writes `QuantDC = DC + Quant * 0x400`. **Engine choice**: the engine forms the word by placing the 10 raw bits in bits 9..0 and the scale in bits 15..10, which is what the word layout `[M: data input format]` (bits 15-10 scale, bits 9-0 DC) requires and which equals the written sum for a non-negative DC.
* **Engine choice**: the end-of-frame meaning of `0111111111` is recognised only where a Cr block is expected. Elsewhere the same 10 bits are an ordinary DC value.

#### Version 3 `[BS-DC: DC v3]`

The DC is coded as a Huffman-coded difference to the previous DC of the same component. There are three predictors: Cr, Cb, and one shared by Y1..Y4 (also across macroblocks). All three start at 0 at the start of the frame.

Code tables. `s` is the sign bit (0 = negative, 1 = positive), `n...` are magnitude bits. The difference is added to the predictor and the result becomes the new predictor and the DC; offsets are written in the stream in units of 4, so the real difference is `value * 4`.

| Cr and Cb code | Y1..Y4 code | Value |
|---|---|---|
| `00` | `100` | 0 |
| `01s` | `00s` | magnitude 1 |
| `10sn` | `01sn` | magnitude 2..3 |
| `110snn` | `101snn` | magnitude 4..7 |
| `1110snnn` | `110snnn` | magnitude 8..15 |
| `11110snnnn` | `1110snnnn` | magnitude 16..31 |
| `111110snnnnn` | `11110snnnnn` | magnitude 32..63 |
| `1111110snnnnnn` | `111110snnnnnn` | magnitude 64..127 |
| `11111110snnnnnnn` | `1111110snnnnnnn` | magnitude 128..255 |
| - | `11111110` | unused |
| `111111110` | `111111110` | unused |
| `1111111110` | `1111111110` | unused |
| `1111111111` | `1111111111` | end of frame |

Reading of the magnitude. psx-spx writes the ranges as "-(03h..02h)" for the negative side and "+(02h..03h)" for the positive side of the row `10sn`, and says the `n` bits index the range of the row. **Engine choice**, consistent with the listed ranges and with the usual coding of a signed magnitude class: for a row with `k` magnitude bits (`k` = 0 for the row `01s`/`00s`, 1 for `10sn`, and so on), let `n` be the `k` bits read as an unsigned number. Then

* `s = 1`: `value = (1 << k) + n` (positive, ascending);
* `s = 0`: `value = n - ((2 << k) - 1)` (negative; `n = 0` is the largest magnitude, so row `10sn` gives -3 then -2).

The largest difference is `255 * 4 = 0x3FC`, which matches the statement "the offsets cover -3FCh..+3FCh".

Wrap. `[BS-DC: DC v3, wrap rules]` describes two behaviours: older decoders work on 11 bits, newer decoders keep 10 bits (`quant * 0x400 + (dc & 0x3FF)`). **Engine choice**: the 10-bit rule. The predictor is kept modulo 1024 (`predictor = (predictor + value * 4) & 0x3FF`), and the DC word is built from that. This agrees with the older rule for every DC an 11-bit decoder could represent.

End of frame (`1111111111`) is recognised where a Cr code is expected. **Engine choice**: the same bits at any other block of a macroblock are an error, since a frame cannot end in the middle of a macroblock.

The `unused` codes are errors.

### 1.5 AC codes of a block

After the DC, the block holds AC codes until the **end-of-block** code `10`. `[BS-AC: Huffman codes for AC values BS v1/v2/v3/iki]`. All versions use the same codes; they only differ by the escape peculiarity of version 1. `[BS-AC: notes]`

Each code produces one 16-bit MDEC word `(run << 10) | (level & 0x3FF)`: `run` (bits 15..10) is the number of zero coefficients to skip, `level` (bits 9..0) is the `s10` value. For a code with a sign bit `s`, the table gives the word for a positive level; when `s = 1` the level is negated (and masked to 10 bits). `x...` are index bits, read as an unsigned number that selects the entry of the word list (first entry = index 0).

| Code | Words, by index | Notes |
|---|---|---|
| `10` | `FE00` | end of block |
| `11s` | `0001` | |
| `011s` | `0401` | |
| `010xs` | `0002`, `0801` | |
| `0011xs` | `1001`, `0C01` | |
| `00101s` | `0003` | |
| `00100xxxs` | `3401`, `0006`, `3001`, `2C01`, `0C02`, `0403`, `0005`, `2801` | |
| `0001xxs` | `1C01`, `1801`, `0402`, `1401` | |
| `00001xxs` | `0802`, `2401`, `0004`, `2001` | |
| `000001` + 16 bits | the 16 bits, as is | escape, see below |
| `0000001xxxs` | `4001`, `1402`, `0007`, `0803`, `0404`, `3C01`, `3801`, `1002` | |
| `00000001xxxxs` | `000B`, `2002`, `1003`, `000A`, `0804`, `1C02`, `5401`, `5001`, `0009`, `4C01`, `4801`, `0405`, `0C03`, `0008`, `1802`, `4401` | |
| `000000001xxxxs` | `2802`, `2402`, `1403`, `0C04`, `0805`, `0407`, `0406`, `000F`, `000E`, `000D`, `000C`, `6801`, `6401`, `6001`, `5C01`, `5801` | |
| `0000000001xxxxs` | `001F`, `001E`, `001D`, `001C`, `001B`, `001A`, `0019`, `0018`, `0017`, `0016`, `0015`, `0014`, `0013`, `0012`, `0011`, `0010` | |
| `00000000001xxxxs` | `0028`, `0027`, `0026`, `0025`, `0024`, `0023`, `0022`, `0021`, `0020`, `040E`, `040D`, `040C`, `040B`, `040A`, `0409`, `0408` | |
| `000000000001xxxxs` | `0412`, `0411`, `0410`, `040F`, `1803`, `4002`, `3C02`, `3802`, `3402`, `3002`, `2C02`, `7C01`, `7801`, `7401`, `7001`, `6C01` | |
| `000000000000` | - | unused: error |

Check done while writing this table: the codes are prefix-free and, with the escape prefix `000001`, fill the code space except for the unused 12-zero prefix; every row lists exactly `2^(number of x bits)` words.

Escape. `[BS-AC, rows "Escape: raw 16-bit value"]`: the 6-bit prefix `000001` is followed by 16 bits, which are the MDEC word as is (6-bit run, 10-bit level), for any version.

* **Version 1.** `[BS-AC, row "level=0 nonsense code"; BS-V: BS v1]`: the version 1 encoder wrote escapes with a run `N` and a level of 0, which carry no coefficient. psx-spx says a decoder may treat them as "add N+1 to the run of the next code", or drop them when the next code is the end of block. **Engine choice**: no special case in the bit reader. The escape word is passed on like any other word; in the dequantiser (1.7) a word with level 0 moves the position by `run + 1` and writes a zero, which gives exactly the output of both behaviours described by psx-spx.
* **Engine choice**: an escape whose 16 bits are `FE00` ends the block, like the code `10` (this is what the word means to the hardware, `[M: data input format]`).

End of block. A block ends at the first end-of-block word. `[BS-V: BS v1/v2/v3]` and `[BS-AC]` give an end-of-block code to every block. **Engine choice**: the end-of-block code is required: a block whose 63 AC positions are full still has to be followed by it (the hardware accepts a missing end-of-block word for a full block, `[M: data input format]`, but the Huffman stream gives each block its terminator). An AC word that moves the position past 63 and has a non-zero level is an error (1.7).

### 1.6 End of frame

* The frame size in macroblocks is `ceil(width / 16) * ceil(height / 16)`, computed from the container (1.11).
* `[BS-DC]`: the end-of-frame code (1.4) takes the place of the Cr DC of a macroblock. A frame that carries it before all macroblocks were read is an error (message with the frame number and the macroblock count read).
* **Engine choice**: decoding stops when the last macroblock was read. The end-of-frame code after it is optional, and any bits after the last macroblock (the encoder pads the data) are ignored.

### 1.7 Dequantisation (words to coefficients)

`[M: rl_decode_block]`, `[M: quantisation tables]`. For one block, with `q` the scale of the DC word (bits 15..10):

1. All 64 coefficients start at 0 and the position `k` starts at 0.
2. **DC**: `value = s10(dc) * qt[0]`.
3. **AC**: each next word advances `k` by `run + 1` (`run` = bits 15..10). If `k` is then greater than 63 the block is finished; a word with a non-zero level in that case is an error (engine choice, 1.5), a word with level 0 is ignored. Otherwise `value = (s10(level) * qt[k] * q + 4) >> 3`.
   **Engine choice**: psx-spx writes `/ 8` and does not say how negative numbers round; the engine floors (arithmetic shift), which is deterministic and needs no branch.
4. **Scale 0.** If `q = 0` the quantisation table is not used: every value, DC included, is `s10(word) * 2`, and it is stored at position `k` itself with **no zig-zag**. `[M: rl_decode_block]` states this and marks the DC formula as uncertain; the engine follows the written order (the scale-0 rule overrides the DC formula).
5. **Saturation.** Every value is clamped to -0x400..+0x3FF (signed 11 bits), DC included.
6. **Zig-zag.** When `q > 0` the value is stored at raster position `zagzig[k]` of the 8x8 block (row-major, `index = row * 8 + column`).
7. The end-of-block word (`FE00`) finishes the block; remaining coefficients stay 0. `[M: data input format]`: an `FE00` at the very start of a block, or right after an end-of-block, is padding and is ignored; the decoder of 1.5 never produces one.

**Quantisation matrix.** The matrix is not part of the BS data: the hardware gets it from a separate command, and psx-spx gives only the start and the end of the default table (`[BS-H: BS fraquant]`: "The decoder is using the default_quant_table (02h,10h,10h,13h,..,53h)"; the same sub-section says that header quant values 0001h..0003h "should give same results as for normal BS decoding", which ties normal decoding to that table). It also says that a luminance table of 64 unsigned bytes is used for Y1..Y4, that an optional second table is used for Cr and Cb, and that entry `qt[k]` is the one the formula above uses for scan position `k`). **Engine choice**: one matrix for all six blocks. Entry 0 is 2, as psx-spx lists it (the MPEG-1 value 8 would clip most of the DC range and contradict the scale-0 rule `s10 * 2`). Entries 1..63 are the default intra quantiser matrix of MPEG-1 (ISO/IEC 11172-2, "default intra quantizer matrix"), kept because they agree with the three other entries psx-spx lists (10h, 10h, 13h in scan order, and 53h = 83 last); entries 4..62 are not given by psx-spx. The table is indexed in scan order `k`:

```text
 2 16 19 22 26 27 29 34
16 16 22 24 27 29 34 37
19 22 26 27 29 34 34 38
22 22 26 27 29 34 37 40
22 26 27 29 32 35 40 48
26 27 29 32 35 40 48 58
26 27 29 34 38 46 56 69
27 29 35 38 46 56 69 83
```

This table is written in raster order (the usual way to print it) and `qt[k]` is the entry at raster position `zagzig[k]`, because the scan index `k` is what the formula uses.

**Zig-zag.** `[M: zig-zag tables]` gives `zigzag[raster] = scan index`. The inverse `zagzig[scan index] = raster`, derived from it, is:

```text
 0  1  8 16  9  2  3 10
17 24 32 25 18 11  4  5
12 19 26 33 40 48 41 34
27 20 13  6  7 14 21 28
35 42 49 56 57 50 43 36
29 22 15 23 30 37 44 51
58 59 52 45 38 31 39 46
53 60 61 54 47 55 62 63
```

(read left to right, top to bottom: scan index 0, 1, 2, ... 63.)

### 1.8 Block coefficient layout

The block handed to the IDCT is `blk[row * 8 + column]`, `row` = vertical frequency, `column` = horizontal frequency.

### 1.9 Inverse transform

`[M: IDCT]`. The hardware multiplies by a 64-entry table of 16-bit signed numbers with a 14-bit fraction and uses only the top 13 bits of each. psx-spx describes a direct matrix algorithm (two passes) and says its rounding is approximate and that the hardware may round at other places.

**Scale table** `S[64]`, signed 16 bits, as given for the standard table (hex, row by row):

```text
5A82 5A82 5A82 5A82 5A82 5A82 5A82 5A82
7D8A 6A6D 471C 18F8 E707 B8E3 9592 8275
7641 30FB CF04 89BE 89BE CF04 30FB 7641
6A6D E707 8275 B8E3 471C 7D8A 18F8 9592
5A82 A57D A57D 5A82 5A82 A57D A57D 5A82
471C 8275 18F8 6A6D 9592 E707 7D8A B8E3
30FB 89BE 7641 CF04 CF04 7641 89BE 30FB
18F8 B8E3 6A6D 8275 7D8A 9592 471C E707
```

The positive entries are `s0..s7 = 5A82, 7D8A, 7641, 6A6D, 5A82, 471C, 30FB, 18F8`; a negative entry is the bitwise complement of the matching positive one (for example `E707` is the complement of `18F8`), not its negation.

**Engine choice, integer algorithm.** Let `T[i] = S[i] >> 3` (arithmetic shift of the signed 16-bit value, i.e. the top 13 bits). One pass transforms a source array `src` into `dst`, for `y` and `x` in 0..7:

```text
sum        = sum over z = 0..7 of  src[y + z*8] * T[x + z*8]
dst[x + y*8] = (sum + 0x0FFF) >> 13
```

Two passes are run: the first from the coefficient block to a temporary block, the second from the temporary block to the output block. The first pass writes its result transposed (`dst[x + y*8]` with `y` taken from the source column), and the second pass transposes it back, so the output is in raster order (`out[row * 8 + column]`, the pixel row being vertical). The `+0x0FFF` is psx-spx's constant (it marks it as "or so"); the shift floors.

* No clamp between passes or after the second pass (psx-spx mentions none). All intermediate values fit in 32 bits: the dequantised inputs are within -1024..1023 and the largest `T` is 4017, so a first-pass sum is at most 8 * 1024 * 4017 (about 3.3e7), a first-pass result at most about 4016, a second-pass sum at most about 1.3e8 and a second-pass result at most about 15 800.
* An implementation may skip work for zero coefficients, as long as the output is bit-identical to the formula above.

### 1.10 Colour conversion

`[M: macroblock layout, yuv_to_rgb]`. A macroblock has the blocks Cr, Cb, Y1 (top left 8x8), Y2 (top right), Y3 (bottom left), Y4 (bottom right), already through the IDCT as signed values. Each chroma sample covers 2x2 pixels (nearest-neighbour up-sampling): for the pixel at `(px, py)` in the 16x16 macroblock, the chroma sample index is `(px / 2) + (py / 2) * 8`, and the luma value is the sample `(px % 8) + (py % 8) * 8` of the Y block that covers it.

psx-spx gives the real-number formulas `R = Y + 1.402 * Cr`, `G = Y - 0.3437 * Cb - 0.7143 * Cr`, `B = Y + 1.772 * Cb`, clamps each channel to -128..127, and adds 128 for the unsigned output (it writes this as an XOR with 0x80). It says that the fixed-point resolution of the hardware is unknown and that a 9-bit intermediate limit probably exists.

**Engine choice**, fixed point with 16 fractional bits (each constant is the real constant times 65536, rounded to the nearest integer):

```text
r = y + ((  91881 * cr                  + 32768) >> 16)
g = y + (( -22525 * cb -  46812 * cr    + 32768) >> 16)
b = y + (( 116130 * cb                  + 32768) >> 16)
channel = clamp(channel, -128, 127) + 128
```

Rounding is half up (the added 32768 then a flooring shift). `y`, `cb`, `cr` are the unclamped IDCT outputs; no 9-bit limit is applied before the formula. With the bounds of 1.9 (outputs within about +-15 800) the products stay inside 32 bits (the largest is `116130 * 15 800`, about 1.84e9). The result is RGBA with alpha 255 (decision D3 of the plan: output is always 24-bit colour).

Monochrome macroblocks (`y_to_mono`) and the 15-bit output are not used.

### 1.11 Macroblock order and cropping

* A picture of `width x height` displayed pixels has `mbColumns = ceil(width / 16)` macroblock columns and `mbRows = ceil(height / 16)` macroblock rows. Macroblocks are 16x16 pixels.
* `[M: macroblock layout]` states that native data is usually arranged vertically (strips 16 pixels wide). **Engine choice**: the macroblocks of a frame are stored column by column: macroblock number `i` (0-based, in stream order) is at column `i / mbRows` and row `i % mbRows`, its top left pixel at `(16 * column, 16 * row)`. The left-most column comes first and each column is read from top to bottom.
* **Cropping** (psx-spx is silent about sizes that are not multiples of 16; **engine choice**): the decoded area is `16 * mbColumns` by `16 * mbRows`, anchored at the top left; pixels with `x >= width` or `y >= height` are decoded and discarded. The output buffer is `width * height * 4` bytes, RGBA, rows top to bottom, no row padding.

### 1.12 Choices a reference could contradict

These engine choices change the decoded picture if they differ from what a real encoder and player used. They are the points to check first when a decoded frame is compared with a reference player; changing one is a change of this specification, of the tests that prove it, and of the ADR.

1. **Quantisation matrix** (1.7): entry 0 is 2 (psx-spx), entries 1..63 are those of the MPEG-1 default intra matrix (entries 4..62 are not given by psx-spx). The `.mdecvideo` container has no field for a matrix, so a picture encoded for another matrix needs a container change.
2. **Macroblock order** (1.11): column by column.
3. **Negative magnitudes of the version 3 DC** (1.4): `n = 0` is the largest magnitude.
4. **IDCT and colour rounding** (1.9, 1.10): psx-spx says the hardware is not reproduced bit for bit; the engine's formulas are exact and documented, so the output may differ from the hardware by small rounding differences.
5. **DC when the scale is 0** (1.7): `s10 * 2`, as written by psx-spx, which marks the DC formula as uncertain.
