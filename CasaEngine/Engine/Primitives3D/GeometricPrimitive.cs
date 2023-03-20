//-----------------------------------------------------------------------------
// Geometric3dPrimitive.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D
{
    /// <summary>
    /// Base class for simple geometric primitive models. This provides a vertex
    /// buffer, an index buffer, plus methods for drawing the model. Classes for
    /// specific types of primitive (CubePrimitive, SpherePrimitive, etc.) are
    /// derived from this common base, and use the AddVertex and AddIndex methods
    /// to specify their geometry.
    /// </summary>
    public abstract class GeometricPrimitive : IDisposable
    {
        private GeometricPrimitiveType _type;

        // During the process of constructing a primitive model, vertex
        // and index data is stored on the CPU in these managed lists.
        private readonly List<VertexPositionNormalTexture> _vertices = new();
        private readonly List<ushort> _indices = new();

        // Once all the geometry has been specified, the InitializePrimitive
        // method copies the vertex and index data into these buffers, which
        // store it on the GPU ready for efficient rendering.
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private BasicEffect _basicEffect;

        /// <summary>
        /// Gets VertexDeclaration
        /// </summary>
        public VertexDeclaration VertexDeclaration => VertexPositionNormalTexture.VertexDeclaration;

        /// <summary>
        /// 
        /// </summary>
        protected GeometricPrimitive(GeometricPrimitiveType type)
        {
            _type = type;
        }

        public StaticMesh CreateMesh()
        {
            var mesh = new StaticMesh();
            mesh.AddVertices(_vertices);
            mesh.AddIndices(_indices);

            return mesh;
        }

        /// <summary>
        /// Adds a new vertex to the primitive model. This should only be called
        /// during the initialization process, before InitializePrimitive.
        /// </summary>
        protected void AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _vertices.Add(new VertexPositionNormalTexture(position, normal, uv));
        }

        /// <summary>
        /// Adds a new index to the primitive model. This should only be called
        /// during the initialization process, before InitializePrimitive.
        /// </summary>
        protected void AddIndex(int index)
        {
            if (index > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            _indices.Add((ushort)index);
        }

        /// <summary>
        /// Queries the index of the current vertex. This starts at
        /// zero, and increments every time AddVertex is called.
        /// </summary>
        protected int CurrentVertex => _vertices.Count;

        /// <summary>
        /// Once all the geometry has been specified by calling AddVertex and AddIndex,
        /// this method copies the vertex and index data into GPU format buffers, ready
        /// for efficient rendering.
        protected void InitializePrimitive(GraphicsDevice graphicsDevice)
        {
            _vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), _vertices.Count, BufferUsage.None);
            _vertexBuffer.SetData(_vertices.ToArray());

            _indexBuffer = new IndexBuffer(graphicsDevice, typeof(ushort), _indices.Count, BufferUsage.None);
            _indexBuffer.SetData(_indices.ToArray());

            _basicEffect = new BasicEffect(graphicsDevice);

            _basicEffect.EnableDefaultLighting();
            _basicEffect.PreferPerPixelLighting = true;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~GeometricPrimitive()
        {
            Dispose(false);
        }

        /// <summary>
        /// Frees resources used by this object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Frees resources used by this object.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            if (_vertexBuffer != null)
            {
                _vertexBuffer.Dispose();
            }

            if (_indexBuffer != null)
            {
                _indexBuffer.Dispose();
            }

            if (_basicEffect != null)
            {
                _basicEffect.Dispose();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="effect"></param>
        public void DrawOnlyVertex()
        {
            var graphicsDevice = EngineComponents.Game.GraphicsDevice;

            graphicsDevice.SetVertexBuffer(_vertexBuffer);
            graphicsDevice.Indices = _indexBuffer;

            var primitiveCount = _indices.Count / 3;

            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, _vertices.Count, primitiveCount);
        }

        /// <summary>
        /// Draws the primitive model, using the specified effect. Unlike the other
        /// Draw overload where you just specify the world/view/projection matrices
        /// and color, this method does not set any renderstates, so you must make
        /// sure all states are set to sensible values before you call it.
        /// </summary>
        public void Draw(Effect effect)
        {
            var graphicsDevice = effect.GraphicsDevice;

            graphicsDevice.SetVertexBuffer(_vertexBuffer);
            graphicsDevice.Indices = _indexBuffer;

            //effect.Parameters["WorldViewProj"] = ;

            foreach (var effectPass in effect.CurrentTechnique.Passes)
            {
                effectPass.Apply();
                var primitiveCount = _indices.Count / 3;
                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
            }
        }

        /// <summary>
        /// Draws the primitive model, using a BasicEffect shader with default
        /// lighting. Unlike the other Draw overload where you specify a custom
        /// effect, this method sets important renderstates to sensible values
        /// for 3D model rendering, so you do not need to set these states before
        /// you call it.
        /// </summary>
        public void Draw(Matrix world, Matrix view, Matrix projection, Color color)
        {
            // Set BasicEffect parameters.
            _basicEffect.World = world;
            _basicEffect.View = view;
            _basicEffect.Projection = projection;
            _basicEffect.DiffuseColor = color.ToVector3();
            _basicEffect.Alpha = color.A / 255.0f;

            // Set important renderstates.
            /*RenderState renderState = basicEffect.GraphicsDevice.RenderState;

            renderState.AlphaTestEnable = false;
            renderState.DepthBufferEnable = true;
            renderState.DepthBufferFunction = CompareFunction.LessEqual;

            if (color.A < 255)
            {
                // Set renderstates for alpha blended rendering.
                renderState.AlphaBlendEnable = true;
                renderState.AlphaBlendOperation = BlendFunction.Add;
                renderState.SourceBlend = Blend.SourceAlpha;
                renderState.DestinationBlend = Blend.InverseSourceAlpha;
                renderState.SeparateAlphaBlendEnabled = false;
                renderState.DepthBufferWriteEnable = false;
            }
            else
            {
                // Set renderstates for opaque rendering.
                renderState.AlphaBlendEnable = false;
                renderState.DepthBufferWriteEnable = true;
            }*/

            // Draw the model, using BasicEffect.
            Draw(_basicEffect);
        }

#if EDITOR
        public List<Vector3> Vertex
        {
            get
            {
                return _vertices.Select(v => v.Position).ToList();
            }
        }
#endif

#if BINARY_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binR_"></param>
		/// <param name="linearize"></param>
		static public GeometricPrimitive Load(GraphicsDevice graphicDevice_, BinaryReader binR_, bool linearize)
		{
			Geometric3DPrimitiveType type = (Geometric3DPrimitiveType)binR_.ReadInt32();

			switch (type)
			{
				case Geometric3DPrimitiveType.Plane:
					return PlanePrimitive.LoadPlane(graphicDevice_, binR_, linearize);

				case Geometric3DPrimitiveType.Sphere:
					return SpherePrimitive.LoadSphere(graphicDevice_, binR_, linearize);

				case Geometric3DPrimitiveType.Cube:
					return BoxPrimitive.LoadCube(graphicDevice_, binR_, linearize);

				default:
					throw new ArgumentException("Geometric3dPrimitive.Load() : Geometric3DPrimitiveType is unknown");
			}
		}

#elif XML_FORMAT

		/// <summary>
		/// 
		/// </summary>
		/// <param name="graphicDevice_"></param>
		/// <param name="el_"></param>
		/// <returns></returns>
		static public GeometricPrimitive Load(GraphicsDevice graphicDevice_, XmlElement el_)
		{
			XmlElement meshNode = (XmlElement)el_.SelectSingleNode("Mesh");
			Geometric3DPrimitiveType type = (Geometric3DPrimitiveType)int.Parse(meshNode.Attributes["type"].Value);

			switch (type)
			{
				/*case Geometric3DPrimitiveType.Plane:
					return PlanePrimitive.LoadPlane(graphicDevice_, el_);*/

				case Geometric3DPrimitiveType.Sphere:
					return SpherePrimitive.LoadSphere(graphicDevice_, meshNode);

				case Geometric3DPrimitiveType.Cube:
					return BoxPrimitive.LoadCube(graphicDevice_, meshNode);

				default:
					throw new ArgumentException("Geometric3dPrimitive.Load() : Geometric3DPrimitiveType is unknown");
			}
		}

#endif

    }
}
