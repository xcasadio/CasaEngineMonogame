float4x4 WorldViewProj;
Texture2D Texture;
sampler2D TextureSampler = sampler_state
{
    Texture = <Texture>;
};

struct VS_INPUT
{
    float3 position : POSITION;
    float3 Normal: NORMAL;
    float2 uv: TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 Pos : POSITION;
    float3 Normal : NORMAL;
    float2 TextureCoordinates : TEXCOORD0;
};



VS_OUTPUT VS(VS_INPUT vertex)
{
    VS_OUTPUT Out = (VS_OUTPUT) 0;
    Out.Pos = mul(float4(vertex.position, 1.0f), WorldViewProj);
    Out.Normal = vertex.Normal;
    Out.TextureCoordinates = vertex.uv;
    return Out;
}

float4 PS(VS_OUTPUT input) : COLOR
{
    return tex2D(TextureSampler, input.TextureCoordinates);
}

technique Simple
{
    pass
    {
        VertexShader = compile vs_4_0 VS();
        PixelShader = compile ps_4_0 PS();
    }
}
