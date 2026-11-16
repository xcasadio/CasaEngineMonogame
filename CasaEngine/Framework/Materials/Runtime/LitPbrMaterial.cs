using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Materials.Runtime;

/// <summary>
/// Metallic-roughness PBR material (glTF conventions) drawn by <c>LitPbr.fx</c>. Colours (<see cref="BaseColor"/>,
/// <see cref="EmissiveColor"/> and the colour textures) are display colours: the shader decodes them to linear light
/// once per pixel when the colour pipeline is linear. The material picks its technique itself (normal map, blending, then the
/// alpha-test state), because the shader variant library alias table only knows the two techniques without a normal map.
/// </summary>
public class LitPbrMaterial : MaterialBase
{
    /// <summary>
    /// Debug switch of the image-based lighting (plan T6.3): when true every <see cref="LitPbrMaterial"/> samples the prefiltered
    /// specular atlas at band 0 whatever its roughness, which isolates the effect of the prefiltering. Not serialized, not a
    /// material property: a process-wide harness setting, false by default (the demos set it from
    /// <c>CASAENGINE_COLOR_DEMO_IBL_FORCE_BASE_LEVEL=1</c>).
    /// </summary>
    public static bool DebugForceIblBaseLevel { get; set; }

    /// <summary>Base colour factor (alpha = opacity), multiplied with <see cref="BaseColorTexture"/> when one is bound.</summary>
    public Color BaseColor { get; set; } = Color.White;

    /// <summary>0 = dielectric, 1 = metal. Multiplied with the blue channel of <see cref="MetallicRoughnessTexture"/>.</summary>
    public float Metallic { get; set; }

    /// <summary>Perceptual roughness. Multiplied with the green channel of <see cref="MetallicRoughnessTexture"/>.</summary>
    public float Roughness { get; set; } = 0.5f;

    /// <summary>Emitted colour added on top of the lighting. Multiplied with <see cref="EmissiveTexture"/> when one is bound.</summary>
    public Vector3 EmissiveColor { get; set; } = Vector3.Zero;

    /// <summary>Weight of the occlusion texture: 0 ignores it, 1 applies it fully.</summary>
    public float OcclusionStrength { get; set; } = 1.0f;

    /// <summary>
    /// Weight of the clear-coat layer (ADR-0115): 0 = none (the rendering is then identical to a material without the layer), 1 = a
    /// full dielectric varnish (F0 0.04) over the base. The layer follows the geometric normal, not the normal map.
    /// </summary>
    public float ClearCoat { get; set; }

    /// <summary>Perceptual roughness of the clear-coat layer. Values under 0.045 render as 0.045.</summary>
    public float ClearCoatRoughness { get; set; } = 0.1f;

    /// <summary>Scale of the x and y components of the tangent-space normal.</summary>
    public float NormalScale { get; set; } = 1.0f;

    public Texture2D BaseColorTexture { get; set; }
    public Guid BaseColorTextureAssetId { get; set; } = Guid.Empty;

    public override Texture2D ShadowMaskTexture => Queue == RenderQueue.AlphaTest ? BaseColorTexture : null;
    public Texture2D NormalTexture { get; set; }
    public Guid NormalTextureAssetId { get; set; } = Guid.Empty;
    public Texture2D MetallicRoughnessTexture { get; set; }
    public Guid MetallicRoughnessTextureAssetId { get; set; } = Guid.Empty;
    public Texture2D OcclusionTexture { get; set; }
    public Guid OcclusionTextureAssetId { get; set; } = Guid.Empty;
    public Texture2D EmissiveTexture { get; set; }
    public Guid EmissiveTextureAssetId { get; set; } = Guid.Empty;

    /// <summary>
    /// Optional detail texture (ADR-0117), same formula as <see cref="LitDiffuseMaterial.DetailTexture"/>: the base texture is
    /// multiplied by <c>2 * detail(texCoord * DetailTiling)</c> before the colour is decoded, so a mid-grey texel leaves it
    /// unchanged. Read only together with <see cref="BaseColorTexture"/>; ignored by the skinned PBR path.
    /// </summary>
    public Texture2D DetailTexture { get; set; }
    public Guid DetailTextureAssetId { get; set; } = Guid.Empty;

    private float _detailTiling = 1.0f;

    /// <summary>
    /// Number of times the detail texture repeats per unit of the base texture coordinates. A value that is not finite or not
    /// above zero is read as 1.
    /// </summary>
    public float DetailTiling
    {
        get => _detailTiling;
        set => _detailTiling = float.IsFinite(value) && value > 0.0f ? value : 1.0f;
    }

    /// <summary>
    /// Technique of <c>LitPbr.fx</c> for the resolved features: <c>LitPbr[_NormalMap][_AlphaTest|_Transparent]</c>. The presence of the
    /// other textures is carried by the Has... constants, not by the technique.
    /// </summary>
    internal static string GetTechniqueName(ShaderFeature features)
    {
        bool hasNormalMap = (features & ShaderFeature.NormalMap) != 0;
        bool isAlphaTest = (features & ShaderFeature.AlphaTest) != 0;
        bool isTransparent = !isAlphaTest && (features & ShaderFeature.Transparent) != 0;

        return (hasNormalMap, isAlphaTest, isTransparent) switch
        {
            (true, true, _) => "LitPbr_NormalMap_AlphaTest",
            (true, false, true) => "LitPbr_NormalMap_Transparent",
            (true, false, false) => "LitPbr_NormalMap",
            (false, true, _) => "LitPbr_AlphaTest",
            (false, false, true) => "LitPbr_Transparent",
            _ => "LitPbr",
        };
    }

