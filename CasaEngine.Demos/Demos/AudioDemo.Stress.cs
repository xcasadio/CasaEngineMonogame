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

/// <summary>The GC stress run of the audio demo (key G, or CASAENGINE_AUDIO_STRESS_SECONDS): validation code, not part of what the demo teaches.</summary>
public partial class AudioDemo
{
    private void ToggleStress(AudioService service)
    {
        if (_stressActive)
        {
            _stressActive = false;
            _stressStarted = false;
            _stressGarbageSink = [];
            StopSpu();
            StopStressReverb(service);
            service.StopAll();
            _loopingVoice = AudioVoiceHandle.None;
            _musicTrack = MusicTrackHandle.None;
            _lastAction = "stress stopped";
            return;
        }

        _stressDurationSeconds = 0;
        _stressExitWhenDone = false;
        _stressActive = true;
        _stressStarted = false;
    }

    private static int GetUnderrunCount(AudioService service)
    {
        return service.Backend is SoftwareAudioBackend software ? software.UnderrunCount : 0;
    }

    private static void LogMonoGameOpenAlState()
    {
        // Reads the private field on purpose: the public Instance property would initialize OpenAL.
        try
        {
            var type = typeof(Microsoft.Xna.Framework.Audio.SoundEffect).Assembly.GetType("Microsoft.Xna.Framework.Audio.OpenALSoundController");
            if (type == null)
            {
                Logs.WriteInfo("Audio stress: monogame-openal-initialized=unknown (type OpenALSoundController not found)");
                return;
            }

            var field = type.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
            {
                Logs.WriteInfo("Audio stress: monogame-openal-initialized=unknown (static field _instance not found)");
                return;
            }

            Logs.WriteInfo($"Audio stress: monogame-openal-initialized={(field.GetValue(null) != null ? "true" : "false")}");
        }
        catch (Exception exception)
        {
            Logs.WriteInfo($"Audio stress: monogame-openal-initialized=unknown ({exception.GetType().Name}: {exception.Message})");
        }
    }

    private void UpdateStress(AudioService service)
    {
        if (!_stressActive || _game == null)
        {
            return;
        }

        var backendName = service.Backend.GetType().Name;

        if (!_stressStarted)
        {
            LogMonoGameOpenAlState();

            if (!service.IsAlive(_loopingVoice) && _clickSound != null)
            {
                _loopingVoice = service.PlaySound(
                    _clickSound,
                    new SoundPlaybackOverrides(isLooped: true),
                    _game.GameManager.CurrentWorld);
            }

            if (!service.Music.IsAlive(_musicTrack) && _music != null)
            {
                _musicTrack = service.Music.Play(_music, MusicFadeInSeconds, _game.GameManager.CurrentWorld);
                _pitchedMusicPlaying = false;
            }

            _stressGcCount = 0;
            _stressLoggedSeconds = 0;
            _stressLastGcMilliseconds = 0;
            _stressClock.Restart();
            _stressStarted = true;
            StartSpu(service);
            StartStressReverb(service);
            _lastAction = "stress running";
            return;
        }

        // Garbage: large short-lived arrays, which live on the large object heap and need gen 2.
        for (var i = 0; i < StressArrayCount; i++)
        {
            _stressGarbageSink = new byte[StressArrayBytes];
        }

        var elapsedMilliseconds = _stressClock.ElapsedMilliseconds;
        if (elapsedMilliseconds - _stressLastGcMilliseconds >= StressGcIntervalMilliseconds)
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            _stressGcCount++;
            _stressLastGcMilliseconds = elapsedMilliseconds;
        }

        var elapsedSeconds = (int)(elapsedMilliseconds / 1000);
        while (_stressLoggedSeconds < elapsedSeconds)
        {
            _stressLoggedSeconds++;
            Logs.WriteInfo($"Audio stress: backend={backendName} elapsed={_stressLoggedSeconds} underruns={GetUnderrunCount(service)}");
        }

