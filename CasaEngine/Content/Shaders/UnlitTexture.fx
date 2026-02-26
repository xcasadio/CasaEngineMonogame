//-----------------------------------------------------------------------------
// UnlitTexture.fx
//
// A minimal unlit material shader:
//   - Transforms vertices using WorldViewProj
//   - Samples an Albedo texture (optional)
//   - Multiplies by TintColor (RGBA) and Alpha
//
// Techniques:
//   Unlit_Textured  — with texture sampling
//   Unlit_Colored   — solid tint color only (no texture)
//-----------------------------------------------------------------------------

#include "Macros.fxh"

// --- Parameters ---

float4x4 WorldViewProj;
float4x4 World;
float4   TintColor = float4(1, 1, 1, 1);
float    Alpha     = 1.0f;

DECLARE_TEXTURE(Texture, 0);

// --- VS ---

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
};

VS_OUTPUT VS_Main(VS_INPUT input)
{
    VS_OUTPUT output;
    output.Position = mul(float4(input.Position, 1.0f), WorldViewProj);
    output.TexCoord = input.TexCoord;
    return output;
}

// --- PS ---

float4 PS_Textured(VS_OUTPUT input) : SV_TARGET
{
    float4 color = SAMPLE_TEXTURE(Texture, input.TexCoord) * TintColor;
    color.a *= Alpha;
    return color;
}

float4 PS_Colored(VS_OUTPUT input) : SV_TARGET
{
    return float4(TintColor.rgb, TintColor.a * Alpha);
}

// --- Techniques ---

TECHNIQUE(Unlit_Textured, VS_Main, PS_Textured)
TECHNIQUE(Unlit_Colored,  VS_Main, PS_Colored)
