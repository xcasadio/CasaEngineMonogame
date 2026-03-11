using Microsoft.Xna.Framework;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.GUI;

/// <summary>
/// Computes UI scale factors and safe-area insets for a <see cref="RenderView"/>.
///
/// <b>Purpose:</b>
/// <list type="bullet">
///   <item>Scale all UI elements uniformly when the viewport differs from the reference resolution.</item>
///   <item>Inset UI elements from screen edges to avoid overscan / notch areas on consoles and TVs.</item>
/// </list>
///
/// <b>Usage:</b>
/// <code>
/// var scaler = new UIScaler(new Point(1920, 1080));
/// float scale  = scaler.ComputeScale(viewportSize);
/// Rectangle sa = scaler.ComputeSafeArea(viewportSize, 0.05f);
/// </code>
/// </summary>
public sealed class UIScaler
{
    /// <summary>The reference (design-time) resolution at which UI assets were authored.</summary>
    public Point ReferenceResolution { get; set; }

    /// <summary>
    /// How the scale is derived from the viewport vs reference resolution.
    /// <see cref="ScaleMode.Fit"/> preserves aspect ratio (letterbox).
    /// <see cref="ScaleMode.Fill"/> scales to fill (may crop).
    /// <see cref="ScaleMode.Width"/> scales by width only.
    /// <see cref="ScaleMode.Height"/> scales by height only.
    /// </summary>
    public ScaleMode Mode { get; set; } = ScaleMode.Fit;

    public UIScaler(Point referenceResolution)
    {
        ReferenceResolution = referenceResolution;
    }

    /// <summary>
    /// Returns the uniform scale factor to apply to UI elements so that
    /// layout designed at <see cref="ReferenceResolution"/> fits within <paramref name="viewportSize"/>.
    /// </summary>
    public float ComputeScale(Point viewportSize)
    {
        float scaleX = (float)viewportSize.X / ReferenceResolution.X;
        float scaleY = (float)viewportSize.Y / ReferenceResolution.Y;

        return Mode switch
        {
            ScaleMode.Fit    => Math.Min(scaleX, scaleY),
            ScaleMode.Fill   => Math.Max(scaleX, scaleY),
            ScaleMode.Width  => scaleX,
            ScaleMode.Height => scaleY,
            _                => Math.Min(scaleX, scaleY),
        };
    }

    /// <summary>
    /// Returns a safe-area rectangle inset from the viewport edges by a relative
    /// margin (e.g. 0.05 = 5% inset on each side).
    /// HUD elements should stay within this rectangle to avoid TV overscan clipping.
    /// </summary>
    /// <param name="viewportSize">Viewport width and height in pixels.</param>
    /// <param name="relativeInset">Fraction of the viewport to inset on each side (0–0.5).</param>
    public Rectangle ComputeSafeArea(Point viewportSize, float relativeInset = 0.05f)
    {
        int insetX = (int)(viewportSize.X * relativeInset);
        int insetY = (int)(viewportSize.Y * relativeInset);
        return new Rectangle(insetX, insetY,
            viewportSize.X - insetX * 2,
            viewportSize.Y - insetY * 2);
    }

    public UIViewMetrics ComputeMetrics(Point viewportSize, float relativeInset = 0.05f)
    {
        return new UIViewMetrics(
            viewportSize,
            ReferenceResolution,
            ComputeScale(viewportSize),
            ComputeSafeArea(viewportSize, relativeInset));
    }
}

/// <summary>Resolved UI layout data for a single render view.</summary>
public readonly record struct UIViewMetrics(
    Point ViewportSize,
    Point ReferenceResolution,
    float Scale,
    Rectangle SafeArea);

/// <summary>Determines how <see cref="UIScaler"/> derives a uniform scale from viewport dimensions.</summary>
public enum ScaleMode
{
    /// <summary>Scale to fit inside the viewport while preserving aspect ratio (may letterbox).</summary>
    Fit,
    /// <summary>Scale to fill the viewport entirely (may crop).</summary>
    Fill,
    /// <summary>Scale only by width.</summary>
    Width,
    /// <summary>Scale only by height.</summary>
    Height,
}
