//-----------------------------------------------------------------------------
// Pbr.fxh
//
// Metallic-roughness BRDF of the PBR lit shader (LitPbr.fx): GGX distribution, height-correlated Smith visibility, Schlick
// Fresnel and a Lambert diffuse lobe, evaluated against the same forward lights as Lighting.fxh.
//
// The including effect declares, BEFORE including this file: the light, shadow and environment constants that Lighting.fxh
// requires, defines HAS_ENVIRONMENT_BINDINGS and HAS_FORWARD_SHADOW_BINDINGS, and includes ColorPipeline.fxh and Lighting.fxh.
// This file reads from them the light-slot getters, MaxDirectionalLights, ColorPair, ComputeDirectionalShadowFactor and the
// light constants; it declares no light constant itself.
//
// Lighting.fxh is NOT modified and its Blinn-Phong accumulation is not reused: the attenuation of the point and spot lights
// is welded to the Blinn-Phong there (AccumulatePointLightSlot, AccumulateSpotLightSlot), and reshaping those 24 unrolled
// slots changed the legacy image on the OpenGL chain (plan open point O10). The attenuation formulas, the guards and the
// slot lists below are therefore RESTATED, identical to Lighting.fxh, and PbrShaderCoverageTests pins each of them against
// the original.
//
// Radiance: both lobes (diffuse and specular) use the DIFFUSE colour of the light, already multiplied by its intensity. The
// specular colour of a light (an authoring field of the Blinn-Phong model) is ignored by the PBR.
//
// The material colours reach this file already decoded to linear light (once per pixel, by the including effect): nothing
// here decodes a colour, in particular nothing inside the light slots (plan open point O10).
//
// Clear coat (ADR-0114). This file is also included by skinEffect.fx, which has no clear coat (plan decision P5) and declares no
// ClearCoat constant. The clear-coat code is therefore compiled ONLY when the including effect defines PBR_CLEAR_COAT before the
// include (LitPbr.fx does); without the define the preprocessed text of this file is the one it had before the clear coat, so
// skinEffect.fx compiles to the same bytes. The direct lobe is, with every symbol per light (N_g the geometric, interpolated
// vertex normal, never the normal-mapped one; V the eye vector; L the light vector; H = normalize(V + L); NoH_c = saturate(N_g . H);
// VoH = saturate(V . H) = LoH; irradiance = lightRadiance * NoL * attenuation, the very value of the base lobes, NoL from the
// surface normal):
//   alpha_c = max(ClearCoatRoughness, PbrMinRoughness)^2
//   F_d     = 0.04 + 0.96 * (1 - VoH)^5                              Schlick, scalar, F0 = 0.04
//   Fc      = ClearCoat * F_d                                        the lobe AND the attenuation of the base are weighted by ClearCoat
//   V_c     = 0.25 / max(VoH^2, 0.0001)                              Kelemen visibility (the floor keeps it finite at grazing VoH)
//   D_c     = alpha_c^2 / (pi * (NoH_c^2 * (alpha_c^2 - 1) + 1)^2)   GGX, the function of the base lobe
//   coat    = D_c * V_c * Fc * irradiance                            added to the specular part, NOT scaled by a transparent alpha
//   base    = (diffuse + specular) * (1 - Fc)                        both base lobes of the same light
// Evaluation: the base walk (EvaluatePbrLights, AccumulatePbrLight) is NOT changed. When ClearCoat > 0 the including effect calls
// EvaluatePbrClearCoatDirectional and EvaluatePbrClearCoatPointAndSpot after it: the same light slots walked a second time, each
// adding the correction  coat - Fc * (base diffuse + base specular)  of AccumulatePbrClearCoatLight, which makes the sum equal to
// the formulas above. With ClearCoat = 0 nothing of this runs and the result is the one of the shader without a clear coat.
// Measured on the OpenGL chain (cause not found, like plan open point O10): the same lobe written inside AccumulatePbrLight,
// even reduced to one extra dot product per slot, made every direct light vanish, and a second walk written as the [unroll]
// for loop with a break of EvaluatePbrLights did nothing; the explicit slot lists below are the form that works.
// The model is the clear-coat layer of Filament (https://google.github.io/filament/Filament.html#materialsystem/clearcoatmodel,
// GGX lobe, Kelemen visibility, Schlick Fresnel at F0 0.04, base scaled by the coat Fresnel); the absence of a coat normal map,
// the use of the slot irradiance and the PbrMinRoughness clamp are the author's arbitration (plan P4). Single scattering inside the
// coat and the absorption of the base colour under the coat are not modelled.
//-----------------------------------------------------------------------------

