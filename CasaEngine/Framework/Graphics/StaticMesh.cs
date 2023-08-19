using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Graphics;

public class StaticMesh
{
    private readonly List<VertexPositionNormalTexture> _vertices = new();
    private readonly List<ushort> _indices = new();
    public VertexBuffer? VertexBuffer { get; private set; }
    public IndexBuffer? IndexBuffer { get; private set; }
    public PrimitiveType PrimitiveType { get; set; } = PrimitiveType.TriangleList;
    public Assets.Textures.Texture? Texture { get; set; }

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

        //Texture?.Initialize(graphicsDevice);

#if EDITOR
        _isInitialized = true;
#endif
    }

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

    public void Load(JsonElement element)
    {
        //base.Save(jObject); //asset ?
        var version = element.GetProperty("version").GetInt32();
        PrimitiveType = element.GetProperty("primitive_type").GetEnum<PrimitiveType>();

        var verticesJObject = element.GetProperty("vertices");

        var arrayEnumerator = verticesJObject.EnumerateArray();
        foreach (var vertex in arrayEnumerator.GetEnumerator())
        {
            _vertices.Add(vertex.GetVertexPositionNormalTexture());
        }

        var indicesJObject = element.GetProperty("indices");
        arrayEnumerator = indicesJObject.EnumerateArray();
        foreach (var index in arrayEnumerator.GetEnumerator())
        {
            _indices.Add(index.GetUInt16());
        }

        var textureElement = element.GetProperty("texture");
        if (textureElement.ToString() != "null")
        {
            Texture = new Assets.Textures.Texture();
            Texture.Load(textureElement, SaveOption.Editor);
        }
    }

#if EDITOR

    private bool _isInitialized;

    public IReadOnlyCollection<VertexPositionNormalTexture> GetVertices()
    {
        return _vertices;
    }

    public void Save(JObject jObject, SaveOption option)
    {
        //base.Save(jObject); //asset ?
        jObject.Add("version", 1);
        jObject.Add("primitive_type", PrimitiveType.ConvertToString());

        var verticesJObject = new JArray();
        jObject.Add("vertices", verticesJObject);

        foreach (var vertex in _vertices)
        {
            var vertexObject = new JObject();
            vertex.Save(vertexObject);
            verticesJObject.Add(vertexObject);
        }

        var indicesJObject = new JArray();
        jObject.Add("indices", indicesJObject);

        foreach (var index in _indices)
        {
            indicesJObject.Add(index);
        }

        var textureJObject = new JObject();
        if (Texture != null)
        {
            Texture.Save(textureJObject, option);
            jObject.Add("texture", textureJObject);
        }
        else
        {
            jObject.Add("texture", "null");
        }
    }
#endif
}