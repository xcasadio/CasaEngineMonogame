using System.Resources;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public class Geometry<T> : Drawable, IGeometry<T> where T : struct, IPrimitiveElement, IVertexType
{
    /*
    public T[] VertexData { get; set; }
    public uint[] IndexData { get; set; }

    private readonly Dictionary<GraphicsDevice, VertexBuffer> _vertexBufferCache = new();

    private readonly Dictionary<GraphicsDevice, IndexBuffer> _indexBufferCache = new();

    public override void ConfigureDeviceBuffers(GraphicsDevice device)
    {
        if (_vertexBufferCache.ContainsKey(device) && _indexBufferCache.ContainsKey(device))
        {
            return;
        }

        var vbo = new VertexBuffer(device, VertexData[0].VertexDeclaration, VertexData.Length, BufferUsage.None);
        vbo.SetData(VertexData);

        var ibo = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, IndexData.Length, BufferUsage.None);
        ibo.SetData(IndexData);

        _vertexBufferCache.Add(device, vbo);
        _indexBufferCache.Add(device, ibo);
    }


    protected override Type GetVertexType()
    {
        return typeof(T);
    }

    protected override void DrawImplementation(GraphicsDevice device, List<Tuple<uint, ResourceSet>> resourceSets)
    {
        foreach (var primitiveSet in PrimitiveSets)
        {
            primitiveSet.Draw(device);
        }
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        if (Mesh != null)
        {
            var vertices = Mesh.GetVertices();

            foreach (var vertex in vertices)
            {
                min = Vector3.Min(min, vertex.Position);
                max = Vector3.Max(max, vertex.Position);
            }
        }
        else // default box
        {
            const float length = 0.5f;
            min = Vector3.One * -length;
            max = Vector3.One * length;
        }

        return new BoundingBox(min, max);
        
        //var bb = BoundingBoxHelper.Create();
        //foreach (var pset in PrimitiveSets)
        //{
        //    bb.ExpandBy(pset.GetBoundingBox());
        //}
        //
        //return bb;
    }

    public override VertexBuffer GetVertexBufferForDevice(GraphicsDevice device)
    {
        if (_vertexBufferCache.ContainsKey(device))
        {
            return _vertexBufferCache[device];
        }
        else
        {
            throw new Exception("No vertex buffer for device");
        }
    }

    public override IndexBuffer GetIndexBufferForDevice(GraphicsDevice device)
    {
        if (_indexBufferCache.ContainsKey(device))
        {
            return _indexBufferCache[device];
        }
        else
        {
            throw new Exception("No index buffer for device");
        }
    }*/
}