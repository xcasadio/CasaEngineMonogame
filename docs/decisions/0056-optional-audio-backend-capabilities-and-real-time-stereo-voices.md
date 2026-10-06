# ADR-0056: Optional audio backend capabilities, real-time stereo voices, loop regions and background music reads

- **Status**: Accepted
- **Date**: 2026-10-05 (decided by the agent in AUTO mode after the author's "fini tout", within the approved
  program envelope; to be confirmed by the author on return)
- **Source**: this chantier: `ai-agent/tasks/audio-modern-tasks.md`, slice S2 (tasks T2.1-T2.5 and T2.7, decisions
  P9-P14, verifier advisories O6-O8). Builds on ADR-0055. Supersedes in part ADR-0039 (software stereo voices): its
  game-thread streaming path stays the path of backends without the new capability.

## Context

- After slice S1, `SoftwareAudioBackend` mixes every voice on the audio thread, but software stereo voices (ADR-0039)
  still go through `StereoVoiceMixer` on the game thread: about 60 ms of queued buffers, resampling by integer
  factors restricted to 8-48 kHz, and left/right gains applied when buffers are built.
- `IAudioBackend` is also implemented by the parent repository (`Alundra.Tests/FakeAudioBackend.cs`): extending it
  forces a parent-repository change.
- The Alundra port already carries per-tone loop points (`LoopStart`/`LoopEnd`) that the engine cannot honour, and
  pitch is clamped to one octave by the engine's own rule (`AudioVoiceParameters`, `SoundAsset`).
- `MusicPlayer` reads its files on the game thread; its public contract is synchronous (playing, with three buffers
  queued, right after `Play`).
- The S1 verifier found three robustness gaps: a dead audio thread left the backend "available" with 100 ms waits
  (O6), a dropped Stop could leave a voice playing after its slot was released (O7), and lost events or dropped
  chunks could leave `GetPendingBufferCount` too high (O8).

## Decision

- **Optional capabilities (P9).** New features reach the backend through public optional interfaces that
  `SoftwareAudioBackend` implements and `AudioService` detects (`backend is I…`). `IAudioBackend` does not change;
  other backends keep today's path.
- **Real-time stereo voices (P10).** `IStereoVoiceBackend`: under the software backend, `PlayClipStereo` plays a
  resident mono voice whose left and right gains are applied at mix time (next block), at any clip rate, with the
  mixer's cubic resampling. `SetVoiceStereoGains` and `GetVoiceStereoGains` work on both paths.
- **Loop regions and a rate multiplier (P11, P12).** `AudioVoiceParameters` gains an optional loop region (frames)
  and a rate multiplier in ]0, 16], added without changing the existing constructor; every existing `With*` method
  keeps them. The software backend honours both on resident and stereo voices; the MonoGame backend loops the whole
  clip and folds the multiplier into its clamped pitch. `SoundAsset` is not changed.
- **Background music reads (P13).** `MusicPlayer` keeps its synchronous open, header and first fill, then a single
  "CasaEngine Audio Streaming" worker thread reads and rewinds the files; the game thread only copies prepared data
  to the voice. `AudioService` uses the worker for real backends and an inline mode for test doubles.
- **Robustness (O6-O8).** The software backend is available only while its output thread lives, and never waits on
  a dead output; a dropped Stop keeps the slot until it is resent; the pending count is read from a per-slot
  (generation, consumed buffers) value published by the mixer instead of events.
- **Deferred.** Explicit sample-accurate volume ramps move to slice S4 with the bus model (P14). Device loss and
  default-device changes (T2.6) are paused until a simulatable device-level design exists.

## Consequences

- Under the software backend, Alundra's stereo voices lose the ~60 ms gain delay and the integer-factor resampling
  of ADR-0039; under the MonoGame backend nothing changes.
- Loop regions and the rate multiplier are available to games now; Alundra still has to pass its tone loop points
  (parent-repository work, open question O15).
- Music file reads no longer happen in `Update` on real backends; `MusicPlayer`'s public behaviour is unchanged.
- Device unplugging still stops the sound until the game restarts (T2.6 paused).
