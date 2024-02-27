using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Primitives3D;

public class SpherePrimitive : GeometricPrimitive
{
#if EDITOR
    private float _diameter;
    private int _tessellation;
#endif

    public SpherePrimitive(float diameter = 1f, int tessellation = 16)
    {
        if (tessellation < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(tessellation));
        }

        uint verticalSegments = (uint)tessellation;
        uint horizontalSegments = (uint)tessellation * 2;

        var radius = diameter / 2;

#if EDITOR
        _diameter = diameter;
        _tessellation = tessellation;
#endif

        // Create rings of vertices at progressively higher latitudes.
        for (int i = 0; i <= verticalSegments; i++)
        {
            float v = 1.0f - (float)i / verticalSegments;

            var latitude = (float)((i * Math.PI / verticalSegments) - Math.PI / 2.0);
            var dy = (float)Math.Sin(latitude);
            var dxz = (float)Math.Cos(latitude);

            // Create a single ring of vertices at this latitude.
            for (int j = 0; j <= horizontalSegments; j++)
            {
                float u = (float)j / horizontalSegments;

                var longitude = (float)(j * 2.0 * Math.PI / horizontalSegments);
                var dx = (float)Math.Sin(longitude);
                var dz = (float)Math.Cos(longitude);

                dx *= dxz;
                dz *= dxz;

                var normal = new Vector3(dx, dy, dz);
                var textureCoordinate = new Vector2(u, v);

                AddVertex(normal * radius, normal, textureCoordinate);
            }
        }

        // Fill the index buffer with triangles joining each pair of latitude rings.
        uint stride = horizontalSegments + 1;

        for (uint i = 0; i < verticalSegments; i++)
        {
            for (uint j = 0; j <= horizontalSegments; j++)
            {
                uint nextI = i + 1;
                uint nextJ = (j + 1) % stride;

                AddIndex(i * stride + j);
                AddIndex(nextI * stride + j);
                AddIndex(i * stride + nextJ);

                AddIndex(i * stride + nextJ);
                AddIndex(nextI * stride + j);
                AddIndex(nextI * stride + nextJ);
            }
        }
    }
}