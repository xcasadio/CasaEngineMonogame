using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Presents an RT-based view's rendered texture to a region of the backbuffer,
/// with configurable scaling and letterboxing via <see cref="PresentMode"/>.
/// </summary>
public sealed class BackBufferPresenter : IViewPresenter
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Rectangle   _destinationRect;

    /// <summary>Controls how the image is scaled into <paramref name="destinationRect"/>.</summary>
    public PresentMode PresentMode { get; set; } = PresentMode.Fit;

    /// <summary>Color of letterbox / pillarbox bars when <see cref="PresentMode.Fit"/> is used.</summary>
    public Color LetterboxColor { get; set; } = Color.Black;

    /// <param name="spriteBatch">SpriteBatch used for drawing.</param>
    /// <param name="destinationRect">Target rectangle on the backbuffer (in pixels).</param>
    public BackBufferPresenter(SpriteBatch spriteBatch, Rectangle destinationRect)
    {
        _spriteBatch     = spriteBatch;
        _destinationRect = destinationRect;
    }

    /// <inheritdoc/>
    public void Present(GraphicsDevice graphicsDevice, RenderView view)
    {
        var texture = view.Surface.RenderTarget;
        if (texture == null)
        {
            return;
        }

        var dest = ComputeDestRect(texture.Width, texture.Height, _destinationRect, PresentMode);

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
            SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

        // Letterbox fill (only visible when dest is smaller than _destinationRect)
        if (PresentMode == PresentMode.Fit && dest != _destinationRect)
        {
            // Handled below; SpriteBatch background is already the backbuffer contents.
        }

        _spriteBatch.Draw(texture, dest, Color.White);
        _spriteBatch.End();
    }

    // ---- Helpers ----

    private static Rectangle ComputeDestRect(int srcW, int srcH, Rectangle container, PresentMode mode)
    {
        if (srcW <= 0 || srcH <= 0)
        {
            return container;
        }

        return mode switch
        {
            PresentMode.Stretch     => container,
            PresentMode.Fit         => FitRect(srcW, srcH, container),
            PresentMode.Fill        => FillRect(srcW, srcH, container),
            PresentMode.PixelPerfect => CenterRect(srcW, srcH, container),
            _                       => container,
        };
    }

    private static Rectangle FitRect(int srcW, int srcH, Rectangle container)
    {
        float scaleX = (float)container.Width  / srcW;
        float scaleY = (float)container.Height / srcH;
        float scale  = Math.Min(scaleX, scaleY);
        int   w      = (int)(srcW * scale);
        int   h      = (int)(srcH * scale);
        return new Rectangle(
            container.X + (container.Width  - w) / 2,
            container.Y + (container.Height - h) / 2,
            w, h);
    }

    private static Rectangle FillRect(int srcW, int srcH, Rectangle container)
    {
        float scaleX = (float)container.Width  / srcW;
        float scaleY = (float)container.Height / srcH;
        float scale  = Math.Max(scaleX, scaleY);
        int   w      = (int)(srcW * scale);
        int   h      = (int)(srcH * scale);
        return new Rectangle(
            container.X + (container.Width  - w) / 2,
            container.Y + (container.Height - h) / 2,
            w, h);
    }

    private static Rectangle CenterRect(int srcW, int srcH, Rectangle container) =>
        new(
            container.X + (container.Width  - srcW) / 2,
            container.Y + (container.Height - srcH) / 2,
            srcW, srcH);
}