#ifndef PBR_FXH
#define PBR_FXH


static const float PbrPi = 3.14159265f;
// Smaller roughness values make alpha = roughness^2 so small that the GGX peak overflows the precision of a pixel shader.
static const float PbrMinRoughness = 0.045f;
static const float PbrDielectricF0 = 0.04f;


// Everything about the surface that does not depend on a light, computed once per pixel.
struct PbrSurface
{
    float3 Normal;
    float3 EyeVector;
    float3 DiffuseAlbedo;     // baseColor * (1 - metallic)
    float3 F0;                // lerp(0.04, baseColor, metallic)
    float NoV;
    float PerceptualRoughness; // the roughness as the material gives it (not raised to PbrMinRoughness): read by the image-based lighting
    float Alpha2;             // alpha^2 where alpha = roughness^2
    float AlphaSquaredOverPi; // the numerator of the GGX distribution
    float SmithViewTerm;      // sqrt(NoV * (NoV - NoV * alpha^2) + alpha^2), the light-independent half of the visibility
#ifdef PBR_CLEAR_COAT
    float ClearCoat;          // weight of the clear coat, 0 to 1; 0 leaves every other clear-coat field unread
    float3 CoatNormal;        // N_g, the geometric (interpolated vertex) normal
    float CoatNoV;            // max(dot(N_g, V), 0.0001)
    float CoatPerceptualRoughness; // ClearCoatRoughness as the material gives it: read by the image-based lighting
    float CoatAlpha2;         // alpha_c^2 where alpha_c = max(ClearCoatRoughness, PbrMinRoughness)^2
    float CoatAlphaSquaredOverPi;
#endif
};


PbrSurface ComputePbrSurface(float3 baseColor, float metallic, float roughness, float3 worldNormal, float3 eyeVector)
{
    PbrSurface surface;

    float clampedRoughness = max(roughness, PbrMinRoughness);
    float alpha = clampedRoughness * clampedRoughness;
    float alpha2 = alpha * alpha;
    float noV = max(dot(worldNormal, eyeVector), 0.0001f);

    surface.Normal = worldNormal;
    surface.EyeVector = eyeVector;
    surface.DiffuseAlbedo = baseColor * (1.0f - metallic);
    surface.F0 = lerp(float3(PbrDielectricF0, PbrDielectricF0, PbrDielectricF0), baseColor, metallic);
    surface.NoV = noV;
    surface.PerceptualRoughness = roughness;
    surface.Alpha2 = alpha2;
    surface.AlphaSquaredOverPi = alpha2 / PbrPi;
    surface.SmithViewTerm = sqrt((noV - alpha2 * noV) * noV + alpha2);

#ifdef PBR_CLEAR_COAT
    surface.ClearCoat = 0.0f;
    surface.CoatNormal = worldNormal;
    surface.CoatNoV = noV;
    surface.CoatPerceptualRoughness = 0.0f;
    surface.CoatAlpha2 = 0.0f;
    surface.CoatAlphaSquaredOverPi = 0.0f;
#endif

    return surface;
}


#ifdef PBR_CLEAR_COAT
// Fills the clear-coat fields of a surface. clearCoat and clearCoatRoughness are already saturated; geometricNormal is the
// normalized interpolated vertex normal. The fields are always written; nothing reads them while ClearCoat is 0.
void ApplyPbrClearCoat(inout PbrSurface surface, float clearCoat, float clearCoatRoughness, float3 geometricNormal)
{
    float alpha = max(clearCoatRoughness, PbrMinRoughness);
    alpha = alpha * alpha;
    float alpha2 = alpha * alpha;

    surface.ClearCoat = clearCoat;
    surface.CoatNormal = geometricNormal;
    surface.CoatNoV = max(dot(geometricNormal, surface.EyeVector), 0.0001f);
    surface.CoatPerceptualRoughness = clearCoatRoughness;
    surface.CoatAlpha2 = alpha2;
    surface.CoatAlphaSquaredOverPi = alpha2 / PbrPi;
}
#endif


