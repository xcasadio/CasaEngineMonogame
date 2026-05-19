//-----------------------------------------------------------------------------
// LitForward.fx
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

#include "Macros.fxh"


DECLARE_TEXTURE(Texture, 0);
DECLARE_TEXTURE(NormalTexture, 1);
DECLARE_CUBEMAP(ReflectionCubeTexture, 2);
DECLARE_CUBEMAP(EnvironmentCubeTexture, 3);
DECLARE_CUBEMAP(LocalReflectionProbeCubeTexture, 4);
DECLARE_CUBEMAP(SecondaryLocalReflectionProbeCubeTexture, 5);
DECLARE_TEXTURE(ShadowMapTexture, 6);


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

    float4 PointLightPositionAndRange[8] _vs(c40) _ps(c40) _cb(c36);
    float4 PointLightDiffuseColors[8] _vs(c48) _ps(c48) _cb(c44);
    float4 PointLightSpecularColors[8] _vs(c56) _ps(c56) _cb(c52);
    float4 SpotLightPositionAndRange[8] _vs(c64) _ps(c64) _cb(c60);
    float4 SpotLightDirectionAndInnerConeCos[8] _vs(c72) _ps(c72) _cb(c68);
    float4 SpotLightDiffuseColors[8] _vs(c80) _ps(c80) _cb(c76);
    float4 SpotLightSpecularColorsAndOuterConeCos[8] _vs(c88) _ps(c88) _cb(c84);

    float3 EyePosition _vs(c28) _ps(c30) _cb(c28);
    float AlphaCutoff _ps(c31) _cb(c28.w);
    float3 AmbientColor _ps(c32) _cb(c29);
    float3 MaterialAmbientColor _ps(c33) _cb(c30);
    float3 EnvironmentAmbientColor _ps(c34) _cb(c31);
    float EnvironmentSpecularIntensity _ps(c35) _cb(c31.w);
    float HasEnvironmentCubeTexture _ps(c36) _cb(c32.x);
    float HasMaterialReflectionCube _ps(c36) _cb(c32.y);
    float HasLocalReflectionProbeTexture _ps(c37) _cb(c33.x);
    float HasSecondaryLocalReflectionProbeTexture _ps(c37) _cb(c33.y);
    float LocalReflectionProbeWeight _ps(c37) _cb(c33.z);
    float SecondaryLocalReflectionProbeWeight _ps(c37) _cb(c33.w);
    float LocalReflectionProbeInfluence _ps(c38) _cb(c34.x);
    float ReflectionAddAmount _ps(c39) _cb(c35.x);
    float ReflectionMultiplyBase _ps(c39) _cb(c35.y);
    float ReflectionMultiplyFactor _ps(c39) _cb(c35.z);
    float ActiveShadowLightCount _ps(c100) _cb(c90.x);
    float ShadowedDirectionalLightIndex _ps(c100) _cb(c90.y);
    float ShadowDepthBias _ps(c100) _cb(c90.z);
    float ShadowNormalBias _ps(c100) _cb(c90.w);
    float2 ShadowMapTexelSize _ps(c101) _cb(c91.xy);
    float ReceiveShadows _ps(c101) _cb(c91.z);

    float4x4 World _vs(c34) _cb(c33);
    float3x3 WorldInverseTranspose _vs(c38) _cb(c37);
    float4x4 ShadowLightViewProjection _ps(c102) _cb(c92);

MATRIX_CONSTANTS

    float4x4 WorldViewProj _vs(c31) _cb(c0);

END_CONSTANTS


#define HAS_ENVIRONMENT_BINDINGS 1
#define HAS_FORWARD_SHADOW_BINDINGS 1
#include "Structures.fxh"
#include "Lighting.fxh"


void ApplyAlphaTest(float alpha)
{
    if (AlphaCutoff > 0.0f)
    {
        clip(alpha - AlphaCutoff);
    }
}


