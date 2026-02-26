
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Materials;

/// <summary>
/// Multi-channel material with 8 texture slots (BaseColor, Opacity, Normal, Specular,
/// Roughness, Tangent, Height, Reflection). Inherits from <see cref="MaterialBase"/>.
/// The BaseColor texture is used as Albedo in the current forward shader.
/// </summary>
public class Material : MaterialBase
{
    public Guid TextureBaseColorAssetId { get; set; }
    public Texture? TextureBaseColor { get; set; }
    public Guid TextureOpacityAssetId { get; set; }
    public Texture? TextureOpacityColor { get; set; }
    public Guid TextureNormalAssetId { get; set; }
    public Texture? TextureNormal { get; set; }
    public Guid TextureSpecularAssetId { get; set; }
    public Texture? TextureSpecular { get; set; }
    public Guid TextureRoughnessAssetId { get; set; }
    public Texture? TextureRoughness { get; set; }
    public Guid TextureTangentAssetId { get; set; }
    public Texture? TextureTangent { get; set; }
    public Guid TextureHeightAssetId { get; set; }
    public Texture? TextureHeight { get; set; }
    public Guid TextureReflectionAssetId { get; set; }
    public Texture? TextureReflection { get; set; }

    public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
    {
        var worldViewProj = world * context.Frame.ViewProjection;
        shader.SetParameter(ShaderParameterNames.WorldViewProj, worldViewProj);
        shader.SetParameter(ShaderParameterNames.World, world);
        var wit = Matrix.Transpose(Matrix.Invert(world));
        shader.SetParameter(ShaderParameterNames.WorldInverseTranspose, wit);
        shader.SetParameter(ShaderParameterNames.EyePosition, context.Frame.CameraPosition);
        shader.SetParameter(ShaderParameterNames.AlbedoTexture, TextureBaseColor?.Resource);
        context.Lighting?.Bind(shader);
    }

    public void LoadTextures(AssetContentManager content)
    {
        if (TextureBaseColorAssetId  != Guid.Empty) TextureBaseColor   = content.Load<Texture>(TextureBaseColorAssetId);
        if (TextureOpacityAssetId    != Guid.Empty) TextureOpacityColor= content.Load<Texture>(TextureOpacityAssetId);
        if (TextureNormalAssetId     != Guid.Empty) TextureNormal      = content.Load<Texture>(TextureNormalAssetId);
        if (TextureSpecularAssetId   != Guid.Empty) TextureSpecular    = content.Load<Texture>(TextureSpecularAssetId);
        if (TextureRoughnessAssetId  != Guid.Empty) TextureRoughness   = content.Load<Texture>(TextureRoughnessAssetId);
        if (TextureTangentAssetId    != Guid.Empty) TextureTangent     = content.Load<Texture>(TextureTangentAssetId);
        if (TextureHeightAssetId     != Guid.Empty) TextureHeight      = content.Load<Texture>(TextureHeightAssetId);
        if (TextureReflectionAssetId != Guid.Empty) TextureReflection  = content.Load<Texture>(TextureReflectionAssetId);
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        TextureBaseColorAssetId   = ParseGuid(element["texture_base_color_asset_id"]);
        TextureOpacityAssetId     = ParseGuid(element["texture_opacity_asset_id"]);
        TextureNormalAssetId      = ParseGuid(element["texture_normal_asset_id"]);
        TextureSpecularAssetId    = ParseGuid(element["texture_specular_asset_id"]);
        TextureRoughnessAssetId   = ParseGuid(element["texture_roughness_asset_id"]);
        TextureTangentAssetId     = ParseGuid(element["texture_tangent_asset_id"]);
        TextureHeightAssetId      = ParseGuid(element["texture_height_asset_id"]);
        TextureReflectionAssetId  = ParseGuid(element["texture_reflection_asset_id"]);
    }

#if EDITOR
    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject["type"] = nameof(Material);
        jObject["texture_base_color_asset_id"]  = TextureBaseColorAssetId;
        jObject["texture_opacity_asset_id"]      = TextureOpacityAssetId;
        jObject["texture_normal_asset_id"]       = TextureNormalAssetId;
        jObject["texture_specular_asset_id"]     = TextureSpecularAssetId;
        jObject["texture_roughness_asset_id"]    = TextureRoughnessAssetId;
        jObject["texture_tangent_asset_id"]      = TextureTangentAssetId;
        jObject["texture_height_asset_id"]       = TextureHeightAssetId;
        jObject["texture_reflection_asset_id"]   = TextureReflectionAssetId;
    }
#endif

    private static Guid ParseGuid(JToken? token)
    {
        var s = token?.Value<string>();
        return s != null && Guid.TryParse(s, out var g) ? g : Guid.Empty;
    }
}
