using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D;

public class CylinderPrimitive : GeometricPrimitive
{
    public CylinderPrimitive(float height = 1f, float diameter = 1f, int tessellation = 32)
    {
        if (tessellation < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(tessellation), "tessellation must be >= 3");
        }

        height /= 2;

        var topOffset = Vector3.UnitY * height;

        float radius = diameter / 2;
        uint stride = (uint)tessellation + 1;

        // Create a ring of triangles around the outside of the cylinder.
        for (uint i = 0; i <= tessellation; i++)
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
    }
}