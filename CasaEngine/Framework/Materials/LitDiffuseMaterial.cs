using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

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
    public Guid ReflectionCubeAssetId { get; set; } = Guid.Empty;
    public XnaTextureCube? ReflectionCube { get; set; }
    public Color DiffuseColor { get; set; } = Color.White;
    public Vector3 AmbientColor { get; set; } = Vector3.Zero;
    public Vector3 EmissiveColor { get; set; } = Vector3.Zero;
    public Vector3 SpecularColor { get; set; } = new(0.5f);
    public float SpecularPower { get; set; } = 16.0f;

    public override void SelectTechnique(ShaderWrapper shader, in RenderContext context, ShaderFeature features)
    {
        bool hasBasColor = (features & ShaderFeature.BasColorTexture) != 0;
        bool hasNormalMap = hasBasColor && (features & ShaderFeature.NormalMap) != 0;
        bool hasReflection = (features & ShaderFeature.Reflection) != 0;
        var oneLight = context.Lighting is { ActiveDirectionalLightCount: 1 };

        if (hasReflection)
        {
            shader.SelectTechnique((hasBasColor, hasNormalMap) switch
            {
                (true, true) => "BasicEffect_PixelLighting_Texture_NormalMap_Reflection",
                (true, false) => "BasicEffect_PixelLighting_Texture_Reflection",
                _ => "BasicEffect_PixelLighting_Reflection",
            });
            return;
        }

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
        shader.SetParameter(ShaderParameterNames.AlphaCutoff, Queue == RenderQueue.AlphaTest ? AlphaCutoff : 0.0f);
        shader.SetParameter(ShaderParameterNames.MaterialAmbientColor, AmbientColor);
        shader.SetParameter(ShaderParameterNames.EmissiveColor, EmissiveColor);
        shader.SetParameter(ShaderParameterNames.SpecularColor, SpecularColor);
        shader.SetParameter(ShaderParameterNames.SpecularPower, SpecularPower);
        shader.SetTextureParameter(ShaderParameterNames.BasColorTexture, BasColor, context.Stats);
        shader.SetTextureCubeParameter(ShaderParameterNames.ReflectionCubeTexture, ReflectionCube, context.Stats);

        if (NormalMap is not null && BasColor is not null)
        {
            shader.SetTextureParameter(ShaderParameterNames.NormalTexture, NormalMap, context.Stats);
        }

        context.Lighting?.Bind(shader);
    }

    public override MaterialShaderCapabilities GetShaderCapabilities()
        => CreateShaderCapabilities(
            MaterialShaderFamily.Lit,
            hasBasColorTexture: BasColor is not null || BasColorAssetId != Guid.Empty,
            hasNormalMap: NormalMap is not null || NormalMapAssetId != Guid.Empty,
            hasEmissive: EmissiveColor != Vector3.Zero,
            hasReflection: ReflectionCube is not null || ReflectionCubeAssetId != Guid.Empty,
            isTransparent: DiffuseColor.A < byte.MaxValue);

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

        if (ReflectionCube is not null || ReflectionCubeAssetId != Guid.Empty)
        {
            features |= ShaderFeature.Reflection;
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

        if (element["texture_reflection_asset_id"] is { } rt)
        {
            ReflectionCubeAssetId = Guid.Parse(rt.Value<string>()!);
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

        if (element["ambient_color"] is JObject ac)
        {
            AmbientColor = new Vector3(
                ac["r"]?.Value<float>() ?? ac["x"]?.Value<float>() ?? 0,
                ac["g"]?.Value<float>() ?? ac["y"]?.Value<float>() ?? 0,
                ac["b"]?.Value<float>() ?? ac["z"]?.Value<float>() ?? 0);
        }

        if (element["specular_color"] is JObject sc)
        {
            SpecularColor = new Vector3(
                sc["r"]?.Value<float>() ?? 0.5f, sc["g"]?.Value<float>() ?? 0.5f, sc["b"]?.Value<float>() ?? 0.5f);
        }

        SpecularPower = element["specular_power"]?.Value<float>() ?? 16.0f;
    }

}
