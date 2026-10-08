using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Application;

/// <summary>
/// The runtime's use of <see cref="VirtualResolutionLayout"/> (ADR-0048), kept apart from <see cref="CasaEngineGame"/>
/// so it can be tested without a graphics device.
/// </summary>
internal static class VirtualResolutionRuntime
{
    /// <summary>
    /// Sizes the single full-window back-buffer view: with a virtual resolution, the integer-fit image; without, the
    /// whole window, as the engine always did.
    /// </summary>
    internal static void ResizeSingleBackBufferView(
        RenderView view, BackBufferSurface surface, int windowWidth, int windowHeight, VirtualResolutionSettings virtualResolution)
        => ResizeSingleBackBufferView(view, surface, windowWidth, windowHeight, new Rectangle(0, 0, windowWidth, windowHeight), virtualResolution);

    /// <summary>
    /// Sizes the single back-buffer view inside the layout area of the view manager (ADR-0070): without a virtual
    /// resolution, the view and its camera take the area; with one, the area must be the whole window.
    /// </summary>
    internal static void ResizeSingleBackBufferView(
        RenderView view, BackBufferSurface surface, int windowWidth, int windowHeight, Rectangle layoutArea,
        VirtualResolutionSettings virtualResolution)
    {
        if (virtualResolution != null)
        {
            ThrowIfLayoutAreaIsNotTheWindow(layoutArea, windowWidth, windowHeight);
            VirtualResolutionLayout.Apply(surface, view.Camera, windowWidth, windowHeight, virtualResolution);
            return;
        }

        surface.ViewportRect = layoutArea;
        view.Camera?.OnScreenResized(layoutArea.Width, layoutArea.Height);
    }

    /// <summary>
    /// A virtual resolution places its integer-fit image in the whole window (ADR-0048); view layout insets (ADR-0070)
    /// cannot be combined with it.
    /// </summary>
    internal static void ThrowIfLayoutAreaIsNotTheWindow(Rectangle layoutArea, int windowWidth, int windowHeight)
    {
        if (layoutArea != new Rectangle(0, 0, windowWidth, windowHeight))
        {
            throw new InvalidOperationException(
                $"View layout insets (ADR-0070) cannot be combined with a virtual resolution (ADR-0048): the layout area {layoutArea} " +
                $"must be the whole {windowWidth}x{windowHeight} window.");
        }
    }

    /// <summary>
    /// True when the back buffer must be cleared to black before the views: a virtual resolution is active, there is
    /// exactly one back-buffer view, and its rectangle does not cover the window.
    /// </summary>
    internal static bool ShouldClearBands(
        IReadOnlyList<RenderView> views, VirtualResolutionSettings virtualResolution, int windowWidth, int windowHeight)
    {
        return virtualResolution != null
            && views.Count == 1
            && views[0].Surface is BackBufferSurface surface
            && surface.ViewportRect != new Rectangle(0, 0, windowWidth, windowHeight);
    }
}
