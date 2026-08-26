using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Streaming;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Plays a <see cref="SoundAsset"/> from an entity: a sound effect, or a streamed track.
/// </summary>
/// <remarks>
/// Derives from <see cref="EntityComponent"/> and not <see cref="SceneComponent"/> on purpose:
/// the V1 audio is 2D only, so a transform would be carried around without ever being read.
/// Making it a scene component is the change to do the day spatialization arrives.
/// The voice is scoped to the world, so leaving the world stops it, and detaching the component
/// stops it too.
/// </remarks>
[DisplayName("Sound Emitter")]
public class SoundEmitterComponent : EntityComponent
{
    private float _volumeOverride = 1f;
    private float _pitchOverride;
    private string _busName = string.Empty;

    private SoundAsset _soundAsset;
    private AudioService _audioService;
    private AudioVoiceHandle _voice = AudioVoiceHandle.None;
    private MusicTrackHandle _track = MusicTrackHandle.None;

    public SoundEmitterComponent()
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

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        _audioService = world?.Game?.AudioSystemComponent?.Service;
        LoadSoundAsset(world);

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

        _voice = _audioService.PlaySound(_soundAsset, CreateOverrides(), owner);
    }

    /// <summary>Stops whatever this emitter started, immediately.</summary>
    public void Stop()
    {
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

    public override void Detach()
    {
        Stop();
        base.Detach();
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
        _soundAsset = null;

        if (SoundAssetId == Guid.Empty || world?.Game == null)
        {
            return;
        }

        try
        {
            _soundAsset = world.Game.AssetContentManager.Load<SoundAsset>(SoundAssetId);
        }
        catch (Exception exception)
        {
            Core.Logging.Logs.WriteException(
                new Exception($"SoundEmitterComponent '{Name}' cannot load sound asset '{SoundAssetId}'.", exception));
        }
    }

    private static float Sanitize(float value, float min, float max, float fallback)
    {
        return float.IsNaN(value) ? fallback : Math.Clamp(value, min, max);
    }
}
