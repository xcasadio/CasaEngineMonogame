//-----------------------------------------------------------------------------
// SceneDepth.fx
//
// Scene depth pre-pass of the static meshes (ADR-0115). Writes the VIEW DEPTH of every opaque and alpha-tested pixel, in world
// units, into the red channel of a floating-point target that screen-space effects read later:
//
//   depth = clip-space w of the vertex, which for a perspective projection is the distance of the point to the camera plane,
//           measured along the camera axis (not the length of the ray to the eye).
//
// The value is not normalised: no near or far plane is needed to read it back. The target is cleared by the host to the background
// constant (SceneDepthPrepass.BackgroundDepth), so a pixel no geometry covers reads that constant.
//
// SceneDepth_Solid draws a mesh as is; SceneDepth_Textured also discards the pixels whose texture alpha is under AlphaCutoff. The
// cut-out uses the alpha of the texture alone, the same approximation as ShadowDepth.fx (the colour pass cuts on the product of the
// material alpha factor and the texture alpha). The skinned meshes have their own techniques in skinEffect.fx.
//-----------------------------------------------------------------------------

#include "Macros.fxh"

float4x4 WorldViewProj;
float AlphaCutoff = 0.0f;

DECLARE_TEXTURE(Texture, 0);

struct VS_INPUT
{
    float3 Position : POSITION;
    float3 Normal   : NORMAL;
    float2 TexCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 Position  : SV_POSITION;
    float2 TexCoord  : TEXCOORD0;
    float  ViewDepth : TEXCOORD1;
};

VS_OUTPUT VS_Main(VS_INPUT input)
{
    VS_OUTPUT output;
    float4 clipPosition = mul(float4(input.Position, 1.0f), WorldViewProj);
    output.Position = clipPosition;
    output.TexCoord = input.TexCoord;
    output.ViewDepth = clipPosition.w;
    return output;
}

float4 PS_Solid(VS_OUTPUT input) : SV_TARGET
{
    return float4(input.ViewDepth, 0.0f, 0.0f, 1.0f);
}

float4 PS_Textured(VS_OUTPUT input) : SV_TARGET
{
    float alpha = SAMPLE_TEXTURE(Texture, input.TexCoord).a;
    if (AlphaCutoff > 0.0f)
    {
        clip(alpha - AlphaCutoff);
    }

    return float4(input.ViewDepth, 0.0f, 0.0f, 1.0f);
}

TECHNIQUE(SceneDepth_Solid, VS_Main, PS_Solid)
TECHNIQUE(SceneDepth_Textured, VS_Main, PS_Textured)
