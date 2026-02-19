using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Render surface that targets a RenderTarget2D (e.g. for MGUI editor panels).
/// Supports dynamic resizing via <see cref="EnsureSize"/>.
/// </summary>
public sealed class RenderTargetSurface : IRenderSurface, IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SurfaceFormat _surfaceFormat;
    private readonly DepthFormat _depthFormat;
    private RenderTarget2D? _renderTarget;
    private bool _disposed;

    public bool IsBackBuffer => false;

    public Rectangle ViewportRect => _renderTarget != null
        ? new Rectangle(0, 0, _renderTarget.Width, _renderTarget.Height)
        : Rectangle.Empty;

    public RenderTarget2D? RenderTarget => _renderTarget;

    /// <summary>Direct access to the produced texture (alias of <see cref="RenderTarget"/>).</summary>
    public Texture2D? Texture => _renderTarget;

    public RenderTargetSurface(
        GraphicsDevice graphicsDevice,
        int width,
        int height,
        SurfaceFormat surfaceFormat = SurfaceFormat.Color,
        DepthFormat depthFormat = DepthFormat.Depth24)
    {
        _graphicsDevice = graphicsDevice;
        _surfaceFormat = surfaceFormat;
        _depthFormat = depthFormat;
        CreateTarget(width, height);
    }

    /// <summary>
    /// Ensures the RenderTarget matches the requested size.
    /// Recreates the texture if dimensions change.
    /// </summary>
    public void EnsureSize(int width, int height)
    {
        if (_renderTarget != null &&
            _renderTarget.Width == width &&
            _renderTarget.Height == height)
        {
            return;
        }

        _renderTarget?.Dispose();
        CreateTarget(width, height);
    }

    private void CreateTarget(int width, int height)
    {
        _renderTarget = new RenderTarget2D(
            _graphicsDevice,
            Math.Max(1, width),
            Math.Max(1, height),
            false,
            _surfaceFormat,
            _depthFormat,
            0,
            RenderTargetUsage.PreserveContents);
    }

    /// <inheritdoc/>
    public void Apply(GraphicsDevice graphicsDevice)
    {
        graphicsDevice.SetRenderTarget(_renderTarget);
        graphicsDevice.Viewport = new Viewport(0, 0, _renderTarget!.Width, _renderTarget.Height);
    }

    /// <inheritdoc/>
    public void Restore(GraphicsDevice graphicsDevice)
    {
        // Restore the backbuffer and full-screen viewport
        graphicsDevice.SetRenderTarget(null);
        var pp = graphicsDevice.PresentationParameters;
        graphicsDevice.Viewport = new Viewport(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _renderTarget?.Dispose();
            _renderTarget = null;
            _disposed = true;
        }
    }
}
