using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.FrozenFrames;

/// <summary>
/// GPU side of the frozen frame: the retained and captured targets (E19.TR-4a, D-TR4A-3).
/// </summary>
/// <remarks>
/// The retained target receives the whole composed view each frame while the option is on (it plays the
/// role of the scene target of <see cref="RenderPipeline"/>, kept for the target view only); on a capture
/// request, before it is cleared, it still holds the image of the previous frame and is copied to the
/// captured target. Both are lazy, sized to the view, and freed by <see cref="Dispose"/>; the captured one
/// is also freed once the service holds no image and no request, the retained one when the option goes off.
/// A lost or resized target invalidates the retained frame, never throws.
/// </remarks>
internal sealed class FrameRetention : IDisposable
{
    private readonly FrozenFrameService _service;
    private RenderTarget2D _retained;
    private RenderTarget2D _captured;
    private bool _retainedValid;
    private bool _enabled;
    private RenderView _targetView;

    internal FrameRetention(FrozenFrameService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>The view whose frame is retained (the active view).</summary>
    internal RenderView TargetView
    {
        get => _targetView;
        set
        {
            if (!ReferenceEquals(_targetView, value))
            {
                _targetView = value;
                _retainedValid = false;
            }
        }
    }

    /// <summary>The retention option, followed from <see cref="FrozenFrameService.RetainLastFrame"/>.</summary>
    internal bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            if (!value)
            {
                _retainedValid = false;
                DisposeTarget(ref _retained);
            }
        }
    }

    internal FrozenFrameService Service => _service;

    /// <summary>The retained target (the scene target of the target view while the option is on).</summary>
    internal RenderTarget2D RetainedTarget => _retained;

    /// <summary>The captured image, or null when none is held.</summary>
    internal Texture2D CapturedTexture => _captured is { IsDisposed: false } ? _captured : null;

    /// <summary>True when a retained frame of this size exists and is not lost.</summary>
    internal bool IsRetainedFrameValid(int width, int height)
    {
        return _retainedValid
            && _retained is { IsDisposed: false, IsContentLost: false }
            && _retained.Width == width
            && _retained.Height == height;
    }

    /// <summary>Creates or resizes the retained target; a new target holds no valid frame.</summary>
    internal RenderTarget2D EnsureRetained(GraphicsDevice graphicsDevice, int width, int height)
    {
        if (_retained is { IsDisposed: false } && _retained.Width == width && _retained.Height == height)
        {
            return _retained;
        }

        _retained?.Dispose();
        _retainedValid = false;
        _retained = new RenderTarget2D(
            graphicsDevice, width, height, false, SurfaceFormat.Color,
            DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
        return _retained;
    }

    /// <summary>The retained target now holds the complete frame that was just drawn.</summary>
    internal void MarkRetainedValid() => _retainedValid = true;

    /// <summary>The retained target no longer holds the last frame of the target view.</summary>
    internal void InvalidateRetained() => _retainedValid = false;

    /// <summary>
    /// Copies the retained frame to the captured target (opaque, point, the whole rectangle) and completes the
    /// request. The retained frame must be valid for <paramref name="width"/> by <paramref name="height"/>.
    /// </summary>
    internal void CopyRetainedToCaptured(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, int width, int height)
    {
        if (_captured is not { IsDisposed: false } || _captured.Width != width || _captured.Height != height)
        {
            _captured?.Dispose();
            _captured = new RenderTarget2D(
                graphicsDevice, width, height, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }

        graphicsDevice.SetRenderTarget(_captured);
        graphicsDevice.Viewport = new Viewport(0, 0, width, height);
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
            SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        spriteBatch.Draw(_retained, new Rectangle(0, 0, width, height), Color.White);
        spriteBatch.End();

        _service.CompleteCapture(width, height);
    }

    /// <summary>Frees the captured target once the service holds neither an image nor a request.</summary>
    internal void ReleaseCapturedWhenUnused()
    {
        if (_captured != null && !_service.HasImage && !_service.CaptureRequested)
        {
            DisposeTarget(ref _captured);
        }
    }

    public void Dispose()
    {
        DisposeTarget(ref _retained);
        DisposeTarget(ref _captured);
        _retainedValid = false;
    }

    private static void DisposeTarget(ref RenderTarget2D target)
    {
        target?.Dispose();
        target = null;
    }
}
