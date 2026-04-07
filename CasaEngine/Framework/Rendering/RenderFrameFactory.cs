using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering.Environment;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Helper that builds a <see cref="RenderFrame"/> from a camera component.
/// </summary>
public static class RenderFrameFactory
{
    /// <summary>
    /// Creates a <see cref="RenderFrame"/> from <paramref name="camera"/> and an explicit viewport rectangle.
    /// </summary>
    public static RenderFrame From(CameraComponent camera, Rectangle viewportRect)
    {
        return new RenderFrame(
            camera.ViewMatrix,
            camera.ProjectionMatrix,
            camera.Position,
            viewportRect);
    }

    /// <summary>
    /// Creates a <see cref="RenderFrame"/> from <paramref name="camera"/>, an explicit viewport rectangle,
    /// and resolved per-view environment data.
    /// </summary>
    public static RenderFrame From(CameraComponent camera, Rectangle viewportRect, in ResolvedEnvironmentSettings environment)
    {
        return new RenderFrame(
            camera.ViewMatrix,
            camera.ProjectionMatrix,
            camera.Position,
            viewportRect,
            in environment);
    }

    /// <summary>
    /// Creates a <see cref="RenderFrame"/> from <paramref name="camera"/> using its own viewport bounds.
    /// </summary>
    public static RenderFrame From(CameraComponent camera)
    {
        return From(camera, camera.Viewport.Bounds);
    }
}
