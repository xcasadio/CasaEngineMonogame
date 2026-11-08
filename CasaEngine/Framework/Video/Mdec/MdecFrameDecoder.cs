namespace CasaEngine.Framework.Video.Mdec;

/// <summary>
/// Second stage of the MDEC decoder (docs/engine/video.md sections 1.7 to 1.11): turns the MDEC words produced by
/// <see cref="MdecBitstreamDecoder"/> into a cropped RGBA picture. For every block the words are dequantised and
/// un-zig-zagged, the 8x8 integer inverse transform is applied, and the six blocks of each macroblock are converted
/// to 16x16 pixels of colour. Macroblocks are placed column by column, and the picture is cropped to the displayed
/// size.<para/>
/// Stateless and allocation-free (the working blocks live on the stack): the caller owns the output buffer. Any
/// malformed input throws an <see cref="InvalidDataException"/> whose message names the frame.<para/>
/// Written from the public psx-spx description of the MDEC and from the engine's own specification.
/// </summary>
public static class MdecFrameDecoder
{
    /// <summary>Width and height of a macroblock, in pixels.</summary>
    public const int MacroblockSize = 16;

    private const int BlockSize = 64;
    private const int MacroblockCoefficients = MdecBitstreamDecoder.BlocksPerMacroblock * BlockSize;

    // Default quantisation matrix in raster order (section 1.7); QuantByScan reads it through the inverse zig-zag.
    private static readonly byte[] QuantRaster =
    {
        2, 16, 19, 22, 26, 27, 29, 34,
        16, 16, 22, 24, 27, 29, 34, 37,
        19, 22, 26, 27, 29, 34, 34, 38,
        22, 22, 26, 27, 29, 34, 37, 40,
        22, 26, 27, 29, 32, 35, 40, 48,
        26, 27, 29, 32, 35, 40, 48, 58,
        26, 27, 29, 34, 38, 46, 56, 69,
        27, 29, 35, 38, 46, 56, 69, 83,
    };

    // zagzig[scan index] = raster position (section 1.7).
    private static readonly byte[] ZagZig =
    {
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,
    };

    // Scale table S of section 1.9, as unsigned 16-bit patterns.
    private static readonly ushort[] ScaleTable =
    {
        0x5A82, 0x5A82, 0x5A82, 0x5A82, 0x5A82, 0x5A82, 0x5A82, 0x5A82,
        0x7D8A, 0x6A6D, 0x471C, 0x18F8, 0xE707, 0xB8E3, 0x9592, 0x8275,
        0x7641, 0x30FB, 0xCF04, 0x89BE, 0x89BE, 0xCF04, 0x30FB, 0x7641,
        0x6A6D, 0xE707, 0x8275, 0xB8E3, 0x471C, 0x7D8A, 0x18F8, 0x9592,
        0x5A82, 0xA57D, 0xA57D, 0x5A82, 0x5A82, 0xA57D, 0xA57D, 0x5A82,
        0x471C, 0x8275, 0x18F8, 0x6A6D, 0x9592, 0xE707, 0x7D8A, 0xB8E3,
        0x30FB, 0x89BE, 0x7641, 0xCF04, 0xCF04, 0x7641, 0x89BE, 0x30FB,
        0x18F8, 0xB8E3, 0x6A6D, 0x8275, 0x7D8A, 0x9592, 0x471C, 0xE707,
    };

    private static readonly int[] QuantByScan = BuildQuantByScan();
    private static readonly int[] IdctTable = BuildIdctTable();

    /// <summary>Number of macroblock columns of a picture of the given displayed width.</summary>
    public static int MacroblockColumns(int width) => (width + MacroblockSize - 1) / MacroblockSize;

    /// <summary>Number of macroblock rows of a picture of the given displayed height.</summary>
    public static int MacroblockRows(int height) => (height + MacroblockSize - 1) / MacroblockSize;

    /// <summary>Number of macroblocks of a picture of the given displayed size.</summary>
    public static int MacroblockCount(int width, int height) => MacroblockColumns(width) * MacroblockRows(height);

    /// <summary>
    /// Decodes the MDEC words of one picture into <paramref name="rgba"/>.
    /// </summary>
    /// <param name="words">
    /// The words of <see cref="MdecBitstreamDecoder.Decode"/>: for each block (Cr, Cb, Y1..Y4 of each macroblock, in
    /// stream order) a DC word, run-length words and an end-of-block word. Only the first
    /// <see cref="MacroblockCount"/> macroblocks are read.
    /// </param>
    /// <param name="frameIndex">Frame number, used in error messages.</param>
    /// <param name="width">Displayed width in pixels.</param>
    /// <param name="height">Displayed height in pixels.</param>
    /// <param name="rgba">Output, at least <c>width * height * 4</c> bytes: RGBA, rows top to bottom, alpha 255.</param>
    /// <exception cref="InvalidDataException">The words are not a valid sequence of blocks.</exception>
    public static void Decode(ReadOnlySpan<ushort> words, int frameIndex, int width, int height, Span<byte> rgba)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (rgba.Length < width * height * 4)
        {
            throw new ArgumentException($"The output holds {rgba.Length} bytes, {width * height * 4} are needed.", nameof(rgba));
        }

        var rows = MacroblockRows(height);
        var count = MacroblockColumns(width) * rows;
        Span<int> coefficients = stackalloc int[MacroblockCoefficients];
        Span<int> temp = stackalloc int[BlockSize];
        var position = 0;

