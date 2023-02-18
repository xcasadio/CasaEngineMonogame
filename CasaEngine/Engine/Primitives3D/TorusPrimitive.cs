//-----------------------------------------------------------------------------
// TorusPrimitive.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D
{
    /// <summary>
    /// Geometric primitive class for drawing toruses.
    /// </summary>
    public class TorusPrimitive : Geometric3DPrimitive
    {
        /// <summary>
        /// Constructs a new torus primitive, using default settings.
        /// </summary>
        public TorusPrimitive(GraphicsDevice graphicsDevice)
            : this(graphicsDevice, 1, 0.333f, 32)
        {
        }


        /// <summary>
        /// Constructs a new torus primitive,
        /// with the specified size and tessellation level.
        /// </summary>
        public TorusPrimitive(GraphicsDevice graphicsDevice,
                              float diameter, float thickness, int tessellation)
            : base(Geometric3DPrimitiveType.Torus)
        {
            if (tessellation < 3)
                throw new ArgumentOutOfRangeException("tessellation");

            Vector2 uv = Vector2.Zero;

            // First we loop around the main ring of the torus.
            for (int i = 0; i < tessellation; i++)
            {
                float outerAngle = i * MathHelper.TwoPi / tessellation;

                // Create a transform matrix that will align geometry to
                // slice perpendicularly though the current ring position.
                Matrix transform = Matrix.CreateTranslation(diameter / 2, 0, 0) *
                                   Matrix.CreateRotationY(outerAngle);

                // Now we loop along the other axis, around the side of the tube.
                for (int j = 0; j < tessellation; j++)
                {
                    float innerAngle = j * MathHelper.TwoPi / tessellation;

                    float dx = (float)Math.Cos(innerAngle);
                    float dy = (float)Math.Sin(innerAngle);

                    // Create a vertex.
                    Vector3 normal = new Vector3(dx, dy, 0);
                    Vector3 position = normal * thickness / 2;

                    position = Vector3.Transform(position, transform);
                    normal = Vector3.TransformNormal(normal, transform);

                    uv.X = (float)(tessellation - 1) / (float)i;
                    uv.Y = (float)(tessellation - 1) / (float)j;
                    uv.Normalize();

                    AddVertex(position, normal, uv);

                    // And create indices for two triangles.
                    int nextI = (i + 1) % tessellation;
                    int nextJ = (j + 1) % tessellation;

                    AddIndex(i * tessellation + j);
                    AddIndex(i * tessellation + nextJ);
                    AddIndex(nextI * tessellation + j);

                    AddIndex(i * tessellation + nextJ);
                    AddIndex(nextI * tessellation + nextJ);
                    AddIndex(nextI * tessellation + j);
                }
            }

            InitializePrimitive(graphicsDevice);
        }
    }
}
