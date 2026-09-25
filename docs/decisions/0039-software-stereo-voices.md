# ADR-0039: Software stereo voices with exact left/right gains

- **Status**: Accepted
- **Date**: 2026-09-25
- **Source**: this chantier: `ai-agent/tasks/audio-stereo-voices-mute-tasks.md`, consumer plan `docs/plan-audio-mix-exact-muet.md` of the parent repository `alundra-casaengine-project-converter` (decision D3 and proposals P3, P4, P5, accepted by the author on 2026-09-25).

## Context

- The Alundra port must give every sound effect voice the left and right volumes the original writes into the PlayStation SPU, per tone, when the voice starts and again while it plays (script opcodes `0xAB`/`0xBF`). Today it projects them onto one volume and one pan per voice (parent plan, "État vérifié").
- `AudioService.SetVoicePan` only changes the pan of a mono voice (`CasaEngine/Framework/Audio/AudioService.cs:274`). In MonoGame 3.8.5.1 DesktopGL (decompiled with `ilspycmd`), `SoundEffectInstance.PlatformSetPan` rotates the OpenAL source position of a mono source by `pan × π/3`. The actual left and right gains therefore come from OpenAL Soft's panning law: no (volume, pan) pair yields chosen left/right gains.
- A resident clip is a MonoGame `SoundEffect` (`Backends/MonoGameAudioClip.cs`), whose samples are not accessible; its `SampleRate` and `ChannelCount` report 0.
- The engine already streams: `AudioService.PlayStream` / `SubmitStreamBuffer` / `StartVoice` / `GetPendingBufferCount` (`AudioService.cs:148-205`), fed by `MusicPlayer` (`Streaming/MusicPlayer.cs`). A streamed stereo source keeps its left channel on the left and its right channel on the right, like the music does today.
- MonoGame refuses a `DynamicSoundEffectInstance` outside 8 000–48 000 Hz (decompiled: `ArgumentOutOfRangeException("sampleRate")`). 73 of the 996 Alundra sound effect tones are outside that range (3 370 to 172 610 Hz).

## Decision

- A **software stereo voice** plays a mono clip on a stereo streaming voice that the engine feeds itself. Every output frame is `left = s × leftGain`, `right = s × rightGain` (16 bit, rounded, saturated). The gains can change while the voice plays and apply from the next buffer submitted.
- Additive public API on `AudioService`: `PlayClipStereo(clip, busName, parameters, leftGain, rightGain, owner)`, `SetVoiceStereoGains(voice, leftGain, rightGain)` and `GetVoiceStereoGains(voice, …)`. The voice is an ordinary voice for everything else: bus, owner, `Stop`, `StopVoicesOwnedBy`, `StopAll`, `StopAllExceptBus`, `Pause`, `Resume`. `parameters.Volume` × bus gain is still applied through the backend voice volume, identically on both channels. `parameters.Pan` and `parameters.Pitch` are ignored. `parameters.IsLooped` loops the whole clip.
- Clips expose their samples through a new sample-access interface. `MonoGameAudioClip` keeps the decoded 16-bit PCM of the mono PCM WAV files loaded by `SoundEffectLoader`, next to its `SoundEffect`. A clip without samples cannot be played in stereo: `AudioVoiceHandle.None` and a throttled log.
- Sample rate: the clip's own rate when it is within 8 000–48 000 Hz. Otherwise it is resampled by an integer factor: the smallest `k` with `rate × k ≥ 8 000`, by linear interpolation; above 48 000 Hz, the average of `k` samples.
- Buffers of about 20 ms, a queue target of 3, and **at most 3 buffers submitted per voice per `Update`**. The bound also protects against a backend that never reports queued buffers.
- `IAudioBackend` does not change: the path is built on the existing streaming contract.

## Consequences

- Each channel gets exactly the gain it was given, with the same fidelity as the streamed music (a stereo source rendered by OpenAL with its channels at ±30°).
- A live gain change is heard after the buffers already queued, up to about 60 ms. The original applies its register writes immediately: a declared deviation.
- The 73 out-of-range tones are resampled by the engine instead of OpenAL: a declared approximation.
- Memory: the PCM of every loaded mono clip is kept twice (in the `SoundEffect` and in the engine), 26 MB at most for the 996 Alundra sound effect files.
- `Update` feeds every stereo voice on the game thread, without allocation (preallocated scratch buffers).
- Not done: loop points (the whole clip loops), pitch on stereo voices, ADSR envelopes. The existing `PlayClip`/`PlaySound` path does not change.
