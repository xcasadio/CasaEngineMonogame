//-----------------------------------------------------------------------------
// SpherePrimitive.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D;

/// <summary>
/// Geometric primitive class for drawing spheres.
/// </summary>
public class SpherePrimitive : GeometricPrimitive
{
#if EDITOR
    private float _diameter;
    private int _tessellation;
#endif

    /// <summary>
    /// Constructs a new sphere primitive, using default settings.
    /// </summary>
    public SpherePrimitive(GraphicsDevice graphicsDevice)
        : this(graphicsDevice, 1, 16)
    {
    }

    /// <summary>
    /// Constructs a new sphere primitive,
    /// with the specified size and tessellation level.
    /// </summary>
    public SpherePrimitive(GraphicsDevice graphicsDevice,
        float diameter, int tessellation)
        : base(GeometricPrimitiveType.Sphere)
    {
        if (tessellation < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(tessellation));
        }

        var verticalSegments = tessellation;
        var horizontalSegments = tessellation * 2;

        var radius = diameter / 2;

#if EDITOR
        _diameter = diameter;
        _tessellation = tessellation;
#endif

        var uv = Vector2.Zero;

        // Start with a single vertex at the bottom of the sphere.
        AddVertex(Vector3.Down * radius, Vector3.Down, uv);

        // Create rings of vertices at progressively higher latitudes.
        for (var i = 0; i < verticalSegments - 1; i++)
        {
            var latitude = ((i + 1) * MathHelper.Pi /
                            verticalSegments) - MathHelper.PiOver2;

            var dy = (float)Math.Sin(latitude);
            var dxz = (float)Math.Cos(latitude);

            // Create a single ring of vertices at this latitude.
            for (var j = 0; j < horizontalSegments; j++)
            {
                var longitude = j * MathHelper.TwoPi / horizontalSegments;

                var dx = (float)Math.Cos(longitude) * dxz;
                var dz = (float)Math.Sin(longitude) * dxz;

                var normal = new Vector3(dx, dy, dz);

                uv.X = i / (float)(verticalSegments - 2);
                uv.Y = j / (float)(horizontalSegments - 1);
                //uv.Normalize();

                AddVertex(normal * radius, normal, uv);
            }
        }

        // Finish with a single vertex at the top of the sphere.
        AddVertex(Vector3.Up * radius, Vector3.Up, Vector2.One);

        // Create a fan connecting the bottom vertex to the bottom latitude ring.
        for (var i = 0; i < horizontalSegments; i++)
        {
            AddIndex(0);
            AddIndex(1 + (i + 1) % horizontalSegments);
            AddIndex(1 + i);
        }

        // Fill the sphere body with triangles joining each pair of latitude rings.
        for (var i = 0; i < verticalSegments - 2; i++)
        {
            for (var j = 0; j < horizontalSegments; j++)
            {
                var nextI = i + 1;
                var nextJ = (j + 1) % horizontalSegments;

                AddIndex(1 + i * horizontalSegments + j);
                AddIndex(1 + i * horizontalSegments + nextJ);
                AddIndex(1 + nextI * horizontalSegments + j);

                AddIndex(1 + i * horizontalSegments + nextJ);
                AddIndex(1 + nextI * horizontalSegments + nextJ);
                AddIndex(1 + nextI * horizontalSegments + j);
            }
        }

        // Create a fan connecting the top vertex to the top latitude ring.
        for (var i = 0; i < horizontalSegments; i++)
        {
            AddIndex(CurrentVertex - 1);
            AddIndex(CurrentVertex - 2 - (i + 1) % horizontalSegments);
            AddIndex(CurrentVertex - 2 - i);
        }

        InitializePrimitive(graphicsDevice);
    }

#if BINARY_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binR_"></param>
		/// <param name="linearize"></param>
		static public SpherePrimitive LoadSphere(GraphicsDevice graphicDevice_, BinaryReader binR_, bool linearize)
		{
			int Tes = binR_.ReadInt32();
			float diameter = binR_.ReadSingle();

			return new SpherePrimitive(graphicDevice_, diameter, Tes);
		}

#elif XML_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="graphicDevice_"></param>
		/// <param name="el_"></param>
		/// <returns></returns>
		static public SpherePrimitive LoadSphere(GraphicsDevice graphicDevice_, XmlElement el_)
		{
			int Tes = int.Parse(el_.Attributes["tessellation"].Value);
			float diameter = float.Parse(el_.Attributes["diameter"].Value);

			return new SpherePrimitive(graphicDevice_, diameter, Tes);
		}

#endif
}