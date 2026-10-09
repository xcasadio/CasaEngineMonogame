using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GizmoTools;

/// <summary>
/// The world-space ray through a screen point, as CasaEngine's <c>RayHelper.CalculateRayFromScreenCoordinate</c>
/// computes it (GizmoTool does not reference CasaEngine).
/// </summary>
public static class GizmoRay
{
    /// <summary>
    /// The ray through <paramref name="position"/> (pixels of <paramref name="viewport"/>), starting on the near plane.
    /// Only the viewport position and size are read, not its depth range. The two points that give the direction are
    /// unprojected in view space, at clip depths 0 and 0.5, then moved to world space: the direction keeps its precision
    /// with the camera far from the origin, and stays defined for a projection without a far plane, where clip depth 1
    /// is the point at infinity.
    /// </summary>
    public static Ray FromScreen(Vector2 position, Viewport viewport, Matrix view, Matrix projection)
    {
        float clipX = (position.X - viewport.X) / viewport.Width * 2f - 1f;
        float clipY = -((position.Y - viewport.Y) / viewport.Height * 2f - 1f);

        Matrix inverseProjection = Matrix.Invert(projection);
        Vector3 nearPoint = UnprojectToViewSpace(clipX, clipY, 0f, in inverseProjection);
        Vector3 farPoint = UnprojectToViewSpace(clipX, clipY, 0.5f, in inverseProjection);

        Matrix inverseView = Matrix.Invert(view);
        Vector3 origin = Vector3.Transform(nearPoint, inverseView);
        Vector3 direction = Vector3.TransformNormal(farPoint - nearPoint, inverseView);
        direction.Normalize();

        return new Ray(origin, direction);
    }

    private static Vector3 UnprojectToViewSpace(float clipX, float clipY, float clipZ, in Matrix inverseProjection)
    {
        Vector4 point = Vector4.Transform(new Vector4(clipX, clipY, clipZ, 1f), inverseProjection);
        return new Vector3(point.X, point.Y, point.Z) / point.W;
    }
}
