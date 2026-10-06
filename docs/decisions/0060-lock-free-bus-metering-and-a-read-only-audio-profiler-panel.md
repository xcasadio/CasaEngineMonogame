# ADR-0060: Lock-free bus metering and a read-only audio profiler panel

- **Status**: Accepted
- **Date**: 2026-10-06 (decided by the agent in AUTO mode after the author's "fini tout", within the approved
  program envelope; to be confirmed by the author on return)
- **Source**: this chantier: `ai-agent/tasks/audio-modern-tasks.md`, slice S6a (tasks T6.1-T6.3, decisions P22 and
  P23, open question O24). Builds on ADR-0055 (software mixer) and ADR-0059 (bus graph on the audio thread).

## Context

- Since slice S4 the software backend mixes a real bus graph on its audio thread, but nothing reports levels: the
  editor has no meter, and clipping cannot be seen after the mix because the Master limiter and the final hard clip
  remove it.
- The audio thread never takes a lock nor allocates (ADR-0055); several editor views may want the same levels.
- An editable mixer panel, a mixer asset, solo and waveforms raise product questions the author has not answered
  (O24): who owns the mix settings, solo semantics, asset scope.
- Editor code lives in `CasaEngine.Editor` and only sees the engine's public API (AGENTS.md §9.9).

## Decision

- **Metering capability (P22).** A public optional capability, `IAudioMeteringBackend`, implemented only by
  `SoftwareAudioBackend` and reached through `AudioService` (`IsMeteringAvailable`, `TryGetMeterBusIndex`,
  `TryReadLevels`). `IAudioBackend` does not change.
- **What is measured.** Per block: each bus's peak (per channel) and sum of squares after its insert effects and own
  gain (the signal it hands to its parent), the output after the limiter and the clip, and the number of samples beyond
  full scale right after the bus mix, before the limiter.
- **How it is published.** A preallocated history of 64 blocks, one slot per block under a sequence counter (odd while
  the audio thread writes it), the block counter published last. Each reader owns a cursor and aggregates every block
  newer than its cursor; a slot overwritten during the copy is reported as missed, never mixed in. Readers do not share
  state, so a panel, a profiler and a game overlay can all read without stealing each other's peaks. Nothing waits or
  allocates on either side.
- **Read-only panel (P23).** An "Audio" tool panel in the editor (Windows > Audio) shows backend statistics and one
  meter per bus. It changes nothing in the mix, so it does not decide who owns mix settings (O24). Meters are read on
  every editor update; texts are rebuilt at most four times per second and only when a value changes.

## Consequences

- Levels and overs are visible in the editor while editing and in play-in-editor, under the software backend only;
  the MonoGame backend shows statistics without meters.
- The history size (64 blocks) keeps every peak for a reader that polls at least every 80 ms as long as an audio block
  lasts 1.25 ms or more; a slower reader is told how many blocks it missed.
- An editable mixer, a mixer asset, solo and waveforms remain for slice S6b, after the author's answers to O24.
