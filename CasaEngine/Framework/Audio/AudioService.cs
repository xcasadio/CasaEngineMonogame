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
    private readonly AudioLogThrottle _missingClipLog = new();

    private int _appliedMixerVersion = -1;
    private bool _isDisposed;

    public AudioService(IAudioBackend backend, AudioMixer mixer = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        Mixer = mixer ?? AudioBusNames.CreateDefaultMixer();
    }

    public IAudioBackend Backend => _backend;

    public AudioMixer Mixer { get; }

    /// <summary>
    /// Resolves the audio file a <see cref="SoundAsset"/> points at. Null until the host wires
    /// the asset content manager; playing a sound then logs and stays silent.
    /// </summary>
    public IAudioClipProvider ClipProvider { get; set; }

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

    /// <summary>
    /// Plays a <see cref="SoundAsset"/> with its authored volume, pitch, loop flag and bus.
    /// </summary>
    /// <remarks>
    /// A missing audio file, an unresolvable clip or a saturated backend all end the same way:
    /// a throttled log and <see cref="AudioVoiceHandle.None"/>. Gameplay code never has to guard
    /// against a broken sound asset.
    /// Streaming assets are refused here; they go through the music player instead.
    /// </remarks>
    public AudioVoiceHandle PlaySound(SoundAsset asset, object owner = null)
    {
        return PlaySound(asset, SoundPlaybackOverrides.None, owner);
    }

    public AudioVoiceHandle PlaySound(SoundAsset asset, in SoundPlaybackOverrides overrides, object owner = null)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (_isDisposed)
        {
            return AudioVoiceHandle.None;
        }

        if (asset.IsStreaming)
        {
            _missingClipLog.WriteWarning(
                $"Audio: sound '{asset.Name}' is marked as streaming and cannot be played as a sound effect.");
            return AudioVoiceHandle.None;
        }

        var clip = ResolveClip(asset);
        if (clip == null)
        {
            return AudioVoiceHandle.None;
        }

        var parameters = overrides.ApplyTo(asset.CreateVoiceParameters());
        var busName = overrides.ResolveBus(asset.BusName);

        return PlayClip(clip, busName, parameters, owner);
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

    /// <summary>
    /// Ramps the voice volume to <paramref name="targetVolume"/> over
    /// <paramref name="durationSeconds"/>. MonoGame has no native fade, so the ramp is advanced
    /// by <see cref="Update"/>. A duration of zero applies the target immediately.
    /// Starting a second fade on the same voice replaces the first one, starting from the volume
    /// reached so far, so chained fades never jump.
    /// </summary>
    public void FadeVoice(
        AudioVoiceHandle voice,
        float targetVolume,
        float durationSeconds,
        AudioFadeCompletion completion = AudioFadeCompletion.None)
    {
        if (!TryGetEntry(voice, out var entry))
        {
            return;
        }

        var target = float.IsNaN(targetVolume)
            ? entry.BaseParameters.Volume
            : Math.Clamp(targetVolume, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);

        if (durationSeconds <= 0f || float.IsNaN(durationSeconds))
        {
            entry.IsFading = false;
            entry.BaseParameters = entry.BaseParameters.WithVolume(target);
            ApplyGain(entry);

            if (completion == AudioFadeCompletion.Stop)
            {
                _backend.Stop(entry.Handle);
                ReleaseEntry(entry);
            }

            return;
        }

        entry.IsFading = true;
        entry.FadeStartVolume = entry.BaseParameters.Volume;
        entry.FadeTargetVolume = target;
        entry.FadeDuration = durationSeconds;
        entry.FadeElapsed = 0f;
        entry.FadeCompletion = completion;
    }

    /// <summary>Fades the voice out and releases it once silent.</summary>
    public void StopWithFade(AudioVoiceHandle voice, float durationSeconds)
    {
        FadeVoice(voice, AudioVoiceParameters.MinVolume, durationSeconds, AudioFadeCompletion.Stop);
    }

    /// <summary>Leaves the voice at the volume reached so far.</summary>
    public void CancelFade(AudioVoiceHandle voice)
    {
        if (TryGetEntry(voice, out var entry))
        {
            entry.IsFading = false;
        }
    }

    public bool IsFading(AudioVoiceHandle voice)
    {
        return TryGetEntry(voice, out var entry) && entry.IsFading;
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

            if (entry.IsFading)
            {
                if (AdvanceFade(entry, elapsedSeconds))
                {
                    continue;
                }

                // The ramp already pushed the current gain to the backend this frame.
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

    /// <summary>
    /// Advances one volume ramp and applies it. Returns true when the voice was released,
    /// so the caller must move on to the next entry.
    /// </summary>
    private bool AdvanceFade(VoiceEntry entry, float elapsedSeconds)
    {
        entry.FadeElapsed += elapsedSeconds;

        var progress = entry.FadeElapsed >= entry.FadeDuration
            ? 1f
            : entry.FadeElapsed / entry.FadeDuration;

        var volume = entry.FadeStartVolume + ((entry.FadeTargetVolume - entry.FadeStartVolume) * progress);
        entry.BaseParameters = entry.BaseParameters.WithVolume(volume);
        ApplyGain(entry);

        if (progress < 1f)
        {
            return false;
        }

        entry.IsFading = false;

        if (entry.FadeCompletion != AudioFadeCompletion.Stop)
        {
            return false;
        }

        _backend.Stop(entry.Handle);
        ReleaseEntry(entry);
        return true;
    }

    private IAudioClip ResolveClip(SoundAsset asset)
    {
        if (asset.AudioFileAssetId == Guid.Empty)
        {
            _missingClipLog.WriteWarning($"Audio: sound '{asset.Name}' references no audio file.");
            return null;
        }

        if (ClipProvider == null)
        {
            _missingClipLog.WriteWarning(
                $"Audio: no clip provider is wired, sound '{asset.Name}' cannot be played.");
            return null;
        }

        var clip = ClipProvider.GetClip(asset.AudioFileAssetId);
        if (clip is not { IsDisposed: false })
        {
            _missingClipLog.WriteWarning(
                $"Audio: the audio file of sound '{asset.Name}' ({asset.AudioFileAssetId}) could not be loaded.");
            return null;
        }

        return clip;
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

        public bool IsFading;
        public float FadeStartVolume;
        public float FadeTargetVolume;
        public float FadeDuration;
        public float FadeElapsed;
        public AudioFadeCompletion FadeCompletion;

        public void Reset()
        {
            Handle = AudioVoiceHandle.None;
            BusName = null;
            Owner = null;
            InUse = false;
            IsFading = false;
            FadeStartVolume = 0f;
            FadeTargetVolume = 0f;
            FadeDuration = 0f;
            FadeElapsed = 0f;
            FadeCompletion = AudioFadeCompletion.None;
        }
    }
}
