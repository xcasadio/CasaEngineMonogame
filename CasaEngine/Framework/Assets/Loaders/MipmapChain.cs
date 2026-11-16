using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets.Loaders;

/// <summary>
/// Computes the mipmap chain of an RGBA image on the CPU, and uploads it into a mipmapped <see cref="Texture2D"/>.
/// Level sizes follow the MonoGame convention: level <c>i</c> measures <c>max(1, dimension >> i)</c> on each axis, and a
/// chain holds <c>floor(log2(max(width, height))) + 1</c> levels down to 1 x 1. Each level is the 2 x 2 box average of
/// the previous one (see <see cref="ComputeNextLevel(ReadOnlySpan{Color}, int, int, Span{Color})"/>).
/// The computation is pure (no graphics device); only <see cref="UploadChain"/> touches a texture.
/// </summary>
/// <remarks>
/// Not for hot paths: every call that returns an array allocates the whole chain (about a third more than level 0).
/// It is meant for load time and for the occasional refresh of a texture that is painted at runtime.
/// </remarks>
public static class MipmapChain
{
    /// <summary>Number of levels of a full chain: <c>floor(log2(max(width, height))) + 1</c>.</summary>
    public static int GetLevelCount(int width, int height)
    {
        ValidateSize(width, height);
        return BitOperations.Log2((uint)Math.Max(width, height)) + 1;
    }

    /// <summary>Size of one axis at <paramref name="level"/>: <c>max(1, size >> level)</c>.</summary>
    public static int GetLevelSize(int size, int level)
    {
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A texture dimension must be at least 1.");
        }

        if (level < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "A mipmap level cannot be negative.");
        }

        return level >= 31 ? 1 : Math.Max(1, size >> level);
    }

    /// <summary>
    /// Computes the level that follows a <paramref name="sourceWidth"/> x <paramref name="sourceHeight"/> level into
    /// <paramref name="destination"/>, which must hold <c>max(1, sourceWidth >> 1) * max(1, sourceHeight >> 1)</c> pixels.
    /// </summary>
    /// <remarks>
    /// Each destination pixel is the average of the 2 x 2 source block at twice its coordinates, rounded to the nearest
    /// value (halves round up), on the four channels alike: alpha is straight (not premultiplied), so a transparent
    /// pixel weighs as much as an opaque one. On an axis of length 1 the block is 1 pixel wide. On an odd axis the last
    /// source row or column is not part of any block.
    /// </remarks>
    public static void ComputeNextLevel(ReadOnlySpan<Color> source, int sourceWidth, int sourceHeight, Span<Color> destination)
    {
        ValidateSize(sourceWidth, sourceHeight);
        if (source.Length != sourceWidth * sourceHeight)
        {
            throw new ArgumentException(
                $"The source holds {source.Length} pixels, expected {sourceWidth} x {sourceHeight} = {sourceWidth * sourceHeight}.",
                nameof(source));
        }

        int destinationWidth = GetLevelSize(sourceWidth, 1);
        int destinationHeight = GetLevelSize(sourceHeight, 1);
        if (destination.Length != destinationWidth * destinationHeight)
        {
            throw new ArgumentException(
                $"The destination holds {destination.Length} pixels, expected {destinationWidth} x {destinationHeight} = {destinationWidth * destinationHeight}.",
                nameof(destination));
        }

        int blockWidth = sourceWidth > 1 ? 2 : 1;
        int blockHeight = sourceHeight > 1 ? 2 : 1;
        int sampleCount = blockWidth * blockHeight;
        int half = sampleCount / 2;

        for (int y = 0; y < destinationHeight; y++)
        {
            for (int x = 0; x < destinationWidth; x++)
            {
                int red = 0;
                int green = 0;
                int blue = 0;
                int alpha = 0;

                for (int blockY = 0; blockY < blockHeight; blockY++)
                {
                    int rowStart = (y * 2 + blockY) * sourceWidth + x * 2;
                    for (int blockX = 0; blockX < blockWidth; blockX++)
                    {
                        Color sample = source[rowStart + blockX];
                        red += sample.R;
                        green += sample.G;
                        blue += sample.B;
                        alpha += sample.A;
                    }
                }

                destination[y * destinationWidth + x] = new Color(
                    (red + half) / sampleCount,
                    (green + half) / sampleCount,
                    (blue + half) / sampleCount,
                    (alpha + half) / sampleCount);
            }
        }
    }

    /// <summary>Computes the level that follows <paramref name="source"/> and returns it in a new array.</summary>
    public static Color[] ComputeNextLevel(Color[] source, int sourceWidth, int sourceHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateSize(sourceWidth, sourceHeight);

        var destination = new Color[GetLevelSize(sourceWidth, 1) * GetLevelSize(sourceHeight, 1)];
        ComputeNextLevel(source, sourceWidth, sourceHeight, destination);
        return destination;
    }

    /// <summary>
    /// Computes the whole chain from the level 0 pixels (row by row, top to bottom). The result has
    /// <see cref="GetLevelCount"/> entries; entry 0 is <paramref name="level0Pixels"/> itself, unchanged, and the last
    /// entry is a single pixel.
    /// </summary>
    public static Color[][] ComputeLevels(Color[] level0Pixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(level0Pixels);
        ValidateSize(width, height);
        if (level0Pixels.Length != width * height)
        {
            throw new ArgumentException(
                $"The pixel array holds {level0Pixels.Length} pixels, expected {width} x {height} = {width * height}.",
                nameof(level0Pixels));
        }

        var levels = new Color[GetLevelCount(width, height)][];
        levels[0] = level0Pixels;

        for (int level = 1; level < levels.Length; level++)
        {
            levels[level] = ComputeNextLevel(levels[level - 1], GetLevelSize(width, level - 1), GetLevelSize(height, level - 1));
        }

        return levels;
    }

    /// <summary>
    /// Writes <paramref name="level0Pixels"/> as level 0 of <paramref name="texture"/> and every level computed from
    /// them into the levels below. The texture must be a <see cref="SurfaceFormat.Color"/> texture created with
    /// <c>mipMap: true</c> and the dimensions of the pixel array. Call it again after repainting level 0 to refresh the
    /// chain (the cost of a level-0 <c>SetData</c> plus a third of it).
    /// </summary>
    public static void UploadChain(Texture2D texture, Color[] level0Pixels)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(level0Pixels);

        if (texture.Format != SurfaceFormat.Color)
        {
            throw new ArgumentException($"The texture format is {texture.Format}, only {SurfaceFormat.Color} is supported.", nameof(texture));
        }

        if (level0Pixels.Length != texture.Width * texture.Height)
        {
            throw new ArgumentException(
                $"The pixel array holds {level0Pixels.Length} pixels, the texture is {texture.Width} x {texture.Height}.",
                nameof(level0Pixels));
        }

        int expectedLevelCount = GetLevelCount(texture.Width, texture.Height);
        if (texture.LevelCount != expectedLevelCount)
        {
            throw new ArgumentException(
                $"The texture has {texture.LevelCount} level(s), expected {expectedLevelCount}: create it with mipMap: true.",
                nameof(texture));
        }

        var levels = ComputeLevels(level0Pixels, texture.Width, texture.Height);
        for (int level = 0; level < levels.Length; level++)
        {
            texture.SetData(level, null, levels[level], 0, levels[level].Length);
        }
    }

    private static void ValidateSize(int width, int height)
    {
        if (width < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A texture width must be at least 1.");
        }

        if (height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "A texture height must be at least 1.");
        }
    }
}
