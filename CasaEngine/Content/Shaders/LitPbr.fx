//-----------------------------------------------------------------------------
// LitPbr.fx
//
// Static-mesh PBR shader (metallic-roughness, glTF conventions), forward, per-pixel lighting only (plan decisions D3, D5).
//
// Techniques (the material picks one by name; the shader variant library maps its canonical names to the two alpha-test
// states, see ShaderVariantLibrary.BuildLitPbrAliases):
//   LitPbr                      opaque / blended surface, vertex format position + normal + texture coordinate
//   LitPbr_AlphaTest            the same, clipped against AlphaCutoff
//   LitPbr_NormalMap            plus a tangent-space normal map; vertex format position + normal + texture coordinate + tangent
//   LitPbr_NormalMap_AlphaTest  both
//   LitPbr_Transparent          blended surface (premultiplied output, see ShadePbr): only the diffuse part is scaled by the alpha
//   LitPbr_NormalMap_Transparent  the same with a tangent-space normal map
// The presence of the base colour, metallic-roughness, occlusion and emissive textures is driven by the Has... constants (like
// HasDetailTexture in LitForward.fx), not by techniques. The normal map is driven by the technique: the NormalMap techniques
// always sample NormalTexture.
//
// Textures (sampler registers; this backend is OpenGL, shader model 3.0, 16 pixel samplers: 14 are used):
//   t0 Texture                           base colour (display colour, alpha = opacity)
//   t1 NormalTexture                     tangent-space normal
//   t2 MetallicRoughnessTexture          G = roughness, B = metallic (glTF convention)
//   t3 EnvironmentCubeTexture            environment cube        (declared for Lighting.fxh)
//   t4 LocalReflectionProbeCubeTexture   local probe              (declared for Lighting.fxh)
//   t5 SecondaryLocalReflectionProbeCubeTexture                    (declared for Lighting.fxh)
//   t6 ShadowMapTexture                  directional shadow map
//   t7 OcclusionTexture                  R = ambient occlusion (glTF convention)
//   t8 EmissiveTexture                   emissive colour (display colour)
//   t9 IrradianceCubeTexture             diffuse irradiance cube of the IBL (32 texels, linear light, E / pi; ADR-0109)
//   t10 PrefilteredSpecularAtlasTexture  prefiltered specular atlas of the IBL (2D, six roughness bands, linear light)
//   t11 BrdfLookupTexture                split-sum BRDF table (x = NoV, y = roughness, channels A and B)
//   t12 PrefilteredLocalReflectionProbeAtlasTexture           prefiltered atlas of the primary local probe (same layout as t10)
//   t13 PrefilteredSecondaryLocalReflectionProbeAtlasTexture   prefiltered atlas of the secondary local probe
// The IBL textures are bound by EnvironmentShaderBinder only because this shader declares HasIbl (ADR-0109). The probe atlases
// are generated like the global one (IblCache.TryGetPrefilteredAtlas); while one does not exist yet its Has... flag
// (HasPrefilteredLocalReflectionProbe, HasPrefilteredSecondaryLocalReflectionProbe) is 0 and the probe is sampled unprefiltered
// from its cube (t4, t5).
//
// Constant layout. The _vs/_ps/_cb macros are empty (Macros.fxh), so the slots are documentation pinned by
// PbrShaderCoverageTests, as in LitForward.fx. The shared lighting block (lights, counts, eye, ambient, environment, probes,
// shadow) keeps exactly the slots of LitForward.fx, so the engine binders that fill it (ForwardLightBinder,
// EnvironmentShaderBinder, the shadow binding) address both shaders alike. The PBR constants take the slots that LitForward.fx
// uses for constants this shader does not have (MaterialAmbientColor, HasMaterialReflectionCube, the reflection factors, the
// detail texture) and are written with their _cb slot only:
//   _cb(c1)     EmissiveColor              display colour
//   _cb(c28.w)  AlphaCutoff                0 outside the alpha-test queue
//   _cb(c29.w)  NormalScale
//   _cb(c30)    BaseColor                  rgba, display colour
//   _cb(c32.y)  Metallic
//   _cb(c34.z)  Roughness
//   _cb(c34.w)  OcclusionStrength
//   _cb(c35.x)  HasBaseColorTexture
//   _cb(c35.y)  HasMetallicRoughnessTexture
//   _cb(c35.z)  HasOcclusionTexture
//   _cb(c35.w)  HasEmissiveTexture
// DiffuseColor, SpecularColor and SpecularPower are declared because Lighting.fxh reads them; the PBR shading never uses them.
// HasIbl (1 when the generated IBL textures are bound, 0 for the flat ambient) is written by EnvironmentShaderBinder every bind;
// IblForceBaseLevel (debug, 0 by default) is written by LitPbrMaterial.Bind and forces the prefiltered band 0 (probes included).
// HasPrefilteredLocalReflectionProbe and HasPrefilteredSecondaryLocalReflectionProbe are written by EnvironmentShaderBinder. None has a
// fixed slot: the compiler places them.
//
// Colours: BaseColor, EmissiveColor and the colour textures are display colours. They are decoded to linear light ONCE per
// pixel, in ShadePbr, never inside the light slots (plan open point O10). Light colours, the ambient and the shadow tint arrive
// already decoded from the CPU. The output is linear light, unclamped.
//-----------------------------------------------------------------------------