// GGX (Trowbridge-Reitz) normal distribution, alpha2 / (pi * (NoH^2 * (alpha2 - 1) + 1)^2). The denominator is written as
// (1 - NoH^2) + NoH^2 * alpha2 with 1 - NoH^2 taken from the cross product of the normal and the half vector (Lagrange's
// identity): computed from NoH, the sum cancels down to the last bits of a float near the highlight of a very smooth surface,
// and the pixel then depends on how the driver rounds the operations (two runs of the same scene differed by a few levels).
float PbrDistributionGgx(float3 normal, float3 halfVector, float noH, float alpha2, float alphaSquaredOverPi)
{
    float3 normalCrossHalf = cross(normal, halfVector);
    float d = dot(normalCrossHalf, normalCrossHalf) + noH * noH * alpha2;
    return alphaSquaredOverPi / (d * d);
}


// Height-correlated Smith visibility, V = G / (4 NoL NoV): the denominator of the specular BRDF is already folded in.
float PbrVisibilitySmithGgxCorrelated(float noL, float alpha2, float noV, float smithViewTerm)
{
    float smithLightTerm = sqrt((noL - alpha2 * noL) * noL + alpha2);
    return 0.5f / (noL * smithViewTerm + noV * smithLightTerm);
}


// Schlick Fresnel.
float3 PbrFresnelSchlick(float3 f0, float voH)
{
    float oneMinusVoH = 1.0f - voH;
    float squared = oneMinusVoH * oneMinusVoH;
    return f0 + (1.0f - f0) * (squared * squared * oneMinusVoH);
}


// Adds one light to result.Diffuse and result.Specular (kept apart). lightRadiance is the diffuse colour of the light times its
// intensity; attenuation is 1 for a directional light. The BRDF terms are the ones of the surface, the light adds only its
// angles, so each slot costs one normalize, one sqrt and a few multiply-adds.
void AccumulatePbrLight(
    inout ColorPair result,
    in PbrSurface surface,
    float3 surfaceToLight,
    float attenuation,
    float3 lightRadiance)
{
    float3 halfVector = normalize(surface.EyeVector + surfaceToLight);
    float noL = saturate(dot(surface.Normal, surfaceToLight));
    float noH = saturate(dot(surface.Normal, halfVector));
    float voH = saturate(dot(surface.EyeVector, halfVector));

    float3 fresnel = PbrFresnelSchlick(surface.F0, voH);
    float distribution = PbrDistributionGgx(surface.Normal, halfVector, noH, surface.Alpha2, surface.AlphaSquaredOverPi);
    float visibility = PbrVisibilitySmithGgxCorrelated(noL, surface.Alpha2, surface.NoV, surface.SmithViewTerm);

    float3 irradiance = lightRadiance * (noL * attenuation);

    result.Diffuse += (1.0f - fresnel) * surface.DiffuseAlbedo * (irradiance / PbrPi);
    result.Specular += fresnel * (distribution * visibility) * irradiance;
}


#ifdef PBR_CLEAR_COAT
// The clear-coat part of one light, added after AccumulatePbrLight for the same light and the same attenuation: it removes
// Fc of the base lobes of that light and adds the coat lobe (see the header). The point and spot slots below restate the
// attenuation of AccumulatePbrPointLightSlot and AccumulatePbrSpotLightSlot, identical, and PbrShaderCoverageTests pins them.
void AccumulatePbrClearCoatLight(
    inout ColorPair result,
    in PbrSurface surface,
    float3 surfaceToLight,
    float attenuation,
    float3 lightRadiance)
{
    float3 halfVector = normalize(surface.EyeVector + surfaceToLight);
    float noL = saturate(dot(surface.Normal, surfaceToLight));
    float noH = saturate(dot(surface.Normal, halfVector));
    float voH = saturate(dot(surface.EyeVector, halfVector));

    float3 fresnel = PbrFresnelSchlick(surface.F0, voH);
    float distribution = PbrDistributionGgx(surface.Normal, halfVector, noH, surface.Alpha2, surface.AlphaSquaredOverPi);
    float visibility = PbrVisibilitySmithGgxCorrelated(noL, surface.Alpha2, surface.NoV, surface.SmithViewTerm);
    float3 irradiance = lightRadiance * (noL * attenuation);

    float coatNoH = saturate(dot(surface.CoatNormal, halfVector));
    float oneMinusVoH = 1.0f - voH;
    float squaredOneMinusVoH = oneMinusVoH * oneMinusVoH;
    float coatFresnel = surface.ClearCoat * (PbrDielectricF0 + (1.0f - PbrDielectricF0) * (squaredOneMinusVoH * squaredOneMinusVoH * oneMinusVoH));
    float coatVisibility = 0.25f / max(voH * voH, 0.0001f);
    float coatDistribution = PbrDistributionGgx(surface.CoatNormal, halfVector, coatNoH, surface.CoatAlpha2, surface.CoatAlphaSquaredOverPi);

    result.Diffuse -= coatFresnel * ((1.0f - fresnel) * surface.DiffuseAlbedo * (irradiance / PbrPi));
    result.Specular += (coatDistribution * coatVisibility - fresnel * (distribution * visibility)) * coatFresnel * irradiance;
}
#endif


