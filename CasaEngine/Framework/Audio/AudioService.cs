using System.Numerics;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Psx;
using CasaEngine.Framework.Audio.Spatial;
using CasaEngine.Framework.Audio.Streaming;

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
/// the backend is that base volume multiplied by the effective gain of its bus, except on a backend with
/// <see cref="IAudioBusBackend"/> (the software backend): there each voice is routed to its bus, the backend mixes
/// the bus graph itself and the volume sent is the base volume alone.
/// </remarks>
public sealed class AudioService : IDisposable
{
    private readonly IAudioBackend _backend;
    private readonly IAudioBusBackend _busBackend;
    private readonly IAudioMeteringBackend _meteringBackend;
    private readonly IAudioVoiceModulationBackend _modulationBackend;
    private readonly AudioLogThrottle _meteringLog = new();
    // Per bus of Mixer.Buses (same order): index on the backend, or -1 when it is not on it (routed to Master).
    private readonly List<int> _backendBusIndices = new();
    private int _syncedBusVersion = -1;
    // Per bus of Mixer.Buses (same order): the insert effects already sent to the backend, in order.
    private readonly List<List<AudioEffect>> _sentEffects = new();
    private int _syncedEffectsVersion;
    private readonly AudioLogThrottle _effectsLog = new();
    private readonly AudioLogThrottle _duckingLog = new();
    // Per bus of Mixer.Buses (same order): the sends already sent to the backend.
    private readonly List<List<AudioBusSend>> _sentSends = new();
    private int _syncedSendsVersion;
    private readonly AudioLogThrottle _sendsLog = new();
    private readonly AudioLogThrottle _limiterLog = new();
    private readonly LimiterEffect _masterLimiter = new();
    private bool _masterLimiterSent;
    private readonly List<VoiceEntry> _voices = new();
    private long _voiceStartCounter;
    private readonly List<BusFade> _busFades = new();
    private readonly AudioLogThrottle _refusedVoiceLog = new();
    private readonly AudioLogThrottle _missingClipLog = new();
    private readonly AudioLogThrottle _spuLog = new();
    private readonly StereoVoiceMixer _stereoVoiceMixer;

    // Send thresholds of the per-voice modulation (plan decision P44): only a change beyond them is sent, so a still sound
    // costs nothing and the error left on the backend never exceeds them.
    private const float GainSendThreshold = 0.001f;
    private const float PanSendThreshold = 0.002f;
    private const float RateSendThreshold = 0.0005f;

    // Below this elapsed time a velocity cannot be derived from a displacement: the last one is kept (plan decision P37).
    private const float MinVelocityElapsed = 1e-6f;

    // Listener stack (plan decision P35): the last registered listener is the active one. Slots are recycled through the
    // pool, so a registration after the first ones allocates nothing.
    private readonly List<ListenerSlot> _listeners = new(8);
    private readonly List<ListenerSlot> _listenerPool = new(8);
    private readonly AudioLogThrottle _listenerLog = new();
    // The pose the voices spatialize against this frame, read from the stack at the start of Update.
    private AudioListenerPose _activePose = AudioListenerPose.Default;
    private bool _hasActiveListener;
    // Velocity of the active listener, derived from the poses pushed in the last two frames (plan decision P37).
    private Vector3 _activeVelocity;
    // Elapsed time of the current Update, read by the voices to derive their own velocity.
    private float _frameElapsed;
    private float _speedOfSound = AudioDoppler.DefaultSpeedOfSound;

    // Game parameters (plan decision P39) and the scratch indices a start resolves its bindings into before the voice exists.
    private readonly AudioGameParameterRegistry _gameParameters = new();
    private readonly int[] _startBindingIndices = new int[SoundAsset.MaxParameterBindings];

    private PsxSpuPort _spuPort;
    private string _spuBusName;
    private int _appliedMixerVersion = -1;
    private bool _isDisposed;
    private Random _variationRandom = Random.Shared;

    public AudioService(IAudioBackend backend, AudioMixer mixer = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        Mixer = mixer ?? AudioBusNames.CreateDefaultMixer();
        _busBackend = _backend as IAudioBusBackend;
        _meteringBackend = _backend as IAudioMeteringBackend;
        _modulationBackend = _backend as IAudioVoiceModulationBackend;
        SyncBuses();
        SyncEffects();
        SyncSends();
        SendMasterLimiter();
        // Real backends stream music from the background worker; test and null backends read inline.
        Music = new MusicPlayer(this, _backend is Backends.SoftwareAudioBackend or Backends.MonoGameAudioBackend);
        _stereoVoiceMixer = new StereoVoiceMixer(this);
    }

    public IAudioBackend Backend => _backend;

    /// <summary>True when the backend measures bus levels on its audio thread (<see cref="IAudioMeteringBackend"/>).</summary>
    public bool IsMeteringAvailable => _meteringBackend != null;

    /// <summary>
    /// Index to read the level of the bus called <paramref name="busName"/> at in <see cref="TryReadLevels"/>. False when the
    /// bus does not exist, when it is beyond the backend capacity (it is mixed into Master, but has no level of its own), or
    /// when the backend has no metering (one throttled line says so). The Master bus is index 0.
    /// </summary>
    public bool TryGetMeterBusIndex(string busName, out int index)
    {
        index = -1;

        if (!CheckMetering())
        {
            return false;
        }

        SyncBuses();

        if (!Mixer.TryGetBus(busName, out var bus))
        {
            return false;
        }

        index = BackendIndexOf(bus);
        return index >= 0;
    }

    /// <summary>
    /// Reads the levels published since <paramref name="cursor"/> (see <see cref="IAudioMeteringBackend.ReadLevels"/>): the
    /// bus levels in <paramref name="buses"/> by the index <see cref="TryGetMeterBusIndex"/> gives, and the output. False,
    /// with everything zero, when the backend has no metering (one throttled line says so). Allocation free.
    /// </summary>
    public bool TryReadLevels(ref AudioMeterCursor cursor, Span<AudioLevel> buses, out AudioLevel output, out AudioMeterRead read)
    {
        if (!CheckMetering())
        {
            buses.Clear();
            output = default;
            read = default;
            return false;
        }

        read = _meteringBackend.ReadLevels(ref cursor, buses, out output);
        return true;
    }

    private bool CheckMetering()
    {
        if (_meteringBackend != null)
        {
            return true;
        }

        if (_meteringLog.ShouldWrite())
        {
            _meteringLog.WriteNow("Audio: this backend does not measure levels, so bus meters are unavailable (use the software backend).");
        }

        return false;
    }

    public AudioMixer Mixer { get; }

    /// <summary>
    /// The limiter of the Master output (plan decision P20): a feed-forward limiter with a -1 dBFS ceiling, on by default under
    /// the software backend, running after the Master gain and before the hard clip. Tune it or set
    /// <see cref="LimiterEffect.IsEnabled"/> to false; the object belongs to this service (do not insert it on a bus). Without
    /// <see cref="IAudioBusBackend"/> the limiter is absent: changing it has no effect, and one throttled line says so.
    /// </summary>
    public LimiterEffect MasterLimiter
    {
        get
        {
            if (_busBackend == null && _limiterLog.ShouldWrite())
            {
                _limiterLog.WriteNow("Audio: this backend has no bus graph, so the master limiter is absent (use the software backend).");
            }

            return _masterLimiter;
        }
    }

    /// <summary>Streamed playback: music and ambiences, with fades and crossfade.</summary>
    public MusicPlayer Music { get; }

    /// <summary>
    /// Resolves the audio file a <see cref="SoundAsset"/> points at. Null until the host wires
    /// the asset content manager; playing a sound then logs and stays silent.
    /// </summary>
    public IAudioClipProvider ClipProvider { get; set; }

    /// <summary>
    /// Random source of the variations drawn by <see cref="PlaySound(SoundAsset, in SoundPlaybackOverrides, object)"/>.
    /// Game thread only. Injectable for tests or a deterministic replay; null restores <see cref="Random.Shared"/>.
    /// </summary>
    public Random VariationRandom
    {
        get => _variationRandom;
        set => _variationRandom = value ?? Random.Shared;
    }

    public bool IsAudioAvailable => _backend.IsAvailable;

    /// <summary>Number of voices the service currently tracks (playing or paused).</summary>
    public int ActiveVoiceCount { get; private set; }

    /// <summary>Number of Play calls refused since creation, because the backend had no voice left.</summary>
    public int RefusedVoiceCount { get; private set; }

    /// <summary>
    /// Number of voices stopped since creation to make room for a <see cref="PlaySound(SoundAsset, in SoundPlaybackOverrides, object)"/>
    /// of a higher priority. A steal whose Play is then refused by the backend is not counted here, only in
    /// <see cref="RefusedVoiceCount"/>: the victim is lost.
    /// </summary>
    public int StolenVoiceCount { get; private set; }

    /// <summary>
    /// Starts <paramref name="clip"/> on <paramref name="busName"/>.
    /// <paramref name="owner"/> scopes the voice: voices owned by a world are stopped when that
    /// world is cleared, while a null owner means the voice outlives worlds (UI, editor preview).
    /// Returns <see cref="AudioVoiceHandle.None"/> when the backend has no voice left; that is a
    /// normal outcome under load, not an error. The voice has no priority: it never steals a voice when
    /// the backend is full, and it can never be stolen.
    /// </summary>
    public AudioVoiceHandle PlayClip(IAudioClip clip, string busName, in AudioVoiceParameters parameters, object owner = null)
    {
        return PlayClipCore(clip, busName, parameters, owner, 0, default);
    }

    private AudioVoiceHandle PlayClipCore(
        IAudioClip clip,
        string busName,
        in AudioVoiceParameters parameters,
        object owner,
        int priority,
        in VoiceModulationStart start)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (_isDisposed)
        {
            return AudioVoiceHandle.None;
        }

