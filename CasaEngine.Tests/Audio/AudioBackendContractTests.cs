using CasaEngine.Framework.Audio;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioBackendContractTests
{
    [Fact]
    public void DefaultVoiceHandle_IsInvalid()
    {
        Assert.False(default(AudioVoiceHandle).IsValid);
        Assert.False(AudioVoiceHandle.None.IsValid);
    }

    [Fact]
    public void VoiceHandle_KeepsIndexAndGeneration()
    {
        var handle = new AudioVoiceHandle(3, 7);

        Assert.True(handle.IsValid);
        Assert.Equal(3, handle.Index);
        Assert.Equal(7, handle.Generation);
        Assert.Equal(new AudioVoiceHandle(3, 7), handle);
        Assert.NotEqual(new AudioVoiceHandle(3, 8), handle);
    }

    [Fact]
    public void VoiceParameters_ClampOutOfRangeValues()
    {
        var parameters = new AudioVoiceParameters(5f, -9f, 42f, true);

        Assert.Equal(AudioVoiceParameters.MaxVolume, parameters.Volume);
        Assert.Equal(AudioVoiceParameters.MinPan, parameters.Pan);
        Assert.Equal(AudioVoiceParameters.MaxPitch, parameters.Pitch);
        Assert.True(parameters.IsLooped);
    }

    [Fact]
    public void VoiceParameters_ReplaceNaNWithTheNeutralValue()
    {
        var parameters = new AudioVoiceParameters(float.NaN, float.NaN, float.NaN, false);

        Assert.Equal(AudioVoiceParameters.MaxVolume, parameters.Volume);
        Assert.Equal(0f, parameters.Pan);
        Assert.Equal(0f, parameters.Pitch);
    }

    [Fact]
    public void Play_ReturnsALiveVoice()
    {
        var backend = new FakeAudioBackend();
        var clip = new FakeAudioClip();

        var voice = backend.Play(clip, AudioVoiceParameters.Default);

        Assert.True(voice.IsValid);
        Assert.Equal(AudioVoiceState.Playing, backend.GetState(voice));
        Assert.Same(clip, backend.GetClip(voice));
        Assert.Equal(1, backend.ActiveVoiceCount);
    }

    [Fact]
    public void Release_InvalidatesTheHandle()
    {
        var backend = new FakeAudioBackend();
        var voice = backend.Play(new FakeAudioClip(), AudioVoiceParameters.Default);

        backend.Release(voice);

        Assert.False(backend.IsVoiceAlive(voice));
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void RecycledSlot_DoesNotAnswerToTheOldHandle()
    {
        var backend = new FakeAudioBackend();
        var firstVoice = backend.Play(new FakeAudioClip("first"), AudioVoiceParameters.Default);
        backend.Release(firstVoice);

        var secondVoice = backend.Play(new FakeAudioClip("second"), AudioVoiceParameters.Default);

        Assert.Equal(firstVoice.Index, secondVoice.Index);
        Assert.NotEqual(firstVoice, secondVoice);
        Assert.False(backend.IsVoiceAlive(firstVoice));
        Assert.True(backend.IsVoiceAlive(secondVoice));
    }

    [Fact]
    public void Play_IsRefusedWhenTheBackendIsSaturated()
    {
        var backend = new FakeAudioBackend(voiceCapacity: 2);
        var clip = new FakeAudioClip();

        Assert.True(backend.Play(clip, AudioVoiceParameters.Default).IsValid);
        Assert.True(backend.Play(clip, AudioVoiceParameters.Default).IsValid);
        var refused = backend.Play(clip, AudioVoiceParameters.Default);

        Assert.False(refused.IsValid);
        Assert.Equal(1, backend.RefusedPlayCount);
    }

    [Fact]
    public void Play_IsRefusedWhenNoDeviceIsAvailable()
    {
        var backend = new FakeAudioBackend { IsAvailable = false };

        var voice = backend.Play(new FakeAudioClip(), AudioVoiceParameters.Default);

        Assert.False(voice.IsValid);
    }

    [Fact]
    public void PauseAndResume_MoveTheVoiceState()
    {
        var backend = new FakeAudioBackend();
        var voice = backend.Play(new FakeAudioClip(), AudioVoiceParameters.Default);

        backend.Pause(voice);
        Assert.Equal(AudioVoiceState.Paused, backend.GetState(voice));

        backend.Resume(voice);
        Assert.Equal(AudioVoiceState.Playing, backend.GetState(voice));
    }

    [Fact]
    public void StopAll_ReleasesEveryVoice()
    {
        var backend = new FakeAudioBackend();
        var clip = new FakeAudioClip();
        var first = backend.Play(clip, AudioVoiceParameters.Default);
        var second = backend.Play(clip, AudioVoiceParameters.Default);

        backend.StopAll();

        Assert.Equal(0, backend.ActiveVoiceCount);
        Assert.False(backend.IsVoiceAlive(first));
        Assert.False(backend.IsVoiceAlive(second));
    }

    [Fact]
    public void StaleHandleOperations_AreIgnored()
    {
        var backend = new FakeAudioBackend();
        var voice = backend.Play(new FakeAudioClip(), AudioVoiceParameters.Default);
        backend.Release(voice);

        // None of these must throw, whatever the caller kept around.
        backend.SetVolume(voice, 0.5f);
        backend.SetParameters(voice, AudioVoiceParameters.Default);
        backend.Pause(voice);
        backend.Resume(voice);
        backend.Stop(voice);
        backend.Release(voice);

        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));
    }
}
