//-----------------------------------------------------------------------------
// CubePrimitive.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D
{
    /// <summary>
    /// Geometric primitive class for drawing cubes.
    /// </summary>
    public
#if EDITOR
    partial
#endif
    class BoxPrimitive : Geometric3DPrimitive
    {
        /// <summary>
        /// Constructs a new cube primitive, using default settings.
        /// </summary>
        public BoxPrimitive(GraphicsDevice graphicsDevice)
            : this(graphicsDevice, 1, 1, 1)
        {
        }


        /// <summary>
		/// Constructs a new cube primitive, with the specified size.
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="width_">X</param>
        /// <param name="height_">Y</param>
        /// <param name="length_">Z</param>
        public BoxPrimitive(GraphicsDevice graphicsDevice, float width_, float height_, float length_)
            : base(Geometric3DPrimitiveType.Cube)
        {
#if EDITOR
            m_Width = width_;
            m_Height = height_;
            m_Length = length_;
#endif

            // A box has six faces, each one pointing in a different direction.
            /*Vector3[] normals =
            {
                new Vector3(0, 0, 1),
                new Vector3(0, 0, -1),
                new Vector3(1, 0, 0),
                new Vector3(-1, 0, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, -1, 0),
            };

            // Create each face in turn.
            foreach (Vector3 normal in normals)
            {
                // Get two vectors perpendicular to the face normal and to each other.
                Vector3 side1 = new Vector3(normal.Y, normal.Z, normal.X);
                Vector3 side2 = Vector3.Cross(normal, side1);

                // Six indices (two triangles) per face.
                AddIndex(CurrentVertex + 0);
                AddIndex(CurrentVertex + 1);
                AddIndex(CurrentVertex + 2);

                AddIndex(CurrentVertex + 0);
                AddIndex(CurrentVertex + 2);
                AddIndex(CurrentVertex + 3);

                // Four vertices per face.
                AddVertex((normal - side1 - side2) * size / 2, normal, Vector2.Zero);
				AddVertex((normal - side1 + side2) * size / 2, normal, Vector2.UnitY);
                AddVertex((normal + side1 + side2) * size / 2, normal, Vector2.One);
                AddVertex((normal + side1 - side2) * size / 2, normal, Vector2.UnitX);
            }*/

            //front
            AddVertex(false);
            AddVertex((Vector3.UnitZ * length_ - Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, Vector3.UnitZ, Vector2.Zero);
            AddVertex((Vector3.UnitZ * length_ - Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, Vector3.UnitZ, Vector2.UnitX);
            AddVertex((Vector3.UnitZ * length_ + Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, Vector3.UnitZ, Vector2.UnitY);
            AddVertex((Vector3.UnitZ * length_ + Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, Vector3.UnitZ, Vector2.One);

            //back
            AddVertex(true);
            AddVertex((-Vector3.UnitZ * length_ - Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, -Vector3.UnitZ, Vector2.Zero);
            AddVertex((-Vector3.UnitZ * length_ - Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, -Vector3.UnitZ, Vector2.UnitX);
            AddVertex((-Vector3.UnitZ * length_ + Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, -Vector3.UnitZ, Vector2.UnitY);
            AddVertex((-Vector3.UnitZ * length_ + Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, -Vector3.UnitZ, Vector2.One);

            //up
            AddVertex(true);
            AddVertex((-Vector3.UnitZ * length_ + Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, Vector3.UnitY, Vector2.Zero);
            AddVertex((-Vector3.UnitZ * length_ + Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, Vector3.UnitY, Vector2.UnitX);
            AddVertex((Vector3.UnitZ * length_ + Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, Vector3.UnitY, Vector2.UnitY);
            AddVertex((Vector3.UnitZ * length_ + Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, Vector3.UnitY, Vector2.One);

            //bottom
            AddVertex(false);
            AddVertex((-Vector3.UnitZ * length_ - Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, -Vector3.UnitY, Vector2.Zero);
            AddVertex((-Vector3.UnitZ * length_ - Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, -Vector3.UnitY, Vector2.UnitX);
            AddVertex((Vector3.UnitZ * length_ - Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, -Vector3.UnitY, Vector2.UnitY);
            AddVertex((Vector3.UnitZ * length_ - Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, -Vector3.UnitY, Vector2.One);

            //right
            AddVertex(true);
            AddVertex((-Vector3.UnitZ * length_ - Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, Vector3.UnitX, Vector2.Zero);
            AddVertex((Vector3.UnitZ * length_ - Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, Vector3.UnitX, Vector2.UnitX);
            AddVertex((-Vector3.UnitZ * length_ + Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, Vector3.UnitX, Vector2.UnitY);
            AddVertex((Vector3.UnitZ * length_ + Vector3.UnitY * height_ + Vector3.UnitX * width_) / 2, Vector3.UnitX, Vector2.One);

            //left
            AddVertex(false);
            AddVertex((-Vector3.UnitZ * length_ - Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, -Vector3.UnitX, Vector2.Zero);
            AddVertex((Vector3.UnitZ * length_ - Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, -Vector3.UnitX, Vector2.UnitX);
            AddVertex((-Vector3.UnitZ * length_ + Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, -Vector3.UnitX, Vector2.UnitY);
            AddVertex((Vector3.UnitZ * length_ + Vector3.UnitY * height_ - Vector3.UnitX * width_) / 2, -Vector3.UnitX, Vector2.One);

            InitializePrimitive(graphicsDevice);
        }

        /// <summary>
        /// 
        /// </summary>
        void AddVertex(bool dir_)
        {
            if (dir_)
            {
                AddIndex(CurrentVertex + 0);
                AddIndex(CurrentVertex + 1);
                AddIndex(CurrentVertex + 2);

                AddIndex(CurrentVertex + 1);
                AddIndex(CurrentVertex + 3);
                AddIndex(CurrentVertex + 2);
            }
            else
            {
                AddIndex(CurrentVertex + 0);
                AddIndex(CurrentVertex + 2);
                AddIndex(CurrentVertex + 1);

                AddIndex(CurrentVertex + 1);
                AddIndex(CurrentVertex + 2);
                AddIndex(CurrentVertex + 3);
            }
        }

#if BINARY_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binR_"></param>
		/// <param name="linearize"></param>
		static public BoxPrimitive LoadCube(GraphicsDevice graphicDevice_, BinaryReader binR_, bool linearize)
		{
			float widht = binR_.ReadSingle();
			float heigth = binR_.ReadSingle();
			float length = binR_.ReadSingle();
			return new BoxPrimitive(graphicDevice_, widht, heigth, length);
		}

#elif XML_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="graphicDevice_"></param>
		/// <param name="el_"></param>
		/// <returns></returns>
		static public BoxPrimitive LoadCube(GraphicsDevice graphicDevice_, XmlElement el_)
		{
			float widht = float.Parse(el_.Attributes["width"].Value);
			float heigth = float.Parse(el_.Attributes["height"].Value);
			float length = float.Parse(el_.Attributes["length"].Value); ;
			return new BoxPrimitive(graphicDevice_, widht, heigth, length);
		}

#endif

    }
}
