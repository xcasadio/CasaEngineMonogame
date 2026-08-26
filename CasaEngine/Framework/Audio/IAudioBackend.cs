namespace CasaEngine.Framework.Audio;

/// <summary>
/// Boundary between the engine audio logic (buses, voice pool, fades) and the platform.
/// No platform type appears here on purpose: the whole mixing layer is unit tested against
/// a fake backend, because an OpenAL device cannot be opened in CI.
/// </summary>
/// <remarks>
/// The contract is 2D only (volume and pan), per the V1 audio decisions: there is no
/// listener, no emitter and no distance attenuation.
/// </remarks>
public interface IAudioBackend : IDisposable
{
    /// <summary>
    /// False when no audio device could be opened. Every call stays valid in that case and
    /// behaves as a silent no-op, so a machine without audio never breaks the game.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>Maximum number of voices the backend accepts simultaneously.</summary>
    int VoiceCapacity { get; }

    /// <summary>Number of voices currently allocated (playing or paused).</summary>
    int ActiveVoiceCount { get; }

    /// <summary>
    /// Starts a new voice for <paramref name="clip"/>.
    /// Returns <see cref="AudioVoiceHandle.None"/> when no voice is available; this is a normal
    /// outcome under load and must not throw.
    /// </summary>
    AudioVoiceHandle Play(IAudioClip clip, in AudioVoiceParameters parameters);

    /// <summary>Applies every parameter at once. Ignored when the handle is stale.</summary>
    void SetParameters(AudioVoiceHandle voice, in AudioVoiceParameters parameters);

    /// <summary>Applies the volume only; used by the per-frame fade ramps.</summary>
    void SetVolume(AudioVoiceHandle voice, float volume);

    /// <summary>Returns <see cref="AudioVoiceState.Stopped"/> for a stale handle.</summary>
    AudioVoiceState GetState(AudioVoiceHandle voice);

    void Pause(AudioVoiceHandle voice);

    void Resume(AudioVoiceHandle voice);

    /// <summary>Stops playback but keeps the slot, so the state stays observable until Release.</summary>
    void Stop(AudioVoiceHandle voice);

    /// <summary>Stops the voice if needed and returns the slot to the backend, invalidating the handle.</summary>
    void Release(AudioVoiceHandle voice);

    /// <summary>Stops and releases every voice. Used when the game shuts down or leaves play mode.</summary>
    void StopAll();

    // ---- streaming ---------------------------------------------------------
    // A streamed voice is fed buffer by buffer instead of playing a resident clip. This is how
    // music is played: the file is decoded on the fly rather than held in memory.

    /// <summary>False when the platform cannot stream; music then stays silent.</summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// Allocates a voice fed by <see cref="SubmitBuffer"/>. The voice does not play until
    /// <see cref="Start"/> is called, so the caller can queue a few buffers first and avoid an
    /// immediate underrun.
    /// </summary>
    AudioVoiceHandle CreateStreamingVoice(int sampleRate, int channelCount, in AudioVoiceParameters parameters);

    /// <summary>
    /// Queues 16 bit PCM audio on a streaming voice. The data is copied, so the caller can reuse
    /// its buffer as soon as the call returns.
    /// </summary>
    void SubmitBuffer(AudioVoiceHandle voice, byte[] buffer, int offset, int count);

    /// <summary>Number of queued buffers not played yet. Zero means the voice is starving.</summary>
    int GetPendingBufferCount(AudioVoiceHandle voice);

    /// <summary>Starts a voice that was created but not started yet.</summary>
    void Start(AudioVoiceHandle voice);
}
