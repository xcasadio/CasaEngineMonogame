using CasaEngine.Framework.Video.Mdec;
using Xunit;

namespace CasaEngine.Tests.Video;

/// <summary>
/// Tests of the second MDEC stage (docs/engine/video.md sections 1.7 to 1.11). The expected pictures come from a
/// reference implementation written here in two-dimensional index form straight from the specification, from
/// hand-computed cases, and from a double-precision inverse transform.
/// </summary>
public class MdecFrameDecoderTests
{
    private const ushort Eob = 0xFE00;

    private static readonly int[] QuantRaster =
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

    private static readonly int[] ZagZig =
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

    private static readonly int[] ScaleTable =
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

    private readonly record struct Ac(int Run, int Level);

    private static ushort Word(int run, int level) => (ushort)((run << 10) | (level & 0x3FF));

    private static void AddBlock(List<ushort> words, int scale, int dc, params Ac[] ac)
    {
        words.Add((ushort)((scale << 10) | (dc & 0x3FF)));
        foreach (var a in ac)
        {
            words.Add(Word(a.Run, a.Level));
        }

        words.Add(Eob);
    }

    private static void AddFlatMacroblock(List<ushort> words, int scale, int cr, int cb, int y1, int y2, int y3, int y4)
    {
        AddBlock(words, scale, cr);
        AddBlock(words, scale, cb);
        AddBlock(words, scale, y1);
        AddBlock(words, scale, y2);
        AddBlock(words, scale, y3);
        AddBlock(words, scale, y4);
    }

    private static byte[] Decode(List<ushort> words, int width, int height, int frameIndex = 0)
    {
        var rgba = new byte[width * height * 4];
        MdecFrameDecoder.Decode(words.ToArray(), frameIndex, width, height, rgba);
        return rgba;
    }

    // ---- reference implementation, written from the specification ----

    private static int S10(int value) => ((value & 0x3FF) ^ 0x200) - 0x200;

    private static int Sat(int value) => Math.Clamp(value, -0x400, 0x3FF);

    private static int[,] ReferenceBlock(List<ushort> words, ref int position)
    {
        var raster = new int[8, 8];
        int dcWord = words[position++];
        var scale = dcWord >> 10;
        var dc = S10(dcWord);
        raster[0, 0] = Sat(scale == 0 ? dc * 2 : dc * QuantRaster[0]);
        var k = 0;
        while (words[position] != Eob)
        {
            var word = words[position++];
            k += (word >> 10) + 1;
            var level = S10(word);
            var cell = scale == 0 ? k : ZagZig[k];
            var value = scale == 0 ? level * 2 : ((level * QuantRaster[ZagZig[k]] * scale + 4) >> 3);
            raster[cell / 8, cell % 8] = Sat(value);
        }

        position++;
        return raster;
    }

    private static int[,] ReferenceIdct(int[,] coefficients)
    {
        var t = new int[8, 8];
        for (var r = 0; r < 8; r++)
        {
            for (var i = 0; i < 8; i++)
            {
                t[r, i] = (short)ScaleTable[r * 8 + i] >> 3;
            }
        }

        // First pass: for every column c of the coefficients, transform along the rows.
        var temp = new int[8, 8];
        for (var c = 0; c < 8; c++)
        {
            for (var i = 0; i < 8; i++)
            {
                var sum = 0;
                for (var r = 0; r < 8; r++)
                {
                    sum += coefficients[r, c] * t[r, i];
                }

                temp[c, i] = (sum + 0x0FFF) >> 13;
            }
        }

        var output = new int[8, 8];
        for (var i = 0; i < 8; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                var sum = 0;
                for (var c = 0; c < 8; c++)
                {
                    sum += temp[c, i] * t[c, j];
                }

                output[i, j] = (sum + 0x0FFF) >> 13;
            }
        }