float4 SampleBaseTexture(float2 texCoord)
{
    return SAMPLE_TEXTURE(Texture, texCoord);
}


float3 ComputeBaseAmbientTerm(float3 baseColor, float3 worldNormal)
{
    float3 ambientTerm = HasEnvironmentCubeTexture > 0.5f
        ? ComputeEnvironmentDiffuseTerm(SampleEnvironmentDiffuse(worldNormal) * EnvironmentAmbientColor, MaterialAmbientColor)
        : ComputeAmbientTerm(AmbientColor, MaterialAmbientColor);
    return baseColor * DiffuseColor.rgb * ambientTerm;
}


float3 ComputeDirectDiffuse(ColorPair lightResult)
{
    return max(lightResult.Diffuse - EmissiveColor, 0.0f);
}


float3 ComputeReflectionContribution(float3 eyeVector, float3 worldNormal)
{
    float3 reflectionVector = normalize(reflect(-eyeVector, worldNormal));
    float3 reflectionColor = HasMaterialReflectionCube > 0.5f
        ? SAMPLE_CUBEMAP(ReflectionCubeTexture, reflectionVector).rgb
        : SampleEnvironmentReflection(reflectionVector);
    return reflectionColor * saturate(SpecularColor);
}


float3 ApplyReflectionContribution(float3 surfaceColor, float3 reflectionContribution)
{
    return surfaceColor * (ReflectionMultiplyBase + reflectionContribution * ReflectionMultiplyFactor)
        + reflectionContribution * ReflectionAddAmount;
}


// Vertex shader: basic.
VSOutput VSBasic(VSInput vin)
{
    VSOutput vout;
    
    CommonVSOutput cout = ComputeCommonVSOutput(vin.Position);
    SetCommonVSOutputParams;
    
    return vout;
}


// Vertex shader: vertex color.
VSOutput VSBasicVc(VSInputVc vin)
{
    VSOutput vout;
    
    CommonVSOutput cout = ComputeCommonVSOutput(vin.Position);
    SetCommonVSOutputParams;
    
    vout.Diffuse *= vin.Color;
    
    return vout;
}


// Vertex shader: texture.
VSOutputTx VSBasicTx(VSInputTx vin)
{
    VSOutputTx vout;
    
    CommonVSOutput cout = ComputeCommonVSOutput(vin.Position);
    SetCommonVSOutputParams;
    
    vout.TexCoord = vin.TexCoord;

    return vout;
}


// Vertex shader: texture + vertex color.
VSOutputTx VSBasicTxVc(VSInputTxVc vin)
{
    VSOutputTx vout;
    
    CommonVSOutput cout = ComputeCommonVSOutput(vin.Position);
    SetCommonVSOutputParams;
    
    vout.TexCoord = vin.TexCoord;
    vout.Diffuse *= vin.Color;
    
    return vout;
}


// Vertex shader: vertex lighting.
VSOutput VSBasicVertexLighting(VSInputNm vin)
{
    VSOutput vout;
    
    CommonVSOutput cout = ComputeCommonVSOutputWithLighting(vin.Position, vin.Normal, 3);
    SetCommonVSOutputParams;
    
    return vout;
}


// Vertex shader: vertex lighting + vertex color.
VSOutput VSBasicVertexLightingVc(VSInputNmVc vin)
{
    VSOutput vout;
    
    CommonVSOutput cout = ComputeCommonVSOutputWithLighting(vin.Position, vin.Normal, 3);
    SetCommonVSOutputParams;
    
    vout.Diffuse *= vin.Color;
    
    return vout;
}


// Vertex shader: vertex lighting + texture.
VSOutputTx VSBasicVertexLightingTx(VSInputNmTx vin)
{
    VSOutputTx vout;
    
    CommonVSOutput cout = ComputeCommonVSOutputWithLighting(vin.Position, vin.Normal, 3);
    SetCommonVSOutputParams;
    
    vout.TexCoord = vin.TexCoord;

    return vout;
}


