using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Defines the rendering strategy for a single <see cref="RenderView"/>.
///
/// Implement this interface to create custom per-view render pipelines:
/// <list type="bullet">
///   <item><b>DefaultViewPipeline</b> — standard game view (clear → world draw → flush → UI composition).</item>
///   <item><b>EditorViewPipeline</b> — adds gizmos, grid, selection outline overlays, then UI composition.</item>
///   <item><b>PreviewPipeline</b> — simplified rendering for inspector asset previews.</item>
/// </list>
///
/// The surface has already been applied (SetRenderTarget + Viewport) and cleared by
/// <see cref="RenderPipeline"/> before <see cref="RenderView"/> is called.
/// The caller restores the initial render target afterwards.
/// </summary>
public interface IViewRenderPipeline
{
    /// <summary>
    /// Performs the full render pass for <paramref name="view"/>.
    /// </summary>
    /// <param name="graphicsDevice">Active graphics device.</param>
    /// <param name="view">The view being rendered.</param>
    /// <param name="frame">Pre-computed camera matrices and viewport data.</param>
    /// <param name="renderers">
    /// Ordered list of flush-able renderers shared across all views
    /// (e.g. StaticMesh → SkinnedMesh → Sprite → Line3d).
    /// </param>
    void RenderView(
        GraphicsDevice                        graphicsDevice,
        RenderView                            view,
        in RenderFrame                        frame,
        IReadOnlyList<IViewFlushableRenderer> renderers);
}
