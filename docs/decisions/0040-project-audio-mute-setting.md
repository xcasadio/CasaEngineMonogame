# ADR-0040: The audio mute is a project setting

- **Status**: Accepted
- **Date**: 2026-09-25
- **Source**: this chantier: `ai-agent/tasks/audio-stereo-voices-mute-tasks.md`, consumer plan `docs/plan-audio-mix-exact-muet.md` of the parent repository `alundra-casaengine-project-converter` (decision D4 and proposal P6; the author asked on 2026-09-25 for a persisted setting read at startup, then for that setting to live in the project rather than in the user's local files).

## Context

- Muting already exists at the bus level: `AudioBus.IsMuted` sets the effective gain of the bus and of every bus below it to 0 (`CasaEngine/Framework/Audio/Mixing/AudioBus.cs:56`, `AudioMixer.cs:116`). Nothing exposes it: no setting, no editor control, no option. `AudioSystemComponent` only offers `MasterVolume`.
- Project settings are read and written field by field by `ProjectSettingsHelper` (`CasaEngine/Framework/Configuration/Project/ProjectSettingsHelper.cs:10-51` and `:53-93`). The optional `DialogueScreenAsset` is read with a default and written only when set (`:29`, `:80-83`), so project files that do not use it stay unchanged.
- The runtime loads the project in `CasaEngineGame.Initialize` before it creates `AudioSystemComponent` (`CasaEngineGame.cs:342-366`). The editor creates its hosted runtime without a project (`CasaEngine.Editor/GameEditor.cs:1029`) and opens projects later through `EditorProjectAuthoringService.LoadProject`, which raises the static `ProjectLoaded` event (`CasaEngine.EditorServices/EditorProjectAuthoringService.cs:15-24`).
- Two `ProjectSettings` instances can coexist; `ApplyDisplaySettings` updates both (`CasaEngineGame.cs:180-193`).

## Decision

- New field `ProjectSettings.IsAudioMuted` (category "Audio", default false, outside `#if !FINAL`). `ProjectSettingsHelper` reads it with a false default and writes the key only when it is true.
- The runtime applies it to the `Master` bus when `AudioSystemComponent` is created, from `CasaEngineGame.RuntimeContext.ProjectSettings`. `AudioSystemComponent.IsMuted` gets and sets the `Master` mute and mirrors the value into the in-memory project settings (both instances), so an editor save keeps it. The runtime never writes the project file.
- Editor side: a small disposable subscriber in `CasaEngine.EditorServices` applies the setting to the editor runtime's mixer on every `ProjectLoaded`. `GameEditor` creates it with its hosted runtime and disposes it with the editor.
- No user interface: the setting is edited in the project file.

## Consequences

- A project that is not muted keeps a byte-identical project file.
- Muting `Master` also silences the editor's sound previews, because the `Editor` bus hangs from `Master`.
- A tool that regenerates the project file must carry the key over. The Alundra converter does so (parent plan, T1.5).
- Bus volumes are still not persisted (known V1 limit, ADR-0001).
