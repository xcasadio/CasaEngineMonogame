using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D
{
    public class PlanePrimitive : GeometricPrimitive
    {
#if EDITOR
        private Vector2 _scale;
        private int _tessellationHorizontal, _tessellationVertical;
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
        public PlanePrimitive(GraphicsDevice graphicsDevice, float sizeH, float sizeV, int tessellationHorizontal, int tessellationVertical)
            : base(GeometricPrimitiveType.Plane)
        {
            if (tessellationHorizontal < 1)
            {
                throw new ArgumentOutOfRangeException("PlanePrimitive() : tessellationHorizontal_");
            }

            if (tessellationVertical < 1)
            {
                throw new ArgumentOutOfRangeException("PlanePrimitive() : tessellationVertical_");
            }

#if EDITOR
            _scale = new Vector2(sizeH, sizeV);
            _tessellationHorizontal = tessellationHorizontal;
            _tessellationVertical = tessellationVertical;
#endif

            var uv = Vector2.Zero;

            var verticalSegments = tessellationVertical + 1;
            var horizontalSegments = tessellationHorizontal + 1;

            var sizeHBy2 = sizeH / 2.0f;
            var sizeVBy2 = sizeV / 2.0f;
            var stepH = sizeH / tessellationHorizontal;
            var stepV = sizeV / tessellationVertical;

            //increment to compute uv
            int stepHTotal = 0, stepVTotal;

            //Compute Vertex
            for (var dx = -sizeHBy2; dx <= sizeHBy2; dx += stepH)
            {
                stepVTotal = 0;

                for (var dz = -sizeVBy2; dz <= sizeVBy2; dz += stepV)
                {
                    uv.X = (float)stepHTotal / tessellationHorizontal;
                    uv.Y = (float)stepVTotal / tessellationVertical;
                    AddVertex(new Vector3(dx, 0.0f, dz), Vector3.UnitY, uv);
                    stepVTotal++;
                }

                stepHTotal++;
            }

            //Compute Index : compute quad
            for (var iy = 0; iy < tessellationVertical; iy++)
            {
                for (var ix = 0; ix < tessellationHorizontal; ix++)
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
