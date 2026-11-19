using CasaEngine.Framework.Video.Mdec;
using Xunit;

namespace CasaEngine.Tests.Video;

public class MdecBitstreamDecoderTests
{
    private const ushort Eob = 0xFE00;

    public static TheoryData<int> AllVersions => new() { 1, 2, 3 };

    private static ushort[] Decode(byte[] frame, int macroblocks, int frameIndex = 0)
    {
        var buffer = new ushort[MdecBitstreamDecoder.MaxWordCount(frame.Length)];
        var count = MdecBitstreamDecoder.Decode(frame, frameIndex, macroblocks, buffer);
        return buffer.AsSpan(0, count).ToArray();
    }

    private static MdecTestBlock Block(int dc, params MdecTestAc[] ac)
    {
        return new MdecTestBlock(dc, ac);
    }

    private static MdecTestBlock[] OneMacroblock(params MdecTestBlock[] blocks)
    {
        Assert.Equal(6, blocks.Length);
        return blocks;
    }

    // ---- hand-assembled streams (written from the tables of the specification, not from the encoder) ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Version1And2_HandAssembledMacroblock_GivesTheExpectedWords(int version)
    {
        // Cr: DC 1, AC 11 0 (+1), 11 1 (-1), end of block.
        // Cb: DC -1, AC 011 1 (-0401 -> 07FF), end of block.
        // Y1: DC 0, AC 010 1 0 (index 1 -> 0801), end of block.
        // Y2: DC 0, escape of 1005, end of block.
        // Y3: DC 0, escape of FE00 (ends the block, no end-of-block code).
        // Y4: DC 0, AC 00000000001 0000 0 (index 0 -> 0028), end of block.
        const string bits =
            "0000000001" + "110" + "111" + "10" +
            "1111111111" + "0111" + "10" +
            "0000000000" + "01010" + "10" +
            "0000000000" + "000001" + "0001000000000101" + "10" +
            "0000000000" + "000001" + "1111111000000000" +
            "0000000000" + "000000000010000" + "0" + "10" +
            "0111111111";
        var frame = MdecTestEncoder.FromBits(version, 3, bits);

        var words = Decode(frame, 1);

        Assert.Equal(
            new ushort[]
            {
                0x0C01, 0x0001, 0x03FF, Eob,
                0x0FFF, 0x07FF, Eob,
                0x0C00, 0x0801, Eob,
                0x0C00, 0x1005, Eob,
                0x0C00, Eob,
                0x0C00, 0x0028, Eob,
            },
            words);
    }

    [Fact]
    public void Version3_HandAssembledMacroblock_GivesTheExpectedWords()
    {
        // Cr 10sn s=1 n=1 -> +3 -> +12; Cb 01s s=0 -> -1 -> -4 (0x3FC);
        // Y1 100 -> 0; Y2 01sn s=0 n=0 -> -3 -> -12 (0x3F4); Y3 101snn s=1 nn=10 -> +6 -> +24 (12); Y4 00s s=1 -> +1 -> 16.
        // Y4 also holds the AC code 11 0 (+1) before its end of block.
        const string bits =
            "1011" + "10" +
            "010" + "10" +
            "100" + "10" +
            "0100" + "10" +
            "101110" + "10" +
            "001" + "110" + "10" +
            "1111111111";
        var frame = MdecTestEncoder.FromBits(3, 2, bits);

        var words = Decode(frame, 1);

        Assert.Equal(
            new ushort[]
            {
                0x080C, Eob,
                0x0BFC, Eob,
                0x0800, Eob,
                0x0BF4, Eob,
                0x080C, Eob,
                0x0810, 0x0001, Eob,
            },
            words);
    }

