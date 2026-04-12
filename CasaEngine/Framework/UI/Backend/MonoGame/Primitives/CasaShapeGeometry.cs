using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Primitives;

internal static class CasaShapeGeometry
{
    public static Vector2[] GetCircleVertices(Vector2 origin, double radius, int sides, double angleOffset = 0.0)
    {
        const double max = 2.0 * Math.PI;
        Vector2[] points = new Vector2[sides];
        double step = max / sides;
        double theta = angleOffset;

        for (int i = 0; i < sides; i++)
        {
            points[i] = origin + new Vector2((float)(radius * Math.Cos(theta)), (float)(radius * Math.Sin(theta)));
            theta += step;
        }

        return points;
    }

    public static Vector2[] GetEllipseVertices(Vector2 origin, float radiusX, float radiusY, int sides)
    {
        Vector2[] vertices = new Vector2[sides];
        double t = 0.0;
        double dt = 2.0 * Math.PI / sides;
        for (int i = 0; i < sides; i++, t += dt)
        {
            float x = (float)(radiusX * Math.Cos(t));
            float y = (float)(radiusY * Math.Sin(t));
            vertices[i] = origin + new Vector2(x, y);
        }

        return vertices;
    }
}