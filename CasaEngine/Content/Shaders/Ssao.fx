#if OPENGL
#define DEBUG_VS_SHADERMODEL vs_3_0
#define DEBUG_PS_SHADERMODEL ps_3_0
#else
#define DEBUG_VS_SHADERMODEL vs_4_0
#define DEBUG_PS_SHADERMODEL ps_4_0
#endif

// Screen-space ambient occlusion (ADR-0115). Four pixel-shader-only techniques drawn through SpriteBatch.Begin(effect:), like
// Tonemap.fx, Bloom.fx and Fxaa.fx: SpriteBatch keeps its own vertex shader and binds the texture it draws as texture 0.
//
//   Ssao_Occlusion      draws the scene depth target (the view depth in world units written by the scene depth pre-pass, the
//                       background constant where no geometry was drawn) and writes an occlusion factor (1 = open, 0 = closed).
//   Ssao_BlurHorizontal / Ssao_BlurVertical
//                       draw the occlusion factor and blur it along one axis; the scene depth is the second texture and the weight
//                       of a tap falls with its depth difference to the centre, so the blur does not cross a depth edge.
//   Ssao_Composite      draws the blurred occlusion factor and writes lerp(1, occlusion, intensity); the host blends it by
//                       multiplication into the HDR scene target.
//
// Occlusion, per pixel (a hemisphere kernel in the style of the published normal-oriented SSAO; written here from its description):
//
//   1. The view-space position of the pixel is rebuilt from the depth with the projection scale (1 / M11, 1 / M22): the
//      depth is the distance along the camera axis, so x = ndc.x * scale.x * depth, y = ndc.y * scale.y * depth, z = -depth.
//   2. The normal is rebuilt from the positions of the four neighbours: on each axis the one-sided difference with the smaller
//      depth step is kept, so the normal is not tilted by a neighbour across a depth edge.
//   3. SSAO_SAMPLES points of a fixed hemisphere kernel (a golden-angle spiral, denser near the pixel) are rotated about the
//      normal by an angle computed from the pixel position (interleaved gradient noise: no texture), scaled by the radius and
//      added to the position. Each point is projected back to the screen; when the depth stored there is nearer than the point
//      by more than the bias, the point is hidden and counts, weighted down when the occluder is farther than the radius from
//      the pixel (the range check).
//   4. The factor is 1 minus the mean of the counts. A pixel at or beyond the sky threshold is left at 1.
//
// Every read is a tex2D with a point filter (the OpenGL chain ignores a requested texture level, ADR-0109), and there is no
// early return, so every read stays in uniform flow control.

// Size of one texel of the target: (1 / width, 1 / height).
float2 SsaoTexelSize = float2(1.0f, 1.0f);

// (1 / M11, 1 / M22) of the perspective projection of the view.
float2 SsaoProjectionScale = float2(1.0f, 1.0f);

// Hemisphere radius (world units) and bias (world units): a point must be hidden by more than the bias to count.
float SsaoRadius = 0.5f;
float SsaoBias = 0.02f;

// Share of the occlusion applied by the composite (1 = full).
float SsaoIntensity = 1.0f;

// A depth at or beyond this value is sky (SceneDepthPrepass.SkyThreshold).
float SsaoSkyThreshold = 30000.0f;


