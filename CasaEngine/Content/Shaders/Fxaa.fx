#if OPENGL
#define DEBUG_VS_SHADERMODEL vs_3_0
#define DEBUG_PS_SHADERMODEL ps_3_0
#else
#define DEBUG_VS_SHADERMODEL vs_4_0
#define DEBUG_PS_SHADERMODEL ps_4_0
#endif

// FXAA, a screen-space anti-aliasing filter applied to the tonemapped, display-encoded scene colour (ADR-0114). Like
// Tonemap.fx and Bloom.fx it is a pixel-shader-only effect drawn through SpriteBatch.Begin(effect:): SpriteBatch keeps its
// own vertex shader and binds the source texture as texture 0, so the technique declares no vertex shader and the pixel
// input mirrors the SpriteBatch vertex output. The source is drawn 1:1 (one texel per destination pixel), so the output
// has the size of the input and no pixel is shifted.
//
// The algorithm is the published idea of FXAA (a post-process that finds luminance edges and blurs along them), written
// here from its description; no third-party code is used. Per pixel:
//
//   1. Luminance of the encoded colour (dot with the Rec. 601 weights). The centre M and its four neighbours N, S, E, W are
//      read. When the local contrast (max - min) is below both an absolute and a relative threshold, the pixel is returned
//      unchanged: flat areas and the interface are never touched.
//   2. The four diagonal neighbours give two second-difference measures, one across a horizontal edge and one across a
//      vertical edge; the larger tells the orientation of the edge.
//   3. Across the edge, the neighbour with the larger luminance step is the side the edge lies on. The edge is followed in
//      both directions along its own axis, one texel at a time up to EdgeSearchSteps, sampling halfway between the pixel and
//      that neighbour (the bilinear filter averages the two), until the average luminance moves away from the local average
//      by a quarter of the step: that is the end of the edge on that side.
//   4. With the distances to the two ends, the pixel is moved towards the nearer end across the edge (0.5 texel at an end, 0
//      in the middle), only when that end is on the side that makes the pixel closer to the neighbour's luminance (otherwise
//      the pixel is already on the right side of the edge).
//   5. A sub-pixel term (how much the pixel differs from the average of its 3 x 3 neighbourhood) catches isolated bright or
//      dark pixels that have no edge; the larger of the two offsets is used.
//   6. The colour is read at the displaced position with the bilinear filter, which does the blend. Alpha is 1.
//
// Sampling uses tex2D only (the OpenGL chain ignores a requested texture level, ADR-0110).

// Size of one source texel: (1 / width, 1 / height).
float2 FxaaTexelSize = float2(1.0f, 1.0f);

