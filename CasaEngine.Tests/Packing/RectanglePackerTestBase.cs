using CasaEngine.Core.Packing;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Packing;

public abstract class RectanglePackerTestBase
{
    protected static float CalculateEfficiency(RectanglePacker packer)
    {
        int areaCovered = 0;

        for (int size = 24; size >= 1; --size)
        {
            if (packer.TryPack(size, size, out Point placement))
            {
                areaCovered += size * size;
            }
        }

        return areaCovered / 4900.0f;
    }

    protected static float Benchmark(Func<RectanglePacker> buildPacker)
    {
        const int averagingRuns = 1;

        Random seedGenerator = new(12345);
        int rectanglesPacked = 0;

        for (int averagingRun = 0; averagingRun < averagingRuns; ++averagingRun)
        {
            Random dimensionGenerator = new(seedGenerator.Next());
            RectanglePacker packer = buildPacker();

            for (;; ++rectanglesPacked)
            {
                int width = dimensionGenerator.Next(16, 64);
                int height = dimensionGenerator.Next(16, 64);

                if (!packer.TryPack(width, height, out Point placement))
                {
                    break;
                }
            }
        }

        return rectanglesPacked / (float)averagingRuns;
    }

    protected static void AssertRejectsTooLargeRectangles(RectanglePacker packer)
    {
        Assert.False(packer.TryPack(129, 10, out Point placement));
        Assert.False(packer.TryPack(10, 129, out placement));
    }

    protected static void AssertThrowsForTooLargeRectangle(RectanglePacker packer)
    {
        Assert.Throws<OutOfSpaceException>(() => packer.Pack(129, 129));
    }

    protected static void AssertPacksBarelyFittingRectangle(RectanglePacker packer)
    {
        Point placement = packer.Pack(128, 128);
        Assert.Equal(new Point(0, 0), placement);
    }
}