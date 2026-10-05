using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Output;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// <see cref="AudioService.PlayClipStereo"/> on <see cref="SoftwareAudioBackend"/>: the stereo voice
/// is mixed on the audio thread through <see cref="IStereoVoiceBackend"/> (ADR-0056), driven here
/// block by block through an offline output.
/// </summary>
public class SoftwareStereoVoiceTests
{
    private const int Rate = 48000;
    private const int Block = 480;
    private const float Tolerance = 1e-6f;

    /// <summary>Offline output that keeps the last rendered block so a test can read each channel.</summary>
    private sealed class CapturingOutput : IAudioOutput
    {
        private AudioRenderCallback _callback;
        private readonly float[] _buffer = new float[Block * 2];

        public bool IsAvailable { get; private set; }

        public int SampleRate => Rate;

        public int BufferFrames => Block;

        public int BufferCount => 4;

        public int UnderrunCount => 0;

        public bool TryOpen()
        {
            IsAvailable = true;
            return true;
        }

        public void Start(AudioRenderCallback callback)
        {
            _callback = callback;
        }

        public float[] Pump()
        {
            _callback(_buffer, Block);
            return _buffer;
        }

        public void Dispose()
        {
            IsAvailable = false;
        }
    }

    private static PcmAudioClip Constant(short value, int frames, int rate = Rate)
    {
        var samples = new short[frames];
        Array.Fill(samples, value);
        return new PcmAudioClip(samples, rate, 1);
    }

    private static AudioService Create(out CapturingOutput output, out SoftwareAudioBackend backend, int capacity = 8)
    {
        output = new CapturingOutput();
        backend = new SoftwareAudioBackend(output, capacity);
        return new AudioService(backend);
    }

    private static void AssertBlock(float[] block, float left, float right)
    {
        for (var i = 0; i < Block; i++)
        {
            Assert.InRange(block[i * 2], left - Tolerance, left + Tolerance);
            Assert.InRange(block[i * 2 + 1], right - Tolerance, right + Tolerance);
        }
    }

    private static float[] PumpTwice(CapturingOutput output)
    {
        // The first block after a start or a change carries the gain ramp.
        output.Pump();
        return output.Pump();
    }

    [Fact]
    public void BackendCapability_IsDetected()
    {
        using var backend = new SoftwareAudioBackend(new CapturingOutput());

        Assert.IsAssignableFrom<IStereoVoiceBackend>(backend);
    }

    [Fact]
    public void PlayClipStereo_WithAClipThatIsNotPcm_KeepsTheStreamingPath()
    {
        // ADR-0056: the capability plays PcmAudioClip only; another clip exposing its samples must
        // still play through the game-thread streaming path, as before, instead of throwing.
        var service = Create(out var output, out var backend);
        var samples = new short[4410];
        Array.Fill(samples, (short)8000);
        var clip = new FakeAudioClip(sampleRate: 22050, channelCount: 1, durationSeconds: 0.2, monoSamples: samples);

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 0.5f);

        Assert.True(voice.IsValid);
        Assert.Equal(1, backend.ActiveVoiceCount);

        service.Update(0.016f);
        var block = PumpTwice(output);
        var hasSignal = false;
        for (var i = 0; i < block.Length; i++)
        {
            if (block[i] != 0f)
            {
                hasSignal = true;
                break;
            }
        }

