//-----------------------------------------------------------------------------
// CylinderPrimitive.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D
{
    /// <summary>
    /// Geometric primitive class for drawing cylinders.
    /// </summary>
    public class CylinderPrimitive : GeometricPrimitive
    {
        /// <summary>
        /// Constructs a new cylinder primitive, using default settings.
        /// </summary>
        public CylinderPrimitive(GraphicsDevice graphicsDevice)
            : this(graphicsDevice, 1, 1, 32)
        {
        }


        /// <summary>
        /// Constructs a new cylinder primitive,
        /// with the specified size and tessellation level.
        /// </summary>
        public CylinderPrimitive(GraphicsDevice graphicsDevice,
                                 float height, float diameter, int tessellation)
            : base(GeometricPrimitiveType.Cylinder)
        {
            if (tessellation < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(tessellation));
            }

            height /= 2;

            float radius = diameter / 2;

            Vector2 uv;

            // Create a ring of triangles around the outside of the cylinder.
            for (int i = 0; i < tessellation; i++)
            {
                Vector3 normal = GetCircleVector(i, tessellation, out uv);

                AddVertex(normal * radius + Vector3.Up * height, normal, uv);
                AddVertex(normal * radius + Vector3.Down * height, normal, uv);

                AddIndex(i * 2);
                AddIndex(i * 2 + 1);
                AddIndex((i * 2 + 2) % (tessellation * 2));

                AddIndex(i * 2 + 1);
                AddIndex((i * 2 + 3) % (tessellation * 2));
                AddIndex((i * 2 + 2) % (tessellation * 2));
            }

            // Create flat triangle fan caps to seal the top and bottom.
            CreateCap(tessellation, height, radius, Vector3.Up);
            CreateCap(tessellation, height, radius, Vector3.Down);

            InitializePrimitive(graphicsDevice);
        }


        /// <summary>
        /// Helper method creates a triangle fan to close the ends of the cylinder.
        /// </summary>
        void CreateCap(int tessellation, float height, float radius, Vector3 normal)
        {
            Vector2 uv;

            // Create cap indices.
            for (int i = 0; i < tessellation - 2; i++)
            {
                if (normal.Y > 0)
                {
                    AddIndex(CurrentVertex);
                    AddIndex(CurrentVertex + (i + 1) % tessellation);
                    AddIndex(CurrentVertex + (i + 2) % tessellation);
                }
                else
                {
                    AddIndex(CurrentVertex);
                    AddIndex(CurrentVertex + (i + 2) % tessellation);
                    AddIndex(CurrentVertex + (i + 1) % tessellation);
                }
            }

            // Create cap vertices.
            for (int i = 0; i < tessellation; i++)
            {
                Vector3 position = GetCircleVector(i, tessellation, out uv) * radius +
                                   normal * height;

                AddVertex(position, normal, uv);
            }
        }


        /// <summary>
        /// Helper method computes a point on a circle.
        /// </summary>
        static Vector3 GetCircleVector(int i, int tessellation, out Vector2 uv_)
        {
            float angle = i * MathHelper.TwoPi / tessellation;

            float dx = (float)Math.Cos(angle);
            float dz = (float)Math.Sin(angle);

            uv_ = new Vector2(dx, dz);
            //uv_.Normalize();

            return new Vector3(dx, 0, dz);
        }
    }
}