#include "Macros.fxh"


DECLARE_TEXTURE(Texture, 0);
DECLARE_TEXTURE(NormalTexture, 1);
DECLARE_TEXTURE(MetallicRoughnessTexture, 2);
DECLARE_CUBEMAP(EnvironmentCubeTexture, 3);
DECLARE_CUBEMAP(LocalReflectionProbeCubeTexture, 4);
DECLARE_CUBEMAP(SecondaryLocalReflectionProbeCubeTexture, 5);
DECLARE_TEXTURE(ShadowMapTexture, 6);
DECLARE_TEXTURE(OcclusionTexture, 7);
DECLARE_TEXTURE(EmissiveTexture, 8);
DECLARE_CUBEMAP(IrradianceCubeTexture, 9);
DECLARE_TEXTURE(PrefilteredSpecularAtlasTexture, 10);
DECLARE_TEXTURE(BrdfLookupTexture, 11);
DECLARE_TEXTURE(PrefilteredLocalReflectionProbeAtlasTexture, 12);
DECLARE_TEXTURE(PrefilteredSecondaryLocalReflectionProbeAtlasTexture, 13);


BEGIN_CONSTANTS

    float4 DiffuseColor _vs(c0) _ps(c1) _cb(c0);
    float3 EmissiveColor _vs(c1) _ps(c2) _cb(c1);
    float3 SpecularColor _vs(c2) _ps(c3) _cb(c2);
    float SpecularPower _vs(c3) _ps(c4) _cb(c2.w);

    float3 DirLight0Direction _vs(c4) _ps(c5) _cb(c3);
    float3 DirLight0DiffuseColor _vs(c5) _ps(c6) _cb(c4);
    float3 DirLight0SpecularColor _vs(c6) _ps(c7) _cb(c5);

    float3 DirLight1Direction _vs(c7) _ps(c8) _cb(c6);
    float3 DirLight1DiffuseColor _vs(c8) _ps(c9) _cb(c7);
    float3 DirLight1SpecularColor _vs(c9) _ps(c10) _cb(c8);

    float3 DirLight2Direction _vs(c10) _ps(c11) _cb(c9);
    float3 DirLight2DiffuseColor _vs(c11) _ps(c12) _cb(c10);
    float3 DirLight2SpecularColor _vs(c12) _ps(c13) _cb(c11);

    float3 DirLight3Direction _vs(c13) _ps(c14) _cb(c12);
    float3 DirLight3DiffuseColor _vs(c14) _ps(c15) _cb(c13);
    float3 DirLight3SpecularColor _vs(c15) _ps(c16) _cb(c14);

    float3 DirLight4Direction _vs(c16) _ps(c17) _cb(c15);
    float3 DirLight4DiffuseColor _vs(c17) _ps(c18) _cb(c16);
    float3 DirLight4SpecularColor _vs(c18) _ps(c19) _cb(c17);

    float3 DirLight5Direction _vs(c19) _ps(c20) _cb(c18);
    float3 DirLight5DiffuseColor _vs(c20) _ps(c21) _cb(c19);
    float3 DirLight5SpecularColor _vs(c21) _ps(c22) _cb(c20);

    float3 DirLight6Direction _vs(c22) _ps(c23) _cb(c21);
    float3 DirLight6DiffuseColor _vs(c23) _ps(c24) _cb(c22);
    float3 DirLight6SpecularColor _vs(c24) _ps(c25) _cb(c23);

    float3 DirLight7Direction _vs(c25) _ps(c26) _cb(c24);
    float3 DirLight7DiffuseColor _vs(c26) _ps(c27) _cb(c25);
    float3 DirLight7SpecularColor _vs(c27) _ps(c28) _cb(c26);

    float ActiveDirectionalLightCount _ps(c29) _cb(c27.x);
    float ActivePointLightCount _vs(c29) _ps(c29) _cb(c27.y);
    float ActiveSpotLightCount _vs(c29) _ps(c29) _cb(c27.z);

    float4 PointLight0PositionAndRange;
    float4 PointLight1PositionAndRange;
    float4 PointLight2PositionAndRange;
    float4 PointLight3PositionAndRange;
    float4 PointLight4PositionAndRange;
    float4 PointLight5PositionAndRange;
    float4 PointLight6PositionAndRange;
    float4 PointLight7PositionAndRange;
    float4 PointLight0DiffuseColor;
    float4 PointLight1DiffuseColor;
    float4 PointLight2DiffuseColor;
    float4 PointLight3DiffuseColor;
    float4 PointLight4DiffuseColor;
    float4 PointLight5DiffuseColor;
    float4 PointLight6DiffuseColor;
    float4 PointLight7DiffuseColor;
    float4 PointLight0SpecularColor;
    float4 PointLight1SpecularColor;
    float4 PointLight2SpecularColor;
    float4 PointLight3SpecularColor;
    float4 PointLight4SpecularColor;
    float4 PointLight5SpecularColor;
    float4 PointLight6SpecularColor;
    float4 PointLight7SpecularColor;
    float4 SpotLight0PositionAndRange;
    float4 SpotLight1PositionAndRange;
    float4 SpotLight2PositionAndRange;
    float4 SpotLight3PositionAndRange;
    float4 SpotLight4PositionAndRange;
    float4 SpotLight5PositionAndRange;
    float4 SpotLight6PositionAndRange;
    float4 SpotLight7PositionAndRange;
    float4 SpotLight0DirectionAndInnerConeCos;
    float4 SpotLight1DirectionAndInnerConeCos;
    float4 SpotLight2DirectionAndInnerConeCos;
    float4 SpotLight3DirectionAndInnerConeCos;
    float4 SpotLight4DirectionAndInnerConeCos;
    float4 SpotLight5DirectionAndInnerConeCos;
    float4 SpotLight6DirectionAndInnerConeCos;
    float4 SpotLight7DirectionAndInnerConeCos;
    float4 SpotLight0DiffuseColor;
    float4 SpotLight1DiffuseColor;
    float4 SpotLight2DiffuseColor;
    float4 SpotLight3DiffuseColor;
    float4 SpotLight4DiffuseColor;
    float4 SpotLight5DiffuseColor;
    float4 SpotLight6DiffuseColor;
    float4 SpotLight7DiffuseColor;
    float4 SpotLight0SpecularColorAndOuterConeCos;
    float4 SpotLight1SpecularColorAndOuterConeCos;
    float4 SpotLight2SpecularColorAndOuterConeCos;
    float4 SpotLight3SpecularColorAndOuterConeCos;
    float4 SpotLight4SpecularColorAndOuterConeCos;
    float4 SpotLight5SpecularColorAndOuterConeCos;
    float4 SpotLight6SpecularColorAndOuterConeCos;
    float4 SpotLight7SpecularColorAndOuterConeCos;

    float3 EyePosition _vs(c28) _ps(c30) _cb(c28);
    float AlphaCutoff _ps(c31) _cb(c28.w);
    float3 AmbientColor _ps(c32) _cb(c29);
    float NormalScale _cb(c29.w);
    float4 BaseColor _cb(c30);
    float3 EnvironmentAmbientColor _ps(c34) _cb(c31);
    float EnvironmentSpecularIntensity _ps(c35) _cb(c31.w);
    float HasEnvironmentCubeTexture _ps(c36) _cb(c32.x);
    float Metallic _cb(c32.y);
    float UseFlatAmbientSource _ps(c36) _cb(c32.z);
    float ColorPipelineIsLinear _vs(c29) _ps(c36) _cb(c32.w);
    float HasLocalReflectionProbeTexture _ps(c37) _cb(c33.x);
    float HasSecondaryLocalReflectionProbeTexture _ps(c37) _cb(c33.y);
    float LocalReflectionProbeWeight _ps(c37) _cb(c33.z);
    float SecondaryLocalReflectionProbeWeight _ps(c37) _cb(c33.w);
    float LocalReflectionProbeInfluence _ps(c38) _cb(c34.x);
    float EnvironmentCubeIsLinear _ps(c38) _cb(c34.y);
    float Roughness _cb(c34.z);
    float OcclusionStrength _cb(c34.w);
    float HasBaseColorTexture _cb(c35.x);
    float HasMetallicRoughnessTexture _cb(c35.y);
    float HasOcclusionTexture _cb(c35.z);
    float HasEmissiveTexture _cb(c35.w);
    float HasIbl;
    float IblForceBaseLevel;
    float HasPrefilteredLocalReflectionProbe;
    float HasPrefilteredSecondaryLocalReflectionProbe;
    float ActiveShadowLightCount _ps(c100) _cb(c90.x);
    float ShadowedDirectionalLightIndex _ps(c100) _cb(c90.y);
    float ShadowDepthBias _ps(c100) _cb(c90.z);
    float ShadowNormalBias _ps(c100) _cb(c90.w);
    float2 ShadowMapTexelSize _ps(c101) _cb(c91.xy);
    float ReceiveShadows _ps(c101) _cb(c91.z);
    float ShadowApplyMode _ps(c101) _cb(c91.w);
    float3 ShadowTintColor _ps(c106) _cb(c96);
    float ShadowFilterRadius _ps(c106) _cb(c96.w);
    float ShadowFadeWidth _ps(c107) _cb(c97.x);

    float4x4 World _vs(c34) _cb(c33);
    float3x3 WorldInverseTranspose _vs(c38) _cb(c37);
    float4x4 ShadowLightViewProjection _ps(c102) _cb(c92);

