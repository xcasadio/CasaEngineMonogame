using CasaEngine.Framework.Particles.Authoring;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleRangeTests
{
    [Fact]
    public void FloatRange_NormalizesInvertedBounds()
    {
        var range = new FloatRange(5.0f, 2.0f);

        Assert.Equal(2.0f, range.Min);
        Assert.Equal(5.0f, range.Max);
    }

    [Fact]
    public void FloatRange_RejectsNonFiniteValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FloatRange(float.NaN, 1.0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FloatRange(0.0f, float.PositiveInfinity));
    }

    [Fact]
    public void Vector2Range_NormalizesEachAxisIndependently()
    {
        var range = new Vector2Range(new Vector2(4.0f, -2.0f), new Vector2(1.0f, 3.0f));

        Assert.Equal(new Vector2(1.0f, -2.0f), range.Min);
        Assert.Equal(new Vector2(4.0f, 3.0f), range.Max);
    }

    [Fact]
    public void ParticleRandom_WithSameSeed_ProducesSameSequence()
    {
        var first = new ParticleRandom(42u);
        var second = new ParticleRandom(42u);

        for (int index = 0; index < 16; index++)
        {
            Assert.Equal(first.NextUInt(), second.NextUInt());
        }
    }

    [Fact]
    public void ParticleRandom_SamplesValuesInsideRange()
    {
        var random = new ParticleRandom(123u);
        var range = new FloatRange(-2.0f, 3.0f);

        for (int index = 0; index < 64; index++)
        {
            float value = range.Sample(ref random);

            Assert.InRange(value, range.Min, range.Max);
        }
    }
}