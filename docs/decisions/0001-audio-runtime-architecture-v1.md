# ADR-0001: Audio runtime architecture V1 (buses, streaming, backend)

- **Status**: Accepted
- **Date**: 2026-08-26
- **Source**: `ai-agent/audits/analysis-audio-system.md:185-204` (section 3, "Décisions prises", arbitrated with the author on 2026-08-26); `docs/engine/audio-system.md:3-4` (the engine doc points at that section as the frozen decisions) and `:185-200` (known limits). Backfilled on 2026-09-06.

## Context

The audio analysis of 2026-08-26 fixed the scope of the first audio system with the author. At that time the engine carried legacy audio files without any caller (`Sound.cs`, `IAudioEmitter.cs`, `AudioComponent.cs`), and the music asset provided for the demo was a WAV file that MonoGame's `MediaPlayer`/`Song` could not play, since it only reads Ogg (source: `analysis-audio-system.md:193`). MonoGame DesktopGL cannot decode MP3 either (source: `audio-system.md:187-189`).

## Decision

- "Channels" are **named mixing buses** (Master / Music / SFX / Voice / UI) with volume, mute and hierarchy; no retro-style pool of numbered voices (source: `analysis-audio-system.md:191`, D1).
- Music streaming is **written in the engine from V1**: `DynamicSoundEffectInstance` fed by an in-engine RIFF PCM reader. `MediaPlayer`/`Song` is **abandoned**; the earlier choice of `MediaPlayer` + `Song` (D2) was revised the same day (source: `analysis-audio-system.md:192-193`, D2 and D2-bis).
- Spatialisation is **2D only** (volume and pan): no `Apply3D`, no listener, no Doppler in V1 (source: `analysis-audio-system.md:196`, D5).
- The audio device and the buses are **global** (a `GameComponent` on `CasaEngineGame`); **voices are bound to the world** and cut by `World.Clear()` (source: `analysis-audio-system.md:198`, D7).
- A backend abstraction **`IAudioBackend`** (MonoGame/OpenAL implementation plus a test fake) makes buses, voices, fades and routing testable without a device; it is justified as a real backend boundary in the engine rules (source: `analysis-audio-system.md:201`, D10).
- V1 consumers are the **demo** in `CasaEngine.Demos`, the **cutscene actions** `PlaySound` / `PlayMusic` / `StopMusic` / `FadeMusic`, and an entity **`SoundEmitterComponent`**; no audio event on 2D animations in V1 (source: `analysis-audio-system.md:202`, D11).

## Consequences

- Crossfade, several simultaneous streams and routing into the Music bus are possible from V1; an Ogg decoder (NVorbis, already a transitive dependency) can be plugged later on the same API without breaking it (source: `analysis-audio-system.md:193`).
- Known limits recorded in the engine doc (source: `audio-system.md:185-200`): no MP3; streaming reads 16-bit PCM only; no 3D audio; disk reads happen on the game thread inside `Update`; 64 voices by default, a refused voice logs a throttled message and never throws; bus volumes are not persisted.
- Implementation observed on 2026-09-06: `CasaEngine/Framework/Audio/Mixing/AudioBusNames.cs`, `AudioBus.cs`, `AudioMixer.cs`; `CasaEngine/Framework/Audio/Streaming/WavStreamReader.cs`, `MusicPlayer.cs`; `CasaEngine/Framework/Audio/IAudioBackend.cs`, `Backends/MonoGameAudioBackend.cs`, `Backends/NullAudioBackend.cs`; `CasaEngine.Tests/Audio/FakeAudioBackend.cs`; `CasaEngine/Framework/Application/Components/AudioSystemComponent.cs`; `CasaEngine/Framework/Scene/Entities/Components/SoundEmitterComponent.cs`; the four cutscene action data classes; `AudioDemo.cs`. No `MediaPlayer`/`Song` usage under `CasaEngine/Framework/Audio/`.