        if (_stressDurationSeconds > 0 && elapsedSeconds >= _stressDurationSeconds)
        {
            Logs.WriteInfo($"Audio stress done: backend={backendName} seconds={_stressDurationSeconds}"
                + $" underruns={GetUnderrunCount(service)} gc={_stressGcCount}");
            _stressActive = false;
            _stressStarted = false;
            _stressGarbageSink = [];
            StopSpu();
            StopStressReverb(service);

            if (_stressExitWhenDone)
            {
                _game.Exit();
            }
        }
    }

    // The stress also runs a reverb send (Sfx and Music to a return bus holding a Freeverb) and the Master limiter, so the
    // 60 second run covers them (T5.4). Without the bus capability the sends and the limiter are absent (one log line).
    private void StartStressReverb(AudioService service)
    {
        var mixer = service.Mixer;

        if (_stressReverbBus == null && !mixer.TryGetBus(StressReverbBusName, out _stressReverbBus))
        {
            _stressReverbBus = mixer.CreateBus(StressReverbBusName, AudioBusNames.Master);
            _stressReverbBus.AddEffect(new ReverbEffect(0.8f));
        }

        mixer.GetBus(AudioBusNames.Sfx).SetSend(_stressReverbBus, 0.4f);
        mixer.GetBus(AudioBusNames.Music).SetSend(_stressReverbBus, 0.3f);
        Logs.WriteInfo($"Audio stress: reverb send on, master limiter enabled={service.MasterLimiter.IsEnabled} ceiling={service.MasterLimiter.CeilingDb} dB");
    }

    private void StopStressReverb(AudioService service)
    {
        if (_stressReverbBus == null)
        {
            return;
        }

        service.Mixer.GetBus(AudioBusNames.Sfx).SetSend(_stressReverbBus, 0f);
        service.Mixer.GetBus(AudioBusNames.Music).SetSend(_stressReverbBus, 0f);
    }

    // One synthetic ADPCM block (shift 0, filter 0) looping on itself: 14 samples of 7000h then 14 of -8000h, a square
    // wave of 28 samples (1575 Hz at pitch 1000h). The SPU uses the engine's default tables (ADPCM
    // filters from psx-spx, formula reverb FIR, no Gaussian table); the block uses filter 0.
    private void StartSpu(AudioService service)
    {
        _spuTried = true;
        if (_spuPort != null)
        {
            return;
        }

        var tables = PsxSpuHardwareTables.CreateDefault();
        if (!service.TryCreatePsxSpu(tables, AudioBusNames.Sfx, out var port))
        {
            return;
        }

        const int soundAddress = 0x1000;
        const ushort modestVolume = 0x0800; // fixed volume register: level 1000h of 7FFFh, about 1/8
        var block = new byte[16];
        block[1] = 7; // loop start, loop end, repeat
        Array.Fill(block, (byte)0x77, 2, 7);
        Array.Fill(block, (byte)0x88, 9, 7);
        ushort[] pitches = [0x1000, 0x1430, 0x1800, 0x0C00];

        var accepted = port.TryUpload(soundAddress, block);
        for (var voice = 0; voice < pitches.Length; voice++)
        {
            accepted &= port.TrySetStartAddress(voice, soundAddress / 8);
            accepted &= port.TrySetPitch(voice, pitches[voice]);
            accepted &= port.TrySetAdsr(voice, 0x1FC0000F);
            accepted &= port.TrySetVolume(voice, false, modestVolume);
            accepted &= port.TrySetVolume(voice, true, modestVolume);
        }

        accepted &= port.TryKeyOn(0xF);
        _spuPort = port;
        Logs.WriteInfo($"Audio demo: SPU started, {pitches.Length} voices, all writes accepted={accepted}");
    }

    private void StopSpu()
    {
        if (_spuPort == null)
        {
            return;
        }

        Logs.WriteInfo($"Audio demo: SPU stopped, refused writes={_spuPort.RefusedWriteCount}");
        _spuPort.TryKeyOff(0xF);
        _spuPort.Dispose();
        _spuPort = null;
    }
}
