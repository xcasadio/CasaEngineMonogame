# ADR-0061: Author's decisions for the audio game layer, the mixer asset and the PSX module

- **Status**: Accepted
- **Date**: 2026-10-06 (decided by the author, answering the open questions of the audio plan)
- **Source**: `ai-agent/tasks/audio-modern-tasks.md`, decisions D5 to D30, answering open questions O4, O11, O12, O13,
  O16, O19, O23, O24, O25 and task T2.6. Supersedes in part ADR-0058 (hardware tables: D25). Each slice that implements
  these decisions records its own detailed ADR.

## Context

- After slices S1-S4, X1 and S6a, the remaining slices of the audio program (S5 game layer, S6b mixer asset and editable
  mixer, X2-X5 PSX data, sequencer and XA) and several open points waited for product, licensing or format decisions
  that only the author can take.
- Alundra plays its music and sound effects from pre-rendered WAV files through `AudioService.PlayClip`; it uses neither
  `.sound` assets nor `SoundEmitterComponent`.

## Decision

**Game layer (S5).**
- Random variations are written in the `.sound` asset (file list, volume and pitch ranges), as additive fields; emitter
  and cutscene overrides must compose with the draw instead of overwriting it. No delay and no sequence at first.
- When every voice is taken, a new sound is still refused by default. A voice is stolen only for a sound with an
  explicitly higher priority (lowest priority first, then oldest); a voice without priority is never stolen; streamed
  voices are always protected. No virtual voices for now.
- The listening point comes from an `AudioListenerComponent` that pushes its pose into `AudioService`.
- Each sound asset has a spatial mode: none (default), 2D or 3D. Distance attenuation follows the distance models of
  the OpenAL 1.1 specification (cited, no code reused). Doppler is off by default and enabled on demand.
- Game parameters drive volume and pitch first; a per-voice filter is a later step.
- `SoundEmitterComponent` becomes a scene component. The author states that no project outside this repository holds a
  saved emitter; loading stays tolerant of the old form anyway.

**Mixer asset and editable mixer (S6b).**
- One mixer asset per project, named by an optional project setting (empty means the engine's default mixer), with the
  extension `.audioMixer`. The Master bus stays out of the asset.
- The mixer panel edits the asset (single source of truth) and applies it to the live mixer, always from the asset to
  the mixer, never the reverse.
- Solo is a panel feature built on the existing mutes; it keeps return buses and the Editor bus audible.
- Waveforms are the output level over time (from the existing metering) and a drawing of a sound file in the sound
  inspector; no oscilloscope.
- The sound inspector lists the buses of the mixer asset.
- The per-change allocation of `MGSlider` is fixed in a separate session, in the MGUI submodule, outside this program.

**PSX module.**
- Music stays pre-rendered WAV: no SEQ/VAB sequencer for now, and no decompilation of Sony's libsnd is reused. If a
  sequencer is written later, its tick rate is configurable per game and it runs on the audio thread.
- XA tracks become audible by offline decoding to files at extraction time (parent repository), played by the existing
  music player.
- The engine ships the five ADPCM filter coefficient pairs (from psx-spx; the author accepts that psx-spx declares no
  licence) and computes an approximate reverb FIR by formula (no hardware value). The Gaussian interpolation table is
  still supplied by the caller. This replaces, for these two tables, the "no table shipped" rule of ADR-0058.
- The readings where psx-spx is ambiguous are judged by listening against a recording of the console once real tables
  are available.

**Other points.**
- No automatic recovery after an audio device is unplugged (the sound stays lost until the game restarts, documented).
- Ogg Vorbis stays resident only; no MP3, FLAC or Opus support.
- The small gain step when a very short bus fade is retargeted between two audio blocks stays as is and is documented.

## Consequences

- S5 and S6b can be detailed and executed; the PSX module gets usable ADPCM decoding and an approximate reverb without
  the caller supplying hardware tables, at the cost of a reverb that does not match the console exactly.
- The sequencer (X3) and the switch of Alundra's music to the SPU are not pursued; how far the PSX data and the sound
  effects go through the SPU (X2/X4) still awaits the author's clarification.
- The plan's open point O22 (choices made in S4, including the Master limiter on by default for Alundra) is not
  covered by this record.
