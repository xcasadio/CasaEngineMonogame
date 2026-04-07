#include "Macros.fxh"

float4x4 InverseViewProjection;
float3 EyePosition;

DECLARE_CUBEMAP(EnvironmentCubeTexture, 0);

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
    return float4(SAMPLE_CUBEMAP(EnvironmentCubeTexture, input.Direction).rgb, 1.0f);
}

TECHNIQUE(SkyCubemap, VS_Main, PS_Main)