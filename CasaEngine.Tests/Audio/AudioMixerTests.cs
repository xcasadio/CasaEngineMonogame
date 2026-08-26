using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioMixerTests
{
    private static AudioMixer CreateTwoLevelMixer(out AudioBus master, out AudioBus music)
    {
        var mixer = new AudioMixer();
        master = mixer.CreateBus("Master", null);
        music = mixer.CreateBus("Music", "Master");
        return mixer;
    }

    [Fact]
    public void NewBus_StartsAtFullVolume()
    {
        var mixer = CreateTwoLevelMixer(out var master, out var music);

        Assert.Same(master, mixer.Root);
        Assert.Equal(1f, master.EffectiveGain);
        Assert.Equal(1f, music.EffectiveGain);
    }

    [Fact]
    public void EffectiveGain_IsTheProductOfTheChain()
    {
        var mixer = CreateTwoLevelMixer(out var master, out var music);
        var sub = mixer.CreateBus("Sub", "Music");

        master.Volume = 0.5f;
        music.Volume = 0.5f;
        sub.Volume = 0.5f;

        Assert.Equal(0.5f, master.EffectiveGain);
        Assert.Equal(0.25f, music.EffectiveGain);
        Assert.Equal(0.125f, sub.EffectiveGain);
    }

    [Fact]
    public void MutingAParent_ZeroesTheChildrenWithoutTouchingTheirVolume()
    {
        CreateTwoLevelMixer(out var master, out var music);
        music.Volume = 0.8f;

        master.IsMuted = true;

        Assert.Equal(0f, master.EffectiveGain);
        Assert.Equal(0f, music.EffectiveGain);
        Assert.Equal(0.8f, music.Volume);

        master.IsMuted = false;

        Assert.Equal(0.8f, music.EffectiveGain);
    }

    [Fact]
    public void MutingABus_LeavesItsSiblingsAudible()
    {
        var mixer = CreateTwoLevelMixer(out _, out var music);
        var sfx = mixer.CreateBus("Sfx", "Master");

        music.IsMuted = true;

        Assert.Equal(0f, music.EffectiveGain);
        Assert.Equal(1f, sfx.EffectiveGain);
    }

    [Fact]
    public void Volume_IsClampedAndIgnoresNaN()
    {
        CreateTwoLevelMixer(out var master, out _);

        master.Volume = 4f;
        Assert.Equal(1f, master.Volume);

        master.Volume = -2f;
        Assert.Equal(0f, master.Volume);

        master.Volume = 0.5f;
        master.Volume = float.NaN;
        Assert.Equal(0.5f, master.Volume);
    }

    [Fact]
    public void Version_ChangesOnlyWhenTheGainsCanChange()
    {
        var mixer = CreateTwoLevelMixer(out var master, out _);
        var version = mixer.Version;

        master.Volume = 0.5f;
        Assert.NotEqual(version, mixer.Version);

        version = mixer.Version;
        master.Volume = 0.5f;
        Assert.Equal(version, mixer.Version);

        master.IsMuted = false;
        Assert.Equal(version, mixer.Version);

        master.IsMuted = true;
        Assert.NotEqual(version, mixer.Version);
    }

    [Fact]
    public void CreateBus_RejectsADuplicateName()
    {
        var mixer = CreateTwoLevelMixer(out _, out _);

        Assert.Throws<ArgumentException>(() => mixer.CreateBus("Music", "Master"));
        Assert.Throws<ArgumentException>(() => mixer.CreateBus("music", "Master"));
    }

    [Fact]
    public void CreateBus_RejectsAnUnknownParent()
    {
        var mixer = CreateTwoLevelMixer(out _, out _);

        Assert.Throws<ArgumentException>(() => mixer.CreateBus("Voice", "DoesNotExist"));
    }

    [Fact]
    public void CreateBus_RejectsASecondRoot()
    {
        var mixer = CreateTwoLevelMixer(out _, out _);

        Assert.Throws<InvalidOperationException>(() => mixer.CreateBus("OtherRoot", null));
    }

    [Fact]
    public void CreateBus_CannotBuildACycleBecauseTheParentMustAlreadyExist()
    {
        var mixer = CreateTwoLevelMixer(out _, out _);

        // A bus can only reference an already created bus and is never reparented,
        // so the graph is a tree by construction.
        Assert.Throws<ArgumentException>(() => mixer.CreateBus("Loop", "Loop"));
    }

    [Fact]
    public void GetEffectiveGain_FallsBackToTheRootForAnUnknownBus()
    {
        var mixer = CreateTwoLevelMixer(out var master, out _);
        master.Volume = 0.25f;

        Assert.Equal(0.25f, mixer.GetEffectiveGain("NotABus"));
        Assert.Equal(0.25f, mixer.GetEffectiveGain(null));
    }
}
