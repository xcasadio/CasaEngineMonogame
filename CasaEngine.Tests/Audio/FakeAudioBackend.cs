using CasaEngine.Framework.Audio;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// In-memory <see cref="IAudioBackend"/> used by every audio test: buses, voice pool, fades and
/// music streaming are all verified against it, because no OpenAL device can be opened in CI.
/// It records what the engine asked for and lets a test drive the voices (finish them, saturate
/// the backend) with no timing dependency.
/// </summary>
public sealed class FakeAudioBackend : IAudioBackend
{
    private readonly List<Slot> _slots = new();

    public FakeAudioBackend(int voiceCapacity = 8)
    {
        VoiceCapacity = voiceCapacity;
        IsAvailable = true;
    }

    public bool IsAvailable { get; set; }

    public int VoiceCapacity { get; set; }

    public int ActiveVoiceCount
    {
        get
        {
            var count = 0;
            foreach (var slot in _slots)
            {
                if (slot.InUse)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Total number of accepted Play calls since creation.</summary>
    public int PlayCount { get; private set; }

    /// <summary>Total number of Play calls refused because no voice was available.</summary>
    public int RefusedPlayCount { get; private set; }

    public bool IsDisposed { get; private set; }

    public AudioVoiceHandle Play(IAudioClip clip, in AudioVoiceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (IsDisposed || !IsAvailable || ActiveVoiceCount >= VoiceCapacity)
        {
            RefusedPlayCount++;
            return AudioVoiceHandle.None;
        }

        var index = FindFreeSlot();
        if (index < 0)
        {
            _slots.Add(new Slot());
            index = _slots.Count - 1;
        }

        var slot = _slots[index];
        slot.InUse = true;
        slot.Clip = clip;
        slot.Parameters = parameters;
        slot.State = AudioVoiceState.Playing;

        PlayCount++;
        return new AudioVoiceHandle(index, slot.Generation);
    }

    public void SetParameters(AudioVoiceHandle voice, in AudioVoiceParameters parameters)
    {
        if (TryGetSlot(voice, out var slot))
        {
            slot.Parameters = parameters;
        }
    }

    public void SetVolume(AudioVoiceHandle voice, float volume)
    {
        if (TryGetSlot(voice, out var slot))
        {
            slot.Parameters = slot.Parameters.WithVolume(volume);
        }
    }

    public AudioVoiceState GetState(AudioVoiceHandle voice)
    {
        return TryGetSlot(voice, out var slot) ? slot.State : AudioVoiceState.Stopped;
    }

    public void Pause(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.State == AudioVoiceState.Playing)
        {
            slot.State = AudioVoiceState.Paused;
        }
    }

    public void Resume(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot) && slot.State == AudioVoiceState.Paused)
        {
            slot.State = AudioVoiceState.Playing;
        }
    }

    public void Stop(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot))
        {
            slot.State = AudioVoiceState.Stopped;
        }
    }

    public void Release(AudioVoiceHandle voice)
    {
        if (!TryGetSlot(voice, out var slot))
        {
            return;
        }

        slot.State = AudioVoiceState.Stopped;
        slot.InUse = false;
        slot.Clip = null;
        slot.Generation++;
    }

    public void StopAll()
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (!slot.InUse)
            {
                continue;
            }

            slot.State = AudioVoiceState.Stopped;
            slot.InUse = false;
            slot.Clip = null;
            slot.Generation++;
        }
    }

    public void Dispose()
    {
        IsDisposed = true;
        StopAll();
    }

    // ---- test helpers -------------------------------------------------------

    /// <summary>Simulates a voice reaching the end of its clip, without releasing its slot.</summary>
    public void CompleteVoice(AudioVoiceHandle voice)
    {
        if (TryGetSlot(voice, out var slot))
        {
            slot.State = AudioVoiceState.Stopped;
        }
    }

    /// <summary>Simulates every playing voice reaching the end of its clip.</summary>
    public void CompleteAllVoices()
    {
        foreach (var slot in _slots)
        {
            if (slot.InUse)
            {
                slot.State = AudioVoiceState.Stopped;
            }
        }
    }

    public AudioVoiceParameters GetParameters(AudioVoiceHandle voice)
    {
        return TryGetSlot(voice, out var slot) ? slot.Parameters : default;
    }

    public IAudioClip GetClip(AudioVoiceHandle voice)
    {
        return TryGetSlot(voice, out var slot) ? slot.Clip : null;
    }

    public bool IsVoiceAlive(AudioVoiceHandle voice) => TryGetSlot(voice, out _);

    // ------------------------------------------------------------------------

    private int FindFreeSlot()
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].InUse)
            {
                return i;
            }
        }

        return -1;
    }

    private bool TryGetSlot(AudioVoiceHandle voice, out Slot slot)
    {
        slot = null;

        if (!voice.IsValid || voice.Index >= _slots.Count)
        {
            return false;
        }

        var candidate = _slots[voice.Index];
        if (!candidate.InUse || candidate.Generation != voice.Generation)
        {
            return false;
        }

        slot = candidate;
        return true;
    }

    private sealed class Slot
    {
        public bool InUse;
        public int Generation;
        public IAudioClip Clip;
        public AudioVoiceParameters Parameters;
        public AudioVoiceState State;
    }
}
