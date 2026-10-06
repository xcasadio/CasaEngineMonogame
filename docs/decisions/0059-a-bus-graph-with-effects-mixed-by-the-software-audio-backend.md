# ADR-0059: A bus graph with effects, sends, ramps and snapshots mixed by the software audio backend

- **Status**: Accepted
- **Date**: 2026-10-06 (decided by the agent in AUTO mode after the author's "fini tout", within the approved
  program envelope; to be confirmed by the author on return)
- **Source**: this chantier: `ai-agent/tasks/audio-modern-tasks.md`, slice S4 (tasks T5.1-T5.6, decisions P14 and
  P18-P21, open questions O20 and O22). Builds on ADR-0055 (engine-owned software mixer) and ADR-0056 (optional backend
  capabilities); ADR-0001's bus model (named buses, effective gain folded into each voice) stays the model of backends
  without the new capability.

## Context

- Until slice S4, the bus gain was multiplied into the volume of each voice by `AudioService` whenever a bus changed,
  and fades were volume steps sent from the game thread once per frame. No bus existed on the audio thread, so there
  was no place for effects, sends, ducking or a protection of the output.
- `AudioBus` is a game-thread class; the audio thread only reads preallocated state fed through lock-free rings, and a
  full command ring drops non-essential commands (ADR-0055).
- The parent repository implements `IAudioBackend` in a test fake and its tests assume the folded gain; Alundra drives
  its music fades by writing the Master volume each tick.
- Effect algorithms must come from cited sources with permissive terms: no GPL, LGPL or MPL code.

## Decision

- **Capability, not interface change (P18).** `IAudioBusBackend`, implemented only by `SoftwareAudioBackend` and
  detected by `AudioService`, carries buses, ramps, effects, sends and the Master limiter. Without it, `AudioService`
  keeps the folded gain and per-frame fades byte for byte; effects, sends and ducking are absent there with one
  throttled log line.
- **Bus graph on the audio thread (T5.1).** 32 preallocated buses (index 0 is Master), one buffer per bus, a maximum
  block size fixed at construction; every voice, streaming voice, stereo voice and the PSX SPU source carries its bus.
  Each bus's own gain is published as a last value (never lost) and ramped across a block. `AudioService` routes each
  voice as part of its start order and stops folding the bus gain.
- **Explicit ramps (P14, P21, T5.2).** Voice and bus fades are "ramp to v over n frames" commands interpolated per
  sample, starting at the next block. The public fade contract does not change: the game thread keeps the chronology
  of each ramp and answers `GetVoiceVolume`, `IsFading`, the end of `StopWithFade` and crossfades from it, at most one
  block away from the audio.
- **Insert effects (P20, T5.3).** Up to 4 per bus, processed in the bus buffer before its gain: biquad filters from the
  W3C Audio EQ Cookbook and a feed-forward compressor from Giannoulis, Massberg and Reiss (JAES, 2012). Parameters are
  immutable snapshots published as last values; DSP state is preallocated in the mixer, with a denormal guard.
- **Sends, returns, reverb, limiter (P20, T5.4).** Post-fader sends (up to 4 per bus) to any bus, cycles refused on the
  game thread; a bus is mixed after every bus that feeds it. Reverb is Freeverb as described on J. O. Smith's CCRMA
  pages, delays scaled to the output rate. A Master limiter (the compressor's detector at an infinite ratio plus a
  per-sample ceiling clamp) runs before the hard clip, on by default at −1 dBFS.
- **Ducking and snapshots (T5.5).** `DuckingEffect` is an insert effect on the target bus: it reads the level of a
  source bus in the same block (the source is ordered before the target, cycles are refused like send cycles) and
  applies a fixed reduction above a threshold, smoothed by the compressor's detector, with a 50 ms peak follower added
  by the engine. `AudioMixerSnapshot` captures each bus's own volume and the parameters of its insert effects;
  `AudioService.ApplySnapshot` ramps the volumes through `FadeBus` and restores the parameters at once. The Editor bus,
  bus mutes (hence the project's Master mute) and the Master limiter are never captured or applied.
- **Serialised mixer configuration deferred (P19).** S4 delivers the runtime API only; a mixer asset and its editing
  belong to the tools slice S6.

## Consequences

- Under the software backend, games get real buses with effects, sends, ducking, snapshots and sample-accurate fades;
  the MonoGame backend and test fakes behave as before.
- The default Master limiter changes how loud mixes sound on the software backend: everything above −1 dBFS is reduced
  instead of hard-clipped at 0 dBFS (open question O22). Tests that read exact full-scale levels disable it.
- `IAudioBusBackend` grew during the slice; it is new on this branch and has not been released, so its shape is fixed
  only when the branch reaches `main`.
- Some choices are the engine's, not the sources' (reverb input gain, limiter clamp, ducking peak follower, sends and
  ducking following the bus's own gain only, snapshot effect parameters applied without a ramp): listed in O22 for the
  author.
