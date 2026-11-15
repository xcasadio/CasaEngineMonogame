#if OPENGL
#define DEBUG_VS_SHADERMODEL vs_3_0
#define DEBUG_PS_SHADERMODEL ps_3_0
#else
#define DEBUG_VS_SHADERMODEL vs_4_0
#define DEBUG_PS_SHADERMODEL ps_4_0
#endif

// Tonemapping of the high-dynamic-range scene target (ADR-0102). It is a pixel-shader-only effect drawn through
// SpriteBatch.Begin(effect:): SpriteBatch keeps its own vertex shader and binds the scene target as texture 0, so the
// technique declares no vertex shader and the pixel input mirrors the SpriteBatch vertex output.
//
// Per texel: the scene colour is multiplied by Exposure, mapped to [0, 1] by the operator of the technique, then encoded
// for display with pow(c, 1 / 2.2) (the engine's colour encoding is the 2.2 power law, not the exact sRGB curve). The
// alpha of the output is 1.

// Scalar exposure applied before the operator.
float Exposure = 1.0f;

// The HDR scene target. SpriteBatch binds the texture it draws to sampler 0; the parameter is declared so the sampler has
// a name. The sampler is point-clamped: the target is drawn 1:1 into a rectangle of its own size.
Texture2D SceneTexture;
sampler2D SceneTextureSampler = sampler_state
{
    Texture = <SceneTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct PS_INPUT
{
    float4 position : SV_POSITION;
    float4 color : COLOR0;
    float2 textureCoordinates : TEXCOORD0;
};

float3 EncodeForDisplay(float3 linearColor)
{
    return pow(saturate(linearColor), 1.0f / 2.2f);
}

float4 PS_Reinhard(PS_INPUT input) : COLOR0
{
    float3 c = max(tex2D(SceneTextureSampler, input.textureCoordinates).rgb * Exposure, 0.0f);
    c = c / (1.0f + c);
    return float4(EncodeForDisplay(c), 1.0f);
}

float4 PS_AcesApprox(PS_INPUT input) : COLOR0
{
    float3 c = max(tex2D(SceneTextureSampler, input.textureCoordinates).rgb * Exposure, 0.0f);
    c = (c * (2.51f * c + 0.03f)) / (c * (2.43f * c + 0.59f) + 0.14f);
    return float4(EncodeForDisplay(c), 1.0f);
}

technique Tonemap_Reinhard
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_Reinhard();
    }
}

technique Tonemap_AcesApprox
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_AcesApprox();
    }
}
