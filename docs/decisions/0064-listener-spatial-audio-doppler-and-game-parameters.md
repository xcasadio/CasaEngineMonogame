# ADR-0064: Listener, spatial audio, Doppler and game parameters

- **Status**: Accepted (agent arbitrations P32 to P44 to be confirmed by the author)
- **Date**: 2026-10-06
- **Source**: `ai-agent/tasks/audio-modern-tasks.md`, slice S5b (tasks T9.1-T9.10, arbitrations P32-P44, open points
  O29-O33), implementing the author's decisions D8 to D13 recorded in ADR-0061. Supersedes in part ADR-0001
  ("Spatialisation is 2D only: no listener, no Doppler").

## Context

- The engine played every voice with a volume and a pan only. `IAudioBackend` is 2D and is also implemented by a
  fake backend of the parent repository; the software backend owns the mix on an audio thread fed by lock-free rings,
  where `SetParameters` may wait up to 100 ms on a full ring and where four same-frame ordering bugs (F1, N1, R1,
  V1/V2) were found in slice S4 around volume ramps.
- The author decided (ADR-0061): a listener component pushing its pose into `AudioService` (D8); one spatial mode per
  asset, none by default (D9); the distance models of the OpenAL 1.1 specification, cited, no code reused (D10); Doppler
  off by default (D11); game parameters drive volume and pitch first (D12); `SoundEmitterComponent` becomes a scene
  component, loading the old form tolerantly (D13).
- `SceneComponent.Position` and `Orientation` add positions and quaternions without composing parent rotations;
  `WorldMatrixNoScale` composes them. The OpenAL 1.1 specification
  (https://www.openal.org/documentation/openal-1.1-specification.pdf) defines distance models (§3.4) and Doppler
  (§3.5.2) but no panning law and no distance unit.

## Decision

- **Per-voice modulation channel** (P32): an optional public capability `IAudioVoiceModulationBackend`, implemented by
  `SoftwareAudioBackend` only, publishes per voice slot three generation-tagged last values (gain in [0, 1], spatial
  pan or NaN, speed ratio in [1/16, 16]) read by the mixer before each block, never queued, never waiting; starting
  values travel with the start (`SetNextVoiceModulation`) or, for a streaming voice, are read when it starts. The
  factor multiplies the channel gains outside the volume, its ramps and the bus gains; with neutral values the output
  is bit for bit unchanged. Without the capability, `AudioService` folds the gain into the volume and the pan and pitch
  into the parameters it sends (total pitch clamped to one octave).
- **Spatial maths** (P33, P36, P43): OpenAL 1.1 distance formulas and Doppler formula applied literally, with declared
  engine additions (gain clamped to [0, 1]; a formula that cannot be evaluated, including a non-positive inverse
  denominator, does not attenuate; Doppler ratio clamped to [0.25, 4]; the sum of pitch bindings clamped to one
  octave). Spatial pan = dot product of the source direction with the listener's right vector, through the mixer's
  existing pan law. 2D mode works on the X/Y plane. Positions are `System.Numerics.Vector3` in world units.
- **Asset fields** (P34, P42): `spatial_mode`, `distance_model` (default `InverseDistanceClamped`), `reference_distance`
  (1), `max_distance` (no limit), `rolloff_factor` (1), `doppler_factor` (0 = off), `parameter_bindings` (at most 8),
  written only when they differ from their default, read tolerantly (unknown names keep the default with a warning).
- **Listener and voices** (P35, P38): the last registered listener wins; no listener means no spatialisation, read every
  frame, so a voice started before the listener spatialises once one registers. Only `PlaySoundAt` and
  `SetVoicePosition` spatialise; a spatial asset played without a position is not spatial. Only changes beyond small
  thresholds are sent (P44).
- **Doppler** (P37): per-asset factor, `AudioService.SpeedOfSound` in world units per second (default 343.3), velocities
  derived from poses pushed frame to frame; no teleport detection.
- **Game parameters** (P39): a registry of 64 named parameters in `AudioService`; linear bindings to a volume factor or
  a pitch offset; bound volume and pitch are part of a voice's start values (sounds and music tracks).
- **Components** (P40, P41): `AudioListenerComponent` (forces its entity to tick); `SoundEmitterComponent` derives from
  `SceneComponent` — the only public API break of the slice, required by D13 — with an additive load valve
  (`AllowsMissingSceneData`) that loads the old form with an identity transform. Poses come from
  `WorldMatrixNoScale`; a component without a parent keeps its own matrix, like the editor gizmo.

Alternatives set aside: folding the distance gain into the voice volume under the software backend (broken by
volume ramps); a queued `SetVoiceGain` command (waits on a full ring); a global distance model; absolute spatial
overrides on the emitter (not requested by the author, O29); attaching entity-level components to the entity root for
audio only (the sound and the gizmo would diverge, O30).

## Consequences

- `.sound` files without the new keys load and save unchanged; Alundra is unaffected (no change to `IAudioBackend`,
  `AudioVoiceParameters` or any existing `AudioService` signature).
- Under the MonoGame backend the pitch of a spatial or bound voice stays within one octave and its pan follows
  MonoGame's OpenAL rotation; under the software backend the speed ratio may exceed one octave.
- Music tracks get parameter bindings but are never spatialised; cutscene sounds are not spatialised; there is no
  cone, listener gain, per-source min/max gain or HRTF.
- An emitter only follows its entity when the entity ticks; in a child entity, the parent entity's root is applied twice
  by the engine's world matrix (existing behaviour, O33).
- Reverting the slice makes entities saved since with an emitter as root or child, or with an `AudioListenerComponent`,
  unreadable by the older engine; the old emitter form stays readable.
- Open for the author: O29 (emitter overrides of spatial mode and Doppler), O30 (pose of entity-level components, an
  emitter becoming an entity root in the editor), O31 (editing bindings in the inspector), O32 (teleport detection,
  project setting for the speed of sound), O33 (double root application); confirmation of P32 to P44.
