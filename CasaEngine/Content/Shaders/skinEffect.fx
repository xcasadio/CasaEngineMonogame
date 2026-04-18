//_______________________________________________________________
// skinEffect.fx
// Skinned (rigged) model shader with skeletal animation.
//
// Techniques: RiggedModelDraw, RiggedModelDrawDualQuaternion, RiggedModelNormalDraw, SkinedDebugModelDraw
//
// Uses the same directional light model as LitForward.fx
// (up to 8 directional lights via Lighting.fxh).
//_______________________________________________________________

#include "Macros.fxh"


DECLARE_TEXTURE(Texture, 0);


//_______________________________________________________________
// Constant buffer — matches LitForward.fx parameter layout
// so that LightingContext.Bind() works identically.
//_______________________________________________________________

BEGIN_CONSTANTS

    float4 DiffuseColor _vs(c0) _ps(c1) _cb(c0);
    float3 EmissiveColor _vs(c1) _ps(c2) _cb(c1);
    float3 SpecularColor _vs(c2) _ps(c3) _cb(c2);
    float  SpecularPower _vs(c3) _ps(c4) _cb(c2.w);

    float3 DirLight0Direction  _vs(c4) _ps(c5)  _cb(c3);
    float3 DirLight0DiffuseColor  _vs(c5) _ps(c6)  _cb(c4);
    float3 DirLight0SpecularColor _vs(c6) _ps(c7)  _cb(c5);

    float3 DirLight1Direction  _vs(c7) _ps(c8)  _cb(c6);
    float3 DirLight1DiffuseColor  _vs(c8) _ps(c9)  _cb(c7);
    float3 DirLight1SpecularColor _vs(c9) _ps(c10) _cb(c8);

    float3 DirLight2Direction  _vs(c10) _ps(c11) _cb(c9);
    float3 DirLight2DiffuseColor  _vs(c11) _ps(c12) _cb(c10);
    float3 DirLight2SpecularColor _vs(c12) _ps(c13) _cb(c11);

    float3 DirLight3Direction  _vs(c13) _ps(c14) _cb(c12);
    float3 DirLight3DiffuseColor  _vs(c14) _ps(c15) _cb(c13);
    float3 DirLight3SpecularColor _vs(c15) _ps(c16) _cb(c14);

    float3 DirLight4Direction  _vs(c16) _ps(c17) _cb(c15);
    float3 DirLight4DiffuseColor  _vs(c17) _ps(c18) _cb(c16);
    float3 DirLight4SpecularColor _vs(c18) _ps(c19) _cb(c17);

    float3 DirLight5Direction  _vs(c19) _ps(c20) _cb(c18);
    float3 DirLight5DiffuseColor  _vs(c20) _ps(c21) _cb(c19);
    float3 DirLight5SpecularColor _vs(c21) _ps(c22) _cb(c20);

    float3 DirLight6Direction  _vs(c22) _ps(c23) _cb(c21);
    float3 DirLight6DiffuseColor  _vs(c23) _ps(c24) _cb(c22);
    float3 DirLight6SpecularColor _vs(c24) _ps(c25) _cb(c23);

    float3 DirLight7Direction  _vs(c25) _ps(c26) _cb(c24);
    float3 DirLight7DiffuseColor  _vs(c26) _ps(c27) _cb(c25);
    float3 DirLight7SpecularColor _vs(c27) _ps(c28) _cb(c26);

    float ActiveDirectionalLightCount _ps(c29) _cb(c27.x);

    float3 EyePosition _vs(c28) _ps(c30) _cb(c28);
    float3 AmbientColor _cb(c29);

    float4x4 World       _vs(c30) _cb(c30);
    float3x3 WorldInverseTranspose _vs(c34) _cb(c34);

MATRIX_CONSTANTS

    float4x4 WorldViewProj _vs(c30) _cb(c0);

    // Skinning-specific parameters — MUST be inside cbuffer for MGFX compatibility.
    // Standalone (global-scope) matrix params are not handled correctly by MGFX.
    float4x4 Bones[128];
    float4 BonesDualQuaternion[256];
    float boneIdToSee;

END_CONSTANTS


#include "Lighting.fxh"


//_______________________________________________________________
// Structs
//_______________________________________________________________

struct VsInputSkinnedQuad
{
    float3 Position : POSITION0;
    float2 TexureCoordinateA : TEXCOORD0;
    float3 Normal : NORMAL0;
    float4 BlendIndices : BLENDINDICES0;
    float4 BlendWeights : BLENDWEIGHT0;
};

struct VsOutputSkinnedQuad
{
    float4 Position : SV_Position;
    float4 Color : Color0;
    float2 TexureCoordinateA : TEXCOORD0;
    float3 Position3D : TEXCOORD1;
    float3 Normal3D : TEXCOORD2;
};


