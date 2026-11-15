#include "Macros.fxh"

float4x4 InverseViewProjection;
float3 EyePosition;

// 1 when the view renders in the linear colour pipeline (ADR-0097); EnvironmentCubeIsLinear is 1 for a cube built from a .hdr
// panorama, which already holds linear light and is not decoded.
float ColorPipelineIsLinear = 0.0f;
float EnvironmentCubeIsLinear = 0.0f;

DECLARE_CUBEMAP(EnvironmentCubeTexture, 0);

#include "ColorPipeline.fxh"

struct VS_INPUT
{
    float3 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 Position : SV_POSITION;
    float3 Direction : TEXCOORD0;
};

VS_OUTPUT VS_Main(VS_INPUT input)
{
    VS_OUTPUT output;

    float4 clipPosition = float4(input.Position.xy, 1.0f, 1.0f);
    float4 worldPosition = mul(clipPosition, InverseViewProjection);
    float3 worldDirection = normalize(worldPosition.xyz / worldPosition.w - EyePosition);

    output.Position = float4(input.Position.xy, 0.9999f, 1.0f);
    output.Direction = worldDirection;
    return output;
}

float4 PS_Main(VS_OUTPUT input) : SV_TARGET
{
    float3 color = SAMPLE_CUBEMAP(EnvironmentCubeTexture, input.Direction).rgb;
    if (EnvironmentCubeIsLinear < 0.5f)
    {
        color = DecodeColor(color);
    }

    return float4(color, 1.0f);
}

TECHNIQUE(SkyCubemap, VS_Main, PS_Main)