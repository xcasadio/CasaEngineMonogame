using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// A view host that targets an off-screen <see cref="RenderTargetSurface"/>.
/// Owns the surface and disposes it (returning the RT to the pool) when closed.
/// Supports debounced resize via <see cref="RenderTargetSurface.EnsureSize"/>.
/// </summary>
public sealed class RenderTargetHost : IViewHost
{
    private readonly RenderTargetSurface _surface;
    private bool _disposed;

    /// <inheritdoc/>
    public ViewId ViewId { get; }

    /// <inheritdoc/>
    public int Width  => _surface.ViewportRect.Width;

    /// <inheritdoc/>
    public int Height => _surface.ViewportRect.Height;

    /// <inheritdoc/>
    public bool IsVisible { get; set; } = true;

    /// <inheritdoc/>
    public event Action<IViewHost, int, int>? Resized;

    /// <inheritdoc/>
    public event Action<IViewHost>? Closed;

    /// <summary>The render-target surface owned by this host.</summary>
    public RenderTargetSurface Surface => _surface;

    /// <param name="viewId">The id of the view this host corresponds to.</param>
    /// <param name="graphicsDevice">Graphics device for creating the render target.</param>
    /// <param name="width">Initial texture width in pixels.</param>
    /// <param name="height">Initial texture height in pixels.</param>
    /// <param name="surfaceFormat">Texture format. Default: Color.</param>
    /// <param name="depthFormat">Depth buffer format. Default: Depth24.</param>
    public RenderTargetHost(
        ViewId        viewId,
        GraphicsDevice graphicsDevice,
        int            width,
        int            height,
        SurfaceFormat  surfaceFormat = SurfaceFormat.Color,
        DepthFormat    depthFormat   = DepthFormat.Depth24)
    {
        ViewId   = viewId;
        _surface = new RenderTargetSurface(graphicsDevice, width, height, surfaceFormat, depthFormat);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Delegates to <see cref="RenderTargetSurface.EnsureSize"/> which applies debouncing:
    /// the RT is only recreated when dimensions actually change.
    /// Call this from the UI resize handler (e.g. WPF SizeChanged event).
    /// </remarks>
    public void NotifyResized(int newWidth, int newHeight)
    {
        _surface.EnsureSize(newWidth, newHeight);
        Resized?.Invoke(this, newWidth, newHeight);
    }

    /// <summary>
    /// Disposes the underlying render-target surface, freeing GPU memory.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _surface.Dispose();
            Closed?.Invoke(this);
        }
    }
}