    [Fact]
    public void Version3_ClassSevenCodes_BothSigns_BothTables_GiveTheExpectedWords()
    {
        // Class 7 has 7 magnitude bits: positive = 128 + n, negative = n - 255. Scale 2 gives 0x0800 to every DC word.
        // Cr  11111110 s=1 n=0       -> +128 -> predictor 512 (0x200)
        // Cb  11111110 s=0 n=0       -> -255 -> -1020 -> predictor 4
        // Y1  1111110  s=1 n=1       -> +129 -> 516 (0x204)
        // Y2  1111110  s=0 n=127     -> -128 -> 516 - 512 = 4
        // Y3  1111110  s=1 n=127     -> +255 -> 4 + 1020 = 1024 -> 0
        // Y4  1111110  s=0 n=0       -> -255 -> -1020 -> 4
        const string bits =
            "11111110" + "1" + "0000000" + "10" +
            "11111110" + "0" + "0000000" + "10" +
            "1111110" + "1" + "0000001" + "10" +
            "1111110" + "0" + "1111111" + "10" +
            "1111110" + "1" + "1111111" + "10" +
            "1111110" + "0" + "0000000" + "10" +
            "1111111111";
        var frame = MdecTestEncoder.FromBits(3, 2, bits);

        var words = Decode(frame, 1);

        Assert.Equal(
            new ushort[]
            {
                0x0A00, Eob,
                0x0804, Eob,
                0x0A04, Eob,
                0x0804, Eob,
                0x0800, Eob,
                0x0804, Eob,
            },
            words);
    }

    // ---- encoder based cases ----

