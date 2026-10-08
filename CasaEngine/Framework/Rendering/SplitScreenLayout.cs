namespace CasaEngine.Framework.Rendering;

/// <summary>Layout mode for split-screen viewport division.</summary>
public enum SplitMode
{
    /// <summary>Two views side by side (left / right).</summary>
    Vertical,

    /// <summary>Two views stacked (top / bottom).</summary>
    Horizontal,

    /// <summary>Four views in a 2x2 grid.</summary>
    Grid4,
}

/// <summary>
/// Helper that computes viewport rectangles for split-screen layouts.
/// </summary>
public static class SplitScreenLayout
{
    /// <summary>
    /// Computes an array of non-overlapping viewport rectangles that together
    /// cover the full screen.
    /// </summary>
    /// <param name="screenWidth">Back-buffer width in pixels.</param>
    /// <param name="screenHeight">Back-buffer height in pixels.</param>
    /// <param name="viewCount">Number of viewports (1..4).</param>
    /// <param name="mode">How to divide the screen.</param>
    /// <returns>Array of <paramref name="viewCount"/> rectangles.</returns>
    public static Rectangle[] Compute(int screenWidth, int screenHeight, int viewCount, SplitMode mode)
        => Compute(new Rectangle(0, 0, screenWidth, screenHeight), viewCount, mode);

    /// <summary>
    /// Computes an array of non-overlapping viewport rectangles that together cover <paramref name="area"/>
    /// (ADR-0070: the layout area of <see cref="ViewManager"/>). An area at the origin gives exactly the rectangles
    /// of the full-screen overload.
    /// </summary>
    /// <param name="area">Back-buffer rectangle to divide.</param>
    /// <param name="viewCount">Number of viewports (1..4).</param>
    /// <param name="mode">How to divide the area.</param>
    /// <returns>Array of <paramref name="viewCount"/> rectangles.</returns>
    public static Rectangle[] Compute(Rectangle area, int viewCount, SplitMode mode)
    {
        viewCount = Math.Clamp(viewCount, 1, 4);

        return mode switch
        {
            SplitMode.Vertical   => ComputeVertical(area, viewCount),
            SplitMode.Horizontal => ComputeHorizontal(area, viewCount),
            SplitMode.Grid4      => ComputeGrid4(area),
            _                    => ComputeVertical(area, viewCount),
        };
    }

    // ---- Left / Right split ----
    private static Rectangle[] ComputeVertical(Rectangle area, int count)
    {
        var rects = new Rectangle[count];
        int colWidth = area.Width / count;

        for (int i = 0; i < count; i++)
        {
            int x = i * colWidth;
            // Last column takes any remainder pixel
            int width = (i == count - 1) ? (area.Width - x) : colWidth;
            rects[i] = new Rectangle(area.X + x, area.Y, width, area.Height);
        }

        return rects;
    }

    // ---- Top / Bottom split ----
    private static Rectangle[] ComputeHorizontal(Rectangle area, int count)
    {
        var rects = new Rectangle[count];
        int rowHeight = area.Height / count;

        for (int i = 0; i < count; i++)
        {
            int y = i * rowHeight;
            int height = (i == count - 1) ? (area.Height - y) : rowHeight;
            rects[i] = new Rectangle(area.X, area.Y + y, area.Width, height);
        }

        return rects;
    }

    // ---- 2×2 grid ----
    private static Rectangle[] ComputeGrid4(Rectangle area)
    {
        int w = area.Width;
        int h = area.Height;
        int hw = w / 2;
        int hh = h / 2;
        int x = area.X;
        int y = area.Y;

        return new Rectangle[]
        {
            new(x,      y,      hw,      hh),
            new(x + hw, y,      w - hw,  hh),
            new(x,      y + hh, hw,      h - hh),
            new(x + hw, y + hh, w - hw,  h - hh),
        };
    }
}
