
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Graphics;

public class StaticMesh : ObjectBase
{
    private readonly List<VertexPositionNormalTexture> _vertices = new();
    private readonly List<uint> _indices = new();

    public VertexBuffer? VertexBuffer { get; private set; }
    public IndexBuffer? IndexBuffer { get; private set; }
    public PrimitiveType PrimitiveType { get; set; } = PrimitiveType.TriangleList;
    public Guid TextureAssetId { get; set; } = Guid.Empty;
    public Assets.Textures.Texture? Texture { get; set; }

    public void Initialize(GraphicsDevice graphicsDevice, AssetContentManager assetContentManager)
    {
#if EDITOR
        if (_isInitialized)
        {
            return;
        }
#endif

        VertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), _vertices.Count, BufferUsage.None);
        VertexBuffer.SetData(_vertices.ToArray());

        IndexBuffer = new IndexBuffer(graphicsDevice, typeof(uint), _indices.Count, BufferUsage.None);
        IndexBuffer.SetData(_indices.ToArray());

        if (TextureAssetId != Guid.Empty)
        {
            Texture = assetContentManager.Load<Assets.Textures.Texture>(TextureAssetId);
            Texture.Load(assetContentManager);
        }

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

    public void AddIndices(List<uint> indices)
    {
        _indices.AddRange(indices);
    }

    public IReadOnlyCollection<VertexPositionNormalTexture> GetVertices()
    {
        return _vertices;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        PrimitiveType = element["primitive_type"].GetEnum<PrimitiveType>();

        _vertices.Clear();
        _vertices.AddRange(element.GetElements("vertices", o => o.GetVertexPositionNormalTexture()));

        _indices.Clear();
        _indices.AddRange(element.GetElements("indices", o => o.GetUInt32()));

        TextureAssetId = element["texture_asset_id"].GetGuid();
    }

#if EDITOR

    private bool _isInitialized;


    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        jObject.Add("primitive_type", PrimitiveType.ConvertToString());

        jObject.AddArray("vertices", _vertices, (v, o) => v.Save(o));
        jObject.AddArray("indices", _indices);

        jObject.Add("texture_asset_id", Texture?.Id ?? Guid.Empty);
    }
#endif
}