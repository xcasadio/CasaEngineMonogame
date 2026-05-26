using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// A passive presenter that simply exposes the view's rendered texture for
/// consumption by an external UI framework (WPF Image, MGUI widget, etc.).
///
/// No GPU blitting is performed — the caller (UI host) is responsible for
/// reading <see cref="Texture"/> and displaying it.
/// </summary>
public sealed class TexturePresenter : IViewPresenter
{
    /// <summary>
    /// The most recently rendered texture, or null if the view has not yet been rendered
    /// or the view's surface is not an RT surface.
    /// </summary>
    public Texture2D Texture { get; private set; }

    /// <summary>
    /// Updates <see cref="Texture"/> from the view's surface render target.
    /// Called automatically by the pipeline after each render pass.
    /// </summary>
    public void Present(GraphicsDevice graphicsDevice, RenderView view)
    {
        Texture = view.Surface.RenderTarget;
    }
}
