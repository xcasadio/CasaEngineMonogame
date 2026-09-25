using System.Buffers.Binary;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// Covers <see cref="AudioService.PlayClipStereo"/>, <see cref="AudioService.SetVoiceStereoGains"/>
/// and <see cref="AudioService.GetVoiceStereoGains"/>: the software stereo voice path of
/// ADR-0039, verified against <see cref="FakeAudioBackend"/> exactly like the rest of the
/// streaming voices.
/// </summary>
public class AudioServiceStereoVoiceTests
{
    private static AudioService CreateService(out FakeAudioBackend backend)
    {
        backend = new FakeAudioBackend { RecordSubmittedBuffers = true };
        return new AudioService(backend);
    }

    private static (short Left, short Right) ReadFrame(byte[] buffer, int frameIndex)
    {
        var offset = frameIndex * 4;
        var left = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(offset, 2));
        var right = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(offset + 2, 2));
        return (left, right);
    }

    [Fact]
    public void PlayClipStereo_AppliesAsymmetricGainsToEachChannel()
    {
        var service = CreateService(out var backend);
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: new short[] { 1000 });

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 0.5f, 0.25f);

        Assert.True(voice.IsValid);
        var buffer = Assert.Single(backend.GetSubmittedBuffers(voice));
        var frame = ReadFrame(buffer, 0);
        Assert.Equal(500, frame.Left);
        Assert.Equal(250, frame.Right);
    }

    [Fact]
    public void PlayClipStereo_WithZeroGains_ProducesSilenceRegardlessOfTheSample()
    {
        var service = CreateService(out var backend);
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: new short[] { 12345 });

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 0f, 0f);

        var buffer = Assert.Single(backend.GetSubmittedBuffers(voice));
        var frame = ReadFrame(buffer, 0);
        Assert.Equal(0, frame.Left);
        Assert.Equal(0, frame.Right);
    }

    [Fact]
    public void PlayClipStereo_RoundsAwayFromZeroAndKeepsTheShortRange()
    {
        var service = CreateService(out var backend);
        // 3 * 0.5 = 1.5, -3 * 0.5 = -1.5: exactly the halfway case AwayFromZero must not
        // round to even. 32767 * 1.0 sits at the extreme of the range without needing a clamp.
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: new short[] { 3, -3, 32767, -32768 });

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);

        var buffer = Assert.Single(backend.GetSubmittedBuffers(voice));
        Assert.Equal((short)3, ReadFrame(buffer, 0).Left);
        Assert.Equal((short)-3, ReadFrame(buffer, 1).Left);
        Assert.Equal((short)32767, ReadFrame(buffer, 2).Left);
        Assert.Equal((short)-32768, ReadFrame(buffer, 3).Left);
    }

    [Fact]
    public void SetVoiceStereoGains_OnlyAffectsBuffersFilledAfterTheChange()
    {
        var service = CreateService(out var backend);
        var samples = new short[2400];
        Array.Fill(samples, (short)1000);
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: samples);
        var loopedParameters = new AudioVoiceParameters(1f, 0f, 0f, true);

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, loopedParameters, 1f, 0f);
        Assert.Equal(3, backend.GetSubmittedBuffers(voice).Count);

        service.SetVoiceStereoGains(voice, 0f, 1f);
        backend.ConsumeBuffers(voice, 3);
        service.Update(0.016f);

        var buffers = backend.GetSubmittedBuffers(voice);
        Assert.Equal(6, buffers.Count);

        for (var i = 0; i < 3; i++)
        {
            var frame = ReadFrame(buffers[i], 0);
            Assert.Equal(1000, frame.Left);
            Assert.Equal(0, frame.Right);
        }

        for (var i = 3; i < 6; i++)
        {
            var frame = ReadFrame(buffers[i], 0);
            Assert.Equal(0, frame.Left);
            Assert.Equal(1000, frame.Right);
        }
    }

    [Fact]
    public void GetVoiceStereoGains_ReturnsTheSanitizedGains()
    {
        var service = CreateService(out _);
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: new short[] { 1000 });

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, float.NaN, 5f);

        Assert.True(service.GetVoiceStereoGains(voice, out var left, out var right));
        Assert.Equal(1f, left);
        Assert.Equal(1f, right);

        service.SetVoiceStereoGains(voice, -1f, float.NaN);
        Assert.True(service.GetVoiceStereoGains(voice, out left, out right));
        Assert.Equal(0f, left);
        Assert.Equal(1f, right);
    }

    [Fact]
    public void GetVoiceStereoGains_OnAStaleHandle_ReturnsFalseAndZero()
    {
        var service = CreateService(out _);

        Assert.False(service.GetVoiceStereoGains(AudioVoiceHandle.None, out var left, out var right));
        Assert.Equal(0f, left);
        Assert.Equal(0f, right);
    }

    [Fact]
    public void AStereoVoice_GoesThroughItsBusLikeAnyOtherVoice_ButTheSamplesStayExact()
    {
        var service = CreateService(out var backend);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: new short[] { 1000 });

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);

        Assert.Equal(0.5f, backend.GetParameters(voice).Volume, 4);

        var buffer = Assert.Single(backend.GetSubmittedBuffers(voice));
        var frame = ReadFrame(buffer, 0);
        Assert.Equal(1000, frame.Left);
        Assert.Equal(1000, frame.Right);
    }

    [Fact]
    public void ALoopingStereoVoice_KeepsFeedingPastTheEndOfTheClip()
    {
        var service = CreateService(out var backend);
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: new short[] { 100, -100, 200, -200 });
        var loopedParameters = new AudioVoiceParameters(1f, 0f, 0f, true);

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, loopedParameters, 1f, 1f);

        for (var i = 0; i < 50; i++)
        {
            backend.ConsumeBuffers(voice, 3);
            service.Update(0.016f);
        }

        Assert.True(service.IsAlive(voice));
        // 4 samples * 4 bytes/frame = 16 bytes of unique content; far more went out, so it looped.
        Assert.True(backend.GetSubmittedBytes(voice) > 16);
    }

    [Fact]
    public void ANonLoopedStereoVoice_IsStoppedAndReleasedOnceItsBufferIsConsumed()
    {
        var service = CreateService(out var backend);
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: new short[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);
        Assert.True(service.IsAlive(voice));

        backend.ConsumeBuffers(voice, 10);
        service.Update(0.016f);

        Assert.False(service.IsAlive(voice));
    }

    [Fact]
    public void AStereoVoice_IsScopedToItsOwner_AndTheFeederForgetsItOnceStopped()
    {
        var service = CreateService(out _);
        var owner = new object();
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: new short[] { 1000 });
        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f, owner);

        service.StopVoicesOwnedBy(owner);
        Assert.False(service.IsAlive(voice));

        // The feeder only notices on the next Update.
        service.Update(0.016f);
        Assert.False(service.GetVoiceStereoGains(voice, out _, out _));
    }

    [Fact]
    public void Update_NeverSubmitsMoreThanThreeBuffersPerVoice_EvenIfTheBackendNeverReportsItsQueue()
    {
        var service = CreateService(out var backend);
        backend.ReportsZeroPendingBuffers = true;
        var samples = new short[100000];
        Array.Fill(samples, (short)500);
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: samples);
        var loopedParameters = new AudioVoiceParameters(1f, 0f, 0f, true);

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, loopedParameters, 1f, 1f);
        Assert.Equal(3, backend.GetSubmittedBuffers(voice).Count);

        service.Update(0.016f);
        Assert.Equal(6, backend.GetSubmittedBuffers(voice).Count);

        service.Update(0.016f);
        Assert.Equal(9, backend.GetSubmittedBuffers(voice).Count);
    }

    [Fact]
    public void PlayClipStereo_IsRefusedWhenTheClipHasNoSamples()
    {
        var service = CreateService(out _);
        var clip = new FakeAudioClip(sampleRate: 24000);

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);

        Assert.False(voice.IsValid);
    }

    [Fact]
    public void PlayClipStereo_IsRefusedWhenTheBackendCannotStream()
    {
        var backend = new FakeAudioBackend { SupportsStreaming = false };
        var service = new AudioService(backend);
        var clip = new FakeAudioClip(sampleRate: 24000, monoSamples: new short[] { 1000 });

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);

        Assert.False(voice.IsValid);
    }

    [Fact]
    public void PlayClipStereo_BelowEightKilohertz_ResamplesByLinearInterpolation()
    {
        var service = CreateService(out var backend);
        // 3370 * 3 = 10110 >= 8000, 3370 * 2 = 6740 < 8000: the smallest factor is 3.
        var clip = new FakeAudioClip(sampleRate: 3370, monoSamples: new short[] { 0, 3000 });

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);

        Assert.Equal(10110, backend.GetSampleRate(voice));
        var buffer = Assert.Single(backend.GetSubmittedBuffers(voice));

        // Between the 2 samples: 3 interpolated frames (0, 1000, 2000), then the last sample
        // repeats flat for 3 more frames since the clip does not loop.
        Assert.Equal(6, buffer.Length / 4);
        Assert.Equal(0, ReadFrame(buffer, 0).Left);
        Assert.Equal(1000, ReadFrame(buffer, 1).Left);
        Assert.Equal(2000, ReadFrame(buffer, 2).Left);
        Assert.Equal(3000, ReadFrame(buffer, 3).Left);
        Assert.Equal(3000, ReadFrame(buffer, 4).Left);
        Assert.Equal(3000, ReadFrame(buffer, 5).Left);
    }

    [Fact]
    public void PlayClipStereo_WhenResampling_RoundsOnlyOnceAfterTheGain()
    {
        var service = CreateService(out var backend);
        // 4000 * 2 = 8000: factor 2, so the second frame is the midpoint 1.5 between 0 and 3.
        var clip = new FakeAudioClip(sampleRate: 4000, monoSamples: new short[] { 0, 3 });

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 0.3f, 1f);

        var buffer = Assert.Single(backend.GetSubmittedBuffers(voice));

        // 1.5 * 0.3 = 0.45 rounds to 0. Rounding the interpolated value first would give
        // round(1.5) * 0.3 = 0.6, which rounds to 1.
        Assert.Equal(0, ReadFrame(buffer, 1).Left);
        Assert.Equal(2, ReadFrame(buffer, 1).Right);
    }

    [Fact]
    public void PlayClipStereo_Above48Kilohertz_ResamplesByAveragingBlocks()
    {
        var service = CreateService(out var backend);
        // 172610 / 4 = 43152.5 <= 48000, 172610 / 3 = 57536.67 > 48000: the smallest factor is 4.
        var clip = new FakeAudioClip(
            sampleRate: 172610,
            monoSamples: new short[] { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000 });

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);

        Assert.Equal(43152, backend.GetSampleRate(voice));
        var buffer = Assert.Single(backend.GetSubmittedBuffers(voice));

        // Groups of 4, the last one partial (2 samples).
        Assert.Equal(3, buffer.Length / 4);
        Assert.Equal(250, ReadFrame(buffer, 0).Left);
        Assert.Equal(650, ReadFrame(buffer, 1).Left);
        Assert.Equal(950, ReadFrame(buffer, 2).Left);
    }
}