        var stole = priority > 0 && TryFreeVoiceFor(priority);

        // The values the voice starts with: neutral without a spatial mode or without a listener, the spatialized ones
        // otherwise. They travel with the start, so the voice never sounds first at full gain (plan decision P32).
        var mode = start.Asset != null && start.HasPosition ? start.Asset.SpatialMode : AudioSpatialMode.None;
        var startGain = 1f;
        var startPan = float.NaN;
        var startRate = 1f;

        if (mode != AudioSpatialMode.None && _listeners.Count > 0)
        {
            EvaluateSpatial(
                _listeners[^1].Pose, mode, start.Asset.DistanceModel, start.Asset.ReferenceDistance, start.Asset.MaxDistance,
                start.Asset.RolloffFactor, start.Position, out startGain, out startPan);
        }

        // The bound parameters are part of the start values too: never a ramp from full gain (plan decision P39).
        var bindingCount = start.Asset != null ? ResolveBindings(start.Asset, _startBindingIndices) : 0;
        if (bindingCount > 0)
        {
            EvaluateBindings(start.Asset.ParameterBindings, _startBindingIndices, bindingCount, out var bindingVolume, out startRate);
            startGain *= bindingVolume;
        }

        var hasCapability = _modulationBackend != null;
        var volume = BackendVolume(parameters.Volume, busName);
        var backendParameters = parameters.WithVolume(hasCapability ? volume : volume * startGain);

        if (!hasCapability && !float.IsNaN(startPan))
        {
            backendParameters = backendParameters.WithPan(startPan);
        }

        if (!hasCapability && startRate != 1f)
        {
            backendParameters = backendParameters.WithPitch(FoldPitch(parameters.Pitch, startRate));
        }

        RouteNextVoice(busName);

        // The backend takes these at the top of its start, refused or not: a refused Play leaves nothing for the next voice.
        if (hasCapability && (startGain != 1f || !float.IsNaN(startPan) || startRate != 1f))
        {
            _modulationBackend.SetNextVoiceModulation(startGain, startPan, startRate);
        }

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
        entry.Priority = priority;
        entry.StartSequence = ++_voiceStartCounter;
        WriteStartModulation(entry, in start, mode, hasCapability, startGain, startPan, startRate);
        if (bindingCount > 0)
        {
            AttachBindings(entry, start.Asset, _startBindingIndices, bindingCount);
        }

        ActiveVoiceCount++;

        if (stole)
        {
            StolenVoiceCount++;
        }

