using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class StaticMesh
{
    public VertexBuffer VertexBuffer { get; private set; }
    public IndexBuffer IndexBuffer { get; private set; }
    public PrimitiveType PrimitiveType { get; set; } = PrimitiveType.TriangleList;
    public Texture2D Texture { get; set; }

    public void Initialize(GraphicsDevice graphicsDevice)
    {
#if EDITOR
        if (_isInitialized)
        {
            return;
        }
#endif

        VertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), _vertices.Count, BufferUsage.None);
        VertexBuffer.SetData(_vertices.ToArray());

        IndexBuffer = new IndexBuffer(graphicsDevice, typeof(ushort), _indices.Count, BufferUsage.None);
        IndexBuffer.SetData(_indices.ToArray());
        _isInitialized = true;
    }

#if EDITOR
    private readonly List<VertexPositionNormalTexture> _vertices = new();
    private readonly List<ushort> _indices = new();
    private bool _isInitialized;

    public void AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        _vertices.Add(new VertexPositionNormalTexture(position, normal, uv));
    }

    public void AddVertex(VertexPositionNormalTexture vertex)
    {
        _vertices.Add(vertex);
    }

    public void AddVertices(IEnumerable<VertexPositionNormalTexture> vertices)
    {
        _vertices.AddRange(vertices);
    }

    public void AddIndices(List<ushort> indices)
    {
        _indices.AddRange(indices);
    }

    public IReadOnlyCollection<VertexPositionNormalTexture> GetVertices()
    {
        return _vertices;
    }
    public void Save(JObject jObject)
    {
        //TODO
    }
#endif
}