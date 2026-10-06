using CasaEngine.Core.Logging;
using Microsoft.Xna.Framework.Audio;

namespace CasaEngine.Framework.Audio.Backends;

/// <summary>
/// <see cref="IAudioBackend"/> implemented on MonoGame/OpenAL.
/// </summary>
/// <remarks>
/// Voice slots are preallocated and recycled. A slot keeps the
/// <see cref="SoundEffectInstance"/> it last used: replaying the same clip on that slot reuses
/// the instance instead of allocating a new one, which matters because gameplay replays the
/// same few sounds over and over.
/// DesktopGL exposes 256 OpenAL sources; past that MonoGame throws
/// <see cref="InstancePlayLimitException"/>, which is caught here and reported as a refused
/// voice rather than propagated to gameplay.
/// A streaming voice only accepts the sample rates <see cref="DynamicSoundEffectInstance"/>
/// accepts (<see cref="MinStreamingSampleRate"/> to <see cref="MaxStreamingSampleRate"/>); any
/// other rate is refused the same way.
/// </remarks>
public sealed class MonoGameAudioBackend : IAudioBackend
{
    /// <summary>Well under the 256 OpenAL sources of DesktopGL, to leave room for streaming voices.</summary>
    public const int DefaultVoiceCapacity = 64;

    /// <summary>Lowest sample rate of a streaming voice, the floor of <see cref="DynamicSoundEffectInstance"/>.</summary>
    public const int MinStreamingSampleRate = 8000;

    /// <summary>Highest sample rate of a streaming voice, the ceiling of <see cref="DynamicSoundEffectInstance"/>.</summary>
    public const int MaxStreamingSampleRate = 48000;

    private readonly VoiceSlot[] _slots;
    private readonly int[] _freeSlots;
    private readonly AudioLogThrottle _playLimitLog = new();
    private readonly AudioLogThrottle _hardwareLog = new();
    private readonly AudioLogThrottle _streamFormatLog = new();

    private int _freeSlotCount;
    private int _activeVoiceCount;
    private bool _isDisposed;

    public MonoGameAudioBackend(int voiceCapacity = DefaultVoiceCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(voiceCapacity);

        _slots = new VoiceSlot[voiceCapacity];
        _freeSlots = new int[voiceCapacity];

        for (var i = 0; i < voiceCapacity; i++)
        {
            _slots[i] = new VoiceSlot();
            // Filled in reverse so that the first Play takes slot 0.
            _freeSlots[i] = voiceCapacity - 1 - i;
        }

        _freeSlotCount = voiceCapacity;
        IsAvailable = true;
    }

    public bool IsAvailable { get; private set; }

    public int VoiceCapacity => _slots.Length;

    public int ActiveVoiceCount => _activeVoiceCount;

    /// <summary>Slots ready for a new voice; lets tests check that a refused voice gave its slot back.</summary>
    internal int FreeVoiceCount => _freeSlotCount;

    public AudioVoiceHandle Play(IAudioClip clip, in AudioVoiceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (_isDisposed || !IsAvailable)
        {
            return AudioVoiceHandle.None;
        }

        MonoGameAudioClip monoGameClip;

        if (clip is MonoGameAudioClip directClip)
        {
            monoGameClip = directClip;
        }
        else if (clip is PcmAudioClip pcmClip)
        {
            if (pcmClip.IsDisposed)
            {
                return AudioVoiceHandle.None;
            }

            monoGameClip = GetOrCreateMonoGameClip(pcmClip);
            if (monoGameClip == null)
            {
                return AudioVoiceHandle.None;
            }
        }
        else
        {
            throw new ArgumentException(
                $"{nameof(MonoGameAudioBackend)} only plays {nameof(MonoGameAudioClip)} and {nameof(PcmAudioClip)} instances, got '{clip.GetType().FullName}'.",
                nameof(clip));
        }

        if (monoGameClip.IsDisposed)
        {
            return AudioVoiceHandle.None;
        }

        var slotIndex = TakeFreeSlot(monoGameClip);
        if (slotIndex < 0)
        {
            if (_playLimitLog.ShouldWrite())
            {
                _playLimitLog.WriteNow($"Audio: no free voice ({VoiceCapacity} in use), sound refused.");
            }

            return AudioVoiceHandle.None;
        }

        var slot = _slots[slotIndex];

        try
        {
            slot.Bind(monoGameClip);
            slot.ApplyParameters(parameters);
            slot.Instance.Play();
        }
        catch (InstancePlayLimitException)
        {
            ReturnSlot(slotIndex, disposeInstance: true);
            _playLimitLog.WriteWarning("Audio: OpenAL source limit reached, sound refused.");
            return AudioVoiceHandle.None;
        }
        catch (NoAudioHardwareException exception)
        {
            ReturnSlot(slotIndex, disposeInstance: true);
            DisableAfterHardwareFailure(exception);
            return AudioVoiceHandle.None;
        }

        slot.InUse = true;
        _activeVoiceCount++;
        return new AudioVoiceHandle(slotIndex, slot.Generation);
    }

