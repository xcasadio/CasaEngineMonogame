using System.Diagnostics;
using CasaEngine.Framework.Application.Components;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// The standard view render pipeline used by game and runtime views.
///
/// Render order:
/// <list type="number">
///   <item>Enqueue world draw commands (<c>World.Draw</c>).</item>
///   <item>Flush all registered renderers in order (mesh → skinned → sprite → line).</item>
///   <item>Compose the per-view UI phase through an <see cref="IUICompositionService"/>.</item>
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
        long worldDrawStartTimestamp = Stopwatch.GetTimestamp();

        // 1. Enqueue world geometry into renderer queues.
        view.World.Draw(in frame);

        view.RenderStats.WorldDrawCpuMilliseconds += GetElapsedMilliseconds(worldDrawStartTimestamp);

        // 2. Flush each renderer for this view's camera frame.
        long rendererFlushStartTimestamp = Stopwatch.GetTimestamp();

        foreach (var renderer in renderers)
        {
            renderer.Flush(in frame, view.RenderStats);
        }

        view.RenderStats.RendererFlushCpuMilliseconds += GetElapsedMilliseconds(rendererFlushStartTimestamp);

        // 3. Compose the UI phase on top of the 3D scene while the view's
        //    render target and viewport are still active.
        long uiComposeStartTimestamp = Stopwatch.GetTimestamp();

        (view.UICompositionService ?? DefaultUICompositionService.Instance)
            .Compose(graphicsDevice, view, in frame);

        view.RenderStats.UiComposeCpuMilliseconds += GetElapsedMilliseconds(uiComposeStartTimestamp);
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }
}

