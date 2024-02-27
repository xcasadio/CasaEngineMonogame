using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D;

public class ConePrimitive : GeometricPrimitive
{
    public ConePrimitive(float height = 1f, float diameter = 1f, int tessellation = 32)
    {
        if (tessellation < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(tessellation), "tesselation parameter must be at least 3");
        }

        height /= 2;

        var topOffset = Vector3.UnitY * height;

        var radius = diameter / 2;
        var stride = tessellation + 1;

        // Create a ring of triangles around the outside of the cone.
        for (var i = 0; i <= tessellation; i++)
        {
            var circlevec = GetCircleVector((uint)i, tessellation);

            var sideOffset = Vector3.Multiply(circlevec, radius);

            var u = i / (float)tessellation;

            var textureCoordinate = new Vector2(u, 0f);

            var pt = Vector3.Subtract(sideOffset, topOffset);

            var normal = Vector3.Cross(
                GetCircleTangent(i, tessellation),
                Vector3.Subtract(topOffset, pt));
            normal = Vector3.Normalize(normal);

            // Duplicate the top vertex for distinct normals
            AddVertex(topOffset, normal, Vector2.Zero);
            AddVertex(pt, normal, Vector2.Add(textureCoordinate, Vector2.UnitY));

            AddIndex((uint)i * 2);
            AddIndex((uint)((i * 2 + 3) % (stride * 2)));
            AddIndex((uint)((i * 2 + 1) % (stride * 2)));
        }

        // Create flat triangle fan caps to seal the bottom.
        CreateCylinderCap(tessellation, height, radius, false);
    }

    private Vector3 GetCircleTangent(int i, int tessellation)
    {
        var angle = i * MathHelper.TwoPi / tessellation + MathHelper.PiOver2;
        var dx = (float)Math.Sin(angle);
        var dz = (float)Math.Cos(angle);

        return new Vector3(dx, 0, dz);
    }
}