// Vertex shader: vertex lighting + texture + vertex color.
VSOutputTx VSBasicVertexLightingTxVc(VSInputNmTxVc vin)
{
    VSOutputTx vout;
    
    CommonVSOutput cout = ComputeCommonVSOutputWithLighting(vin.Position, vin.Normal, 3);
    SetCommonVSOutputParams;
    
    vout.TexCoord = vin.TexCoord;
    vout.Diffuse *= vin.Color;
    
    return vout;
}


// Vertex shader: one light.
VSOutput VSBasicOneLight(VSInputNm vin)
{
    VSOutput vout;
    
    CommonVSOutput cout = ComputeCommonVSOutputWithLighting(vin.Position, vin.Normal, 1);
    SetCommonVSOutputParams;
    
    return vout;
}


// Vertex shader: one light + vertex color.
VSOutput VSBasicOneLightVc(VSInputNmVc vin)
{
    VSOutput vout;
    
    CommonVSOutput cout = ComputeCommonVSOutputWithLighting(vin.Position, vin.Normal, 1);
    SetCommonVSOutputParams;
    
    vout.Diffuse *= vin.Color;
    
    return vout;
}


// Vertex shader: one light + texture.
VSOutputTx VSBasicOneLightTx(VSInputNmTx vin)
{
    VSOutputTx vout;
    
    CommonVSOutput cout = ComputeCommonVSOutputWithLighting(vin.Position, vin.Normal, 1);
    SetCommonVSOutputParams;
    
    vout.TexCoord = vin.TexCoord;

    return vout;
}


// Vertex shader: one light + texture + vertex color.
VSOutputTx VSBasicOneLightTxVc(VSInputNmTxVc vin)
{
    VSOutputTx vout;
    
    CommonVSOutput cout = ComputeCommonVSOutputWithLighting(vin.Position, vin.Normal, 1);
    SetCommonVSOutputParams;
    
    vout.TexCoord = vin.TexCoord;
    vout.Diffuse *= vin.Color;
    
    return vout;
}


// Vertex shader: pixel lighting.
VSOutputPixelLighting VSBasicPixelLighting(VSInputNm vin)
{
    VSOutputPixelLighting vout;
    
    CommonVSOutputPixelLighting cout = ComputeCommonVSOutputPixelLighting(vin.Position, vin.Normal);
    SetCommonVSOutputParamsPixelLighting;

    vout.Diffuse = float4(1, 1, 1, DiffuseColor.a);
    
    return vout;
}


// Vertex shader: pixel lighting + vertex color.
VSOutputPixelLighting VSBasicPixelLightingVc(VSInputNmVc vin)
{
    VSOutputPixelLighting vout;
    
    CommonVSOutputPixelLighting cout = ComputeCommonVSOutputPixelLighting(vin.Position, vin.Normal);
    SetCommonVSOutputParamsPixelLighting;
    
    vout.Diffuse.rgb = vin.Color.rgb;
    vout.Diffuse.a = vin.Color.a * DiffuseColor.a;
    
    return vout;
}


// Vertex shader: pixel lighting + texture.
VSOutputPixelLightingTx VSBasicPixelLightingTx(VSInputNmTx vin)
{
    VSOutputPixelLightingTx vout;
    
    CommonVSOutputPixelLighting cout = ComputeCommonVSOutputPixelLighting(vin.Position, vin.Normal);
    SetCommonVSOutputParamsPixelLighting;
    
    vout.Diffuse = float4(1, 1, 1, DiffuseColor.a);
    vout.TexCoord = vin.TexCoord;

    return vout;
}


