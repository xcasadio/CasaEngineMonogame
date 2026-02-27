//-----------------------------------------------------------------------------
// BasicEffect.fx
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

#include "Macros.fxh"


DECLARE_TEXTURE(Texture, 0);
DECLARE_TEXTURE(NormalTexture, 1);


BEGIN_PER_FRAME

    float3 DirLightDirections[3]    _vs(c4) _ps(c5) _cb(c0);
    float3 DirLightDiffuseColors[3] _vs(c7) _ps(c8) _cb(c3);
    float3 DirLightSpecularColors[3] _vs(c10) _ps(c11) _cb(c6);

    float3 EyePosition _vs(c13) _ps(c14) _cb(c9);

END_PER_FRAME

BEGIN_PER_OBJECT

    float4 DiffuseColor _vs(c0) _ps(c1) _cb(c0);
    float3 EmissiveColor _vs(c1) _ps(c2) _cb(c1);
    float3 SpecularColor _vs(c2) _ps(c3) _cb(c2);
    float SpecularPower _vs(c3) _ps(c4) _cb(c2.w);

    float4x4 World _vs(c19) _cb(c3);
    float3x3 WorldInverseTranspose _vs(c23) _cb(c7);
    float4x4 WorldViewProj _vs(c15) _cb(c10);

END_PER_OBJECT


#include "Structures.fxh"
#include "Lighting.fxh"


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
    return pin.Diffuse;
}


// Pixel shader: texture.
float4 PSBasicTx(VSOutputTx pin) : SV_Target0
{
    return SAMPLE_TEXTURE(Texture, pin.TexCoord) * pin.Diffuse;
}


// Pixel shader: vertex lighting.
float4 PSBasicVertexLighting(VSOutput pin) : SV_Target0
{
    float4 color = pin.Diffuse;

    AddSpecular(color, pin.Specular.rgb);
    
    return color;
}


// Pixel shader: vertex lighting + texture.
float4 PSBasicVertexLightingTx(VSOutputTx pin) : SV_Target0
{
    float4 color = SAMPLE_TEXTURE(Texture, pin.TexCoord) * pin.Diffuse;
    
    AddSpecular(color, pin.Specular.rgb);
    
    return color;
}


// Pixel shader: pixel lighting.
float4 PSBasicPixelLighting(VSOutputPixelLighting pin) : SV_Target0
{
    float4 color = pin.Diffuse;

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);
    
    ColorPair lightResult = ComputeLights(eyeVector, worldNormal, 3);

    color.rgb *= lightResult.Diffuse;
    
    AddSpecular(color, lightResult.Specular);
    
    return color;
}


// Pixel shader: pixel lighting + texture.
float4 PSBasicPixelLightingTx(VSOutputPixelLightingTx pin) : SV_Target0
{
    float4 color = SAMPLE_TEXTURE(Texture, pin.TexCoord) * pin.Diffuse;
    
    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);
    
    ColorPair lightResult = ComputeLights(eyeVector, worldNormal, 3);
    
    color.rgb *= lightResult.Diffuse;

    AddSpecular(color, lightResult.Specular);
    
    return color;
}


// Pixel shader: pixel lighting (one light only).
float4 PSBasicPixelLightingOneLight(VSOutputPixelLighting pin) : SV_Target0
{
    float4 color = pin.Diffuse;

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);

    ColorPair lightResult = ComputeLights(eyeVector, worldNormal, 1);

    color.rgb *= lightResult.Diffuse;

    AddSpecular(color, lightResult.Specular);

    return color;
}


