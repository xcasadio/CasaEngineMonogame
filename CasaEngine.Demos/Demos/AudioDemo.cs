using System;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Scene.Entities.Components;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Exercises the audio system: one-shot and looping sound effects, and the mixing buses.
///
/// Keys:
///   Space      play the sound effect once
///   L          start or stop the looping sound effect
///   F          fade the looping sound out over one second
///   S          stop every sound
///   Up/Down    Master bus volume
///   Left/Right Sfx bus volume
///   M          mute or unmute the Master bus
///   N          mute or unmute the Sfx bus
/// </summary>
public class AudioDemo : Demo
{
    private static readonly Guid ClickSoundAssetId = new("b41f0a6c-2d58-4a19-9f73-0c5e8a91d2b4");
    private const float VolumeStep = 0.1f;

    private CasaEngineGame? _game;
    private SoundAsset? _clickSound;
    private AudioVoiceHandle _loopingVoice = AudioVoiceHandle.None;
    private KeyboardState _previousKeyboard;
    private DynamicSpriteFont? _font;
    private Texture2D? _panelBackground;
    private string _lastAction = "ready";

    public override string Title => "Audio demo";

    public override string Description =>
        "Plays a sound effect once or looping, and shows how the named mixing buses (Master, Sfx) scale it.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        _previousKeyboard = Keyboard.GetState();

        try
        {
            _clickSound = game.AssetContentManager.Load<SoundAsset>(ClickSoundAssetId);
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception("AudioDemo could not load its sound asset.", exception));
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
            _lastAction = "everything stopped";
        }

        var mixer = service.Mixer;

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

        var mixer = audio.Service.Mixer;
        var master = mixer.GetBus(AudioBusNames.Master);
        var sfx = mixer.GetBus(AudioBusNames.Sfx);

        var spriteBatch = game.SpriteBatch;
        spriteBatch.Begin();
        spriteBatch.Draw(_panelBackground, new Rectangle(10, 10, 430, 178), Color.White);

        var y = 16f;
        DrawLine(spriteBatch, ref y, audio.IsAudioAvailable
            ? "Audio device: available"
            : "Audio device: NOT available (everything is silent)");
        DrawLine(spriteBatch, ref y, $"Master  volume {master.Volume:0.00}  muted {master.IsMuted}  gain {master.EffectiveGain:0.00}");
        DrawLine(spriteBatch, ref y, $"Sfx     volume {sfx.Volume:0.00}  muted {sfx.IsMuted}  gain {sfx.EffectiveGain:0.00}");
        DrawLine(spriteBatch, ref y, $"Active voices: {audio.Service.ActiveVoiceCount}  refused: {audio.Service.RefusedVoiceCount}");
        DrawLine(spriteBatch, ref y, $"Last action: {_lastAction}");
        DrawLine(spriteBatch, ref y, "Space one-shot   L loop on/off   F fade out   S stop all");
        DrawLine(spriteBatch, ref y, "Up/Down Master   Left/Right Sfx   M mute Master   N mute Sfx");

        spriteBatch.End();
    }

    public override void Clean()
    {
        var service = _game?.AudioSystemComponent?.Service;
        service?.StopAll();

        _loopingVoice = AudioVoiceHandle.None;
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