// The tonemapped scene drawn by SpriteBatch (sampler 0). Bilinear and clamped: the blend is a bilinear read between two texels.
Texture2D FxaaSourceTexture;
sampler2D FxaaSourceSampler = sampler_state
{
    Texture = <FxaaSourceTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Local contrast below which a pixel is left alone: the larger of an absolute floor and a fraction of the brightest
// neighbour (so the dark areas keep a lower absolute threshold than the bright ones).
static const float ContrastFloor = 0.0312f;
static const float ContrastRelative = 0.125f;

// Number of texels the edge is followed in each direction (the longest edge handled well is about twice this).
#define EDGE_SEARCH_STEPS 12

// Share of the sub-pixel blend (1 = full, 0 = none).
static const float SubpixelBlend = 0.75f;

struct PS_INPUT
{
    float4 position : SV_POSITION;
    float4 color : COLOR0;
    float2 textureCoordinates : TEXCOORD0;
};

float4 SampleSource(float2 uv)
{
    return tex2D(FxaaSourceSampler, uv);
}

float Luma(float3 rgb)
{
    return dot(rgb, float3(0.299f, 0.587f, 0.114f));
}

float LumaAt(float2 uv)
{
    return Luma(SampleSource(uv).rgb);
}

float4 PS_Fxaa(PS_INPUT input) : COLOR0
{
    float2 uv = input.textureCoordinates;
    float2 texel = FxaaTexelSize;

    float4 centre = SampleSource(uv);
    float lumaM = Luma(centre.rgb);
    float lumaN = LumaAt(uv + float2(0.0f, -texel.y));
    float lumaS = LumaAt(uv + float2(0.0f, texel.y));
    float lumaW = LumaAt(uv + float2(-texel.x, 0.0f));
    float lumaE = LumaAt(uv + float2(texel.x, 0.0f));

    float lumaMin = min(lumaM, min(min(lumaN, lumaS), min(lumaW, lumaE)));
    float lumaMax = max(lumaM, max(max(lumaN, lumaS), max(lumaW, lumaE)));
    float range = lumaMax - lumaMin;
    // Flat area: the offset below stays 0 and the centre is returned (no early exit: every read stays in uniform flow control).
    bool isFlat = range < max(ContrastFloor, lumaMax * ContrastRelative);

    float lumaNW = LumaAt(uv + float2(-texel.x, -texel.y));
    float lumaNE = LumaAt(uv + float2(texel.x, -texel.y));
    float lumaSW = LumaAt(uv + float2(-texel.x, texel.y));
    float lumaSE = LumaAt(uv + float2(texel.x, texel.y));

    // Second differences across a horizontal edge (rows differ) and across a vertical edge (columns differ).
    float edgeHorizontal = abs(lumaNW + lumaSW - 2.0f * lumaW) + 2.0f * abs(lumaN + lumaS - 2.0f * lumaM) + abs(lumaNE + lumaSE - 2.0f * lumaE);
    float edgeVertical = abs(lumaNW + lumaNE - 2.0f * lumaN) + 2.0f * abs(lumaW + lumaE - 2.0f * lumaM) + abs(lumaSW + lumaSE - 2.0f * lumaS);
    bool isHorizontal = edgeHorizontal >= edgeVertical;

    // The side of the edge: the neighbour across the edge with the larger luminance step.
    float lumaBefore = isHorizontal ? lumaN : lumaW;
    float lumaAfter = isHorizontal ? lumaS : lumaE;
    float gradientBefore = abs(lumaBefore - lumaM);
    float gradientAfter = abs(lumaAfter - lumaM);
    bool sideIsBefore = gradientBefore >= gradientAfter;
    float gradient = max(gradientBefore, gradientAfter);
    float lumaSide = sideIsBefore ? lumaBefore : lumaAfter;
    float sideSign = sideIsBefore ? -1.0f : 1.0f;

    // Unit steps (in uv): across the edge (towards the side) and along it.
    float2 acrossStep = isHorizontal ? float2(0.0f, texel.y * sideSign) : float2(texel.x * sideSign, 0.0f);
    float2 alongStep = isHorizontal ? float2(texel.x, 0.0f) : float2(0.0f, texel.y);

    float lumaLocalAverage = 0.5f * (lumaSide + lumaM);
    float gradientThreshold = 0.25f * gradient;

    // Follow the edge both ways, halfway between the pixel and the side neighbour.
    float2 edgeUv = uv + acrossStep * 0.5f;
    float distanceForward = (float)EDGE_SEARCH_STEPS;
    float distanceBackward = (float)EDGE_SEARCH_STEPS;
    float lumaEndForward = lumaLocalAverage;
    float lumaEndBackward = lumaLocalAverage;
    bool doneForward = false;
    bool doneBackward = false;

    [unroll]
    for (int i = 1; i <= EDGE_SEARCH_STEPS; i++)
    {
        float lumaForward = LumaAt(edgeUv + alongStep * (float)i);
        float lumaBackward = LumaAt(edgeUv - alongStep * (float)i);

        bool endForward = abs(lumaForward - lumaLocalAverage) >= gradientThreshold;
        bool endBackward = abs(lumaBackward - lumaLocalAverage) >= gradientThreshold;

        if (!doneForward && endForward)
        {
            doneForward = true;
            distanceForward = (float)i;
            lumaEndForward = lumaForward;
        }

        if (!doneBackward && endBackward)
        {
            doneBackward = true;
            distanceBackward = (float)i;
            lumaEndBackward = lumaBackward;
        }
    }

    // Towards the nearer end; valid only when the luminance at that end lies on the other side of the local average than the
    // centre does (the pixel then belongs to the part of the edge that must be blended).
    bool forwardIsNearer = distanceForward < distanceBackward;
    float distanceNearest = forwardIsNearer ? distanceForward : distanceBackward;
    float lumaEnd = forwardIsNearer ? lumaEndForward : lumaEndBackward;
    bool centreBelowAverage = lumaM < lumaLocalAverage;
    bool endBelowAverage = (lumaEnd - lumaLocalAverage) < 0.0f;
    float edgeOffset = (centreBelowAverage != endBelowAverage) ? 0.5f - distanceNearest / (distanceForward + distanceBackward) : 0.0f;

    // Sub-pixel term: difference with the weighted 3 x 3 average, relative to the local contrast, smoothed and squared.
    float lumaAverage = (2.0f * (lumaN + lumaS + lumaW + lumaE) + lumaNW + lumaNE + lumaSW + lumaSE) / 12.0f;
    float subpixel = saturate(abs(lumaAverage - lumaM) / max(range, 0.0001f));
    subpixel = subpixel * subpixel * (3.0f - 2.0f * subpixel);
    subpixel = subpixel * subpixel * SubpixelBlend;

    float offset = isFlat ? 0.0f : max(edgeOffset, subpixel);
    return float4(SampleSource(uv + acrossStep * offset).rgb, 1.0f);
}

technique Fxaa
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_Fxaa();
    }
}
