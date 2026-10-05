using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Output;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// The loop region and the rate multiplier (ADR-0056, P11/P12) must survive every path through
/// <see cref="AudioService"/> that rebuilds the voice parameters: bus gain, pan, fades, bus changes.
/// </summary>
public class AudioServiceLoopRegionTests
{
    private const int Rate = 48000;
    private const int Block = 480;

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

    private static AudioVoiceParameters RegionParameters()
    {
        return AudioVoiceParameters.Default.WithLooping(true).WithLoopRegion(1000, 2000).WithRateMultiplier(4f);
    }

    private static void AssertCarries(AudioVoiceParameters parameters)
    {
        Assert.True(parameters.HasLoopRegion);
        Assert.Equal(1000, parameters.LoopStartFrame);
        Assert.Equal(2000, parameters.LoopEndFrame);
        Assert.Equal(4f, parameters.RateMultiplier);
    }

    private static void AssertThroughEveryChange(AudioService service, FakeAudioBackend backend, AudioVoiceHandle voice)
    {
        AssertCarries(backend.GetParameters(voice));

        service.SetVoicePan(voice, 0.5f);
        AssertCarries(backend.GetParameters(voice));

        service.FadeVoice(voice, 0.5f, 1f);
        service.Update(0.25f);
        AssertCarries(backend.GetParameters(voice));

        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.5f;
        service.Update(0.25f);
        AssertCarries(backend.GetParameters(voice));

        service.FadeVoice(voice, 0.25f, 0f);
        AssertCarries(backend.GetParameters(voice));
    }

    [Fact]
    public void PlayClip_KeepsTheRegionAndTheMultiplierThroughEveryChange()
    {
        var backend = new FakeAudioBackend();
        var service = new AudioService(backend);
        var clip = new FakeAudioClip(sampleRate: Rate, monoSamples: new short[3000]);

        var voice = service.PlayClip(clip, AudioBusNames.Sfx, RegionParameters());

        Assert.True(voice.IsValid);
        AssertThroughEveryChange(service, backend, voice);
    }

    [Fact]
    public void PlayClipStereo_OnAStreamingBackend_KeepsTheRegionAndTheMultiplierThroughEveryChange()
    {
        var backend = new FakeAudioBackend { RecordSubmittedBuffers = true };
        var service = new AudioService(backend);
        var clip = new FakeAudioClip(sampleRate: Rate, monoSamples: new short[3000]);

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, RegionParameters(), 1f, 1f);

        Assert.True(voice.IsValid);
        AssertThroughEveryChange(service, backend, voice);
    }

    [Fact]
    public void PlayClipStereo_OnTheSoftwareBackend_StillLoopsTheRegionAtTheMultiplierAfterEveryChange()
    {
        // The software backend is observed through its rendered output: the clip is silent outside
        // [1000, 2000) and constant inside it. At multiplier 4 the voice enters the region after 250
        // frames and stays in it for good; a lost region would let the whole clip loop (zeros again
        // after 500 frames), a lost multiplier would take 4 times longer to reach the region.
        var output = new CapturingOutput();
        using var backend = new SoftwareAudioBackend(output, 8);
        var service = new AudioService(backend);
        var samples = new short[3000];
        Array.Fill(samples, (short)16384, 1000, 1000);
        var clip = new PcmAudioClip(samples, Rate, 1);

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, RegionParameters(), 1f, 1f);
        Assert.True(voice.IsValid);

        var first = (float[])output.Pump().Clone();
        Assert.Equal(0f, first[0]);
        Assert.True(first[(Block - 1) * 2] > 0.4f, "the voice must be inside the region within the first block (multiplier 4)");

        service.SetVoicePan(voice, 0.5f);
        service.FadeVoice(voice, 0.5f, 0.2f);
        service.Mixer.GetBus(AudioBusNames.Sfx).Volume = 0.8f;

        for (var block = 0; block < 40; block++)
        {
            service.Update(0.01f);
            var buffer = output.Pump();

            for (var i = 0; i < Block; i++)
            {
                Assert.True(buffer[i * 2] > 0.05f, $"frame {i} of block {block} fell out of the loop region");
            }
        }
    }
}
