using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T6.2 of the PBR plan: the split-sum BRDF table (ADR-0107). The first four tests do not depend on the integrated formula: they
/// pin the limit of a smooth surface (analytic), the monotony, the range and the axes. The fifth compares a few texels with an
/// independent double-precision integration at a high sample count.
/// </summary>
public class BrdfLookupTableTests
{
    private static readonly Lazy<Vector2[]> Table = new(() => BrdfLookupTable.Compute());

    private readonly ITestOutputHelper _output;

    public BrdfLookupTableTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Vector2 Texel(int x, int y) => Table.Value[(y * BrdfLookupTable.Size) + x];

    private static float NoV(int x) => (x + 0.5f) / BrdfLookupTable.Size;

    [Fact]
    public void Shape_IsSixtyFourSquareAndRowMajor()
    {
        Assert.Equal(64, BrdfLookupTable.Size);
        Assert.Equal(64 * 64, Table.Value.Length);
    }

    // Test (1): first row (smallest roughness), A = 1 - (1 - NoV)^5 and B = (1 - NoV)^5 within 0.02.
    [Theory]
    [InlineData(15)]
    [InlineData(31)]
    [InlineData(47)]
    [InlineData(63)]
    public void FirstRow_FollowsTheSchlickLimitOfASmoothSurface(int x)
    {
        float noV = NoV(x);
        float fresnelWeight = MathF.Pow(1.0f - noV, 5.0f);
        Vector2 texel = Texel(x, 0);

        _output.WriteLine($"NoV {noV:F4}: A {texel.X:F4} (expected {1.0f - fresnelWeight:F4}), B {texel.Y:F4} (expected {fresnelWeight:F4})");
        Assert.InRange(texel.X, (1.0f - fresnelWeight) - 0.02f, (1.0f - fresnelWeight) + 0.02f);
        Assert.InRange(texel.Y, fresnelWeight - 0.02f, fresnelWeight + 0.02f);
    }

    // Test (2): at a fixed NoV, A + B does not grow with the roughness (a small slack absorbs the sampling noise). Measured on the
    // table and on a 65536-sample integration: this holds from NoV about 0.3 up; at grazing angles the single-scatter GGX integral
    // itself rises slightly with the roughness (see the next test), so the columns below start at NoV 0.38.
    [Theory]
    [InlineData(24)]
    [InlineData(40)]
    [InlineData(63)]
    public void SumOfScaleAndBias_DoesNotGrowWithRoughness(int x)
    {
        float previous = float.MaxValue;
        for (int y = 0; y < BrdfLookupTable.Size; y++)
        {
            Vector2 texel = Texel(x, y);
            float sum = texel.X + texel.Y;
            Assert.True(sum <= previous + 0.01f, $"A + B grows at x {x}, row {y}: {previous} then {sum}");
            previous = Math.Min(previous, sum);
        }
    }

    // Documents the exception: at a grazing angle (NoV 0.13) A + B first falls, then rises by about 0.016 around roughness 0.4 to 0.55
    // before falling again; the 1024-sample table and a 65536-sample integration agree on it, so it is the integral, not noise.
    [Fact]
    public void AtAGrazingAngle_TheSumRisesALittleWithRoughness_AsTheReferenceDoes()
    {
        const int x = 8;
        float noV = NoV(x);
        float minimum = float.MaxValue;
        float maximumRise = 0.0f;
        for (int y = 0; y < BrdfLookupTable.Size; y++)
        {
            Vector2 texel = Table.Value[(y * BrdfLookupTable.Size) + x];
            float sum = texel.X + texel.Y;
            maximumRise = Math.Max(maximumRise, sum - minimum);
            minimum = Math.Min(minimum, sum);
        }

        _output.WriteLine($"NoV {noV:F3}: largest rise of A + B along the roughness: {maximumRise:F4}");
        Assert.InRange(maximumRise, 0.0f, 0.03f);
    }

    // Test (3): A and B stay in [0, 1].
    [Fact]
    public void ScaleAndBias_StayInTheUnitRange()
    {
        foreach (Vector2 texel in Table.Value)
        {
            Assert.InRange(texel.X, 0.0f, 1.0f);
            Assert.InRange(texel.Y, 0.0f, 1.0f);
        }
    }