MATRIX_CONSTANTS

    float4x4 WorldViewProj _vs(c31) _cb(c0);

END_CONSTANTS


#include "ColorPipeline.fxh"

#define HAS_ENVIRONMENT_BINDINGS 1
#define HAS_FORWARD_SHADOW_BINDINGS 1
#include "Structures.fxh"
#include "Lighting.fxh"
#include "Pbr.fxh"


// The base colour modulated by its texture, as a display colour (not decoded): the decoding is done once in ShadePbr.
float4 SamplePbrBaseColor(float2 texCoord)
{
    float4 baseColor = BaseColor;
    if (HasBaseColorTexture > 0.5f)
    {
        baseColor *= SAMPLE_TEXTURE(Texture, texCoord);
    }

    return baseColor;
}


void ApplyAlphaTest(float alpha)
{
    if (AlphaCutoff > 0.0f)
    {
        clip(alpha - AlphaCutoff);
    }
}


// ---- Image-based lighting (ADR-0109, plan T6.3) ----------------------------------------------------------------------------
// The specular result of the IBL is a 2D atlas, not a cube with mips: the OpenGL chain ignores the level of a cube sample, so a
// cube is never read at an explicit level for it. The atlas holds six latitude-longitude bands of 256 rows, band n
// prefiltered for the roughness n / 5.
static const float IblBandMax = 5.0f;           // number of prefiltered bands minus one: the band of roughness 1
static const float IblBandCount = 6.0f;         // bands stacked in the atlas
static const float IblBandHalfTexel = 0.5f / 256.0f; // half a texel of a band, in the vertical coordinate of the band
static const float IblBrdfHalfTexel = 0.5f / 64.0f;  // half a texel of the BRDF table
static const float IblInverseTwoPi = 0.15915494f;
static const float IblInversePi = 0.31830989f;
// The generated textures are linear light. The Legacy colour pipeline works in display colours: they are re-encoded there.
static const float IblLegacyEncodeExponent = 1.0f / 2.2f;


