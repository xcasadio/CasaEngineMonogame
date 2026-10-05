namespace CasaEngine.Framework.Audio;

/// <summary>
/// Backend neutral <see cref="IAudioClip"/>: fully resident interleaved 16 bit PCM, mono or
/// stereo, with no dependency on any MonoGame type. Produced by the wav loader; each backend
/// turns it into whatever it needs to play it.
/// </summary>
/// <remarks>
/// Also implements <see cref="IAudioClipSamples"/> (ADR-0039): <see cref="MonoSamples"/> is the
/// sample data for a mono clip and empty for a stereo one.
/// </remarks>
public sealed class PcmAudioClip : IAudioClip, IAudioClipSamples
{
    private readonly short[] _samples;

    /// <param name="samples">Interleaved 16 bit samples, used as-is (not copied).</param>
    /// <param name="sampleRate">Sample rate in Hz, greater than zero.</param>
    /// <param name="channelCount">1 or 2.</param>
    /// <exception cref="ArgumentException">The sample count is not a whole number of frames.</exception>
    public PcmAudioClip(short[] samples, int sampleRate, int channelCount)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        if (channelCount is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount), channelCount, "Only mono and stereo clips are supported.");
        }

        if (samples.Length % channelCount != 0)
        {
            throw new ArgumentException(
                $"The sample count ({samples.Length}) is not a multiple of the channel count ({channelCount}).", nameof(samples));
        }

        _samples = samples;
        SampleRate = sampleRate;
        ChannelCount = channelCount;
    }

    public int SampleRate { get; }

    public int ChannelCount { get; }

    /// <summary>Number of frames, i.e. samples per channel.</summary>
    public int FrameCount => _samples.Length / ChannelCount;

    public TimeSpan Duration => TimeSpan.FromSeconds((double)FrameCount / SampleRate);

    public bool IsDisposed { get; private set; }

    /// <summary>The interleaved samples, <see cref="ChannelCount"/> per frame.</summary>
    public ReadOnlyMemory<short> Samples => _samples;

    /// <inheritdoc/>
    public ReadOnlyMemory<short> MonoSamples => ChannelCount == 1 ? _samples : ReadOnlyMemory<short>.Empty;

    /// <summary>The sample array itself, for a backend that needs an array without a copy.</summary>
    internal short[] SampleArray => _samples;

    /// <summary>
    /// Backend specific resource built from this clip (for example a native sound), owned by the
    /// clip so it is released with it. Only the backend that set it knows its type.
    /// </summary>
    internal IDisposable BackendResource { get; set; }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;

        var resource = BackendResource;
        BackendResource = null;
        resource?.Dispose();
    }
}