    public void SetParameters(AudioVoiceHandle voice, in AudioVoiceParameters parameters)
    {
        if (!TryGetSlot(voice, out var slot))
        {
            return;
        }

        slot.ApplyParameters(parameters);
    }

    public void SetVolume(AudioVoiceHandle voice, float volume)
    {
        if (!TryGetSlot(voice, out var slot))
        {
            return;
        }

        slot.Instance.Volume = Math.Clamp(
            float.IsNaN(volume) ? AudioVoiceParameters.MaxVolume : volume,
            AudioVoiceParameters.MinVolume,
            AudioVoiceParameters.MaxVolume);
    }

    public AudioVoiceState GetState(AudioVoiceHandle voice)
    {
        if (!TryGetSlot(voice, out var slot))
        {
            return AudioVoiceState.Stopped;
        }

        return slot.Instance.State switch
        {
            SoundState.Playing => AudioVoiceState.Playing,
            SoundState.Paused => AudioVoiceState.Paused,
            _ => AudioVoiceState.Stopped,
        };
    }

    public void Pause(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.Instance.State == SoundState.Playing)
        {
            slot.Instance.Pause();
        }
    }

    public void Resume(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.Instance.State == SoundState.Paused)
        {
            slot.Instance.Resume();
        }
    }

    public void Stop(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.Instance.State != SoundState.Stopped)
        {
            slot.Instance.Stop();
        }
    }

    public void Release(AudioVoiceHandle voice)
    {
        if (!TryGetSlot(voice, out var slot))
        {
            return;
        }

        if (slot.Instance.State != SoundState.Stopped)
        {
            slot.Instance.Stop();
        }

        ReturnSlot(voice.Index, disposeInstance: false);
    }

    public void StopAll()
    {
        for (var i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].InUse)
            {
                continue;
            }

            if (_slots[i].Instance is { IsDisposed: false, State: not SoundState.Stopped })
            {
                _slots[i].Instance.Stop();
            }

            ReturnSlot(i, disposeInstance: false);
        }
    }

    public bool SupportsStreaming => true;

    public AudioVoiceHandle CreateStreamingVoice(int sampleRate, int channelCount, in AudioVoiceParameters parameters)
    {
        if (_isDisposed || !IsAvailable)
        {
            return AudioVoiceHandle.None;
        }

        if (channelCount is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount), channelCount, "Only mono and stereo are supported.");
        }

        // The rate comes from asset data (a wav header), so it is refused rather than thrown:
        // DynamicSoundEffectInstance would throw ArgumentOutOfRangeException for it.
        if (sampleRate is < MinStreamingSampleRate or > MaxStreamingSampleRate)
        {
            if (_streamFormatLog.ShouldWrite())
            {
                _streamFormatLog.WriteNow(
                    $"Audio: stream refused, its sample rate ({sampleRate} Hz) is outside the {MinStreamingSampleRate}-{MaxStreamingSampleRate} Hz range MonoGame can stream.");
            }

            return AudioVoiceHandle.None;
        }

        var slotIndex = TakeFreeSlot(null);
        if (slotIndex < 0)
        {
            if (_playLimitLog.ShouldWrite())
            {
                _playLimitLog.WriteNow($"Audio: no free voice ({VoiceCapacity} in use), stream refused.");
            }

            return AudioVoiceHandle.None;
        }

        var slot = _slots[slotIndex];

        try
        {
            slot.BindStreaming(sampleRate, channelCount);
            slot.ApplyParameters(parameters);
        }
        catch (InstancePlayLimitException)
        {
            // Thrown by the DynamicSoundEffectInstance constructor when no OpenAL source is left.
            ReturnSlot(slotIndex, disposeInstance: true);
            _playLimitLog.WriteWarning("Audio: OpenAL source limit reached, stream refused.");
            return AudioVoiceHandle.None;
        }
        catch (NoAudioHardwareException exception)
        {
            ReturnSlot(slotIndex, disposeInstance: true);
            DisableAfterHardwareFailure(exception);
            return AudioVoiceHandle.None;
        }
        catch
        {
            // Not a refusal: the slot still goes back so the voice is not lost, and the error surfaces.
            ReturnSlot(slotIndex, disposeInstance: true);
            throw;
        }

        slot.InUse = true;
        _activeVoiceCount++;
        return new AudioVoiceHandle(slotIndex, slot.Generation);
    }

    public void SubmitBuffer(AudioVoiceHandle voice, byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (!TryGetSlot(voice, out var slot) || slot.Instance is not DynamicSoundEffectInstance dynamicInstance)
        {
            return;
        }

        if (count <= 0)
        {
            return;
        }

        try
        {
            // SubmitBuffer copies the data, so the caller can reuse its array immediately.
            dynamicInstance.SubmitBuffer(buffer, offset, count);
        }
        catch (InstancePlayLimitException)
        {
            _playLimitLog.WriteWarning("Audio: OpenAL source limit reached while streaming.");
        }
    }

    public int GetPendingBufferCount(AudioVoiceHandle voice)
    {
        if (!TryGetSlot(voice, out var slot) || slot.Instance is not DynamicSoundEffectInstance dynamicInstance)
        {
            return 0;
        }

        return dynamicInstance.PendingBufferCount;
    }

    public void Start(AudioVoiceHandle voice)
    {
        if (!TryGetSlot(voice, out var slot) || slot.Instance.State == SoundState.Playing)
        {
            return;
        }

        try
        {
            slot.Instance.Play();
        }
        catch (InstancePlayLimitException)
        {
            _playLimitLog.WriteWarning("Audio: OpenAL source limit reached, stream not started.");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        for (var i = 0; i < _slots.Length; i++)
        {
            _slots[i].DisposeInstance();
        }

        _activeVoiceCount = 0;
        _freeSlotCount = 0;
    }

    // A PcmAudioClip is played through a MonoGame SoundEffect built once from its samples and kept
    // in the clip's BackendResource, so it is released with the clip and slot reuse keeps working.
    private MonoGameAudioClip GetOrCreateMonoGameClip(PcmAudioClip pcmClip)
    {
        if (pcmClip.BackendResource is MonoGameAudioClip cached)
        {
            return cached;
        }

        try
        {
            var wavBytes = CreateWavBytes(pcmClip);
            using var wavStream = new MemoryStream(wavBytes);
            var soundEffect = SoundEffect.FromStream(wavStream);

            // Mono samples are kept next to the effect for the software stereo path (ADR-0039).
            var created = pcmClip.ChannelCount == 1
                ? new MonoGameAudioClip(soundEffect, pcmClip.SampleArray, pcmClip.SampleRate)
                : new MonoGameAudioClip(soundEffect);

            pcmClip.BackendResource = created;
            return created;
        }
        catch (NoAudioHardwareException exception)
        {
            DisableAfterHardwareFailure(exception);
            return null;
        }
    }

    // RIFF header (44 bytes) followed by the 16 bit PCM samples, as SoundEffect.FromStream expects.
    private static byte[] CreateWavBytes(PcmAudioClip clip)
    {
        const int headerSize = 44;
        const int bitsPerSample = 16;

        var dataSize = clip.SampleArray.Length * sizeof(short);
        var blockAlign = clip.ChannelCount * (bitsPerSample / 8);
        var bytes = new byte[headerSize + dataSize];

        using (var stream = new MemoryStream(bytes))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)clip.ChannelCount);
            writer.Write(clip.SampleRate);
            writer.Write(clip.SampleRate * blockAlign);
            writer.Write((short)blockAlign);
            writer.Write((short)bitsPerSample);
            writer.Write("data"u8);
            writer.Write(dataSize);
        }

        Buffer.BlockCopy(clip.SampleArray, 0, bytes, headerSize, dataSize);
        return bytes;
    }

    // Prefers a free slot that already holds an instance of the same clip, so the common case
    // (the same sound replayed) does not allocate a new SoundEffectInstance.
    private int TakeFreeSlot(MonoGameAudioClip clip)
    {
        if (_freeSlotCount == 0)
        {
            return -1;
        }

        for (var i = _freeSlotCount - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_slots[_freeSlots[i]].Clip, clip))
            {
                continue;
            }

            var matchedIndex = _freeSlots[i];
            _freeSlots[i] = _freeSlots[_freeSlotCount - 1];
            _freeSlotCount--;
            return matchedIndex;
        }

        _freeSlotCount--;
        return _freeSlots[_freeSlotCount];
    }

    private void ReturnSlot(int slotIndex, bool disposeInstance)
    {
        var slot = _slots[slotIndex];

        if (slot.InUse)
        {
            _activeVoiceCount--;
            slot.InUse = false;
        }

        if (disposeInstance)
        {
            slot.DisposeInstance();
        }

        slot.Generation++;
        _freeSlots[_freeSlotCount] = slotIndex;
        _freeSlotCount++;
    }

    private bool TryGetSlot(AudioVoiceHandle voice, out VoiceSlot slot)
    {
        slot = null;

        if (_isDisposed || !voice.IsValid || voice.Index >= _slots.Length)
        {
            return false;
        }

        var candidate = _slots[voice.Index];
        if (!candidate.InUse || candidate.Generation != voice.Generation || candidate.Instance is not { IsDisposed: false })
        {
            return false;
        }

        slot = candidate;
        return true;
    }

    private void DisableAfterHardwareFailure(Exception exception)
    {
        IsAvailable = false;
        _hardwareLog.WriteError($"Audio: no audio hardware available, sound is disabled. {exception.Message}");
        Logs.WriteWarning("Audio: the game keeps running without sound.");
    }

    private sealed class VoiceSlot
    {
        public SoundEffectInstance Instance;
        public MonoGameAudioClip Clip;
        public int Generation;
        public bool InUse;

        public void Bind(MonoGameAudioClip clip)
        {
            if (ReferenceEquals(Clip, clip) && Instance is { IsDisposed: false } and not DynamicSoundEffectInstance)
            {
                if (Instance.State != SoundState.Stopped)
                {
                    Instance.Stop();
                }

                return;
            }

            DisposeInstance();
            Instance = clip.SoundEffect.CreateInstance();
            Clip = clip;
        }

        /// <summary>
        /// A streaming instance is never reused: its sample rate and channel count are fixed at
        /// creation, and the queued buffers belong to the previous stream.
        /// </summary>
        public void BindStreaming(int sampleRate, int channelCount)
        {
            DisposeInstance();
            Instance = new DynamicSoundEffectInstance(sampleRate, (AudioChannels)channelCount);
            Clip = null;
        }

        /// <remarks>
        /// The loop region of the parameters is ignored: a looped clip loops whole (SoundEffectInstance has
        /// no region). The rate multiplier is folded into the pitch, pitch + log2(multiplier), clamped to
        /// the +-1 octave range of SoundEffectInstance.
        /// </remarks>
        public void ApplyParameters(in AudioVoiceParameters parameters)
        {
            Instance.Volume = parameters.Volume;
            Instance.Pan = parameters.Pan;
            Instance.Pitch = Math.Clamp(
                parameters.Pitch + MathF.Log2(parameters.RateMultiplier),
                AudioVoiceParameters.MinPitch,
                AudioVoiceParameters.MaxPitch);

            // XNA forbids IsLooped on a dynamic instance: looping a stream is the reader's job,
            // it rewinds and keeps submitting.
            if (Instance is not DynamicSoundEffectInstance)
            {
                Instance.IsLooped = parameters.IsLooped;
            }
        }

        public void DisposeInstance()
        {
            if (Instance is { IsDisposed: false })
            {
                Instance.Dispose();
            }

            Instance = null;
            Clip = null;
        }
    }
}