        return output;
    }

    private static double[,] DoubleIdct(int[,] coefficients)
    {
        var output = new double[8, 8];
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var sum = 0.0;
                for (var v = 0; v < 8; v++)
                {
                    for (var u = 0; u < 8; u++)
                    {
                        var cu = u == 0 ? Math.Sqrt(0.5) : 1.0;
                        var cv = v == 0 ? Math.Sqrt(0.5) : 1.0;
                        sum += cu * cv * coefficients[v, u]
                               * Math.Cos((2 * x + 1) * u * Math.PI / 16) * Math.Cos((2 * y + 1) * v * Math.PI / 16);
                    }
                }

                output[y, x] = sum / 4;
            }
        }

        return output;
    }

    private static int Clamp128(int value) => Math.Clamp(value, -128, 127);

    private static byte[] Reference(List<ushort> words, int width, int height)
    {
        var rows = (height + 15) / 16;
        var columns = (width + 15) / 16;
        var rgba = new byte[width * height * 4];
        var position = 0;

        for (var mb = 0; mb < rows * columns; mb++)
        {
            var blocks = new int[6][,];
            for (var b = 0; b < 6; b++)
            {
                blocks[b] = ReferenceIdct(ReferenceBlock(words, ref position));
            }

            var originX = (mb / rows) * 16;
            var originY = (mb % rows) * 16;
            for (var py = 0; py < 16; py++)
            {
                for (var px = 0; px < 16; px++)
                {
                    var x = originX + px;
                    var y = originY + py;
                    if (x >= width || y >= height)
                    {
                        continue;
                    }

                    var luma = blocks[2 + (py / 8) * 2 + px / 8][py % 8, px % 8];
                    var cr = blocks[0][py / 2, px / 2];
                    var cb = blocks[1][py / 2, px / 2];
                    var r = luma + ((91881 * cr + 32768) >> 16);
                    var g = luma + ((-22525 * cb - 46812 * cr + 32768) >> 16);
                    var b = luma + ((116130 * cb + 32768) >> 16);
                    var o = (y * width + x) * 4;
                    rgba[o] = (byte)(Clamp128(r) + 128);
                    rgba[o + 1] = (byte)(Clamp128(g) + 128);
                    rgba[o + 2] = (byte)(Clamp128(b) + 128);
                    rgba[o + 3] = 255;
                }
            }
        }

        return rgba;
    }

    private static List<ushort> RandomFrame(Random random, int macroblocks, int scaleMin)
    {
        var words = new List<ushort>();
        for (var i = 0; i < macroblocks * 6; i++)
        {
            var acs = new List<Ac>();
            var k = 0;
            while (random.Next(4) != 0)
            {
                var run = random.Next(0, 6);
                if (k + run + 1 > 63)
                {
                    break;
                }

                k += run + 1;
                acs.Add(new Ac(run, random.Next(-60, 61)));
            }

            AddBlock(words, random.Next(scaleMin, 64), random.Next(-200, 200), acs.ToArray());
        }

        return words;
    }

    // ---- DC only: flat blocks ----

    [Fact]
    public void DcOnlyLuma_GivesAFlatPictureOfTheHandComputedColour()
    {
        // Scale 1, Y DC 100: coefficient 100 * qt[0] = 200. First pass (200 * 2896 + 4095) >> 13 = 71,
        // second pass (71 * 2896 + 4095) >> 13 = 25. Chroma 0, so every channel is 25 + 128 = 153.
        var words = new List<ushort>();
        AddFlatMacroblock(words, 1, 0, 0, 100, 100, 100, 100);

        var rgba = Decode(words, 16, 16);

        for (var i = 0; i < rgba.Length; i += 4)
        {
            Assert.Equal(153, rgba[i]);
            Assert.Equal(153, rgba[i + 1]);
            Assert.Equal(153, rgba[i + 2]);
            Assert.Equal(255, rgba[i + 3]);
        }
    }

    [Fact]
    public void DcOnlyChroma_GivesTheFixedPointColour()
    {
        // Cr DC 64 and Cb DC -32 at scale 1: coefficients 128 and -64.
        // Cr: (128 * 2896 + 4095) >> 13 = 45, (45 * 2896 + 4095) >> 13 = 16.
        // Cb: (-64 * 2896 + 4095) >> 13 = -23 (floor of -22.2), (-23 * 2896 + 4095) >> 13 = -8 (floor of -7.6).
        // Y = 0: R = (91881 * 16 + 32768) >> 16 = 22, G = (-22525 * -8 - 46812 * 16 + 32768) >> 16 = -9,
        // B = (116130 * -8 + 32768) >> 16 = -14.
        var words = new List<ushort>();
        AddFlatMacroblock(words, 1, 64, -32, 0, 0, 0, 0);

        var rgba = Decode(words, 16, 16);

        Assert.Equal(128 + 22, rgba[0]);
        Assert.Equal(128 - 9, rgba[1]);
        Assert.Equal(128 - 14, rgba[2]);
        Assert.Equal(rgba[0], rgba[(15 * 16 + 15) * 4]);
    }

    [Fact]
    public void DcOnly_MatchesTheDoublePrecisionTransformWithinOne()
    {
        // A flat block of DC d is d / 8 for every sample.
        foreach (var dc in new[] { -200, -37, -1, 0, 1, 5, 64, 200 })
        {
            var words = new List<ushort>();
            AddFlatMacroblock(words, 4, 0, 0, dc, dc, dc, dc);
            var rgba = Decode(words, 16, 16);

            // scale 4 does not scale the DC: coefficient = dc * 2, flat output = coefficient / 8.
            var expected = Math.Clamp((int)Math.Round(dc * 2 / 8.0), -128, 127) + 128;
            Assert.InRange(rgba[0], expected - 1, expected + 1);
            Assert.Equal(rgba[0], rgba[1]);
        }
    }

    // ---- one AC coefficient per position ----

    [Theory]
    [InlineData(1, 30)]
    [InlineData(5, -45)]
    [InlineData(63, 50)]
    public void SingleAcCoefficient_MatchesTheReferenceAtEveryPosition(int scale, int level)
    {
        for (var k = 1; k <= 63; k++)
        {
            var words = new List<ushort>();
            AddBlock(words, scale, 0);
            AddBlock(words, scale, 0);
            AddBlock(words, scale, 20, new Ac(k - 1, level));
            AddBlock(words, scale, 20);
            AddBlock(words, scale, 20);
            AddBlock(words, scale, 20);

            Assert.Equal(Reference(words, 16, 16), Decode(words, 16, 16));
        }
    }

    [Fact]
    public void SingleAcCoefficient_FollowsTheDoublePrecisionTransform()
    {
        // Scale 8: dequantised value = level * qt[k] (= (level * qt * 8 + 4) >> 3 for level * qt * 8 divisible by 8).
        for (var k = 1; k <= 63; k++)
        {
            var words = new List<ushort>();
            AddBlock(words, 8, 0);
            AddBlock(words, 8, 0);
            AddBlock(words, 8, 0, new Ac(k - 1, 20));
            AddBlock(words, 8, 0);
            AddBlock(words, 8, 0);
            AddBlock(words, 8, 0);

            var cell = ZagZig[k];
            var coefficients = new int[8, 8];
            coefficients[cell / 8, cell % 8] = Sat(20 * QuantRaster[cell]);
            var exact = DoubleIdct(coefficients);

            var integer = ReferenceIdct(coefficients);
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    Assert.InRange(integer[y, x], exact[y, x] - 2.5, exact[y, x] + 2.5);
                }
            }

            // The decoder luma at (x, y) of Y1 is the integer transform (chroma 0: grey = luma).
            var rgba = Decode(words, 16, 16);
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    Assert.Equal(Clamp128(integer[y, x]) + 128, rgba[(y * 16 + x) * 4]);
                }
            }
        }
    }

    // ---- a full macroblock and random content ----

    [Fact]
    public void FullMacroblock_MatchesTheReference()
    {
        var random = new Random(12345);
        for (var round = 0; round < 40; round++)
        {
            var words = RandomFrame(random, 1, 1);
            Assert.Equal(Reference(words, 16, 16), Decode(words, 16, 16));
        }
    }

    [Fact]
    public void RunsThatReachTheLastPosition_Work()
    {
        var words = new List<ushort>();
        AddBlock(words, 3, 10, new Ac(62, 25));
        AddBlock(words, 3, 10, new Ac(0, 9), new Ac(61, -9));
        AddBlock(words, 3, 10);
        AddBlock(words, 3, 10);
        AddBlock(words, 3, 10);
        AddBlock(words, 3, 10);

        Assert.Equal(Reference(words, 16, 16), Decode(words, 16, 16));
    }

    [Fact]
    public void LevelZeroWords_OnlyMoveThePosition()
    {
        // A word with level 0 and run 3 skips 4 positions; the next coefficient then lands at position 5.
        var withSkip = new List<ushort>();
        AddBlock(withSkip, 2, 0);
        AddBlock(withSkip, 2, 0);
        AddBlock(withSkip, 2, 5, new Ac(3, 0), new Ac(0, 30));
        AddBlock(withSkip, 2, 5);
        AddBlock(withSkip, 2, 5);
        AddBlock(withSkip, 2, 5);

        var direct = new List<ushort>();
        AddBlock(direct, 2, 0);
        AddBlock(direct, 2, 0);
        AddBlock(direct, 2, 5, new Ac(4, 30));
        AddBlock(direct, 2, 5);
        AddBlock(direct, 2, 5);
        AddBlock(direct, 2, 5);

        Assert.Equal(Decode(direct, 16, 16), Decode(withSkip, 16, 16));
    }

    // ---- scale 0 ----

    [Fact]
    public void ScaleZero_UsesTwiceTheLevelAtTheScanIndexWithoutZigZag()
    {
        var words = new List<ushort>();
        AddBlock(words, 0, 0);
        AddBlock(words, 0, 0);
        AddBlock(words, 0, 30, new Ac(1, 40), new Ac(0, -20));
        AddBlock(words, 0, 30);
        AddBlock(words, 0, 30);
        AddBlock(words, 0, 30);

        Assert.Equal(Reference(words, 16, 16), Decode(words, 16, 16));

        // Without zig-zag, scan index 2 is raster cell 2 (row 0, column 2) and scan index 3 is cell 3, so Y1 has no
        // vertical frequency: its rows are all equal.
        var rgba = Decode(words, 16, 16);
        for (var y = 1; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                Assert.Equal(rgba[x * 4], rgba[(y * 16 + x) * 4]);
            }
        }
    }

    [Fact]
    public void ScaleZero_IgnoresTheQuantisationMatrix()
    {
        // Coefficient 2 * 100 = 200 at the DC: flat 25 as in the hand-computed case, whatever the matrix says.
        var words = new List<ushort>();
        AddFlatMacroblock(words, 0, 0, 0, 100, 100, 100, 100);

        var rgba = Decode(words, 16, 16);

        Assert.Equal(153, rgba[0]);
        Assert.Equal(153, rgba[(16 * 16 - 1) * 4]);
    }

    // ---- saturation ----

    [Fact]
    public void Saturation_ClampsCoefficientsAndChannels()
    {
        // Coefficients: the largest AC (level +511, scale 63) clamps to +1023 and the smallest DC (-512 * 2) is
        // exactly -1024.
        var words = new List<ushort>();
        AddBlock(words, 63, 0);
        AddBlock(words, 63, 0);
        AddBlock(words, 63, -512, new Ac(0, 511), new Ac(0, -512));
        AddBlock(words, 63, 511);
        AddBlock(words, 63, -512);
        AddBlock(words, 63, 0);

        Assert.Equal(Reference(words, 16, 16), Decode(words, 16, 16));

        var rgba = Decode(words, 16, 16);
        // Y2 is a flat 1022 / 8 = ~127: white; Y3 is a flat -1024 / 8 = -128: black.
        Assert.Equal(255, rgba[(0 * 16 + 8) * 4]);
        Assert.Equal(0, rgba[(8 * 16 + 0) * 4]);
        Assert.Equal(0, rgba[(8 * 16 + 0) * 4 + 1]);
        Assert.Equal(0, rgba[(8 * 16 + 0) * 4 + 2]);
    }

    [Fact]
    public void Saturation_OfTheChannelsIsPerChannel()
    {
        // Strong red chroma (Cr high) on a mid grey: red saturates, blue stays.
        var words = new List<ushort>();
        AddFlatMacroblock(words, 1, 500, 0, 40, 40, 40, 40);

        var rgba = Decode(words, 16, 16);

        Assert.Equal(255, rgba[0]);
        Assert.True(rgba[2] < 255);
        Assert.Equal(Reference(words, 16, 16), rgba);
    }

    // ---- sizes ----

    [Theory]
    [InlineData(20, 18)]
    [InlineData(33, 16)]
    [InlineData(17, 49)]
    [InlineData(1, 1)]
    [InlineData(48, 32)]
    public void SizeNotMultipleOf16_IsCroppedFromTheTopLeftAndMatchesTheReference(int width, int height)
    {
        var random = new Random(width * 1000 + height);
        var words = RandomFrame(random, MdecFrameDecoder.MacroblockCount(width, height), 1);

        var rgba = Decode(words, width, height);

        Assert.Equal(Reference(words, width, height), rgba);
    }

    [Fact]
    public void Macroblocks_AreStoredColumnByColumn()
    {
        // 32 x 32 = 2 columns x 2 rows. Stream order: (col 0,row 0), (col 0,row 1), (col 1,row 0), (col 1,row 1).
        var words = new List<ushort>();
        AddFlatMacroblock(words, 1, 0, 0, 20, 20, 20, 20);
        AddFlatMacroblock(words, 1, 0, 0, 60, 60, 60, 60);
        AddFlatMacroblock(words, 1, 0, 0, 100, 100, 100, 100);
        AddFlatMacroblock(words, 1, 0, 0, 140, 140, 140, 140);

        var rgba = Decode(words, 32, 32);

        var topLeft = rgba[0];
        var bottomLeft = rgba[(16 * 32) * 4];
        var topRight = rgba[16 * 4];
        var bottomRight = rgba[(16 * 32 + 16) * 4];
        Assert.True(topLeft < bottomLeft);
        Assert.True(bottomLeft < topRight);
        Assert.True(topRight < bottomRight);
    }

    [Fact]
    public void ChromaCoversTwoByTwoPixels_AndLumaBlocksCoverTheirQuadrant()
    {
        var words = new List<ushort>();
        AddBlock(words, 1, 0, new Ac(0, 40));
        AddBlock(words, 1, 0);
        AddBlock(words, 1, 10);
        AddBlock(words, 1, 30);
        AddBlock(words, 1, 50);
        AddBlock(words, 1, 70);

        var rgba = Decode(words, 16, 16);

        // Pairs of pixels share chroma; with a luma that is flat per quadrant, the difference between the two pixels of
        // a pair comes from nothing, and the red channel only changes between pairs.
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x += 2)
            {
                Assert.Equal(rgba[(y * 16 + x) * 4], rgba[(y * 16 + x + 1) * 4]);
            }
        }

        Assert.NotEqual(rgba[(0 * 16 + 0) * 4 + 1], rgba[(0 * 16 + 8) * 4 + 1]);
        Assert.NotEqual(rgba[(0 * 16 + 8) * 4 + 1], rgba[(8 * 16 + 0) * 4 + 1]);
        Assert.NotEqual(rgba[(8 * 16 + 0) * 4 + 1], rgba[(8 * 16 + 8) * 4 + 1]);
    }

    // ---- errors ----

    [Fact]
    public void WordsThatEndBeforeTheLastBlock_ThrowAndNameTheFrame()
    {
        var words = new List<ushort>();
        AddFlatMacroblock(words, 1, 0, 0, 0, 0, 0, 0);
        words.RemoveRange(words.Count - 3, 3);

        var ex = Assert.Throws<InvalidDataException>(() => Decode(words, 16, 16, frameIndex: 42));
        Assert.Contains("frame 42", ex.Message);
    }

    [Fact]
    public void ABlockWithoutEndOfBlock_ThrowsAndNamesTheFrame()
    {
        var words = new List<ushort> { 0x0400, 0x0401 };

        var ex = Assert.Throws<InvalidDataException>(() => Decode(words, 16, 16, frameIndex: 7));
        Assert.Contains("frame 7", ex.Message);
    }

    [Fact]
    public void ACoefficientPastPosition63_ThrowsAndNamesTheFrame()
    {
        var words = new List<ushort>();
        AddBlock(words, 1, 0, new Ac(40, 5), new Ac(30, 5));
        for (var i = 0; i < 5; i++)
        {
            AddBlock(words, 1, 0);
        }

        var ex = Assert.Throws<InvalidDataException>(() => Decode(words, 16, 16, frameIndex: 3));
        Assert.Contains("frame 3", ex.Message);
    }

    [Fact]
    public void AWordAfterALevelZeroWordPastPosition63_ThrowsAndNamesTheFrame()
    {
        var words = new List<ushort>();
        AddBlock(words, 1, 0, new Ac(40, 5), new Ac(30, 0), new Ac(0, 3));
        for (var i = 0; i < 5; i++)
        {
            AddBlock(words, 1, 0);
        }

        var ex = Assert.Throws<InvalidDataException>(() => Decode(words, 16, 16, frameIndex: 11));
        Assert.Contains("frame 11", ex.Message);
        Assert.Contains("position 63", ex.Message);
    }

    [Fact]
    public void ALevelZeroWordPastPosition63_IsIgnored()
    {
        var overflow = new List<ushort>();
        AddBlock(overflow, 1, 0, new Ac(40, 5), new Ac(30, 0));
        for (var i = 0; i < 5; i++)
        {
            AddBlock(overflow, 1, 0);
        }

        var plain = new List<ushort>();
        AddBlock(plain, 1, 0, new Ac(40, 5));
        for (var i = 0; i < 5; i++)
        {
            AddBlock(plain, 1, 0);
        }

        Assert.Equal(Decode(plain, 16, 16), Decode(overflow, 16, 16));
    }

    [Fact]
    public void ADcWordEqualToTheEndOfBlockWord_IsADc()
    {
        // 0xFE00 as a DC word: scale 63, DC 0x200 = -512. It is a DC, and the block still needs its end of block.
        var words = new List<ushort> { Eob, Eob };
        AddBlock(words, 1, 0);
        AddBlock(words, 1, 0);
        AddBlock(words, 1, 0);
        AddBlock(words, 1, 0);
        AddBlock(words, 1, 0);

        Assert.Equal(Reference(words, 16, 16), Decode(words, 16, 16));
    }

    [Fact]
    public void ATooSmallOutput_IsAnArgumentError()
    {
        var words = new List<ushort>();
        AddFlatMacroblock(words, 1, 0, 0, 0, 0, 0, 0);

        Assert.Throws<ArgumentException>(() => MdecFrameDecoder.Decode(words.ToArray(), 0, 16, 16, new byte[16 * 16 * 4 - 1]));
    }

    // ---- allocations ----

    [Fact]
    public void Decoding_DoesNotAllocate()
    {
        var random = new Random(99);
        var words = RandomFrame(random, MdecFrameDecoder.MacroblockCount(40, 24), 1).ToArray();
        var rgba = new byte[40 * 24 * 4];
        MdecFrameDecoder.Decode(words, 0, 40, 24, rgba);

        var before = AllocationWindow.Start();
        for (var i = 0; i < 200; i++)
        {
            MdecFrameDecoder.Decode(words, i, 40, 24, rgba);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    // ---- the two stages together ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BitstreamThenFrameDecoder_ProduceTheReferencePicture(int version)
    {
        var random = new Random(version);
        var words = RandomFrame(random, 4, 2);

        // Re-encode through the test encoder: version 3 predicts the DC, so build the blocks from the words.
        var blocks = new List<MdecTestBlock>();
        var position = 0;
        for (var i = 0; i < 24; i++)
        {
            int dcWord = words[position++];
            var acs = new List<MdecTestAc>();
            while (words[position] != Eob)
            {
                var w = words[position++];
                acs.Add(S10(w) == 0 ? MdecTestAc.Escaped(w >> 10, 0) : MdecTestAc.Of(w >> 10, S10(w)));
            }

            position++;
            var dc = S10(dcWord);
            blocks.Add(new MdecTestBlock(version == 3 ? dc / 4 * 4 : dc, acs.ToArray()));
        }

        var frame = MdecTestEncoder.Encode(version, 5, blocks);
        var decodedWords = new ushort[MdecBitstreamDecoder.MaxWordCount(frame.Length)];
        var count = MdecBitstreamDecoder.Decode(frame, 0, 4, decodedWords);
        var rgba = new byte[32 * 32 * 4];
        MdecFrameDecoder.Decode(decodedWords.AsSpan(0, count), 0, 32, 32, rgba);

        Assert.Equal(Reference(decodedWords.Take(count).ToList(), 32, 32), rgba);
    }
}