        for (var macroblock = 0; macroblock < count; macroblock++)
        {
            for (var block = 0; block < MdecBitstreamDecoder.BlocksPerMacroblock; block++)
            {
                var target = coefficients.Slice(block * BlockSize, BlockSize);
                position = ReadBlock(words, position, frameIndex, macroblock, block, target);
                Idct(target, temp);
            }

            // Column by column, each column top to bottom (section 1.11).
            var originX = (macroblock / rows) * MacroblockSize;
            var originY = (macroblock % rows) * MacroblockSize;
            WriteMacroblock(coefficients, originX, originY, width, height, rgba);
        }
    }

    private static int[] BuildQuantByScan()
    {
        var table = new int[BlockSize];
        for (var k = 0; k < BlockSize; k++)
        {
            table[k] = QuantRaster[ZagZig[k]];
        }

        return table;
    }

    private static int[] BuildIdctTable()
    {
        var table = new int[BlockSize];
        for (var i = 0; i < BlockSize; i++)
        {
            table[i] = ((short)ScaleTable[i]) >> 3;
        }

        return table;
    }

    private static int SignExtend10(int value) => ((value & 0x3FF) ^ 0x200) - 0x200;

    private static int Clamp11(int value) => value < -0x400 ? -0x400 : (value > 0x3FF ? 0x3FF : value);

    private static int Clamp128(int value) => value < -128 ? -128 : (value > 127 ? 127 : value);

    private static int ReadBlock(ReadOnlySpan<ushort> words, int position, int frameIndex, int macroblock, int block, Span<int> target)
    {
        target.Clear();

        if (position >= words.Length)
        {
            throw Error(frameIndex, $"the words end before block {block} of macroblock {macroblock}");
        }

        int dcWord = words[position++];
        var scale = dcWord >> 10;
        var dc = SignExtend10(dcWord);

        // Scale 0 bypasses the quantisation matrix and the zig-zag (section 1.7, step 4).
        target[0] = Clamp11(scale == 0 ? dc * 2 : dc * QuantByScan[0]);

        var k = 0;
        var finished = false;
        while (true)
        {
            if (position >= words.Length)
            {
                throw Error(frameIndex, $"the words end inside block {block} of macroblock {macroblock}, before its end-of-block word");
            }

            var word = words[position++];
            if (word == MdecBitstreamDecoder.EndOfBlockWord)
            {
                return position;
            }

            if (finished)
            {
                continue;
            }

            var level = SignExtend10(word);
            k += (word >> 10) + 1;
            if (k > 63)
            {
                if (level != 0)
                {
                    throw Error(frameIndex, $"a coefficient moves past position 63 in block {block} of macroblock {macroblock}");
                }

                finished = true;
                continue;
            }

            if (scale == 0)
            {
                target[k] = Clamp11(level * 2);
            }
            else
            {
                target[ZagZig[k]] = Clamp11((level * QuantByScan[k] * scale + 4) >> 3);
            }
        }
    }

    // Two passes of the integer transform of section 1.9. The first pass writes its result transposed, the second
    // transposes it back. A pass over a column of zeros gives (0 + 0x0FFF) >> 13 = 0, so zero columns are skipped.
    private static void Idct(Span<int> block, Span<int> temp)
    {
        var table = IdctTable;
        for (var y = 0; y < 8; y++)
        {
            var any = 0;
            for (var z = 0; z < 8; z++)
            {
                any |= block[y + z * 8];
            }

            for (var x = 0; x < 8; x++)
            {
                if (any == 0)
                {
                    temp[x + y * 8] = 0;
                    continue;
                }

                var sum = 0;
                for (var z = 0; z < 8; z++)
                {
                    sum += block[y + z * 8] * table[x + z * 8];
                }

                temp[x + y * 8] = (sum + 0x0FFF) >> 13;
            }
        }

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var sum = 0;
                for (var z = 0; z < 8; z++)
                {
                    sum += temp[y + z * 8] * table[x + z * 8];
                }

                block[x + y * 8] = (sum + 0x0FFF) >> 13;
            }
        }
    }

    private static void WriteMacroblock(ReadOnlySpan<int> blocks, int originX, int originY, int width, int height, Span<byte> rgba)
    {
        var cr = blocks.Slice(0, BlockSize);
        var cb = blocks.Slice(BlockSize, BlockSize);
        var yEnd = Math.Min(MacroblockSize, height - originY);
        var xEnd = Math.Min(MacroblockSize, width - originX);

        for (var py = 0; py < yEnd; py++)
        {
            var output = ((originY + py) * width + originX) * 4;
            for (var px = 0; px < xEnd; px++)
            {
                var chroma = (px >> 1) + (py >> 1) * 8;
                var lumaBlock = 2 + (px >> 3) + (py >> 3) * 2;
                var y = blocks[lumaBlock * BlockSize + (px & 7) + (py & 7) * 8];
                var crValue = cr[chroma];
                var cbValue = cb[chroma];

                var r = y + ((91881 * crValue + 32768) >> 16);
                var g = y + ((-22525 * cbValue - 46812 * crValue + 32768) >> 16);
                var b = y + ((116130 * cbValue + 32768) >> 16);

                rgba[output++] = (byte)(Clamp128(r) + 128);
                rgba[output++] = (byte)(Clamp128(g) + 128);
                rgba[output++] = (byte)(Clamp128(b) + 128);
                rgba[output++] = 255;
            }
        }
    }

    private static InvalidDataException Error(int frameIndex, string message)
    {
        return new InvalidDataException($"MDEC frame {frameIndex}: {message}.");
    }
}
