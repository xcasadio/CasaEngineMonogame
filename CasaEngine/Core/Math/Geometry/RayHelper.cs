using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Core.Math.Geometry;

public static class RayHelper
{
    /// <summary>
    /// The world-space ray through the screen point <paramref name="pos"/> (pixels of <paramref name="viewport"/>),
    /// starting on the near plane. Only the viewport position and size are read, not its depth range.
    /// <para>
    /// The two points that give the direction are unprojected in view space, at clip depths 0 (the near plane) and 0.5,
    /// then moved to world space: their coordinates stay small whatever the distance of the camera to the origin, so the
    /// direction keeps its precision far from it, and clip depth 0.5 stays finite for a projection without a far plane
    /// (FarPlane +infinity), where clip depth 1 is the point at infinity. Valid for perspective and orthographic projections.
    /// </para>
    /// </summary>
    public static Ray CalculateRayFromScreenCoordinate(Vector2 pos, Matrix projectionMatrix, Matrix viewMatrix, Viewport viewport)
    {
        float clipX = (pos.X - viewport.X) / viewport.Width * 2f - 1f;
        float clipY = -((pos.Y - viewport.Y) / viewport.Height * 2f - 1f);

        Matrix inverseProjection = Matrix.Invert(projectionMatrix);
        Vector3 nearPoint = UnprojectToViewSpace(clipX, clipY, 0f, in inverseProjection);
        Vector3 farPoint = UnprojectToViewSpace(clipX, clipY, 0.5f, in inverseProjection);

        Matrix inverseView = Matrix.Invert(viewMatrix);
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