using CasaEngine.Framework.Audio;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioVoiceParametersTests
{
    [Fact]
    public void Default_HasNoRegionAndAUnitMultiplier()
    {
        var parameters = AudioVoiceParameters.Default;

        Assert.False(parameters.HasLoopRegion);
        Assert.Equal(0, parameters.LoopStartFrame);
        Assert.Equal(0, parameters.LoopEndFrame);
        Assert.Equal(1f, parameters.RateMultiplier);
    }

    [Fact]
    public void DefaultStruct_HasAUnitMultiplier()
    {
        Assert.Equal(1f, default(AudioVoiceParameters).RateMultiplier);
    }

    [Fact]
    public void FourArgumentConstructor_EqualsAndHashesLikeDefaultOnTheOldFields()
    {
        var a = new AudioVoiceParameters(1f, 0f, 0f, false);

        Assert.Equal(AudioVoiceParameters.Default, a);
        Assert.Equal(AudioVoiceParameters.Default.GetHashCode(), a.GetHashCode());
        Assert.Equal(a, default(AudioVoiceParameters).WithVolume(1f));
    }

    [Fact]
    public void RegionAndMultiplier_ParticipateInEqualityAndHash()
    {
        var plain = new AudioVoiceParameters(0.5f, 0.1f, 0.2f, true);
        var region = plain.WithLoopRegion(10, 20);
        var rate = plain.WithRateMultiplier(2f);

        Assert.NotEqual(plain, region);
        Assert.NotEqual(plain, rate);
        Assert.NotEqual(region, plain.WithLoopRegion(10, 21));
        Assert.Equal(region, plain.WithLoopRegion(10, 20));
        Assert.Equal(region.GetHashCode(), plain.WithLoopRegion(10, 20).GetHashCode());
        Assert.Equal(plain, region.WithoutLoopRegion());
        Assert.Contains("Region:[10,20)", region.ToString());
        Assert.Contains("Rate:x2", rate.ToString());
    }

    [Fact]
    public void ToString_WithoutTheNewOptions_IsUnchanged()
    {
        var parameters = new AudioVoiceParameters(0.5f, -0.25f, 0.5f, true);

        Assert.Equal($"Volume:{0.5f} Pan:{-0.25f} Pitch:{0.5f} Looped:True", parameters.ToString());
    }

    [Fact]
    public void EveryWithMethod_PreservesTheRegionAndTheMultiplier()
    {
        var source = new AudioVoiceParameters(0.5f, 0.1f, 0.2f, true).WithLoopRegion(1000, 2000).WithRateMultiplier(4f);

        var results = new[]
        {
            source.WithVolume(0.3f),
            source.WithPan(-0.4f),
            source.WithPitch(-0.5f),
            source.WithLooping(false),
            source.WithLooping(true),
        };

        foreach (var result in results)
        {
            Assert.True(result.HasLoopRegion);
            Assert.Equal(1000, result.LoopStartFrame);
            Assert.Equal(2000, result.LoopEndFrame);
            Assert.Equal(4f, result.RateMultiplier);
        }

        Assert.Equal(0.3f, results[0].Volume);
        Assert.Equal(-0.4f, results[1].Pan);
        Assert.Equal(-0.5f, results[2].Pitch);
        Assert.False(results[3].IsLooped);
    }

    [Fact]
    public void RegionAndMultiplierSetters_PreserveTheOtherFields()
    {
        var source = new AudioVoiceParameters(0.5f, 0.1f, 0.2f, true).WithRateMultiplier(3f);
        var withRegion = source.WithLoopRegion(5, 9);

        Assert.Equal(3f, withRegion.RateMultiplier);
        Assert.Equal(0.5f, withRegion.Volume);
        Assert.True(withRegion.IsLooped);

        var withRate = withRegion.WithRateMultiplier(2f);
        Assert.True(withRate.HasLoopRegion);
        Assert.Equal(5, withRate.LoopStartFrame);
        Assert.Equal(9, withRate.LoopEndFrame);
        Assert.Equal(0.2f, withRate.Pitch);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(0f)]
    [InlineData(-2f)]
    [InlineData(16.01f)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void RateMultiplier_OutOfRangeBecomesOne(float value)
    {
        Assert.Equal(1f, AudioVoiceParameters.Default.WithRateMultiplier(value).RateMultiplier);
    }

    [Theory]
    [InlineData(0.01f)]
    [InlineData(1f)]
    [InlineData(16f)]
    public void RateMultiplier_InRangeIsKept(float value)
    {
        Assert.Equal(value, AudioVoiceParameters.Default.WithRateMultiplier(value).RateMultiplier);
    }

    [Fact]
    public void PitchClamp_IsUnchanged()
    {
        Assert.Equal(1f, AudioVoiceParameters.Default.WithPitch(5f).Pitch);
        Assert.Equal(-1f, AudioVoiceParameters.Default.WithPitch(-5f).Pitch);
    }
}
