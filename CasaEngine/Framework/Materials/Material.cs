using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

public class Material
{
    public long TextureBaseColorAssetId { get; set; }
    public Texture TextureBaseColor;

    public long TextureOpacityAssetId { get; set; }
    public Texture TextureOpacityColor;

    public long TextureNormalAssetId { get; set; }
    public Texture TextureNormal;

    public long TextureSpecularAssetId { get; set; }
    public Texture TextureSpecular;

    public long TextureRoughnessAssetId { get; set; }
    public Texture TextureRoughness;

    public long TextureTangentAssetId { get; set; }
    public Texture TextureTangent;

    public long TextureHeightAssetId { get; set; }
    public Texture TextureHeight;

    public long TextureReflectionAssetId { get; set; }
    public Texture TextureReflection;

    public void Load(AssetContentManager content)
    {
        if (TextureBaseColorAssetId != IdManager.InvalidId)
        {
            TextureBaseColor = LoadTexture(TextureBaseColorAssetId, content);
        }

        if (TextureOpacityAssetId != IdManager.InvalidId)
        {
            TextureOpacityColor = LoadTexture(TextureOpacityAssetId, content);
        }

        if (TextureNormalAssetId != IdManager.InvalidId)
        {
            TextureNormal = LoadTexture(TextureNormalAssetId, content);
        }

        if (TextureSpecularAssetId != IdManager.InvalidId)
        {
            TextureSpecular = LoadTexture(TextureSpecularAssetId, content);
        }

        if (TextureRoughnessAssetId != IdManager.InvalidId)
        {
            TextureRoughness = LoadTexture(TextureRoughnessAssetId, content);
        }

        if (TextureTangentAssetId != IdManager.InvalidId)
        {
            TextureTangent = LoadTexture(TextureTangentAssetId, content);
        }

        if (TextureHeightAssetId != IdManager.InvalidId)
        {
            TextureHeight = LoadTexture(TextureHeightAssetId, content);
        }

        if (TextureReflectionAssetId != IdManager.InvalidId)
        {
            TextureReflection = LoadTexture(TextureReflectionAssetId, content);
        }
    }

    private Texture LoadTexture(long assetId, AssetContentManager content)
    {
        var assetInfo = GameSettings.AssetInfoManager.Get(assetId);
        return content.Load<Texture>(assetInfo);
    }

    public void Load(JsonElement element)
    {
        TextureBaseColorAssetId = element.GetProperty("texture_base_color_asset_id").GetInt64();
        TextureOpacityAssetId = element.GetProperty("texture_opacity_asset_id").GetInt64();
        TextureNormalAssetId = element.GetProperty("texture_normal_asset_id").GetInt64();
        TextureSpecularAssetId = element.GetProperty("texture_specular_asset_id").GetInt64();
        TextureRoughnessAssetId = element.GetProperty("texture_roughness_asset_id").GetInt64();
        TextureTangentAssetId = element.GetProperty("texture_tangent_asset_id").GetInt64();
        TextureHeightAssetId = element.GetProperty("texture_height_asset_id").GetInt64();
        TextureReflectionAssetId = element.GetProperty("texture_reflection_asset_id").GetInt64();
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