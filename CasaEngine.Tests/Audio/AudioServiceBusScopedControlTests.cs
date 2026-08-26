using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// Covers the rule the editor relies on: ending or pausing a play session must not touch the
/// preview, which lives on the Editor bus.
/// </summary>
public class AudioServiceBusScopedControlTests
{
    private static AudioService CreateService(out FakeAudioBackend backend)
    {
        backend = new FakeAudioBackend();
        return new AudioService(backend);
    }

    private static AudioVoiceHandle PlayOn(AudioService service, string busName)
    {
        return service.PlayClip(new FakeAudioClip(), busName, AudioVoiceParameters.Default);
    }

    [Fact]
    public void StopAllExceptBus_SparesThePreservedBus()
    {
        var service = CreateService(out _);
        var gameVoice = PlayOn(service, AudioBusNames.Sfx);
        var musicVoice = PlayOn(service, AudioBusNames.Music);
        var editorVoice = PlayOn(service, AudioBusNames.Editor);

        service.StopAllExceptBus(AudioBusNames.Editor);

        Assert.False(service.IsAlive(gameVoice));
        Assert.False(service.IsAlive(musicVoice));
        Assert.True(service.IsAlive(editorVoice));
        Assert.Equal(1, service.ActiveVoiceCount);
    }

    [Fact]
    public void StopAllExceptBus_IsCaseInsensitive()
    {
        var service = CreateService(out _);
        var editorVoice = PlayOn(service, "editor");

        service.StopAllExceptBus(AudioBusNames.Editor);

        Assert.True(service.IsAlive(editorVoice));
    }

    [Fact]
    public void PauseAllExceptBus_LeavesThePreservedBusPlaying()
    {
        var service = CreateService(out _);
        var gameVoice = PlayOn(service, AudioBusNames.Sfx);
        var editorVoice = PlayOn(service, AudioBusNames.Editor);

        service.PauseAllExceptBus(AudioBusNames.Editor);

        Assert.False(service.IsPlaying(gameVoice));
        Assert.True(service.IsAlive(gameVoice));
        Assert.True(service.IsPlaying(editorVoice));
    }

    [Fact]
    public void ResumeAllExceptBus_RestartsWhatWasPaused()
    {
        var service = CreateService(out _);
        var gameVoice = PlayOn(service, AudioBusNames.Sfx);

        service.PauseAllExceptBus(AudioBusNames.Editor);
        service.ResumeAllExceptBus(AudioBusNames.Editor);

        Assert.True(service.IsPlaying(gameVoice));
    }

    [Fact]
    public void ResumeAllExceptBus_DoesNotRestartAVoiceTheGamePausedItself()
    {
        var service = CreateService(out _);
        var gameplayPaused = PlayOn(service, AudioBusNames.Sfx);
        var sessionPaused = PlayOn(service, AudioBusNames.Music);

        // Gameplay paused this one on its own before the session pause.
        service.Pause(gameplayPaused);
        service.PauseAllExceptBus(AudioBusNames.Editor);
        service.ResumeAllExceptBus(AudioBusNames.Editor);

        Assert.False(service.IsPlaying(gameplayPaused));
        Assert.True(service.IsPlaying(sessionPaused));
    }

    [Fact]
    public void StreamingVoices_AreAlsoPausedAndResumed()
    {
        var service = CreateService(out _);
        var stream = service.PlayStream(22050, 2, AudioBusNames.Music, AudioVoiceParameters.Default);
        service.StartVoice(stream);

        service.PauseAllExceptBus(AudioBusNames.Editor);
        Assert.False(service.IsPlaying(stream));

        service.ResumeAllExceptBus(AudioBusNames.Editor);
        Assert.True(service.IsPlaying(stream));
    }

    [Fact]
    public void StopAllExceptBus_WithANullBus_StopsEverything()
    {
        var service = CreateService(out _);
        PlayOn(service, AudioBusNames.Sfx);
        PlayOn(service, AudioBusNames.Editor);

        service.StopAllExceptBus(null);

        Assert.Equal(0, service.ActiveVoiceCount);
    }
}
