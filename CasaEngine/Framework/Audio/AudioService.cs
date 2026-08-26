using CasaEngine.Framework.Audio.Mixing;

namespace CasaEngine.Framework.Audio;

/// <summary>
/// Engine-side audio logic: voice pool, bus routing and ownership.
/// </summary>
/// <remarks>
/// Deliberately free of any MonoGame type, including <c>Game</c>: this is what makes the audio
/// behaviour unit testable, since neither an OpenAL device nor a game window can be created in
/// CI. <see cref="Application.Components.AudioSystemComponent"/> is the thin GameComponent that
/// drives it from the game loop.
/// A voice keeps its "base" parameters (what the caller asked for); the volume actually sent to
/// the backend is that base volume multiplied by the effective gain of its bus.
/// </remarks>
public sealed class AudioService : IDisposable
{
    private readonly IAudioBackend _backend;
    private readonly List<VoiceEntry> _voices = new();
    private readonly AudioLogThrottle _refusedVoiceLog = new();

    private int _appliedMixerVersion = -1;
    private bool _isDisposed;

    public AudioService(IAudioBackend backend, AudioMixer mixer = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        Mixer = mixer ?? AudioBusNames.CreateDefaultMixer();
    }

    public IAudioBackend Backend => _backend;

    public AudioMixer Mixer { get; }

    public bool IsAudioAvailable => _backend.IsAvailable;

    /// <summary>Number of voices the service currently tracks (playing or paused).</summary>
    public int ActiveVoiceCount { get; private set; }

    /// <summary>Number of Play calls refused since creation, because the backend had no voice left.</summary>
    public int RefusedVoiceCount { get; private set; }

    /// <summary>
    /// Starts <paramref name="clip"/> on <paramref name="busName"/>.
    /// <paramref name="owner"/> scopes the voice: voices owned by a world are stopped when that
    /// world is cleared, while a null owner means the voice outlives worlds (UI, editor preview).
    /// Returns <see cref="AudioVoiceHandle.None"/> when the backend has no voice left; that is a
    /// normal outcome under load, not an error.
    /// </summary>
    public AudioVoiceHandle PlayClip(IAudioClip clip, string busName, in AudioVoiceParameters parameters, object owner = null)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (_isDisposed)
        {
            return AudioVoiceHandle.None;
        }

        var gain = Mixer.GetEffectiveGain(busName);
        var backendParameters = parameters.WithVolume(parameters.Volume * gain);

        var handle = _backend.Play(clip, backendParameters);
        if (!handle.IsValid)
        {
            RefusedVoiceCount++;
            _refusedVoiceLog.WriteWarning("Audio: a sound was refused, no voice left on the backend.");
            return AudioVoiceHandle.None;
        }

        var entry = GetOrCreateEntry(handle.Index);
        entry.Handle = handle;
        entry.BusName = busName;
        entry.BaseParameters = parameters;
        entry.Owner = owner;
        entry.InUse = true;
        ActiveVoiceCount++;

