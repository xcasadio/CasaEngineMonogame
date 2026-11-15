#include "Macros.fxh"

// Convolution of an environment cube into the two textures of the image-based lighting (ADR-0109). Every technique draws one
// full-screen quad into the viewport the CPU has set; the quad covers the viewport with TexCoord in [0, 1], (0, 0) at the top
// left, and the texel of the target at (u, v) is a direction of the sphere:
//
//   Irradiance      writes one face of the irradiance cube: the CPU sets the basis of the face (FaceForward, FaceRight, FaceUp,
//                   the same direction convention as PanoramaEnvironmentGenerator.GetDirectionForFace) and the direction is
//                   normalize(FaceForward + x FaceRight + y FaceUp) with x = 2 u - 1 and y = 1 - 2 v. Cosine-weighted average
//                   of the incoming radiance around the direction (fixed grid of 16 x 32 samples, normalised by the sum of the
//                   weights, so a uniform cube gives back its own colour: the result is E / pi, meant to be multiplied by the
//                   diffuse albedo without a further 1 / pi).
//   PrefilterGgx    writes one band of the prefiltered atlas (latitude-longitude, the mapping of
//                   PanoramaEnvironmentGenerator.GetPanoramaUv: u = 0.5 + atan2(x, -z) / 2 pi, v = 0.5 - asin(y) / pi). GGX
//                   importance sampling (64 samples, N = V = R) of the radiance around the direction, for the perceptual
//                   roughness Roughness (alpha = Roughness squared). Roughness 0 returns the source as is.
//   SampleCubeLevel diagnostic: the texel of the source at the direction FaceForward, requesting the mip level Level. Used by
//                   the GPU test that pins the outcome of step 1b of T6.1: on the OpenGL chain the level is ignored.
//
// The prefiltered result is a 2D atlas, not a cube with mips, because texCUBElod is translated to a plain textureCube on this
// chain (the requested level is ignored); see ADR-0109.
//
// Every sample of a source that is not linear (SourceIsLinear = 0) is decoded with pow(abs(c), 2.2) before it is accumulated, so
// the generated textures always hold linear light, whatever the colour pipeline of the view. The shader never reads a colour
// pipeline constant. The source is read without mips (a source cube has none).

float3 FaceForward = float3(0.0f, 0.0f, 1.0f);
float3 FaceRight = float3(1.0f, 0.0f, 0.0f);
float3 FaceUp = float3(0.0f, 1.0f, 0.0f);

// Perceptual roughness of the prefiltered band.
float Roughness = 0.0f;

// 1 when the source cube holds linear light, 0 when it holds display colours.
float SourceIsLinear = 1.0f;

// Mip level read by SampleCubeLevel.
float Level = 0.0f;

DECLARE_CUBEMAP(SourceCube, 0);

#if OPENGL
#define IBL_VS_SHADERMODEL vs_3_0
#define IBL_PS_SHADERMODEL ps_3_0
#else
#define IBL_VS_SHADERMODEL vs_4_0
#define IBL_PS_SHADERMODEL ps_4_0
#endif

static const float Pi = 3.14159265f;

static const int IrradianceThetaSteps = 16;
static const int IrradiancePhiSteps = 32;
static const int PrefilterSampleCount = 64;