// Pixel shader: pixel lighting + texture (one light only).
float4 PSBasicPixelLightingTxOneLight(VSOutputPixelLightingTx pin) : SV_Target0
{
    float4 color = SAMPLE_TEXTURE(Texture, pin.TexCoord) * pin.Diffuse;

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    float3 worldNormal = normalize(pin.NormalWS);

    ColorPair lightResult = ComputeLights(eyeVector, worldNormal, 1);

    color.rgb *= lightResult.Diffuse;

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
    float4 color = SAMPLE_TEXTURE(Texture, pin.TexCoord) * pin.Diffuse;

    // Sample normal map and unpack from [0,1] to [-1,1]
    float3 normalMap = SAMPLE_TEXTURE(NormalTexture, pin.TexCoord).rgb * 2.0 - 1.0;

    // Build TBN matrix and transform normal to world space
    float3 N = normalize(pin.NormalWS);
    float3 T = normalize(pin.TangentWS);
    float3 B = normalize(pin.BitangentWS);
    float3x3 TBN = float3x3(T, B, N);
    float3 worldNormal = normalize(mul(normalMap, TBN));

    float3 eyeVector = normalize(EyePosition - pin.PositionWS.xyz);
    ColorPair lightResult = ComputeLights(eyeVector, worldNormal, 3);

    color.rgb *= lightResult.Diffuse;
    AddSpecular(color, lightResult.Specular);

    return color;
}


// NOTE: The order of the techniques here are
// defined to match the indexing in BasicEffect.cs.

TECHNIQUE(BasicEffect, VSBasic, PSBasic);
TECHNIQUE(BasicEffect_VertexColor, VSBasicVc, PSBasic);
TECHNIQUE(BasicEffect_Texture, VSBasicTx, PSBasicTx);
TECHNIQUE(BasicEffect_Texture_VertexColor, VSBasicTxVc, PSBasicTx);

TECHNIQUE(BasicEffect_VertexLighting, VSBasicVertexLighting, PSBasicVertexLighting);
TECHNIQUE(BasicEffect_VertexLighting_VertexColor, VSBasicVertexLightingVc, PSBasicVertexLighting);
TECHNIQUE(BasicEffect_VertexLighting_Texture, VSBasicVertexLightingTx, PSBasicVertexLightingTx);
TECHNIQUE(BasicEffect_VertexLighting_Texture_VertexColor, VSBasicVertexLightingTxVc, PSBasicVertexLightingTx);

TECHNIQUE(BasicEffect_OneLight, VSBasicOneLight, PSBasicVertexLighting);
TECHNIQUE(BasicEffect_OneLight_VertexColor, VSBasicOneLightVc, PSBasicVertexLighting);
TECHNIQUE(BasicEffect_OneLight_Texture, VSBasicOneLightTx, PSBasicVertexLightingTx);
TECHNIQUE(BasicEffect_OneLight_Texture_VertexColor, VSBasicOneLightTxVc, PSBasicVertexLightingTx);

TECHNIQUE(BasicEffect_PixelLighting, VSBasicPixelLighting, PSBasicPixelLighting);
TECHNIQUE(BasicEffect_PixelLighting_VertexColor, VSBasicPixelLightingVc, PSBasicPixelLighting);
TECHNIQUE(BasicEffect_PixelLighting_Texture, VSBasicPixelLightingTx, PSBasicPixelLightingTx);
TECHNIQUE(BasicEffect_PixelLighting_Texture_VertexColor, VSBasicPixelLightingTxVc, PSBasicPixelLightingTx);

TECHNIQUE(BasicEffect_PixelLighting_OneLight, VSBasicPixelLighting, PSBasicPixelLightingOneLight);
TECHNIQUE(BasicEffect_PixelLighting_OneLight_VertexColor, VSBasicPixelLightingVc, PSBasicPixelLightingOneLight);
TECHNIQUE(BasicEffect_PixelLighting_OneLight_Texture, VSBasicPixelLightingTx, PSBasicPixelLightingTxOneLight);
TECHNIQUE(BasicEffect_PixelLighting_OneLight_Texture_VertexColor, VSBasicPixelLightingTxVc, PSBasicPixelLightingTxOneLight);

TECHNIQUE(BasicEffect_PixelLighting_Texture_NormalMap, VSBasicPixelLightingTxTan, PSBasicPixelLightingTxNorm);
