using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D;

public class CapsulePrimitive : GeometricPrimitive
{
#if EDITOR
    private float _length;
    private float _radius;
    private int _tessellation;
#endif

    public CapsulePrimitive(GraphicsDevice graphicsDevice, float length = 1.0f, float radius = 0.5f, int tessellation = 8, float uScale = 1.0f, float vScale = 1.0f, bool toLeftHanded = false) : base(GeometricPrimitiveType.Sphere)
    {
#if EDITOR
        _length = length;
        _radius = radius;
        _tessellation = tessellation;
#endif

        if (tessellation < 3) tessellation = 3;

        uint verticalSegments = 2 * (uint)tessellation;
        uint horizontalSegments = 4 * (uint)tessellation;

        var vertexCount = 0;
        // Create rings of vertices at progressively higher latitudes.
        for (int i = 0; i < verticalSegments; i++)
        {
            float v;
            float deltaY;
            float latitude;
            if (i < verticalSegments / 2)
            {
                deltaY = -length / 2;
                v = 1.0f - (0.25f * i / (tessellation - 1));
                latitude = (float)((i * Math.PI / (verticalSegments - 2)) - Math.PI / 2.0);
            }
            else
            {
                deltaY = length / 2;
                v = 0.5f - (0.25f * (i - 1) / (tessellation - 1));
                latitude = (float)(((i - 1) * Math.PI / (verticalSegments - 2)) - Math.PI / 2.0);
            }

            var dy = MathF.Sin(latitude);
            var dxz = MathF.Cos(latitude);

            // Create a single ring of vertices at this latitude.
            for (int j = 0; j <= horizontalSegments; j++)
            {
                float u = (float)j / horizontalSegments;

                var longitude = (float)(j * 2.0 * Math.PI / horizontalSegments);
                var dx = MathF.Sin(longitude);
                var dz = MathF.Cos(longitude);

                dx *= dxz;
                dz *= dxz;

                var normal = new Vector3(dx, dy, dz);
                var textureCoordinate = new Vector2(u * uScale, v * vScale);
                var position = radius * normal + new Vector3(0, deltaY, 0);

                AddVertex(position, normal, textureCoordinate);
            }
        }

        // Fill the index buffer with triangles joining each pair of latitude rings.
        uint stride = horizontalSegments + 1;

        int indexCount = 0;
        for (uint i = 0; i < verticalSegments - 1; i++)
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

        InitializePrimitive(graphicsDevice);
    }
}