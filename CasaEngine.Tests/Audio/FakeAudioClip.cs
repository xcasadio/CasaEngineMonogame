using CasaEngine.Framework.Audio;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// Deterministic <see cref="IAudioClip"/> for the tests: no device, no decoding. Also implements
/// <see cref="IAudioClipSamples"/> so the software stereo voice path (ADR-0039) can be tested
/// without a real wav file: pass <paramref name="monoSamples"/> to expose them, or leave it null
/// for a clip that <see cref="AudioService.PlayClipStereo"/> must refuse.
/// </summary>
public sealed class FakeAudioClip : IAudioClip, IAudioClipSamples
{
    public FakeAudioClip(
        string name = "clip",
        int sampleRate = 44100,
        int channelCount = 2,
        double durationSeconds = 1.0,
        short[] monoSamples = null)
    {
        Name = name;
        SampleRate = sampleRate;
        ChannelCount = channelCount;
        Duration = TimeSpan.FromSeconds(durationSeconds);
        MonoSamples = monoSamples ?? Array.Empty<short>();
    }

    public string Name { get; }

    public int SampleRate { get; }

    public int ChannelCount { get; }

    public TimeSpan Duration { get; }

    public bool IsDisposed { get; private set; }

    public ReadOnlyMemory<short> MonoSamples { get; }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public override string ToString() => Name;
}
