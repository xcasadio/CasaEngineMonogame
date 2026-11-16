using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Optional capability of an <see cref="IViewRenderPipeline"/>: it can render the scene of a view without the user
/// interface (ADR-0105). In the linear HDR colour pipeline <see cref="RenderPipeline"/> renders the scene into an HDR
/// target, tonemaps it into the destination of the view, then composes the interface itself, so the interface is never
/// tonemapped. A view pipeline that does not implement this interface is rendered whole, interface included, into the HDR
/// target (a warning is logged once); in the legacy pipeline this interface is never used.
/// </summary>
public interface IScenePhaseViewPipeline
{
    /// <summary>
    /// False when this pipeline wraps another one that cannot render its scene alone, in which case the pipeline is
    /// treated as if it did not implement the interface. The default is true.
    /// </summary>
    bool SupportsScenePhase => true;

    /// <summary>
    /// Renders the scene of <paramref name="view"/> (world draw and renderer flushes) into the render target and viewport
    /// that are bound, and nothing of the interface phase.
    /// </summary>
    void RenderScene(
        GraphicsDevice                        graphicsDevice,
        RenderView                            view,
        in RenderFrame                        frame,
        IReadOnlyList<IViewFlushableRenderer> renderers);
}
