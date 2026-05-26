namespace CasaEngine.Framework.Rendering;

/// <summary>
/// A view host that targets a rectangular region of the backbuffer.
/// Owns a <see cref="BackBufferSurface"/> and updates its rectangle on resize.
/// </summary>
public sealed class BackBufferHost : IViewHost
{
    private readonly BackBufferSurface _surface;
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
    public event Action<IViewHost, int, int> Resized;

    /// <inheritdoc/>
    public event Action<IViewHost> Closed;

    /// <summary>The backbuffer surface owned by this host.</summary>
    public BackBufferSurface Surface => _surface;

    /// <param name="viewId">The id of the view this host corresponds to.</param>
    /// <param name="initialRect">Initial viewport rectangle on the backbuffer.</param>
    public BackBufferHost(ViewId viewId, Rectangle initialRect)
    {
        ViewId   = viewId;
        _surface = new BackBufferSurface(initialRect);
    }

    /// <inheritdoc/>
    public void NotifyResized(int newWidth, int newHeight)
    {
        var rect = _surface.ViewportRect;
        _surface.ViewportRect = new Rectangle(rect.X, rect.Y, newWidth, newHeight);
        Resized?.Invoke(this, newWidth, newHeight);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Closed?.Invoke(this);
        }
    }
}
