using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D;

public abstract class GeometricPrimitive
{
    private readonly List<VertexPositionNormalTexture> _vertices = new();
    private readonly List<uint> _indices = new();

    protected uint CurrentVertex => (uint)_vertices.Count;

    public StaticMesh CreateMesh()
    {
        var mesh = new StaticMesh();
        mesh.AddVertices(_vertices);
        mesh.AddIndices(_indices);

        return mesh;
    }

    protected void AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        _vertices.Add(new VertexPositionNormalTexture(position, normal, uv));
    }

    protected void AddIndex(uint index)
    {
        if (index > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _indices.Add((uint)index);
    }

    protected static Vector3 GetCircleVector(uint i, int tessellation)
    {
        var angle = (float)(i * 2.0 * Math.PI / tessellation);
        var dx = (float)Math.Sin(angle);
        var dz = (float)Math.Cos(angle);

        return new Vector3(dx, 0, dz);
    }

    protected void CreateCylinderCap(int tessellation, float height, float radius, bool isTop)
    {
        // Create cap indices.
        for (uint i = 0; i < tessellation - 2; i++)
        {
            uint i1 = (i + 1) % (uint)tessellation;
            uint i2 = (i + 2) % (uint)tessellation;

            if (isTop)
            {
                (i1, i2) = (i2, i1);
            }

            uint currentIndex = CurrentVertex;
            AddIndex(currentIndex);
            AddIndex(currentIndex + i1);
            AddIndex(currentIndex + i2);
        }

        // Which end of the cylinder is this?
        var normal = Vector3.UnitY;
        var textureScale = new Vector2(-0.5f);

        if (!isTop)
        {
            normal = -normal;
            textureScale.X = -textureScale.X;
        }

        // Create cap vertices.
        for (uint i = 0; i < tessellation; i++)
        {
            var circleVector = GetCircleVector(i, tessellation);
            var position = (circleVector * radius) + (normal * height);
            var textureCoordinate = new Vector2(circleVector.X * textureScale.X + 0.5f, circleVector.Z * textureScale.Y + 0.5f);

            AddVertex(position, normal, textureCoordinate);
        }
    }

#if EDITOR
    public List<Vector3> Points
    {
        get
        {
            return _vertices.Select(v => v.Position).ToList();
        }
    }

    public List<VertexPositionNormalTexture> Vertices => _vertices;
    public List<uint> Indices => _indices;

#endif
}