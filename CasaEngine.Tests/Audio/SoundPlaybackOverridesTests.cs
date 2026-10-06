using CasaEngine.Framework.Audio;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class SoundPlaybackOverridesTests
{
    private static AudioVoiceParameters WithRegionAndRate()
    {
        return new AudioVoiceParameters(0.3f, 0.2f, 0.1f, false)
            .WithLoopRegion(1000, 2000)
            .WithRateMultiplier(4f);
    }

    [Fact]
    public void None_KeepsEveryField_RegionAndRateIncluded()
    {
        var parameters = WithRegionAndRate();

        Assert.Equal(parameters, SoundPlaybackOverrides.None.ApplyTo(parameters));
    }

    [Fact]
    public void AllFourOverrides_ChangeTheirFields_AndKeepRegionAndRate()
    {
        var overrides = new SoundPlaybackOverrides(volume: 0.7f, pan: -0.5f, pitch: -0.25f, isLooped: true);

        var result = overrides.ApplyTo(WithRegionAndRate());

        Assert.Equal(0.7f, result.Volume);
        Assert.Equal(-0.5f, result.Pan);
        Assert.Equal(-0.25f, result.Pitch);
        Assert.True(result.IsLooped);
        Assert.True(result.HasLoopRegion);
        Assert.Equal(1000, result.LoopStartFrame);
        Assert.Equal(2000, result.LoopEndFrame);
        Assert.Equal(4f, result.RateMultiplier);
    }

    [Fact]
    public void AVolumeOnlyOverride_KeepsEverythingElse()
    {
        var parameters = WithRegionAndRate();

        var result = new SoundPlaybackOverrides(volume: 0.9f).ApplyTo(parameters);

        Assert.Equal(parameters.WithVolume(0.9f), result);
        Assert.Equal(parameters.Pan, result.Pan);
        Assert.Equal(parameters.Pitch, result.Pitch);
        Assert.Equal(parameters.IsLooped, result.IsLooped);
        Assert.Equal(4f, result.RateMultiplier);
    }

    [Fact]
    public void None_OnTheDefault_IsTheDefault()
    {
        Assert.Equal(AudioVoiceParameters.Default, SoundPlaybackOverrides.None.ApplyTo(AudioVoiceParameters.Default));
    }

    [Fact]
    public void ANaNVolumeOverride_IsSanitisedAsBefore()
    {
        // Before O17 the four-argument constructor turned a NaN volume into the maximum volume.
        var result = new SoundPlaybackOverrides(volume: float.NaN).ApplyTo(WithRegionAndRate());

        Assert.Equal(AudioVoiceParameters.MaxVolume, result.Volume);
    }
}
