# ADR-0093: A world video can run to its end after the cutscene that started it

- **Status**: Accepted
- **Date**: 2026-11-12
- **Source**: this chantier: `ai-agent/tasks/video-sprite-runs-to-end-tasks.md` (decisions D1 to D6, settled with the author on 2026-11-12, and choices C1 to C9, locked by the approval of the plan on 2026-11-12), branch `chantier/video-sprite-runs-to-end`. Follows ADR-0090 and ADR-0091.

## Context

- `PlayVideoSprite` (ADR-0091) shows a world video entity, plays its film, waits for its end and hides the entity. Its cleanup runs in a `finally`: when the play stops first (stop, replacement by `ExitOnInput`, failure), it stops the film and hides the entity.
- A data-only project needs the opposite on an exit: the film keeps playing under the exit fade until its last picture, then its entity is hidden. It also needs the film to be restarted by the next lap of a `Repeat`.
- The movement and the tint of a sprite (ADR-0090) are carried by the component and already outlive the cutscene that started them.
- In a frame of the game, `World.Update` runs the coroutines, then the entities. The film ends in the component's `Update`, so `PlayVideoSprite` sees the end one update later and the last picture stays drawn one more frame.
- A stopped coroutine runs its `finally` only at a later update of the coroutine manager.

## Decision

- **`StartVideoSprite`** (`{"type": "StartVideoSprite", "entity": "<name>"}`), a new cut-scene action.
  - It checks the entity like `PlayVideoSprite`: an unknown entity, a root component that is not a `VideoSpriteComponent`, or a component without a player fails the play (`Failed`), with a message prefixed `StartVideoSprite:` that names the entity. The messages of `PlayVideoSprite` do not change. The validator refuses a missing, blank or malformed `entity`.
  - It makes the entity visible and calls `Play(hideWhenEnded: true)`, then returns at once: it yields no frame, and the next action of a `Sequence` runs in the same update.
  - It leaves no coroutine and no cleanup. A stop, a replacement or a failure of the play, or its normal end, does not touch the film. Only clearing the world stops it, through `Detach`.
  - In an endless `Repeat`, a lap that holds only `StartVideoSprite` lets no frame pass and fails the play by the existing rule: a lap holds a `Wait` or another action that takes time.
- **`VideoSpriteComponent.Play(bool hideWhenEnded)`**, a new public overload. `Play()` is `Play(false)`.
  - Each `Play` sets the flag to the value received; `Stop()` clears it.
  - When the player ends during playback, the end of a decode failure included, `Update` stops the playback as before and, if the flag is set, clears it and hides the entity in that same update. The last picture is therefore drawn exactly its duration.
  - No allocation and no log per frame.
- **Living with `PlayVideoSprite`**, through `PlayGeneration`: a `StartVideoSprite` on a film a `PlayVideoSprite` waits for takes the film over, and the older action leaves it alone; a `PlayVideoSprite` on a film started by `StartVideoSprite` calls `Play()`, which clears the flag, and owns the film with its cleanup.
- **Additive.** `PlayVideoSprite`, its wait, its cleanup and its extra frame at the end do not change. Existing projects, worlds and cutscenes load and behave as before.

## Consequences

- A cutscene can start a film and leave it behind, like a sprite movement: an exit fade runs over the film until its last picture, then the entity disappears.
- A film started by `StartVideoSprite` cannot be stopped or hidden by another action; this is deferred.
- The first picture drawn is still the one reached after the first advance of the component in the update of the start (`docs/engine/video.md` §9.3), for both actions.
- Deferred: sound, editing the new action in the editor (the read-only builder and the save path are done).
- The ADR number may change if `origin/main` gains ADRs before this branch is merged.
