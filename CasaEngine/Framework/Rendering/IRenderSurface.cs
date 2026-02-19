using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Represents an output surface for rendering: either the backbuffer or a RenderTarget2D.
/// </summary>
public interface IRenderSurface
{
    /// <summary>True if this surface writes directly to the backbuffer.</summary>
    bool IsBackBuffer { get; }

    /// <summary>Viewport rectangle in pixels.</summary>
    Rectangle ViewportRect { get; }

    /// <summary>The render target, or null when targeting the backbuffer.</summary>
    RenderTarget2D? RenderTarget { get; }

    /// <summary>
    /// Activates the surface: sets the render target and viewport on the GraphicsDevice.
    /// </summary>
    void Apply(GraphicsDevice graphicsDevice);

    /// <summary>
    /// Restores the backbuffer and full-screen viewport when necessary
    /// (no-op if already on the backbuffer).
    /// </summary>
    void Restore(GraphicsDevice graphicsDevice);
}
