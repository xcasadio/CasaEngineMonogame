float4x4 matWorldViewProj;
float4 color;

struct VS_OUTPUT
{
    float4 Pos : POSITION;
};

VS_OUTPUT VS(float4 Pos : POSITION)
{
    VS_OUTPUT Out = (VS_OUTPUT)0;
    Out.Pos = mul(Pos, matWorldViewProj);
    return Out;
}

float4 PS() : COLOR
{
    return color;
}

technique Simple { pass { VertexShader = compile vs_4_0 VS(); PixelShader = compile ps_4_0 PS(); } }