struct VS_INPUT
{
    float3 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VS_OUTPUT VS_Main(VS_INPUT input)
{
    VS_OUTPUT output;
    output.Position = float4(input.Position.xy, 0.0f, 1.0f);
    output.TexCoord = input.TexCoord;
    return output;
}

float3 FaceDirection(float2 texCoord)
{
    float u = texCoord.x * 2.0f - 1.0f;
    float v = 1.0f - texCoord.y * 2.0f;
    return normalize(FaceForward + u * FaceRight + v * FaceUp);
}

// Direction of a texel of a latitude-longitude band (u to the east, v to the south); inverse of GetPanoramaUv.
float3 LatLongDirection(float2 texCoord)
{
    float longitude = (texCoord.x - 0.5f) * (2.0f * Pi);
    float latitude = (0.5f - texCoord.y) * Pi;
    float cosLatitude = cos(latitude);
    return float3(cosLatitude * sin(longitude), sin(latitude), -cosLatitude * cos(longitude));
}

// Linear radiance of the source in a direction, at its level 0. SAMPLE_CUBEMAP_LEVEL is used because a plain cube sample is a
// gradient operation, refused inside a dynamic loop on the D3D target; on the OpenGL chain the level is ignored (see Macros.fxh).
float3 SampleSource(float3 direction)
{
    float3 c = SAMPLE_CUBEMAP_LEVEL(SourceCube, direction, 0.0f).rgb;
    if (SourceIsLinear < 0.5f)
    {
        c = pow(abs(c), 2.2f);
    }

    return c;
}

// Orthonormal frame around n: tangent, bitangent.
void BuildFrame(float3 n, out float3 tangent, out float3 bitangent)
{
    float3 helper = abs(n.y) < 0.999f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
    tangent = normalize(cross(helper, n));
    bitangent = cross(n, tangent);
}

// Radical inverse in base 2 of an index below 64, in floating point (shader model 3 has no bit operations).
float RadicalInverse64(float index)
{
    float result = 0.0f;
    float weight = 0.5f;
    float remaining = index;
    for (int bit = 0; bit < 6; bit++)
    {
        float quotient = floor(remaining * 0.5f);
        float digit = remaining - quotient * 2.0f;
        result += digit * weight;
        weight *= 0.5f;
        remaining = quotient;
    }

    return result;
}

float4 PS_Irradiance(VS_OUTPUT input) : COLOR0
{
    float3 n = FaceDirection(input.TexCoord);
    float3 tangent;
    float3 bitangent;
    BuildFrame(n, tangent, bitangent);

    float3 sum = float3(0.0f, 0.0f, 0.0f);
    float weightSum = 0.0f;

    for (int t = 0; t < IrradianceThetaSteps; t++)
    {
        float theta = (t + 0.5f) * (0.5f * Pi / IrradianceThetaSteps);
        float sinTheta = sin(theta);
        float cosTheta = cos(theta);
        float weight = cosTheta * sinTheta;

        for (int p = 0; p < IrradiancePhiSteps; p++)
        {
            float phi = (p + 0.5f) * (2.0f * Pi / IrradiancePhiSteps);
            float3 direction = tangent * (sinTheta * cos(phi)) + bitangent * (sinTheta * sin(phi)) + n * cosTheta;
            sum += SampleSource(direction) * weight;
            weightSum += weight;
        }
    }

    return float4(sum / weightSum, 1.0f);
}

float4 PS_PrefilterGgx(VS_OUTPUT input) : COLOR0
{
    float3 n = LatLongDirection(input.TexCoord);
    float3 tangent;
    float3 bitangent;
    BuildFrame(n, tangent, bitangent);

    float alpha = Roughness * Roughness;
    float alphaSquared = alpha * alpha;

    float3 sum = float3(0.0f, 0.0f, 0.0f);
    float weightSum = 0.0f;

    for (int i = 0; i < PrefilterSampleCount; i++)
    {
        float xi1 = (i + 0.5f) / PrefilterSampleCount;
        float xi2 = RadicalInverse64(i);

        float phi = 2.0f * Pi * xi2;
        float cosTheta = sqrt((1.0f - xi1) / (1.0f + (alphaSquared - 1.0f) * xi1));
        float sinTheta = sqrt(max(1.0f - cosTheta * cosTheta, 0.0f));

        float3 h = tangent * (sinTheta * cos(phi)) + bitangent * (sinTheta * sin(phi)) + n * cosTheta;
        float3 l = 2.0f * dot(n, h) * h - n;
        float nol = dot(n, l);
        if (nol > 0.0f)
        {
            sum += SampleSource(l) * nol;
            weightSum += nol;
        }
    }

    return float4(sum / max(weightSum, 1e-5f), 1.0f);
}

float4 PS_SampleCubeLevel(VS_OUTPUT input) : COLOR0
{
    return SAMPLE_CUBEMAP_LEVEL(SourceCube, FaceForward, Level);
}

technique Irradiance
{
    pass P0
    {
        VertexShader = compile IBL_VS_SHADERMODEL VS_Main();
        PixelShader = compile IBL_PS_SHADERMODEL PS_Irradiance();
    }
}

technique PrefilterGgx
{
    pass P0
    {
        VertexShader = compile IBL_VS_SHADERMODEL VS_Main();
        PixelShader = compile IBL_PS_SHADERMODEL PS_PrefilterGgx();
    }
}

technique SampleCubeLevel
{
    pass P0
    {
        VertexShader = compile IBL_VS_SHADERMODEL VS_Main();
        PixelShader = compile IBL_PS_SHADERMODEL PS_SampleCubeLevel();
    }
}