    /// <summary>
    /// The alias table only names the two techniques without a normal map and without blending, so a normal-mapped or
    /// a blended draw always needs the material to name its technique.
    /// </summary>
    public override bool RequiresMaterialTechniqueSelection(
        bool techniqueSelectedBySelector,
        in RenderContext context,
        ShaderFeature features)
        => !techniqueSelectedBySelector || (features & (ShaderFeature.NormalMap | ShaderFeature.Transparent)) != 0;

    public override void SelectTechnique(ShaderWrapper shader, in RenderContext context, ShaderFeature features)
        => shader.SelectTechnique(GetTechniqueName(features));

    /// <summary>
    /// LitPbr.fx defaults nothing: every material constant and every Has... flag is written on every bind, so a value left
    /// by the previous draw never leaks into this one.
    /// </summary>
    public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
    {
        shader.SetParameter(ShaderParameterNames.WorldViewProj, world * context.Frame.ViewProjection);
        shader.SetParameter(ShaderParameterNames.World, world);
        shader.SetParameter(ShaderParameterNames.WorldInverseTranspose, Matrix.Transpose(Matrix.Invert(world)));
        shader.SetParameter(ShaderParameterNames.EyePosition, context.Frame.CameraPosition);

        shader.SetParameter(ShaderParameterNames.BaseColor, BaseColor.ToVector4());
        shader.SetParameter(ShaderParameterNames.Metallic, Metallic);
        shader.SetParameter(ShaderParameterNames.Roughness, Roughness);
        shader.SetParameter(ShaderParameterNames.EmissiveColor, EmissiveColor);
        shader.SetParameter(ShaderParameterNames.OcclusionStrength, OcclusionStrength);
        shader.SetParameter(ShaderParameterNames.NormalScale, NormalScale);
        shader.SetParameter(ShaderParameterNames.ClearCoat, ClearCoat);
        shader.SetParameter(ShaderParameterNames.ClearCoatRoughness, ClearCoatRoughness);
        shader.SetParameter(ShaderParameterNames.IblForceBaseLevel, DebugForceIblBaseLevel ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.AlphaCutoff, Queue == RenderQueue.AlphaTest ? AlphaCutoff : 0.0f);

        BindOptionalTexture(shader, ShaderParameterNames.BasColorTexture, ShaderParameterNames.HasBaseColorTexture, BaseColorTexture, in context);
        BindOptionalTexture(shader, ShaderParameterNames.MetallicRoughnessTexture, ShaderParameterNames.HasMetallicRoughnessTexture, MetallicRoughnessTexture, in context);
        BindOptionalTexture(shader, ShaderParameterNames.OcclusionTexture, ShaderParameterNames.HasOcclusionTexture, OcclusionTexture, in context);
        BindOptionalTexture(shader, ShaderParameterNames.EmissiveTexture, ShaderParameterNames.HasEmissiveTexture, EmissiveTexture, in context);

        // The detail texture modulates the base texture only: without a base texture the shader never reads it.
        var hasDetailTexture = DetailTexture is not null && BaseColorTexture is not null;
        shader.SetParameter(ShaderParameterNames.HasDetailTexture, hasDetailTexture ? 1.0f : 0.0f);
        shader.SetParameter(ShaderParameterNames.DetailTiling, DetailTiling);
        if (hasDetailTexture)
        {
            shader.SetTextureParameter(ShaderParameterNames.DetailTexture, DetailTexture, context.Stats);
        }

        // The NormalMap techniques always sample NormalTexture; the others never read it.
        if (NormalTexture is not null)
        {
            shader.SetTextureParameter(ShaderParameterNames.NormalTexture, NormalTexture, context.Stats);
        }
    }

    private static void BindOptionalTexture(
        ShaderWrapper shader,
        string textureParameter,
        string hasTextureParameter,
        Texture2D texture,
        in RenderContext context)
    {
        shader.SetParameter(hasTextureParameter, texture is not null ? 1.0f : 0.0f);
        if (texture is not null)
        {
            shader.SetTextureParameter(textureParameter, texture, context.Stats);
        }
    }

    public override MaterialShaderCapabilities GetShaderCapabilities()
        => CreateShaderCapabilities(
            MaterialShaderFamily.LitPbr,
            hasBasColorTexture: BaseColorTexture is not null || BaseColorTextureAssetId != Guid.Empty,
            hasNormalMap: NormalTexture is not null || NormalTextureAssetId != Guid.Empty,
            hasEmissive: EmissiveColor != Vector3.Zero || EmissiveTexture is not null || EmissiveTextureAssetId != Guid.Empty,
            isTransparent: BaseColor.A < byte.MaxValue);
}