// Linear light of the IBL in the space of the view: unchanged when the colour pipeline is linear, re-encoded to display colour
// otherwise (the approximation is written in ADR-0109). It never decodes: nothing of the IBL is a display colour.
float3 IblToViewSpace(float3 linearLight)
{
    return ColorPipelineIsLinear > 0.5f ? linearLight : pow(max(linearLight, 0.0f), IblLegacyEncodeExponent);
}


// One band of a prefiltered atlas at the latitude-longitude coordinate uv (the mapping of
// PanoramaEnvironmentGenerator.GetPanoramaUv, twin of IblCubeGenerator.GetAtlasUv). The vertical coordinate stays half a texel
// inside the band so that the bilinear footprint never reaches the neighbouring band. source picks the atlas: 0 the global one
// (t10), 1 the atlas of the primary local probe (t12), 2 the one of the secondary local probe (t13); all three have the same layout.
float3 SampleIblPrefilteredBand(float2 uv, float band, float source)
{
    float localV = clamp(uv.y, IblBandHalfTexel, 1.0f - IblBandHalfTexel);
    float2 coord = float2(uv.x, (band + localV) / IblBandCount);

    float3 result = SAMPLE_TEXTURE(PrefilteredSpecularAtlasTexture, coord).rgb;
    if (source > 1.5f)
    {
        result = SAMPLE_TEXTURE(PrefilteredSecondaryLocalReflectionProbeAtlasTexture, coord).rgb;
    }
    else if (source > 0.5f)
    {
        result = SAMPLE_TEXTURE(PrefilteredLocalReflectionProbeAtlasTexture, coord).rgb;
    }

    return result;
}


