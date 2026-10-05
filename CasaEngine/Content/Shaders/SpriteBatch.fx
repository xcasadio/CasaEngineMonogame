#if OPENGL
#define DEBUG_VS_SHADERMODEL vs_3_0
#define DEBUG_PS_SHADERMODEL ps_3_0
#else
#define DEBUG_VS_SHADERMODEL vs_4_0
#define DEBUG_PS_SHADERMODEL ps_4_0
#endif

float4x4 ViewProj;
float4x4 World;
float4 Color;

// Raw-alpha window (min, max] of the texels this draw keeps (ADR-0051). A texel whose own alpha, before the Color
// product, is outside the window is discarded. The neutral window (-1, 2) keeps everything, so a draw that does not set
// the window behaves exactly as before it existed. The test reads the sampled alpha as it is stored: it assumes the point
// sampling the sprite renderer sets (a filtered sample would blend the alpha of neighbours).
float2 AlphaWindow = float2(-1.0f, 2.0f);
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
    float4 sampled = tex2D(TextureSampler, input.textureCoordinates);
    if (sampled.a <= AlphaWindow.x || sampled.a > AlphaWindow.y)
    {
        discard;
    }

    float4 texel = sampled * Color;
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
        VertexShader = compile DEBUG_VS_SHADERMODEL VS();
        PixelShader = compile DEBUG_PS_SHADERMODEL PS();
    }
}