void AccumulatePbrPointLightSlot(
    inout ColorPair result,
    in PbrSurface surface,
    float3 worldPosition,
    float4 lightPositionAndRange,
    float4 lightDiffuse)
{
    float3 toLight = lightPositionAndRange.xyz - worldPosition;
    float distanceToLight = length(toLight);
    float range = lightPositionAndRange.w;
    if (distanceToLight <= 0.0001f || range <= 0.0f)
    {
        return;
    }

    float attenuation = saturate(1.0f - distanceToLight / range);
    attenuation *= attenuation;
    if (attenuation <= 0.0f)
    {
        return;
    }

    AccumulatePbrLight(result, surface, toLight / distanceToLight, attenuation, lightDiffuse.xyz);
}


void AccumulatePbrPointLightSlots(inout ColorPair result, in PbrSurface surface, float3 worldPosition)
{
    int count = (int)ActivePointLightCount;
    if (count > 0) AccumulatePbrPointLightSlot(result, surface, worldPosition, PointLight0PositionAndRange, PointLight0DiffuseColor);
    if (count > 1) AccumulatePbrPointLightSlot(result, surface, worldPosition, PointLight1PositionAndRange, PointLight1DiffuseColor);
    if (count > 2) AccumulatePbrPointLightSlot(result, surface, worldPosition, PointLight2PositionAndRange, PointLight2DiffuseColor);
    if (count > 3) AccumulatePbrPointLightSlot(result, surface, worldPosition, PointLight3PositionAndRange, PointLight3DiffuseColor);
    if (count > 4) AccumulatePbrPointLightSlot(result, surface, worldPosition, PointLight4PositionAndRange, PointLight4DiffuseColor);
    if (count > 5) AccumulatePbrPointLightSlot(result, surface, worldPosition, PointLight5PositionAndRange, PointLight5DiffuseColor);
    if (count > 6) AccumulatePbrPointLightSlot(result, surface, worldPosition, PointLight6PositionAndRange, PointLight6DiffuseColor);
    if (count > 7) AccumulatePbrPointLightSlot(result, surface, worldPosition, PointLight7PositionAndRange, PointLight7DiffuseColor);
}


// specularAndOuterConeCos carries the outer cone cosine in .w; its .xyz (the Blinn-Phong specular colour of the light) is ignored.
void AccumulatePbrSpotLightSlot(
    inout ColorPair result,
    in PbrSurface surface,
    float3 worldPosition,
    float4 lightPositionAndRange,
    float4 directionAndInnerConeCos,
    float4 lightDiffuse,
    float4 specularAndOuterConeCos)
{
    float3 toLight = lightPositionAndRange.xyz - worldPosition;
    float distanceToLight = length(toLight);
    float range = lightPositionAndRange.w;
    if (distanceToLight <= 0.0001f || range <= 0.0f)
    {
        return;
    }

    float attenuation = saturate(1.0f - distanceToLight / range);
    attenuation *= attenuation;
    if (attenuation <= 0.0f)
    {
        return;
    }

    float3 surfaceToLight = toLight / distanceToLight;
    float spotCos = dot(directionAndInnerConeCos.xyz, -surfaceToLight);
    float coneAttenuation = saturate((spotCos - specularAndOuterConeCos.w) / max(directionAndInnerConeCos.w - specularAndOuterConeCos.w, 0.0001f));
    attenuation *= coneAttenuation;
    if (attenuation <= 0.0f)
    {
        return;
    }

    AccumulatePbrLight(result, surface, surfaceToLight, attenuation, lightDiffuse.xyz);
}


