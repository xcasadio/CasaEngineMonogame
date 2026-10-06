using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Spatial;
using CasaEngine.Framework.Audio.Streaming;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using NumericsVector3 = System.Numerics.Vector3;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Plays a <see cref="SoundAsset"/> from an entity: a sound effect, or a streamed track.
/// </summary>
/// <remarks>
/// A scene component (decision D13): its world position is the position of a sound whose asset is spatial
/// (<see cref="SoundAsset.SpatialMode"/> other than none), read as described in <see cref="AudioScenePose"/>, and the
/// voice follows the component when it moves. The spatial mode comes from the asset only. A sound without a spatial mode
/// plays as before, whatever the transform.
/// The voice is scoped to the world, so leaving the world stops it, and detaching the component
/// stops it too.
/// An emitter saved before it was a scene component has no transform and no children: it loads with an identity
/// transform (<see cref="AllowsMissingSceneData"/>).
/// </remarks>
[DisplayName("Sound Emitter")]
public class SoundEmitterComponent : SceneComponent
{
    private float _volumeOverride = 1f;
    private float _pitchOverride;
    private string _busName = string.Empty;

    private SoundAsset _soundAsset;

    // ADR-0037: the sound asset is shared, held through a counted handle for as long as this emitter
    // references it, given back in ReleaseSoundAssetHandle (a reload and Detach).
    private AssetHandle<SoundAsset> _soundAssetHandle;
    private AudioService _audioService;
    private AudioVoiceHandle _voice = AudioVoiceHandle.None;
    private MusicTrackHandle _track = MusicTrackHandle.None;

    // A voice started with PlaySoundAt follows the position of this component, sent only when it changed.
    private bool _isVoiceSpatial;
    private NumericsVector3 _lastVoicePosition;
    private bool _isBoundForTests;

    public SoundEmitterComponent() : base()
    {
    }

    public SoundEmitterComponent(SoundEmitterComponent other) : base(other)
    {
        SoundAssetId = other.SoundAssetId;
        PlayOnStart = other.PlayOnStart;
        IsLoopedOverride = other.IsLoopedOverride;
        BusName = other.BusName;
        VolumeOverride = other.VolumeOverride;
        PitchOverride = other.PitchOverride;
    }

    /// <summary>Id of the <c>.sound</c> asset to play.</summary>
    public Guid SoundAssetId { get; set; } = Guid.Empty;

    /// <summary>Plays as soon as the entity enters the world.</summary>
    public bool PlayOnStart { get; set; }

    /// <summary>Null keeps the loop flag of the asset.</summary>
    public bool? IsLoopedOverride { get; set; }

    /// <summary>Empty keeps the bus of the asset. See <see cref="AudioBusNames"/>.</summary>
    public string BusName
    {
        get => _busName;
        set => _busName = value ?? string.Empty;
    }

    /// <summary>Scales the asset volume, in [0,1].</summary>
    public float VolumeOverride
    {
        get => _volumeOverride;
        set => _volumeOverride = Sanitize(value, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume, _volumeOverride);
    }

    /// <summary>Added to the asset pitch, in [-1,1].</summary>
    public float PitchOverride
    {
        get => _pitchOverride;
        set => _pitchOverride = Sanitize(value, AudioVoiceParameters.MinPitch, AudioVoiceParameters.MaxPitch, _pitchOverride);
    }

    /// <summary>The asset currently referenced, once the world has been entered.</summary>
    public SoundAsset SoundAsset => _soundAsset;

    /// <summary>True while this emitter has a sound or a track playing.</summary>
    public bool IsPlaying
    {
        get
        {
            if (_audioService == null)
            {
                return false;
            }

            return _audioService.IsAlive(_voice) || _audioService.Music.IsAlive(_track);
        }
    }

    protected override bool AllowsMissingSceneData => true;

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        if (!_isBoundForTests)
        {
            _audioService = world?.Game?.AudioSystemComponent?.Service;
            LoadSoundAsset(world);
        }

