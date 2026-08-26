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
/// </remarks>
public sealed class MonoGameAudioBackend : IAudioBackend
{
    /// <summary>Well under the 256 OpenAL sources of DesktopGL, to leave room for streaming voices.</summary>
    public const int DefaultVoiceCapacity = 64;

    private readonly VoiceSlot[] _slots;
    private readonly int[] _freeSlots;
    private readonly AudioLogThrottle _playLimitLog = new();
    private readonly AudioLogThrottle _hardwareLog = new();

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

    public AudioVoiceHandle Play(IAudioClip clip, in AudioVoiceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (_isDisposed || !IsAvailable)
        {
            return AudioVoiceHandle.None;
        }

        if (clip is not MonoGameAudioClip monoGameClip)
        {
            throw new ArgumentException(
                $"{nameof(MonoGameAudioBackend)} only plays {nameof(MonoGameAudioClip)} instances, got '{clip.GetType().FullName}'.",
                nameof(clip));
        }

        if (monoGameClip.IsDisposed)
        {
            return AudioVoiceHandle.None;
        }

        var slotIndex = TakeFreeSlot(monoGameClip);
        if (slotIndex < 0)
        {
            _playLimitLog.WriteWarning(
                $"Audio: no free voice ({VoiceCapacity} in use), sound refused.");
            return AudioVoiceHandle.None;
        }

        var slot = _slots[slotIndex];

        try
        {
            slot.Bind(monoGameClip);
            slot.Instance.Volume = parameters.Volume;
            slot.Instance.Pan = parameters.Pan;
            slot.Instance.Pitch = parameters.Pitch;
            slot.Instance.IsLooped = parameters.IsLooped;
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

        slot.Instance.Volume = parameters.Volume;
        slot.Instance.Pan = parameters.Pan;
        slot.Instance.Pitch = parameters.Pitch;
        slot.Instance.IsLooped = parameters.IsLooped;
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
            if (ReferenceEquals(Clip, clip) && Instance is { IsDisposed: false })
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