        Assert.True(hasSignal);
    }

    [Fact]
    public void PlayClipStereo_MixesExactGainsPerChannel()
    {
        var service = Create(out var output, out _);
        var voice = service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 0.6f, 0.2f);

        Assert.True(voice.IsValid);
        Assert.True(service.GetVoiceStereoGains(voice, out var left, out var right));
        Assert.Equal(0.6f, left);
        Assert.Equal(0.2f, right);
        AssertBlock(PumpTwice(output), 0.3f, 0.1f);
    }

    [Fact]
    public void PlayClipStereo_IgnoresPanAndPitch_AndAppliesVolume()
    {
        var service = Create(out var output, out _);
        var parameters = new AudioVoiceParameters(0.5f, 1f, 1f, false);
        service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, parameters, 1f, 0.5f);

        AssertBlock(PumpTwice(output), 0.25f, 0.125f);
    }

    [Fact]
    public void SetVoiceStereoGains_IsVisibleAtTheNextBlock()
    {
        var service = Create(out var output, out _);
        var voice = service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);
        PumpTwice(output);

        service.SetVoiceStereoGains(voice, 0.2f, 0.8f);
        output.Pump(); // ramp
        AssertBlock(output.Pump(), 0.1f, 0.4f);

        Assert.True(service.GetVoiceStereoGains(voice, out var left, out var right));
        Assert.Equal(0.2f, left);
        Assert.Equal(0.8f, right);
    }

    [Fact]
    public void SetVoiceStereoGains_RampsAcrossTheNextBlock()
    {
        var service = Create(out var output, out _);
        var voice = service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);
        PumpTwice(output);

        service.SetVoiceStereoGains(voice, 0f, 1f);
        var block = output.Pump();

        Assert.True(block[0] > block[(Block - 1) * 2]);
        Assert.InRange(block[(Block - 1) * 2], -1e-4f, 1e-4f); // end of a linear ramp, float accumulation
        Assert.InRange(block[(Block - 1) * 2 + 1], 0.5f - 1e-4f, 0.5f + 1e-4f);
    }

    [Fact]
    public void SetVoiceStereoGains_SanitizesLikeThePublicContract()
    {
        var service = Create(out var output, out _);
        var voice = service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 5f, -3f);

        Assert.True(service.GetVoiceStereoGains(voice, out var left, out var right));
        Assert.Equal(1f, left);
        Assert.Equal(0f, right);

        service.SetVoiceStereoGains(voice, float.NaN, 0.5f);
        Assert.True(service.GetVoiceStereoGains(voice, out left, out right));
        Assert.Equal(1f, left);
        Assert.Equal(0.5f, right);
        output.Pump();
    }

    [Theory]
    [InlineData(3370)]
    [InlineData(172610)]
    public void PlayClipStereo_PlaysAnyClipRate_AndKeepsItsLength(int clipRate)
    {
        var service = Create(out var output, out _);
        // One second of audio at the clip rate.
        var voice = service.PlayClipStereo(Constant(16384, clipRate, clipRate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);

        Assert.True(voice.IsValid);

        var soundingFrames = 0;
        for (var block = 0; block < 200 && service.IsAlive(voice); block++)
        {
            var data = output.Pump();
            for (var i = 0; i < Block; i++)
            {
                if (data[i * 2] > 0.01f)
                {
                    soundingFrames++;
                }
            }

            service.Update(0f);
        }

        Assert.False(service.IsAlive(voice));
        Assert.InRange(soundingFrames, Rate - Block, Rate + Block);
    }

    [Fact]
    public void Voice_IsRecycledAtItsEnd_AndTheSlotIsReusable()
    {
        var service = Create(out var output, out _, capacity: 1);
        var voice = service.PlayClipStereo(Constant(16384, 100), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);
        Assert.True(voice.IsValid);

        output.Pump();
        service.Update(0f);

        Assert.False(service.IsAlive(voice));
        Assert.False(service.GetVoiceStereoGains(voice, out _, out _));
        Assert.Equal(0, service.ActiveVoiceCount);

        var next = service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);
        Assert.True(next.IsValid);
        Assert.True(service.IsAlive(next));
    }

    [Fact]
    public void LoopedVoice_KeepsPlayingPastTheClipEnd()
    {
        var service = Create(out var output, out _);
        var looped = service.PlayClipStereo(Constant(16384, 100), AudioBusNames.Sfx, new AudioVoiceParameters(1f, 0f, 0f, true), 1f, 1f);

        PumpTwice(output);
        service.Update(0f);

        Assert.True(service.IsAlive(looped));
        AssertBlock(output.Pump(), 0.5f, 0.5f);
    }

    [Fact]
    public void FadeScalesBothChannels()
    {
        var service = Create(out var output, out _);
        var voice = service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 0.5f);
        PumpTwice(output);

        service.FadeVoice(voice, 0.5f, 0f);
        output.Pump(); // ramp
        AssertBlock(output.Pump(), 0.25f, 0.125f);
    }

    [Fact]
    public void BusVolumeChange_ScalesBothChannels()
    {
        var service = Create(out var output, out _);
        service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 0.5f);
        PumpTwice(output);

        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
        service.Update(0f);
        output.Pump(); // ramp
        AssertBlock(output.Pump(), 0.25f, 0.125f);
    }

    [Fact]
    public void StopVoicesOwnedBy_StopsTheVoice()
    {
        var service = Create(out var output, out _);
        var owner = new object();
        var other = new object();
        var mine = service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f, owner);
        var kept = service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f, other);
        PumpTwice(output);

        service.StopVoicesOwnedBy(owner);

        Assert.False(service.IsAlive(mine));
        Assert.True(service.IsAlive(kept));
        AssertBlock(output.Pump(), 0.5f, 0.5f);
    }

    [Fact]
    public void PauseAndResume_HoldAndRestoreTheOutput()
    {
        var service = Create(out var output, out _);
        var voice = service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 0.4f, 0.8f);
        PumpTwice(output);

        service.Pause(voice);
        AssertBlock(output.Pump(), 0f, 0f);

        service.Resume(voice);
        AssertBlock(output.Pump(), 0.2f, 0.4f);
    }

    [Fact]
    public void StaleHandle_IsIgnoredBySetAndGet()
    {
        var service = Create(out var output, out _);
        var voice = service.PlayClipStereo(Constant(16384, Rate), AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);
        service.Stop(voice);

        service.SetVoiceStereoGains(voice, 0f, 0f);

        Assert.False(service.GetVoiceStereoGains(voice, out var left, out var right));
        Assert.Equal(0f, left);
        Assert.Equal(0f, right);
        output.Pump();
    }

    [Fact]
    public void StereoClip_IsRefused()
    {
        var service = Create(out _, out _);
        var stereo = new PcmAudioClip(new short[200], Rate, 2);

        Assert.False(service.PlayClipStereo(stereo, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f).IsValid);
    }

    [Fact]
    public void SteadyStateFrames_DoNotAllocate()
    {
        var service = Create(out var output, out _);
        var voiceA = service.PlayClipStereo(Constant(16384, Rate * 20), AudioBusNames.Sfx, new AudioVoiceParameters(1f, 0f, 0f, true), 1f, 0.5f);
        var voiceB = service.PlayClipStereo(Constant(8000, 3370 * 20, 3370), AudioBusNames.Sfx, new AudioVoiceParameters(1f, 0f, 0f, true), 0.2f, 0.9f);

        for (var i = 0; i < 20; i++)
        {
            service.SetVoiceStereoGains(voiceA, 0.5f, 1f);
            service.Update(0.01f);
            output.Pump();
        }

        var before = AllocationWindow.Start();
        for (var i = 0; i < 200; i++)
        {
            var gain = (i % 10) / 10f;
            service.SetVoiceStereoGains(voiceA, gain, 1f - gain);
            service.SetVoiceStereoGains(voiceB, 1f - gain, gain);
            service.Update(0.01f);
            output.Pump();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
