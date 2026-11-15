#if OPENGL
#define DEBUG_VS_SHADERMODEL vs_3_0
#define DEBUG_PS_SHADERMODEL ps_3_0
#else
#define DEBUG_VS_SHADERMODEL vs_4_0
#define DEBUG_PS_SHADERMODEL ps_4_0
#endif

// Bloom of the high-dynamic-range scene target, applied before the tonemapping (ADR-0099). Like Tonemap.fx it is a
// pixel-shader-only effect drawn through SpriteBatch.Begin(effect:): SpriteBatch keeps its own vertex shader and binds the
// source texture as texture 0, so the techniques declare no vertex shader and the pixel input mirrors the SpriteBatch
// vertex output. Every technique reads linear light and writes linear light (no encoding).
//
//   Bloom_BrightPass     scene (full size) -> half size. Keeps what is above BloomThreshold (soft knee), per texel, then
//                        averages the 2 x 2 texels covered by the destination pixel.
//   Bloom_Downsample     level n -> level n + 1: average of the 2 x 2 source texels.
//   Bloom_BlurHorizontal separable blur, 9 taps along x, spacing BloomTexelSize.x (the CPU passes Radius / width).
//   Bloom_BlurVertical   same along y, spacing BloomTexelSize.y.
//   Bloom_Composite      BloomIntensity times the blurred texture, meant to be drawn with additive blending onto the scene.

// Scene luminance (largest colour component, linear light) above which a texel feeds the bloom. The knee is half of it:
// the contribution starts at BloomThreshold / 2 and is fully linear from 1.5 * BloomThreshold.
float BloomThreshold = 1.0f;

// Scale of the blurred bright part added to the scene by Bloom_Composite.
float BloomIntensity = 0.5f;

// Size of one source texel (1 / width, 1 / height) for the bright pass and the downsample; the blur tap spacing
// (Radius / width, Radius / height) for the two blur techniques.
float2 BloomTexelSize = float2(1.0f, 1.0f);

