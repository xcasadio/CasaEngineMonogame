using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioServiceOwnershipTests
{
    private sealed class FakeOwner
    {
        public FakeOwner(string name) => Name = name;

        public string Name { get; }

        public override string ToString() => Name;
    }

    [Fact]
    public void StopVoicesOwnedBy_OnlyStopsThatOwner()
    {
        var backend = new FakeAudioBackend();
        var service = new AudioService(backend);
        var clip = new FakeAudioClip();
        var world = new FakeOwner("world");
        var otherWorld = new FakeOwner("other");

        var worldVoice = service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, world);
        var otherVoice = service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, otherWorld);
        var globalVoice = service.PlayClip(clip, AudioBusNames.Ui, AudioVoiceParameters.Default);

        service.StopVoicesOwnedBy(world);

        Assert.False(service.IsAlive(worldVoice));
        Assert.True(service.IsAlive(otherVoice));
        Assert.True(service.IsAlive(globalVoice));
        Assert.Equal(2, service.ActiveVoiceCount);
    }

    [Fact]
    public void StopVoicesOwnedBy_WithNull_OnlyStopsTheOwnerlessVoices()
    {
        var service = new AudioService(new FakeAudioBackend());
        var clip = new FakeAudioClip();
        var world = new FakeOwner("world");

        var worldVoice = service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, world);
        var globalVoice = service.PlayClip(clip, AudioBusNames.Ui, AudioVoiceParameters.Default);

        service.StopVoicesOwnedBy(null);

        Assert.True(service.IsAlive(worldVoice));
        Assert.False(service.IsAlive(globalVoice));
    }

    [Fact]
    public void StopVoicesOwnedBy_AnUnknownOwnerChangesNothing()
    {
        var service = new AudioService(new FakeAudioBackend());
        var voice = service.PlayClip(new FakeAudioClip(), AudioBusNames.Sfx, AudioVoiceParameters.Default, new FakeOwner("world"));

        service.StopVoicesOwnedBy(new FakeOwner("never used"));

        Assert.True(service.IsAlive(voice));
        Assert.Equal(1, service.ActiveVoiceCount);
    }

    [Fact]
    public void StoppedVoiceSlots_AreReusableAfterTheOwnerIsCleared()
    {
        var service = new AudioService(new FakeAudioBackend(voiceCapacity: 1));
        var clip = new FakeAudioClip();
        var world = new FakeOwner("world");

        service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, world);
        service.StopVoicesOwnedBy(world);

        var afterClear = service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, world);

        Assert.True(afterClear.IsValid);
    }
}
