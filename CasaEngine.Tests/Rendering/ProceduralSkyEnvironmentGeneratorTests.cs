using CasaEngine.Framework.Rendering.Environment;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class ProceduralSkyEnvironmentGeneratorTests
{
    [Fact]
    public void EvaluateColor_ReturnsZenithColorForUpDirection()
    {
        var settings = new ProceduralSkySettings
        {
            ZenithColor = Color.CornflowerBlue,
            HorizonColor = Color.White,
            GroundColor = Color.Black,
            SkyExponent = 1.0f,
            GroundExponent = 1.0f,
        };

        Vector4 color = ProceduralSkyEnvironmentGenerator.EvaluateColor(Vector3.Up, settings);

        Vector3 expected = Color.CornflowerBlue.ToVector3();
        Assert.Equal(expected.X, color.X, 3);
        Assert.Equal(expected.Y, color.Y, 3);
        Assert.Equal(expected.Z, color.Z, 3);
    }

    [Fact]
    public void EvaluateColor_ReturnsGroundColorForDownDirection()
    {
        var settings = new ProceduralSkySettings
        {
            ZenithColor = Color.Blue,
            HorizonColor = Color.White,
            GroundColor = Color.SaddleBrown,
            SkyExponent = 1.0f,
            GroundExponent = 1.0f,
        };

        Vector4 color = ProceduralSkyEnvironmentGenerator.EvaluateColor(Vector3.Down, settings);

        Vector3 expected = Color.SaddleBrown.ToVector3();
        Assert.Equal(expected.X, color.X, 3);
        Assert.Equal(expected.Y, color.Y, 3);
        Assert.Equal(expected.Z, color.Z, 3);
    }

    [Fact]
    public void EvaluateColor_ReturnsHorizonColorForFlatDirection()
    {
        var settings = new ProceduralSkySettings
        {
            ZenithColor = Color.Blue,
            HorizonColor = Color.BlanchedAlmond,
            GroundColor = Color.Brown,
            SkyExponent = 1.0f,
            GroundExponent = 1.0f,
        };

        Vector4 color = ProceduralSkyEnvironmentGenerator.EvaluateColor(Vector3.Forward, settings);

        Vector3 expected = Color.BlanchedAlmond.ToVector3();
        Assert.Equal(expected.X, color.X, 3);
        Assert.Equal(expected.Y, color.Y, 3);
        Assert.Equal(expected.Z, color.Z, 3);
    }
}