// The prefiltered radiance of an atlas (see SampleIblPrefilteredBand for source) along direction for a perceptual roughness: two
// bands floor(b) and min(floor(b) + 1, 5) with b = roughness * 5, interpolated by frac(b). IblForceBaseLevel (a debug constant)
// forces band 0. The global atlas and the probe atlases are all read by this function, once.
float3 SampleIblPrefiltered(float3 direction, float roughness, float source)
{
    float2 uv;
    uv.x = 0.5f + atan2(direction.x, -direction.z) * IblInverseTwoPi;
    uv.y = 0.5f - asin(clamp(direction.y, -1.0f, 1.0f)) * IblInversePi;

    float b = roughness * IblBandMax;
    if (IblForceBaseLevel > 0.5f)
    {
        b = 0.0f;
    }

    float lowBand = floor(b);
    float highBand = min(lowBand + 1.0f, IblBandMax);
    float3 low = SampleIblPrefilteredBand(uv, lowBand, source);
    float3 high = SampleIblPrefilteredBand(uv, highBand, source);
    return lerp(low, high, frac(b));
}


// The split-sum scale A and bias B, read at (NoV, roughness) with the coordinates kept half a texel inside the table.
float2 SampleIblBrdf(float noV, float roughness)
{
    float2 uv = clamp(float2(noV, roughness), IblBrdfHalfTexel, 1.0f - IblBrdfHalfTexel);
    return SAMPLE_TEXTURE(BrdfLookupTexture, uv).rg;
}


// The ambient lighting of the surface, kept in two parts so a transparent surface scales only the diffuse one.
struct PbrAmbient
{
    float3 Diffuse;
    float3 Specular;
};


