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
    /// <summary>
    /// Base class for simple geometric primitive models. This provides a vertex
    /// buffer, an index buffer, plus methods for drawing the model. Classes for
    /// specific types of primitive (CubePrimitive, SpherePrimitive, etc.) are
    /// derived from this common base, and use the AddVertex and AddIndex methods
    /// to specify their geometry.
    /// </summary>
    public abstract partial class GeometricPrimitive : IDisposable
    {

        /// <summary>
        /// Get Vertex
        /// </summary>
        public List<Vector3> Vertex
        {
            get
            {
                List<Vector3> list = new List<Vector3>();

                foreach (VertexPositionNormalTexture v in vertices)
                {
                    list.Add(v.Position);
                }

                return list;
            }
        }

        /// <summary>
        /// Draws the primitive model, using the specified effect. Unlike the other
        /// Draw overload where you just specify the world/view/projection matrices
        /// and color, this method does not set any renderstates, so you must make
        /// sure all states are set to sensible values before you call it.
        /// </summary>
		//public void DrawPick(PickBuffer pick_buf, ICamera camera)
        //{
        //    pick_buf.PushVertexBuffer(vertexBuffer);
        //    pick_buf.PushIndexBuffer(indexBuffer);
        //    pick_buf.PushVertexDeclaration(vertexDeclaration);
        //
        //    int primitiveCount = indices.Count / 3;
        //
        //    pick_buf.QueueIndexedPrimitives(
        //        Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList,
        //        0,
        //        0,
        //        0,
        //        vertices.Count,
        //        0,
        //        primitiveCount);
        //
        //    pick_buf.PopVertexDeclaration();
        //    pick_buf.PopIndexBuffer();
        //    pick_buf.PopVertexBuffer();
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="binW_"></param>
        /// <param name="linearize_"></param>
        public virtual void Save(BinaryWriter binW_, bool linearize_)
        {
            binW_.Write((int)m_Type);
        }

    }
}
