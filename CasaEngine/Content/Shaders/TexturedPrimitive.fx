//-----------------------------------------------------------------------------
// TexturedPrimitive.fx
//
// Unlit 2D screen-space effect for textured, vertex-coloured triangle lists
// (VertexPositionColorTexture). Used by the CasaEngine MGUI backend to honour
// IUIDrawContext.DrawTexturedTriangleList, which maps textured paints onto
// rounded box geometry.
//
//   - Transforms vertices with a single pre-combined WorldViewProj matrix
//   - Samples Texture with the CALLER'S GraphicsDevice.SamplerStates[0]: the
//     sampler is declared through DECLARE_TEXTURE and carries no filter or
//     address entry on purpose, so the caller stays free to select a Wrap
//     sampler to tile the texture, or a Clamp one to stretch it. Adding any
//     entry to that declaration would bake a SamplerState into the effect and
//     silently override the caller's choice.
//   - Output = texel * vertex colour mask. No lighting and no alpha test, so a
//     fully transparent texel still writes a transparent fragment and blends as
//     the caller's BlendState dictates, rather than being discarded.
//
// Techniques:
//   TexturedPrimitive  — textured + vertex colour
//-----------------------------------------------------------------------------

#include "Macros.fxh"

// --- Parameters ---

float4x4 WorldViewProj;

// 1 when the view renders in the linear colour pipeline (ADR-0103): the texture and the vertex colour are display colours.
// Only a renderer of the scene (the particle renderer, on its own clone of this effect) binds it; the UI backend never does,
// so the UI keeps its display colours.
float ColorPipelineIsLinear = 0.0f;

DECLARE_TEXTURE(Texture, 0);

#include "ColorPipeline.fxh"

// --- VS ---

struct VS_INPUT
{
    float3 Position : POSITION;
    float4 Color    : COLOR;
    float2 TexCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR;
    float2 TexCoord : TEXCOORD0;
};

VS_OUTPUT VS_Main(VS_INPUT input)
{
    VS_OUTPUT output;
    output.Position = mul(float4(input.Position, 1.0f), WorldViewProj);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    return output;
}

// --- PS ---

float4 PS_Main(VS_OUTPUT input) : SV_TARGET
{
    return DecodeColor4(SAMPLE_TEXTURE(Texture, input.TexCoord) * input.Color);
}

// --- Techniques ---

TECHNIQUE(TexturedPrimitive, VS_Main, PS_Main)
