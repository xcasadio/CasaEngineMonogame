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

/// <summary>The text overlay of the audio demo: what is drawn on screen, kept apart from the audio calls.</summary>
public partial class AudioDemo
{
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
        spriteBatch.Draw(_panelBackground, new Rectangle(10, 10, 520, 614), Color.White);

        var y = 16f;
        DrawLine(spriteBatch, ref y, audio.IsAudioAvailable
            ? "Audio device: available"
            : "Audio device: NOT available (everything is silent)");
        DrawLine(spriteBatch, ref y, $"Backend: {service.Backend.GetType().Name}   active voices: {service.ActiveVoiceCount}"
            + (_stressActive ? "   STRESS ON" : string.Empty));
        DrawLine(spriteBatch, ref y, _spuPort != null
            ? $"SPU: active   refused writes {_spuPort.RefusedWriteCount}"
            : _spuTried ? "SPU: unavailable" : "SPU: not started (the stress starts it)");
        if (service.Backend is SoftwareAudioBackend software)
        {
            DrawLine(spriteBatch, ref y, $"Lead {software.LeadMilliseconds} ms   output {software.OutputSampleRate} Hz"
                + $"   underruns {software.UnderrunCount}");
        }

        DrawLine(spriteBatch, ref y, $"Master  volume {master.Volume:0.00}  muted {master.IsMuted}  gain {master.EffectiveGain:0.00}");
        DrawLine(spriteBatch, ref y, $"Sfx     volume {sfx.Volume:0.00}  muted {sfx.IsMuted}  gain {sfx.EffectiveGain:0.00}");
        DrawLine(spriteBatch, ref y, $"Music   volume {music.Volume:0.00}  muted {music.IsMuted}  gain {music.EffectiveGain:0.00}");
        DrawLine(spriteBatch, ref y, $"Voices: {service.ActiveVoiceCount} active, {service.RefusedVoiceCount} refused, {service.StolenVoiceCount} stolen"
            + $"   Music tracks: {service.Music.ActiveTrackCount}");
        DrawLine(spriteBatch, ref y, service.Music.IsAlive(_musicTrack)
            ? $"Music position {service.Music.GetPosition(_musicTrack):mm\\:ss}"
              + $"  queued buffers {service.Music.GetPendingBufferCount(_musicTrack)}"
            : "Music stopped");
        var keys = _hudText ??= AudioHudText.Create(action => DemoBindingText.KeyLabel(DemoInput.Describe(game, action).Key));
        DrawLine(spriteBatch, ref y, service.Music.IsAlive(_loopPointTrack)
            ? $"Loop point ({keys.LoopPoint}) position {service.Music.GetPosition(_loopPointTrack).TotalSeconds:0.0} s"
              + $"  queued buffers {service.Music.GetPendingBufferCount(_loopPointTrack)}"
            : $"Loop point ({keys.LoopPoint}) stopped");
        DrawLine(spriteBatch, ref y, service.Backend is IAudioBusBackend
            ? $"Reverb ({keys.Reverb}) {(_demoReverbOn ? "ON" : "off")}   Filter ({keys.LowPass}) {(_demoFilter != null ? "ON" : "off")}"
              + $"   Ducking ({keys.Ducking}) {(_demoDucking != null ? "ON" : "off")}"
            : $"Effects ({keys.Reverb}, {keys.LowPass}, {keys.Ducking}) absent on this backend (software backend only)");
        DrawLine(spriteBatch, ref y, SpatialModeText);
        DrawLine(spriteBatch, ref y, service.IsAlive(_orbitVoice)
            ? $"Orbit ({keys.Orbit}) ON   distance {NumericsVector3.Distance(_orbitPosition, ListenerPosition):0.0}"
              + $"   x {_orbitPosition.X:0.0}  z {_orbitPosition.Z:0.0}"
            : $"Orbit ({keys.Orbit}) off");
        var intensity = service.GetGameParameter(_intensityParameter);
        DrawLine(spriteBatch, ref y, $"Doppler ({keys.Doppler}) {(_dopplerOn ? "ON" : "off")}   speed of sound {service.SpeedOfSound:0} u/s"
            + (float.IsNaN(intensity) ? $"   intensity ({keys.Intensity}) unset" : $"   intensity ({keys.Intensity}) {intensity:0.0}"));
        DrawLine(spriteBatch, ref y, $"Last action: {_lastAction}");
        for (int i = 0; i < keys.HelpLines.Length; i++)
        {
            DrawLine(spriteBatch, ref y, keys.HelpLines[i]);
        }

        spriteBatch.End();
    }

    private void DrawLine(SpriteBatch spriteBatch, ref float y, string text)
    {
        spriteBatch.DrawString(_font, text, new Vector2(20f, y), Color.White);
        y += 22f;
    }

    private static string EffectsNote(AudioService service)
    {
        return service.Backend is IAudioBusBackend ? string.Empty : " (no effect on this backend)";
    }
}