    [Theory]
    [MemberData(nameof(AllVersions))]
    public void DcOnly_GivesOneDcWordAndOneEndOfBlockPerBlock(int version)
    {
        var frame = MdecTestEncoder.Encode(
            version,
            5,
            OneMacroblock(Block(8), Block(-8), Block(4), Block(4), Block(12), Block(0)));

        var words = Decode(frame, 1);

        Assert.Equal(
            new ushort[] { 0x1408, Eob, 0x17F8, Eob, 0x1404, Eob, 0x1404, Eob, 0x140C, Eob, 0x1400, Eob },
            words);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Version1And2_DcRangeAndTheScaleBits(int version)
    {
        var frame = MdecTestEncoder.Encode(
            version,
            0x3F,
            OneMacroblock(Block(-512), Block(511), Block(-1), Block(0x1FF), Block(1), Block(0)));

        var words = Decode(frame, 1);

        // The scale sits in bits 15..10, the 10 raw bits of the DC in bits 9..0.
        Assert.Equal(
            new ushort[] { 0xFE00, Eob, 0xFDFF, Eob, 0xFFFF, Eob, 0xFDFF, Eob, 0xFC01, Eob, 0xFC00, Eob },
            words);
    }

    [Theory]
    [MemberData(nameof(AllVersions))]
    public void ShortAcCodes_GiveTheDocumentedWordsForBothSigns(int version)
    {
        var frame = MdecTestEncoder.Encode(
            version,
            1,
            OneMacroblock(
                Block(0, MdecTestAc.Of(0, 1), MdecTestAc.Of(0, -1), MdecTestAc.Of(1, 1), MdecTestAc.Of(1, -1)),
                Block(0, MdecTestAc.Of(0, 2), MdecTestAc.Of(2, -1), MdecTestAc.Of(4, 1)),
                Block(0, MdecTestAc.Of(0, 3), MdecTestAc.Of(0, -3)),
                Block(0),
                Block(0),
                Block(0)));

        var words = Decode(frame, 1);

        Assert.Equal(
            new ushort[]
            {
                0x0400, 0x0001, 0x03FF, 0x0401, 0x07FF, Eob,
                0x0400, 0x0002, 0x0BFF, 0x1001, Eob,
                0x0400, 0x0003, 0x03FD, Eob,
                0x0400, Eob, 0x0400, Eob, 0x0400, Eob,
            },
            words);
    }

    [Theory]
    [MemberData(nameof(AllVersions))]
    public void EveryCodeOfTheAcTable_RoundTripsForBothSigns(int version)
    {
        var blocks = new List<MdecTestBlock>();
        var expected = new List<ushort>();
        foreach (var (_, _, rowWords) in MdecTestEncoder.TableRows())
        {
            foreach (var word in rowWords)
            {
                foreach (var negative in new[] { false, true })
                {
                    var run = word >> 10;
                    var level = negative ? -(word & 0x3FF) : word & 0x3FF;
                    blocks.Add(Block(0, MdecTestAc.Of(run, level)));
                    expected.Add(0x0800);
                    expected.Add((ushort)((run << 10) | (level & 0x3FF)));
                    expected.Add(Eob);
                }
            }
        }

        // Pad to whole macroblocks with empty blocks.
        while (blocks.Count % 6 != 0)
        {
            blocks.Add(Block(0));
            expected.Add(0x0800);
            expected.Add(Eob);
        }

        var frame = MdecTestEncoder.Encode(version, 2, blocks);

        var words = Decode(frame, blocks.Count / 6);

        Assert.Equal(expected.ToArray(), words);
    }

    [Fact]
    public void TheAcTableIsPrefixFreeAndFillsTheCodeSpace()
    {
        // End of block (2 bits), escape (6 bits) and the unused 12 zero bits take their share of the code space.
        var space = Math.Pow(2, -2) + Math.Pow(2, -6) + Math.Pow(2, -12);
        foreach (var (prefix, indexBits, words) in MdecTestEncoder.TableRows())
        {
            Assert.Equal(1 << indexBits, words.Length);
            space += Math.Pow(2, -prefix.Length);
        }

        Assert.Equal(1.0, space, 12);
    }

    [Theory]
    [MemberData(nameof(AllVersions))]
    public void LongAcCodes_AndEscapes_PassTheWordOnUnchanged(int version)
    {
        var frame = MdecTestEncoder.Encode(
            version,
            0,
            OneMacroblock(
                // The longest table code (0412), a word that has a table code coded as an escape anyway (0001),
                // then words without a code: run 62 with level -512 (FA00) and run 1 with level 511 (05FF).
                Block(0, MdecTestAc.Of(1, 0x12), MdecTestAc.Escaped(0, 1), MdecTestAc.Of(62, -512), MdecTestAc.Of(1, 511)),
                // The version 1 peculiarity: an escape with a run and a level of 0 is passed on as is.
                Block(0, MdecTestAc.Escaped(5, 0), MdecTestAc.Of(0, 1)),
                Block(0, MdecTestAc.Escaped(63, 0)),
                // An escaped FE00 ends the block like the code 10 (no end-of-block code follows it).
                new MdecTestBlock(0, new[] { MdecTestAc.Of(0, 1), MdecTestAc.Raw(Eob) }, false),
                Block(0),
                Block(0)));

        var words = Decode(frame, 1);

        Assert.Equal(
            new ushort[]
            {
                0x0000, 0x0412, 0x0001, 0xFA00, 0x05FF, Eob,
                0x0000, 0x1400, 0x0001, Eob,
                0x0000, 0xFC00, Eob,
                0x0000, 0x0001, Eob,
                0x0000, Eob,
                0x0000, Eob,
            },
            words);
    }

    [Theory]
    [MemberData(nameof(AllVersions))]
    public void SeveralMacroblocks_EndOfFrameCodeAndTrailingBitsAreOptional(int version)
    {
        var macroblocks = new List<MdecTestBlock>();
        var expected = new List<ushort>();
        for (var m = 0; m < 3; m++)
        {
            for (var b = 0; b < 6; b++)
            {
                var dc = 4 * (m + b);
                macroblocks.Add(Block(dc, MdecTestAc.Of(b, m + 1)));
                expected.Add((ushort)(0x2400 | dc));
                expected.Add((ushort)((b << 10) | (m + 1)));
                expected.Add(Eob);
            }
        }

        foreach (var (endOfFrame, trailing) in new[] { (true, ""), (false, ""), (true, "1010101010101010101"), (false, "1111111111111111111111") })
        {
            var frame = MdecTestEncoder.Encode(version, 9, macroblocks, endOfFrame, trailing);

            Assert.Equal(expected.ToArray(), Decode(frame, 3));
        }
    }

    [Fact]
    public void Version3_EveryDifferenceFromMinus255To255_RoundTripsOnEachPredictor()
    {
        // One macroblock per difference. Cr and Y1 move by the difference, Cb by its opposite, Y2..Y4 stay on the shared predictor.
        var blocks = new List<MdecTestBlock>();
        var expected = new List<ushort>();
        int cr = 0, cb = 0, y = 0;
        for (var v = -255; v <= 255; v++)
        {
            cr = (cr + 4 * v) & 0x3FF;
            cb = (cb - 4 * v) & 0x3FF;
            y = (y + 4 * v) & 0x3FF;
            int[] dcs = { cr, cb, y, y, y, y };
            foreach (var dc in dcs)
            {
                blocks.Add(Block(dc));
                expected.Add((ushort)(0x1000 | dc));
                expected.Add(Eob);
            }
        }

        var frame = MdecTestEncoder.Encode(3, 4, blocks);

        Assert.Equal(expected.ToArray(), Decode(frame, 511));
    }

    [Fact]
    public void Version3_PredictorWrapsOn10Bits()
    {
        // 1020 then 0 is a difference of +4 modulo 1024; 0 then 1020 is -4.
        var blocks = new[]
        {
            Block(1020), Block(1020), Block(1020), Block(0), Block(0), Block(0),
            Block(0), Block(0), Block(1020), Block(1020), Block(1020), Block(1020),
        };
        var frame = MdecTestEncoder.Encode(3, 0, blocks);

        Assert.Equal(
            new ushort[]
            {
                0x03FC, Eob, 0x03FC, Eob, 0x03FC, Eob, 0x0000, Eob, 0x0000, Eob, 0x0000, Eob,
                0x0000, Eob, 0x0000, Eob, 0x03FC, Eob, 0x03FC, Eob, 0x03FC, Eob, 0x03FC, Eob,
            },
            Decode(frame, 2));
    }

    [Fact]
    public void Version1And2_EndOfFrameDcIsAnOrdinaryDcOutsideTheCrPosition()
    {
        var frame = MdecTestEncoder.Encode(2, 0, OneMacroblock(Block(0), Block(0x1FF), Block(0), Block(0), Block(0), Block(0)));

        Assert.Equal(0x01FF, Decode(frame, 1)[2]);
    }

    // ---- errors ----

    [Theory]
    [MemberData(nameof(AllVersions))]
    public void TruncatedStream_ThrowsAndNamesTheFrame(int version)
    {
        var blocks = new List<MdecTestBlock>();
        for (var b = 0; b < 12; b++)
        {
            blocks.Add(Block(4 * b, MdecTestAc.Of(0, 1), MdecTestAc.Of(3, -2), MdecTestAc.Escaped(1, 5)));
        }

        var frame = MdecTestEncoder.Encode(version, 3, blocks, endOfFrame: false);

        // The padding is less than one halfword: cutting two halfwords or more always removes coded bits.
        for (var length = MdecBitstreamDecoder.HeaderSize; length <= frame.Length - 4; length += 2)
        {
            var cut = frame.AsSpan(0, length).ToArray();
            var ex = Assert.Throws<InvalidDataException>(() => Decode(cut, 2, 42));
            Assert.Contains("frame 42", ex.Message);
        }
    }

    [Fact]
    public void AFrameCutRightAfterADc_ReportsATruncation_NotAnInvalidCode()
    {
        // The DC fills ten bits of the only halfword: the six bits left are padding, too few for any AC code.
        var frame = MdecTestEncoder.FromBits(2, 1, "0000000000");

        var ex = Assert.Throws<InvalidDataException>(() => Decode(frame, 1, 5));

        Assert.Contains("frame 5", ex.Message);
        Assert.Contains("truncated", ex.Message);
        Assert.DoesNotContain("invalid AC code", ex.Message);
    }

    [Fact]
    public void FrameShorterThanItsHeader_ThrowsAndNamesTheFrame()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Decode(new byte[5], 1, 7));

