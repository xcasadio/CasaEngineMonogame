using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Graphics;

/// <summary>
/// A single sub-mesh that belongs to a <see cref="StaticModel"/>.
/// Contains GPU-ready vertex/index buffers plus texture reference.
/// </summary>
public class StaticModelMesh
{
    private VertexPositionNormalTexture[] _vertices = Array.Empty<VertexPositionNormalTexture>();
    private uint[] _indices = Array.Empty<uint>();

    public string Name { get; set; } = string.Empty;

    public PrimitiveType PrimitiveType { get; set; } = PrimitiveType.TriangleList;

    /// <summary>Index into the parent <see cref="StaticModel.Meshes"/> table (not used at mesh level — kept for info).</summary>
    public int MaterialIndex { get; set; } = -1;

    /// <summary>Asset ID of the texture bound to this mesh.</summary>
    public Guid TextureAssetId { get; set; } = Guid.Empty;

    /// <summary>Runtime texture (loaded via <see cref="LoadTexture"/>).</summary>
    public Assets.Textures.Texture? Texture { get; set; }

    // --- GPU resources (created inside Initialize) ---
    public VertexBuffer? VertexBuffer { get; private set; }
    public IndexBuffer? IndexBuffer { get; private set; }

    // --- Bounding info ---
    public Vector3 Min { get; set; }
    public Vector3 Max { get; set; }

    public IReadOnlyList<VertexPositionNormalTexture> GetVertices() => _vertices;

    public void SetData(VertexPositionNormalTexture[] vertices, uint[] indices)
    {
        _vertices = vertices;
        _indices = indices;

        if (vertices.Length > 0)
        {
            Min = vertices[0].Position;
            Max = vertices[0].Position;
            foreach (var v in vertices)
            {
                Min = Vector3.Min(Min, v.Position);
                Max = Vector3.Max(Max, v.Position);
            }
        }
    }

    public void Initialize(GraphicsDevice graphicsDevice)
    {
        if (_vertices.Length == 0)
        {
            return;
        }

        VertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), _vertices.Length, BufferUsage.None);
        VertexBuffer.SetData(_vertices);

        IndexBuffer = new IndexBuffer(graphicsDevice, typeof(uint), _indices.Length, BufferUsage.None);
        IndexBuffer.SetData(_indices);
    }

    public void LoadTexture(Guid textureAssetId, Assets.AssetContentManager assetContentManager)
    {
        if (textureAssetId != Guid.Empty)
        {
            TextureAssetId = textureAssetId;
            Texture = assetContentManager.Load<Assets.Textures.Texture>(TextureAssetId);
            Texture.Load(assetContentManager);
        }
    }

    public void Load(JObject element)
    {
        Name = element["name"].GetString();
        PrimitiveType = element["primitive_type"].GetEnum<PrimitiveType>();
        MaterialIndex = element["material_index"].GetInt32();
        TextureAssetId = element["texture_asset_id"].GetGuid();

        _vertices = element.GetElements("vertices", o => o.GetVertexPositionNormalTexture()).ToArray();
        _indices = element.GetElements("indices", o => o.GetUInt32()).ToArray();

        if (_vertices.Length > 0)
        {
            Min = _vertices[0].Position;
            Max = _vertices[0].Position;
            foreach (var v in _vertices)
            {
                Min = Vector3.Min(Min, v.Position);
                Max = Vector3.Max(Max, v.Position);
            }
        }
    }

#if EDITOR
    public void Save(JObject jObject)
    {
        jObject.Add("name", Name);
        jObject.Add("primitive_type", PrimitiveType.ConvertToString());
        jObject.Add("material_index", MaterialIndex);
        jObject.Add("texture_asset_id", TextureAssetId.ToString());

        jObject.AddArray("vertices", _vertices, (v, o) => v.Save(o));
        jObject.AddArray("indices", _indices);
    }
#endif
}
