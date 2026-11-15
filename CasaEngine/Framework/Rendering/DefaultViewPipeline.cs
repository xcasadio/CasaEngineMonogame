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
///   <item>Compose the per-view UI phase through an <see cref="IUICompositionService"/>: the UI
///   itself, then any post-UI overlays registered on the view (<see cref="IPostUIOverlay"/>).</item>
/// </list>
///
/// Surface clearing and render target management are handled by
/// <see cref="RenderPipeline"/> before and after this pipeline is invoked.
/// </summary>
public sealed class DefaultViewPipeline : IViewRenderPipeline, IScenePhaseViewPipeline
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
        RenderSceneCore(view, in frame, renderers, skipPostTonemapRenderers: false);

        // 3. Compose the UI phase on top of the 3D scene while the view's
        //    render target and viewport are still active.
        long uiComposeStartTimestamp = Stopwatch.GetTimestamp();

        (view.UICompositionService ?? DefaultUICompositionService.Instance)
            .Compose(graphicsDevice, view, in frame);

        view.RenderStats.UiComposeCpuMilliseconds += GetElapsedMilliseconds(uiComposeStartTimestamp);
    }

    /// <summary>
    /// Steps 1 and 2 of <see cref="RenderView"/>: the world draw and the renderer flushes, without the UI phase
    /// (<see cref="IScenePhaseViewPipeline"/>). In a view that really draws through the HDR target the renderers that are
    /// <see cref="IPostTonemapRenderer"/> are not flushed here: <see cref="RenderPipeline"/> flushes them after the tonemapping
    /// (ADR-0098). Without that colour pipeline every renderer is flushed, in the order of the legacy pipeline.
    /// </summary>
    public void RenderScene(
        GraphicsDevice                        graphicsDevice,
        RenderView                            view,
        in RenderFrame                        frame,
        IReadOnlyList<IViewFlushableRenderer> renderers)
    {
        RenderSceneCore(view, in frame, renderers, skipPostTonemapRenderers: frame.Environment.LinearColorActive);
    }

    private static void RenderSceneCore(
        RenderView                            view,
        in RenderFrame                        frame,
        IReadOnlyList<IViewFlushableRenderer> renderers,
        bool                                  skipPostTonemapRenderers)
    {
        long worldDrawStartTimestamp = Stopwatch.GetTimestamp();

        // 1. Enqueue world geometry into renderer queues.
        view.World.Draw(in frame);

        view.RenderStats.WorldDrawCpuMilliseconds += GetElapsedMilliseconds(worldDrawStartTimestamp);

        // 2. Flush each renderer for this view's camera frame.
        long rendererFlushStartTimestamp = Stopwatch.GetTimestamp();

        foreach (var renderer in renderers)
        {
            if (skipPostTonemapRenderers && renderer is IPostTonemapRenderer)
            {
                continue;
            }

            renderer.Flush(in frame, view.RenderStats);
        }

        view.RenderStats.RendererFlushCpuMilliseconds += GetElapsedMilliseconds(rendererFlushStartTimestamp);
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }
}

