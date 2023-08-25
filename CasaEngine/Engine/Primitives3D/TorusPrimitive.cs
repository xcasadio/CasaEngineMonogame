using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D;

public class TorusPrimitive : GeometricPrimitive
{
    public TorusPrimitive(GraphicsDevice graphicsDevice, float diameter = 1, float thickness = 0.333f, int tessellation = 32) : base(GeometricPrimitiveType.Torus)
    {
        if (tessellation < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(tessellation));
        }

        int stride = tessellation + 1;

        // First we loop around the main ring of the torus.
        for (int i = 0; i <= tessellation; i++)
        {
            float u = (float)i / tessellation;

            float outerAngle = i * MathHelper.TwoPi / tessellation - MathHelper.PiOver2;

            // Create a transform matrix that will align geometry to
            // slice perpendicularly though the current ring position.
            var transform = Matrix.CreateTranslation(diameter / 2, 0, 0) * Matrix.CreateRotationY(outerAngle);

            // Now we loop along the other axis, around the side of the tube.
            for (int j = 0; j <= tessellation; j++)
            {
                float v = 1 - (float)j / tessellation;

                float innerAngle = j * MathHelper.TwoPi / tessellation + MathHelper.Pi;
                float dx = (float)Math.Cos(innerAngle), dy = (float)Math.Sin(innerAngle);

                // Create a vertex.
                var normal = new Vector3(dx, dy, 0);
                var position = normal * thickness / 2;
                var textureCoordinate = new Vector2(u, v);

                Vector3.Transform(ref position, ref transform, out position);
                Vector3.TransformNormal(ref normal, ref transform, out normal);

                AddVertex(position, normal, textureCoordinate);

                // And create indices for two triangles.
                int nextI = (i + 1) % stride;
                int nextJ = (j + 1) % stride;

                AddIndex(i * stride + j);
                AddIndex(i * stride + nextJ);
                AddIndex(nextI * stride + j);

                AddIndex(i * stride + nextJ);
                AddIndex(nextI * stride + nextJ);
                AddIndex(nextI * stride + j);
            }
        }

        InitializePrimitive(graphicsDevice);
    }
}