using CasaEngine.Framework.Assets.Loaders;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Assets;

public class MipmapChainTests
{
    [Fact]
    public void Texture2DLoader_DoesNotGenerateMipmapsByDefault()
    {
        Assert.False(new Texture2DLoader().GenerateMipmaps);
        Assert.True(new Texture2DLoader { GenerateMipmaps = true }.GenerateMipmaps);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 2)]
    [InlineData(3, 3, 2)]
    [InlineData(4, 4, 3)]
    [InlineData(5, 3, 3)]
    [InlineData(7, 1, 3)]
    [InlineData(1, 9, 4)]
    [InlineData(37, 19, 6)]
    [InlineData(64, 32, 7)]
    [InlineData(1024, 1, 11)]
    [InlineData(1000, 700, 10)]
    public void GetLevelCount_IsFloorLog2OfTheLargestDimensionPlusOne(int width, int height, int expected)
    {
        Assert.Equal(expected, MipmapChain.GetLevelCount(width, height));
    }

    [Theory]
    [InlineData(37, 19)]
    [InlineData(64, 32)]
    [InlineData(5, 3)]
    [InlineData(1, 9)]
    [InlineData(1000, 700)]
    [InlineData(1, 1)]
    public void ComputeLevels_LevelSizesFollowMaxOneAndDimensionShiftedByTheLevel(int width, int height)
    {
        var levels = MipmapChain.ComputeLevels(new Color[width * height], width, height);

        Assert.Equal(MipmapChain.GetLevelCount(width, height), levels.Length);
        for (int level = 0; level < levels.Length; level++)
        {
            int expectedWidth = Math.Max(1, width >> level);
            int expectedHeight = Math.Max(1, height >> level);
            Assert.Equal(expectedWidth, MipmapChain.GetLevelSize(width, level));
            Assert.Equal(expectedHeight, MipmapChain.GetLevelSize(height, level));
            Assert.Equal(expectedWidth * expectedHeight, levels[level].Length);
        }

        Assert.Single(levels[^1]);
    }

    [Fact]
    public void ComputeLevels_LeavesLevelZeroUnchanged()
    {
        var level0 = new Color[5 * 3];
        for (int i = 0; i < level0.Length; i++)
        {
            level0[i] = new Color(i * 10, 255 - i, i, 200);
        }

        var expected = (Color[])level0.Clone();

        var levels = MipmapChain.ComputeLevels(level0, 5, 3);

        Assert.Same(level0, levels[0]);
        Assert.Equal(expected, levels[0]);
    }

    [Fact]
    public void ComputeNextLevel_AveragesEachTwoByTwoBlock()
    {
        // 4 x 2: two blocks.
        var source = new[]
        {
            new Color(0, 0, 0, 255), new Color(100, 20, 4, 255), new Color(10, 10, 10, 10), new Color(10, 10, 10, 10),
            new Color(100, 40, 8, 255), new Color(200, 60, 12, 255), new Color(10, 10, 10, 10), new Color(20, 30, 40, 50),
        };

        var next = MipmapChain.ComputeNextLevel(source, 4, 2);

        Assert.Equal(2, next.Length);
        Assert.Equal(new Color(100, 30, 6, 255), next[0]);
        Assert.Equal(new Color(13, 15, 18, 20), next[1]);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 0, 1, 0)]
    [InlineData(0, 0, 1, 1, 1)]
    [InlineData(0, 0, 0, 2, 1)]
    [InlineData(255, 255, 255, 0, 191)]
    [InlineData(255, 255, 255, 255, 255)]
    [InlineData(1, 1, 1, 2, 1)]
    public void ComputeNextLevel_RoundsToTheNearestValueWithHalvesUp(int a, int b, int c, int d, int expected)
    {
        var source = new[] { new Color(a, a, a, a), new Color(b, b, b, b), new Color(c, c, c, c), new Color(d, d, d, d) };

        var next = MipmapChain.ComputeNextLevel(source, 2, 2);

        Assert.Equal(new Color(expected, expected, expected, expected), next[0]);
    }

    [Fact]
    public void ComputeNextLevel_AveragesStraightAlphaLikeTheColourChannels()
    {
        // Two opaque pixels and two fully transparent ones: the transparent pixels' colour is not weighted away.
        var source = new[]
        {
            new Color(200, 100, 50, 255), new Color(200, 100, 50, 255),
            new Color(0, 0, 0, 0), new Color(0, 0, 0, 0),
        };

        var next = MipmapChain.ComputeNextLevel(source, 2, 2);

        Assert.Equal(new Color(100, 50, 25, 128), next[0]);
    }

    [Fact]
    public void ComputeNextLevel_OddDimensionsIgnoreTheLastRowAndColumn()
    {
        // 3 x 3 gives 1 x 1: the average of the top-left 2 x 2 block.
        var source = new Color[9];
        source[0] = new Color(10, 10, 10, 10);
        source[1] = new Color(20, 20, 20, 20);
        source[3] = new Color(30, 30, 30, 30);
        source[4] = new Color(40, 40, 40, 40);
        for (int i = 0; i < source.Length; i++)
        {
            if (i is 2 or 5 or 6 or 7 or 8)
            {
                source[i] = new Color(255, 255, 255, 255);
            }
        }

        var next = MipmapChain.ComputeNextLevel(source, 3, 3);

        Assert.Equal(new Color(25, 25, 25, 25), Assert.Single(next));
    }

    [Fact]
    public void ComputeNextLevel_AnAxisOfLengthOneIsAveragedOnTheOtherAxisOnly()
    {
        var column = new[] { new Color(10, 0, 0, 255), new Color(21, 0, 0, 255), new Color(100, 0, 0, 255), new Color(200, 0, 0, 255) };

        var next = MipmapChain.ComputeNextLevel(column, 1, 4);

        Assert.Equal(2, next.Length);
        Assert.Equal(new Color(16, 0, 0, 255), next[0]);
        Assert.Equal(new Color(150, 0, 0, 255), next[1]);

        var row = new[] { new Color(0, 0, 0, 255), new Color(1, 0, 0, 255), new Color(6, 0, 0, 255), new Color(8, 0, 0, 255) };

        var nextRow = MipmapChain.ComputeNextLevel(row, 4, 1);

        Assert.Equal(new Color(1, 0, 0, 255), nextRow[0]);
        Assert.Equal(new Color(7, 0, 0, 255), nextRow[1]);
    }

    [Fact]
    public void ComputeNextLevel_FromOneByOneKeepsThePixel()
    {
        var next = MipmapChain.ComputeNextLevel(new[] { new Color(7, 8, 9, 10) }, 1, 1);

        Assert.Equal(new Color(7, 8, 9, 10), Assert.Single(next));
    }

    [Fact]
    public void ComputeLevels_ASolidColourStaysSolidDownToOneByOne()
    {
        var solid = new Color(12, 99, 201, 77);
        var level0 = new Color[37 * 19];
        Array.Fill(level0, solid);

        var levels = MipmapChain.ComputeLevels(level0, 37, 19);

        foreach (var level in levels)
        {
            Assert.All(level, pixel => Assert.Equal(solid, pixel));
        }
    }

    [Fact]
    public void ComputeLevels_EachLevelIsTheNextLevelOfThePreviousOne()
    {
        var level0 = new Color[13 * 6];
        for (int i = 0; i < level0.Length; i++)
        {
            level0[i] = new Color((i * 37) % 256, (i * 11) % 256, (i * 5) % 256, (i * 3) % 256);
        }

        var levels = MipmapChain.ComputeLevels(level0, 13, 6);

        int width = 13;
        int height = 6;
        for (int level = 1; level < levels.Length; level++)
        {
            Assert.Equal(MipmapChain.ComputeNextLevel(levels[level - 1], width, height), levels[level]);
            width = Math.Max(1, width >> 1);
            height = Math.Max(1, height >> 1);
        }
    }

    [Fact]
    public void ComputeNextLevel_SpanOverloadWritesIntoTheDestination()
    {
        var source = new[] { new Color(0, 0, 0, 0), new Color(4, 4, 4, 4), new Color(8, 8, 8, 8), new Color(12, 12, 12, 12) };
        var destination = new Color[1];

        MipmapChain.ComputeNextLevel(source, 2, 2, destination);

        Assert.Equal(new Color(6, 6, 6, 6), destination[0]);
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MipmapChain.GetLevelCount(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => MipmapChain.GetLevelCount(4, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MipmapChain.GetLevelSize(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MipmapChain.GetLevelSize(4, -1));
        Assert.Throws<ArgumentNullException>(() => MipmapChain.ComputeLevels(null!, 2, 2));
        Assert.Throws<ArgumentException>(() => MipmapChain.ComputeLevels(new Color[3], 2, 2));
        Assert.Throws<ArgumentException>(() => MipmapChain.ComputeNextLevel(new Color[4], 2, 2, new Color[2]));
    }

    [Fact]
    public void GetLevelSize_PastTheLastLevelIsOne()
    {
        Assert.Equal(1, MipmapChain.GetLevelSize(37, 5));
        Assert.Equal(1, MipmapChain.GetLevelSize(37, 20));
        Assert.Equal(1, MipmapChain.GetLevelSize(37, 40));
    }
}
