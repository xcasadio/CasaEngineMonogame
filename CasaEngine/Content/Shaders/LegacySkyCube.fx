#include "Macros.fxh"

float4x4 ViewProjection;
float4 SkyTintColor = float4(0.91f, 0.91f, 0.91f, 1.0f);
float SkyCubeScale = 100.0f;

DECLARE_CUBEMAP(SkyCube, 0);

struct VS_INPUT
{
    float3 Position : POSITION;
};

struct VS_OUTPUT
{
    float4 Position : SV_POSITION;
    float3 CubeDirection : TEXCOORD0;
};

VS_OUTPUT VS_Main(VS_INPUT input)
{
    VS_OUTPUT output;
    float3 scaledPosition = input.Position * SkyCubeScale;
    output.Position = mul(float4(scaledPosition, 1.0f), ViewProjection);
    // The race world is already converted to CasaEngine's Y-up space.
    // Keep cubemap sampling in runtime world axes instead of replaying the legacy Z-up swap.
    output.CubeDirection = input.Position;
    return output;
}

float4 PS_Main(VS_OUTPUT input) : SV_TARGET
{
    return SkyTintColor * SAMPLE_CUBEMAP(SkyCube, input.CubeDirection);
}

TECHNIQUE(SkyCube, VS_Main, PS_Main)