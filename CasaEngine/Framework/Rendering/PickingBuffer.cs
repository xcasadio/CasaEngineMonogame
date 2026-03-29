using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Color-based (entity-ID) GPU picking buffer.
///
/// Each selectable entity is rendered into an off-screen RT using a flat shader
/// that encodes its numeric ID as a unique RGBA color. Reading back the pixel
/// under the mouse cursor identifies the entity instantly without CPU raycasting.
///
/// Usage:
/// <code>
/// // Render the picking pass for the active view
/// pickingBuffer.Render(graphicsDevice, view, in frame);
///
/// // On mouse click, query the entity under the cursor
/// var entity = pickingBuffer.Pick(mouseX, mouseY);
/// </code>
///
/// The picking buffer is an optional component — attach one to an editor
/// <see cref="RenderView"/> and integrate the render call in
/// <see cref="OverlayViewPipeline"/>.
/// </summary>
public sealed class PickingBuffer : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private RenderTarget2D?         _renderTarget;
    private bool                    _disposed;

    // CPU-side readback buffer (reused to avoid GC)
    private Color[]? _colorBuffer;

    // Map from encoded color → entity (populated each render pass)
    private readonly Dictionary<int, Entity> _idMap = new();

    /// <summary>Number of entities registered in the last render pass.</summary>
    public int LastEntityCount { get; private set; }

    public PickingBuffer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    /// <summary>
    /// Renders all selectable entities in <paramref name="view"/> into the picking RT
    /// using flat ID-encoded colors.
    ///
    /// <para>
    /// Call this in <see cref="OverlayViewPipeline.RenderGizmos"/> or a dedicated
    /// picking pass, before reading back pixels with <see cref="Pick"/>.
    /// </para>
    /// </summary>
    /// <param name="graphicsDevice">Active graphics device.</param>
    /// <param name="view">The view whose selectable entities to render.</param>
    /// <param name="frame">Camera matrices and viewport for this view.</param>
    public void Render(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)
    {
        EnsureSize(frame.ViewportRect.Width, frame.ViewportRect.Height);

        _idMap.Clear();
        LastEntityCount = 0;

        // TODO: Replace with a real "entity ID" shader pass once the shader is authored.
        // For now this is a stub that prepares the RT and records the entity map.
        //
        // Placeholder implementation:
        graphicsDevice.SetRenderTarget(_renderTarget);
        graphicsDevice.Clear(Color.Black);  // ID 0 = "no entity"

        // When the ID shader is available, iterate view.World selectable components
        // and render each with color = EncodeId(entity.UniqueId).

        graphicsDevice.SetRenderTarget(null);
    }

    /// <summary>
    /// Reads the pixel at <paramref name="screenX"/>, <paramref name="screenY"/>
    /// (relative to the view's viewport) and returns the corresponding entity,
    /// or null if no entity was hit.
    ///
    /// <para>
    /// This performs a GPU→CPU readback which is relatively slow. Cache the result
    /// or throttle the query (e.g. only on mouse-button-down, not per-frame).
    /// </para>
    /// </summary>
    public Entity? Pick(int screenX, int screenY)
    {
        if (_renderTarget == null)
        {
            return null;
        }

        int w = _renderTarget.Width;
        int h = _renderTarget.Height;

        // Clamp to RT bounds
        screenX = Math.Clamp(screenX, 0, w - 1);
        screenY = Math.Clamp(screenY, 0, h - 1);

        // Lazy-allocate / reuse readback buffer for the full RT
        int totalPixels = w * h;
        if (_colorBuffer == null || _colorBuffer.Length < totalPixels)
        {
            _colorBuffer = new Color[totalPixels];
        }

        _renderTarget.GetData(_colorBuffer, 0, totalPixels);

        var pixel = _colorBuffer[screenY * w + screenX];
        int id    = DecodeId(pixel);

        return id > 0 && _idMap.TryGetValue(id, out var entity) ? entity : null;
    }

    /// <summary>
    /// Ensures the picking RT matches the requested dimensions.
    /// Uses <see cref="RenderTargetPool.Shared"/> if available; otherwise allocates directly.
    /// </summary>
    public void EnsureSize(int width, int height)
    {
        if (_renderTarget != null &&
            _renderTarget.Width  == Math.Max(1, width) &&
            _renderTarget.Height == Math.Max(1, height))
        {
            return;
        }

        // Return old RT to pool or dispose it
        if (_renderTarget != null)
        {
            if (RenderTargetPool.Shared != null)
            {
                RenderTargetPool.Shared.Release(_renderTarget);
            }
            else
            {
                _renderTarget.Dispose();
            }
        }

        _colorBuffer = null;  // Force readback buffer reallocation

        // Acquire from pool or create new
        if (RenderTargetPool.Shared != null)
        {
            _renderTarget = RenderTargetPool.Shared.Acquire(
                Math.Max(1, width), Math.Max(1, height),
                SurfaceFormat.Color, DepthFormat.None);
        }
        else
        {
            _renderTarget = new RenderTarget2D(
                _graphicsDevice,
                Math.Max(1, width),
                Math.Max(1, height),
                false,
                SurfaceFormat.Color,
                DepthFormat.None);
        }
    }

    // ---- Color encoding helpers ----

    /// <summary>Encodes an entity numeric ID into an RGBA color (no alpha confusion).</summary>
    public static Color EncodeId(int id)
    {
        // Use RGB only; alpha = 255 so the RT blends correctly.
        byte r = (byte)((id >>  0) & 0xFF);
        byte g = (byte)((id >>  8) & 0xFF);
        byte b = (byte)((id >> 16) & 0xFF);
        return new Color(r, g, b, (byte)255);
    }

    /// <summary>Decodes a color back to an entity ID.</summary>
    public static int DecodeId(Color c) =>
        c.R | (c.G << 8) | (c.B << 16);

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            if (_renderTarget != null)
            {
                if (RenderTargetPool.Shared != null)
                {
                    RenderTargetPool.Shared.Release(_renderTarget);
                }
                else
                {
                    _renderTarget.Dispose();
                }

                _renderTarget = null;
            }
        }
    }
}
