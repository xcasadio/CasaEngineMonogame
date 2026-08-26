using CasaEngine.Framework.Audio;

namespace CasaEngine.Tests.Audio;

/// <summary>Deterministic <see cref="IAudioClip"/> for the tests: no device, no decoding.</summary>
public sealed class FakeAudioClip : IAudioClip
{
    public FakeAudioClip(string name = "clip", int sampleRate = 44100, int channelCount = 2, double durationSeconds = 1.0)
    {
        Name = name;
        SampleRate = sampleRate;
        ChannelCount = channelCount;
        Duration = TimeSpan.FromSeconds(durationSeconds);
    }

    public string Name { get; }

    public int SampleRate { get; }

    public int ChannelCount { get; }

    public TimeSpan Duration { get; }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public override string ToString() => Name;
}
