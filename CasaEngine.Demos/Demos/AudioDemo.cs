using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Psx;
using CasaEngine.Framework.Audio.Spatial;
using CasaEngine.Framework.Audio.Streaming;
using CasaEngine.Framework.Scene.Entities.Components;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NumericsVector3 = System.Numerics.Vector3;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Exercises the audio system: sound effects, streamed music and the mixing buses.
///
/// Sound effect keys:
///   Space      play the sound effect once
///   L          start or stop the looping sound effect
///   U          fade the looping sound out over one second
///   S          stop every sound
///   B          play a mono beep on a software stereo voice (ADR-0039): left only, then right
///              only, then both channels, one step per press
///
/// Variation and priority keys:
///   V          play the varied click (menu_click_varied.sound): each press draws a volume factor in
///              [0.6, 1] and a pitch offset in [-0.15, 0.15] octave
///   J          start looping clicks at volume 0.05 and priority 1 on every free voice, so that the
///              next Space is refused; S stops them
///   H          play the click with priority 10: when every voice is taken it steals the oldest
///              priority-1 voice (the Voices line counts the steal)
///
/// Music keys (streamed from disk, never fully loaded):
///   P          start the music with a one second fade in, or fade it out over two seconds
///   C          crossfade to the other music track over two seconds
///   Q          loop point (E19.L-a): play or stop loop_point_demo.sound, a 3 second 44.1 kHz stream whose loop
///              starts at 1 second. Expected sound: a 440 Hz tone once, then 660 Hz beeps every half second
///              for ever, with no gap or click at the seam. The position jumps back from about 3 s to 1 s
///              (it leads the sound by the queue depth, about 2 s). Before the loop point existed, the tone
///              came back every 3 seconds
///   PageUp/Dn  Music bus volume
///
/// Bus keys:
///   Up/Down    Master bus volume
///   Left/Right Sfx bus volume
///   M          mute or unmute the Master bus
///   N          mute or unmute the Sfx bus
///
/// Effect keys (software backend only; the panel says so on the MonoGame backend):
///   R          reverb: a send from Sfx to a return bus holding a ReverbEffect, on or off
///   T          low-pass filter (600 Hz) inserted on the Music bus, on or off
///   D          ducking of Music by Sfx, on or off (play the music with P, then a sound effect)
///
/// Spatial keys (a looping click created in code, Spatial3D, inverse distance clamped, reference distance 1, max
/// distance 50, heard by a listener at the origin looking down -Z):
///   O          start or stop the orbit: the loop circles the listener in the X/Z plane at a quarter turn per
///              second while its radius swings between 2 and 12 units every 8 seconds; the pan follows the side,
///              the gain follows the distance
///   K          Doppler on or off (Doppler factor 1, speed of sound 16 units per second): the pitch rises while
///              the loop comes closer and falls while it moves away; a running orbit restarts its voice, because
///              the service reads the Doppler factor when a voice starts
///   I          game parameter demo_intensity: 0, then 0.5, then 1, one step per press (bound to the volume,
///              0.3 to 1, and to the pitch, -0.3 to +0.3 octave); never written it is neutral
///
/// Stress key:
///   G          start or stop the GC stress: looping sound and music play continuously while the
///              demo allocates garbage every frame and forces a full collection every 500 ms
///
/// Without keyboard, CASAENGINE_AUDIO_STRESS_SECONDS=&lt;n&gt; starts the stress when the demo is
/// initialized, logs the backend and its underrun count every second, then logs a summary after
/// n seconds and exits the game.
/// </summary>
public partial class AudioDemo : Demo
{
    private static readonly Guid ClickSoundAssetId = new("b41f0a6c-2d58-4a19-9f73-0c5e8a91d2b4");
    private static readonly Guid VariedClickSoundAssetId = new("f5c920e0-ab46-45d8-81c0-feb6f481b27d");
    private static readonly Guid MusicAssetId = new("7c9e5d31-4b62-4a08-8e17-2f6ba0c4d5e9");
    private static readonly Guid PitchedMusicAssetId = new("2e60f8a4-9c13-4d75-b3ea-58c7d1904f26");
    private static readonly Guid LoopPointAssetId = new("f138a08a-bb02-4c28-b186-72e9caea112a");

