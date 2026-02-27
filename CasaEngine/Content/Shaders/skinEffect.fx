//_______________________________________________________________
// skinEffect.fx
// Skinned (rigged) model shader with skeletal animation.
//
// Techniques: RiggedModelDraw, RiggedModelNormalDraw, SkinedDebugModelDraw
//
// Uses the same directional light model as basicEffect.fx
// (3 directional lights via Lighting.fxh).
//_______________________________________________________________

#include "Macros.fxh"


DECLARE_TEXTURE(TextureA, 0);


//_______________________________________________________________
// Constant buffer — matches basicEffect.fx parameter layout
// so that LightingContext.Bind() works identically.
//_______________________________________________________________

BEGIN_CONSTANTS

    float4 DiffuseColor _vs(c0) _ps(c1) _cb(c0);
    float3 EmissiveColor _vs(c1) _ps(c2) _cb(c1);
    float3 SpecularColor _vs(c2) _ps(c3) _cb(c2);
    float  SpecularPower _vs(c3) _ps(c4) _cb(c2.w);

    float3 DirLightDirections[3]    _vs(c4) _ps(c5)  _cb(c3);
    float3 DirLightDiffuseColors[3] _vs(c7) _ps(c8)  _cb(c6);
    float3 DirLightSpecularColors[3] _vs(c10) _ps(c11) _cb(c9);

    float3 EyePosition _vs(c13) _ps(c14) _cb(c12);
    float3 AmbientColor _cb(c13);

    float4x4 World       _vs(c19) _cb(c15);
    float3x3 WorldInverseTranspose _vs(c23) _cb(c19);

MATRIX_CONSTANTS

    float4x4 WorldViewProj _vs(c15) _cb(c0);

END_CONSTANTS

// Skinning-specific parameters (outside the shared cbuffer)
float4x4 View;
float4x4 Projection;
float4x4 Bones[128];
float boneIdToSee = -1.0f;


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

    pos  = mul(pos, mbones);
    norm = mul(norm, mbones);

    pos  = mul(pos, World);
    norm = normalize(mul(norm, WorldInverseTranspose));
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
    output.Position3D = pos.xyz;
    output.Normal3D = norm;
    float4x4 vp = mul(View, Projection);
    output.Position = mul(pos, vp);
    return output;
}

float4 PixelShaderRiggedModelDraw(VsOutputSkinnedQuad input) : COLOR0
{
    float4 texelColor = SAMPLE_TEXTURE(TextureA, input.TexureCoordinateA) * input.Color;

    float3 eyeVector  = normalize(EyePosition - input.Position3D);
    float3 worldNormal = normalize(input.Normal3D);

    ColorPair lightResult = ComputeLights(eyeVector, worldNormal, 3);

    float4 color = texelColor;
    color.rgb *= lightResult.Diffuse;
    AddSpecular(color, lightResult.Specular);

    return color;
}

TECHNIQUE(RiggedModelDraw, VertexShaderRiggedModelDraw, PixelShaderRiggedModelDraw)


//_______________________________________________________________
// RiggedModelNormalDraw — debug: visualizes normals with lighting
//_______________________________________________________________

float4 PixelShaderRiggedModelNormalDraw(VsOutputSkinnedQuad input) : COLOR0
{
    float3 N = normalize(input.Normal3D);
    float3 eyeVector = normalize(EyePosition - input.Position3D);

    ColorPair lightResult = ComputeLights(eyeVector, N, 3);

    float4 texelColor = SAMPLE_TEXTURE(TextureA, input.TexureCoordinateA) * input.Color;
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
    output.Position3D = pos.xyz;
    output.Normal3D = norm;
    float4x4 vp = mul(View, Projection);
    output.Position = mul(pos, vp);
    return output;
}

float4 PixelShaderDebugSkinnedDraw(VsOutputSkinnedQuad input) : COLOR0
{
    float4 result = SAMPLE_TEXTURE(TextureA, input.TexureCoordinateA) * input.Color;
    return result;
}

TECHNIQUE(SkinedDebugModelDraw, VertexShaderDebugSkinnedDraw, PixelShaderDebugSkinnedDraw)


