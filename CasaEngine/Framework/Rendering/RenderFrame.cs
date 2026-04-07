using Microsoft.Xna.Framework;
using CasaEngine.Framework.Rendering.Environment;

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

    /// <summary>Effective environment data for the current view.</summary>
    public ResolvedEnvironmentSettings Environment { get; init; }

    public RenderFrame(Matrix view, Matrix projection, Vector3 cameraPosition, Rectangle viewportRect)
        : this(view, projection, cameraPosition, viewportRect, default)
    {
    }

    public RenderFrame(
        Matrix view,
        Matrix projection,
        Vector3 cameraPosition,
        Rectangle viewportRect,
        in ResolvedEnvironmentSettings environment)
    {
        View = view;
        Projection = projection;
        ViewProjection = view * projection;
        CameraPosition = cameraPosition;
        ViewportRect = viewportRect;
        Environment = environment;
    }
}