void AccumulatePbrSpotLightSlots(inout ColorPair result, in PbrSurface surface, float3 worldPosition)
{
    int count = (int)ActiveSpotLightCount;
    if (count > 0) AccumulatePbrSpotLightSlot(result, surface, worldPosition, SpotLight0PositionAndRange, SpotLight0DirectionAndInnerConeCos, SpotLight0DiffuseColor, SpotLight0SpecularColorAndOuterConeCos);
    if (count > 1) AccumulatePbrSpotLightSlot(result, surface, worldPosition, SpotLight1PositionAndRange, SpotLight1DirectionAndInnerConeCos, SpotLight1DiffuseColor, SpotLight1SpecularColorAndOuterConeCos);
    if (count > 2) AccumulatePbrSpotLightSlot(result, surface, worldPosition, SpotLight2PositionAndRange, SpotLight2DirectionAndInnerConeCos, SpotLight2DiffuseColor, SpotLight2SpecularColorAndOuterConeCos);
    if (count > 3) AccumulatePbrSpotLightSlot(result, surface, worldPosition, SpotLight3PositionAndRange, SpotLight3DirectionAndInnerConeCos, SpotLight3DiffuseColor, SpotLight3SpecularColorAndOuterConeCos);
    if (count > 4) AccumulatePbrSpotLightSlot(result, surface, worldPosition, SpotLight4PositionAndRange, SpotLight4DirectionAndInnerConeCos, SpotLight4DiffuseColor, SpotLight4SpecularColorAndOuterConeCos);
    if (count > 5) AccumulatePbrSpotLightSlot(result, surface, worldPosition, SpotLight5PositionAndRange, SpotLight5DirectionAndInnerConeCos, SpotLight5DiffuseColor, SpotLight5SpecularColorAndOuterConeCos);
    if (count > 6) AccumulatePbrSpotLightSlot(result, surface, worldPosition, SpotLight6PositionAndRange, SpotLight6DirectionAndInnerConeCos, SpotLight6DiffuseColor, SpotLight6SpecularColorAndOuterConeCos);
    if (count > 7) AccumulatePbrSpotLightSlot(result, surface, worldPosition, SpotLight7PositionAndRange, SpotLight7DirectionAndInnerConeCos, SpotLight7DiffuseColor, SpotLight7SpecularColorAndOuterConeCos);
}


// Walks the directional, point and spot lights. result.Diffuse and result.Specular are the two lobes, separate and without any
// emissive or ambient part; result.ShadowFactor is the visibility of the shadow-casting directional light (1 when there is none),
// for the MultiplyLitColor shadow mode (ShadowApplyMode = 1) where the light is left alone and the pixel is tinted afterwards.
// The shadow factor scales the light at ShadowedDirectionalLightIndex only, and only in the default shadow mode.
ColorPair EvaluatePbrLights(in PbrSurface surface, float3 worldPosition, uniform int numDirectionalLights)
{
    ColorPair result;
    result.Diffuse = 0;
    result.Specular = 0;

#ifdef HAS_FORWARD_SHADOW_BINDINGS
    float shadowFactor = ComputeDirectionalShadowFactor(worldPosition, surface.Normal);
    float lightOcclusionFactor = ShadowApplyMode > 0.5f ? 1.0f : shadowFactor;
#else
    float shadowFactor = 1.0f;
    float lightOcclusionFactor = 1.0f;
#endif
    // A shadow-casting light that this variant does not shade casts no tint either.
    result.ShadowFactor = (int)ShadowedDirectionalLightIndex < numDirectionalLights ? shadowFactor : 1.0f;

    [unroll]
    for (int i = 0; i < MaxDirectionalLights; i++)
    {
        if (i >= numDirectionalLights)
        {
            break;
        }

        float3 lightDirection = GetDirectionalLightDirection(i);
        float3 lightDiffuse = GetDirectionalLightDiffuseColor(i);
        float directionalShadowFactor = i == (int)ShadowedDirectionalLightIndex ? lightOcclusionFactor : 1.0f;
        AccumulatePbrLight(result, surface, -lightDirection, directionalShadowFactor, lightDiffuse);
    }

    AccumulatePbrPointLightSlots(result, surface, worldPosition);
    AccumulatePbrSpotLightSlots(result, surface, worldPosition);

    return result;
}


