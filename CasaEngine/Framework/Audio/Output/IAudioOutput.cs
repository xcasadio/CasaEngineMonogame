namespace CasaEngine.Framework.Audio.Output;

/// <summary>
/// Fills <paramref name="interleavedStereo"/> (length at least 2 * <paramref name="frameCount"/>) with
/// <paramref name="frameCount"/> stereo frames. Called on the audio thread: it must not allocate or block.
/// </summary>
internal delegate void AudioRenderCallback(Span<float> interleavedStereo, int frameCount);

/// <summary>
/// A stereo float audio output fed by a render callback on a dedicated thread (ADR-0055).
/// Two phases: <see cref="TryOpen"/> opens the device so the mixer can learn <see cref="SampleRate"/>,
/// then <see cref="Start"/> begins pulling audio through the callback.
/// </summary>
internal interface IAudioOutput : IDisposable
{
    /// <summary>True once <see cref="TryOpen"/> succeeded and until the output fails or is disposed.</summary>
    bool IsAvailable { get; }

    /// <summary>Device sample rate in Hz; valid after a successful <see cref="TryOpen"/>.</summary>
    int SampleRate { get; }

    /// <summary>Frames per buffer; valid after a successful <see cref="TryOpen"/>.</summary>
    int BufferFrames { get; }

    /// <summary>Number of buffers kept queued (the lead is BufferCount * BufferFrames frames).</summary>
    int BufferCount { get; }

    /// <summary>Number of times the device ran out of queued audio. Safe to read from any thread.</summary>
    int UnderrunCount { get; }

    /// <summary>
    /// Opens the device and blocks until the audio thread has opened it or failed (bounded by a timeout).
    /// Returns false, after logging the reason once, when no output can be produced.
    /// </summary>
    bool TryOpen();

    /// <summary>Starts playback; <paramref name="callback"/> is stored once and called on the audio thread.</summary>
    void Start(AudioRenderCallback callback);
}
