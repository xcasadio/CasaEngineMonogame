using CasaEngine.Framework.Particles.Authoring;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ColorGradientTests
{
    [Fact]
    public void Evaluate_WithNoKeys_ReturnsWhite()
    {
        var gradient = new ColorGradient();

        Assert.Equal(Color.White, gradient.Evaluate(0.5f));
    }

    [Fact]
    public void AddColorKey_InsertsKeysInTimeOrder()
    {
        var gradient = new ColorGradient();
        gradient.AddColorKey(1.0f, Color.Blue);
        gradient.AddColorKey(0.0f, Color.Red);
        gradient.AddColorKey(0.5f, Color.Green);

        Assert.Equal(0.0f, gradient.ColorKeys[0].Time);
        Assert.Equal(0.5f, gradient.ColorKeys[1].Time);
        Assert.Equal(1.0f, gradient.ColorKeys[2].Time);
    }

    [Fact]
    public void Evaluate_InterpolatesColorAndAlphaLinearly()
    {
        var gradient = new ColorGradient();
        gradient.AddColorKey(0.0f, new Color(0, 0, 0));
        gradient.AddColorKey(1.0f, new Color(100, 200, 50));
        gradient.AddAlphaKey(0.0f, 0.0f);
        gradient.AddAlphaKey(1.0f, 1.0f);

        Color color = gradient.Evaluate(0.5f);

        Assert.Equal(new Color(50, 100, 25, 128), color);
    }

    [Fact]
    public void Evaluate_ClampsOutsideNormalizedTime()
    {
        var gradient = new ColorGradient();
        gradient.AddColorKey(0.25f, Color.Red);
        gradient.AddColorKey(0.75f, Color.Blue);

        Assert.Equal(Color.Red, gradient.Evaluate(-1.0f));
        Assert.Equal(Color.Blue, gradient.Evaluate(2.0f));
    }

    [Fact]
    public void AlphaKey_ClampsAlphaToNormalizedRange()
    {
        var low = new AlphaGradientKey(0.0f, -5.0f);
        var high = new AlphaGradientKey(1.0f, 5.0f);

        Assert.Equal(0.0f, low.Alpha);
        Assert.Equal(1.0f, high.Alpha);
    }
}