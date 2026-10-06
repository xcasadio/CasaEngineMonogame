# ADR-0058: A software PlayStation SPU hosted by the software audio backend

- **Status**: Accepted (hardware tables policy superseded in part by ADR-0061: the engine is to ship the ADPCM coefficients and a formula-computed reverb FIR)
- **Date**: 2026-10-06 (decided by the agent in AUTO mode after the author's "fini tout", within the approved
  program envelope; to be confirmed by the author on return)
- **Source**: this chantier: `ai-agent/tasks/audio-modern-tasks.md`, slice X1 (tasks T4.1-T4.5, decisions P9 and
  P17, open questions O12 and O19). Builds on ADR-0055 (engine-owned software mixer) and ADR-0056 (optional backend
  capabilities).

## Context

- The author wants a PlayStation-specific audio path for PSX games (BGM and SFX), on the same platforms as MonoGame.
  The parent repository plays Alundra's sounds through engine voices and pre-rendered music; nothing in the engine
  understands SPU registers, ADPCM sound banks, envelopes or the reverb unit.
- psx-spx (<https://psx-spx.consoledev.net/ps1/spu/soundprocessingunitspu/>) is the only public description of the
  SPU. It declares no licence; the analyser that ships with the parent repository contains GPL-derived code (P.E.Op.S
  ADSR tables), and other emulators are GPL or MPL.
- Three hardware constant tables (ADPCM filter pairs, reverb FIR, Gaussian interpolation) are measured hardware data
  whose reuse the author has to decide (O12).
- The software mixer renders on a dedicated audio thread fed only through lock-free single-producer rings
  (ADR-0055); `IAudioBackend` is also implemented by a parent-repository test fake (ADR-0056).

## Decision

- **A pure SPU core** (`PsxSpu`, `CasaEngine.Framework.Audio.Psx`): register-level, 44,100 Hz, 16-bit, written from
  psx-spx formulas only, no third-party code. Every point psx-spx leaves open is chosen by the most literal reading,
  marked `AMBIGUOUS (psx-spx)` in the code and listed for the author (O19).
- **Hardware tables injected by the caller (P17).** `PsxSpuHardwareTables` validates and copies them; the engine ships
  none. Without FIR coefficients the reverb is bypassed; without a Gaussian table an engine cubic interpolation is
  used, behind the `IPsxSpuInterpolator` extension point.
- **Hosting through an optional capability (P9).** `IPsxSpuHost`, implemented by `SoftwareAudioBackend` only, and
  `AudioService.TryCreatePsxSpu` create one SPU per backend and return a game-thread `PsxSpuPort`. The SPU is a source
  pulled by the mixer once per block, resampled to the output rate and added before the final clip; its gain is
  multiplied by the effective gain of a bus, like a voice, until the bus graph exists.
- **Bounded, ordered, allocation-free transport.** Register writes go through a 16,384-entry ring, uploads through a
  1 MB staging buffer applied 256 KB per block; a numbered marker per upload keeps every later register write behind
  it. A full ring or buffer refuses the call (false and a counter) without waiting. ENDX and ENVX come back through a
  per-block snapshot under a sequence counter.

## Consequences

- A PSX game can drive a sound driver against the SPU (key on/off, ADSR, pitch, reverb) on the software backend; the
  MonoGame backend has no SPU and says so once.
- Register timing is quantised to the mixer block (10 ms by default); a sequencer that needs finer timing would have to
  run on the audio thread (slice X3, paused on O4 and O16).
- Until the author answers O12, a game has to supply the hardware tables itself; the engine tests and the demo use
  synthetic tables.
- Accuracy rests on the O19 readings: the tests recompute the same formulas, so a misreading of psx-spx would pass
  them. The reverb output may be about half as loud as on the console (FIR gain not compensated).
