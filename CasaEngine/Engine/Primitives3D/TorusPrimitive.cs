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
    public class TorusPrimitive : GeometricPrimitive
    {
        /// <summary>
        /// Constructs a new torus primitive, using default settings.
        /// </summary>
        public TorusPrimitive(GraphicsDevice graphicsDevice) : this(graphicsDevice, 1, 0.333f, 32)
        {
        }

        /// <summary>
        /// Constructs a new torus primitive,
        /// with the specified size and tessellation level.
        /// </summary>
        public TorusPrimitive(GraphicsDevice graphicsDevice, float diameter, float thickness, int tessellation)
            : base(GeometricPrimitiveType.Torus)
        {
            if (tessellation < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(tessellation));
            }

            var uv = Vector2.Zero;

            // First we loop around the main ring of the torus.
            for (var i = 0; i < tessellation; i++)
            {
                var outerAngle = i * MathHelper.TwoPi / tessellation;

                // Create a transform matrix that will align geometry to
                // slice perpendicularly though the current ring position.
                var transform = Matrix.CreateTranslation(diameter / 2, 0, 0) *
                                Matrix.CreateRotationY(outerAngle);

                // Now we loop along the other axis, around the side of the tube.
                for (var j = 0; j < tessellation; j++)
                {
                    var innerAngle = j * MathHelper.TwoPi / tessellation;

                    var dx = (float)Math.Cos(innerAngle);
                    var dy = (float)Math.Sin(innerAngle);

                    // Create a vertex.
                    var normal = new Vector3(dx, dy, 0);
                    var position = normal * thickness / 2;

                    position = Vector3.Transform(position, transform);
                    normal = Vector3.TransformNormal(normal, transform);

                    uv.X = (float)(tessellation - 1) / (float)i;
                    uv.Y = (float)(tessellation - 1) / (float)j;
                    uv.Normalize();

                    AddVertex(position, normal, uv);

                    // And create indices for two triangles.
                    var nextI = (i + 1) % tessellation;
                    var nextJ = (j + 1) % tessellation;

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