// Vertex shader: pixel lighting + texture + vertex color.
VSOutputPixelLightingTx VSBasicPixelLightingTxVc(VSInputNmTxVc vin)
{
    VSOutputPixelLightingTx vout;
    
    CommonVSOutputPixelLighting cout = ComputeCommonVSOutputPixelLighting(vin.Position, vin.Normal);
    SetCommonVSOutputParamsPixelLighting;
    
    vout.Diffuse.rgb = vin.Color.rgb;
    vout.Diffuse.a = vin.Color.a * DiffuseColor.a;
    vout.TexCoord = vin.TexCoord;
    
    return vout;
}


// Pixel shader: basic.
float4 PSBasic(VSOutput pin) : SV_Target0
{
    ApplyAlphaTest(pin.Diffuse.a);
    return pin.Diffuse;
}


// Pixel shader: texture.
float4 PSBasicTx(VSOutputTx pin) : SV_Target0
{
    float4 color = SampleBaseTexture(pin.TexCoord) * pin.Diffuse;
    ApplyAlphaTest(color.a);
    return color;
}


// Pixel shader: vertex lighting.
float4 PSBasicVertexLighting(VSOutput pin) : SV_Target0
{
    float4 color = pin.Diffuse;

    ApplyAlphaTest(color.a);

    AddSpecular(color, pin.Specular.rgb);
    
    return color;
}


// Pixel shader: vertex lighting + texture.
float4 PSBasicVertexLightingTx(VSOutputTx pin) : SV_Target0
{
    float4 color = SampleBaseTexture(pin.TexCoord) * pin.Diffuse;

    ApplyAlphaTest(color.a);
    
    AddSpecular(color, pin.Specular.rgb);
    
    return color;
}


// Pixel shader: pixel lighting.
float4 PSBasicPixelLighting(VSOutputPixelLighting pin) : SV_Target0
{
    float4 color = pin.Diffuse;

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);
    
    ColorPair lightResult = ComputeLights(eyeVector, pin.PositionWS.xyz, worldNormal, (int)ActiveDirectionalLightCount);

    color.rgb = ComposeLitSurfaceColor(
        color.rgb,
        ComputeDirectDiffuse(lightResult),
        ComputeBaseAmbientTerm(color.rgb, worldNormal),
        EmissiveColor);

    ApplyAlphaTest(color.a);
    
    AddSpecular(color, lightResult.Specular);
    
    return color;
}


// Pixel shader: pixel lighting + texture.
float4 PSBasicPixelLightingTx(VSOutputPixelLightingTx pin) : SV_Target0
{
    float4 color = SampleBaseTexture(pin.TexCoord) * pin.Diffuse;
    
    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);
    
    ColorPair lightResult = ComputeLights(eyeVector, pin.PositionWS.xyz, worldNormal, (int)ActiveDirectionalLightCount);

    color.rgb = ComposeLitSurfaceColor(
        color.rgb,
        ComputeDirectDiffuse(lightResult),
        ComputeBaseAmbientTerm(color.rgb, worldNormal),
        EmissiveColor);

    ApplyAlphaTest(color.a);

    AddSpecular(color, lightResult.Specular);
    
    return color;
}


// Pixel shader: pixel lighting (one light only).
float4 PSBasicPixelLightingOneLight(VSOutputPixelLighting pin) : SV_Target0
{
    float4 color = pin.Diffuse;

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);

    ColorPair lightResult = ComputeLights(eyeVector, pin.PositionWS.xyz, worldNormal, 1);

    color.rgb = ComposeLitSurfaceColor(
        color.rgb,
        ComputeDirectDiffuse(lightResult),
        ComputeBaseAmbientTerm(color.rgb, worldNormal),
        EmissiveColor);

    ApplyAlphaTest(color.a);

    AddSpecular(color, lightResult.Specular);

    return color;
}


