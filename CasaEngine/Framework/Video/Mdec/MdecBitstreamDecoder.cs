using System.Buffers.Binary;
using System.Numerics;

namespace CasaEngine.Framework.Video.Mdec;

/// <summary>
/// First stage of the MDEC decoder (docs/engine/video.md sections 1.2 to 1.6): turns a BS frame (versions 1, 2 and 3)
/// into the 16-bit words the macroblock decoder consumes. For each block, in the macroblock order Cr, Cb, Y1, Y2, Y3,
/// Y4, the output holds one DC word <c>(quantScale &lt;&lt; 10) | (dc &amp; 0x3FF)</c>, the run-length words
/// <c>(run &lt;&lt; 10) | (level &amp; 0x3FF)</c> exactly as coded (an escape word is passed on unchanged), and one
/// end-of-block word <see cref="EndOfBlockWord"/>. A DC word may equal <see cref="EndOfBlockWord"/>: consumers read a
/// block as "DC word, then words up to the first end-of-block word".<para/>
/// Stateless and allocation-free: the caller owns the output buffer. Any malformed frame throws an
/// <see cref="InvalidDataException"/> whose message names the frame.<para/>
/// Written from the public psx-spx description of the BS formats
/// (https://psx-spx.consoledev.net/cdromfileformats/, "BS Compression" sections) and of the MDEC data input.
/// </summary>
public static class MdecBitstreamDecoder
{
    /// <summary>Size of the BS frame header, in bytes.</summary>
    public const int HeaderSize = 8;

    /// <summary>The end-of-block MDEC word.</summary>
    public const ushort EndOfBlockWord = 0xFE00;

    /// <summary>Number of blocks of a macroblock: Cr, Cb, Y1, Y2, Y3, Y4.</summary>
    public const int BlocksPerMacroblock = 6;

    private const ushort FrameIdentifier = 0x3800;
    private const int MaxQuantScale = 0x3F;
    private const uint EndOfFrameDc = 0x1FF;

    // AC code table of docs/engine/video.md section 1.5, one array per row, named after the row's prefix.
    // Entry i is the MDEC word of the row's index i, for a positive level.
    private static readonly ushort[] Row010 = { 0x0002, 0x0801 };
    private static readonly ushort[] Row0011 = { 0x1001, 0x0C01 };
    private static readonly ushort[] Row00100 = { 0x3401, 0x0006, 0x3001, 0x2C01, 0x0C02, 0x0403, 0x0005, 0x2801 };
    private static readonly ushort[] Row0001 = { 0x1C01, 0x1801, 0x0402, 0x1401 };
    private static readonly ushort[] Row00001 = { 0x0802, 0x2401, 0x0004, 0x2001 };
    private static readonly ushort[] Row0000001 = { 0x4001, 0x1402, 0x0007, 0x0803, 0x0404, 0x3C01, 0x3801, 0x1002 };
    private static readonly ushort[] Row00000001 = { 0x000B, 0x2002, 0x1003, 0x000A, 0x0804, 0x1C02, 0x5401, 0x5001, 0x0009, 0x4C01, 0x4801, 0x0405, 0x0C03, 0x0008, 0x1802, 0x4401 };
    private static readonly ushort[] Row000000001 = { 0x2802, 0x2402, 0x1403, 0x0C04, 0x0805, 0x0407, 0x0406, 0x000F, 0x000E, 0x000D, 0x000C, 0x6801, 0x6401, 0x6001, 0x5C01, 0x5801 };
    private static readonly ushort[] Row0000000001 = { 0x001F, 0x001E, 0x001D, 0x001C, 0x001B, 0x001A, 0x0019, 0x0018, 0x0017, 0x0016, 0x0015, 0x0014, 0x0013, 0x0012, 0x0011, 0x0010 };
    private static readonly ushort[] Row00000000001 = { 0x0028, 0x0027, 0x0026, 0x0025, 0x0024, 0x0023, 0x0022, 0x0021, 0x0020, 0x040E, 0x040D, 0x040C, 0x040B, 0x040A, 0x0409, 0x0408 };
    private static readonly ushort[] Row000000000001 = { 0x0412, 0x0411, 0x0410, 0x040F, 0x1803, 0x4002, 0x3C02, 0x3802, 0x3402, 0x3002, 0x2C02, 0x7C01, 0x7801, 0x7401, 0x7001, 0x6C01 };

