# ADR-0055: Engine-owned software audio mixer with thin native outputs

- **Status**: Accepted
- **Date**: 2026-10-05 (decisions D1-D4 taken with the author on 2026-10-05; plan and proposals P1-P8 approved on 2026-10-05)
- **Source**: this chantier: `ai-agent/tasks/audio-modern-tasks.md` (decisions D1-D4, proposals P1-P8, program envelope
  S1-S6 and X1-X5), branch `chantier/audio-modern`. Supersedes in part ADR-0001 (the MonoGame/OpenAL implementation
  of `IAudioBackend` as the only backend); ADR-0001's buses, streaming API and `IAudioBackend` boundary, and ADR-0039's
  software stereo voices, are kept.

## Context

- The author asked for an audio part that is "at least modern", with a dedicated part for PSX games (sound effects and
  music), running on the same platforms as MonoGame; retargeting the engine itself (`net9.0-windows`, x64 today) is out
  of this chantier.
- The current backend, `MonoGameAudioBackend`, plays one MonoGame voice per engine voice. Streaming voices are fed from
  the game thread: the software stereo voices of ADR-0039 queue about 60 ms and receive at most 3 buffers per `Update`
  (`Streaming/StereoVoiceMixer.cs:32-42`), and MonoGame allocates an `OALSoundBuffer` per submitted buffer (MonoGame
  3.8.5.1 decompiled with `ilspycmd`, `DynamicSoundEffectInstance.cs:350-363`). Pitch is limited to one octave each way
  (`AudioVoiceParameters.cs:18-19`). MonoGame's OpenAL effects are internal and unreachable.
- The clip type is tied to the backend: `SoundEffectLoader` always builds a `MonoGameAudioClip`, and
  `MonoGameAudioBackend.Play` refuses any other clip (`MonoGameAudioBackend.cs:65-70`).
