using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;

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
    /// Gives the camera of a buffered view (ADR-0087) a viewport of the virtual size at zoom 1, so it frames exactly the
    /// target. Call it AFTER anything that sized the camera to the whole window (<c>World.OnScreenResized</c>).
    /// </summary>
    internal static void FrameBufferedCamera(CameraComponent camera, VirtualResolutionPresenter presenter)
    {
        if (camera == null)
        {
            return;
        }

        camera.OnScreenResized(presenter.VirtualWidth, presenter.VirtualHeight);
        if (camera is Camera2dComponent camera2d)
        {
            camera2d.Zoom = 1f;
        }
    }

    /// <summary>
    /// The single view of a buffered mode (ADR-0087) after a window resize: the target keeps its virtual size, the
    /// camera goes back to the virtual viewport at zoom 1 (the world sized it to the window just before), and the
    /// presenter recomputes where it shows the target. Returns false when <paramref name="views"/> is not exactly one
    /// view presented by a <see cref="VirtualResolutionPresenter"/>.
    /// </summary>
    internal static bool TryResizeBufferedView(IReadOnlyList<RenderView> views, int windowWidth, int windowHeight)
    {
        if (views.Count != 1 || views[0].Presenter is not VirtualResolutionPresenter presenter)
        {
            return false;
        }

        presenter.Resize(windowWidth, windowHeight);
        FrameBufferedCamera(views[0].Camera, presenter);
        return true;
    }

    /// <summary>
    /// Releases what the default view of a buffered mode owns (ADR-0087) when that view is removed, for example by
    /// <see cref="ViewManager.Clear"/> on a world change. Any other view is left as it was.
    /// </summary>
    internal static void ReleaseOwnedPresentation(RenderView view)
    {
        if (view?.Presenter is VirtualResolutionPresenter presenter)
        {
            presenter.Dispose();
        }
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