    /// <summary>
    /// Upper bound of the number of words <see cref="Decode"/> can write for a frame of
    /// <paramref name="frameLength"/> bytes: every word costs at least two bits of the stream.
    /// </summary>
    public static int MaxWordCount(int frameLength)
    {
        var streamBytes = (frameLength - HeaderSize) & ~1;
        return streamBytes > 0 ? streamBytes * 4 : 0;
    }

    /// <summary>
    /// Decodes the first <paramref name="macroblockCount"/> macroblocks of a BS frame into MDEC words.
    /// </summary>
    /// <param name="frame">The whole BS frame, header included.</param>
    /// <param name="frameIndex">Frame number, used in error messages.</param>
    /// <param name="macroblockCount">Number of macroblocks of the picture (section 1.6).</param>
    /// <param name="words">Output buffer; <see cref="MaxWordCount"/> words always suffice.</param>
    /// <returns>The number of words written: always a whole number of blocks.</returns>
    /// <exception cref="InvalidDataException">The header, a code or the length of the frame is invalid.</exception>
    public static int Decode(ReadOnlySpan<byte> frame, int frameIndex, int macroblockCount, Span<ushort> words)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(macroblockCount);

        if (frame.Length < HeaderSize)
        {
            throw Error(frameIndex, $"the frame is {frame.Length} bytes long, shorter than its {HeaderSize} byte header");
        }

