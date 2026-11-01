# ADR-0083: Sounds declare an exclusion group

- **Status**: Accepted
- **Date**: 2026-11-01
- **Source**: this chantier: `ai-agent/tasks/sound-exclusion-groups-tasks.md` (decisions D1 and D2, points P1 to P7, approved by the author on 2026-11-01), branch `chantier/sound-exclusion-groups`.

## Context

- Every `play_sound` stacks a voice: `CasaEngine/Framework/Audio` has no instance limit.
- `Priority` only steals a voice when the backend is full (`AudioService.TryFreeVoiceFor`), so it does not stop one sound from playing over another.
- Data alone cannot stop a sound: `.uiscreen` commands have no stop action and `play_sound` drops its handle.
- A limit per asset is not enough: two different assets can belong to the same family of sounds.

## Decision

- **`.sound` gains two additive keys.** `exclusion_group` is a string, empty by default. `exclusion_fade_seconds` is a non-negative number, 0 by default. Neither is written at its default.
- **A group name is compared with `OrdinalIgnoreCase`,** like bus names. Null and blank names become empty, and names are trimmed when they are set.
- **Starting a sound stops the voices of its group, whichever asset started them.** The stop happens after the clip is resolved and before the voice starts, so a missing file cuts nothing.
- **`exclusion_fade_seconds` is read on the incoming sound.** 0 stops at once. A positive value stops through `StopWithFade`, up to `SoundAsset.MaxExclusionFadeSeconds`, 10 s, because a voice fading out no longer answers to its group. The stop is immediate on a full backend, otherwise the new voice would be refused, and for a paused voice, which is silent.
- **A voice already fading out to a stop is left alone, unless the backend is full.** On a full backend every voice of the group stops at once, even if one free slot would do. A voice under an ordinary fade is stopped. A paused voice counts and stops, including one paused by the system, as voice stealing already does. A finished voice that was not recycled yet is released.
- **The group holds across the whole service.** Neither the owner, nor the bus, nor a muted bus, nor the priority protects a voice.
- **The Editor bus stays out of the rule.** A preview stops no game voice, and a game sound stops no preview.

## Consequences

- A family of sounds can stop overlapping through data, without a DLL.
- A voice started by `PlayClip` or `PlayClipStereo` has no group.
- Streams have no group: `play_sound` refuses them, and `MusicPlayer` does not use `PlaySoundCore`.
- A `SoundEmitterComponent` that plays again stops its own voice before the rule, without the group's fade.
- With the MonoGame fallback backend, a fade advances once per frame: a fade shorter than a frame leaves the old voice at full volume, then stops it at the first `Update`.
- A sound on a muted bus still stops an audible voice of its group.
- A group name with a line break works at run time, but the inspector shows it as empty: `MGTextBox` refuses that text.
- An immediate stop can click. The discontinuity can be measured from an offline render, and judging it by ear stays with the author of each project.
