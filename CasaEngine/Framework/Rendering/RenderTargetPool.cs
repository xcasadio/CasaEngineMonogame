using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// A simple pool of <see cref="RenderTarget2D"/> objects keyed by (width, height, format, depth).
/// Avoids repeated GPU allocation/deallocation when views are resized during docking operations.
///
/// Usage:
/// <code>
/// var rt = RenderTargetPool.Shared.Acquire(width, height, SurfaceFormat.Color, DepthFormat.Depth24);
/// // ... use rt ...
/// RenderTargetPool.Shared.Release(rt);
/// </code>
///
/// Lifetime: call <see cref="DisposeAll"/> when the game exits or the device is reset.
/// </summary>
public sealed class RenderTargetPool : IDisposable
{
    // Key = (width, height, surfaceFormat, depthFormat)
    private readonly Dictionary<(int, int, SurfaceFormat, DepthFormat), Queue<RenderTarget2D>> _pool = new();
    private readonly GraphicsDevice _graphicsDevice;
    private bool _disposed;

    /// <summary>A process-wide shared pool. Assign to the engine's GameManager or CasaEngineGame.</summary>
    public static RenderTargetPool? Shared { get; set; }

    /// <summary>
    /// Total number of render targets currently tracked (acquired + in pool).
    /// Useful for leak detection in tests/sandbox.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>Number of render targets currently available in the pool (not in use).</summary>
    public int FreeCount
    {
        get
        {
            int count = 0;
            foreach (var queue in _pool.Values) count += queue.Count;
            return count;
        }
    }

    public RenderTargetPool(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    /// <summary>
    /// Returns an RT with the requested dimensions and format.
    /// If a matching RT is in the pool it is reused; otherwise a new one is created.
    /// </summary>
    public RenderTarget2D Acquire(
        int           width,
        int           height,
        SurfaceFormat surfaceFormat = SurfaceFormat.Color,
        DepthFormat   depthFormat   = DepthFormat.Depth24)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = (width, height, surfaceFormat, depthFormat);

        if (_pool.TryGetValue(key, out var queue) && queue.Count > 0)
        {
            return queue.Dequeue();
        }

        // Create a brand-new RT
        TotalCount++;
        return new RenderTarget2D(
            _graphicsDevice,
            Math.Max(1, width),
            Math.Max(1, height),
            false,
            surfaceFormat,
            depthFormat,
            0,
            RenderTargetUsage.PreserveContents);
    }

    /// <summary>
    /// Returns a render target to the pool so it can be reused.
    /// The caller must NOT use <paramref name="rt"/> after calling this.
    /// </summary>
    public void Release(RenderTarget2D rt)
    {
        if (_disposed || rt.IsDisposed) return;

        var key = (rt.Width, rt.Height, rt.Format, rt.DepthStencilFormat);

        if (!_pool.TryGetValue(key, out var queue))
        {
            queue = new Queue<RenderTarget2D>();
            _pool[key] = queue;
        }

        queue.Enqueue(rt);
    }

    /// <summary>
    /// Disposes and removes all pooled render targets that are currently free
    /// (not acquired). Safe to call periodically to trim memory usage.
    /// </summary>
    public void Trim()
    {
        foreach (var queue in _pool.Values)
        {
            while (queue.TryDequeue(out var rt))
            {
                TotalCount--;
                rt.Dispose();
            }
        }
        _pool.Clear();
    }

    /// <summary>
    /// Disposes all render targets in the pool (does not affect acquired RTs).
    /// Should be called on game exit or device reset.
    /// </summary>
    public void DisposeAll() => Trim();

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Trim();
        }
    }
}