// The flat ambient of phase 3 (and the fallback while the IBL is not generated, HasIbl = 0): the flat ambient colour (or, when
// an environment cube is bound and the world does not ask for the flat source, its diffuse sample) times the occlusion times the
// diffuse albedo; no specular part.
PbrAmbient ComputePbrFlatAmbient(float3 worldNormal, float3 diffuseAlbedo, float occlusion)
{
    float3 ambient = (HasEnvironmentCubeTexture > 0.5f && UseFlatAmbientSource < 0.5f)
        ? SampleEnvironmentDiffuse(worldNormal) * EnvironmentAmbientColor
        : AmbientColor;

    PbrAmbient result;
    result.Diffuse = ambient * occlusion * diffuseAlbedo;
    result.Specular = 0.0f;
    return result;
}


// The ambient of the surface: with the IBL generated (HasIbl = 1) the irradiance times the diffuse albedo (the irradiance cube
// holds E / pi, so no further division) plus the prefiltered radiance times (F0 * A + B), both times the occlusion; the diffuse
// part is modulated by EnvironmentAmbientColor and the specular part by EnvironmentSpecularIntensity. The specular radiance is the
// global prefiltered atlas blended with the local probes (T7.1). With UseFlatAmbientSource
// the diffuse part stays the flat ambient colour and only the specular part comes from the atlas. Without the IBL: the flat
// ambient of phase 3.
PbrAmbient ComputePbrAmbient(PbrSurface surface, float occlusion)
{
    if (HasIbl < 0.5f)
    {
        return ComputePbrFlatAmbient(surface.Normal, surface.DiffuseAlbedo, occlusion);
    }

    float3 reflectionVector = reflect(-surface.EyeVector, surface.Normal);

    float3 diffuseAmbient = AmbientColor;
    if (UseFlatAmbientSource < 0.5f)
    {
        diffuseAmbient = IblToViewSpace(SAMPLE_CUBEMAP(IrradianceCubeTexture, surface.Normal).rgb) * EnvironmentAmbientColor;
    }

    float3 prefiltered = IblToViewSpace(SampleIblPrefiltered(reflectionVector, surface.PerceptualRoughness, 0.0f));

    // The local reflection probes replace the global radiance by their weighted blend, scaled by LocalReflectionProbeInfluence,
    // as SampleEnvironmentReflection does in Lighting.fxh. A probe whose prefiltered atlas exists is read by the same function as
    // the global atlas (HasPrefiltered... = 1); until then (the atlas is generated at the start of the next frame) it is sampled
    // unprefiltered from its cube, with the decoding of Lighting.fxh.
    float3 localReflection = 0;
    float localWeightTotal = 0;
    if (HasLocalReflectionProbeTexture > 0.5f)
    {
        float3 probe = DecodeColor(SAMPLE_CUBEMAP(LocalReflectionProbeCubeTexture, reflectionVector).rgb);
        if (HasPrefilteredLocalReflectionProbe > 0.5f)
        {
            probe = IblToViewSpace(SampleIblPrefiltered(reflectionVector, surface.PerceptualRoughness, 1.0f));
        }

        localReflection += probe * LocalReflectionProbeWeight;
        localWeightTotal += LocalReflectionProbeWeight;
    }

    if (HasSecondaryLocalReflectionProbeTexture > 0.5f)
    {
        float3 probe = DecodeColor(SAMPLE_CUBEMAP(SecondaryLocalReflectionProbeCubeTexture, reflectionVector).rgb);
        if (HasPrefilteredSecondaryLocalReflectionProbe > 0.5f)
        {
            probe = IblToViewSpace(SampleIblPrefiltered(reflectionVector, surface.PerceptualRoughness, 2.0f));
        }

        localReflection += probe * SecondaryLocalReflectionProbeWeight;
        localWeightTotal += SecondaryLocalReflectionProbeWeight;
    }

    if (localWeightTotal > 0.0f)
    {
        localReflection /= max(localWeightTotal, 0.0001f);
        prefiltered = lerp(prefiltered, localReflection, saturate(LocalReflectionProbeInfluence));
    }

    float2 brdf = SampleIblBrdf(surface.NoV, surface.PerceptualRoughness);

    PbrAmbient result;
    result.Diffuse = diffuseAmbient * occlusion * surface.DiffuseAlbedo;
    result.Specular = prefiltered * (surface.F0 * brdf.x + brdf.y) * (EnvironmentSpecularIntensity * occlusion);
    return result;
}