// Pixel shader: pixel lighting + texture (one light only).
float4 PSBasicPixelLightingTxOneLight(VSOutputPixelLightingTx pin) : SV_Target0
{
    float4 color = SampleBaseTexture(pin.TexCoord) * pin.Diffuse;

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);

    ColorPair lightResult = ComputeLights(eyeVector, pin.PositionWS.xyz, worldNormal, 1);

    color.rgb = ComposeLitSurfaceColor(
        color.rgb,
        ComputeDirectDiffuse(lightResult),
        ComputeBaseAmbientTerm(color.rgb, worldNormal),
        EmissiveColor);

    ApplyAlphaTest(color.a);

    AddSpecular(color, lightResult.Specular);

    return color;
}


// Vertex shader: pixel lighting + texture + tangent (for normal mapping).
VSOutputPixelLightingTxTan VSBasicPixelLightingTxTan(VSInputNmTxTan vin)
{
    VSOutputPixelLightingTxTan vout;

    CommonVSOutputPixelLighting cout = ComputeCommonVSOutputPixelLighting(vin.Position, vin.Normal);

    vout.PositionPS = cout.Pos_ps;
    vout.PositionWS = float4(cout.Pos_ws, 1);
    vout.NormalWS = cout.Normal_ws;
    vout.Diffuse = float4(1, 1, 1, DiffuseColor.a);
    vout.TexCoord = vin.TexCoord;
    vout.TangentWS = normalize(mul(vin.Tangent.xyz, (float3x3)World));
    vout.BitangentWS = cross(vout.NormalWS, vout.TangentWS) * vin.Tangent.w;

    return vout;
}


// Pixel shader: pixel lighting + texture + normal map.
float4 PSBasicPixelLightingTxNorm(VSOutputPixelLightingTxTan pin) : SV_Target0
{
    float4 color = SampleBaseTexture(pin.TexCoord) * pin.Diffuse;

    // Sample normal map and unpack from [0,1] to [-1,1]
    float3 normalMap = SAMPLE_TEXTURE(NormalTexture, pin.TexCoord).rgb * 2.0 - 1.0;

    // Build TBN matrix and transform normal to world space
    float3 N = normalize(pin.NormalWS);
    float3 T = normalize(pin.TangentWS);
    float3 B = normalize(pin.BitangentWS);
    float3x3 TBN = float3x3(T, B, N);
    float3 worldNormal = normalize(mul(normalMap, TBN));

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    ColorPair lightResult = ComputeLights(eyeVector, pin.PositionWS.xyz, worldNormal, (int)ActiveDirectionalLightCount);

    color.rgb = ComposeLitSurfaceColor(
        color.rgb,
        ComputeDirectDiffuse(lightResult),
        ComputeBaseAmbientTerm(color.rgb, worldNormal),
        EmissiveColor);
    ApplyAlphaTest(color.a);
    AddSpecular(color, lightResult.Specular);

    return color;
}


float4 PSBasicPixelLightingReflection(VSOutputPixelLighting pin) : SV_Target0
{
    float4 color = PSBasicPixelLighting(pin);

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);
    color.rgb = ApplyReflectionContribution(color.rgb, ComputeReflectionContribution(eyeVector, worldNormal));

    return color;
}


float4 PSBasicPixelLightingTxReflection(VSOutputPixelLightingTx pin) : SV_Target0
{
    float4 color = PSBasicPixelLightingTx(pin);

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);
    color.rgb = ApplyReflectionContribution(color.rgb, ComputeReflectionContribution(eyeVector, worldNormal));

    return color;
}


float4 PSBasicPixelLightingTxNormReflection(VSOutputPixelLightingTxTan pin) : SV_Target0
{
    float4 color = SampleBaseTexture(pin.TexCoord) * pin.Diffuse;

    float3 normalMap = SAMPLE_TEXTURE(NormalTexture, pin.TexCoord).rgb * 2.0 - 1.0;

    float3 N = normalize(pin.NormalWS);
    float3 T = normalize(pin.TangentWS);
    float3 B = normalize(pin.BitangentWS);
    float3x3 TBN = float3x3(T, B, N);
    float3 worldNormal = normalize(mul(normalMap, TBN));

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    ColorPair lightResult = ComputeLights(eyeVector, pin.PositionWS.xyz, worldNormal, (int)ActiveDirectionalLightCount);

    color.rgb = ComposeLitSurfaceColor(
        color.rgb,
        ComputeDirectDiffuse(lightResult),
        ComputeBaseAmbientTerm(color.rgb, worldNormal),
        EmissiveColor);

    ApplyAlphaTest(color.a);
    AddSpecular(color, lightResult.Specular);
    color.rgb = ApplyReflectionContribution(color.rgb, ComputeReflectionContribution(eyeVector, worldNormal));

    return color;
}


