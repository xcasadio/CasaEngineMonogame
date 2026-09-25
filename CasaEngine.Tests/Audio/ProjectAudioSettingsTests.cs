using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Configuration.Project;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>Covers <see cref="ProjectAudioSettings"/> (ADR-0040) against a plain <see cref="AudioMixer"/>
/// and, for the live-voice case, an <see cref="AudioService"/> backed by <see cref="FakeAudioBackend"/>.</summary>
public class ProjectAudioSettingsTests
{
    [Fact]
    public void Apply_WithMutedProject_MutesMasterBus()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        var projectSettings = new ProjectSettings { IsAudioMuted = true };

        ProjectAudioSettings.Apply(mixer, projectSettings);

        Assert.True(mixer.GetBus(AudioBusNames.Master).IsMuted);
    }

    [Fact]
    public void Apply_WithMutedProject_SilencesALiveVoiceAfterUpdate()
    {
        var backend = new FakeAudioBackend();
        var service = new AudioService(backend);
        var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);
        var projectSettings = new ProjectSettings { IsAudioMuted = true };

        ProjectAudioSettings.Apply(service.Mixer, projectSettings);
        service.Update(0.016f);

        Assert.Equal(0f, backend.GetParameters(voice).Volume, 4);
    }

    [Fact]
    public void Apply_WithUnmutedProject_RestoresALiveVoiceVolumeAfterUpdate()
    {
        var backend = new FakeAudioBackend();
        var service = new AudioService(backend);
        var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default);

        ProjectAudioSettings.Apply(service.Mixer, new ProjectSettings { IsAudioMuted = true });
        service.Update(0.016f);
        Assert.Equal(0f, backend.GetParameters(voice).Volume, 4);

        ProjectAudioSettings.Apply(service.Mixer, new ProjectSettings { IsAudioMuted = false });
        service.Update(0.016f);

        Assert.Equal(1f, backend.GetParameters(voice).Volume, 4);
    }

    [Fact]
    public void SetMuted_WithDifferentInstances_UpdatesBoth()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        var runtimeSettings = new ProjectSettings();
        var globalSettings = new ProjectSettings();

        ProjectAudioSettings.SetMuted(mixer, true, runtimeSettings, globalSettings);

        Assert.True(mixer.GetBus(AudioBusNames.Master).IsMuted);
        Assert.True(runtimeSettings.IsAudioMuted);
        Assert.True(globalSettings.IsAudioMuted);
    }

    [Fact]
    public void SetMuted_WithTheSameInstance_UpdatesItOnce()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        var settings = new ProjectSettings();

        ProjectAudioSettings.SetMuted(mixer, true, settings, settings);

        Assert.True(mixer.GetBus(AudioBusNames.Master).IsMuted);
        Assert.True(settings.IsAudioMuted);
    }

    [Fact]
    public void SetMuted_WithFalse_UnmutesAndMirrorsFalse()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        var runtimeSettings = new ProjectSettings { IsAudioMuted = true };
        var globalSettings = new ProjectSettings { IsAudioMuted = true };
        mixer.GetBus(AudioBusNames.Master).IsMuted = true;

        ProjectAudioSettings.SetMuted(mixer, false, runtimeSettings, globalSettings);

        Assert.False(mixer.GetBus(AudioBusNames.Master).IsMuted);
        Assert.False(runtimeSettings.IsAudioMuted);
        Assert.False(globalSettings.IsAudioMuted);
    }
}
