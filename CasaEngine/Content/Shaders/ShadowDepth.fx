//-----------------------------------------------------------------------------
// ShadowDepth.fx
//
// Minimal shadow-map shader for static meshes.
// Writes projected depth into color so the texture can be sampled later by forward lighting.
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
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float Depth     : TEXCOORD1;
};

float EncodeShadowDepth(float4 clipPosition)
{
    float depth = clipPosition.z / clipPosition.w;

#if OPENGL
    depth = depth * 0.5f + 0.5f;
#endif

    return saturate(depth);
}

void ApplyAlphaTest(float alpha)
{
    if (AlphaCutoff > 0.0f)
    {
        clip(alpha - AlphaCutoff);
    }
}

VS_OUTPUT VS_Main(VS_INPUT input)
{
    VS_OUTPUT output;
    float4 clipPosition = mul(float4(input.Position, 1.0f), WorldViewProj);
    output.Position = clipPosition;
    output.TexCoord = input.TexCoord;
    output.Depth = EncodeShadowDepth(clipPosition);
    return output;
}

float4 PS_Solid(VS_OUTPUT input) : SV_TARGET
{
    return float4(input.Depth, input.Depth, input.Depth, 1.0f);
}

float4 PS_Textured(VS_OUTPUT input) : SV_TARGET
{
    float alpha = SAMPLE_TEXTURE(Texture, input.TexCoord).a;
    ApplyAlphaTest(alpha);
    return float4(input.Depth, input.Depth, input.Depth, 1.0f);
}

TECHNIQUE(ShadowDepth_Solid, VS_Main, PS_Solid)
TECHNIQUE(ShadowDepth_Textured, VS_Main, PS_Textured)