float4x4 ViewProj;
float4x4 World;
float4 Color;
Texture2D Texture;
sampler2D TextureSampler = sampler_state
{
    Texture = <Texture>;
};

struct VS_INPUT
{
    float3 position : POSITION;
    float2 textureCoordinates: TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 position : POSITION;
    float2 textureCoordinates : TEXCOORD0;
};



VS_OUTPUT VS(VS_INPUT vertex)
{
    VS_OUTPUT Out = (VS_OUTPUT) 0;
    Out.position = mul(float4(vertex.position, 1.0f), mul(World, ViewProj));
    Out.textureCoordinates = vertex.textureCoordinates;
    return Out;
}

float4 PS(VS_OUTPUT input) : COLOR
{
    float4 texel = tex2D(TextureSampler, input.textureCoordinates) * Color;
    if (texel.a <= 0.01f)
    {
	    discard;
    }

    return texel;
}

technique Simple
{
    pass
    {
        VertexShader = compile vs_4_0 VS();
        PixelShader = compile ps_4_0 PS();
    }
}
