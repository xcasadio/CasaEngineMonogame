using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

/// <summary>
/// Diffuse-lit material using Lambert shading + specular highlight.
/// Uses directional lights from <see cref="LightingContext"/>.
/// Implemented fully in Phase 5; stub created here for use by MaterialLoader.
/// </summary>
public class LitDiffuseMaterial : MaterialBase
{
    public override bool SupportsVariantTechniqueSelection => false;

    public Texture2D? BasColor { get; set; }
    public Guid BasColorAssetId { get; set; } = Guid.Empty;
    public Texture2D? NormalMap { get; set; }
    public Guid NormalMapAssetId { get; set; } = Guid.Empty;
    public Color DiffuseColor { get; set; } = Color.White;
    public Vector3 EmissiveColor { get; set; } = Vector3.Zero;
    public Vector3 SpecularColor { get; set; } = new(0.5f);
    public float SpecularPower { get; set; } = 16.0f;

    public override void SelectTechnique(ShaderWrapper shader, in RenderContext context, ShaderFeature features)
    {
        var hasBasColor = BasColor is not null;
        var hasNormalMap = NormalMap is not null && hasBasColor;
        var oneLight = context.Lighting is { ActiveDirectionalLightCount: 1 };

        if (hasNormalMap)
        {
            shader.SelectTechnique("BasicEffect_PixelLighting_Texture_NormalMap");
        }
        else
        {
            shader.SelectTechnique((hasBasColor, oneLight) switch
            {
                (true, true)   => "BasicEffect_PixelLighting_OneLight_Texture",
                (true, false)  => "BasicEffect_PixelLighting_Texture",
                (false, true)  => "BasicEffect_PixelLighting_OneLight",
                (false, false) => "BasicEffect_PixelLighting",
            });
        }
    }

    public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
    {

        var worldViewProj = world * context.Frame.ViewProjection;
        shader.SetParameter(ShaderParameterNames.WorldViewProj, worldViewProj);
        shader.SetParameter(ShaderParameterNames.World, world);
        var wit = Matrix.Transpose(Matrix.Invert(world));
        shader.SetParameter(ShaderParameterNames.WorldInverseTranspose, wit);
        shader.SetParameter(ShaderParameterNames.EyePosition, context.Frame.CameraPosition);

        shader.SetParameter(ShaderParameterNames.DiffuseColor, DiffuseColor.ToVector4());
        shader.SetParameter(ShaderParameterNames.EmissiveColor, EmissiveColor);
        shader.SetParameter(ShaderParameterNames.SpecularColor, SpecularColor);
        shader.SetParameter(ShaderParameterNames.SpecularPower, SpecularPower);
        shader.SetParameter(ShaderParameterNames.BasColorTexture, BasColor);

        if (NormalMap is not null && BasColor is not null)
        {
            shader.SetParameter(ShaderParameterNames.NormalTexture, NormalMap);
        }

        context.Lighting?.Bind(shader);
    }

    public override ShaderFeature GetFeatures(Graphics.StaticModelMesh? mesh = null)
    {
        var features = ShaderFeature.None;
        if (BasColor is not null)
        {
            features |= ShaderFeature.BasColorTexture;
        }

        if (EmissiveColor != Vector3.Zero)
        {
            features |= ShaderFeature.Emissive;
        }

        return features;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element["BasColor_asset_id"] is { } a)
        {
            BasColorAssetId = Guid.Parse(a.Value<string>()!);
        }

        if (element["normal_map_asset_id"] is { } nm)
        {
            NormalMapAssetId = Guid.Parse(nm.Value<string>()!);
        }

        if (element["diffuse_color"] is JObject dc)
        {
            DiffuseColor = new Color(
                dc["r"]?.Value<int>() ?? 255, dc["g"]?.Value<int>() ?? 255,
                dc["b"]?.Value<int>() ?? 255, dc["a"]?.Value<int>() ?? 255);
        }

        if (element["emissive_color"] is JObject ec)
        {
            EmissiveColor = new Vector3(
                ec["r"]?.Value<float>() ?? 0, ec["g"]?.Value<float>() ?? 0, ec["b"]?.Value<float>() ?? 0);
        }

        if (element["specular_color"] is JObject sc)
        {
            SpecularColor = new Vector3(
                sc["r"]?.Value<float>() ?? 0.5f, sc["g"]?.Value<float>() ?? 0.5f, sc["b"]?.Value<float>() ?? 0.5f);
        }

        SpecularPower = element["specular_power"]?.Value<float>() ?? 16.0f;
    }

}
