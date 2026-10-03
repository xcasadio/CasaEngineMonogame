namespace CasaEngine.Framework.Rendering;

/// <summary>The result of <see cref="VirtualResolutionLayout.Compute"/>.</summary>
public readonly struct VirtualResolutionFit
{
    public VirtualResolutionFit(int scale, Rectangle imageRect, bool coversWindow)
    {
        Scale = scale;
        ImageRect = imageRect;
        CoversWindow = coversWindow;
    }

    /// <summary>Integer factor applied to the virtual resolution.</summary>
    public int Scale { get; }

    /// <summary>Rectangle of the image in window pixels, already cropped by the window.</summary>
    public Rectangle ImageRect { get; }

    /// <summary>True when <see cref="ImageRect"/> is the whole window (no band to clear).</summary>
    public bool CoversWindow { get; }
}

/// <summary>
/// Integer-fit layout of a virtual resolution inside a window (ADR-0048): the largest whole factor that fits, the scaled
/// image centered, the rest of the window left for black bands. Pure: no device, no state.
/// </summary>
public static class VirtualResolutionLayout
{
    /// <summary>
    /// <c>k = max(1, floor(min(window width / virtual width, window height / virtual height)))</c>; the image is
    /// <c>virtual width·k × virtual height·k</c>, centered with offsets floored, then cropped by the window (a window
    /// smaller than the image shows its center at factor 1).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A virtual dimension is not positive.</exception>
    public static VirtualResolutionFit Compute(int windowWidth, int windowHeight, int virtualWidth, int virtualHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(virtualWidth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(virtualHeight, 0);

        int scale = Math.Max(1, Math.Min(windowWidth / virtualWidth, windowHeight / virtualHeight));
        int imageWidth = virtualWidth * scale;
        int imageHeight = virtualHeight * scale;
        var image = new Rectangle(
            FloorHalf(windowWidth - imageWidth),
            FloorHalf(windowHeight - imageHeight),
            imageWidth,
            imageHeight);

        var window = new Rectangle(0, 0, Math.Max(0, windowWidth), Math.Max(0, windowHeight));
        var cropped = Rectangle.Intersect(image, window);

        return new VirtualResolutionFit(scale, cropped, cropped == window);
    }

    // Floor, not truncation: the offset of a window smaller than the image is negative.
    private static int FloorHalf(int value) => value >> 1;
}