// The texture drawn by SpriteBatch (sampler 0). Bilinear and clamped: the composite upsamples a small level to the scene size.
Texture2D BloomSourceTexture;
sampler2D BloomSourceSampler = sampler_state
{
    Texture = <BloomSourceTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Gaussian, sigma 2, 9 taps (offsets 0, +-1 ... +-4). BlurWeight0 + 2 * (BlurWeight1 + ... + BlurWeight4) = 1, so the blur
// does not change the energy of the image (checked by a test).
static const float BlurWeight0 = 0.204163689f;
static const float BlurWeight1 = 0.180173823f;
static const float BlurWeight2 = 0.123831537f;
static const float BlurWeight3 = 0.066282245f;
static const float BlurWeight4 = 0.027630551f;

struct PS_INPUT
{
    float4 position : SV_POSITION;
    float4 color : COLOR0;
    float2 textureCoordinates : TEXCOORD0;
};

// Part of a texel kept by the soft threshold: 0 below BloomThreshold / 2, a quadratic ramp up to 1.5 * BloomThreshold, then the
// plain excess over BloomThreshold. Returned as a colour (the texel scaled by the kept fraction).
float3 BrightPart(float3 c)
{
    float brightness = max(c.r, max(c.g, c.b));
    float knee = BloomThreshold * 0.5f;
    float soft = clamp(brightness - BloomThreshold + knee, 0.0f, 2.0f * knee);
    soft = soft * soft / (4.0f * knee + 0.00001f);
    float kept = max(soft, brightness - BloomThreshold);
    return c * (kept / max(brightness, 0.00001f));
}

float4 PS_BrightPass(PS_INPUT input) : COLOR0
{
    float2 uv = input.textureCoordinates;
    float2 h = BloomTexelSize * 0.5f;
    float3 sum = BrightPart(tex2D(BloomSourceSampler, uv + float2(-h.x, -h.y)).rgb);
    sum += BrightPart(tex2D(BloomSourceSampler, uv + float2(h.x, -h.y)).rgb);
    sum += BrightPart(tex2D(BloomSourceSampler, uv + float2(-h.x, h.y)).rgb);
    sum += BrightPart(tex2D(BloomSourceSampler, uv + float2(h.x, h.y)).rgb);
    return float4(sum * 0.25f, 1.0f);
}

float4 PS_Downsample(PS_INPUT input) : COLOR0
{
    float2 uv = input.textureCoordinates;
    float2 h = BloomTexelSize * 0.5f;
    float3 sum = tex2D(BloomSourceSampler, uv + float2(-h.x, -h.y)).rgb;
    sum += tex2D(BloomSourceSampler, uv + float2(h.x, -h.y)).rgb;
    sum += tex2D(BloomSourceSampler, uv + float2(-h.x, h.y)).rgb;
    sum += tex2D(BloomSourceSampler, uv + float2(h.x, h.y)).rgb;
    return float4(sum * 0.25f, 1.0f);
}

float4 PS_BlurHorizontal(PS_INPUT input) : COLOR0
{
    float2 uv = input.textureCoordinates;
    float dx = BloomTexelSize.x;
    float3 sum = tex2D(BloomSourceSampler, uv).rgb * BlurWeight0;
    sum += (tex2D(BloomSourceSampler, uv + float2(dx, 0.0f)).rgb + tex2D(BloomSourceSampler, uv - float2(dx, 0.0f)).rgb) * BlurWeight1;
    sum += (tex2D(BloomSourceSampler, uv + float2(2.0f * dx, 0.0f)).rgb + tex2D(BloomSourceSampler, uv - float2(2.0f * dx, 0.0f)).rgb) * BlurWeight2;
    sum += (tex2D(BloomSourceSampler, uv + float2(3.0f * dx, 0.0f)).rgb + tex2D(BloomSourceSampler, uv - float2(3.0f * dx, 0.0f)).rgb) * BlurWeight3;
    sum += (tex2D(BloomSourceSampler, uv + float2(4.0f * dx, 0.0f)).rgb + tex2D(BloomSourceSampler, uv - float2(4.0f * dx, 0.0f)).rgb) * BlurWeight4;
    return float4(sum, 1.0f);
}

float4 PS_BlurVertical(PS_INPUT input) : COLOR0
{
    float2 uv = input.textureCoordinates;
    float dy = BloomTexelSize.y;
    float3 sum = tex2D(BloomSourceSampler, uv).rgb * BlurWeight0;
    sum += (tex2D(BloomSourceSampler, uv + float2(0.0f, dy)).rgb + tex2D(BloomSourceSampler, uv - float2(0.0f, dy)).rgb) * BlurWeight1;
    sum += (tex2D(BloomSourceSampler, uv + float2(0.0f, 2.0f * dy)).rgb + tex2D(BloomSourceSampler, uv - float2(0.0f, 2.0f * dy)).rgb) * BlurWeight2;
    sum += (tex2D(BloomSourceSampler, uv + float2(0.0f, 3.0f * dy)).rgb + tex2D(BloomSourceSampler, uv - float2(0.0f, 3.0f * dy)).rgb) * BlurWeight3;
    sum += (tex2D(BloomSourceSampler, uv + float2(0.0f, 4.0f * dy)).rgb + tex2D(BloomSourceSampler, uv - float2(0.0f, 4.0f * dy)).rgb) * BlurWeight4;
    return float4(sum, 1.0f);
}

float4 PS_Composite(PS_INPUT input) : COLOR0
{
    float3 bloom = tex2D(BloomSourceSampler, input.textureCoordinates).rgb * BloomIntensity;
    return float4(bloom, 0.0f);
}

technique Bloom_BrightPass
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_BrightPass();
    }
}

technique Bloom_Downsample
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_Downsample();
    }
}

technique Bloom_BlurHorizontal
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_BlurHorizontal();
    }
}

technique Bloom_BlurVertical
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_BlurVertical();
    }
}

technique Bloom_Composite
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_Composite();
    }
}
