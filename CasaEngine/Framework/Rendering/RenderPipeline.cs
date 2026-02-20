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
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;

    /// <param name="graphicsDevice">The graphics device to draw on.</param>
    /// <param name="renderers">
    /// Ordered list of renderer components to flush per view
    /// (e.g. StaticMesh → SkinnedMesh → Sprite → Line3d).
    /// </param>
    /// <param name="spriteBatch">
    /// Used to fill backbuffer viewport rectangles with the clear color.
    /// MonoGame's <see cref="GraphicsDevice.Clear"/> ignores the current viewport
    /// and always clears the full render target; using SpriteBatch is the correct
    /// way to scope a color fill to a sub-rectangle of the backbuffer.
    /// </param>
    public RenderPipeline(GraphicsDevice graphicsDevice, IReadOnlyList<IViewFlushableRenderer> renderers, SpriteBatch spriteBatch)
    {
        _graphicsDevice = graphicsDevice;
        _renderers = renderers;
        _spriteBatch = spriteBatch;

        // 1×1 white pixel used to fill viewport areas with the clear color.
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>
    /// Renders all enabled views.
    /// RenderTarget views are processed first so that the final SetRenderTarget(null)
    /// used to restore the backbuffer happens before any BackBuffer view is drawn.
    /// (On D3D11/MonoGame, SetRenderTarget(null) after a RT invalidates backbuffer
    /// content, so BackBuffer views must always be last.)
    /// </summary>
    public void Render(IReadOnlyList<RenderView> views)
    {
        // RenderTarget views first, then BackBuffer views.
        var orderedViews = views
            .Where(v => v.Enabled && !v.Surface.IsBackBuffer)
            .Concat(views.Where(v => v.Enabled && v.Surface.IsBackBuffer));

        foreach (var view in orderedViews)
        {
            // 1. Apply surface: SetRenderTarget + Viewport
            view.Surface.Apply(_graphicsDevice);

            // 2. Clear
            //
            // IMPORTANT: GraphicsDevice.Clear() always clears the FULL render target,
            // ignoring the current viewport. For split-screen BackBufferSurface views
            // this would wipe every previously rendered view.
            //
            // Solution for BackBuffer color clear: draw a viewport-sized quad via
            // SpriteBatch (which respects the current viewport) instead of calling
            // Clear(Target). GraphicsDevice.Clear() is still used for depth/stencil
            // because depth precision artifacts from a leftover depth buffer are
            // acceptable and no quad-draw trick exists for depth.
            //
            // For RenderTarget surfaces a full Clear(Target) is safe (they are
            // independent textures, not shared with other views).
            if (view.ClearColorBuffer && view.Surface.IsBackBuffer)
            {
                // Fill this viewport's rectangle with the clear color.
                var vp = view.Surface.ViewportRect;
                _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                    null, DepthStencilState.None, RasterizerState.CullNone);
                _spriteBatch.Draw(_pixel, new Rectangle(0, 0, vp.Width, vp.Height), view.ClearColor);
                _spriteBatch.End();

                // Clear depth/stencil only (full-target, but that is acceptable).
                _graphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil,
                    view.ClearColor, 1.0f, 0);
            }
            else
            {
                var clearOptions = ClearOptions.DepthBuffer | ClearOptions.Stencil;
                if (view.ClearColorBuffer)
                {
                    clearOptions |= ClearOptions.Target;
                }
                _graphicsDevice.Clear(clearOptions, view.ClearColor, 1.0f, 0);
            }

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
