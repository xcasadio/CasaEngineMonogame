//-----------------------------------------------------------------------------
// GeometricPrimitive.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D
{
    public enum GeometricPrimitiveType
    {
        Cube,
        Cylinder,
        Plane,
        Sphere,
        Teapot,
        Torus
    }

    /// <summary>
    /// Base class for simple geometric primitive models. This provides a vertex
    /// buffer, an index buffer, plus methods for drawing the model. Classes for
    /// specific types of primitive (CubePrimitive, SpherePrimitive, etc.) are
    /// derived from this common base, and use the AddVertex and AddIndex methods
    /// to specify their geometry.
    /// </summary>
    public abstract
#if EDITOR
    partial
#endif
    class GeometricPrimitive
        : IDisposable
    {
        GeometricPrimitiveType m_Type;

        // During the process of constructing a primitive model, vertex
        // and index data is stored on the CPU in these managed lists.
        List<VertexPositionNormalTexture> vertices = new List<VertexPositionNormalTexture>();
        List<ushort> indices = new List<ushort>();

        // Once all the geometry has been specified, the InitializePrimitive
        // method copies the vertex and index data into these buffers, which
        // store it on the GPU ready for efficient rendering.
        VertexBuffer vertexBuffer;
        IndexBuffer indexBuffer;
        BasicEffect basicEffect;

        /// <summary>
        /// Gets VertexDeclaration
        /// </summary>
        public VertexDeclaration VertexDeclaration
        {
            get { return VertexPositionNormalTexture.VertexDeclaration; }
        }

        /// <summary>
        /// 
        /// </summary>
        protected GeometricPrimitive(GeometricPrimitiveType type_)
        {
            m_Type = type_;
        }

        /// <summary>
        /// Adds a new vertex to the primitive model. This should only be called
        /// during the initialization process, before InitializePrimitive.
        /// </summary>
        protected void AddVertex(Vector3 position, Vector3 normal, Vector2 UV_)
        {
            vertices.Add(new VertexPositionNormalTexture(position, normal, UV_));
        }

        /// <summary>
        /// Adds a new index to the primitive model. This should only be called
        /// during the initialization process, before InitializePrimitive.
        /// </summary>
        protected void AddIndex(int index)
        {
            if (index > ushort.MaxValue)
                throw new ArgumentOutOfRangeException("index");

            indices.Add((ushort)index);
        }

        /// <summary>
        /// Queries the index of the current vertex. This starts at
        /// zero, and increments every time AddVertex is called.
        /// </summary>
        protected int CurrentVertex
        {
            get { return vertices.Count; }
        }

        /// <summary>
        /// Once all the geometry has been specified by calling AddVertex and AddIndex,
        /// this method copies the vertex and index data into GPU format buffers, ready
        /// for efficient rendering.
        protected void InitializePrimitive(GraphicsDevice graphicsDevice)
        {
            // Create a vertex buffer, and copy our vertex data into it.
            vertexBuffer = new VertexBuffer(graphicsDevice,
                                            typeof(VertexPositionNormalTexture),
                                            vertices.Count, BufferUsage.None);

            vertexBuffer.SetData(vertices.ToArray());

            // Create an index buffer, and copy our index data into it.
            indexBuffer = new IndexBuffer(graphicsDevice, typeof(ushort),
                                          indices.Count, BufferUsage.None);

            indexBuffer.SetData(indices.ToArray());

            // Create a BasicEffect, which will be used to render the primitive.
            basicEffect = new BasicEffect(graphicsDevice);

            basicEffect.EnableDefaultLighting();
            basicEffect.PreferPerPixelLighting = true;
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
            if (disposing)
            {
                if (vertexBuffer != null)
                    vertexBuffer.Dispose();

                if (indexBuffer != null)
                    indexBuffer.Dispose();

                if (basicEffect != null)
                    basicEffect.Dispose();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="effect"></param>
        public void DrawOnlyVertex()
        {
            GraphicsDevice graphicsDevice = Framework.Game.Engine.Instance.Game.GraphicsDevice;

            graphicsDevice.SetVertexBuffer(vertexBuffer);
            graphicsDevice.Indices = indexBuffer;

            int primitiveCount = indices.Count / 3;

            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
        }

        /// <summary>
        /// Draws the primitive model, using the specified effect. Unlike the other
        /// Draw overload where you just specify the world/view/projection matrices
        /// and color, this method does not set any renderstates, so you must make
        /// sure all states are set to sensible values before you call it.
        /// </summary>
        public void Draw(Effect effect)
        {
            GraphicsDevice graphicsDevice = effect.GraphicsDevice;
            graphicsDevice.SetVertexBuffer(vertexBuffer);
            graphicsDevice.Indices = indexBuffer;

            foreach (EffectPass effectPass in effect.CurrentTechnique.Passes)
            {
                effectPass.Apply();

                int primitiveCount = indices.Count / 3;

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
            basicEffect.World = world;
            basicEffect.View = view;
            basicEffect.Projection = projection;
            basicEffect.DiffuseColor = color.ToVector3();
            basicEffect.Alpha = color.A / 255.0f;

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
            Draw(basicEffect);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="binR_"></param>
        /// <param name="linearize"></param>
        //static public GeometricPrimitive Load(GraphicsDevice graphicDevice_, BinaryReader binR_, bool linearize)
        //{
        //    GeometricPrimitiveType type = (GeometricPrimitiveType)binR_.ReadInt32();
        //
        //    switch (type)
        //    {
        //        case GeometricPrimitiveType.Plane:
        //            return PlanePrimitive.LoadPlane(graphicDevice_, binR_, linearize);
        //
        //        case GeometricPrimitiveType.Sphere:
        //            return SpherePrimitive.LoadSphere(graphicDevice_, binR_, linearize);
        //
        //        case GeometricPrimitiveType.Cube:
        //            return BoxPrimitive.LoadCube(graphicDevice_, binR_, linearize);
        //
        //        default:
        //            throw new ArgumentException("GeometricPrimitive.Load() : GeometricPrimitiveType is unknown");
        //    }
        //}
    }
}
