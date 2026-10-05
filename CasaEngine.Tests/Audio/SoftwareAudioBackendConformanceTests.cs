using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;

namespace CasaEngine.Tests.Audio;

public class SoftwareAudioBackendConformanceTests : AudioBackendConformanceTests
{
    private const int PumpFrames = 256;
    private const int MaxPumps = 2000;

    private OfflineAudioOutput _output;

    protected override IAudioBackend CreateBackend(int capacity)
    {
        _output = new OfflineAudioOutput();
        return new SoftwareAudioBackend(_output, capacity);
    }

    protected override IAudioBackend CreateUnavailableBackend()
    {
        return new SoftwareAudioBackend(new OfflineAudioOutput(canOpen: false), 4);
    }

    protected override IAudioClip CreateClip(int frames, int channels)
    {
        var samples = new short[frames * channels];
        Array.Fill(samples, (short)4000);
        return new PcmAudioClip(samples, 48000, channels);
    }

    protected override void EndResidentVoice(IAudioBackend backend, AudioVoiceHandle voice)
    {
        // The clip is 4800 frames at the output rate: 50 pumps of 256 frames are plenty.
        for (var i = 0; i < 50 && backend.GetState(voice) != AudioVoiceState.Stopped; i++)
        {
            _output.Pump(PumpFrames);
        }
    }

    protected override void ConsumeBuffers(IAudioBackend backend, AudioVoiceHandle voice, int count)
    {
        var target = backend.GetPendingBufferCount(voice) - count;

        for (var i = 0; i < MaxPumps && backend.GetPendingBufferCount(voice) > target; i++)
        {
            _output.Pump(PumpFrames);
        }
    }
}
