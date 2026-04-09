using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Materials.Runtime;

/// <summary>
/// Diffuse-lit material using Lambert shading + specular highlight.
/// Uses directional lights from <see cref="LightingContext"/>.
/// Implemented fully in Phase 5; stub created here for use by MaterialLoader.
/// </summary>
public class LitDiffuseMaterial : MaterialBase
{
    public Texture2D? BasColor { get; set; }
    public Guid BasColorAssetId { get; set; } = Guid.Empty;
    public Texture2D? NormalMap { get; set; }
    public Guid NormalMapAssetId { get; set; } = Guid.Empty;
    public Guid ReflectionCubeAssetId { get; set; } = Guid.Empty;
    public XnaTextureCube? ReflectionCube { get; set; }
    public bool UseSceneReflectionCube { get; set; }
    public Color DiffuseColor { get; set; } = Color.White;
    public Vector3 AmbientColor { get; set; } = Vector3.Zero;
    public Vector3 EmissiveColor { get; set; } = Vector3.Zero;
    public Vector3 SpecularColor { get; set; } = new(0.5f);
    public float SpecularPower { get; set; } = 16.0f;
    public Vector3 TintColor { get; set; } = Vector3.One;
    public float TintStrength { get; set; }
    public float TintMaskFromBaseAlpha { get; set; }
    public float ReflectionAddAmount { get; set; } = 1.0f;
    public float ReflectionMultiplyBase { get; set; } = 1.0f;
    public float ReflectionMultiplyFactor { get; set; }

    private static bool HasReflection(in RenderContext context, ShaderFeature features)
        => (features & ShaderFeature.Reflection) != 0 || context.Environment.SpecularEnvironmentCubemap is not null;

    internal static string GetTechniqueName(ShaderFeature features, bool oneLight, bool hasReflection)
    {
        bool hasBasColor = (features & ShaderFeature.BasColorTexture) != 0;
        bool hasNormalMap = hasBasColor && (features & ShaderFeature.NormalMap) != 0;
        bool hasVertexColor = (features & ShaderFeature.VertexColor) != 0;

        if (hasReflection)
        {
            return (hasBasColor, hasNormalMap) switch
            {
                (true, true) => "LitForward_PixelLighting_Texture_NormalMap_Reflection",
                (true, false) => "LitForward_PixelLighting_Texture_Reflection",
                _ => "LitForward_PixelLighting_Reflection",
            };
        }

        if (hasNormalMap)
        {
            return "LitForward_PixelLighting_Texture_NormalMap";
        }

        var techniqueName = (hasBasColor, oneLight) switch
        {
            (true, true) => "LitForward_PixelLighting_OneLight_Texture",
            (true, false) => "LitForward_PixelLighting_Texture",
            (false, true) => "LitForward_PixelLighting_OneLight",
            _ => "LitForward_PixelLighting",
        };

        return hasVertexColor ? techniqueName + "_VertexColor" : techniqueName;
    }

    public override bool RequiresMaterialTechniqueSelection(
        bool techniqueSelectedBySelector,
        in RenderContext context,
        ShaderFeature features)
    {
        if (!techniqueSelectedBySelector)
        {
            return true;
        }

        if ((features & ShaderFeature.NormalMap) != 0 || HasReflection(in context, features))
        {
            return true;
        }

        return context.Lighting is { ActiveDirectionalLightCount: 1 };
    }

    public override void SelectTechnique(ShaderWrapper shader, in RenderContext context, ShaderFeature features)
    {
        var oneLight = context.Lighting is { ActiveDirectionalLightCount: 1 };
        var hasReflection = HasReflection(in context, features);

        shader.SelectTechnique(GetTechniqueName(features, oneLight, hasReflection));
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
        shader.SetParameter(ShaderParameterNames.HasMaterialReflectionCube, ReflectionCube is not null ? 1.0f : 0.0f);
        shader.SetTextureParameter(ShaderParameterNames.BasColorTexture, BasColor, context.Stats);
        XnaTextureCube? reflectionCube = ReflectionCube ?? (UseSceneReflectionCube ? context.Lighting?.ReflectionCube : null);
        shader.SetTextureCubeParameter(ShaderParameterNames.ReflectionCubeTexture, reflectionCube, context.Stats);

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
            hasReflection: UseSceneReflectionCube || ReflectionCube is not null || ReflectionCubeAssetId != Guid.Empty,
            isTransparent: DiffuseColor.A < byte.MaxValue);

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

        if (element["use_scene_reflection_cube"] is { } useSceneReflection)
        {
            UseSceneReflectionCube = useSceneReflection.Value<bool>();
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
