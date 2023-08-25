using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D;

public class CylinderPrimitive : GeometricPrimitive
{
    public CylinderPrimitive(GraphicsDevice graphicsDevice, float height = 1f, float diameter = 1f, int tessellation = 32) :
        base(GeometricPrimitiveType.Cylinder)
    {
        if (tessellation < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(tessellation), "tessellation must be >= 3");
        }

        height /= 2;

        var topOffset = Vector3.UnitY * height;

        float radius = diameter / 2;
        int stride = tessellation + 1;

        // Create a ring of triangles around the outside of the cylinder.
        for (int i = 0; i <= tessellation; i++)
        {
            var normal = GetCircleVector(i, tessellation);

            var sideOffset = normal * radius;

            var textureCoordinate = new Vector2((float)i / tessellation, 0);

            AddVertex(sideOffset + topOffset, normal, textureCoordinate);
            AddVertex(sideOffset - topOffset, normal, textureCoordinate + Vector2.UnitY);

            AddIndex(i * 2);
            AddIndex((i * 2 + 2) % (stride * 2));
            AddIndex(i * 2 + 1);

            AddIndex(i * 2 + 1);
            AddIndex((i * 2 + 2) % (stride * 2));
            AddIndex((i * 2 + 3) % (stride * 2));
        }

        // Create flat triangle fan caps to seal the top and bottom.
        CreateCylinderCap(tessellation, height, radius, true);
        CreateCylinderCap(tessellation, height, radius, false);

        InitializePrimitive(graphicsDevice);
    }

    private static Vector3 GetCircleVector(int i, int tessellation)
    {
        var angle = (float)(i * 2.0 * Math.PI / tessellation);
        var dx = (float)Math.Sin(angle);
        var dz = (float)Math.Cos(angle);

        return new Vector3(dx, 0, dz);
    }

    private void CreateCylinderCap(int tessellation, float height, float radius, bool isTop)
    {
        // Create cap indices.
        for (int i = 0; i < tessellation - 2; i++)
        {
            int i1 = (i + 1) % tessellation;
            int i2 = (i + 2) % tessellation;

            if (isTop)
            {
                (i1, i2) = (i2, i1);
            }

            int vbase = CurrentVertex;
            AddIndex(vbase);
            AddIndex(vbase + i1);
            AddIndex(vbase + i2);
        }

        // Which end of the cylinder is this?
        var normal = Vector3.UnitY;
        var textureScale = new Vector2(-0.5f);

        if (!isTop)
        {
            normal = -normal;
            textureScale.X = -textureScale.X;
        }

        // Create cap vertices.
        for (int i = 0; i < tessellation; i++)
        {
            var circleVector = GetCircleVector(i, tessellation);
            var position = (circleVector * radius) + (normal * height);
            var textureCoordinate = new Vector2(circleVector.X * textureScale.X + 0.5f, circleVector.Z * textureScale.Y + 0.5f);

            AddVertex(position, normal, textureCoordinate);
        }
    }
}