    // Test (4): axes. Column = NoV, row = roughness: (u = 1, v = 0) is the normal-incidence smooth case, close to (1, 0); the
    // grazing smooth corner (u = 0, v = 0) has the large bias, so a transposed table would fail here.
    [Fact]
    public void AxesConvention_UIsNoVAndVIsRoughness()
    {
        Vector2 normalIncidence = Texel(BrdfLookupTable.Size - 1, 0);
        Assert.InRange(normalIncidence.X, 0.95f, 1.0f);
        Assert.InRange(normalIncidence.Y, 0.0f, 0.05f);

        Vector2 grazingSmooth = Texel(0, 0);
        Vector2 grazingRough = Texel(0, BrdfLookupTable.Size - 1);
        Vector2 normalRough = Texel(BrdfLookupTable.Size - 1, BrdfLookupTable.Size - 1);
        Assert.True(grazingSmooth.Y > 0.5f, $"grazing and smooth: bias {grazingSmooth.Y}");
        Assert.True(normalRough.X + normalRough.Y < normalIncidence.X + normalIncidence.Y, "a rough surface reflects less than a smooth one");
        Assert.True(grazingRough.X + grazingRough.Y < 1.0f);
    }

    // Test (5): the table equals an independent double-precision integration (65536 samples, written here from the formula of
    // the reference paper) at a few texels.
    [Theory]
    [InlineData(31, 31)]
    [InlineData(55, 12)]
    [InlineData(8, 50)]
    [InlineData(40, 63)]
    public void Texels_MatchADoublePrecisionIntegration(int x, int y)
    {
        double noV = NoV(x);
        double roughness = (y + 0.5) / BrdfLookupTable.Size;
        (double a, double b) = ReferenceIntegration(noV, roughness, 65536);
        Vector2 texel = Texel(x, y);

        _output.WriteLine($"NoV {noV:F4} roughness {roughness:F4}: table ({texel.X:F4}, {texel.Y:F4}), reference ({a:F4}, {b:F4})");
        Assert.InRange(texel.X, a - 0.01, a + 0.01);
        Assert.InRange(texel.Y, b - 0.01, b + 0.01);
    }

    private static (double A, double B) ReferenceIntegration(double noV, double roughness, int sampleCount)
    {
        double alpha = roughness * roughness;
        double k = alpha / 2.0;
        double vx = Math.Sqrt(1.0 - (noV * noV));
        double vz = noV;
        double a = 0.0;
        double b = 0.0;

        for (int i = 0; i < sampleCount; i++)
        {
            double xi1 = i / (double)sampleCount;
            double xi2 = VanDerCorput(i);
            double phi = 2.0 * Math.PI * xi1;
            double cosTheta = Math.Sqrt((1.0 - xi2) / (1.0 + (((alpha * alpha) - 1.0) * xi2)));
            double sinTheta = Math.Sqrt(Math.Max(0.0, 1.0 - (cosTheta * cosTheta)));
            double hx = sinTheta * Math.Cos(phi);
            double hz = cosTheta;
            double vh = (vx * hx) + (vz * hz);
            double lz = (2.0 * vh * hz) - vz;
            if (lz <= 0.0 || hz <= 0.0 || vh <= 0.0)
            {
                continue;
            }

            double g = (lz / ((lz * (1.0 - k)) + k)) * (noV / ((noV * (1.0 - k)) + k));
            double visibility = g * vh / (hz * noV);
            double fresnel = Math.Pow(1.0 - vh, 5.0);
            a += (1.0 - fresnel) * visibility;
            b += fresnel * visibility;
        }

        return (a / sampleCount, b / sampleCount);
    }

    private static double VanDerCorput(int index)
    {
        double result = 0.0;
        double fraction = 0.5;
        for (int bits = index; bits != 0; bits >>= 1)
        {
            if ((bits & 1) != 0)
            {
                result += fraction;
            }

            fraction *= 0.5;
        }

        return result;
    }
}

/// <summary>The texture of the BRDF table on a real device. Skipped where no device can be created.</summary>
[Collection(GpuDeviceCollection.Name)]
public class BrdfLookupTableGpuTests
{
    [GpuFact]
    public void Texture_HoldsTheComputedTableInTwoFloatChannels()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using Texture2D texture = BrdfLookupTable.CreateTexture(GpuDeviceHost.Instance.GraphicsDevice);

            Assert.Equal(SurfaceFormat.Vector2, texture.Format);
            Assert.Equal(BrdfLookupTable.Size, texture.Width);
            Assert.Equal(BrdfLookupTable.Size, texture.Height);
            Assert.Equal(1, texture.LevelCount);

            var back = new Vector2[BrdfLookupTable.Size * BrdfLookupTable.Size];
            texture.GetData(back);
            Vector2[] expected = BrdfLookupTable.Compute();
            for (int i = 0; i < back.Length; i += 97)
            {
                Assert.Equal(expected[i].X, back[i].X, 5);
                Assert.Equal(expected[i].Y, back[i].Y, 5);
            }
        });
    }
}