// Tangent-space normal from the normal map, scaled by NormalScale on its xy components (glTF), to a world-space normal.
float3 ComputePerturbedNormal(float2 texCoord, float3 normalWS, float3 tangentWS, float3 bitangentWS)
{
    float3 normalMap = SAMPLE_TEXTURE(NormalTexture, texCoord).rgb * 2.0f - 1.0f;
    normalMap.xy *= NormalScale;

    float3 N = normalize(normalWS);
    float3 T = normalize(tangentWS);
    float3 B = normalize(bitangentWS);
    float3x3 TBN = float3x3(T, B, N);
    return normalize(mul(normalMap, TBN));
}


// The shading shared by every technique. baseColor is the display colour with its alpha (see SamplePbrBaseColor); the
// material colours are decoded here, once per pixel.
float4 ShadePbr(float2 texCoord, float3 positionWS, float3 worldNormal, float4 baseColor, float diffuseOpacity)
{
    float3 linearBaseColor = DecodeColor(baseColor.rgb);

    float metallic = Metallic;
    float roughness = Roughness;
    if (HasMetallicRoughnessTexture > 0.5f)
    {
        float3 metallicRoughness = SAMPLE_TEXTURE(MetallicRoughnessTexture, texCoord).rgb;
        roughness *= metallicRoughness.g;
        metallic *= metallicRoughness.b;
    }

    float occlusion = 1.0f;
    if (HasOcclusionTexture > 0.5f)
    {
        occlusion = 1.0f + OcclusionStrength * (SAMPLE_TEXTURE(OcclusionTexture, texCoord).r - 1.0f);
    }

    float3 emissive = EmissiveColor;
    if (HasEmissiveTexture > 0.5f)
    {
        emissive *= SAMPLE_TEXTURE(EmissiveTexture, texCoord).rgb;
    }
    emissive = DecodeColor(emissive);

    float3 eyeVector = normalize(EyePosition - positionWS);
    PbrSurface surface = ComputePbrSurface(linearBaseColor, saturate(metallic), saturate(roughness), worldNormal, eyeVector);

    ColorPair lightResult = EvaluatePbrLights(surface, positionWS, (int)ActiveDirectionalLightCount);

    // diffuseOpacity is 1 for the opaque and alpha-test techniques. The transparent techniques pass the alpha of the base colour:
    // the diffuse parts (direct and ambient) are attenuated by it, the specular and the emissive parts are not, so a glass
    // surface keeps its highlights, the specular part of the ambient included. The output is premultiplied (the blend state is One,
    // InverseSourceAlpha).
    PbrAmbient ambient = ComputePbrAmbient(surface, occlusion);

    float3 color = lightResult.Diffuse * diffuseOpacity
        + lightResult.Specular
        + ambient.Diffuse * diffuseOpacity
        + ambient.Specular
        + emissive;

    // The shadow tint (ShadowApplyMode = 1, ADR-0101) applies to the whole pixel except the emissive part.
    color = ApplyShadowTint(color, emissive, lightResult.ShadowFactor);

    return float4(color, baseColor.a);
}


// Vertex shader: position, normal, texture coordinate.
VSOutputPixelLightingTx VSLitPbr(VSInputNmTx vin)
{
    VSOutputPixelLightingTx vout;

    CommonVSOutputPixelLighting cout = ComputeCommonVSOutputPixelLighting(vin.Position, vin.Normal);
    SetCommonVSOutputParamsPixelLighting;

    vout.Diffuse = float4(1, 1, 1, 1);
    vout.TexCoord = vin.TexCoord;

    return vout;
}


