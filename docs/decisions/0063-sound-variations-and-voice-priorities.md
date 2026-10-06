# ADR-0063: Sound variations and voice priorities

- **Status**: Accepted (agent arbitrations P24 to P31 to be confirmed by the author)
- **Date**: 2026-10-06
- **Source**: `ai-agent/tasks/audio-modern-tasks.md`, slice S5a (tasks T8.1-T8.7, arbitrations P24-P31), implementing
  the author's decisions D5, D6, D7 and D14 recorded in ADR-0061. Supersedes in part ADR-0002 ("no random variations
  in V1").

## Context

- A `.sound` asset described one file, a volume, a pitch, a loop flag, a bus and a streaming flag; every play sounded
  the same. When every backend voice was taken, a new sound was refused (`RefusedVoiceCount`), whatever its
  importance.
- The author decided (ADR-0061) that random variations live in the `.sound` as additive fields and must compose with
  the emitter and cutscene overrides; that the refusal stays the default and a voice is stolen only for an explicitly
  higher priority (lowest first, then oldest), never a voice without priority, never a streamed voice; no virtual
  voices; no delay and no sequence.
- `SoundEmitterComponent` and the cutscene `PlaySound` action pass absolute overrides (`asset.Volume * factor`,
  `asset.Pitch + offset`). `SoundPlaybackOverrides.ApplyTo` rebuilt the parameters with the four-argument
  constructor and dropped the loop region and the rate multiplier (open point O17).
- The parent repository (Alundra) plays through `AudioService.PlayClip` and `PlayClipStereo` and implements its own
  fake `IAudioBackend`; it uses neither `.sound` assets nor emitters.

## Decision

- **O17 fixed first**: `SoundPlaybackOverrides.ApplyTo` replaces only the overridden fields and keeps the loop region
  and the rate multiplier.
- **Variations in the `.sound`** (additive keys `variation_audio_file_asset_ids`, `variation_volume_min`,
  `variation_volume_max`, `variation_pitch_min`, `variation_pitch_max`):
  - ranges are relative: a volume factor in [0, 1] (attenuation only, since the final volume is clamped to 1) and a
    pitch offset in octaves in [-1, 1];
  - the file is drawn uniformly among the primary file and the non-empty extra files, without anti-repetition;
  - an inverted range is kept and sorted at draw time, a degenerate range draws nothing.
- **Composition**: the override replaces the asset value as before, the draw applies on top, `AudioVoiceParameters`
  clamps the result; one draw per play; no fallback when the drawn file fails to load; streaming assets have no draw.
  The emitter and the cutscene action do not change.
- **Injectable random source**: `AudioService.VariationRandom` (`System.Random`, `Random.Shared` by default, game
  thread only); calls in a fixed order (file, volume, pitch), none when nothing is drawn, returned values clamped so
  a faulty source never throws. The draw does not allocate.
- **Priority** carried by `SoundAsset.Priority` (key `priority`, 0 = none, clamped to [0, 100]) and by
  `SoundPlaybackOverrides.Priority` (`int?`, `init`; 0 removes the priority). It is not added to
  `AudioVoiceParameters` nor to `IAudioBackend`: `PlayClip`, `PlayClipStereo` and `PlayStream` always play without
  priority.
- **Steal rule** in `AudioService` only: for a priority above 0, when the backend is available and full, first release
  voices that already finished but were not recycled (not a steal); otherwise stop the voice of lowest priority
  strictly below the requested one, oldest on a tie, excluding voices without priority, streamed voices and backend
  stereo voices; paused and fading voices are eligible. `StolenVoiceCount` counts a steal only when the following
  `Play` succeeds; a Play still refused after a steal counts as a refusal and the victim is lost. No log per steal, no
  allocation.
- **Serialization**: each new key is written by the editor only when its own value differs from its default; loading
  is tolerant (a value of an unexpected type keeps the default and logs a warning naming the asset and the key; an
  invalid list entry is skipped the same way). No migration is needed.
- **Editor**: the sound inspector edits the variation files, the two ranges and the priority; its preview plays with
  priority 0, so it never steals a game voice.

Alternatives set aside: a separate "container" asset type (more types, loaders and editors for the same need);
absolute ranges (would change the emitter and the cutscene action to pass factors); a volume factor above 1 (clipped
most of the time); priority on `AudioVoiceParameters` or `IAudioBackend` (would change the parent repository and its
fake backend); virtual voices (D7).

## Consequences

- A `.sound` without the new keys loads, plays and re-saves exactly as before; Alundra is unchanged (no change to
  `IAudioBackend`, `AudioVoiceParameters`, the backends or any public signature), but cannot give priorities to the
  voices it starts through `PlayClip`.
- An external caller of `SoundPlaybackOverrides.ApplyTo` now keeps the loop region and the rate multiplier of its
  input (none in this repository relied on losing them).
- A looping low-priority sound (ambience) can be stolen and does not come back; an emitter sees it through
  `IsPlaying`. Music is never stolen and never steals.
- Each drawn variation file is loaded on its first play, on the game thread, and stays loaded for the life of the clip
  provider; there is no preloading.
- Open for the author: anti-repetition of the file draw (O27); a priority override per emitter or per cutscene action,
  and the stolen-voice counter in the editor's Audio panel (O28); confirmation of P24 to P31.
