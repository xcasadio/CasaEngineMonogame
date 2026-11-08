using System.Text;

namespace CasaEngine.Tests.Video;

/// <summary>One AC entry of a test block: a run-length word given as run and level, coded by the table or as an escape.</summary>
internal readonly record struct MdecTestAc(ushort Word, bool ForceEscape)
{
    /// <summary>A coefficient: <paramref name="run"/> zeros then <paramref name="level"/> (-512..511, not 0), coded by the table when it has a code, else escaped.</summary>
    public static MdecTestAc Of(int run, int level)
    {
        return new MdecTestAc(MakeWord(run, level), false);
    }

    /// <summary>The same word, always coded as an escape (000001 + 16 bits).</summary>
    public static MdecTestAc Escaped(int run, int level)
    {
        return new MdecTestAc(MakeWord(run, level), true);
    }

    /// <summary>An arbitrary 16-bit word coded as an escape.</summary>
    public static MdecTestAc Raw(ushort word)
    {
        return new MdecTestAc(word, true);
    }

    private static ushort MakeWord(int run, int level)
    {
        return (ushort)((run << 10) | (level & 0x3FF));
    }
}

/// <summary>One block of a test frame: its absolute DC value (-512..511) and its AC entries.</summary>
internal sealed record MdecTestBlock(int Dc, MdecTestAc[] Ac, bool EndOfBlock)
{
    public MdecTestBlock(int dc, params MdecTestAc[] ac) : this(dc, ac, true)
    {
    }
}

/// <summary>
/// Minimal BS encoder for tests (plan choice C8), written anew from the codes of docs/engine/video.md sections 1.4
/// and 1.5. It builds BS frames of versions 1, 2 and 3 from known coefficients. It is not an image encoder: the caller
/// gives the DC and the run-length entries of every block, six blocks per macroblock in the order Cr, Cb, Y1..Y4.
/// </summary>
internal static class MdecTestEncoder
{
    private sealed record Row(string Prefix, int IndexBits, ushort[] Words);

    // The AC table of section 1.5, as it is written in the document (prefix, index bits, words by index).
    private static readonly Row[] AcRows =
    {
        new("11", 0, new ushort[] { 0x0001 }),
        new("011", 0, new ushort[] { 0x0401 }),
        new("010", 1, new ushort[] { 0x0002, 0x0801 }),
        new("0011", 1, new ushort[] { 0x1001, 0x0C01 }),
        new("00101", 0, new ushort[] { 0x0003 }),
        new("00100", 3, new ushort[] { 0x3401, 0x0006, 0x3001, 0x2C01, 0x0C02, 0x0403, 0x0005, 0x2801 }),
        new("0001", 2, new ushort[] { 0x1C01, 0x1801, 0x0402, 0x1401 }),
        new("00001", 2, new ushort[] { 0x0802, 0x2401, 0x0004, 0x2001 }),
        new("0000001", 3, new ushort[] { 0x4001, 0x1402, 0x0007, 0x0803, 0x0404, 0x3C01, 0x3801, 0x1002 }),
        new("00000001", 4, new ushort[] { 0x000B, 0x2002, 0x1003, 0x000A, 0x0804, 0x1C02, 0x5401, 0x5001, 0x0009, 0x4C01, 0x4801, 0x0405, 0x0C03, 0x0008, 0x1802, 0x4401 }),
        new("000000001", 4, new ushort[] { 0x2802, 0x2402, 0x1403, 0x0C04, 0x0805, 0x0407, 0x0406, 0x000F, 0x000E, 0x000D, 0x000C, 0x6801, 0x6401, 0x6001, 0x5C01, 0x5801 }),
        new("0000000001", 4, new ushort[] { 0x001F, 0x001E, 0x001D, 0x001C, 0x001B, 0x001A, 0x0019, 0x0018, 0x0017, 0x0016, 0x0015, 0x0014, 0x0013, 0x0012, 0x0011, 0x0010 }),
        new("00000000001", 4, new ushort[] { 0x0028, 0x0027, 0x0026, 0x0025, 0x0024, 0x0023, 0x0022, 0x0021, 0x0020, 0x040E, 0x040D, 0x040C, 0x040B, 0x040A, 0x0409, 0x0408 }),
        new("000000000001", 4, new ushort[] { 0x0412, 0x0411, 0x0410, 0x040F, 0x1803, 0x4002, 0x3C02, 0x3802, 0x3402, 0x3002, 0x2C02, 0x7C01, 0x7801, 0x7401, 0x7001, 0x6C01 }),
    };

    private const string EndOfBlockCode = "10";
    private const string EscapePrefix = "000001";

    // Version 3 DC prefixes by magnitude class k (k magnitude bits), from the tables of section 1.4.
    private static readonly string[] ChromaClassPrefix = { "01", "10", "110", "1110", "11110", "111110", "1111110", "11111110" };
    private static readonly string[] LumaClassPrefix = { "00", "01", "101", "110", "1110", "11110", "111110", "1111110" };

