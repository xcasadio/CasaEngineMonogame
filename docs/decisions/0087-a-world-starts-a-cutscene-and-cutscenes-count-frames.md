# ADR-0087: A world starts a cutscene, and cutscenes count frames, open screens and change worlds

- **Status**: Accepted
- **Date**: 2026-11-08
- **Source**: this chantier: `ai-agent/tasks/timed-screen-cutscenes-tasks.md` (decisions D1 to D7, settled with the author on 2026-11-08, and choices C1 to C9, locked by the approval of the plan on 2026-11-08), branch `chantier/timed-screen-cutscenes`. Extends ADR-0020; follows ADR-0037, ADR-0078, ADR-0081 and ADR-0086.

## Context

- A project with no gameplay DLL can open only one screen: `FirstScreenName`, once per game, on the first world (`GameManager.OpenFirstScreen`, ADR-0078). A world loaded after it has no way to open a screen from data.
- Cutscenes (ADR-0020) are data assets played by `CutsceneDirector` on the world's `CoroutineManager`, but nothing starts one without code: only demos call `CutsceneDirector.Play`, and no `.world` field names a cutscene.
- Cutscene time is in seconds only. `Wait` counts seconds. `FadeScreen` takes a target RGB, `duration_seconds` and a blend mode: it starts from the current overlay colour, stays opaque, never sets the overlay layer, and starts its ramp one update after it is reached. `ScreenEffectService` ramps over seconds from summed frame deltas, so a duration does not map to an exact number of frames. `WaitForFrames` exists in the coroutine layer but no cutscene uses it.
- The screens' `fade_screen` (ADR-0081) takes `from` and `to` colours with alpha, a layer and a blend mode, so it can fade above the UI.
- `CutsceneDirector` has no event: its end is seen only by polling `IsPlaying` or its debug snapshot.
- In `CasaEngineGame.Update`, the UI views update first, then `GameManager.UpdateWorld` runs the world's coroutines, then the game components, `ScreenEffectComponent` included, run. `UpdateWorld` loads a pending world, calls `BeginPlay`, opens the first screen, then updates the world in the same call. `StartCoroutine` only queues: a coroutine first runs at the next `CoroutineManager.Update`.

## Decision

- **A frame is one game update** (`Game.Update`): the world update, then the components. Frame-counted actions ignore elapsed time and `TimeScale`.
- **A world names a startup cutscene.** The optional `.world` key `startup_cutscene_asset_id` holds the id of a cutscene asset (`World.StartupCutsceneAssetId`). Without the key the value is `Guid.Empty`, also on reload; the key is written only when an id is set. The cutscene is read as a copy with `LoadCopy<CutsceneAsset>` (ADR-0037) and played at the end of `World.BeginPlay`, so only when `RunBeginPlay` is true: in the game and in the editor's simulation, not in its preview. Its first action runs in the update that loads the world, after the first screen opens. An unknown id throws an `InvalidOperationException` naming the world and the id. An invalid cutscene is logged once with the world and the validator's messages, and the world runs without it.
- **`WaitFrames`** (`frames`): reached at update U, the next action runs at U+N; with N = 0 it runs at U.
- **`FadeScreenFrames`** (`from`, `to` as `[r, g, b]` or `[r, g, b, a]`, `frames`, `layer`, `blend_mode`, read like the screens' `fade_screen`): reached at update U, it sets the layer and starts the ramp at U. For k from 1 to N the value drawn at U+k-1 is `Lerp(from, to, k/N)`, rounded like the seconds ramp; `from` is frame 0 and is never drawn; the target is drawn exactly from U+N-1; the next action runs at U+N. With N = 0 the target is set at once and the next action runs at U. Without a screen effect service the action completes at once, like `FadeScreen`.
- **`ScreenEffectService.StartFadeFrames`** ramps over a number of `Update` calls, one step per call whatever the elapsed time. `StartFade`, `SetOverlay` and `Clear` cancel it as they cancel a seconds ramp. The existing overloads do not change.
- **`OpenScreen`** (`target`: a screen asset id or name) pushes the data-driven screen on the active UI view, like `FirstScreenName`, without touching its open-once rule. Under external view management (the editor) it opens nothing, logs it, and completes. An unknown screen, no active UI view or no game fails the play (`Failed`, reason logged once).
- **`ChangeWorld`** (`target`: a world file name) checks the world is catalogued, requests it with `GameManager.SetWorldToLoad(string)` and completes at once. The world changes at the next update; if the cutscene still plays then, clearing the old world stops it. An unknown world or no game fails the play.
- **Required fields.** `frames`, `from`, `to`, `layer`, `blend_mode` and `target` are required. A missing or malformed field, or a negative or non-integer `frames`, is a validation error naming the action and the field, so the cutscene does not play.
- **`CutsceneDirector.Ended`** (`EventHandler<CutsceneEndedEventArgs>`, with the final state and the asset) is raised exactly once per started play: when the play runs to its end (`Completed`, or `Failed` after a reported failure, once every branch the failed play still runs is stopped; a failed play stopped or replaced before it unwound raises `Failed` from `Stop`), and when `Stop` stops a play in progress (`Stopped`), which includes clearing the world and a `Play` that replaces a play in progress. A `Play` of an invalid asset starts nothing and raises nothing. An exception thrown by a handler is logged with its context, like a coroutine exception, so it never interrupts `Stop`, a replacing `Play` or a world clear; it is rethrown only when `ThrowCoroutineExceptionsInDebug` is set. A handler may call `Play`; when it does so while a replacing `Play` stops the previous play, the play started by the handler stands and the replacing call starts nothing. A `Play` started by a handler while the world is cleared does not run: the clear stops it.
- **Editor.** The read-only cutscene view shows the fields of the four actions, and the world serializer writes the new key. The world inspector does not show it.
- **Additive format.** An existing `.world` or `.cutscene` loads and plays exactly as before.

## Consequences

- A project with no DLL can chain timed screens from data: each world starts a cutscene that opens its screen, waits, fades and changes world. A world loaded after the first can open a screen.
- Frame-exact timing is defined by update count, not by time: on a machine that runs more updates per second, frame-counted actions run faster in seconds. That is the intent.
- The overlay service belongs to the game, not to the world, so a fade to black before a world change stays black in the next world until something changes it.
- A screen pushed by a cutscene during `UpdateWorld` is updated from the next frame, as the first screen already is.
- **Known limit.** An exception thrown by an action (for example `MoveTo` on a missing entity or a timeout) is caught by the coroutine manager, which stops the coroutine: the director's state stays `Playing` and `Ended` is not raised until a `Stop` or a world clear, which raise `Stopped`; a `Play` that replaces it raises nothing for it. That behaviour predates this decision and is unchanged.
- `FadeScreen` in seconds is unchanged: it still starts one update late and does not set the layer. The screens' `fade_screen` stays in seconds.
- Deferred: a frame-counted `fade_screen` for screens, the new key in the world inspector, and a policy guard for `ChangeWorld` in the editor's simulation.
