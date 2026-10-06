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

    [Fact]
    public void ResolvePriority_NullKeepsTheAssetValue()
    {
        Assert.Equal(7, SoundPlaybackOverrides.None.ResolvePriority(7));
        Assert.Equal(0, SoundPlaybackOverrides.None.ResolvePriority(0));
        Assert.Equal(SoundAsset.MaxPriority, new SoundPlaybackOverrides(volume: 0.5f).ResolvePriority(500));
    }

    [Fact]
    public void ResolvePriority_ZeroRemovesAnyPriority()
    {
        var overrides = new SoundPlaybackOverrides(busName: "Sfx") { Priority = 0 };

        Assert.Equal(0, overrides.ResolvePriority(50));
    }

    [Fact]
    public void ResolvePriority_ClampsTheOverride()
    {
        Assert.Equal(SoundAsset.MaxPriority, new SoundPlaybackOverrides { Priority = 500 }.ResolvePriority(1));
        Assert.Equal(0, new SoundPlaybackOverrides { Priority = -3 }.ResolvePriority(40));
        Assert.Equal(9, new SoundPlaybackOverrides { Priority = 9 }.ResolvePriority(0));
    }

    [Fact]
    public void ThePriorityInitializer_DoesNotTouchTheOtherFields()
    {
        var overrides = new SoundPlaybackOverrides(volume: 0.4f, busName: "Ui") { Priority = 3 };

        Assert.Equal(3, overrides.Priority);
        Assert.Equal(0.4f, overrides.Volume);
        Assert.Equal("Ui", overrides.ResolveBus("Sfx"));
        Assert.Null(SoundPlaybackOverrides.None.Priority);
    }
}
