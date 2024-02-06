using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

public class Material : ISerializable
{
    public MaterialAsset Diffuse;

    public Guid TextureBaseColorAssetId { get; set; }
    public Texture TextureBaseColor;

    public Guid TextureOpacityAssetId { get; set; }
    public Texture TextureOpacityColor;

    public Guid TextureNormalAssetId { get; set; }
    public Texture TextureNormal;

    public Guid TextureSpecularAssetId { get; set; }
    public Texture TextureSpecular;

    public Guid TextureRoughnessAssetId { get; set; }
    public Texture TextureRoughness;

    public Guid TextureTangentAssetId { get; set; }
    public Texture TextureTangent;

    public Guid TextureHeightAssetId { get; set; }
    public Texture TextureHeight;

    public Guid TextureReflectionAssetId { get; set; }
    public Texture TextureReflection;

    public void Load(AssetContentManager content)
    {
        if (TextureBaseColorAssetId != Guid.Empty)
        {
            TextureBaseColor = LoadTexture(TextureBaseColorAssetId, content);
        }

        if (TextureOpacityAssetId != Guid.Empty)
        {
            TextureOpacityColor = LoadTexture(TextureOpacityAssetId, content);
        }

        if (TextureNormalAssetId != Guid.Empty)
        {
            TextureNormal = LoadTexture(TextureNormalAssetId, content);
        }

        if (TextureSpecularAssetId != Guid.Empty)
        {
            TextureSpecular = LoadTexture(TextureSpecularAssetId, content);
        }

        if (TextureRoughnessAssetId != Guid.Empty)
        {
            TextureRoughness = LoadTexture(TextureRoughnessAssetId, content);
        }

        if (TextureTangentAssetId != Guid.Empty)
        {
            TextureTangent = LoadTexture(TextureTangentAssetId, content);
        }

        if (TextureHeightAssetId != Guid.Empty)
        {
            TextureHeight = LoadTexture(TextureHeightAssetId, content);
        }

        if (TextureReflectionAssetId != Guid.Empty)
        {
            TextureReflection = LoadTexture(TextureReflectionAssetId, content);
        }
    }

    private Texture LoadTexture(Guid assetId, AssetContentManager content)
    {
        return content.Load<Texture>(assetId);
    }

    public void Load(JsonElement element)
    {
        TextureBaseColorAssetId = element.GetProperty("texture_base_color_asset_id").GetGuid();
        TextureOpacityAssetId = element.GetProperty("texture_opacity_asset_id").GetGuid();
        TextureNormalAssetId = element.GetProperty("texture_normal_asset_id").GetGuid();
        TextureSpecularAssetId = element.GetProperty("texture_specular_asset_id").GetGuid();
        TextureRoughnessAssetId = element.GetProperty("texture_roughness_asset_id").GetGuid();
        TextureTangentAssetId = element.GetProperty("texture_tangent_asset_id").GetGuid();
        TextureHeightAssetId = element.GetProperty("texture_height_asset_id").GetGuid();
        TextureReflectionAssetId = element.GetProperty("texture_reflection_asset_id").GetGuid();
    }

#if EDITOR

    public void Save(JObject jObject)
    {
        jObject.Add("texture_base_color_asset_id", TextureBaseColorAssetId);
        jObject.Add("texture_opacity_asset_id", TextureOpacityAssetId);
        jObject.Add("texture_normal_asset_id", TextureNormalAssetId);
        jObject.Add("texture_specular_asset_id", TextureSpecularAssetId);
        jObject.Add("texture_roughness_asset_id", TextureRoughnessAssetId);
        jObject.Add("texture_tangent_asset_id", TextureTangentAssetId);
        jObject.Add("texture_height_asset_id", TextureHeightAssetId);
        jObject.Add("texture_reflection_asset_id", TextureReflectionAssetId);
    }

#endif
}