        var identifier = BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(2, 2));
        var quantScale = BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(4, 2));
        var version = BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(6, 2));

        if (identifier != FrameIdentifier)
        {
            throw Error(frameIndex, $"the identifier is 0x{identifier:X4}, expected 0x{FrameIdentifier:X4}");
        }

        if (quantScale > MaxQuantScale)
        {
            throw Error(frameIndex, $"the quantisation scale is {quantScale}, above {MaxQuantScale}");
        }

        if (version is < 1 or > 3)
        {
            throw Error(frameIndex, $"the version is {version}, only 1, 2 and 3 are supported");
        }

        var reader = new MdecBitReader(frame.Slice(HeaderSize), frameIndex);
        var quantBits = (uint)quantScale << 10;
        var count = 0;
        var predictorCr = 0;
        var predictorCb = 0;
        var predictorY = 0;

        for (var macroblock = 0; macroblock < macroblockCount; macroblock++)
        {
            for (var block = 0; block < BlocksPerMacroblock; block++)
            {
                uint dc;
                if (version == 3)
                {
                    var difference = ReadDcDifference(ref reader, frameIndex, block, out var endOfFrame);
                    if (endOfFrame)
                    {
                        throw EarlyEndOfFrame(frameIndex, macroblock, macroblockCount);
                    }

                    // Cr and Cb have their own predictor, Y1..Y4 share one, also across macroblocks (section 1.4).
                    switch (block)
                    {
                        case 0:
                            predictorCr = (predictorCr + difference * 4) & 0x3FF;
                            dc = (uint)predictorCr;
                            break;
                        case 1:
                            predictorCb = (predictorCb + difference * 4) & 0x3FF;
                            dc = (uint)predictorCb;
                            break;
                        default:
                            predictorY = (predictorY + difference * 4) & 0x3FF;
                            dc = (uint)predictorY;
                            break;
                    }
                }
                else
                {
                    var raw = reader.Peek(10);
                    if (block == 0 && raw == EndOfFrameDc)
                    {
                        throw EarlyEndOfFrame(frameIndex, macroblock, macroblockCount);
                    }

                    reader.Skip(10);
                    dc = raw;
                }

                Put(words, ref count, frameIndex, (ushort)(quantBits | dc));
                ReadBlockAc(ref reader, frameIndex, words, ref count);
            }
        }

        return count;
    }

    private static void ReadBlockAc(ref MdecBitReader reader, int frameIndex, Span<ushort> words, ref int count)
    {
        while (true)
        {
            var bits = reader.Peek(17);
            var zeros = BitOperations.LeadingZeroCount(bits) - 15;
            int prefixLength;
            int indexBits;
            ushort[] row;
            switch (zeros)
            {
                case 0:
                    if ((bits & 0x8000) == 0)
                    {
                        // 10: end of block.
                        reader.Skip(2);
                        Put(words, ref count, frameIndex, EndOfBlockWord);
                        return;
                    }

                    // 11s
                    reader.Skip(3);
                    PutSigned(words, ref count, frameIndex, 0x0001, (bits >> 14) & 1);
                    continue;
                case 1:
                    if ((bits & 0x4000) != 0)
                    {
                        // 011s
                        reader.Skip(4);
                        PutSigned(words, ref count, frameIndex, 0x0401, (bits >> 13) & 1);
                        continue;
                    }

                    prefixLength = 3; // 010xs
                    indexBits = 1;
                    row = Row010;
                    break;
                case 2:
                    if ((bits & 0x2000) != 0)
                    {
                        prefixLength = 4; // 0011xs
                        indexBits = 1;
                        row = Row0011;
                    }
                    else if ((bits & 0x1000) != 0)
                    {
                        // 00101s
                        reader.Skip(6);
                        PutSigned(words, ref count, frameIndex, 0x0003, (bits >> 11) & 1);
                        continue;
                    }
                    else
                    {
                        prefixLength = 5; // 00100xxxs
                        indexBits = 3;
                        row = Row00100;
                    }

                    break;
                case 3:
                    prefixLength = 4; // 0001xxs
                    indexBits = 2;
                    row = Row0001;
                    break;
                case 4:
                    prefixLength = 5; // 00001xxs
                    indexBits = 2;
                    row = Row00001;
                    break;
                case 5:
                    // 000001 + 16 bits: escape, the word as is. FE00 ends the block like the code 10 (section 1.5).
                    reader.Skip(6);
                    var escaped = (ushort)reader.Read(16);
                    Put(words, ref count, frameIndex, escaped);
                    if (escaped == EndOfBlockWord)
                    {
                        return;
                    }

                    continue;
                case 6:
                    prefixLength = 7;
                    indexBits = 3;
                    row = Row0000001;
                    break;
                case 7:
                    prefixLength = 8;
                    indexBits = 4;
                    row = Row00000001;
                    break;
                case 8:
                    prefixLength = 9;
                    indexBits = 4;
                    row = Row000000001;
                    break;
                case 9:
                    prefixLength = 10;
                    indexBits = 4;
                    row = Row0000000001;
                    break;
                case 10:
                    prefixLength = 11;
                    indexBits = 4;
                    row = Row00000000001;
                    break;
                case 11:
                    prefixLength = 12;
                    indexBits = 4;
                    row = Row000000000001;
                    break;
                default:
                    throw Error(frameIndex, "invalid AC code (twelve zero bits)");
            }

            // The code is left aligned in the 17 peeked bits: prefix, index bits, then the sign bit.
            var shift = 17 - prefixLength - indexBits;
            var index = (int)((bits >> shift) & ((1u << indexBits) - 1));
            reader.Skip(prefixLength + indexBits + 1);
            PutSigned(words, ref count, frameIndex, row[index], (bits >> (shift - 1)) & 1);
        }
    }

    // Version 3 DC (section 1.4): Huffman-coded difference to the predictor, in units of 4. Sets endOfFrame, and
    // consumes the code, for 1111111111 at a Cr block; any other use of that code, and the unused codes, throw.
    private static int ReadDcDifference(ref MdecBitReader reader, int frameIndex, int block, out bool endOfFrame)
    {
        endOfFrame = false;
        var bits = reader.Peek(10);
        var ones = BitOperations.LeadingZeroCount(~(bits << 22));
        int prefixLength;
        int magnitudeBits; // number of n bits of the row: the row's class, 0 for 01s / 00s

        if (block < 2)
        {
            // Cr and Cb table.
            if ((bits >> 8) == 0b00)
            {
                reader.Skip(2); // 00: zero
                return 0;
            }

            if ((bits >> 8) == 0b01)
            {
                prefixLength = 2; // 01s
                magnitudeBits = 0;
            }
            else if ((bits >> 8) == 0b10)
            {
                prefixLength = 2; // 10sn
                magnitudeBits = 1;
            }
            else if (ones <= 7)
            {
                prefixLength = ones + 1; // 110snn ... 11111110snnnnnnn
                magnitudeBits = ones;
            }
            else
            {
                prefixLength = 0;
                magnitudeBits = 0;
            }
        }
        else
        {
            // Y1..Y4 table.
            if ((bits >> 7) == 0b100)
            {
                reader.Skip(3); // 100: zero
                return 0;
            }

            if ((bits >> 8) == 0b00)
            {
                prefixLength = 2; // 00s
                magnitudeBits = 0;
            }
            else if ((bits >> 8) == 0b01)
            {
                prefixLength = 2; // 01sn
                magnitudeBits = 1;
            }
            else if ((bits >> 7) == 0b101)
            {
                prefixLength = 3; // 101snn
                magnitudeBits = 2;
            }
            else if ((bits >> 7) == 0b110)
            {
                prefixLength = 3; // 110snnn
                magnitudeBits = 3;
            }
            else if (ones <= 6)
            {
                prefixLength = ones + 1; // 1110snnnn ... 1111110snnnnnnn
                magnitudeBits = ones + 1;
            }
            else
            {
                prefixLength = 0;
                magnitudeBits = 0;
            }
        }

        if (prefixLength == 0)
        {
            // Only the unused codes and the end-of-frame code are left.
            if (ones >= 10 && block == 0)
            {
                reader.Skip(10);
                endOfFrame = true;
                return 0;
            }

            throw Error(
                frameIndex,
                ones >= 10 ? "the end-of-frame code appears in the middle of a macroblock" : "unused version 3 DC code");
        }

        // Prefix, sign bit (1 = positive), then the magnitude bits.
        var codeLength = prefixLength + 1 + magnitudeBits;
        var code = reader.Peek(codeLength);
        reader.Skip(codeLength);
        var n = (int)(code & ((1u << magnitudeBits) - 1));
        var positive = ((code >> magnitudeBits) & 1) != 0;
        return positive ? (1 << magnitudeBits) + n : n - ((2 << magnitudeBits) - 1);
    }

    private static void Put(Span<ushort> words, ref int count, int frameIndex, ushort word)
    {
        if ((uint)count >= (uint)words.Length)
        {
            throw Error(frameIndex, $"the output buffer of {words.Length} words is too small");
        }

        words[count++] = word;
    }

    // A code with a sign bit: the table gives the word of a positive level, a set sign bit negates the level.
    private static void PutSigned(Span<ushort> words, ref int count, int frameIndex, ushort word, uint sign)
    {
        if (sign != 0)
        {
            word = (ushort)((word & 0xFC00) | (-(word & 0x3FF) & 0x3FF));
        }

        Put(words, ref count, frameIndex, word);
    }

    private static InvalidDataException EarlyEndOfFrame(int frameIndex, int macroblocksRead, int macroblockCount)
    {
        return Error(frameIndex, $"end of frame after {macroblocksRead} of {macroblockCount} macroblocks");
    }

    private static InvalidDataException Error(int frameIndex, string reason)
    {
        return new InvalidDataException($"MDEC frame {frameIndex}: {reason}.");
    }
}
