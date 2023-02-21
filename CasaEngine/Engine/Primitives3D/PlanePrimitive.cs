using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D
{
    public class PlanePrimitive : GeometricPrimitive
    {
#if EDITOR
        private Vector2 m_Scale;
        private int m_TessellationHorizontal, m_TessellationVertical;
#endif

        /// <summary>
        /// Constructs a new sphere primitive, using default settings.
        /// </summary>
        public PlanePrimitive(GraphicsDevice graphicsDevice)
            : this(graphicsDevice, 1.0f, 1.0f, 1, 1)
        {
        }

        /// <summary>
        /// Constructs a new plane primitive,
        /// with the specified horizontal and vertical tessellation level.
        /// </summary>
        public PlanePrimitive(GraphicsDevice graphicsDevice,
                                float sizeH_, float sizeV_,
                               int tessellationHorizontal_, int tessellationVertical_)
            : base(GeometricPrimitiveType.Plane)
        {
            if (tessellationHorizontal_ < 1)
            {
                throw new ArgumentOutOfRangeException("PlanePrimitive() : tessellationHorizontal_");
            }

            if (tessellationVertical_ < 1)
            {
                throw new ArgumentOutOfRangeException("PlanePrimitive() : tessellationVertical_");
            }

#if EDITOR
            m_Scale = new Vector2(sizeH_, sizeV_);
            m_TessellationHorizontal = tessellationHorizontal_;
            m_TessellationVertical = tessellationVertical_;
#endif

            Vector2 uv = Vector2.Zero;

            int verticalSegments = tessellationVertical_ + 1;
            int horizontalSegments = tessellationHorizontal_ + 1;

            float sizeHBy2 = sizeH_ / 2.0f;
            float sizeVBy2 = sizeV_ / 2.0f;
            float stepH = sizeH_ / tessellationHorizontal_;
            float stepV = sizeV_ / tessellationVertical_;

            //increment to compute uv
            int stepHTotal = 0, stepVTotal;

            //Compute Vertex
            for (float dx = -sizeHBy2; dx <= sizeHBy2; dx += stepH)
            {
                stepVTotal = 0;

                for (float dz = -sizeVBy2; dz <= sizeVBy2; dz += stepV)
                {
                    uv.X = (float)stepHTotal / tessellationHorizontal_;
                    uv.Y = (float)stepVTotal / tessellationVertical_;
                    AddVertex(new Vector3(dx, 0.0f, dz), Vector3.UnitY, uv);
                    stepVTotal++;
                }

                stepHTotal++;
            }

            //Compute Index : compute quad
            for (int iy = 0; iy < tessellationVertical_; iy++)
            {
                for (int ix = 0; ix < tessellationHorizontal_; ix++)
                {
                    //first triangle
                    AddIndex(ix);
                    AddIndex((iy + 1) * horizontalSegments + ix);
                    AddIndex(ix + 1);

                    //second triangle
                    AddIndex(ix + 1);
                    AddIndex((iy + 1) * horizontalSegments + ix);
                    AddIndex((iy + 1) * horizontalSegments + ix + 1);
                }
            }

            InitializePrimitive(graphicsDevice);
        }

#if BINARY_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binR_"></param>
		/// <param name="linearize"></param>
		static public PlanePrimitive LoadPlane(GraphicsDevice graphicDevice_, BinaryReader binR_, bool linearize)
		{
			int TesH = binR_.ReadInt32();
			int TesV = binR_.ReadInt32();
			Vector2 scale = binR_.ReadVector2();

			return new PlanePrimitive(graphicDevice_, scale.X, scale.Y, TesH, TesV);
		}

#elif XML_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="graphicDevice_"></param>
		/// <param name="el_"></param>
		/// <returns></returns>
		static public PlanePrimitive LoadPlane(GraphicsDevice graphicDevice_, XmlElement el_)
		{
			Vector2 scale = new Vector2();

			int TesH = int.Parse(el_.Attributes["TessellationH"].Value);
			int TesV = int.Parse(el_.Attributes["m_TessellationVertical"].Value);
			((XmlElement)el_.SelectSingleNode("Scale")).Read(ref scale);			

			return new PlanePrimitive(graphicDevice_, scale.X, scale.Y, TesH, TesV);
		}

#endif

    }
}
