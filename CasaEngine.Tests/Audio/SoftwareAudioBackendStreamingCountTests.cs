using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Software;
using CasaEngine.Framework.Audio.Streaming;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>Reliable pending buffer counts and guaranteed voice stops of <see cref="SoftwareAudioBackend"/> (T2.2).</summary>
public class SoftwareAudioBackendStreamingCountTests
{
    private const int Rate = 48000;

    private static SoftwareAudioBackend Create(out OfflineAudioOutput output, int capacity)
    {
        output = new OfflineAudioOutput();
        return new SoftwareAudioBackend(output, capacity);
    }

    private static byte[] Constant(int frames, short value)
    {
        var buffer = new byte[frames * 2];
        for (var i = 0; i < buffer.Length; i += 2)
        {
            BitConverter.TryWriteBytes(buffer.AsSpan(i, 2), value);
        }

        return buffer;
    }

    [Fact]
    public void QueueOverflow_BringsThePendingCountBackToZero()
    {
        using var backend = Create(out var output, 4);
        var voice = backend.CreateStreamingVoice(Rate, 1, AudioVoiceParameters.Default);
        var buffer = Constant(64, 1000);
        var capacity = SoftwareMixer.VoiceChunkQueueCapacity;
        var submitted = capacity + 6;

        for (var i = 0; i < submitted; i++)
        {
            backend.SubmitBuffer(voice, buffer, 0, buffer.Length);
        }

        Assert.Equal(submitted, backend.GetPendingBufferCount(voice));

        output.Pump(1);
        Assert.Equal(capacity, backend.GetPendingBufferCount(voice));
        Assert.Equal(6, backend.DroppedChunkCount);

        backend.Start(voice);
        output.Pump(64 * capacity + 16);

        Assert.Equal(0, backend.GetPendingBufferCount(voice));
    }

    [Fact]
    public void ReusedSlot_DoesNotInheritTheConsumedCountOfTheOldVoice()
    {
        using var backend = Create(out var output, 1);
        var first = backend.CreateStreamingVoice(Rate, 1, AudioVoiceParameters.Default);
        var buffer = Constant(100, 1000);

        for (var i = 0; i < 5; i++)
        {
            backend.SubmitBuffer(first, buffer, 0, buffer.Length);
        }

        backend.Start(first);
        output.Pump(1000);
        Assert.Equal(0, backend.GetPendingBufferCount(first));
        backend.Release(first);

        var second = backend.CreateStreamingVoice(Rate, 1, AudioVoiceParameters.Default);
        Assert.True(second.IsValid);
        Assert.Equal(first.Index, second.Index);

        // The audio thread has not applied the creation yet: the old value must be ignored.
        for (var i = 0; i < 3; i++)
        {
            backend.SubmitBuffer(second, buffer, 0, buffer.Length);
        }

        Assert.Equal(3, backend.GetPendingBufferCount(second));
        Assert.Equal(0, backend.GetPendingBufferCount(first));

        output.Pump(1);
        Assert.Equal(3, backend.GetPendingBufferCount(second));
    }

    [Fact]
    public void MusicPlay_OfALoopedTrackInAReusedSlot_ReturnsWithTheQueueFilled()
    {
        var output = new OfflineAudioOutput();
        using var backend = new SoftwareAudioBackend(output, 1);
        var provider = new FakeAudioClipProvider();
        var service = new AudioService(backend) { ClipProvider = provider };
        var wav = WavBuilder.CreatePcm16(sampleRate: 22050, channelCount: 2, sampleCount: 65536, formatChunkSize: 18);
        var asset = new SoundAsset
        {
            Name = "loop",
            AudioFileAssetId = provider.RegisterStream(wav),
            BusName = AudioBusNames.Music,
            IsStreaming = true,
            IsLooped = true,
        };

        var first = service.Music.Play(asset);
        Assert.True(first.IsValid);
        output.Pump(20000);
        service.Update(0.016f);
        service.Music.Stop(first);

        var second = service.Music.Play(asset);

        Assert.True(second.IsValid);
        Assert.Equal(MusicPlayer.DefaultQueuedBufferTarget, service.Music.GetPendingBufferCount(second));
    }

    [Fact]
    public void ADroppedStop_IsResentLater_TheVoiceFallsSilentAndTheSlotComesBack()
    {
        using var backend = Create(out var output, 1);
        var clip = new PcmAudioClip(Enumerable.Repeat((short)8000, Rate).ToArray(), Rate, 1);
        var voice = backend.Play(clip, new AudioVoiceParameters(1f, 0f, 0f, true));
        Assert.True(voice.IsValid);
        Assert.True(output.Pump(100) > 0.05f);

        // Fill the command ring (nothing pumps it), so that the Stop of Release is refused.
        for (var i = 0; i < 6000; i++)
        {
            backend.SetVolume(voice, 1f);
        }

        backend.Release(voice);

        Assert.Equal(0, backend.ActiveVoiceCount);
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));

        // Still pending: the ring is full, the slot is not given back.
        Assert.False(backend.CreateStreamingVoice(Rate, 1, AudioVoiceParameters.Default).IsValid);

        // The audio thread drains the ring (the voice still sounds, the Stop was never sent).
        Assert.True(output.Pump(100) > 0.05f);

        // The next backend call resends the Stop and frees the slot.
        var next = backend.CreateStreamingVoice(Rate, 1, AudioVoiceParameters.Default);
        Assert.True(next.IsValid);
        Assert.Equal(voice.Index, next.Index);
        Assert.Equal(0f, output.Pump(100));
    }
}
