using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Render surface that targets the backbuffer within a given viewport rectangle.
/// </summary>
public sealed class BackBufferSurface : IRenderSurface
{
    public bool IsBackBuffer => true;
    public Rectangle ViewportRect { get; }
    public RenderTarget2D? RenderTarget => null;

    public BackBufferSurface(Rectangle viewportRect)
    {
        ViewportRect = viewportRect;
    }

    /// <inheritdoc/>
    public void Apply(GraphicsDevice graphicsDevice)
    {
        // Ensure we are on the backbuffer
        graphicsDevice.SetRenderTarget(null);

        graphicsDevice.Viewport = new Viewport(
            ViewportRect.X,
            ViewportRect.Y,
            ViewportRect.Width,
            ViewportRect.Height);
    }

    /// <inheritdoc/>
    public void Restore(GraphicsDevice graphicsDevice)
    {
        // Already on the backbuffer — restore only the full-screen viewport.
        var pp = graphicsDevice.PresentationParameters;
        graphicsDevice.Viewport = new Viewport(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
    }
}
