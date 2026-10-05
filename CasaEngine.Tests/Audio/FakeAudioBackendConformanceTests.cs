using CasaEngine.Framework.Audio;

namespace CasaEngine.Tests.Audio;

public class FakeAudioBackendConformanceTests : AudioBackendConformanceTests
{
    protected override IAudioBackend CreateBackend(int capacity) => new FakeAudioBackend(capacity);

    protected override IAudioBackend CreateUnavailableBackend() => new FakeAudioBackend { IsAvailable = false };

    protected override IAudioClip CreateClip(int frames, int channels)
    {
        return new FakeAudioClip(channelCount: channels, sampleRate: 48000);
    }

    protected override void EndResidentVoice(IAudioBackend backend, AudioVoiceHandle voice)
    {
        ((FakeAudioBackend)backend).CompleteVoice(voice);
    }

    protected override void ConsumeBuffers(IAudioBackend backend, AudioVoiceHandle voice, int count)
    {
        ((FakeAudioBackend)backend).ConsumeBuffers(voice, count);
    }
}
