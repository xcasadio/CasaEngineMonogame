using Microsoft.Xna.Framework;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shadows;

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

    /// <summary>Effective lighting data for the current view.</summary>
    public LightingContext? Lighting { get; init; }

    /// <summary>Effective forward shadow-map settings for the current view.</summary>
    public ShadowSettings? Shadows { get; init; }

    public RenderFrame(Matrix view, Matrix projection, Vector3 cameraPosition, Rectangle viewportRect)
        : this(view, projection, cameraPosition, viewportRect, default, null, null)
    {
    }

    public RenderFrame(
        Matrix view,
        Matrix projection,
        Vector3 cameraPosition,
        Rectangle viewportRect,
        in ResolvedEnvironmentSettings environment,
        LightingContext? lighting)
        : this(view, projection, cameraPosition, viewportRect, in environment, lighting, null)
    {
    }

    public RenderFrame(
        Matrix view,
        Matrix projection,
        Vector3 cameraPosition,
        Rectangle viewportRect,
        in ResolvedEnvironmentSettings environment,
        LightingContext? lighting,
        ShadowSettings? shadows)
    {
        View = view;
        Projection = projection;
        ViewProjection = view * projection;
        CameraPosition = cameraPosition;
        ViewportRect = viewportRect;
        Environment = environment;
        Lighting = lighting;
        Shadows = shadows;
    }
}
