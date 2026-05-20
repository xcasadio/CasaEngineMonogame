using CasaEngine.Framework.Particles.Authoring;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class FloatCurveTests
{
    [Fact]
    public void Evaluate_WithNoKeys_ReturnsZero()
    {
        var curve = new FloatCurve();

        Assert.Equal(0.0f, curve.Evaluate(0.5f));
    }

    [Fact]
    public void AddKey_InsertsKeysInTimeOrder()
    {
        var curve = new FloatCurve();
        curve.AddKey(1.0f, 3.0f);
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(0.5f, 2.0f);

        Assert.Equal(0.0f, curve.Keys[0].Time);
        Assert.Equal(0.5f, curve.Keys[1].Time);
        Assert.Equal(1.0f, curve.Keys[2].Time);
    }

    [Fact]
    public void Evaluate_InterpolatesLinearlyBetweenKeys()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.0f, 2.0f);
        curve.AddKey(1.0f, 6.0f);

        Assert.Equal(4.0f, curve.Evaluate(0.5f));
    }

    [Fact]
    public void Evaluate_ClampsOutsideNormalizedTime()
    {
        var curve = new FloatCurve();
        curve.AddKey(0.25f, 2.0f);
        curve.AddKey(0.75f, 6.0f);

        Assert.Equal(2.0f, curve.Evaluate(-1.0f));
        Assert.Equal(6.0f, curve.Evaluate(2.0f));
    }

    [Fact]
    public void Presets_ReturnExpectedValues()
    {
        Assert.Equal(3.0f, FloatCurve.Constant(3.0f).Evaluate(0.35f));
        Assert.Equal(0.5f, FloatCurve.FadeIn().Evaluate(0.5f));
        Assert.Equal(0.5f, FloatCurve.FadeOut().Evaluate(0.5f));
        Assert.Equal(1.0f, FloatCurve.Bell().Evaluate(0.5f));
        Assert.Equal(1.0f, FloatCurve.Pulse().Evaluate(0.5f));
    }
}