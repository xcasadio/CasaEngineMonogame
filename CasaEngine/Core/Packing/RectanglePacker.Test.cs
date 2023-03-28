/*
Nuclex Framework
Copyright (C) 2002-2011 Nuclex Development Labs

This library is free software; you can redistribute it and/or
modify it under the terms of the IBM Common Public License as
published by the IBM Corporation; either version 1.0 of the
License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
IBM Common Public License for more details.

You should have received a copy of the IBM Common Public
License along with this library
*/

namespace CasaEngine.Core.Packing;

/// <summary>Base class for unit testing the rectangle packers</summary>
internal abstract class RectanglePackerTest
{

    /// <summary>Delegate for a Rectangle Packer factory method</summary>
    /// <returns>A new rectangle packer</returns>
    protected delegate RectanglePacker RectanglePackerBuilder();

    /// <summary>Determines the efficiency of a packer with a packing area of 70x70</summary>
    /// <param name="packer">Packer with a packing area of 70x70 units</param>
    /// <returns>The efficiency factor of the packer</returns>
    /// <remarks>
    ///   A perfect packer would achieve an efficiency rating of 1.0. This is
    ///   impossible however since the 24 squares cannot all be packed into
    ///   the 70x70 square with no overlap (Bitner &amp; Reingold 1975). The closer
    ///   the efficiency rating is to 1.0, the better, with 0.99 being the
    ///   mathematically best rating achievable.
    /// </remarks>
    protected float CalculateEfficiency(RectanglePacker packer)
    {
        // If we take a 1x1 square, a 2x2 square, etc. up to a 24x24 square,
        // the sum of the areas of these squares is 4900, which is 70². This
        // is the only nontrivial sum of consecutive squares starting with
        // one which is a perfect square (Watson 1918).
        int areaCovered = 0;

        for (int size = 24; size >= 1; --size)
        {
            Point placement;

            if (packer.TryPack(size, size, out placement))
            {
                areaCovered += size * size;
            }
        }

        return areaCovered / 4900.0f;
    }

    /// <summary>Benchmarks the provided rectangle packer using random data</summary>
    /// <param name="buildPacker">
    ///   Rectangle packer build method returning new rectangle packers
    ///   with an area of 1024 x 1024
    /// </param>
    /// <returns>The achieved benchmark score</returns>
    protected float Benchmark(RectanglePackerBuilder buildPacker)
    {
        // How many runs to perform for getting a stable average
        const int averagingRuns = 1;

        // Generates the random number seeds. This is used so that each run produces
        // the same number sequences and makes the comparison of different algorithms
        // a little bit more stable.
        Random seedGenerator = new Random(12345);
        int rectanglesPacked = 0;

        // Perform a number of  runs to get a semi-stable average score
        for (int averagingRun = 0; averagingRun < averagingRuns; ++averagingRun)
        {
            Random dimensionGenerator = new Random(seedGenerator.Next());
            RectanglePacker packer = buildPacker();

            // Try to cramp as many rectangles into the packing area as possible
            for (; ; ++rectanglesPacked)
            {
                Point placement;

                int width = dimensionGenerator.Next(16, 64);
                int height = dimensionGenerator.Next(16, 64);

                // As soon as the packer rejects the first rectangle, the run is over
                if (!packer.TryPack(width, height, out placement))
                {
                    break;
                }
            }
        }

        // Return the average score achieved by the packer
        return rectanglesPacked / (float)averagingRuns;
    }

}

// namespace CasaEngineCommon.Packing