    private const float VolumeStep = 0.1f;
    private const float MusicFadeInSeconds = 1f;
    private const float MusicFadeOutSeconds = 2f;
    private const float CrossfadeSeconds = 2f;

    private const float FillVoiceVolume = 0.05f;
    private const int FillVoicePriority = 1;
    private const int HighPriority = 10;

    private const string StressSecondsVariable = "CASAENGINE_AUDIO_STRESS_SECONDS";
    private const int StressGcIntervalMilliseconds = 500;
    private const int StressArrayCount = 8;
    private const int StressArrayBytes = 512 * 1024;
    private const string StressReverbBusName = "StressReverb";

    private const string DemoReverbBusName = "DemoReverb";
    private const float DemoReverbSendLevel = 0.5f;
    private const float DemoFilterFrequencyHz = 600f;

    private const int BeepSampleRate = 22050;
    private const float BeepFrequency = 440f;
    private const float BeepSeconds = 0.3f;
    private const float BeepAmplitude = 12000f;

    private const string IntensityParameterName = "demo_intensity";
    private const float SpatialReferenceDistance = 1f;
    private const float SpatialMaxDistance = 50f;
    private const float OrbitTurnsPerSecond = 0.25f;
    private const float OrbitRadiusMiddle = 7f;
    private const float OrbitRadiusSwing = 5f;
    private const float OrbitRadiusCyclesPerSecond = 0.125f;
    // Both motions repeat after 8 seconds (4 s per turn, 8 s per radius cycle): the clock wraps there without a jump.
    private const float OrbitPatternSeconds = 8f;
    private const float DemoDopplerFactor = 1f;
    // A circle centred on the listener has no radial speed, so only the radius swing makes a Doppler shift. Its
    // largest radial speed is 5 x 2 pi x 0.125 = 3.93 units per second; at 16 units per second the ratio goes from
    // 16 / (16 + 3.93) = 0.80 moving away to 16 / (16 - 3.93) = 1.33 coming closer, about -0.32 to +0.41 octave.
    private const float DemoSpeedOfSound = 16f;
    private const string SpatialModeText = "Spatial loop: Spatial3D, inverse distance clamped, ref 1, max 50";

    private static readonly float[] IntensitySteps = [0f, 0.5f, 1f];
    private static readonly NumericsVector3 ListenerPosition = NumericsVector3.Zero;

    private CasaEngineGame? _game;
    private AssetHandle<SoundAsset>? _clickSoundHandle;
    private AssetHandle<SoundAsset> _variedClickSoundHandle;
    private AssetHandle<SoundAsset>? _musicHandle;
    private AssetHandle<SoundAsset>? _pitchedMusicHandle;
    private AssetHandle<SoundAsset>? _loopPointHandle;
    private SoundAsset? _clickSound;
    private SoundAsset _variedClickSound;
    private SoundAsset? _music;
    private SoundAsset? _pitchedMusic;
    private SoundAsset? _loopPoint;
    private PcmAudioClip? _stereoBeep;
    private int _stereoBeepStep;
    private AudioVoiceHandle _loopingVoice = AudioVoiceHandle.None;
    private MusicTrackHandle _musicTrack = MusicTrackHandle.None;
    private MusicTrackHandle _loopPointTrack = MusicTrackHandle.None;
    private bool _pitchedMusicPlaying;
    private KeyboardState _previousKeyboard;
    private DynamicSpriteFont? _font;
    private Texture2D? _panelBackground;
    private string _lastAction = "ready";

    private bool _stressActive;
    private int _stressDurationSeconds;
    private bool _stressExitWhenDone;
    private Stopwatch _stressClock = new();
    private bool _stressStarted;
    private long _stressLastGcMilliseconds;
    private int _stressLoggedSeconds;
    private int _stressGcCount;
    private byte[] _stressGarbageSink = [];
    private AudioBus _stressReverbBus;

    private AudioBus _demoReverbBus;
    private BiquadFilterEffect _demoFilter;
    private DuckingEffect _demoDucking;
    private bool _demoReverbOn;