        if (PlayOnStart)
        {
            Play();
        }
    }

    /// <summary>
    /// Starts the sound. A streaming asset goes to the music player, everything else becomes a
    /// regular voice. Calling it again restarts the sound.
    /// </summary>
    public void Play()
    {
        if (_audioService == null || _soundAsset == null)
        {
            return;
        }

        Stop();

        var owner = Owner?.World;

        if (_soundAsset.IsStreaming)
        {
            _track = _audioService.Music.Play(_soundAsset, 0f, owner);
            return;
        }

        if (_soundAsset.SpatialMode == AudioSpatialMode.None)
        {
            _voice = _audioService.PlaySound(_soundAsset, CreateOverrides(), owner);
            return;
        }

        _lastVoicePosition = AudioScenePose.GetWorldPosition(this);
        _voice = _audioService.PlaySoundAt(_soundAsset, _lastVoicePosition, CreateOverrides(), owner);
        _isVoiceSpatial = _voice.IsValid;
    }

    /// <summary>Stops whatever this emitter started, immediately.</summary>
    public void Stop()
    {
        _isVoiceSpatial = false;

        if (_audioService == null)
        {
            return;
        }

        if (_voice.IsValid)
        {
            _audioService.Stop(_voice);
            _voice = AudioVoiceHandle.None;
        }

        if (_track.IsValid)
        {
            _audioService.Music.Stop(_track);
            _track = MusicTrackHandle.None;
        }
    }

    /// <summary>Fades out over <paramref name="durationSeconds"/>, then stops.</summary>
    public void StopWithFade(float durationSeconds)
    {
        _isVoiceSpatial = false;

        if (_audioService == null)
        {
            return;
        }

        if (_voice.IsValid)
        {
            _audioService.StopWithFade(_voice, durationSeconds);
            _voice = AudioVoiceHandle.None;
        }

        if (_track.IsValid)
        {
            _audioService.Music.Stop(_track, durationSeconds);
            _track = MusicTrackHandle.None;
        }
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        if (!_isVoiceSpatial || _audioService == null)
        {
            return;
        }

        if (!_audioService.IsAlive(_voice))
        {
            _isVoiceSpatial = false;
            return;
        }

        var position = AudioScenePose.GetWorldPosition(this);

        if (position != _lastVoicePosition)
        {
            _lastVoicePosition = position;
            _audioService.SetVoicePosition(_voice, position);
        }
    }

    public override void Detach()
    {
        Stop();
        ReleaseSoundAssetHandle();
        base.Detach();
    }

    public override BoundingBox GetBoundingBox() => AudioScenePose.GetBoundingBox(this);

    /// <summary>Test seam: the voice started by <see cref="Play"/>, to read what the backend received for it.</summary>
    internal AudioVoiceHandle VoiceForTests => _voice;

    /// <summary>Test seam: uses <paramref name="service"/> and <paramref name="asset"/> instead of the ones of the world.</summary>
    internal void BindServicesForTests(AudioService service, SoundAsset asset)
    {
        _audioService = service;
        _soundAsset = asset;
        _isBoundForTests = true;
    }

    public override SoundEmitterComponent Clone() => new(this);

    public override void Load(JObject element)
    {
        base.Load(element);

        SoundAssetId = element.ContainsKey("sound_asset_id") ? element["sound_asset_id"].GetGuid() : Guid.Empty;
        PlayOnStart = element["play_on_start"]?.GetBoolean() ?? PlayOnStart;
        BusName = element["bus_name"]?.GetString() ?? string.Empty;
        VolumeOverride = element["volume_override"]?.GetSingle() ?? 1f;
        PitchOverride = element["pitch_override"]?.GetSingle() ?? 0f;

        IsLoopedOverride = element.ContainsKey("is_looped_override") && element["is_looped_override"].Type != JTokenType.Null
            ? element["is_looped_override"].GetBoolean()
            : null;
    }

    private SoundPlaybackOverrides CreateOverrides()
    {
        var assetParameters = _soundAsset.CreateVoiceParameters();

        return new SoundPlaybackOverrides(
            volume: assetParameters.Volume * VolumeOverride,
            pitch: assetParameters.Pitch + PitchOverride,
            isLooped: IsLoopedOverride,
            busName: BusName);
    }

    private void LoadSoundAsset(World.World world)
    {
        ReleaseSoundAssetHandle();
        _soundAsset = null;

        if (SoundAssetId == Guid.Empty || world?.Game == null)
        {
            return;
        }

        try
        {
            _soundAssetHandle = world.Game.AssetContentManager.Acquire<SoundAsset>(SoundAssetId);
            _soundAsset = _soundAssetHandle.Asset;
        }
        catch (Exception exception)
        {
            Core.Logging.Logs.WriteException(
                new Exception($"SoundEmitterComponent '{Name}' cannot load sound asset '{SoundAssetId}'.", exception));
        }
    }

    /// <summary>Gives back the hold this emitter took on its sound asset (ADR-0037).</summary>
    private void ReleaseSoundAssetHandle()
    {
        _soundAssetHandle?.Dispose();
        _soundAssetHandle = null;
    }

    private static float Sanitize(float value, float min, float max, float fallback)
    {
        return float.IsNaN(value) ? fallback : Math.Clamp(value, min, max);
    }
}
