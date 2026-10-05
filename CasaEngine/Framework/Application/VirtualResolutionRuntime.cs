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
    {
        if (virtualResolution != null)
        {
            VirtualResolutionLayout.Apply(surface, view.Camera, windowWidth, windowHeight, virtualResolution);
            return;
        }

        surface.ViewportRect = new Rectangle(0, 0, windowWidth, windowHeight);
        view.Camera?.OnScreenResized(windowWidth, windowHeight);
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