    private PsxSpuPort _spuPort;
    private bool _spuTried;

    private SoundAsset _spatialLoop;
    private AudioVoiceHandle _orbitVoice = AudioVoiceHandle.None;
    private float _orbitSeconds;
    private NumericsVector3 _orbitPosition;
    private bool _dopplerOn;
    private int _intensityParameter = -1;
    private int _intensityStep = -1;
    private bool _listenerSet;
    private bool _speedOfSoundSaved;
    private float _savedSpeedOfSound;

    public override string Title => "Audio demo";

    public override string Description =>
        "Sound effects (one-shot, looping, fade out) and streamed music (fade, crossfade) routed through the " +
        "Master, Sfx and Music buses, with effects and a spatial loop that orbits the listener. The text on " +
        "screen shows the state of the mixer.";

    public override IReadOnlyList<DemoControl> Controls { get; } = new DemoControl[]
    {
        new("Space", "play a one-shot sound"),
        new("L", "start or stop a loop (U: fade it out)"),
        new("S", "stop every sound"),
        new("P", "start or stop the music (C: crossfade to the other track)"),
        new("Up, Down", "Master volume (M: mute)"),
        new("Left, Right", "Sfx volume (N: mute)"),
        new("Page Up, Page Down", "Music volume"),
        new("R, T, D", "reverb, low-pass on Music, ducking of Music by Sfx"),
        new("O, K, I", "spatial orbit, Doppler, intensity parameter"),
        new("B, V, H, J, Q, G", "stereo test, varied click, priority and voice-stealing tests, loop point, GC stress"),
    };

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "With ducking on, the music drops while a sound effect plays.",
        "With the orbit on, the loop pans from one ear to the other and its volume follows the distance.",
    };

    public override string DocumentationPath => "docs/engine/audio-system.md";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _previousKeyboard = DemoKeyboard.Read(game);

        _clickSoundHandle = TryAcquire(game, ClickSoundAssetId);
        _clickSound = _clickSoundHandle?.Asset;
        _variedClickSoundHandle = TryAcquire(game, VariedClickSoundAssetId);
        _variedClickSound = _variedClickSoundHandle?.Asset;
        _musicHandle = TryAcquire(game, MusicAssetId);
        _music = _musicHandle?.Asset;
        _pitchedMusicHandle = TryAcquire(game, PitchedMusicAssetId);
        _pitchedMusic = _pitchedMusicHandle?.Asset;
        _loopPointHandle = TryAcquire(game, LoopPointAssetId);
        _loopPoint = _loopPointHandle?.Asset;
        _stereoBeep = CreateStereoBeep();

        var service = game.AudioSystemComponent?.Service;
        if (service != null)
        {
            StartSpatial(service);
        }

        var stressText = Environment.GetEnvironmentVariable(StressSecondsVariable);
        if (int.TryParse(stressText, out var stressSeconds) && stressSeconds > 0)
        {
            _stressDurationSeconds = stressSeconds;
            _stressExitWhenDone = true;
            _stressActive = true;
            _stressStarted = false;
        }
    }

    /// <summary>
    /// The demo's wav files are stereo, and a software stereo voice plays a mono clip: the beep is
    /// synthesized here instead, a short sine with a linear fade in and out.
    /// </summary>
    private static PcmAudioClip CreateStereoBeep()
    {
        var sampleCount = (int)(BeepSampleRate * BeepSeconds);
        var fadeSamples = sampleCount / 10;
        var samples = new short[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var envelope = Math.Min(1f, Math.Min(i, sampleCount - 1 - i) / (float)fadeSamples);
            var value = MathF.Sin(2f * MathF.PI * BeepFrequency * i / BeepSampleRate) * BeepAmplitude * envelope;
            samples[i] = (short)value;
        }

        return new PcmAudioClip(samples, BeepSampleRate, 1);
    }

    /// <summary>
    /// Registers the demo as the audio listener (the demo is not an entity, the service is enough), sets the speed of
    /// sound of the orbit, resolves the game parameter and builds the spatial loop from the click asset.
    /// </summary>
    private void StartSpatial(AudioService service)
    {
        service.SetListener(this, AudioListenerPose.Create(
            ListenerPosition, new NumericsVector3(0f, 0f, -1f), new NumericsVector3(0f, 1f, 0f)));
        _listenerSet = true;

        _savedSpeedOfSound = service.SpeedOfSound;
        _speedOfSoundSaved = true;
        service.SpeedOfSound = DemoSpeedOfSound;

        _intensityParameter = service.GetGameParameterIndex(IntensityParameterName);
        _intensityStep = -1;
        _dopplerOn = false;
        _spatialLoop = _clickSound != null ? CreateSpatialLoop(_clickSound) : null;
    }

    /// <summary>
    /// A copy of the click asset's settings, looped and spatial, with a volume and a pitch binding on
    /// <see cref="IntensityParameterName"/>. Built in code: no content file is added for the demo.
    /// </summary>
    private static SoundAsset CreateSpatialLoop(SoundAsset click)
    {
        var asset = new SoundAsset
        {
            Name = "spatial_click_loop",
            AudioFileAssetId = click.AudioFileAssetId,
            Volume = click.Volume,
            Pitch = click.Pitch,
            BusName = click.BusName,
            Priority = click.Priority,
            IsLooped = true,
            SpatialMode = AudioSpatialMode.Spatial3D,
            DistanceModel = AudioDistanceModel.InverseDistanceClamped,
            ReferenceDistance = SpatialReferenceDistance,
            MaxDistance = SpatialMaxDistance,
            DopplerFactor = 0f,
        };

        asset.SetParameterBindings(
        [
            new AudioParameterBinding(IntensityParameterName, AudioParameterTarget.Volume, 0f, 1f, 0.3f, 1f),
            new AudioParameterBinding(IntensityParameterName, AudioParameterTarget.Pitch, 0f, 1f, -0.3f, 0.3f),
        ]);

        return asset;
    }

    private void StopSpatial(AudioService service)
    {
        if (_listenerSet)
        {
            service.RemoveListener(this);
            _listenerSet = false;
        }

        if (_speedOfSoundSaved)
        {
            service.SpeedOfSound = _savedSpeedOfSound;
            _speedOfSoundSaved = false;
        }
    }

    private static AssetHandle<SoundAsset>? TryAcquire(CasaEngineGame game, Guid assetId)
    {
        try
        {
            return game.AssetContentManager.Acquire<SoundAsset>(assetId);
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"AudioDemo could not load sound asset '{assetId}'.", exception));
            return null;
        }
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        // Nothing to look at: this demo is about what you hear.
        base.InitializeCamera(camera);
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null)
        {
            return;
        }

        var keyboard = DemoKeyboard.Read(_game);
        var audio = _game.AudioSystemComponent;

        if (audio == null || _clickSound == null)
        {
            _previousKeyboard = keyboard;
            return;
        }

        var service = audio.Service;

        if (WasJustPressed(keyboard, Keys.G))
        {
            ToggleStress(service);
        }

        UpdateStress(service);

        if (WasJustPressed(keyboard, Keys.Space))
        {
            var voice = service.PlaySound(_clickSound, _game.GameManager.CurrentWorld);
            _lastAction = voice.IsValid ? "one-shot played" : "one-shot refused (no voice left)";
        }

        if (WasJustPressed(keyboard, Keys.L))
        {
            if (service.IsAlive(_loopingVoice))
            {
                service.Stop(_loopingVoice);
                _loopingVoice = AudioVoiceHandle.None;
                _lastAction = "loop stopped";
            }
            else
            {
                _loopingVoice = service.PlaySound(
                    _clickSound,
                    new SoundPlaybackOverrides(isLooped: true),
                    _game.GameManager.CurrentWorld);
                _lastAction = _loopingVoice.IsValid ? "loop started" : "loop refused (no voice left)";
            }
        }

        if (WasJustPressed(keyboard, Keys.U) && service.IsAlive(_loopingVoice))
        {
            service.StopWithFade(_loopingVoice, 1f);
            _loopingVoice = AudioVoiceHandle.None;
            _lastAction = "loop fading out over 1s";
        }

        if (WasJustPressed(keyboard, Keys.V))
        {
            PlayVariedClick(service);
        }

        if (WasJustPressed(keyboard, Keys.J))
        {
            FillFreeVoices(service, _clickSound);
        }

        if (WasJustPressed(keyboard, Keys.H))
        {
            PlayHighPriorityClick(service, _clickSound);
        }

        if (WasJustPressed(keyboard, Keys.S))
        {
            service.StopAll();
            _loopingVoice = AudioVoiceHandle.None;
            _musicTrack = MusicTrackHandle.None;
            _loopPointTrack = MusicTrackHandle.None;
            _lastAction = "everything stopped";
        }

        if (WasJustPressed(keyboard, Keys.B))
        {
            PlayStereoBeep(service);
        }

        if (WasJustPressed(keyboard, Keys.P))
        {
            TogglePlayMusic(service);
        }

        if (WasJustPressed(keyboard, Keys.C))
        {
            CrossfadeMusic(service);
        }

        var mixer = service.Mixer;

        if (WasJustPressed(keyboard, Keys.Q))
        {
            ToggleLoopPointDemo(service);
        }

        if (WasJustPressed(keyboard, Keys.PageUp))
        {
            ChangeVolume(mixer, AudioBusNames.Music, VolumeStep);
        }

        if (WasJustPressed(keyboard, Keys.PageDown))
        {
            ChangeVolume(mixer, AudioBusNames.Music, -VolumeStep);
        }

        if (WasJustPressed(keyboard, Keys.Up))
        {
            ChangeVolume(mixer, AudioBusNames.Master, VolumeStep);
        }

        if (WasJustPressed(keyboard, Keys.Down))
        {
            ChangeVolume(mixer, AudioBusNames.Master, -VolumeStep);
        }

        if (WasJustPressed(keyboard, Keys.Right))
        {
            ChangeVolume(mixer, AudioBusNames.Sfx, VolumeStep);
        }

        if (WasJustPressed(keyboard, Keys.Left))
        {
            ChangeVolume(mixer, AudioBusNames.Sfx, -VolumeStep);
        }

        if (WasJustPressed(keyboard, Keys.M))
        {
            ToggleMute(mixer, AudioBusNames.Master);
        }

        if (WasJustPressed(keyboard, Keys.N))
        {
            ToggleMute(mixer, AudioBusNames.Sfx);
        }

        if (WasJustPressed(keyboard, Keys.R))
        {
            ToggleDemoReverb(service);
        }

        if (WasJustPressed(keyboard, Keys.T))
        {
            ToggleDemoFilter(service);
        }

        if (WasJustPressed(keyboard, Keys.D))
        {
            ToggleDemoDucking(service);
        }

        if (WasJustPressed(keyboard, Keys.O))
        {
            ToggleOrbit(service);
        }

        if (WasJustPressed(keyboard, Keys.K))
        {
            ToggleDoppler(service);
        }

        if (WasJustPressed(keyboard, Keys.I))
        {
            CycleIntensity(service);
        }

        UpdateOrbit(service, (float)gameTime.ElapsedGameTime.TotalSeconds);

        _previousKeyboard = keyboard;
    }

    public override void Clean()
    {
        var service = _game?.AudioSystemComponent?.Service;
        service?.StopAll();
        StopSpu();
        if (service != null)
        {
            StopDemoEffects(service);
            StopSpatial(service);
        }

        _orbitVoice = AudioVoiceHandle.None;
        _orbitSeconds = 0f;
        _spatialLoop = null;
        _dopplerOn = false;
        _intensityParameter = -1;
        _intensityStep = -1;
        _loopingVoice = AudioVoiceHandle.None;
        _musicTrack = MusicTrackHandle.None;
        _panelBackground?.Dispose();
        _panelBackground = null;
        _font = null;
        _clickSound = null;
        _variedClickSound = null;
        _music = null;
        _pitchedMusic = null;
        _loopPoint = null;
        _loopPointTrack = MusicTrackHandle.None;
        _clickSoundHandle?.Dispose();
        _clickSoundHandle = null;
        _variedClickSoundHandle?.Dispose();
        _variedClickSoundHandle = null;
        _musicHandle?.Dispose();
        _musicHandle = null;
        _pitchedMusicHandle?.Dispose();
        _pitchedMusicHandle = null;
        _loopPointHandle?.Dispose();
        _loopPointHandle = null;
        _stereoBeep?.Dispose();
        _stereoBeep = null;
        _stereoBeepStep = 0;
        _stressActive = false;
        _stressStarted = false;
        _stressGarbageSink = [];
        _game = null;
    }

    private void ToggleDemoReverb(AudioService service)
    {
        var mixer = service.Mixer;

        if (_demoReverbBus == null && !mixer.TryGetBus(DemoReverbBusName, out _demoReverbBus))
        {
            _demoReverbBus = mixer.CreateBus(DemoReverbBusName, AudioBusNames.Master);
            _demoReverbBus.AddEffect(new ReverbEffect(0.8f));
        }

        _demoReverbOn = !_demoReverbOn;
        mixer.GetBus(AudioBusNames.Sfx).SetSend(_demoReverbBus, _demoReverbOn ? DemoReverbSendLevel : 0f);
        _lastAction = $"reverb send {(_demoReverbOn ? "on" : "off")}" + EffectsNote(service);
    }

    private void ToggleDemoFilter(AudioService service)
    {
        var music = service.Mixer.GetBus(AudioBusNames.Music);

        if (_demoFilter != null)
        {
            music.RemoveEffect(_demoFilter);
            _demoFilter = null;
        }
        else
        {
            _demoFilter = new BiquadFilterEffect(BiquadFilterType.LowPass, DemoFilterFrequencyHz);
            music.AddEffect(_demoFilter);
        }

        _lastAction = $"music low-pass {(_demoFilter != null ? "on" : "off")}" + EffectsNote(service);
    }

    private void ToggleDemoDucking(AudioService service)
    {
        var mixer = service.Mixer;
        var music = mixer.GetBus(AudioBusNames.Music);

        if (_demoDucking != null)
        {
            music.RemoveEffect(_demoDucking);
            _demoDucking = null;
        }
        else
        {
            _demoDucking = new DuckingEffect(mixer.GetBus(AudioBusNames.Sfx));
            music.AddEffect(_demoDucking);
        }

        _lastAction = $"music ducking {(_demoDucking != null ? "on" : "off")}" + EffectsNote(service);
    }

    private void ToggleOrbit(AudioService service)
    {
        if (service.IsAlive(_orbitVoice))
        {
            service.Stop(_orbitVoice);
            _orbitVoice = AudioVoiceHandle.None;
            _lastAction = "orbit stopped";
            return;
        }

        if (_spatialLoop == null)
        {
            _lastAction = "spatial loop unavailable (click asset missing)";
            return;
        }

        _orbitSeconds = 0f;
        _orbitPosition = ComputeOrbitPosition(_orbitSeconds);
        _orbitVoice = service.PlaySoundAt(_spatialLoop, _orbitPosition, SoundPlaybackOverrides.None, _game?.GameManager.CurrentWorld);
        _lastAction = _orbitVoice.IsValid ? "orbit started" : "orbit refused (no voice left)";
    }

    /// <summary>
    /// The service reads the Doppler factor of the asset when a voice starts: the factor is set on the spatial loop, and
    /// a running orbit restarts its voice at its current position so that the change is heard at once.
    /// </summary>
    private void ToggleDoppler(AudioService service)
    {
        if (_spatialLoop == null)
        {
            _lastAction = "spatial loop unavailable (click asset missing)";
            return;
        }

        _dopplerOn = !_dopplerOn;
        _spatialLoop.DopplerFactor = _dopplerOn ? DemoDopplerFactor : 0f;

        if (!service.IsAlive(_orbitVoice))
        {
            _lastAction = $"Doppler {(_dopplerOn ? "on" : "off")} (heard once the orbit runs, O)";
            return;
        }

        service.Stop(_orbitVoice);
        _orbitVoice = service.PlaySoundAt(_spatialLoop, _orbitPosition, SoundPlaybackOverrides.None, _game?.GameManager.CurrentWorld);
        _lastAction = _orbitVoice.IsValid
            ? $"Doppler {(_dopplerOn ? "on" : "off")}, orbit voice restarted"
            : "orbit voice refused on restart (no voice left)";
    }

    private void CycleIntensity(AudioService service)
    {
        _intensityStep = (_intensityStep + 1) % IntensitySteps.Length;
        var value = IntensitySteps[_intensityStep];
        service.SetGameParameter(_intensityParameter, value);
        _lastAction = $"{IntensityParameterName} = {value:0.0}";
    }

    /// <summary>
    /// Moves the orbiting voice every frame, so that the service measures its speed for the Doppler shift. Allocation
    /// free. A voice stopped elsewhere (S, the stress G) ends the orbit.
    /// </summary>
    private void UpdateOrbit(AudioService service, float elapsedSeconds)
    {
        if (!_orbitVoice.IsValid)
        {
            return;
        }

        if (!service.IsAlive(_orbitVoice))
        {
            _orbitVoice = AudioVoiceHandle.None;
            _lastAction = "orbit stopped (its voice was stopped)";
            return;
        }

        _orbitSeconds += elapsedSeconds;
        if (_orbitSeconds >= OrbitPatternSeconds)
        {
            _orbitSeconds -= OrbitPatternSeconds;
        }

        _orbitPosition = ComputeOrbitPosition(_orbitSeconds);
        service.SetVoicePosition(_orbitVoice, _orbitPosition);
    }

    /// <summary>
    /// Position on the orbit around the listener: it starts in front (-Z) and turns towards the right (+X), the radius
    /// swinging between <see cref="OrbitRadiusMiddle"/> - <see cref="OrbitRadiusSwing"/> and
    /// <see cref="OrbitRadiusMiddle"/> + <see cref="OrbitRadiusSwing"/>.
    /// </summary>
    private static NumericsVector3 ComputeOrbitPosition(float seconds)
    {
        var angle = 2f * MathF.PI * OrbitTurnsPerSecond * seconds;
        var radius = OrbitRadiusMiddle + OrbitRadiusSwing * MathF.Sin(2f * MathF.PI * OrbitRadiusCyclesPerSecond * seconds);

        return new NumericsVector3(
            ListenerPosition.X + radius * MathF.Sin(angle),
            ListenerPosition.Y,
            ListenerPosition.Z - radius * MathF.Cos(angle));
    }

    private void StopDemoEffects(AudioService service)
    {
        var mixer = service.Mixer;

        if (_demoReverbBus != null)
        {
            mixer.GetBus(AudioBusNames.Sfx).SetSend(_demoReverbBus, 0f);
        }

        if (_demoFilter != null)
        {
            mixer.GetBus(AudioBusNames.Music).RemoveEffect(_demoFilter);
        }

        if (_demoDucking != null)
        {
            mixer.GetBus(AudioBusNames.Music).RemoveEffect(_demoDucking);
        }

        _demoReverbOn = false;
        _demoFilter = null;
        _demoDucking = null;
    }

    private void PlayVariedClick(AudioService service)
    {
        if (_variedClickSound == null)
        {
            _lastAction = "varied click asset is missing";
            return;
        }

        var voice = service.PlaySound(_variedClickSound, _game?.GameManager.CurrentWorld);
        _lastAction = voice.IsValid ? "varied click played" : "varied click refused (no voice left)";
    }

    /// <summary>
    /// Starts a quiet looping click of priority 1 on every voice the backend still has free, so that a sound without
    /// priority is then refused and a sound of a higher priority steals one of them.
    /// </summary>
    private void FillFreeVoices(AudioService service, SoundAsset clickSound)
    {
        var freeVoices = service.Backend.VoiceCapacity - service.Backend.ActiveVoiceCount;
        var overrides = new SoundPlaybackOverrides(isLooped: true, volume: FillVoiceVolume) { Priority = FillVoicePriority };
        var started = 0;

        for (var i = 0; i < freeVoices; i++)
        {
            if (!service.PlaySound(clickSound, overrides, _game?.GameManager.CurrentWorld).IsValid)
            {
                break;
            }

            started++;
        }

        _lastAction = started > 0
            ? $"{started} priority-1 loops started"
            : "no free voice to fill";
    }

    private void PlayHighPriorityClick(AudioService service, SoundAsset clickSound)
    {
        var stolenBefore = service.StolenVoiceCount;
        var overrides = new SoundPlaybackOverrides { Priority = HighPriority };
        var voice = service.PlaySound(clickSound, overrides, _game?.GameManager.CurrentWorld);

        if (!voice.IsValid)
        {
            _lastAction = "priority 10 click refused (no voice to steal)";
        }
        else
        {
            _lastAction = service.StolenVoiceCount > stolenBefore
                ? "priority 10 click played, a voice was stolen"
                : "priority 10 click played, a voice was free";
        }
    }

    private void PlayStereoBeep(AudioService service)
    {
        if (_stereoBeep == null)
        {
            _lastAction = "stereo beep unavailable (no audio hardware)";
            return;
        }

        var (leftGain, rightGain, label) = _stereoBeepStep switch
        {
            0 => (1f, 0f, "left only"),
            1 => (0f, 1f, "right only"),
            _ => (1f, 1f, "both channels"),
        };
        _stereoBeepStep = (_stereoBeepStep + 1) % 3;

        var voice = service.PlayClipStereo(
            _stereoBeep, AudioBusNames.Sfx, AudioVoiceParameters.Default, leftGain, rightGain, _game?.GameManager.CurrentWorld);
        _lastAction = voice.IsValid ? $"stereo beep, {label}" : "stereo beep refused";
    }

    private void TogglePlayMusic(AudioService service)
    {
        if (service.Music.IsAlive(_musicTrack))
        {
            service.Music.Stop(_musicTrack, MusicFadeOutSeconds);
            _musicTrack = MusicTrackHandle.None;
            _lastAction = $"music fading out over {MusicFadeOutSeconds:0}s";
            return;
        }

        if (_music == null)
        {
            _lastAction = "music asset is missing";
            return;
        }

        _musicTrack = service.Music.Play(_music, MusicFadeInSeconds, _game?.GameManager.CurrentWorld);
        _pitchedMusicPlaying = false;
        _lastAction = _musicTrack.IsValid ? "music fading in" : "music refused";
    }

    private void ToggleLoopPointDemo(AudioService service)
    {
        if (service.Music.IsAlive(_loopPointTrack))
        {
            service.Music.Stop(_loopPointTrack, MusicFadeOutSeconds);
            _loopPointTrack = MusicTrackHandle.None;
            _lastAction = $"loop point music fading out over {MusicFadeOutSeconds:0}s";
            return;
        }

        if (_loopPoint == null)
        {
            _lastAction = "loop point asset is missing";
            return;
        }

        _loopPointTrack = service.Music.Play(_loopPoint, 0f, _game?.GameManager.CurrentWorld);
        _lastAction = _loopPointTrack.IsValid ? "loop point music playing" : "loop point music refused";
    }

    private void CrossfadeMusic(AudioService service)
    {
        if (!service.Music.IsAlive(_musicTrack))
        {
            _lastAction = "nothing to crossfade from, press P first";
            return;
        }

        var next = _pitchedMusicPlaying ? _music : _pitchedMusic;
        if (next == null)
        {
            _lastAction = "the other music asset is missing";
            return;
        }

        var newTrack = service.Music.Crossfade(_musicTrack, next, CrossfadeSeconds, _game?.GameManager.CurrentWorld);
        if (!newTrack.IsValid)
        {
            _lastAction = "crossfade refused, current music kept";
            return;
        }

        _musicTrack = newTrack;
        _pitchedMusicPlaying = !_pitchedMusicPlaying;
        _lastAction = $"crossfading to {next.Name} over {CrossfadeSeconds:0}s";
    }

    private void ChangeVolume(AudioMixer mixer, string busName, float delta)
    {
        var bus = mixer.GetBus(busName);
        bus.Volume += delta;
        _lastAction = $"{busName} volume {bus.Volume:0.00}";
    }

    private void ToggleMute(AudioMixer mixer, string busName)
    {
        var bus = mixer.GetBus(busName);
        bus.IsMuted = !bus.IsMuted;
        _lastAction = $"{busName} {(bus.IsMuted ? "muted" : "unmuted")}";
    }

    private bool WasJustPressed(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }
}
