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
    float Alpha2;             // alpha^2 where alpha = roughness^2
    float AlphaSquaredOverPi; // the numerator of the GGX distribution
    float SmithViewTerm;      // sqrt(NoV * (NoV - NoV * alpha^2) + alpha^2), the light-independent half of the visibility
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
    surface.Alpha2 = alpha2;
    surface.AlphaSquaredOverPi = alpha2 / PbrPi;
    surface.SmithViewTerm = sqrt((noV - alpha2 * noV) * noV + alpha2);

    return surface;
}


// GGX (Trowbridge-Reitz) normal distribution.
float PbrDistributionGgx(float noH, float alpha2, float alphaSquaredOverPi)
{
    float d = noH * noH * (alpha2 - 1.0f) + 1.0f;
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
    float distribution = PbrDistributionGgx(noH, surface.Alpha2, surface.AlphaSquaredOverPi);
    float visibility = PbrVisibilitySmithGgxCorrelated(noL, surface.Alpha2, surface.NoV, surface.SmithViewTerm);

    float3 irradiance = lightRadiance * (noL * attenuation);

    result.Diffuse += (1.0f - fresnel) * surface.DiffuseAlbedo * (irradiance / PbrPi);
    result.Specular += fresnel * (distribution * visibility) * irradiance;
}


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

#endif
