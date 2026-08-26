using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioBusNamesTests
{
    [Fact]
    public void DefaultMixer_HangsEveryBusFromMaster()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();

        Assert.Equal(AudioBusNames.Master, mixer.Root.Name);

        foreach (var name in new[]
                 {
                     AudioBusNames.Music,
                     AudioBusNames.Sfx,
                     AudioBusNames.Voice,
                     AudioBusNames.Ui,
                     AudioBusNames.Editor,
                 })
        {
            Assert.True(mixer.TryGetBus(name, out var bus), $"Bus '{name}' is missing.");
            Assert.Same(mixer.Root, bus.Parent);
            Assert.Equal(1f, bus.EffectiveGain);
        }
    }

    [Fact]
    public void MasterVolume_PropagatesToEveryBus()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();

        mixer.GetBus(AudioBusNames.Master).Volume = 0.5f;

        Assert.Equal(0.5f, mixer.GetEffectiveGain(AudioBusNames.Music));
        Assert.Equal(0.5f, mixer.GetEffectiveGain(AudioBusNames.Sfx));
        Assert.Equal(0.5f, mixer.GetEffectiveGain(AudioBusNames.Editor));
    }

    [Fact]
    public void MutingMaster_SilencesEverythingIncludingTheEditorBus()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();

        mixer.GetBus(AudioBusNames.Master).IsMuted = true;

        Assert.Equal(0f, mixer.GetEffectiveGain(AudioBusNames.Music));
        Assert.Equal(0f, mixer.GetEffectiveGain(AudioBusNames.Sfx));
        Assert.Equal(0f, mixer.GetEffectiveGain(AudioBusNames.Voice));
        Assert.Equal(0f, mixer.GetEffectiveGain(AudioBusNames.Ui));
        Assert.Equal(0f, mixer.GetEffectiveGain(AudioBusNames.Editor));
    }
}
