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
        VertexShader = compile vs_4_0 VS();
        PixelShader = compile ps_4_0 PS();
    }
}