// Vertex shader: position, normal, texture coordinate, tangent (for the normal map).
VSOutputPixelLightingTxTan VSLitPbrTangent(VSInputNmTxTan vin)
{
    VSOutputPixelLightingTxTan vout;

    CommonVSOutputPixelLighting cout = ComputeCommonVSOutputPixelLighting(vin.Position, vin.Normal);

    vout.PositionPS = cout.Pos_ps;
    vout.PositionWS = float4(cout.Pos_ws, 1);
    vout.NormalWS = cout.Normal_ws;
    vout.Diffuse = float4(1, 1, 1, 1);
    vout.TexCoord = vin.TexCoord;
    vout.TangentWS = normalize(mul(vin.Tangent.xyz, (float3x3)World));
    vout.BitangentWS = cross(vout.NormalWS, vout.TangentWS) * vin.Tangent.w;

    return vout;
}


float4 PSLitPbr(VSOutputPixelLightingTx pin) : SV_Target0
{
    float4 baseColor = SamplePbrBaseColor(pin.TexCoord);
    return ShadePbr(pin.TexCoord, pin.PositionWS.xyz, normalize(pin.NormalWS), baseColor, 1.0f);
}


float4 PSLitPbrAlphaTest(VSOutputPixelLightingTx pin) : SV_Target0
{
    float4 baseColor = SamplePbrBaseColor(pin.TexCoord);
    ApplyAlphaTest(baseColor.a);
    return ShadePbr(pin.TexCoord, pin.PositionWS.xyz, normalize(pin.NormalWS), baseColor, 1.0f);
}


float4 PSLitPbrNormalMap(VSOutputPixelLightingTxTan pin) : SV_Target0
{
    float4 baseColor = SamplePbrBaseColor(pin.TexCoord);
    float3 worldNormal = ComputePerturbedNormal(pin.TexCoord, pin.NormalWS, pin.TangentWS, pin.BitangentWS);
    return ShadePbr(pin.TexCoord, pin.PositionWS.xyz, worldNormal, baseColor, 1.0f);
}


float4 PSLitPbrNormalMapAlphaTest(VSOutputPixelLightingTxTan pin) : SV_Target0
{
    float4 baseColor = SamplePbrBaseColor(pin.TexCoord);
    ApplyAlphaTest(baseColor.a);
    float3 worldNormal = ComputePerturbedNormal(pin.TexCoord, pin.NormalWS, pin.TangentWS, pin.BitangentWS);
    return ShadePbr(pin.TexCoord, pin.PositionWS.xyz, worldNormal, baseColor, 1.0f);
}


float4 PSLitPbrTransparent(VSOutputPixelLightingTx pin) : SV_Target0
{
    float4 baseColor = SamplePbrBaseColor(pin.TexCoord);
    return ShadePbr(pin.TexCoord, pin.PositionWS.xyz, normalize(pin.NormalWS), baseColor, baseColor.a);
}


float4 PSLitPbrNormalMapTransparent(VSOutputPixelLightingTxTan pin) : SV_Target0
{
    float4 baseColor = SamplePbrBaseColor(pin.TexCoord);
    float3 worldNormal = ComputePerturbedNormal(pin.TexCoord, pin.NormalWS, pin.TangentWS, pin.BitangentWS);
    return ShadePbr(pin.TexCoord, pin.PositionWS.xyz, worldNormal, baseColor, baseColor.a);
}


TECHNIQUE(LitPbr, VSLitPbr, PSLitPbr);
TECHNIQUE(LitPbr_AlphaTest, VSLitPbr, PSLitPbrAlphaTest);
TECHNIQUE(LitPbr_NormalMap, VSLitPbrTangent, PSLitPbrNormalMap);
TECHNIQUE(LitPbr_NormalMap_AlphaTest, VSLitPbrTangent, PSLitPbrNormalMapAlphaTest);
TECHNIQUE(LitPbr_Transparent, VSLitPbr, PSLitPbrTransparent);
TECHNIQUE(LitPbr_NormalMap_Transparent, VSLitPbrTangent, PSLitPbrNormalMapTransparent);
