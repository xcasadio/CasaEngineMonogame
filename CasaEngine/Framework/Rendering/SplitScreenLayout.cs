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
    {
        viewCount = Math.Clamp(viewCount, 1, 4);

        return mode switch
        {
            SplitMode.Vertical   => ComputeVertical(screenWidth, screenHeight, viewCount),
            SplitMode.Horizontal => ComputeHorizontal(screenWidth, screenHeight, viewCount),
            SplitMode.Grid4      => ComputeGrid4(screenWidth, screenHeight),
            _                    => ComputeVertical(screenWidth, screenHeight, viewCount),
        };
    }

    // ---- Left / Right split ----
    private static Rectangle[] ComputeVertical(int w, int h, int count)
    {
        var rects = new Rectangle[count];
        int colWidth = w / count;

        for (int i = 0; i < count; i++)
        {
            int x = i * colWidth;
            // Last column takes any remainder pixel
            int width = (i == count - 1) ? (w - x) : colWidth;
            rects[i] = new Rectangle(x, 0, width, h);
        }

        return rects;
    }

    // ---- Top / Bottom split ----
    private static Rectangle[] ComputeHorizontal(int w, int h, int count)
    {
        var rects = new Rectangle[count];
        int rowHeight = h / count;

        for (int i = 0; i < count; i++)
        {
            int y = i * rowHeight;
            int height = (i == count - 1) ? (h - y) : rowHeight;
            rects[i] = new Rectangle(0, y, w, height);
        }

        return rects;
    }

    // ---- 2×2 grid ----
    private static Rectangle[] ComputeGrid4(int w, int h)
    {
        int hw = w / 2;
        int hh = h / 2;

        return new Rectangle[]
        {
            new Rectangle(0,  0,  hw,      hh),
            new Rectangle(hw, 0,  w - hw,  hh),
            new Rectangle(0,  hh, hw,      h - hh),
            new Rectangle(hw, hh, w - hw,  h - hh),
        };
    }
}
