#if OPENGL
#define DEBUG_VS_SHADERMODEL vs_3_0
#define DEBUG_PS_SHADERMODEL ps_3_0
#else
#define DEBUG_VS_SHADERMODEL vs_4_0
#define DEBUG_PS_SHADERMODEL ps_4_0
#endif

// Free PSX quad (ADR-0068). It draws the quads of SpriteRendererComponent.DrawPsxQuad at the resolution of the screen, with the
// rule of the plan of E19.g G2b: at the integer factor k (screen pixels per PS1 pixel), for the screen pixel (sx, sy),
//  - coverage: the top-left corner of the screen pixel, (sx / k, sy / k) in PS1 units, is tested against the quad (top-left rule).
//    The rasterizer samples the pixel centre, so the vertex shader moves the geometry half a SCREEN pixel right and down.
//  - texel: floor(u(p) + 1/2), p = ((sx + 1/2) / k - 1/2, (sy + 1/2) / k - 1/2), u the affine map of the triangle that covers.
//    The interpolated coordinate is the one at the top-left corner (the geometry shift); the CPU already added 1/2 texel plus
//    1/4096 texel to it; the pixel shader moves it to p with the term -(k / 2 - 1/2) (ddx + ddy) (zero at k = 1) and then
//    chooses the texel itself (floor, then a read at the centre of that texel): the sampler decides nothing, because Direct3D 11
//    only guarantees 8 sub-texel bits, so a pixel just under a texel boundary could flip.
// Otherwise the pixel logic of SpriteBatch.fx: raw-alpha window, then the rejection of a texel whose alpha is at most 0.01.

float4x4 ViewProj;
float4x4 World;
float4 Color;

// Half a screen pixel in clip space, to the right and down: (1 / viewport width, -1 / viewport height).
float2 HalfPixel;

// k: screen pixels per world unit (PS1 pixel).
float Scale = 1.0f;

// Size of the bound texture in texels.
float2 TextureSize = float2(1.0f, 1.0f);

// Raw-alpha window (min, max] of the texels this draw keeps (ADR-0051), as in SpriteBatch.fx.
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
    Out.position.xy += HalfPixel * Out.position.w;
    Out.textureCoordinates = vertex.textureCoordinates;
    return Out;
}

float4 PS(VS_OUTPUT input) : COLOR
{
    float2 t = input.textureCoordinates * TextureSize;
    t -= (0.5f * Scale - 0.5f) * (ddx(t) + ddy(t));

    float2 texel = floor(t);
    float4 sampled = tex2D(TextureSampler, (texel + 0.5f) / TextureSize);
    if (sampled.a <= AlphaWindow.x || sampled.a > AlphaWindow.y)
    {
        discard;
    }

    float4 result = sampled * Color;
    if (result.a <= 0.01f)
    {
        discard;
    }

    return result;
}

technique Simple
{
    pass
    {
        VertexShader = compile DEBUG_VS_SHADERMODEL VS();
        PixelShader = compile DEBUG_PS_SHADERMODEL PS();
    }
}