    /// <summary>Every (prefix, index bits, word) row of the AC table, for tests that check the table itself.</summary>
    public static IEnumerable<(string Prefix, int IndexBits, ushort[] Words)> TableRows()
    {
        foreach (var row in AcRows)
        {
            yield return (row.Prefix, row.IndexBits, row.Words);
        }
    }

    /// <summary>Builds a BS frame. <paramref name="blocks"/> holds six blocks per macroblock.</summary>
    public static byte[] Encode(
        int version, int quantScale, IReadOnlyList<MdecTestBlock> blocks, bool endOfFrame = true, string trailingBits = "")
    {
        var bits = new StringBuilder();
        var predictors = new int[3];

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var component = i % 6;
            if (version == 3)
            {
                WriteDcVersion3(bits, block.Dc, component, predictors);
            }
            else
            {
                AppendNumber(bits, block.Dc & 0x3FF, 10);
            }

            foreach (var ac in block.Ac)
            {
                WriteAc(bits, ac);
            }

            if (block.EndOfBlock)
            {
                bits.Append(EndOfBlockCode);
            }
        }

        if (endOfFrame)
        {
            bits.Append(version == 3 ? "1111111111" : "0111111111");
        }

        bits.Append(trailingBits);
        return FromBits(version, quantScale, bits.ToString());
    }

    /// <summary>Builds a BS frame from a string of '0' and '1' (spaces are ignored), padded with zeros to a halfword.</summary>
    public static byte[] FromBits(int version, int quantScale, string bits)
    {
        var clean = bits.Replace(" ", string.Empty);
        var halfwords = (clean.Length + 15) / 16;
        var frame = new byte[8 + halfwords * 2];

        // Size of the expanded data in 32-bit words, rounded up to a multiple of 0x20 (not interpreted by the decoder).
        var words = (halfwords + 1) / 2;
        WriteU16(frame, 0, (words + 0x1F) / 0x20 * 0x20);
        WriteU16(frame, 2, 0x3800);
        WriteU16(frame, 4, quantScale);
        WriteU16(frame, 6, version);

        for (var h = 0; h < halfwords; h++)
        {
            var value = 0;
            for (var b = 0; b < 16; b++)
            {
                var index = h * 16 + b;
                value <<= 1;
                if (index < clean.Length && clean[index] == '1')
                {
                    value |= 1;
                }
            }

            WriteU16(frame, 8 + h * 2, value);
        }

        return frame;
    }

    private static void WriteU16(byte[] data, int offset, int value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
    }

    private static void AppendNumber(StringBuilder bits, int value, int width)
    {
        for (var b = width - 1; b >= 0; b--)
        {
            bits.Append(((value >> b) & 1) != 0 ? '1' : '0');
        }
    }

    private static void WriteAc(StringBuilder bits, MdecTestAc ac)
    {
        if (!ac.ForceEscape)
        {
            var level = ac.Word & 0x3FF;
            var negative = level >= 0x200;
            var magnitude = negative ? (0x400 - level) & 0x3FF : level;
            var positiveWord = (ac.Word & 0xFC00) | magnitude;
            foreach (var row in AcRows)
            {
                var index = Array.IndexOf(row.Words, (ushort)positiveWord);
                if (index < 0)
                {
                    continue;
                }

                bits.Append(row.Prefix);
                if (row.IndexBits > 0)
                {
                    AppendNumber(bits, index, row.IndexBits);
                }

                bits.Append(negative ? '1' : '0');
                return;
            }
        }

        bits.Append(EscapePrefix);
        AppendNumber(bits, ac.Word, 16);
    }

    private static void WriteDcVersion3(StringBuilder bits, int dc, int component, int[] predictors)
    {
        var predictorIndex = component < 2 ? component : 2;
        var predictor = predictors[predictorIndex];
        var difference = (((dc - predictor + 512) & 0x3FF) - 512);
        if (difference % 4 != 0)
        {
            throw new ArgumentException($"Version 3 DC {dc} is not a multiple of 4 away from the predictor {predictor}.");
        }

        var value = difference / 4;
        predictors[predictorIndex] = (predictor + difference) & 0x3FF;
        var isChroma = component < 2;

        if (value == 0)
        {
            bits.Append(isChroma ? "00" : "100");
            return;
        }

        var magnitude = Math.Abs(value);
        var k = 0;
        while ((2 << k) <= magnitude)
        {
            k++;
        }

        bits.Append(isChroma ? ChromaClassPrefix[k] : LumaClassPrefix[k]);
        if (value > 0)
        {
            bits.Append('1');
            AppendNumber(bits, value - (1 << k), k);
        }
        else
        {
            bits.Append('0');
            AppendNumber(bits, value + (2 << k) - 1, k);
        }
    }
}