//_______________________________________________________________
// Shared skinning helper: applies bone transforms to position & normal
//_______________________________________________________________

void ApplyBoneTransforms(VsInputSkinnedQuad input, out float4 pos, out float3 norm)
{
    pos = float4(input.Position, 1);
    norm = input.Normal;

    float sum = input.BlendWeights.x + input.BlendWeights.y + input.BlendWeights.z + input.BlendWeights.w;
    float4x4 mbones =
        Bones[input.BlendIndices.x] * (float)input.BlendWeights.x / sum +
        Bones[input.BlendIndices.y] * (float)input.BlendWeights.y / sum +
        Bones[input.BlendIndices.z] * (float)input.BlendWeights.z / sum +
        Bones[input.BlendIndices.w] * (float)input.BlendWeights.w / sum;

    // Apply bone transform only (no World) — caller applies World or WorldViewProj
    pos  = mul(pos, mbones);
    norm = mul(norm, (float3x3)mbones);
}

float4 QuaternionConjugate(float4 q)
{
    return float4(-q.xyz, q.w);
}

float4 QuaternionMultiply(float4 left, float4 right)
{
    return float4(
        left.w * right.xyz + right.w * left.xyz + cross(left.xyz, right.xyz),
        left.w * right.w - dot(left.xyz, right.xyz));
}

float3 RotateByQuaternion(float3 v, float4 q)
{
    float3 doubledCross = 2.0f * cross(q.xyz, v);
    return v + q.w * doubledCross + cross(q.xyz, doubledCross);
}

float3 ExtractDualQuaternionTranslation(float4 realQuaternion, float4 dualQuaternion)
{
    float4 translationQuaternion = QuaternionMultiply(dualQuaternion * 2.0f, QuaternionConjugate(realQuaternion));
    return translationQuaternion.xyz;
}

void ReadBoneDualQuaternion(uint boneIndex, out float4 realQuaternion, out float4 dualQuaternion)
{
    uint paletteIndex = boneIndex * 2u;
    realQuaternion = BonesDualQuaternion[paletteIndex];
    dualQuaternion = BonesDualQuaternion[paletteIndex + 1u];
}

void AccumulateBoneDualQuaternion(uint boneIndex, float weight, float4 referenceReal, inout float4 accumulatedReal, inout float4 accumulatedDual)
{
    float4 boneReal;
    float4 boneDual;
    ReadBoneDualQuaternion(boneIndex, boneReal, boneDual);

    if (dot(boneReal, referenceReal) < 0.0f)
    {
        boneReal = -boneReal;
        boneDual = -boneDual;
    }

    accumulatedReal += boneReal * weight;
    accumulatedDual += boneDual * weight;
}

void ApplyBoneDualQuaternionTransforms(VsInputSkinnedQuad input, out float4 pos, out float3 norm)
{
    pos = float4(input.Position, 1.0f);
    norm = input.Normal;

    float sum = input.BlendWeights.x + input.BlendWeights.y + input.BlendWeights.z + input.BlendWeights.w;
    float inverseSum = sum > 0.0f ? rcp(sum) : 1.0f;

    float4 referenceReal;
    float4 referenceDual;
    ReadBoneDualQuaternion((uint)input.BlendIndices.x, referenceReal, referenceDual);

    float4 accumulatedReal = float4(0.0f, 0.0f, 0.0f, 0.0f);
    float4 accumulatedDual = float4(0.0f, 0.0f, 0.0f, 0.0f);
    AccumulateBoneDualQuaternion((uint)input.BlendIndices.x, input.BlendWeights.x * inverseSum, referenceReal, accumulatedReal, accumulatedDual);
    AccumulateBoneDualQuaternion((uint)input.BlendIndices.y, input.BlendWeights.y * inverseSum, referenceReal, accumulatedReal, accumulatedDual);
    AccumulateBoneDualQuaternion((uint)input.BlendIndices.z, input.BlendWeights.z * inverseSum, referenceReal, accumulatedReal, accumulatedDual);
    AccumulateBoneDualQuaternion((uint)input.BlendIndices.w, input.BlendWeights.w * inverseSum, referenceReal, accumulatedReal, accumulatedDual);

    float quaternionLength = max(length(accumulatedReal), 1e-5f);
    accumulatedReal /= quaternionLength;
    accumulatedDual /= quaternionLength;

    pos.xyz = RotateByQuaternion(pos.xyz, accumulatedReal) + ExtractDualQuaternionTranslation(accumulatedReal, accumulatedDual);
    norm = normalize(RotateByQuaternion(norm, accumulatedReal));
}


//_______________________________________________________________
// RiggedModelDraw — skinned mesh with per-pixel lighting
//_______________________________________________________________