        return handle;
    }

    /// <summary>
    /// Writes every modulation field of a started entry. Every start path calls it (or
    /// <see cref="WriteNeutralModulation"/>): <see cref="VoiceEntry.Reset"/> leaves the folded gain at zero on purpose, so a
    /// path that forgot would be heard at once.
    /// </summary>
    private static void WriteStartModulation(
        VoiceEntry entry,
        in VoiceModulationStart start,
        AudioSpatialMode mode,
        bool hasCapability,
        float startGain,
        float startPan,
        float startRate)
    {
        WriteNeutralModulation(entry);

        if (start.Asset != null)
        {
            entry.AssetSpatialMode = start.Asset.SpatialMode;
            entry.DistanceModel = start.Asset.DistanceModel;
            entry.ReferenceDistance = start.Asset.ReferenceDistance;
            entry.MaxDistance = start.Asset.MaxDistance;
            entry.RolloffFactor = start.Asset.RolloffFactor;
        }

        entry.SpatialMode = mode;
        entry.HasModulation = mode != AudioSpatialMode.None;
        entry.HasPosition = start.HasPosition;
        entry.Position = start.Position;
        entry.DopplerFactor = start.Asset?.DopplerFactor ?? 0f;
        // A position given at the start is the first pose of the voice: the next push is measured against it.
        entry.PositionPushedThisFrame = start.HasPosition;
        entry.SentGain = startGain;
        entry.SentPan = startPan;
        entry.SentRate = startRate;
        entry.FoldedGain = hasCapability ? 1f : startGain;
        entry.FoldedPan = hasCapability ? float.NaN : startPan;
        entry.FoldedPitchOffset = hasCapability ? 0f : MathF.Log2(startRate);
    }

    /// <summary>The modulation of a voice that is not spatial: nothing sent, nothing folded.</summary>
    private static void WriteNeutralModulation(VoiceEntry entry)
    {
        entry.HasModulation = false;
        entry.SpatialMode = AudioSpatialMode.None;
        entry.AssetSpatialMode = AudioSpatialMode.None;
        entry.DistanceModel = AudioDistanceModel.None;
        entry.ReferenceDistance = 1f;
        entry.MaxDistance = float.MaxValue;
        entry.RolloffFactor = 1f;
        entry.Position = Vector3.Zero;
        entry.HasPosition = false;
        entry.DopplerFactor = 0f;
        entry.PreviousPosition = Vector3.Zero;
        entry.HasPreviousPosition = false;
        entry.Velocity = Vector3.Zero;
        entry.PositionPushedThisFrame = false;
        entry.SentGain = 1f;
        entry.SentPan = float.NaN;
        entry.SentRate = 1f;
        entry.FoldedGain = 1f;
        entry.FoldedPan = float.NaN;
        entry.FoldedPitchOffset = 0f;
        entry.Bindings = null;
        entry.BindingCount = 0;
        entry.BindingsStale = false;
        entry.BindingVolumeFactor = 1f;
        entry.BindingRateFactor = 1f;
    }

    private static float FoldPitch(float basePitch, float rate)
    {
        return Math.Clamp(basePitch + MathF.Log2(rate), AudioVoiceParameters.MinPitch, AudioVoiceParameters.MaxPitch);
    }

    /// <summary>Resolves the parameter names of the bindings of <paramref name="asset"/> into registry indices (-1 when the registry is full). Returns the bound count.</summary>
    private int ResolveBindings(SoundAsset asset, int[] indices)
    {
        var bindings = asset.ParameterBindings;
        var count = Math.Min(bindings.Count, indices.Length);
        for (var i = 0; i < count; i++)
        {
            indices[i] = _gameParameters.GetOrCreateIndex(bindings[i].ParameterName);
        }

        return count;
    }

    /// <summary>
    /// Volume factor (product of the written Volume bindings) and speed ratio (2 to the sum, bounded to one octave, of the
    /// written Pitch bindings). A parameter never written is neutral. Allocation free.
    /// </summary>
    private void EvaluateBindings(
        IReadOnlyList<AudioParameterBinding> bindings,
        int[] indices,
        int count,
        out float volumeFactor,
        out float rateFactor)
    {
        volumeFactor = 1f;
        var pitchSum = 0f;

        for (var i = 0; i < count; i++)
        {
            var index = indices[i];
            if (index < 0)
            {
                continue;
            }

            var input = _gameParameters.Get(index);
            if (float.IsNaN(input))
            {
                continue;
            }

            var binding = bindings[i];
            var output = binding.Evaluate(input);
            if (binding.Target == AudioParameterTarget.Volume)
            {
                volumeFactor *= output;
            }
            else
            {
                pitchSum += output;
            }
        }

        rateFactor = pitchSum == 0f ? 1f : MathF.Pow(2f, Math.Clamp(pitchSum, -1f, 1f));
    }

    /// <summary>Binds the parameters of <paramref name="asset"/> to a voice: indices, versions and the first factors. The next <see cref="UpdateModulation"/> recomputes them regardless.</summary>
    private void AttachBindings(VoiceEntry entry, SoundAsset asset, int[] indices, int count)
    {
        entry.Bindings = asset.ParameterBindings;
        entry.BindingCount = count;
        Array.Copy(indices, entry.BindingIndices, count);
        SnapshotBindingVersions(entry);
        EvaluateBindings(entry.Bindings, entry.BindingIndices, count, out entry.BindingVolumeFactor, out entry.BindingRateFactor);
        entry.BindingsStale = true;
        entry.HasModulation = true;
    }

    private void SnapshotBindingVersions(VoiceEntry entry)
    {
        for (var i = 0; i < entry.BindingCount; i++)
        {
            entry.BindingVersions[i] = _gameParameters.GetVersion(entry.BindingIndices[i]);
        }
    }

    /// <summary>Recomputes the binding factors of a voice when a bound parameter changed, or on the first call after the binding.</summary>
    private void RefreshBindingFactors(VoiceEntry entry)
    {
        var changed = entry.BindingsStale;
        for (var i = 0; !changed && i < entry.BindingCount; i++)
        {
            changed = _gameParameters.GetVersion(entry.BindingIndices[i]) != entry.BindingVersions[i];
        }

        if (!changed)
        {
            return;
        }

        entry.BindingsStale = false;
        SnapshotBindingVersions(entry);
        EvaluateBindings(entry.Bindings, entry.BindingIndices, entry.BindingCount, out entry.BindingVolumeFactor, out entry.BindingRateFactor);
    }

    /// <summary>
    /// Binds the game parameters of a streaming <paramref name="asset"/> to a music voice created by <see cref="PlayStream"/>
    /// and not started yet: the factor and the speed ratio of the bindings become the starting values of the voice (never a
    /// ramp from full gain). The music player calls it between the creation and <see cref="StartVoice"/>; tracks are not
    /// spatialized.
    /// </summary>
    internal void BindSoundParameters(AudioVoiceHandle voice, SoundAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!TryGetEntry(voice, out var entry) || !entry.IsStreaming)
        {
            return;
        }

        var count = ResolveBindings(asset, _startBindingIndices);
        if (count == 0)
        {
            return;
        }

        AttachBindings(entry, asset, _startBindingIndices, count);

        var gain = entry.BindingVolumeFactor;
        var rate = entry.BindingRateFactor;
        entry.SentGain = gain;
        entry.SentRate = rate;

        if (gain == 1f && rate == 1f)
        {
            return;
        }

        if (_modulationBackend != null)
        {
            // Before the start, this is the starting value of the voice.
            _modulationBackend.SetVoiceModulation(entry.Handle, gain, float.NaN, rate);
            return;
        }

        entry.FoldedGain = gain;
        entry.FoldedPitchOffset = MathF.Log2(rate);
        _backend.SetParameters(entry.Handle, BuildBackendParameters(entry));
    }

    /// <summary>
    /// Index of the game parameter <paramref name="name"/> (case-insensitive), created on first use. Resolve it once and
    /// use <see cref="SetGameParameter(int, float)"/> in hot paths. -1 for an empty name or when the 64 slots are taken.
    /// A parameter never written is neutral for the sounds bound to it. Game thread only.
    /// </summary>
    public int GetGameParameterIndex(string name)
    {
        return _gameParameters.GetOrCreateIndex(name);
    }

    /// <summary>
    /// Writes a game parameter by index, the allocation free way. The sounds bound to it follow at the next
    /// <see cref="Update"/>. An invalid index is ignored. Game thread only.
    /// </summary>
    public void SetGameParameter(int index, float value)
    {
        _gameParameters.Set(index, value);
    }

    /// <summary>
    /// Writes a game parameter by name, creating it on first use (a linear search, no allocation once the name exists;
    /// prefer the index overload in hot paths). Game thread only.
    /// </summary>
    public void SetGameParameter(string name, float value)
    {
        _gameParameters.Set(_gameParameters.GetOrCreateIndex(name), value);
    }

    /// <summary>The value of a game parameter; NaN when it was never written (a sound bound to it is then neutral) or the index is invalid. Game thread only.</summary>
    public float GetGameParameter(int index)
    {
        return _gameParameters.Get(index);
    }

    /// <summary>
    /// Makes room for a sound of <paramref name="priority"/> when the backend is full. A voice that already
    /// finished but was not recycled yet is released (not a steal, returns false). Otherwise the victim is the
    /// voice with the lowest priority strictly below <paramref name="priority"/>, the oldest on a tie; voices
    /// without priority, streamed voices and backend stereo voices are never victims. Paused and fading voices
    /// are eligible. Returns true only when a voice was stopped.
    /// </summary>
    private bool TryFreeVoiceFor(int priority)
    {
        if (!_backend.IsAvailable || _backend.ActiveVoiceCount < _backend.VoiceCapacity)
        {
            return false;
        }

        var releasedFinished = false;
        for (var i = 0; i < _voices.Count; i++)
        {
            var entry = _voices[i];
            if (entry.InUse && !entry.IsStreaming && _backend.GetState(entry.Handle) == AudioVoiceState.Stopped)
            {
                ReleaseEntry(entry);
                releasedFinished = true;
            }
        }

        if (releasedFinished)
        {
            return false;
        }

        VoiceEntry victim = null;
        for (var i = 0; i < _voices.Count; i++)
        {
            var entry = _voices[i];
            if (!entry.InUse || entry.IsStreaming || entry.IsBackendStereo || entry.Priority <= 0 || entry.Priority >= priority)
            {
                continue;
            }

            if (victim == null
                || entry.Priority < victim.Priority
                || (entry.Priority == victim.Priority && entry.StartSequence < victim.StartSequence))
            {
                victim = entry;
            }
        }

        if (victim == null)
        {
            return false;
        }

        _backend.Stop(victim.Handle);
        ReleaseEntry(victim);
        return true;
    }

    /// <summary>
    /// Plays a <see cref="SoundAsset"/> with its authored volume, pitch, loop flag and bus.
    /// </summary>
    /// <remarks>
    /// A missing audio file, an unresolvable clip or a saturated backend all end the same way:
    /// a throttled log and <see cref="AudioVoiceHandle.None"/>. Gameplay code never has to guard
    /// against a broken sound asset.
    /// Streaming assets are refused here; they go through the music player instead, and have no variation draw.
    /// <para/>
    /// Composition of the parameters: the overrides replace the asset values, then the variation draw of the
    /// asset (file, volume factor, pitch offset) applies on top, and <see cref="AudioVoiceParameters"/> clamps
    /// the result. One draw per play: a looped voice keeps its draw. An asset without variation never calls
    /// <see cref="VariationRandom"/>. If the drawn file cannot be loaded, nothing plays (no fallback).
    /// <para/>
    /// Priority: when every voice is taken, a sound without priority (0) is refused. A sound with a priority
    /// (<see cref="SoundAsset.Priority"/>, or <see cref="SoundPlaybackOverrides.Priority"/>) stops the voice of
    /// strictly lower priority (lowest first, oldest on a tie) and takes its place. A voice without priority,
    /// a streamed voice and a backend stereo voice are never stolen. Stealing counts in
    /// <see cref="StolenVoiceCount"/>; there are no virtual voices.
    /// </remarks>
    public AudioVoiceHandle PlaySound(SoundAsset asset, object owner = null)
    {
        return PlaySound(asset, SoundPlaybackOverrides.None, owner);
    }

    public AudioVoiceHandle PlaySound(SoundAsset asset, in SoundPlaybackOverrides overrides, object owner = null)
    {
        return PlaySoundCore(asset, overrides, owner, false, default);
    }

    /// <summary>
    /// <see cref="PlaySound(SoundAsset, in SoundPlaybackOverrides, object)"/> at a world position: a sound whose
    /// <see cref="SoundAsset.SpatialMode"/> is not <see cref="AudioSpatialMode.None"/> is attenuated by the distance to the
    /// active listener and panned by its direction (plan decisions P35, P36, P38). Without a listener the voice plays
    /// neutral and is spatialized by the <see cref="Update"/> that follows the registration of one. Move it with
    /// <see cref="SetVoicePosition"/>. Same refusals, variation draw and priority as
    /// <see cref="PlaySound(SoundAsset, in SoundPlaybackOverrides, object)"/>.
    /// </summary>
    public AudioVoiceHandle PlaySoundAt(SoundAsset asset, Vector3 position, in SoundPlaybackOverrides overrides, object owner = null)
    {
        return PlaySoundCore(asset, overrides, owner, true, position);
    }

    private AudioVoiceHandle PlaySoundCore(SoundAsset asset, in SoundPlaybackOverrides overrides, object owner, bool hasPosition, Vector3 position)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (_isDisposed)
        {
            return AudioVoiceHandle.None;
        }

        if (asset.IsStreaming)
        {
            if (_missingClipLog.ShouldWrite())
            {
                _missingClipLog.WriteNow($"Audio: sound '{asset.Name}' is marked as streaming and cannot be played as a sound effect.");
            }

            return AudioVoiceHandle.None;
        }

        var draw = SoundVariation.Draw(asset, _variationRandom);

        var clip = ResolveClip(asset, draw.AudioFileAssetId);
        if (clip == null)
        {
            return AudioVoiceHandle.None;
        }

        var parameters = draw.ApplyTo(overrides.ApplyTo(asset.CreateVoiceParameters()));
        var busName = overrides.ResolveBus(asset.BusName);

        return PlayClipCore(clip, busName, parameters, owner, overrides.ResolvePriority(asset.Priority), new VoiceModulationStart(asset, hasPosition, position));
    }

    /// <summary>
    /// Allocates a voice fed buffer by buffer instead of playing a resident clip. The voice is
    /// not started: queue a few buffers first with <see cref="SubmitStreamBuffer"/>, then call
    /// <see cref="StartVoice"/>, so it does not starve on its first frame.
    /// </summary>
    /// <remarks>
    /// A streaming voice takes part in the buses, the fades and the ownership like any other, but
    /// it is never recycled by <see cref="Update"/> when it stops: only its feeder knows whether
    /// the silence means the end of the stream or a temporary underrun.
    /// </remarks>
    public AudioVoiceHandle PlayStream(
        int sampleRate,
        int channelCount,
        string busName,
        in AudioVoiceParameters parameters,
        object owner = null)
    {
        if (_isDisposed || !_backend.SupportsStreaming)
        {
            return AudioVoiceHandle.None;
        }

        var backendParameters = parameters.WithVolume(BackendVolume(parameters.Volume, busName));

        RouteNextVoice(busName);
        var handle = _backend.CreateStreamingVoice(sampleRate, channelCount, backendParameters);
        if (!handle.IsValid)
        {
            RefusedVoiceCount++;
            _refusedVoiceLog.WriteWarning("Audio: a stream was refused by the backend (no voice left or unsupported sample rate).");
            return AudioVoiceHandle.None;
        }

        var entry = GetOrCreateEntry(handle.Index);
        entry.Handle = handle;
        entry.BusName = busName;
        entry.BaseParameters = parameters;
        entry.Owner = owner;
        entry.InUse = true;
        entry.IsStreaming = true;
        WriteNeutralModulation(entry);
        ActiveVoiceCount++;

        return handle;
    }

    /// <summary>
    /// Plays a mono clip on a software stereo voice the engine feeds itself, with an exact left
    /// and right gain per output frame (ADR-0039). For callers that need independent left and
    /// right levels, which a single (volume, pan) pair on a mono voice cannot give: on DesktopGL
    /// the pan of a mono voice only moves the OpenAL source.
    /// </summary>
    /// <remarks>
    /// <paramref name="parameters"/>.Volume and IsLooped apply, through a bus like any other
    /// voice; Pan and Pitch are ignored, since the explicit gains already say where each channel
    /// sits. The returned handle is an ordinary voice for everything else: <see cref="Stop"/>,
    /// <see cref="StopVoicesOwnedBy"/>, <see cref="StopAll"/>, <see cref="Pause"/>,
    /// <see cref="Resume"/>, <see cref="SetVoiceVolume"/>, <see cref="FadeVoice"/>. Returns
    /// <see cref="AudioVoiceHandle.None"/>, with a throttled log, when the clip exposes no usable
    /// samples (see <see cref="IAudioClipSamples"/>) or the backend has no voice left.
    /// </remarks>
    public AudioVoiceHandle PlayClipStereo(
        IAudioClip clip,
        string busName,
        in AudioVoiceParameters parameters,
        float leftGain,
        float rightGain,
        object owner = null)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (_isDisposed)
        {
            return AudioVoiceHandle.None;
        }

        if (clip is not IAudioClipSamples clipSamples || clipSamples.MonoSamples.IsEmpty || clipSamples.SampleRate <= 0)
        {
            _missingClipLog.WriteWarning("Audio: a stereo sound was refused, its clip exposes no samples.");
            return AudioVoiceHandle.None;
        }

        // The backend capability plays PcmAudioClip only (ADR-0056); any other clip that exposes its
        // samples keeps the game-thread streaming path of ADR-0039, which works on every backend.
        if (_backend is IStereoVoiceBackend stereoBackend && clip is PcmAudioClip)
        {
            return PlayClipStereoOnBackend(stereoBackend, clip, busName, parameters, SanitizeGain(leftGain), SanitizeGain(rightGain), owner);
        }

        if (!_backend.SupportsStreaming)
        {
            _missingClipLog.WriteWarning("Audio: a stereo sound was refused, the audio backend cannot stream.");
            return AudioVoiceHandle.None;
        }

        var voiceParameters = parameters.WithPan(0f).WithPitch(0f);

        return _stereoVoiceMixer.Play(
            clipSamples,
            busName,
            voiceParameters,
            SanitizeGain(leftGain),
            SanitizeGain(rightGain),
            owner);
    }

    /// <summary>
    /// Changes the left and right gains of a voice started with <see cref="PlayClipStereo"/>.
    /// Buffers already submitted keep their old gain; the next one filled uses the new one, so
    /// the change is heard after about 60 ms at the default queue depth. Ignored on a stale
    /// handle or a voice that is not a software stereo voice.
    /// </summary>
    public void SetVoiceStereoGains(AudioVoiceHandle voice, float leftGain, float rightGain)
    {
        leftGain = SanitizeGain(leftGain);
        rightGain = SanitizeGain(rightGain);

        if (TryGetEntry(voice, out var entry) && entry.IsBackendStereo)
        {
            entry.StereoLeftGain = leftGain;
            entry.StereoRightGain = rightGain;
            ((IStereoVoiceBackend)_backend).SetStereoGains(voice, leftGain, rightGain);
            return;
        }

        _stereoVoiceMixer.SetGains(voice, leftGain, rightGain);
    }

    /// <summary>
    /// Left and right gains of a voice started with <see cref="PlayClipStereo"/>. Returns false,
    /// with both gains at zero, for a stale handle or a voice that is not a software stereo voice.
    /// </summary>
    public bool GetVoiceStereoGains(AudioVoiceHandle voice, out float leftGain, out float rightGain)
    {
        if (TryGetEntry(voice, out var entry) && entry.IsBackendStereo)
        {
            leftGain = entry.StereoLeftGain;
            rightGain = entry.StereoRightGain;
            return true;
        }

        return _stereoVoiceMixer.TryGetGains(voice, out leftGain, out rightGain);
    }

    /// <summary>
    /// <see cref="PlayClipStereo"/> on a backend that mixes explicit gains itself: an ordinary resident
    /// voice, recycled at its end by <see cref="Update"/>, with no engine fed buffers.
    /// </summary>
    private AudioVoiceHandle PlayClipStereoOnBackend(
        IStereoVoiceBackend stereoBackend,
        IAudioClip clip,
        string busName,
        in AudioVoiceParameters parameters,
        float leftGain,
        float rightGain,
        object owner)
    {
        var voiceParameters = parameters.WithPan(0f).WithPitch(0f);
        var backendParameters = voiceParameters.WithVolume(BackendVolume(voiceParameters.Volume, busName));

        RouteNextVoice(busName);
        var handle = stereoBackend.PlayStereo(clip, backendParameters, leftGain, rightGain);
        if (!handle.IsValid)
        {
            RefusedVoiceCount++;
            _refusedVoiceLog.WriteWarning("Audio: a stereo sound was refused, no voice left on the backend or an unsupported clip.");
            return AudioVoiceHandle.None;
        }

        var entry = GetOrCreateEntry(handle.Index);
        entry.Handle = handle;
        entry.BusName = busName;
        entry.BaseParameters = voiceParameters;
        entry.Owner = owner;
        entry.InUse = true;
        entry.IsBackendStereo = true;
        entry.StereoLeftGain = leftGain;
        entry.StereoRightGain = rightGain;
        WriteNeutralModulation(entry);
        ActiveVoiceCount++;

        return handle;
    }

    /// <summary>Queues 16 bit PCM audio on a streaming voice. The data is copied by the backend.</summary>
    public void SubmitStreamBuffer(AudioVoiceHandle voice, byte[] buffer, int offset, int count)
    {
        if (TryGetEntry(voice, out var entry) && entry.IsStreaming)
        {
            _backend.SubmitBuffer(voice, buffer, offset, count);
        }
    }

    /// <summary>Buffers queued and not played yet. Zero means the voice is about to starve.</summary>
    public int GetPendingBufferCount(AudioVoiceHandle voice)
    {
        return TryGetEntry(voice, out _) ? _backend.GetPendingBufferCount(voice) : 0;
    }

    /// <summary>Starts a voice created but not started yet.</summary>
    public void StartVoice(AudioVoiceHandle voice)
    {
        if (TryGetEntry(voice, out _))
        {
            _backend.Start(voice);
        }
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

        // A fade the backend ramps owns the volume (the next Update sets the chronology value back, as the per-frame
        // fade does): pushing a volume here would end the ramp.
        if (!entry.IsBackendRamp)
        {
            ApplyGain(entry);
        }
    }

    /// <summary>Volume asked for by the caller, before the bus gain. Zero for a stale handle.</summary>
    public float GetVoiceVolume(AudioVoiceHandle voice)
    {
        return TryGetEntry(voice, out var entry) ? entry.BaseParameters.Volume : 0f;
    }

    /// <summary>
    /// Sets the pan of an already-playing voice. Pan is otherwise only an initial parameter at
    /// <see cref="Play"/> time; there is no per-parameter backend setter for it, so this goes
    /// through <see cref="IAudioBackend.SetParameters"/> — which pushes every parameter, not pan
    /// alone. Pushing <c>BaseParameters</c> as-is would silently overwrite the gain-applied
    /// volume the backend currently has (see <see cref="ApplyGain"/>) with the caller's pre-gain
    /// volume: invisible whenever the bus gain is 1, a real regression otherwise. So this
    /// reapplies the same gain contract as <see cref="ApplyGain"/> while pushing the full
    /// parameter set.
    /// </summary>
    public void SetVoicePan(AudioVoiceHandle voice, float pan)
    {
        if (!TryGetEntry(voice, out var entry))
        {
            return;
        }

        entry.BaseParameters = entry.BaseParameters.WithPan(pan);
        _backend.SetParameters(entry.Handle, BuildBackendParameters(entry));
    }

    /// <summary>
    /// Sets the base pitch of an already-playing voice, in octaves (clamped to the range of
    /// <see cref="AudioVoiceParameters"/>). Pushed like <see cref="SetVoicePan"/>; a spatial Doppler ratio is applied on top
    /// of it and is kept when the voice moves. Ignored for a stale handle.
    /// </summary>
    public void SetVoicePitch(AudioVoiceHandle voice, float pitch)
    {
        if (!TryGetEntry(voice, out var entry))
        {
            return;
        }

        entry.BaseParameters = entry.BaseParameters.WithPitch(pitch);
        _backend.SetParameters(entry.Handle, BuildBackendParameters(entry));
    }

    /// <summary>Base pitch of the voice, in octaves, without the Doppler ratio; 0 for a stale handle.</summary>
    public float GetVoicePitch(AudioVoiceHandle voice)
    {
        return TryGetEntry(voice, out var entry) ? entry.BaseParameters.Pitch : 0f;
    }

    /// <summary>
    /// Moves a voice started with <see cref="PlaySoundAt"/> (or gives a position to a voice of a spatial sound started
    /// without one): the next <see cref="Update"/> recomputes its distance gain and pan. Ignored for a stale handle.
    /// </summary>
    public void SetVoicePosition(AudioVoiceHandle voice, Vector3 position)
    {
        if (!TryGetEntry(voice, out var entry))
        {
            return;
        }

        entry.Position = position;
        entry.HasPosition = true;
        entry.PositionPushedThisFrame = true;

        // A spatial sound started without a position plays as a non spatial one until it gets one (plan decision P38).
        if (entry.SpatialMode == AudioSpatialMode.None && entry.AssetSpatialMode != AudioSpatialMode.None)
        {
            entry.SpatialMode = entry.AssetSpatialMode;
            entry.HasModulation = true;
        }
    }

    /// <summary>
    /// Registers the pose of the listener <paramref name="source"/>, or updates it when the source is already registered.
    /// The last registered listener is the active one (plan decision P35); a second source registering while another is
    /// registered logs one throttled warning. Without any listener, spatial voices play neutral. Allocation free after the
    /// first registrations.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public void SetListener(object source, in AudioListenerPose pose)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (_isDisposed)
        {
            return;
        }

        for (var i = 0; i < _listeners.Count; i++)
        {
            if (ReferenceEquals(_listeners[i].Source, source))
            {
                _listeners[i].Pose = pose;
                _listeners[i].PushedThisFrame = true;
                return;
            }
        }

        if (_listeners.Count > 0)
        {
            _listenerLog.WriteWarning("Audio: a second audio listener was registered; the last registered one is the active listener.");
        }

        ListenerSlot slot;

        if (_listenerPool.Count > 0)
        {
            slot = _listenerPool[^1];
            _listenerPool.RemoveAt(_listenerPool.Count - 1);
        }
        else
        {
            slot = new ListenerSlot();
        }

        slot.Source = source;
        slot.Pose = pose;
        slot.PushedThisFrame = true;
        slot.HasPreviousPosition = false;
        slot.Velocity = Vector3.Zero;
        _listeners.Add(slot);
    }

    /// <summary>
    /// Removes the listener <paramref name="source"/>, wherever it is in the stack; when it was the active one the previous
    /// one becomes active. Ignored for an unknown source.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public void RemoveListener(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        for (var i = 0; i < _listeners.Count; i++)
        {
            if (ReferenceEquals(_listeners[i].Source, source))
            {
                RecycleListener(i);
                return;
            }
        }
    }

    /// <summary>True while at least one listener is registered.</summary>
    public bool HasListener => _listeners.Count > 0;

    private void RecycleListener(int index)
    {
        var slot = _listeners[index];
        _listeners.RemoveAt(index);
        slot.Source = null;
        _listenerPool.Add(slot);
    }

    /// <summary>
    /// Speed of sound of the Doppler effect, in world units per second. It must be tuned to the game's world units (the
    /// default is 343.3, the value of the OpenAL specification for metres); the Doppler effect stays off unless a sound asset
    /// sets <c>doppler_factor</c> above zero. A NaN or non-positive value is ignored and the previous one is kept.
    /// </summary>
    public float SpeedOfSound
    {
        get => _speedOfSound;
        set
        {
            if (value > 0f)
            {
                _speedOfSound = value;
            }
        }
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
    /// <remarks>
    /// With <see cref="IAudioBusBackend"/> the backend interpolates the ramp itself, sample by sample on the audio
    /// thread, and this service keeps its own chronology of it (start, target, duration, time elapsed in
    /// <see cref="Update"/>): <see cref="GetVoiceVolume"/>, <see cref="IsFading"/>, the release at the end of
    /// <see cref="StopWithFade"/> and the start of the next fade follow that chronology, which is at most one audio
    /// block (about 10 ms) away from what is rendered. Without the capability the volume is stepped each frame.
    /// </remarks>
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
            entry.IsBackendRamp = false;
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
        entry.IsBackendRamp = _busBackend != null && _busBackend.TryRampVoiceVolume(entry.Handle, entry.FadeStartVolume, target, durationSeconds);
    }

    /// <summary>
    /// Ramps the own volume of the bus <paramref name="busName"/> to <paramref name="targetVolume"/> over
    /// <paramref name="durationSeconds"/>; <see cref="AudioBus.Volume"/> follows the ramp as <see cref="Update"/> advances
    /// it, and a zero duration applies the target at once. An unknown bus is ignored. A second fade on the same bus
    /// replaces the first, starting from the volume reached so far.
    /// </summary>
    /// <remarks>
    /// With <see cref="IAudioBusBackend"/> the backend interpolates the gain per sample on the audio thread, within one
    /// block (about 10 ms) of the volume of <see cref="AudioBus"/>. Muting or unmuting the bus during the fade takes
    /// effect at the next <see cref="Update"/>, as without the capability: the backend ramp is then abandoned by an
    /// explicit publish, and the rest of the fade is published each frame from the chronology. Without the capability the
    /// bus volume is stepped each frame, so the gain of every voice on the bus is reapplied at each <see cref="Update"/>.
    /// </remarks>
    public void FadeBus(string busName, float targetVolume, float durationSeconds)
    {
        if (_isDisposed || float.IsNaN(targetVolume) || !Mixer.TryGetBus(busName, out var bus))
        {
            return;
        }

        var target = Math.Clamp(targetVolume, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);
        var fade = FindBusFade(bus);
        var backendIndex = _busBackend != null ? BackendIndexOf(bus) : -1;

        if (durationSeconds <= 0f || float.IsNaN(durationSeconds))
        {
            var wasRamped = fade is { Active: true, BackendRamp: true };

            if (fade != null)
            {
                fade.Active = false;
            }

            bus.Volume = target;

            // An explicit publish also ends a ramp the backend still holds.
            if (wasRamped)
            {
                _busBackend.SetBusGain(backendIndex, bus.IsMuted ? 0f : target);
            }

            return;
        }

        if (fade == null)
        {
            fade = new BusFade { Bus = bus };
            _busFades.Add(fade);
        }

        fade.Active = true;
        fade.Start = bus.Volume;
        fade.Target = target;
        fade.Duration = durationSeconds;
        fade.Elapsed = 0f;
        fade.BackendIndex = backendIndex;
        fade.MutedAtSend = bus.IsMuted;

        // A muted bus is not ramped by the backend: its fade is published each frame from the chronology, which is 0 while
        // it is muted, so the mute applies at the next Update (ramping from the audible gain would keep it audible).
        fade.BackendRamp = !bus.IsMuted && backendIndex >= 0 && _busBackend.TryRampBusGain(backendIndex, fade.Start, target, durationSeconds);
    }

    /// <summary>
    /// Captures the own volume of every bus and the parameters of the insert effects (see <see cref="AudioMixerSnapshot"/>); the
    /// Editor bus is skipped. Allocates: call it on demand, not every frame.
    /// </summary>
    public AudioMixerSnapshot CaptureSnapshot()
    {
        return AudioMixerSnapshot.Capture(Mixer);
    }

    /// <summary>
    /// Brings the mixer back to a captured state: the volume of every captured bus ramps to its captured value over
    /// <paramref name="durationSeconds"/> like <see cref="FadeBus"/> (on the backend, sample by sample, with
    /// <see cref="IAudioBusBackend"/>; stepped each <see cref="Update"/> without it; a zero duration applies the volumes at once).
    /// The parameters of the insert effects are published at once, at the call, whatever the duration (effect parameters are not
    /// ramped), and only with <see cref="IAudioBusBackend"/>: without it the effects are absent and only the volumes apply.
    /// The Editor bus, and the mute of every bus (so the Master mute that the project settings drive), are never touched.
    /// </summary>
    /// <exception cref="ArgumentNullException">The snapshot is null.</exception>
    /// <exception cref="ArgumentException">The snapshot was captured from another mixer.</exception>
    public void ApplySnapshot(AudioMixerSnapshot snapshot, float durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!ReferenceEquals(snapshot.Mixer, Mixer))
        {
            throw new ArgumentException("The snapshot was captured from another audio mixer.", nameof(snapshot));
        }

        if (_isDisposed)
        {
            return;
        }

        if (_busBackend != null)
        {
            snapshot.RestoreEffectParameters();
        }

        snapshot.RestoreBusVolumes(this, durationSeconds);
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
            if (entry.IsFading && entry.IsBackendRamp)
            {
                // The backend stops where it is; the volume stays the one the chronology reached.
                _busBackend.FreezeVoiceVolume(entry.Handle);

                // And the backend takes the volume the chronology reached, which a SetVoiceVolume of this frame may have
                // moved from the rendered one. If this volume is dropped (full ring) the freeze still stopped the ramp.
                ApplyGain(entry);
            }

            entry.IsFading = false;
            entry.IsBackendRamp = false;
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

    /// <summary>
    /// Stops every voice except those routed to <paramref name="preservedBusName"/>.
    /// The editor uses it to end a play session without touching its own preview, which lives on
    /// the Editor bus.
    /// </summary>
    public void StopAllExceptBus(string preservedBusName)
    {
        for (var i = 0; i < _voices.Count; i++)
        {
            var entry = _voices[i];
            if (!entry.InUse || IsOnBus(entry, preservedBusName))
            {
                continue;
            }

            _backend.Stop(entry.Handle);
            ReleaseEntry(entry);
        }
    }

    /// <summary>
    /// Pauses every voice except those routed to <paramref name="preservedBusName"/>, remembering
    /// which ones were paused so <see cref="ResumeAllExceptBus"/> does not resume a voice the game
    /// had paused on its own.
    /// </summary>
    public void PauseAllExceptBus(string preservedBusName)
    {
        for (var i = 0; i < _voices.Count; i++)
        {
            var entry = _voices[i];
            if (!entry.InUse || entry.IsPausedBySystem || IsOnBus(entry, preservedBusName))
            {
                continue;
            }

            if (_backend.GetState(entry.Handle) != AudioVoiceState.Playing)
            {
                continue;
            }

            _backend.Pause(entry.Handle);
            entry.IsPausedBySystem = true;
        }
    }

    /// <summary>Resumes only the voices paused by <see cref="PauseAllExceptBus"/>.</summary>
    public void ResumeAllExceptBus(string preservedBusName)
    {
        for (var i = 0; i < _voices.Count; i++)
        {
            var entry = _voices[i];
            if (!entry.InUse || !entry.IsPausedBySystem || IsOnBus(entry, preservedBusName))
            {
                continue;
            }

            _backend.Resume(entry.Handle);
            entry.IsPausedBySystem = false;
        }
    }

    private static bool IsOnBus(VoiceEntry entry, string busName)
    {
        return busName != null && string.Equals(entry.BusName, busName, StringComparison.OrdinalIgnoreCase);
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
    /// Creates the PlayStation SPU on a backend that hosts one (<see cref="IPsxSpuHost"/>, the software backend)
    /// and routes its output to <paramref name="busName"/> "like a voice": the gain set on the port is multiplied by
    /// the effective gain of the bus, and re-applied when the bus gains change; with <see cref="IAudioBusBackend"/> the SPU
    /// is routed to the bus and mixed with it by the backend instead. Without the capability, or when an SPU is already alive or the backend is unavailable,
    /// returns false with a throttled "SPU unavailable" log and a null <paramref name="port"/>. Game thread only.
    /// </summary>
    public bool TryCreatePsxSpu(PsxSpuHardwareTables tables, string busName, out PsxSpuPort port)
    {
        ArgumentNullException.ThrowIfNull(tables);
        port = null;

        if (_isDisposed)
        {
            return false;
        }

        if (_backend is not IPsxSpuHost host)
        {
            _spuLog.WriteWarning("Audio: SPU unavailable, the audio backend does not host the PlayStation SPU.");
            return false;
        }

        if (!host.TryCreatePsxSpu(tables, out port))
        {
            port = null;
            _spuLog.WriteWarning("Audio: SPU unavailable, the audio backend is not running or an SPU is already alive.");
            return false;
        }

        _spuPort = port;
        _spuBusName = busName;
        if (_busBackend != null)
        {
            _busBackend.TrySetPsxSpuBus(port, ResolveBackendBus(busName));
        }
        else
        {
            port.SetBusGain(Mixer.GetEffectiveGain(busName));
        }

        return true;
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

        _frameElapsed = elapsedSeconds;
        AdvanceBusFades(elapsedSeconds);
        AdvanceListener(elapsedSeconds);

        var mixerChanged = _appliedMixerVersion != Mixer.Version;

        if (mixerChanged)
        {
            SyncBuses();
        }

        if (_syncedEffectsVersion != Mixer.EffectsVersion)
        {
            SyncEffects();
        }

        if (_syncedSendsVersion != Mixer.SendsVersion)
        {
            SyncSends();
        }

        if (!_masterLimiterSent)
        {
            SendMasterLimiter();
        }

        for (var i = 0; i < _voices.Count; i++)
        {
            var entry = _voices[i];
            if (!entry.InUse)
            {
                continue;
            }

            // A silent stream is not necessarily finished: it may just be starving. Only its
            // feeder can tell, so streaming voices are never recycled here.
            if (!entry.IsStreaming && _backend.GetState(entry.Handle) == AudioVoiceState.Stopped)
            {
                ReleaseEntry(entry);
                continue;
            }

            // Before the fade: a gain folded here is applied by the fade right after, never by two pushes in one frame.
            var volumePushed = entry.HasModulation && UpdateModulation(entry);

            if (entry.IsFading)
            {
                if (AdvanceFade(entry, elapsedSeconds))
                {
                    continue;
                }

                // The ramp already pushed the current gain to the backend this frame.
                continue;
            }

            // With the bus capability the backend applies the bus gain itself: nothing to reapply here.
            if (mixerChanged && _busBackend == null && !volumePushed)
            {
                ApplyGain(entry);
            }
        }

        if (_spuPort != null)
        {
            if (_spuPort.IsDisposed)
            {
                _spuPort = null;
            }
            else if (mixerChanged && _busBackend == null)
            {
                _spuPort.SetBusGain(Mixer.GetEffectiveGain(_spuBusName));
            }
        }

        if (mixerChanged)
        {
            _appliedMixerVersion = Mixer.Version;
        }

        // After the voices: a fade out that just ended released its voice, and the music player
        // drops the matching track on the same frame.
        Music.Update(elapsedSeconds);
        _stereoVoiceMixer.Update(elapsedSeconds);
    }

    /// <summary>Reads the active listener (the top of the stack) for the voices of this frame.</summary>
    private void AdvanceListener(float elapsedSeconds)
    {
        // Every slot advances, not only the active one: a listener that becomes active later must not measure its
        // displacement over frames it was not followed.
        for (var i = 0; i < _listeners.Count; i++)
        {
            var slot = _listeners[i];
            var position = slot.Pose.Position;

            if (slot.PushedThisFrame)
            {
                if (!slot.HasPreviousPosition)
                {
                    slot.Velocity = Vector3.Zero;
                }
                else if (elapsedSeconds > MinVelocityElapsed)
                {
                    slot.Velocity = (position - slot.PreviousPosition) / elapsedSeconds;
                }

                slot.PreviousPosition = position;
                slot.HasPreviousPosition = true;
                slot.PushedThisFrame = false;
            }
            else
            {
                // Not pushed in this frame: no motion is known, and the next push has nothing to compare with.
                slot.Velocity = Vector3.Zero;
                slot.HasPreviousPosition = false;
            }
        }

        _hasActiveListener = _listeners.Count > 0;

        if (_hasActiveListener)
        {
            var active = _listeners[^1];
            _activePose = active.Pose;
            _activeVelocity = active.Velocity;
        }
        else
        {
            _activeVelocity = Vector3.Zero;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        while (_listeners.Count > 0)
        {
            RecycleListener(_listeners.Count - 1);
        }

        _hasActiveListener = false;
        Music.Dispose();
        StopAll();
        _spuPort?.Dispose();
        _spuPort = null;
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

        // A ramp interpolated by the backend needs no per-frame push: this is only its chronology.
        if (!entry.IsBackendRamp)
        {
            ApplyGain(entry);
        }

        if (progress < 1f)
        {
            return false;
        }

        entry.IsFading = false;
        entry.IsBackendRamp = false;

        if (entry.FadeCompletion != AudioFadeCompletion.Stop)
        {
            return false;
        }

        _backend.Stop(entry.Handle);
        ReleaseEntry(entry);
        return true;
    }

    /// <summary>
    /// Advances the chronology of every bus fade and writes the volume it reaches to the bus. A fade ramped by the
    /// backend publishes its final gain once, when it ends, so the backend holds exactly the volume of the bus.
    /// </summary>
    private void AdvanceBusFades(float elapsedSeconds)
    {
        for (var i = 0; i < _busFades.Count; i++)
        {
            var fade = _busFades[i];

            if (!fade.Active)
            {
                continue;
            }

            fade.Elapsed += elapsedSeconds;

            var progress = fade.Elapsed >= fade.Duration ? 1f : fade.Elapsed / fade.Duration;
            fade.Bus.Volume = fade.Start + ((fade.Target - fade.Start) * progress);

            if (progress < 1f)
            {
                continue;
            }

            fade.Active = false;

            if (fade.BackendRamp)
            {
                _busBackend.SetBusGain(fade.BackendIndex, fade.Bus.IsMuted ? 0f : fade.Bus.Volume);
            }
        }
    }

    private BusFade FindBusFade(AudioBus bus)
    {
        for (var i = 0; i < _busFades.Count; i++)
        {
            if (ReferenceEquals(_busFades[i].Bus, bus))
            {
                return _busFades[i];
            }
        }

        return null;
    }

    // True while the backend ramps the gain of the bus: its gain is not published from the bus volume meanwhile.
    private bool IsRampedByBackend(AudioBus bus)
    {
        var fade = FindBusFade(bus);

        if (fade is not { Active: true, BackendRamp: true })
        {
            return false;
        }

        // The mute changed since the ramp was sent: the ramp (sent with the old mute) is abandoned, the caller publishes.
        if (bus.IsMuted != fade.MutedAtSend)
        {
            fade.BackendRamp = false;
            return false;
        }

        return true;
    }

    private IAudioClip ResolveClip(SoundAsset asset, Guid audioFileAssetId)
    {
        if (audioFileAssetId == Guid.Empty)
        {
            if (_missingClipLog.ShouldWrite())
            {
                _missingClipLog.WriteNow($"Audio: sound '{asset.Name}' references no audio file.");
            }

            return null;
        }

        if (ClipProvider == null)
        {
            if (_missingClipLog.ShouldWrite())
            {
                _missingClipLog.WriteNow($"Audio: no clip provider is wired, sound '{asset.Name}' cannot be played.");
            }

            return null;
        }

        var clip = ClipProvider.GetClip(audioFileAssetId);
        if (clip is not { IsDisposed: false })
        {
            if (_missingClipLog.ShouldWrite())
            {
                _missingClipLog.WriteNow($"Audio: the audio file of sound '{asset.Name}' ({audioFileAssetId}) could not be loaded.");
            }

            return null;
        }

        return clip;
    }

    /// <summary>
    /// Volume sent to the backend for a voice. Without the bus capability the effective gain of the bus is folded
    /// into it; with it the backend mixes the bus itself, so folding it here would apply it twice.
    /// </summary>
    private float BackendVolume(float volume, string busName)
    {
        return _busBackend != null ? volume : volume * Mixer.GetEffectiveGain(busName);
    }

    /// <summary>With the bus capability, makes the voice the backend starts next go to the bus of <paramref name="busName"/>.</summary>
    private void RouteNextVoice(string busName)
    {
        _busBackend?.SetNextVoiceBus(ResolveBackendBus(busName));
    }

    /// <summary>
    /// Index on the backend of the bus a voice is routed to: the named bus, or the root when the name is unknown
    /// (as <see cref="AudioMixer.GetEffectiveGain"/> falls back to the root gain). Master, index 0, for a bus the
    /// backend could not hold.
    /// </summary>
    private int ResolveBackendBus(string busName)
    {
        SyncBuses();

        if (!Mixer.TryGetBus(busName, out var bus))
        {
            bus = Mixer.Root;
        }

        var buses = Mixer.Buses;

        for (var i = 0; i < _backendBusIndices.Count; i++)
        {
            if (ReferenceEquals(buses[i], bus))
            {
                return Math.Max(0, _backendBusIndices[i]);
            }
        }

        return 0;
    }

    /// <summary>
    /// With the bus capability: creates on the backend the buses the mixer gained since the last call (they are
    /// created in parent first order, which is the backend index order), and publishes the own gain of every bus
    /// when something changed: its volume, or 0 when it is muted. The backend multiplies along the chain itself.
    /// Allocation free when nothing changed.
    /// </summary>
    private void SyncBuses()
    {
        if (_busBackend == null)
        {
            return;
        }

        var buses = Mixer.Buses;

        while (_backendBusIndices.Count < buses.Count)
        {
            var bus = buses[_backendBusIndices.Count];
            var index = -1;

            if (bus.Parent == null)
            {
                index = 0;
            }
            else
            {
                var parentIndex = BackendIndexOf(bus.Parent);

                if (parentIndex >= 0 && _busBackend.TryCreateBus(parentIndex, out var created))
                {
                    index = created;
                }
            }

            _backendBusIndices.Add(index);
        }

        if (_syncedBusVersion == Mixer.Version)
        {
            return;
        }

        for (var i = 0; i < buses.Count; i++)
        {
            if (_backendBusIndices[i] >= 0 && !IsRampedByBackend(buses[i]))
            {
                _busBackend.SetBusGain(_backendBusIndices[i], buses[i].IsMuted ? 0f : buses[i].Volume);
            }
        }

        _syncedBusVersion = Mixer.Version;
    }

    /// <summary>
    /// Sends the insert effects of the buses to the backend: the ones removed first, then the new ones in order. Without
    /// <see cref="IAudioBusBackend"/> effects are absent and that is logged (once per throttle window). A command that
    /// could not be sent is retried on the next <see cref="Update"/>. Allocation free when nothing changed.
    /// </summary>
    private void SyncEffects()
    {
        var buses = Mixer.Buses;

        if (_busBackend == null)
        {
            for (var i = 0; i < buses.Count; i++)
            {
                var effects = buses[i].Effects;

                for (var j = 0; j < effects.Count; j++)
                {
                    if (effects[j] is DuckingEffect)
                    {
                        _duckingLog.WriteWarning("Audio: this backend has no bus graph, so the ducking of the audio buses is absent (use the software backend).");
                    }
                    else
                    {
                        _effectsLog.WriteWarning("Audio: this backend has no bus graph, so the insert effects of the audio buses are ignored (use the software backend).");
                    }
                }
            }

            _syncedEffectsVersion = Mixer.EffectsVersion;
            return;
        }

        SyncBuses();
        var allSent = true;

        for (var i = 0; i < buses.Count; i++)
        {
            while (_sentEffects.Count <= i)
            {
                _sentEffects.Add(new List<AudioEffect>());
            }

            var backendIndex = _backendBusIndices[i];

            if (backendIndex < 0)
            {
                continue;
            }

            var wanted = buses[i].Effects;
            var sent = _sentEffects[i];

            for (var j = sent.Count - 1; j >= 0; j--)
            {
                if (!ContainsEffect(wanted, sent[j]))
                {
                    if (_busBackend.TryRemoveBusEffect(backendIndex, sent[j]))
                    {
                        sent.RemoveAt(j);
                    }
                    else
                    {
                        allSent = false;
                    }
                }
            }

            for (var j = 0; j < wanted.Count; j++)
            {
                if (ContainsEffect(sent, wanted[j]))
                {
                    continue;
                }

                // Insertion order: a later effect never goes before an earlier one that failed to send.
                var accepted = false;

                if (wanted[j] is DuckingEffect ducking)
                {
                    var sourceIndex = BackendIndexOf(ducking.Source);

                    if (sourceIndex < 0)
                    {
                        // The source is not on the backend (more buses than it holds): the relation cannot run.
                        _duckingLog.WriteWarning("Audio: the source bus of a ducking is not on the backend, so the ducking is absent.");
                        accepted = true;
                    }
                    else
                    {
                        accepted = _busBackend.TryAddBusDucking(backendIndex, ducking, sourceIndex);
                    }
                }
                else
                {
                    accepted = _busBackend.TryAddBusEffect(backendIndex, wanted[j]);
                }

                if (accepted)
                {
                    sent.Add(wanted[j]);
                }
                else
                {
                    allSent = false;
                    break;
                }
            }
        }

        if (allSent)
        {
            _syncedEffectsVersion = Mixer.EffectsVersion;
        }
    }

    /// <summary>Sends the Master limiter to the backend that has the capability; a failure is retried at the next <see cref="Update"/>.</summary>
    private void SendMasterLimiter()
    {
        _masterLimiterSent = _busBackend == null || _busBackend.TrySetMasterLimiter(_masterLimiter);
    }

    /// <summary>
    /// Sends the sends of the buses to the backend: the ones removed or lowered to zero first (they free slots), then the new
    /// and changed ones. Without <see cref="IAudioBusBackend"/> sends are absent and that is logged (once per throttle window).
    /// A command that could not be sent is retried on the next <see cref="Update"/>. Allocation free when nothing changed.
    /// </summary>
    private void SyncSends()
    {
        var buses = Mixer.Buses;

        if (_busBackend == null)
        {
            for (var i = 0; i < buses.Count; i++)
            {
                if (buses[i].Sends.Count > 0)
                {
                    _sendsLog.WriteWarning("Audio: this backend has no bus graph, so the sends of the audio buses are ignored (use the software backend).");
                    break;
                }
            }

            _syncedSendsVersion = Mixer.SendsVersion;
            return;
        }

        SyncBuses();
        var allSent = true;

        for (var i = 0; i < buses.Count; i++)
        {
            while (_sentSends.Count <= i)
            {
                _sentSends.Add(new List<AudioBusSend>());
            }

            var backendIndex = _backendBusIndices[i];

            if (backendIndex < 0)
            {
                continue;
            }

            var wanted = buses[i].Sends;
            var sent = _sentSends[i];

            for (var j = sent.Count - 1; j >= 0; j--)
            {
                if (buses[i].GetSend(sent[j].Target) > 0f)
                {
                    continue;
                }

                var targetIndex = BackendIndexOf(sent[j].Target);

                if (targetIndex < 0 || _busBackend.TrySetBusSend(backendIndex, targetIndex, 0f))
                {
                    sent.RemoveAt(j);
                }
                else
                {
                    allSent = false;
                }
            }

            for (var j = 0; j < wanted.Count; j++)
            {
                var send = wanted[j];
                var known = -1;

                for (var k = 0; k < sent.Count; k++)
                {
                    if (ReferenceEquals(sent[k].Target, send.Target))
                    {
                        known = k;
                        break;
                    }
                }

                if (known >= 0 && sent[known].Level.Equals(send.Level))
                {
                    continue;
                }

                var targetIndex = BackendIndexOf(send.Target);

                if (targetIndex < 0)
                {
                    continue;
                }

                if (_busBackend.TrySetBusSend(backendIndex, targetIndex, send.Level))
                {
                    if (known >= 0)
                    {
                        sent[known] = send;
                    }
                    else
                    {
                        sent.Add(send);
                    }
                }
                else
                {
                    allSent = false;
                    _sendsLog.WriteWarning("Audio: a bus send could not be sent to the backend and will be retried.");
                }
            }
        }

        if (allSent)
        {
            _syncedSendsVersion = Mixer.SendsVersion;
        }
    }

    private static bool ContainsEffect(IReadOnlyList<AudioEffect> effects, AudioEffect effect)
    {
        for (var i = 0; i < effects.Count; i++)
        {
            if (ReferenceEquals(effects[i], effect))
            {
                return true;
            }
        }

        return false;
    }

    private int BackendIndexOf(AudioBus bus)
    {
        var buses = Mixer.Buses;

        for (var i = 0; i < _backendBusIndices.Count; i++)
        {
            if (ReferenceEquals(buses[i], bus))
            {
                return _backendBusIndices[i];
            }
        }

        return -1;
    }

    private void ApplyGain(VoiceEntry entry)
    {
        _backend.SetVolume(entry.Handle, BackendVolume(entry.BaseParameters.Volume, entry.BusName) * entry.FoldedGain);
    }

    /// <summary>
    /// The full parameter set sent to a backend without the modulation capability: the base parameters of the voice with
    /// the bus gain and the folded spatial gain in the volume, the spatial pan (which replaces the base pan) and the folded
    /// pitch offset. With the capability the folded values are neutral and this is the base parameters alone.
    /// </summary>
    private AudioVoiceParameters BuildBackendParameters(VoiceEntry entry)
    {
        var parameters = entry.BaseParameters;
        var pan = float.IsNaN(entry.FoldedPan) ? parameters.Pan : entry.FoldedPan;
        var pitch = Math.Clamp(parameters.Pitch + entry.FoldedPitchOffset, AudioVoiceParameters.MinPitch, AudioVoiceParameters.MaxPitch);

        return parameters
            .WithVolume(BackendVolume(parameters.Volume, entry.BusName) * entry.FoldedGain)
            .WithPan(pan)
            .WithPitch(pitch);
    }

    private static void EvaluateSpatial(
        in AudioListenerPose listener,
        AudioSpatialMode mode,
        AudioDistanceModel model,
        float referenceDistance,
        float maxDistance,
        float rolloffFactor,
        Vector3 position,
        out float gain,
        out float pan)
    {
        var distance = AudioSpatialMath.Distance(mode, listener.Position, position);
        gain = AudioDistanceAttenuation.Evaluate(model, distance, referenceDistance, maxDistance, rolloffFactor);
        pan = AudioSpatialMath.Pan(mode, in listener, position);
    }

    /// <summary>
    /// Recomputes the spatial gain and pan of a voice against the listener of this frame (neutral without one) and sends
    /// what changed beyond the P44 thresholds: to the backend modulation channel, or folded into the volume and the
    /// parameters on a backend without it. Returns true when a volume was pushed to the backend, so the caller does not push
    /// it a second time in the same frame. A fading voice is not pushed here: its fade applies the volume right after.
    /// Allocation free.
    /// </summary>
    private bool UpdateModulation(VoiceEntry entry)
    {
        var gain = 1f;
        var pan = float.NaN;
        var rate = 1f;

        AdvanceVoiceVelocity(entry);

        if (entry.BindingCount > 0)
        {
            RefreshBindingFactors(entry);
        }

        if (_hasActiveListener && entry.SpatialMode != AudioSpatialMode.None && entry.HasPosition)
        {
            EvaluateSpatial(
                _activePose, entry.SpatialMode, entry.DistanceModel, entry.ReferenceDistance, entry.MaxDistance,
                entry.RolloffFactor, entry.Position, out gain, out pan);

            if (entry.DopplerFactor > 0f)
            {
                rate = AudioDoppler.Ratio(
                    entry.SpatialMode, _activePose.Position, _activeVelocity, entry.Position, entry.Velocity,
                    _speedOfSound, entry.DopplerFactor);
            }
        }

        gain *= entry.BindingVolumeFactor;
        rate *= entry.BindingRateFactor;

        var gainChanged = MathF.Abs(gain - entry.SentGain) > GainSendThreshold;
        var panChanged = PanDiffers(pan, entry.SentPan);
        var rateChanged = MathF.Abs((rate / entry.SentRate) - 1f) > RateSendThreshold;

        if (!gainChanged && !panChanged && !rateChanged)
        {
            return false;
        }

        if (gainChanged)
        {
            entry.SentGain = gain;
        }

        if (panChanged)
        {
            entry.SentPan = pan;
        }

        if (rateChanged)
        {
            entry.SentRate = rate;
        }

        if (_modulationBackend != null)
        {
            _modulationBackend.SetVoiceModulation(entry.Handle, entry.SentGain, entry.SentPan, entry.SentRate);
            return false;
        }

        if (gainChanged)
        {
            entry.FoldedGain = gain;
        }

        if (panChanged)
        {
            entry.FoldedPan = pan;
        }

        if (rateChanged)
        {
            // Without the channel the speed ratio is a pitch offset in octaves; BuildBackendParameters bounds the total.
            entry.FoldedPitchOffset = MathF.Log2(rate);
        }

        if (panChanged || rateChanged)
        {
            // Pushes the whole parameter set, the volume included.
            _backend.SetParameters(entry.Handle, BuildBackendParameters(entry));
            return true;
        }

        if (!entry.IsFading)
        {
            ApplyGain(entry);
            return true;
        }

        return false;
    }

    /// <summary>Derives the velocity of a voice from the position pushed since the last frame (plan decision P37) and clears the flag.</summary>
    private void AdvanceVoiceVelocity(VoiceEntry entry)
    {
        if (entry.PositionPushedThisFrame)
        {
            if (!entry.HasPreviousPosition)
            {
                entry.Velocity = Vector3.Zero;
            }
            else if (_frameElapsed > MinVelocityElapsed)
            {
                entry.Velocity = (entry.Position - entry.PreviousPosition) / _frameElapsed;
            }

            entry.PreviousPosition = entry.Position;
            entry.HasPreviousPosition = true;
            entry.PositionPushedThisFrame = false;
        }
        else
        {
            entry.Velocity = Vector3.Zero;
            entry.HasPreviousPosition = false;
        }
    }

    private static bool PanDiffers(float pan, float sent)
    {
        var panIsNone = float.IsNaN(pan);
        var sentIsNone = float.IsNaN(sent);

        return panIsNone || sentIsNone ? panIsNone != sentIsNone : MathF.Abs(pan - sent) > PanSendThreshold;
    }

    /// <summary>Same sanitizing contract as <see cref="AudioVoiceParameters.Volume"/>: NaN becomes full gain, otherwise clamped to [0, 1].</summary>
    private static float SanitizeGain(float value)
    {
        return float.IsNaN(value) ? AudioVoiceParameters.MaxVolume : Math.Clamp(value, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);
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
        public bool IsStreaming;
        public bool IsBackendStereo;

        /// <summary>Steal priority, 0 for a voice that is never stolen. Only <see cref="PlayClipCore"/> sets it.</summary>
        public int Priority;

        /// <summary>Order of start, to find the oldest voice among equal priorities.</summary>
        public long StartSequence;

        public float StereoLeftGain;
        public float StereoRightGain;
        public bool IsPausedBySystem;

        public bool IsFading;
        public float FadeStartVolume;
        public float FadeTargetVolume;
        public float FadeDuration;
        public float FadeElapsed;
        public AudioFadeCompletion FadeCompletion;

        /// <summary>The backend interpolates the running fade itself (<see cref="IAudioBusBackend.TryRampVoiceVolume"/>); the fields above are its chronology.</summary>
        public bool IsBackendRamp;

        /// <summary>True for a voice <see cref="UpdateModulation"/> recomputes each frame: its stored spatial mode is not None.</summary>
        public bool HasModulation;

        /// <summary>Stored mode: the mode of the asset when the voice has a position, None otherwise (plan decision P38). Never depends on the listener.</summary>
        public AudioSpatialMode SpatialMode;

        /// <summary>Mode of the asset the voice plays, kept so that giving the voice a position later can spatialize it.</summary>
        public AudioSpatialMode AssetSpatialMode;

        public AudioDistanceModel DistanceModel;
        public float ReferenceDistance;
        public float MaxDistance;
        public float RolloffFactor;
        public Vector3 Position;
        public bool HasPosition;

        /// <summary>Doppler factor of the asset (0 = off).</summary>
        public float DopplerFactor;

        /// <summary>Position of the last frame the voice was followed in, to derive <see cref="Velocity"/>.</summary>
        public Vector3 PreviousPosition;
        public bool HasPreviousPosition;

        /// <summary>World units per second, derived from the positions pushed in two successive frames (plan decision P37).</summary>
        public Vector3 Velocity;

        /// <summary>True when the position was pushed since the last <see cref="UpdateModulation"/>.</summary>
        public bool PositionPushedThisFrame;

        /// <summary>Last values sent beyond the thresholds of P44 (to the modulation channel, or folded).</summary>
        public float SentGain;
        public float SentPan;
        public float SentRate;

        /// <summary>Spatial gain folded into the volume sent, on a backend without the modulation channel; 1 under it.</summary>
        public float FoldedGain;

        /// <summary>Spatial pan sent in the parameters on a backend without the channel; NaN (none) under it or when neutral.</summary>
        public float FoldedPan;

        /// <summary>Pitch offset, in octaves, folded into the parameters on a backend without the channel.</summary>
        public float FoldedPitchOffset;

        /// <summary>Bindings of the asset (null without), their registry indices and last seen versions, allocated once per entry and reused.</summary>
        public IReadOnlyList<AudioParameterBinding> Bindings;
        public readonly int[] BindingIndices = new int[SoundAsset.MaxParameterBindings];
        public readonly int[] BindingVersions = new int[SoundAsset.MaxParameterBindings];
        public int BindingCount;

        /// <summary>True until the first <see cref="UpdateModulation"/> after the binding, which recomputes the factors whatever the versions.</summary>
        public bool BindingsStale;

        public float BindingVolumeFactor;
        public float BindingRateFactor;

        public void Reset()
        {
            Handle = AudioVoiceHandle.None;
            BusName = null;
            Owner = null;
            InUse = false;
            IsStreaming = false;
            IsBackendStereo = false;
            Priority = 0;
            StartSequence = 0;
            StereoLeftGain = 0f;
            StereoRightGain = 0f;
            IsPausedBySystem = false;
            IsFading = false;
            FadeStartVolume = 0f;
            FadeTargetVolume = 0f;
            FadeDuration = 0f;
            FadeElapsed = 0f;
            FadeCompletion = AudioFadeCompletion.None;
            IsBackendRamp = false;
            HasModulation = false;
            SpatialMode = AudioSpatialMode.None;
            AssetSpatialMode = AudioSpatialMode.None;
            DistanceModel = AudioDistanceModel.None;
            ReferenceDistance = 0f;
            MaxDistance = 0f;
            RolloffFactor = 0f;
            Position = Vector3.Zero;
            HasPosition = false;
            DopplerFactor = 0f;
            PreviousPosition = Vector3.Zero;
            HasPreviousPosition = false;
            Velocity = Vector3.Zero;
            PositionPushedThisFrame = false;
            SentGain = 0f;
            SentPan = 0f;
            SentRate = 0f;
            // Zero on purpose: every start path writes the real values, and one that did not would be silent.
            FoldedGain = 0f;
            FoldedPan = float.NaN;
            FoldedPitchOffset = 0f;
            Bindings = null;
            BindingCount = 0;
            BindingsStale = false;
            BindingVolumeFactor = 1f;
            BindingRateFactor = 1f;
        }
    }

    private sealed class ListenerSlot
    {
        public object Source;
        public AudioListenerPose Pose;
        public Vector3 PreviousPosition;
        public bool HasPreviousPosition;
        public Vector3 Velocity;
        public bool PushedThisFrame;
    }

    // What a start tells the modulation: the asset (null for a plain PlayClip), and the position given to PlaySoundAt.
    private readonly struct VoiceModulationStart
    {
        public VoiceModulationStart(SoundAsset asset, bool hasPosition, Vector3 position)
        {
            Asset = asset;
            HasPosition = hasPosition;
            Position = position;
        }

        public SoundAsset Asset { get; }

        public bool HasPosition { get; }

        public Vector3 Position { get; }
    }

    // Chronology of one bus fade: the bus volume follows it, and the backend ramps the gain itself when it can. One
    // entry per bus, reused by the next fade of that bus, so a fade allocates nothing after the first one.
    private sealed class BusFade
    {
        public AudioBus Bus;
        public bool Active;
        public float Start;
        public float Target;
        public float Duration;
        public float Elapsed;
        public int BackendIndex;
        public bool BackendRamp;
        public bool MutedAtSend;
    }
}