        return handle;
    }

    /// <summary>Stops a voice and returns it to the backend. A stale handle is ignored.</summary>
    public void Stop(AudioVoiceHandle voice)
    {
        if (!TryGetEntry(voice, out var entry))
        {
            return;
        }

        _backend.Stop(voice);
        ReleaseEntry(entry);
    }

    /// <summary>True while the voice exists and is not stopped.</summary>
    public bool IsPlaying(AudioVoiceHandle voice)
    {
        return TryGetEntry(voice, out _) && _backend.GetState(voice) == AudioVoiceState.Playing;
    }

    public bool IsAlive(AudioVoiceHandle voice) => TryGetEntry(voice, out _);

    public void Pause(AudioVoiceHandle voice)
    {
        if (TryGetEntry(voice, out _))
        {
            _backend.Pause(voice);
        }
    }

    public void Resume(AudioVoiceHandle voice)
    {
        if (TryGetEntry(voice, out _))
        {
            _backend.Resume(voice);
        }
    }

    /// <summary>
    /// Sets the volume the caller asked for, before the bus gain. The value actually applied is
    /// this one multiplied by the effective gain of the voice bus.
    /// </summary>
    public void SetVoiceVolume(AudioVoiceHandle voice, float volume)
    {
        if (!TryGetEntry(voice, out var entry))
        {
            return;
        }

        entry.BaseParameters = entry.BaseParameters.WithVolume(volume);
        ApplyGain(entry);
    }

    /// <summary>Volume asked for by the caller, before the bus gain. Zero for a stale handle.</summary>
    public float GetVoiceVolume(AudioVoiceHandle voice)
    {
        return TryGetEntry(voice, out var entry) ? entry.BaseParameters.Volume : 0f;
    }

    /// <summary>Bus the voice is routed to, or null for a stale handle.</summary>
    public string GetVoiceBus(AudioVoiceHandle voice)
    {
        return TryGetEntry(voice, out var entry) ? entry.BusName : null;
    }

    /// <summary>Stops every voice started with that owner. Used when a world is cleared.</summary>
    public void StopVoicesOwnedBy(object owner)
    {
        for (var i = 0; i < _voices.Count; i++)
        {
            var entry = _voices[i];
            if (!entry.InUse || !ReferenceEquals(entry.Owner, owner))
            {
                continue;
            }

            _backend.Stop(entry.Handle);
            ReleaseEntry(entry);
        }
    }

    /// <summary>Stops every voice, whoever owns it.</summary>
    public void StopAll()
    {
        for (var i = 0; i < _voices.Count; i++)
        {
            var entry = _voices[i];
            if (!entry.InUse)
            {
                continue;
            }

            _backend.Stop(entry.Handle);
            entry.Reset();
        }

        _backend.StopAll();
        ActiveVoiceCount = 0;
    }

    /// <summary>
    /// Per-frame maintenance: recycles the voices that finished, and reapplies the bus gains when
    /// the mixer changed. Allocation free.
    /// </summary>
    public void Update(float elapsedSeconds)
    {
        if (_isDisposed)
        {
            return;
        }

        var mixerChanged = _appliedMixerVersion != Mixer.Version;

        for (var i = 0; i < _voices.Count; i++)
        {
            var entry = _voices[i];
            if (!entry.InUse)
            {
                continue;
            }

            if (_backend.GetState(entry.Handle) == AudioVoiceState.Stopped)
            {
                ReleaseEntry(entry);
                continue;
            }

            if (mixerChanged)
            {
                ApplyGain(entry);
            }
        }

        if (mixerChanged)
        {
            _appliedMixerVersion = Mixer.Version;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        StopAll();
        _isDisposed = true;
        _backend.Dispose();
    }

    private void ApplyGain(VoiceEntry entry)
    {
        _backend.SetVolume(entry.Handle, entry.BaseParameters.Volume * Mixer.GetEffectiveGain(entry.BusName));
    }

    private VoiceEntry GetOrCreateEntry(int index)
    {
        while (_voices.Count <= index)
        {
            _voices.Add(new VoiceEntry());
        }

        return _voices[index];
    }

    private bool TryGetEntry(AudioVoiceHandle voice, out VoiceEntry entry)
    {
        entry = null;

        if (_isDisposed || !voice.IsValid || voice.Index >= _voices.Count)
        {
            return false;
        }

        var candidate = _voices[voice.Index];
        if (!candidate.InUse || candidate.Handle != voice)
        {
            return false;
        }

        entry = candidate;
        return true;
    }

    private void ReleaseEntry(VoiceEntry entry)
    {
        if (!entry.InUse)
        {
            return;
        }

        _backend.Release(entry.Handle);
        entry.Reset();
        ActiveVoiceCount--;
    }

    // Class rather than struct: entries are mutated in place through the list, and a struct
    // would force a copy back on every change.
    private sealed class VoiceEntry
    {
        public AudioVoiceHandle Handle;
        public string BusName;
        public AudioVoiceParameters BaseParameters;
        public object Owner;
        public bool InUse;

        public void Reset()
        {
            Handle = AudioVoiceHandle.None;
            BusName = null;
            Owner = null;
            InUse = false;
        }
    }
}
