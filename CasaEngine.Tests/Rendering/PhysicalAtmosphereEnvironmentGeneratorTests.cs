using CasaEngine.Framework.Rendering.Environment;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class PhysicalAtmosphereEnvironmentGeneratorTests
{
    [Fact]
    public void NormalizeSunDirection_ZeroVectorFallsBackToUpwardDirection()
    {
        Vector3 direction = PhysicalAtmosphereEnvironmentGenerator.NormalizeSunDirection(Vector3.Zero);

        Assert.True(direction.Y > 0.99f);
    }

    [Fact]
    public void EvaluateColor_LookingTowardSun_IsBrighterThanLookingAway()
    {
        var settings = new PhysicalAtmosphereSettings
        {
            SunDirection = Vector3.Normalize(new Vector3(0.0f, 0.9f, 0.3f)),
            SunIntensity = 6.0f,
            SunDiscSize = 0.03f,
        };

        Vector4 towardSun = PhysicalAtmosphereEnvironmentGenerator.EvaluateColor(settings.SunDirection, settings);
        Vector4 awayFromSun = PhysicalAtmosphereEnvironmentGenerator.EvaluateColor(-settings.SunDirection, settings);

        Assert.True(GetLuminance(towardSun) > GetLuminance(awayFromSun));
    }

    [Fact]
    public void EvaluateColor_BelowHorizon_BlendsTowardGroundColor()
    {
        var settings = new PhysicalAtmosphereSettings
        {
            GroundColor = Color.SaddleBrown,
            GroundFalloff = 1.0f,
        };

        Vector4 color = PhysicalAtmosphereEnvironmentGenerator.EvaluateColor(Vector3.Down, settings);
        Vector3 expected = Color.SaddleBrown.ToVector3();

        Assert.Equal(expected.X, color.X, 3);
        Assert.Equal(expected.Y, color.Y, 3);
        Assert.Equal(expected.Z, color.Z, 3);
    }

    [Fact]
    public void EvaluateColor_UpperSky_RetainsSpaceContribution()
    {
        var settings = new PhysicalAtmosphereSettings
        {
            SpaceColor = new Color(4, 8, 24),
            RayleighColor = new Color(84, 140, 255),
            AtmosphereDensity = 0.8f,
            RayleighDensity = 0.9f,
        };

        Vector4 color = PhysicalAtmosphereEnvironmentGenerator.EvaluateColor(Vector3.Up, settings);

        Assert.True(color.Z > color.X);
        Assert.True(color.Z > 0.08f);
    }

    private static float GetLuminance(Vector4 color)
        => (0.2126f * color.X) + (0.7152f * color.Y) + (0.0722f * color.Z);
}