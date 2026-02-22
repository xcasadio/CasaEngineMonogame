using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// The standard view render pipeline used by game and runtime views.
///
/// Render order:
/// <list type="number">
///   <item>Enqueue world draw commands (<c>World.Draw</c>).</item>
///   <item>Flush all registered renderers in order (mesh → skinned → sprite → line).</item>
/// </list>
///
/// Surface clearing and render target management are handled by
/// <see cref="RenderPipeline"/> before and after this pipeline is invoked.
/// </summary>
public sealed class DefaultViewPipeline : IViewRenderPipeline
{
    /// <summary>Singleton instance for shared usage.</summary>
    public static readonly DefaultViewPipeline Instance = new();

    /// <inheritdoc/>
    public void RenderView(
        GraphicsDevice                        graphicsDevice,
        RenderView                            view,
        in RenderFrame                        frame,
        IReadOnlyList<IViewFlushableRenderer> renderers)
    {
        // 1. Enqueue world geometry into renderer queues.
        view.World.Draw(in frame);

        // 2. Flush each renderer for this view's camera frame.
        foreach (var renderer in renderers)
        {
            renderer.Flush(in frame);
        }
    }
}
