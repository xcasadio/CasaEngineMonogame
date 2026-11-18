# ADR-0096: The cutscene editor previews in an isolated world and projects its action tree on a timeline

- **Status**: Accepted
- **Date**: 2026-11-18
- **Source**: this chantier: `ai-agent/tasks/cutscene-editor-preview-tasks.md` (decisions D1 to D7, settled with the author on 2026-11-18, and choices P1 to P4 and P9, approved with the plan and open to correction at its acceptance), branch `chantier/cutscene-property-editing`. Completes ADR-0095 and supersedes decision 2 of ADR-0014.

## Context

- ADR-0095 made the action tree of a `.cutscene` editable, and deferred the preview in an isolated world and a read-only timeline projection.
- The editor opens a `.cutscene` as a panel of text fields: there is no world and no way to play the cutscene. The author's acceptance on 2026-11-18 rejected it on ergonomics.
- `CutsceneDirector` exposes `Play`, `Stop`, `IsPlaying`, `GetDebugSnapshot` and the `Ended` event. Its coroutines run in `World.Update`. `Stop` does not restore the entities of the world.
- Some actions reach outside the world: sound (`world.Game.AudioSystemComponent`), screen fades (`world.Game.ScreenEffectComponent`), `ChangeWorld` (asks the game manager to replace the current world), `OpenScreen` and `PlayVideo` (game manager), and `ExitOnInput` (game input devices, then starts the next cutscene).
- `MoveTo` throws an exception on a timeout, on a missing entity and on an entity without a character controller, while `NavigateTo` fails the play through `FailPlay`.
- `CasaEngineGame.ExecutionPolicy` belongs to the whole game. In the editor it is `EditorPreview`, which stops animated sprites, physics and gameplay scripts.
- ADR-0014 (decision 2) forbids a `CutsceneTimelineAdapter` and keeps the cutscene editor a tree control, a timeline being only a read-only projection. The archived plan `timeline-generic-agent-plan.md` excludes a flat cutscene model for the same reason.

## Decision

- **The cutscene document has four zones**: a world preview, the action tree, typed details of the selected action, and a timeline.
- **The action tree stays the saved model.** The timeline is a projection of the tree nodes, not a flat model and not a new asset with tracks. A "time section" action that allows free placement of clips may come later (D1).
- **The preview runs in an isolated copy of a world.** The editor loads a copy of a project world, never the world open in the editor. Play and restart always rebuild that copy from its asset, because `Stop` restores nothing. The real `CutsceneDirector` plays the cutscene being edited, unsaved changes included.
- **Injection point.** A public interface `ICutsceneEffectHost` and a property `CutsceneDirector.EffectHost`, null by default, with three members: `AudioService Audio`, `ScreenEffectService ScreenEffects` and `bool AllowGameRequest(CutsceneActionData action)`. When it is null, behaviour is unchanged (P1).
- **`AllowGameRequest` is asked before `ChangeWorld`, `OpenScreen`, `PlayVideo` and `ExitOnInput`.** On `false`, the action is skipped and the play goes on (P2). In the preview, a skipped `ChangeWorld` ends the play at the next step with a message saying that it would change the world, which stands for the world being cleared in the game (P3).
- **Global effects in the preview** (D3): `ChangeWorld` and the chaining of `ExitOnInput` are only reported; sound is muted by default, with a button to enable it; screen fades are drawn over the preview.
- **World choice** (D4): the displayed world is found automatically, with a list of choices remembered in the editor settings of the project. The `.cutscene` format does not change.
- **Frozen animations** (D2): in the preview, animated sprites, physics and scripts stay frozen for now, because the execution policy belongs to the whole game. A policy per world is deferred.
- **`MoveTo` fails instead of throwing** on a timeout, a missing entity or an entity without a character controller: the play ends with `Ended` and the `Failed` state, as `NavigateTo` does (P4).
- **Time is shown in seconds and in frames** (D5).
- **Supersession.** This ADR supersedes decision 2 of ADR-0014 and lifts the exclusion in the archived plan `timeline-generic-agent-plan.md`, because the timeline adapter projects the nodes of the action tree and not a flat model. ADR-0014 is not rewritten: its status line only mentions the supersession (P9).
- This ADR completes the points that ADR-0095 deferred: the preview in an isolated world and the read-only timeline projection.

## Consequences

- A cutscene can be played in the editor on a copy of a world, without touching the world open, the sound device or the keyboard.
- The runtime gains one additive public interface and one property; a null host keeps today's behaviour.
- A play that fails on `MoveTo` no longer throws: callers that relied on the exception would see a `Failed` end instead.
- The preview shows no animated sprite, physics or script behaviour, so it is not a faithful rendering of those. Sound and fades are limited as described above until the later slices.
- The preview cannot show the effect of `ChangeWorld`, `OpenScreen`, `PlayVideo` or the chaining of `ExitOnInput`; it only reports them.
- Deferred: the timeline itself, the time section action, a per-world execution policy, the sound button and the drawing of fades.
- The ADR number may change if `origin/main` gains ADRs before this branch is merged.
