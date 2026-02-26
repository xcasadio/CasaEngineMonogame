using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Editor-specific view render pipeline.
/// Extends <see cref="DefaultViewPipeline"/> with additional editor overlay passes:
/// grid, gizmos, selection outline, and ui overlays.
///
/// Each overlay step is a virtual method so derived classes can selectively
/// override individual stages.
///
/// Active under the EDITOR compilation symbol only.
/// The pipeline itself is always compiled but the editor-only components
/// (GridComponent, GizmoComponent, AxisComponent) are invoked indirectly via
/// the injected action delegates so this assembly does not depend on them.
/// </summary>
public class EditorViewPipeline : IViewRenderPipeline
{
    // ---- Overlay delegates (injected by the editor host) ----

    /// <summary>
    /// Called after the main world flush to render the editor grid.
    /// Signature: (graphicsDevice, view, frame).
    /// </summary>
    public Action<GraphicsDevice, RenderView, RenderFrame>? RenderGridAction { get; set; }

    /// <summary>
    /// Called to render gizmos (translation/rotation/scale handles).
    /// </summary>
    public Action<GraphicsDevice, RenderView, RenderFrame>? RenderGizmosAction { get; set; }

    /// <summary>
    /// Called to render the axis orientation indicator (corner XYZ icon).
    /// </summary>
    public Action<GraphicsDevice, RenderView, RenderFrame>? RenderAxisAction { get; set; }

    /// <summary>
    /// Called to render the selection outline around selected entities.
    /// </summary>
    public Action<GraphicsDevice, RenderView, RenderFrame>? RenderSelectionOutlineAction { get; set; }

    /// <summary>
    /// Called last to render any 2D editor UI overlay (labels, handles, etc.).
    /// </summary>
    public Action<GraphicsDevice, RenderView, RenderFrame>? RenderUIOverlayAction { get; set; }

    /// <inheritdoc/>
    public virtual void RenderView(
        GraphicsDevice                        graphicsDevice,
        RenderView                            view,
        in RenderFrame                        frame,
        IReadOnlyList<IViewFlushableRenderer> renderers)
    {
        // --- Pass 1: Opaque world geometry ---
        RenderOpaque(graphicsDevice, view, in frame, renderers);

        // --- Pass 2: Transparent geometry ---
        RenderTransparent(graphicsDevice, view, in frame, renderers);

        // --- Pass 3: Editor grid ---
        RenderGrid(graphicsDevice, view, in frame);

        // --- Pass 4: Gizmos ---
        RenderGizmos(graphicsDevice, view, in frame);

        // --- Pass 5: Axis orientation indicator ---
        RenderAxis(graphicsDevice, view, in frame);

        // --- Pass 6: Selection outline ---
        RenderSelectionOutline(graphicsDevice, view, in frame);

        // --- Pass 7: 2D UI overlay (labels, debug info) ---
        RenderUIOverlay(graphicsDevice, view, in frame);
    }

    // ---- Overridable stages ----

    /// <summary>
    /// Enqueue opaque world draw commands and flush renderers.
    /// Override to add custom opaque passes.
    /// </summary>
    protected virtual void RenderOpaque(
        GraphicsDevice                        graphicsDevice,
        RenderView                            view,
        in RenderFrame                        frame,
        IReadOnlyList<IViewFlushableRenderer> renderers)
    {
        view.World.Draw(in frame);

        foreach (var renderer in renderers)
        {
            renderer.Flush(in frame);
        }
    }

    /// <summary>
    /// Transparent pass. Override for alpha-blended geometry (e.g. particles, decals).
    /// Base implementation is a no-op — transparent objects are already enqueued by
    /// <see cref="RenderOpaque"/>.
    /// </summary>
    protected virtual void RenderTransparent(
        GraphicsDevice                        graphicsDevice,
        RenderView                            view,
        in RenderFrame                        frame,
        IReadOnlyList<IViewFlushableRenderer> renderers)
    {
        // No-op in base. Override to add transparency passes.
    }

    /// <summary>Renders the editor ground grid. Calls <see cref="RenderGridAction"/> if set.</summary>
    protected virtual void RenderGrid(GraphicsDevice gd, RenderView view, in RenderFrame frame)
        => RenderGridAction?.Invoke(gd, view, frame);

    /// <summary>Renders transform gizmos. Calls <see cref="RenderGizmosAction"/> if set.</summary>
    protected virtual void RenderGizmos(GraphicsDevice gd, RenderView view, in RenderFrame frame)
        => RenderGizmosAction?.Invoke(gd, view, frame);

    /// <summary>Renders the axis orientation indicator. Calls <see cref="RenderAxisAction"/> if set.</summary>
    protected virtual void RenderAxis(GraphicsDevice gd, RenderView view, in RenderFrame frame)
        => RenderAxisAction?.Invoke(gd, view, frame);

    /// <summary>Renders the selection outline. Calls <see cref="RenderSelectionOutlineAction"/> if set.</summary>
    protected virtual void RenderSelectionOutline(GraphicsDevice gd, RenderView view, in RenderFrame frame)
        => RenderSelectionOutlineAction?.Invoke(gd, view, frame);

    /// <summary>Renders 2D editor overlays. Calls <see cref="RenderUIOverlayAction"/> if set,
    /// then draws the per-view MGUI UIRoot.</summary>
    protected virtual void RenderUIOverlay(GraphicsDevice gd, RenderView view, in RenderFrame frame)
    {
        RenderUIOverlayAction?.Invoke(gd, view, frame);
        view.UIRoot?.Draw();
    }
}
