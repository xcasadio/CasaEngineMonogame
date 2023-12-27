using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public class DrawElements<T> : PrimitiveSet where T : struct, IPrimitiveElement, IVertexType
{
    private readonly uint _indexCount = 0;
    private readonly uint _instanceCount = 0;
    private readonly uint _indexStart = 0;
    private readonly int _vertexOffset = 0;
    private readonly uint _instanceStart = 0;

    private readonly IGeometry<T> _geometry;

    public static IPrimitiveSet Create(
        IGeometry<T> geometry,
        PrimitiveType primitiveTopology,
        uint indexCount,
        uint instanceCount,
        uint indexStart,
        int vertexOffset,
        uint instanceStart)
    {
        return new DrawElements<T>(
            geometry,
            primitiveTopology,
            indexCount,
            instanceCount,
            indexStart,
            vertexOffset,
            instanceStart);
    }

    protected DrawElements(
        IGeometry<T> geometry,
        PrimitiveType primitiveTopology,
        uint indexCount,
        uint instanceCount,
        uint indexStart,
        int vertexOffset,
        uint instanceStart)
        : base(geometry, primitiveTopology)
    {
        _geometry = geometry;
        _indexCount = indexCount;
        _instanceCount = instanceCount;
        _indexStart = indexStart;
        _vertexOffset = vertexOffset;
        _instanceStart = instanceStart;
    }

    public override void Draw(GraphicsDevice device)
    {
        device.DrawIndexedPrimitives(PrimitiveTopology, _vertexOffset, (int)_indexStart, (int)_instanceCount);
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        var bb = BoundingBoxHelper.Create();
        for (var idx = _indexStart; idx < (_indexStart + _indexCount); ++idx)
        {
            bb.ExpandBy(_geometry.VertexData[_geometry.IndexData[idx]].VertexPosition);
        }

        return bb;
    }
}