VsOutputSkinnedQuad VertexShaderRiggedModelDraw(VsInputSkinnedQuad input)
{
    VsOutputSkinnedQuad output;

    float4 pos;
    float3 norm;
    ApplyBoneTransforms(input, pos, norm);

    output.Color = float4(1, 1, 1, 1);
    output.TexureCoordinateA = input.TexureCoordinateA;
    // World-space position and normal for lighting
    float4 worldPos = mul(pos, World);
    output.Position3D = worldPos.xyz;
    output.Normal3D = normalize(mul(norm, WorldInverseTranspose));
    // Clip-space: bone-space pos * WorldViewProj (cbuffer, pre-computed on C# side)
    output.Position = mul(pos, WorldViewProj);
    return output;
}

float4 PixelShaderRiggedModelDraw(VsOutputSkinnedQuad input) : COLOR0
{
    float4 texelColor = SAMPLE_TEXTURE(Texture, input.TexureCoordinateA) * input.Color;

    float3 eyeVector  = normalize(EyePosition - input.Position3D);
    float3 worldNormal = normalize(input.Normal3D);

    ColorPair lightResult = ComputeLights(eyeVector, worldNormal, (int)ActiveDirectionalLightCount);

    float4 color = texelColor;
    color.rgb *= lightResult.Diffuse;
    AddSpecular(color, lightResult.Specular);

    return color;
}

TECHNIQUE(RiggedModelDraw, VertexShaderRiggedModelDraw, PixelShaderRiggedModelDraw)


VsOutputSkinnedQuad VertexShaderRiggedModelDrawDualQuaternion(VsInputSkinnedQuad input)
{
    VsOutputSkinnedQuad output;

    float4 pos;
    float3 norm;
    ApplyBoneDualQuaternionTransforms(input, pos, norm);

    output.Color = float4(1, 1, 1, 1);
    output.TexureCoordinateA = input.TexureCoordinateA;
    float4 worldPos = mul(pos, World);
    output.Position3D = worldPos.xyz;
    output.Normal3D = normalize(mul(norm, WorldInverseTranspose));
    output.Position = mul(pos, WorldViewProj);
    return output;
}

TECHNIQUE(RiggedModelDrawDualQuaternion, VertexShaderRiggedModelDrawDualQuaternion, PixelShaderRiggedModelDraw)


//_______________________________________________________________
// RiggedModelNormalDraw — debug: visualizes normals with lighting
//_______________________________________________________________

float4 PixelShaderRiggedModelNormalDraw(VsOutputSkinnedQuad input) : COLOR0
{
    float3 N = normalize(input.Normal3D);
    float3 eyeVector = normalize(EyePosition - input.Position3D);

    ColorPair lightResult = ComputeLights(eyeVector, N, (int)ActiveDirectionalLightCount);

    float4 texelColor = SAMPLE_TEXTURE(Texture, input.TexureCoordinateA) * input.Color;
    float4 lightColor = float4(lightResult.Diffuse, 1.0f);
    float4 result = (lightColor * 0.60f + texelColor * 0.40f);
    return result;
}

TECHNIQUE(RiggedModelNormalDraw, VertexShaderRiggedModelDraw, PixelShaderRiggedModelNormalDraw)


//_______________________________________________________________
// SkinedDebugModelDraw — highlights vertices belonging to a selected bone
//_______________________________________________________________

VsOutputSkinnedQuad VertexShaderDebugSkinnedDraw(VsInputSkinnedQuad input)
{
    VsOutputSkinnedQuad output;

    float4 pos;
    float3 norm;
    ApplyBoneTransforms(input, pos, norm);

    float4 col = float4(0.40f, 0.40f, 0.40f, 1.0f);
    if (input.BlendIndices.x == boneIdToSee || input.BlendIndices.y == boneIdToSee ||
        input.BlendIndices.z == boneIdToSee || input.BlendIndices.w == boneIdToSee)
        col = float4(0.49f, 0.99f, 0.49f, 1.0f);

    output.Color = col;
    output.TexureCoordinateA = input.TexureCoordinateA;
    float4 worldPos = mul(pos, World);
    output.Position3D = worldPos.xyz;
    output.Normal3D = normalize(mul(norm, WorldInverseTranspose));
    output.Position = mul(pos, WorldViewProj);
    return output;
}

float4 PixelShaderDebugSkinnedDraw(VsOutputSkinnedQuad input) : COLOR0
{
    float4 result = SAMPLE_TEXTURE(Texture, input.TexureCoordinateA) * input.Color;
    return result;
}

TECHNIQUE(SkinedDebugModelDraw, VertexShaderDebugSkinnedDraw, PixelShaderDebugSkinnedDraw)