#ifdef PBR_CLEAR_COAT
// The clear coat of the directional lights, a second walk over the same lights after EvaluatePbrLights: it adds to
// lightResult the correction of AccumulatePbrClearCoatLight for each light. directionalOcclusion is the factor EvaluatePbrLights
// gave to the shadow-casting light (1 in the MultiplyLitColor mode, else lightResult.ShadowFactor).
void AccumulatePbrClearCoatDirectionalSlot(inout ColorPair result, in PbrSurface surface, int slot, float directionalOcclusion)
{
    float3 lightDirection = GetDirectionalLightDirection(slot);
    float3 lightDiffuse = GetDirectionalLightDiffuseColor(slot);
    float directionalShadowFactor = slot == (int)ShadowedDirectionalLightIndex ? directionalOcclusion : 1.0f;
    AccumulatePbrClearCoatLight(result, surface, -lightDirection, directionalShadowFactor, lightDiffuse);
}


void EvaluatePbrClearCoatDirectional(inout ColorPair result, in PbrSurface surface, int numDirectionalLights, float directionalOcclusion)
{
    if (numDirectionalLights > 0) AccumulatePbrClearCoatDirectionalSlot(result, surface, 0, directionalOcclusion);
    if (numDirectionalLights > 1) AccumulatePbrClearCoatDirectionalSlot(result, surface, 1, directionalOcclusion);
    if (numDirectionalLights > 2) AccumulatePbrClearCoatDirectionalSlot(result, surface, 2, directionalOcclusion);
    if (numDirectionalLights > 3) AccumulatePbrClearCoatDirectionalSlot(result, surface, 3, directionalOcclusion);
    if (numDirectionalLights > 4) AccumulatePbrClearCoatDirectionalSlot(result, surface, 4, directionalOcclusion);
    if (numDirectionalLights > 5) AccumulatePbrClearCoatDirectionalSlot(result, surface, 5, directionalOcclusion);
    if (numDirectionalLights > 6) AccumulatePbrClearCoatDirectionalSlot(result, surface, 6, directionalOcclusion);
    if (numDirectionalLights > 7) AccumulatePbrClearCoatDirectionalSlot(result, surface, 7, directionalOcclusion);
}


void AccumulatePbrClearCoatPointLightSlot(
    inout ColorPair result,
    in PbrSurface surface,
    float3 worldPosition,
    float4 lightPositionAndRange,
    float4 lightDiffuse)
{
    float3 toLight = lightPositionAndRange.xyz - worldPosition;
    float distanceToLight = length(toLight);
    float range = lightPositionAndRange.w;
    if (distanceToLight <= 0.0001f || range <= 0.0f)
    {
        return;
    }

    float attenuation = saturate(1.0f - distanceToLight / range);
    attenuation *= attenuation;
    if (attenuation <= 0.0f)
    {
        return;
    }

    AccumulatePbrClearCoatLight(result, surface, toLight / distanceToLight, attenuation, lightDiffuse.xyz);
}


void AccumulatePbrClearCoatSpotLightSlot(
    inout ColorPair result,
    in PbrSurface surface,
    float3 worldPosition,
    float4 lightPositionAndRange,
    float4 directionAndInnerConeCos,
    float4 lightDiffuse,
    float4 specularAndOuterConeCos)
{
    float3 toLight = lightPositionAndRange.xyz - worldPosition;
    float distanceToLight = length(toLight);
    float range = lightPositionAndRange.w;
    if (distanceToLight <= 0.0001f || range <= 0.0f)
    {
        return;
    }

    float attenuation = saturate(1.0f - distanceToLight / range);
    attenuation *= attenuation;
    if (attenuation <= 0.0f)
    {
        return;
    }

    float3 surfaceToLight = toLight / distanceToLight;
    float spotCos = dot(directionAndInnerConeCos.xyz, -surfaceToLight);
    float coneAttenuation = saturate((spotCos - specularAndOuterConeCos.w) / max(directionAndInnerConeCos.w - specularAndOuterConeCos.w, 0.0001f));
    attenuation *= coneAttenuation;
    if (attenuation <= 0.0f)
    {
        return;
    }

    AccumulatePbrClearCoatLight(result, surface, surfaceToLight, attenuation, lightDiffuse.xyz);
}


