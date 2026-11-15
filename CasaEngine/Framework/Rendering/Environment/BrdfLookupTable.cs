using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// The split-sum BRDF table of the image-based lighting (ADR-0109): a small 2D texture, computed once on the CPU by numerical
/// integration, that gives for a view angle and a roughness the scale <c>A</c> and the bias <c>B</c> applied to the specular
/// reflectance at normal incidence: <c>F0 * A + B</c>.
/// <para/>
/// Axes: <c>u</c> (columns, x) is <c>NoV</c>, the cosine between the normal and the view direction; <c>v</c> (rows, y) is the
/// perceptual roughness; both in [0, 1], texel <c>i</c> holding the centre of its cell (<c>(i + 0.5) / size</c>), so a shader
/// reads it at <c>(NoV, roughness)</c> without a remap. Row 0 is the lowest roughness. Channel x is <c>A</c>, channel y is
/// <c>B</c>. The integration follows Karis, "Real Shading in Unreal Engine 4" (2013): GGX importance sampling of the half
/// vector with <c>alpha = roughness^2</c>, Smith geometry term with <c>k = alpha / 2</c>, <c>N = V = R</c>.
/// </summary>
public static class BrdfLookupTable
{
    /// <summary>Side of the table, in texels.</summary>
    public const int Size = 64;

    /// <summary>Number of importance samples per texel.</summary>
    public const int SampleCount = 1024;

    /// <summary>
    /// Integrates the table: <paramref name="size"/> by <paramref name="size"/> values, row-major (index <c>y * size + x</c>,
    /// x = NoV, y = roughness), x = scale <c>A</c>, y = bias <c>B</c>.
    /// </summary>
    public static Vector2[] Compute(int size = Size, int sampleCount = SampleCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleCount, 1);

        var table = new Vector2[size * size];
        for (int y = 0; y < size; y++)
        {
            float roughness = (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float noV = (x + 0.5f) / size;
                table[(y * size) + x] = Integrate(noV, roughness, sampleCount);
            }
        }

        return table;
    }

    /// <summary>The scale and the bias for one view angle and one perceptual roughness.</summary>
    public static Vector2 Integrate(float noV, float roughness, int sampleCount = SampleCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleCount, 1);

        noV = Math.Clamp(noV, 1e-4f, 1.0f);
        roughness = Math.Clamp(roughness, 1e-4f, 1.0f);

        float alpha = roughness * roughness;
        float k = alpha * 0.5f;
        float vX = MathF.Sqrt(1.0f - (noV * noV));
        float vZ = noV;

        float scale = 0.0f;
        float bias = 0.0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float xi1 = (i + 0.5f) / sampleCount;
            float xi2 = RadicalInverse(i);

            // GGX importance sampling of the half vector around N = (0, 0, 1).
            float phi = 2.0f * MathF.PI * xi1;
            float cosTheta = MathF.Sqrt((1.0f - xi2) / (1.0f + (((alpha * alpha) - 1.0f) * xi2)));
            float sinTheta = MathF.Sqrt(MathF.Max(0.0f, 1.0f - (cosTheta * cosTheta)));
            float hX = sinTheta * MathF.Cos(phi);
            float hY = sinTheta * MathF.Sin(phi);
            float hZ = cosTheta;

            float vDotH = (vX * hX) + (vZ * hZ);
            float lZ = (2.0f * vDotH * hZ) - vZ;
            float noL = lZ;
            float noH = hZ;
            vDotH = MathF.Max(vDotH, 0.0f);

            if (noL <= 0.0f || noH <= 0.0f || vDotH <= 0.0f)
            {
                continue;
            }

            float gL = noL / ((noL * (1.0f - k)) + k);
            float gV = noV / ((noV * (1.0f - k)) + k);
            float visibility = gL * gV * vDotH / (noH * noV);
            float oneMinusVoH = 1.0f - vDotH;
            float squared = oneMinusVoH * oneMinusVoH;
            float fresnel = squared * squared * oneMinusVoH;

            scale += (1.0f - fresnel) * visibility;
            bias += fresnel * visibility;
        }

        return new Vector2(scale / sampleCount, bias / sampleCount);
    }

    /// <summary>
    /// Creates the texture of the table: <see cref="Size"/> by <see cref="Size"/>, <see cref="SurfaceFormat.Vector2"/>, no mips.
    /// The caller owns it.
    /// </summary>
    public static Texture2D CreateTexture(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        var texture = new Texture2D(graphicsDevice, Size, Size, false, SurfaceFormat.Vector2);
        texture.SetData(Compute());
        return texture;
    }

    // Van der Corput sequence in base 2 (the second coordinate of the Hammersley set).
    private static float RadicalInverse(int index)
    {
        uint bits = (uint)index;
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        return bits * 2.3283064365386963e-10f;
    }
}
