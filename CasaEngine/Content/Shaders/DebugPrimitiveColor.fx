#if OPENGL
#define DEBUG_VS_SHADERMODEL vs_3_0
#define DEBUG_PS_SHADERMODEL ps_3_0
#else
#define DEBUG_VS_SHADERMODEL vs_4_0
#define DEBUG_PS_SHADERMODEL ps_4_0
#endif

float4x4 WorldViewProj;
float4 ColorMultiplier = float4(1.0f, 1.0f, 1.0f, 1.0f);

struct VS_INPUT
{
    float3 position : POSITION;
    float4 color : COLOR;
};

struct VS_OUTPUT
{
    float4 position : POSITION;
    float4 color : COLOR;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output = (VS_OUTPUT)0;
    output.position = mul(float4(input.position, 1.0f), WorldViewProj);
    output.color = input.color * ColorMultiplier;
    return output;
}

float4 PS(VS_OUTPUT input) : COLOR
{
    return input.color;
}

technique Simple
{
    pass
    {
        VertexShader = compile DEBUG_VS_SHADERMODEL VS();
        PixelShader = compile DEBUG_PS_SHADERMODEL PS();
    }
}