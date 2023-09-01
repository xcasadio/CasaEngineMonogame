using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
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
    public long TextureAssetId { get; set; } = IdManager.InvalidId;
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

        IndexBuffer = new IndexBuffer(graphicsDevice, typeof(ushort), _indices.Count, BufferUsage.None);
        IndexBuffer.SetData(_indices.ToArray());

        if (TextureAssetId != IdManager.InvalidId)
        {
            var assetInfo = GameSettings.AssetInfoManager.Get(TextureAssetId);
            Texture = assetContentManager.Load<Assets.Textures.Texture>(assetInfo);
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

    public void AddIndices(List<ushort> indices)
    {
        _indices.AddRange(indices);
    }

    public void Load(JsonElement element)
    {
        PrimitiveType = element.GetProperty("primitive_type").GetEnum<PrimitiveType>();

        _vertices.Clear();
        _vertices.AddRange(element.GetElements("vertices", o => o.GetVertexPositionNormalTexture()));

        _indices.Clear();
        _indices.AddRange(element.GetElements("indices", o => o.GetUInt16()));

        TextureAssetId = element.GetProperty("texture_asset_id").GetInt64();
    }

#if EDITOR

    private bool _isInitialized;

    public IReadOnlyCollection<VertexPositionNormalTexture> GetVertices()
    {
        return _vertices;
    }

    public void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("primitive_type", PrimitiveType.ConvertToString());

        jObject.AddArray("vertices", _vertices, (v, o) => v.Save(o));
        jObject.AddArray("indices", _indices);

        var textureJObject = new JObject();
        jObject.Add("texture_asset_id", Texture == null ? IdManager.InvalidId : Texture.AssetInfo.Id);
    }
#endif
}