// The texture drawn by SpriteBatch (sampler 0): the depth for the occlusion pass, the occlusion factor for the blur and the
// composite. Point filtered and clamped.
Texture2D SsaoSourceTexture;
sampler2D SsaoSourceSampler = sampler_state
{
    Texture = <SsaoSourceTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

// The scene depth read by the blur (sampler 1).
Texture2D SsaoDepthTexture;
sampler2D SsaoDepthSampler = sampler_state
{
    Texture = <SsaoDepthTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

#define SSAO_SAMPLES 12
#define BLUR_RADIUS 4

struct PS_INPUT
{
    float4 position : SV_POSITION;
    float4 color : COLOR0;
    float2 textureCoordinates : TEXCOORD0;
};

float3 ViewPosition(float2 uv, float depth)
{
    float2 ndc = float2(uv.x * 2.0f - 1.0f, 1.0f - uv.y * 2.0f);
    return float3(ndc * SsaoProjectionScale * depth, -depth);
}

float2 ScreenUv(float3 viewPosition)
{
    // The inverse of ViewPosition for a point in front of the camera (z < 0).
    float depth = max(-viewPosition.z, 0.0001f);
    float2 ndc = viewPosition.xy / (SsaoProjectionScale * depth);
    return float2(ndc.x * 0.5f + 0.5f, 0.5f - ndc.y * 0.5f);
}

float DepthAt(float2 uv)
{
    return tex2D(SsaoSourceSampler, uv).r;
}

float4 PS_Occlusion(PS_INPUT input) : COLOR0
{
    float2 uv = input.textureCoordinates;
    float2 texel = SsaoTexelSize;

    float depthC = DepthAt(uv);
    float depthL = DepthAt(uv + float2(-texel.x, 0.0f));
    float depthR = DepthAt(uv + float2(texel.x, 0.0f));
    float depthU = DepthAt(uv + float2(0.0f, -texel.y));
    float depthD = DepthAt(uv + float2(0.0f, texel.y));

    float3 positionC = ViewPosition(uv, depthC);
    float3 positionL = ViewPosition(uv + float2(-texel.x, 0.0f), depthL);
    float3 positionR = ViewPosition(uv + float2(texel.x, 0.0f), depthR);
    float3 positionU = ViewPosition(uv + float2(0.0f, -texel.y), depthU);
    float3 positionD = ViewPosition(uv + float2(0.0f, texel.y), depthD);

    // The one-sided difference with the smaller depth step on each axis.
    float3 deltaX = abs(depthR - depthC) < abs(depthC - depthL) ? positionR - positionC : positionC - positionL;
    float3 deltaY = abs(depthD - depthC) < abs(depthC - depthU) ? positionD - positionC : positionC - positionU;

    // Image y grows downwards, so the vertical difference is the opposite of the view-space up axis: cross(deltaX, deltaY)
    // points away from the camera on a front-facing surface, hence the reversed order of the product below.
    float3 normal = normalize(cross(deltaY, deltaX) + float3(0.0f, 0.0f, 0.000001f));
    // Facing the camera: the view-space position points away from it.
    normal = dot(normal, positionC) > 0.0f ? -normal : normal;

    // Rotation of the kernel about the normal: interleaved gradient noise of the pixel position.
    float2 pixel = uv / texel;
    float noise = frac(52.9829189f * frac(dot(pixel, float2(0.06711056f, 0.00583715f))));
    float angle = noise * 6.2831853f;
    float3 randomVector = float3(cos(angle), sin(angle), 0.0f);
    float3 tangent = randomVector - normal * dot(randomVector, normal);
    tangent = normalize(tangent + float3(0.000001f, 0.0f, 0.0f));
    float3 bitangent = cross(normal, tangent);

    float occluded = 0.0f;
    [unroll]
    for (int i = 0; i < SSAO_SAMPLES; i++)
    {
        float fraction = ((float)i + 0.5f) / (float)SSAO_SAMPLES;
        // Height above the surface (never grazing, so a flat surface does not occlude itself), azimuth by the golden angle.
        float height = lerp(0.15f, 1.0f, fraction);
        float azimuth = (float)i * 2.3999632f;
        float ring = sqrt(max(1.0f - height * height, 0.0f));
        float3 direction = float3(ring * cos(azimuth), ring * sin(azimuth), height);
        // Denser near the pixel.
        float scale = lerp(0.25f, 1.0f, fraction * fraction);

        float3 offset = tangent * direction.x + bitangent * direction.y + normal * direction.z;
        float3 samplePosition = positionC + offset * (scale * SsaoRadius);
        float2 sampleUv = ScreenUv(samplePosition);
        float sampleDepth = -samplePosition.z;
        float sceneDepth = DepthAt(sampleUv);

        float isHidden = sceneDepth < sampleDepth - SsaoBias ? 1.0f : 0.0f;
        float rangeWeight = smoothstep(0.0f, 1.0f, SsaoRadius / max(abs(depthC - sceneDepth), 0.0001f));
        occluded += isHidden * rangeWeight;
    }

    float occlusion = 1.0f - occluded / (float)SSAO_SAMPLES;
    float isSky = depthC >= SsaoSkyThreshold ? 1.0f : 0.0f;
    occlusion = lerp(occlusion, 1.0f, isSky);
    return float4(occlusion, occlusion, occlusion, 1.0f);
}

float4 BlurAlong(float2 uv, float2 step)
{
    float depthC = tex2D(SsaoDepthSampler, uv).r;
    // The tolerance on the depth difference grows with the distance, so a slanted surface keeps its blur.
    float tolerance = 0.03f * depthC + 0.05f;

    float sum = 0.0f;
    float weightSum = 0.0f;
    [unroll]
    for (int i = -BLUR_RADIUS; i <= BLUR_RADIUS; i++)
    {
        float2 tapUv = uv + step * (float)i;
        float spatial = exp(-(float)(i * i) / 8.0f);
        float depthDifference = (tex2D(SsaoDepthSampler, tapUv).r - depthC) / tolerance;
        float weight = spatial * exp(-depthDifference * depthDifference);
        sum += tex2D(SsaoSourceSampler, tapUv).r * weight;
        weightSum += weight;
    }

    float blurred = sum / weightSum;
    float isSky = depthC >= SsaoSkyThreshold ? 1.0f : 0.0f;
    blurred = lerp(blurred, 1.0f, isSky);
    return float4(blurred, blurred, blurred, 1.0f);
}

float4 PS_BlurHorizontal(PS_INPUT input) : COLOR0
{
    return BlurAlong(input.textureCoordinates, float2(SsaoTexelSize.x, 0.0f));
}

float4 PS_BlurVertical(PS_INPUT input) : COLOR0
{
    return BlurAlong(input.textureCoordinates, float2(0.0f, SsaoTexelSize.y));
}

float4 PS_Composite(PS_INPUT input) : COLOR0
{
    float occlusion = tex2D(SsaoSourceSampler, input.textureCoordinates).r;
    float factor = saturate(lerp(1.0f, occlusion, SsaoIntensity));
    return float4(factor, factor, factor, 1.0f);
}

technique Ssao_Occlusion
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_Occlusion();
    }
}

technique Ssao_BlurHorizontal
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_BlurHorizontal();
    }
}

technique Ssao_BlurVertical
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_BlurVertical();
    }
}

technique Ssao_Composite
{
    pass P0
    {
        PixelShader = compile DEBUG_PS_SHADERMODEL PS_Composite();
    }
}
