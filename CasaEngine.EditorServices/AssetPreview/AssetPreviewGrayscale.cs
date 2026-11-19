namespace CasaEngine.EditorServices.AssetPreview;

/// <summary>Builds the grayscale image of a square 8-bit map (a height map for instance).</summary>
public static class AssetPreviewGrayscale
{
    /// <summary>Largest side of the image built by the editor.</summary>
    public const int MaxPreviewSide = 512;

    /// <summary>
    /// Samples the <paramref name="side"/> x <paramref name="side"/> map with the nearest neighbour so that no side exceeds
    /// <paramref name="maxSide"/>. Returns row-major gray values and sets <paramref name="outputSide"/>.
    /// </summary>
    public static byte[] Downsample(ReadOnlySpan<byte> map, int side, int maxSide, out int outputSide)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(side, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSide, 1);
        if (map.Length < (long)side * side)
        {
            throw new ArgumentException($"A {side} x {side} map needs {(long)side * side} bytes, got {map.Length}.", nameof(map));
        }

        outputSide = Math.Min(side, maxSide);
        var result = new byte[outputSide * outputSide];
        for (int y = 0; y < outputSide; y++)
        {
            int sourceRow = (int)((long)y * side / outputSide) * side;
            for (int x = 0; x < outputSide; x++)
            {
                result[(y * outputSide) + x] = map[sourceRow + (int)((long)x * side / outputSide)];
            }
        }

        return result;
    }
}