        Assert.Contains("frame 7", ex.Message);
    }

    [Fact]
    public void InvalidAcCode_ThrowsAndNamesTheFrame()
    {
        var frame = MdecTestEncoder.FromBits(2, 1, "0000000000" + "000000000000" + "1111");

        var ex = Assert.Throws<InvalidDataException>(() => Decode(frame, 1, 9));

        Assert.Contains("frame 9", ex.Message);
        Assert.Contains("invalid AC code", ex.Message);
    }

    [Theory]
    [InlineData("111111110")]
    [InlineData("1111111110")]
    [InlineData("1111111111")]
    public void Version3_UnusedAndMisplacedChromaCodes_Throw(string cbCode)
    {
        // Cr: zero difference + end of block, then the bad code in the Cb position.
        var frame = MdecTestEncoder.FromBits(3, 0, "00" + "10" + cbCode + "0000000000");

        var ex = Assert.Throws<InvalidDataException>(() => Decode(frame, 1, 3));

        Assert.Contains("frame 3", ex.Message);
    }

    [Theory]
    [InlineData("11111110")]
    [InlineData("111111110")]
    [InlineData("1111111110")]
    [InlineData("1111111111")]
    public void Version3_UnusedAndMisplacedLumaCodes_Throw(string lumaCode)
    {
        var frame = MdecTestEncoder.FromBits(3, 0, "00" + "10" + "00" + "10" + lumaCode + "0000000000");

        var ex = Assert.Throws<InvalidDataException>(() => Decode(frame, 1, 3));

        Assert.Contains("frame 3", ex.Message);
    }

    [Theory]
    [MemberData(nameof(AllVersions))]
    public void EndOfFrameBeforeTheLastMacroblock_ThrowsWithTheCount(int version)
    {
        var frame = MdecTestEncoder.Encode(version, 1, OneMacroblock(Block(0), Block(0), Block(0), Block(0), Block(0), Block(0)));

        var ex = Assert.Throws<InvalidDataException>(() => Decode(frame, 3, 5));

        Assert.Contains("frame 5", ex.Message);
        Assert.Contains("after 1 of 3 macroblocks", ex.Message);
    }

    [Fact]
    public void InvalidHeaders_Throw()
    {
        var good = MdecTestEncoder.Encode(2, 1, OneMacroblock(Block(0), Block(0), Block(0), Block(0), Block(0), Block(0)));

        var badIdentifier = (byte[])good.Clone();
        badIdentifier[3] = 0x39;
        var badScale = (byte[])good.Clone();
        badScale[4] = 0x40;
        var versionZero = (byte[])good.Clone();
        versionZero[6] = 0;
        var versionFour = (byte[])good.Clone();
        versionFour[6] = 4;

        foreach (var frame in new[] { badIdentifier, badScale, versionZero, versionFour })
        {
            var ex = Assert.Throws<InvalidDataException>(() => Decode(frame, 1, 11));
            Assert.Contains("frame 11", ex.Message);
        }
    }

    [Fact]
    public void OutputBufferTooSmall_Throws()
    {
        var frame = MdecTestEncoder.Encode(2, 1, OneMacroblock(Block(0), Block(0), Block(0), Block(0), Block(0), Block(0)));

        var ex = Assert.Throws<InvalidDataException>(() => MdecBitstreamDecoder.Decode(frame, 4, 1, new ushort[5]));

        Assert.Contains("frame 4", ex.Message);
    }

    [Fact]
    public void MaxWordCount_IsEnoughForAStreamOfOnlyShortCodes()
    {
        // Every two bits can carry one end-of-block word: a stream of "10" repeated.
        var frame = MdecTestEncoder.FromBits(2, 0, string.Concat(Enumerable.Repeat("10", 64)));

        Assert.True(MdecBitstreamDecoder.MaxWordCount(frame.Length) >= 64);
    }

    // ---- allocations ----

    [Theory]
    [MemberData(nameof(AllVersions))]
    public void Decoding_AllocatesNothingPerFrame(int version)
    {
        var blocks = new List<MdecTestBlock>();
        for (var m = 0; m < 20; m++)
        {
            for (var b = 0; b < 6; b++)
            {
                blocks.Add(Block(4 * ((m + b) % 8), MdecTestAc.Of(0, 1), MdecTestAc.Of(2, -3), MdecTestAc.Escaped(7, 100), MdecTestAc.Of(1, 0x12)));
            }
        }

        var frame = MdecTestEncoder.Encode(version, 6, blocks);
        var buffer = new ushort[MdecBitstreamDecoder.MaxWordCount(frame.Length)];
        var expected = MdecBitstreamDecoder.Decode(frame, 0, 20, buffer);
        Assert.Equal(20 * 6 * 6, expected);

        var before = AllocationWindow.Start();
        var total = 0;
        for (var i = 0; i < 200; i++)
        {
            total += MdecBitstreamDecoder.Decode(frame, i, 20, buffer);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(200 * expected, total);
        Assert.Equal(0, allocated);
    }
}
