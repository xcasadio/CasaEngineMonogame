using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Render surface that targets a RenderTarget2D (e.g. for MGUI editor panels).
///
/// v2 improvements:
/// <list type="bullet">
///   <item>
///     Integrates with <see cref="RenderTargetPool.Shared"/> when available —
///     old RTs are returned to the pool instead of being disposed.
///   </item>
///   <item>
///     Debounced resize: if the requested size changes more than once per frame
///     (common during docking operations) the RT is only recreated once per Draw call.
///     Call <see cref="RequestResize"/> from a UI resize handler and
///     <see cref="EnsureSize"/> will apply the pending size on the next render.
///   </item>
/// </list>
/// </summary>
public sealed class RenderTargetSurface : IRenderSurface, IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SurfaceFormat  _surfaceFormat;
    private readonly DepthFormat    _depthFormat;
    private readonly RenderTargetPool? _renderTargetPool;
    private RenderTarget2D?         _renderTarget;
    private bool                    _disposed;

    // Debounce state
    private int  _pendingWidth;
    private int  _pendingHeight;
    private bool _resizeDirty;

    public bool IsBackBuffer => false;

    public Rectangle ViewportRect => _renderTarget != null
        ? new Rectangle(0, 0, _renderTarget.Width, _renderTarget.Height)
        : Rectangle.Empty;

    public RenderTarget2D? RenderTarget => _renderTarget;

    /// <summary>Direct access to the produced texture (alias of <see cref="RenderTarget"/>).</summary>
    public Texture2D? Texture => _renderTarget;

    public RenderTargetSurface(
        GraphicsDevice graphicsDevice,
        int            width,
        int            height,
        SurfaceFormat  surfaceFormat = SurfaceFormat.Color,
        DepthFormat    depthFormat   = DepthFormat.Depth24,
        RenderTargetPool? renderTargetPool = null)
    {
        _graphicsDevice = graphicsDevice;
        _surfaceFormat  = surfaceFormat;
        _depthFormat    = depthFormat;
        _renderTargetPool = renderTargetPool;
        CreateTarget(width, height);
    }

    // ---- Resize API ----

    /// <summary>
    /// Schedules a resize to be applied on the next call to <see cref="EnsureSize"/>.
    /// Safe to call many times per frame (e.g. from a WPF SizeChanged handler) —
    /// the RT is only recreated once when <see cref="EnsureSize"/> is invoked.
    /// </summary>
    public void RequestResize(int width, int height)
    {
        int w = Math.Max(1, width);
        int h = Math.Max(1, height);

        if (_renderTarget == null || _renderTarget.Width != w || _renderTarget.Height != h)
        {
            _pendingWidth  = w;
            _pendingHeight = h;
            _resizeDirty   = true;
        }
    }

    /// <summary>
    /// Ensures the RenderTarget matches the requested size.
    /// If a pending resize is queued (via <see cref="RequestResize"/>) it is applied now.
    /// Recreates the texture only if dimensions actually changed.
    /// </summary>
    public void EnsureSize(int width, int height)
    {
        // Merge an explicit call with any pending debounced resize
        RequestResize(width, height);
        ApplyPendingResize();
    }

    /// <summary>
    /// Applies a pending debounced resize if one is queued.
    /// Called automatically at the start of <see cref="Apply"/> so the RT is always
    /// up-to-date before a new render pass begins.
    /// </summary>
    public void ApplyPendingResize()
    {
        if (!_resizeDirty) return;
        _resizeDirty = false;
        ReplaceTarget(_pendingWidth, _pendingHeight);
    }

    // ---- IRenderSurface ----

    /// <inheritdoc/>
    public void Apply(GraphicsDevice graphicsDevice)
    {
        // Consume any pending debounced resize before rendering into the RT
        ApplyPendingResize();

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

    // ---- Private helpers ----

    private void CreateTarget(int width, int height)
    {
        var pool = RenderTargetPool.Resolve(_renderTargetPool);
        if (pool != null)
        {
            _renderTarget = pool.Acquire(
                Math.Max(1, width), Math.Max(1, height),
                _surfaceFormat, _depthFormat);
        }
        else
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
    }

    private void ReplaceTarget(int width, int height)
    {
        var pool = RenderTargetPool.Resolve(_renderTargetPool);
        if (_renderTarget != null)
        {
            if (pool != null)
                pool.Release(_renderTarget);
            else
                _renderTarget.Dispose();

            _renderTarget = null;
        }

        CreateTarget(width, height);
    }

    // ---- IDisposable ----

    public void Dispose()
    {
        if (!_disposed)
        {
            var pool = RenderTargetPool.Resolve(_renderTargetPool);
            if (_renderTarget != null)
            {
                if (pool != null)
                    pool.Release(_renderTarget);
                else
                    _renderTarget.Dispose();

                _renderTarget = null;
            }

            _disposed = true;
        }
    }
}
