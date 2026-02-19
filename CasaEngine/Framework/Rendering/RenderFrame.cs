using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Holds the camera data for a single render pass.
/// Passed to renderers when flushing per-view.
/// </summary>
public readonly struct RenderFrame
{
    public Matrix View { get; init; }
    public Matrix Projection { get; init; }
    public Matrix ViewProjection { get; init; }
    public Vector3 CameraPosition { get; init; }

    /// <summary>Screen-space viewport rectangle (in pixels).</summary>
    public Rectangle ViewportRect { get; init; }

    public RenderFrame(Matrix view, Matrix projection, Vector3 cameraPosition, Rectangle viewportRect)
    {
        View = view;
        Projection = projection;
        ViewProjection = view * projection;
        CameraPosition = cameraPosition;
        ViewportRect = viewportRect;
    }
}
