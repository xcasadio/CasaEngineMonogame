using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// A drawable registered on a <see cref="RenderView"/> that is composed immediately after the
/// view's own UI (<see cref="RenderView.UIView"/>) has been drawn (D2,
/// ai-agent/tasks/screen-effect-above-ui-tasks.md, T1.1). Every pipeline that goes through
/// <see cref="IUICompositionService"/> - <see cref="DefaultViewPipeline"/> and
/// <c>OverlayViewPipeline</c> alike - draws registered overlays in this same spot.
/// </summary>
/// <remarks>
/// Used by consumers that need to draw on top of the composed UI, such as a screen fade/tint in
/// <see cref="ScreenEffects.ScreenEffectLayer.AboveUI"/> mode. Implementations must submit through a
/// renderer and flush it immediately (D3): nothing may be left queued from one frame to the next.
/// </remarks>
public interface IPostUIOverlay
{
    /// <summary>
    /// Draws this overlay. Called by <see cref="DefaultUICompositionService.Compose"/> right after
    /// <c>view.UIView?.Draw()</c>, with the view's render target and viewport still active.
    /// </summary>
    void Draw(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame);
}
