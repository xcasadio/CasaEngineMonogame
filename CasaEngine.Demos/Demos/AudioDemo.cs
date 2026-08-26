using System;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Streaming;
using CasaEngine.Framework.Scene.Entities.Components;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Exercises the audio system: sound effects, streamed music and the mixing buses.
///
/// Sound effect keys:
///   Space      play the sound effect once
///   L          start or stop the looping sound effect
///   F          fade the looping sound out over one second
///   S          stop every sound
///
/// Music keys (streamed from disk, never fully loaded):
///   P          start the music with a one second fade in, or fade it out over two seconds
///   C          crossfade to the other music track over two seconds
///   PageUp/Dn  Music bus volume
///
/// Bus keys:
///   Up/Down    Master bus volume
///   Left/Right Sfx bus volume
///   M          mute or unmute the Master bus
///   N          mute or unmute the Sfx bus
/// </summary>
public class AudioDemo : Demo
{
    private static readonly Guid ClickSoundAssetId = new("b41f0a6c-2d58-4a19-9f73-0c5e8a91d2b4");
    private static readonly Guid MusicAssetId = new("7c9e5d31-4b62-4a08-8e17-2f6ba0c4d5e9");
    private static readonly Guid PitchedMusicAssetId = new("2e60f8a4-9c13-4d75-b3ea-58c7d1904f26");

    private const float VolumeStep = 0.1f;
    private const float MusicFadeInSeconds = 1f;
    private const float MusicFadeOutSeconds = 2f;
    private const float CrossfadeSeconds = 2f;

    private CasaEngineGame? _game;
    private SoundAsset? _clickSound;
    private SoundAsset? _music;
    private SoundAsset? _pitchedMusic;
    private AudioVoiceHandle _loopingVoice = AudioVoiceHandle.None;
    private MusicTrackHandle _musicTrack = MusicTrackHandle.None;
    private bool _pitchedMusicPlaying;
    private KeyboardState _previousKeyboard;
    private DynamicSpriteFont? _font;
    private Texture2D? _panelBackground;
    private string _lastAction = "ready";

    public override string Title => "Audio demo";

    public override string Description =>
        "Sound effects (one-shot, looping, fade out) and music streamed from disk (fade in/out, crossfade), "
        + "routed through the named mixing buses Master, Sfx and Music.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _previousKeyboard = Keyboard.GetState();

        _clickSound = TryLoad(game, ClickSoundAssetId);
        _music = TryLoad(game, MusicAssetId);
        _pitchedMusic = TryLoad(game, PitchedMusicAssetId);
    }

    private static SoundAsset? TryLoad(CasaEngineGame game, Guid assetId)
    {
        try
        {
            return game.AssetContentManager.Load<SoundAsset>(assetId);
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

        var keyboard = _game.IsActive ? Keyboard.GetState() : new KeyboardState();
        var audio = _game.AudioSystemComponent;

        if (audio == null || _clickSound == null)
        {
            _previousKeyboard = keyboard;
            return;
        }

        var service = audio.Service;

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

        if (WasJustPressed(keyboard, Keys.F) && service.IsAlive(_loopingVoice))
        {
            service.StopWithFade(_loopingVoice, 1f);
            _loopingVoice = AudioVoiceHandle.None;
            _lastAction = "loop fading out over 1s";
        }

        if (WasJustPressed(keyboard, Keys.S))
        {
            service.StopAll();
            _loopingVoice = AudioVoiceHandle.None;
            _musicTrack = MusicTrackHandle.None;
            _lastAction = "everything stopped";
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

        _previousKeyboard = keyboard;
    }

    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        var audio = game.AudioSystemComponent;
        if (audio == null)
        {
            return;
        }

        _font ??= game.FontSystem.GetFont(14);
        if (_panelBackground == null)
        {
            _panelBackground = new Texture2D(game.GraphicsDevice, 1, 1);
            _panelBackground.SetData([new Color(0, 0, 0, 170)]);
        }

        var service = audio.Service;
        var mixer = service.Mixer;
        var master = mixer.GetBus(AudioBusNames.Master);
        var sfx = mixer.GetBus(AudioBusNames.Sfx);
        var music = mixer.GetBus(AudioBusNames.Music);

        var spriteBatch = game.SpriteBatch;
        spriteBatch.Begin();
        spriteBatch.Draw(_panelBackground, new Rectangle(10, 10, 520, 244), Color.White);

        var y = 16f;
        DrawLine(spriteBatch, ref y, audio.IsAudioAvailable
            ? "Audio device: available"
            : "Audio device: NOT available (everything is silent)");
        DrawLine(spriteBatch, ref y, $"Master  volume {master.Volume:0.00}  muted {master.IsMuted}  gain {master.EffectiveGain:0.00}");
        DrawLine(spriteBatch, ref y, $"Sfx     volume {sfx.Volume:0.00}  muted {sfx.IsMuted}  gain {sfx.EffectiveGain:0.00}");
        DrawLine(spriteBatch, ref y, $"Music   volume {music.Volume:0.00}  muted {music.IsMuted}  gain {music.EffectiveGain:0.00}");
        DrawLine(spriteBatch, ref y, $"Voices: {service.ActiveVoiceCount} active, {service.RefusedVoiceCount} refused"
            + $"   Music tracks: {service.Music.ActiveTrackCount}");
        DrawLine(spriteBatch, ref y, service.Music.IsAlive(_musicTrack)
            ? $"Music position {service.Music.GetPosition(_musicTrack):mm\\:ss}"
              + $"  queued buffers {service.Music.GetPendingBufferCount(_musicTrack)}"
            : "Music stopped");
        DrawLine(spriteBatch, ref y, $"Last action: {_lastAction}");
        DrawLine(spriteBatch, ref y, "Space one-shot   L loop on/off   F fade out   S stop all");
        DrawLine(spriteBatch, ref y, "P music on/off   C crossfade      PageUp/PageDown Music");
        DrawLine(spriteBatch, ref y, "Up/Down Master   Left/Right Sfx   M mute Master   N mute Sfx");

        spriteBatch.End();
    }

    public override void Clean()
    {
        var service = _game?.AudioSystemComponent?.Service;
        service?.StopAll();

        _loopingVoice = AudioVoiceHandle.None;
        _musicTrack = MusicTrackHandle.None;
        _panelBackground?.Dispose();
        _panelBackground = null;
        _font = null;
        _clickSound = null;
        _game = null;
    }

    private void DrawLine(SpriteBatch spriteBatch, ref float y, string text)
    {
        spriteBatch.DrawString(_font, text, new Vector2(20f, y), Color.White);
        y += 22f;
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
