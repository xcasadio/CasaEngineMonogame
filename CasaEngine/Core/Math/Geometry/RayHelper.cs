using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Core.Math.Geometry;

public static class RayHelper
{
    public static Ray CalculateRayFromScreenCoordinate(Vector2 pos, Matrix projectionMatrix, Matrix viewMatrix, Viewport viewport)
    {
        // create 2 positions in screenspace using the cursor position. 0 is as
        // close as possible to the camera, 1 is as far away as possible.
        Vector3 nearSource = new Vector3(pos, 0f);
        Vector3 farSource = new Vector3(pos, 1f);

        // use Viewport.Unproject to tell what those two screen space positions
        // would be in world space. we'll need the projection matrix and view
        // matrix, which we have saved as member variables. We also need a world
        // matrix, which can just be identity.
        Vector3 nearPoint = viewport.Unproject(nearSource,
            projectionMatrix, viewMatrix, Matrix.Identity);

        Vector3 farPoint = viewport.Unproject(farSource,
            projectionMatrix, viewMatrix, Matrix.Identity);

        // find the direction vector that goes from the nearPoint to the farPoint
        // and normalize it....
        Vector3 direction = farPoint - nearPoint;
        direction.Normalize();

        // and then create a new ray using nearPoint as the source.
        return new Ray(nearPoint, direction);
    }
}