void EvaluatePbrClearCoatPointAndSpot(inout ColorPair result, in PbrSurface surface, float3 worldPosition)
{
    int pointCount = (int)ActivePointLightCount;
    if (pointCount > 0) AccumulatePbrClearCoatPointLightSlot(result, surface, worldPosition, PointLight0PositionAndRange, PointLight0DiffuseColor);
    if (pointCount > 1) AccumulatePbrClearCoatPointLightSlot(result, surface, worldPosition, PointLight1PositionAndRange, PointLight1DiffuseColor);
    if (pointCount > 2) AccumulatePbrClearCoatPointLightSlot(result, surface, worldPosition, PointLight2PositionAndRange, PointLight2DiffuseColor);
    if (pointCount > 3) AccumulatePbrClearCoatPointLightSlot(result, surface, worldPosition, PointLight3PositionAndRange, PointLight3DiffuseColor);
    if (pointCount > 4) AccumulatePbrClearCoatPointLightSlot(result, surface, worldPosition, PointLight4PositionAndRange, PointLight4DiffuseColor);
    if (pointCount > 5) AccumulatePbrClearCoatPointLightSlot(result, surface, worldPosition, PointLight5PositionAndRange, PointLight5DiffuseColor);
    if (pointCount > 6) AccumulatePbrClearCoatPointLightSlot(result, surface, worldPosition, PointLight6PositionAndRange, PointLight6DiffuseColor);
    if (pointCount > 7) AccumulatePbrClearCoatPointLightSlot(result, surface, worldPosition, PointLight7PositionAndRange, PointLight7DiffuseColor);

    int spotCount = (int)ActiveSpotLightCount;
    if (spotCount > 0) AccumulatePbrClearCoatSpotLightSlot(result, surface, worldPosition, SpotLight0PositionAndRange, SpotLight0DirectionAndInnerConeCos, SpotLight0DiffuseColor, SpotLight0SpecularColorAndOuterConeCos);
    if (spotCount > 1) AccumulatePbrClearCoatSpotLightSlot(result, surface, worldPosition, SpotLight1PositionAndRange, SpotLight1DirectionAndInnerConeCos, SpotLight1DiffuseColor, SpotLight1SpecularColorAndOuterConeCos);
    if (spotCount > 2) AccumulatePbrClearCoatSpotLightSlot(result, surface, worldPosition, SpotLight2PositionAndRange, SpotLight2DirectionAndInnerConeCos, SpotLight2DiffuseColor, SpotLight2SpecularColorAndOuterConeCos);
    if (spotCount > 3) AccumulatePbrClearCoatSpotLightSlot(result, surface, worldPosition, SpotLight3PositionAndRange, SpotLight3DirectionAndInnerConeCos, SpotLight3DiffuseColor, SpotLight3SpecularColorAndOuterConeCos);
    if (spotCount > 4) AccumulatePbrClearCoatSpotLightSlot(result, surface, worldPosition, SpotLight4PositionAndRange, SpotLight4DirectionAndInnerConeCos, SpotLight4DiffuseColor, SpotLight4SpecularColorAndOuterConeCos);
    if (spotCount > 5) AccumulatePbrClearCoatSpotLightSlot(result, surface, worldPosition, SpotLight5PositionAndRange, SpotLight5DirectionAndInnerConeCos, SpotLight5DiffuseColor, SpotLight5SpecularColorAndOuterConeCos);
    if (spotCount > 6) AccumulatePbrClearCoatSpotLightSlot(result, surface, worldPosition, SpotLight6PositionAndRange, SpotLight6DirectionAndInnerConeCos, SpotLight6DiffuseColor, SpotLight6SpecularColorAndOuterConeCos);
    if (spotCount > 7) AccumulatePbrClearCoatSpotLightSlot(result, surface, worldPosition, SpotLight7PositionAndRange, SpotLight7DirectionAndInnerConeCos, SpotLight7DiffuseColor, SpotLight7SpecularColorAndOuterConeCos);
}
#endif

#endif
