using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Multi-view render pipeline. Iterates a list of <see cref="RenderView"/> instances
/// and for each one: applies the surface, clears, enqueues world draw commands,
/// then flushes all registered renderers using the view's camera frame.
/// </summary>
public sealed class RenderPipeline
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly IReadOnlyList<IViewFlushableRenderer> _renderers;

    /// <param name="graphicsDevice">The graphics device to draw on.</param>
    /// <param name="renderers">
    /// Ordered list of renderer components to flush per view
    /// (e.g. StaticMesh → SkinnedMesh → Sprite → Line3d).
    /// </param>
    public RenderPipeline(GraphicsDevice graphicsDevice, IReadOnlyList<IViewFlushableRenderer> renderers)
    {
        _graphicsDevice = graphicsDevice;
        _renderers = renderers;
    }

    /// <summary>
    /// Renders all enabled views in order.
    /// </summary>
    public void Render(IReadOnlyList<RenderView> views)
    {
        foreach (var view in views)
        {
            if (!view.Enabled)
            {
                continue;
            }

            // 1. Apply surface: SetRenderTarget + Viewport
            view.Surface.Apply(_graphicsDevice);

            // 2. Clear
            var clearOptions = ClearOptions.DepthBuffer | ClearOptions.Stencil;
            if (view.ClearColorBuffer)
            {
                clearOptions |= ClearOptions.Target;
            }
            _graphicsDevice.Clear(clearOptions, view.ClearColor, 1.0f, 0);

            // 3. Build the camera frame for this view
            var frame = RenderFrameFactory.From(view.Camera, view.Surface.ViewportRect);

            // 4. Enqueue world draw commands (fills renderer queues)
            view.World.Draw(frame.ViewProjection);

            // 5. Flush all renderers for this view (drains their queues)
            foreach (var renderer in _renderers)
            {
                renderer.Flush(in frame);
            }

            // 6. If the view targeted a RenderTarget, restore the backbuffer
            if (!view.Surface.IsBackBuffer)
            {
                view.Surface.Restore(_graphicsDevice);
            }
        }

        // After all views: reset to backbuffer + full-screen viewport
        _graphicsDevice.SetRenderTarget(null);
        var pp = _graphicsDevice.PresentationParameters;
        _graphicsDevice.Viewport = new Viewport(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
    }
}