- Reference engines mix in their own engine code and keep only a thin platform output: Unreal's Audio Mixer is
  "a multi-platform audio renderer that lives in its own module"
  (https://dev.epicgames.com/documentation/en-us/unreal-engine/audio-mixer-overview-in-unreal-engine); Godot mixes its
  buses and their effects in its own audio engine (https://docs.godotengine.org/en/stable/tutorials/audio/audio_buses.html).
- Evaluated alternatives:
  - SoundFlow 1.4.1 has no per-voice sample-rate conversion in its player, runs managed mixing with a lock on the
    native callback, has no automated tests and a single maintainer on hiatus until about February 2027.
  - OpenAL Soft voices as the mixer have no submix buses and no insert effects (EFX effects only as auxiliary sends,
    an on/off compressor, a fixed 4-band EQ, a device-wide limiter).
  - FAudio follows XAudio2 but its EQ and limiter effects are empty stubs, it ships no binaries, Android is
    unofficial, and upstream refuses AI-written contributions.
  - XAudio2 is Windows-only.
  - FMOD and Wwise are proprietary, licensed per developer.
- MonoGame already ships OpenAL Soft 1.24.3 for all its public platforms except WindowsDX (`MonoGame.Library.OpenAL`
  1.24.3.4, twelve runtime identifiers). The extensions this decision relies on (`ALC_EXT_thread_local_context`,
  `AL_EXT_FLOAT32`, `AL_SOFT_direct_channels`) are present in every binary. MonoGame opens OpenAL only when one of
  its audio types is first used, and then sets the process-global current context
  (decompiled `OpenALSoundController.cs:107-120`).

## Decision

- **Architecture (P1).** The engine mixes all of its audio itself, in C# (32-bit float), on a dedicated audio thread
  that renders a few tens of milliseconds ahead of the device. Each platform gets a thin native output that only
  plays the mixed stream. Buses, effects, 3D, the game layer and the PSX module are built on this mixer, slice by
  slice. SoundFlow and proprietary middleware are not used.
- **First output (P2).** OpenAL Soft as shipped by MonoGame, through a hand-written minimal binding to the `openal`
  library. The engine opens its own device and context, made current only on its audio thread
  (`ALC_EXT_thread_local_context`), and never touches MonoGame's global context. The output is stereo 32-bit float
  (`AL_EXT_FLOAT32`) played without spatialisation (`AL_SOFT_direct_channels`) at the device's native rate. A missing
  extension at run time makes the output unavailable: logged once, the game runs without sound.
- **Transition (P3).** `IAudioBackend` and `AudioService`'s public API do not change in the first slice. A new
  `SoftwareAudioBackend` implements `IAudioBackend`. The backend is chosen at startup, in this order: the environment
  variable `CASAENGINE_AUDIO_BACKEND` (`Software` or `MonoGame`, valid for every host including the editor), then the
  project setting `AudioBackend` when a project is loaded and the setting is present, then a single default
  (`MonoGame` until the author's listening test, then `Software`). The choice and its source are logged once.
  `MonoGameAudioBackend` stays available as a fallback; removing it is a later public API decision.
- **Neutral clip (P4).** A new `PcmAudioClip` (interleaved 16-bit PCM, mono or stereo, real rate and duration) is
  produced by a new WAV loader that replaces `SoundEffectLoader` in the registry. The loader reads integer PCM
  8/16/24/32-bit and 32-bit float, including `WAVE_FORMAT_EXTENSIBLE`; ADPCM comes in a later slice (no file in the
  repository uses it). `SoundEffectLoader` stays, obsolete and unregistered. The MonoGame backend accepts
  `PcmAudioClip` by building its `SoundEffect` once from an in-memory 16-bit WAV.
- **Semantics (P5).** Same as today for volume (linear, clamped to [0, 1]), pitch (octaves, clamped to ±1, applied as
  2^pitch) and whole-clip looping. Deliberate audible change: a mono clip is panned with a constant-power law (-3 dB at
  the centre) instead of OpenAL's source rotation; a stereo clip's pan is a balance; stereo voices are rendered exactly
  left to left and right to right.
- **Mixing (P6).** 4-point cubic Hermite resampling; by default 4 buffers of 10 ms ahead, configurable; hard clipping
  at the output (a limiter comes with the buses and effects slice). The render path and the audio thread loop do not
  allocate, do not take blocking locks, and use no LINQ or closures. Commands go from the game thread to the audio thread
  through a lock-free queue; voice ends come back through a second one.
- **Licence (P8).** The LGPL notice of OpenAL Soft, with a link to its sources, is added under `Licences/`.

## Consequences

- The same audio features will exist on every platform that has an output. A new platform (WindowsDX, MonoGame's
  Native platform, consoles) only needs a thin output, for example XAudio2 or FAudio.
- The whole mix can be rendered offline and tested sample by sample without an audio device.
- All signal processing (resampling, filters, reverb, limiter, panning, the PSX SPU) is engine code to write and
  maintain.
- Managed code produces the audio. A garbage collection pause longer than the lead empties the output queue: the
  first slice measures it with a GC stress run (0 underrun over 60 s is required), and a failure stops the program
  for a decision by the author. A lead of 40 ms plus the device's own buffering sets the latency floor.
- Until the default switches, existing projects keep the MonoGame backend. After the switch, the editor and every
  project without the setting use the software mixer; `CASAENGINE_AUDIO_BACKEND=MonoGame` or the project setting
  reverts to MonoGame. The default switched to `Software` on 2026-10-05, after the author's listening test of the
  demo, the editor and Alundra (plan task T1.9).
- The editor's runtime is created without a project, so it follows the environment variable or the default, never
  the setting of a project opened later.
- Later slices may change public APIs additively and touch the parent repository under their own approval; a slice
  that extends `IAudioBackend` also updates the parent repository's `Alundra.Tests/FakeAudioBackend.cs`.
