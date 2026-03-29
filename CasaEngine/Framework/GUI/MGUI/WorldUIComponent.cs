using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Materials;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.GUI;

/// <summary>
/// Renders a view UI runtime into a <see cref="RenderTarget2D"/>
/// so that it can be mapped onto a 3D quad in world-space (e.g. in-game screens,
/// arcade monitors, holographic panels).
///
/// The component owns the offscreen UI pass and can bind the produced texture to
/// world materials such as <see cref="UnlitTextureMaterial"/> or <see cref="LitDiffuseMaterial"/>.
/// </summary>
public sealed class WorldUIComponent : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly RenderTargetSurface _surface;
    private readonly List<Action<Texture2D?>> _textureBindings = [];
    private bool                    _disposed;

    /// <summary>The hosted UI runtime rendered to the world texture.</summary>
    public IUIViewRuntime? UIView { get; set; }

    /// <summary>The render target that the UI is painted into each frame.</summary>
    public RenderTarget2D? RenderTarget => _surface.RenderTarget;

    /// <summary>The pooled render surface backing the offscreen UI pass.</summary>
    public RenderTargetSurface Surface => _surface;

    /// <summary>Alias of <see cref="RenderTarget"/> for material or quad consumers.</summary>
    public Texture2D? Texture => _surface.Texture;

    /// <summary>Clear color used before drawing the offscreen UI.</summary>
    public Color ClearColor { get; set; } = Color.Transparent;

    /// <summary>Resolution of the world-space UI texture (width × height in pixels).</summary>
    public Point Resolution { get; private set; }

    public WorldUIComponent(GraphicsDevice graphicsDevice, int width = 512, int height = 256)
    {
        _graphicsDevice = graphicsDevice;
        _surface = new RenderTargetSurface(
            graphicsDevice,
            width,
            height,
            SurfaceFormat.Color,
            DepthFormat.Depth24);
        Resolution = new Point(width, height);
    }

    /// <summary>Resizes the backing render target.</summary>
    public void Resize(int width, int height)
    {
        _surface.EnsureSize(width, height);
        Resolution = new Point(Math.Max(1, width), Math.Max(1, height));
        RefreshBindings();
    }

    /// <summary>Updates the hosted UI runtime before the offscreen draw pass.</summary>
    public void Update(GameTime gameTime)
    {
        if (_disposed || UIView == null)
        {
            return;
        }

        var viewport = new Rectangle(0, 0, Math.Max(1, Resolution.X), Math.Max(1, Resolution.Y));
        UIView.UpdateMetrics(new UIViewMetrics(Resolution, Resolution, 1.0f, viewport));
        UIView.Update(gameTime);
    }

    /// <summary>Binds the produced UI texture to an unlit material.</summary>
    public void BindToMaterial(UnlitTextureMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        BindTexture(texture => material.BasColor = texture);
    }

    /// <summary>Binds the produced UI texture to a lit material.</summary>
    public void BindToMaterial(LitDiffuseMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        BindTexture(texture => material.BasColor = texture);
    }

    public void BindTexture(Action<Texture2D?> textureBinding)
    {
        ArgumentNullException.ThrowIfNull(textureBinding);

        _textureBindings.Add(textureBinding);
        textureBinding(Texture);
    }

    /// <summary>
    /// Renders the hosted UI runtime into <see cref="RenderTarget"/> for this frame.
    /// Call this before world draw so the texture is up-to-date when the world quad is drawn.
    ///
    /// </summary>
    public void DrawToTexture()
    {
        if (_disposed || UIView == null)
        {
            return;
        }

        using var guard = new GraphicsStateGuard(_graphicsDevice);

        _surface.Apply(_graphicsDevice);
        RefreshBindings();
        _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, ClearColor, 1.0f, 0);
        UIView.Draw();
    }

    private void RefreshBindings()
    {
        var texture = Texture;
        foreach (var binding in _textureBindings)
        {
            binding(texture);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _surface.Dispose();
    }
}