// NOTE: The ordering below preserves the historical XNA BasicEffect layout
// even though the CasaEngine shader now carries a semantic LitForward name.

TECHNIQUE(LitForward, VSBasic, PSBasic);
TECHNIQUE(LitForward_VertexColor, VSBasicVc, PSBasic);
TECHNIQUE(LitForward_Texture, VSBasicTx, PSBasicTx);
TECHNIQUE(LitForward_Texture_VertexColor, VSBasicTxVc, PSBasicTx);

TECHNIQUE(LitForward_VertexLighting, VSBasicVertexLighting, PSBasicVertexLighting);
TECHNIQUE(LitForward_VertexLighting_VertexColor, VSBasicVertexLightingVc, PSBasicVertexLighting);
TECHNIQUE(LitForward_VertexLighting_Texture, VSBasicVertexLightingTx, PSBasicVertexLightingTx);
TECHNIQUE(LitForward_VertexLighting_Texture_VertexColor, VSBasicVertexLightingTxVc, PSBasicVertexLightingTx);

TECHNIQUE(LitForward_OneLight, VSBasicOneLight, PSBasicVertexLighting);
TECHNIQUE(LitForward_OneLight_VertexColor, VSBasicOneLightVc, PSBasicVertexLighting);
TECHNIQUE(LitForward_OneLight_Texture, VSBasicOneLightTx, PSBasicVertexLightingTx);
TECHNIQUE(LitForward_OneLight_Texture_VertexColor, VSBasicOneLightTxVc, PSBasicVertexLightingTx);

TECHNIQUE(LitForward_PixelLighting, VSBasicPixelLighting, PSBasicPixelLighting);
TECHNIQUE(LitForward_PixelLighting_VertexColor, VSBasicPixelLightingVc, PSBasicPixelLighting);
TECHNIQUE(LitForward_PixelLighting_Texture, VSBasicPixelLightingTx, PSBasicPixelLightingTx);
TECHNIQUE(LitForward_PixelLighting_Texture_VertexColor, VSBasicPixelLightingTxVc, PSBasicPixelLightingTx);

TECHNIQUE(LitForward_PixelLighting_OneLight, VSBasicPixelLighting, PSBasicPixelLightingOneLight);
TECHNIQUE(LitForward_PixelLighting_OneLight_VertexColor, VSBasicPixelLightingVc, PSBasicPixelLightingOneLight);
TECHNIQUE(LitForward_PixelLighting_OneLight_Texture, VSBasicPixelLightingTx, PSBasicPixelLightingTxOneLight);
TECHNIQUE(LitForward_PixelLighting_OneLight_Texture_VertexColor, VSBasicPixelLightingTxVc, PSBasicPixelLightingTxOneLight);

TECHNIQUE(LitForward_PixelLighting_Texture_NormalMap, VSBasicPixelLightingTxTan, PSBasicPixelLightingTxNorm);
TECHNIQUE(LitForward_PixelLighting_Reflection, VSBasicPixelLighting, PSBasicPixelLightingReflection);
TECHNIQUE(LitForward_PixelLighting_Texture_Reflection, VSBasicPixelLightingTx, PSBasicPixelLightingTxReflection);
TECHNIQUE(LitForward_PixelLighting_Texture_NormalMap_Reflection, VSBasicPixelLightingTxTan, PSBasicPixelLightingTxNormReflection);
