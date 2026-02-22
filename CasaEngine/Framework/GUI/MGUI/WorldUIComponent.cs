using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.GUI;

/// <summary>
/// Renders a <see cref="UIRoot"/>'s MGUI desktop into a <see cref="RenderTarget2D"/>
/// so that it can be mapped onto a 3D quad in world-space (e.g. in-game screens,
/// arcade monitors, holographic panels).
///
/// <b>Status: stub / not yet functional.</b>
/// This component wires up the RenderTarget plumbing but does not yet draw the
/// quad into the world. See the task file for the full implementation plan.
/// </summary>
public sealed class WorldUIComponent : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private RenderTarget2D?         _renderTarget;
    private bool                    _disposed;

    /// <summary>The <see cref="UIRoot"/> whose desktop is rendered to the world texture.</summary>
    public UIRoot? UIRoot { get; set; }

    /// <summary>The render target that the UI is painted into each frame.</summary>
    public RenderTarget2D? RenderTarget => _renderTarget;

    /// <summary>Resolution of the world-space UI texture (width × height in pixels).</summary>
    public Point Resolution { get; private set; }

    public WorldUIComponent(GraphicsDevice graphicsDevice, int width = 512, int height = 256)
    {
        _graphicsDevice = graphicsDevice;
        Resize(width, height);
    }

    /// <summary>Resizes the backing render target.</summary>
    public void Resize(int width, int height)
    {
        _renderTarget?.Dispose();
        _renderTarget = new RenderTarget2D(_graphicsDevice, width, height);
        Resolution   = new Point(width, height);
    }

    /// <summary>
    /// Renders the <see cref="UIRoot"/>'s desktop into <see cref="RenderTarget"/> for this frame.
    /// Call this before world draw so the texture is up-to-date when the world quad is drawn.
    ///
    /// <b>TODO:</b> Set <c>_graphicsDevice.SetRenderTarget(_renderTarget)</c>, call
    /// <c>UIRoot.Draw()</c>, then restore the previous render target.
    /// </summary>
    public void DrawToTexture()
    {
        // TODO (PR6): implement world-UI render pass.
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _renderTarget?.Dispose();
    }
}
