float4x4 WorldViewProj;

struct VS_INPUT
{
    float3 position : POSITION;
    float4 color: COLOR;
};

struct VS_OUTPUT
{
    float4 position : POSITION;
    float4 color : NORMAL;
};



VS_OUTPUT VS(VS_INPUT vertex)
{
    VS_OUTPUT Out = (VS_OUTPUT) 0;
    Out.position = mul(float4(vertex.position, 1.0f), WorldViewProj);
    Out.color = vertex.color;
    return Out;
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
