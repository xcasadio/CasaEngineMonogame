# ADR-0129: Cutscene actions drive named sprite entities over time

- **Status**: Accepted
- **Date**: 2026-11-09
- **Source**: this chantier: `ai-agent/tasks/sprite-cutscene-actions-tasks.md` (decisions D1 to D8, settled with the author on 2026-11-09, and choices C1 to C10, locked by the approval of the plan on 2026-11-09), branch `chantier/sprite-cutscene-actions`. Follows ADR-0020, ADR-0051, ADR-0080 and ADR-0088.
- Renumbered from ADR-0090 on 2026-11-19 when origin/main was merged (its ADR-0090 is another decision)

## Context

- A data-only project needs a cutscene to play a 2D sprite sequence: show a sprite at a position with an additive or subtractive blend, move it at constant speed, tint it over a duration, hide it, loop the sequence, and leave it for another cutscene when the player presses one key among a list.
- Cutscenes (ADR-0020, ADR-0088) run sequentially on the world's coroutines with `Sequence` and `Parallel`; there is no loop, no key wait, and no action that targets a sprite. `CutsceneDirector` plays one cutscene at a time and raises `Ended` once per play.
- `StaticSpriteComponent` always draws with white and the opaque path, ignores its `Color` property (which defaults to transparent black and is not copied), and reads only `spriteDataName` from JSON. Resolving that name at load time reads `Owner.World` before the component is attached, so a sprite named in JSON does not load today.
- `SpriteRendererComponent` already blends per draw entry (`Opaque`, `AlphaBlend`, `Additive`, `Subtractive`); its only `Sprite` overload with a blend mode also takes a sort key, and keyed entries leave the Z order.
- `Entity.IsVisible` exists and gates drawing, but is not read from JSON.
- Named input entries exist in `.buttonsMapping` assets and are evaluated by `InputMappingManager` (pressed this frame = pressed and not pressed on the previous update).

## Decision

- **Sprites are named world entities** whose root component is a `StaticSpriteComponent`. Cutscene actions target them by entity name.
- **Sprite component, additive fields.** `Color` defaults to white, is copied and is passed to the renderer. `BlendMode` (`SpriteBlendMode`) defaults to the current drawing path. JSON gains an optional `color` object (`r`, `g`, `b`, `a`, 0 to 255, white when absent) and an optional `blend_mode` (exact enum name). An entity gains an optional `is_visible` key (true when absent), written by the entity save path. A malformed field fails the load with an error naming the component, the field and the value. A blended sprite with a `DepthSortable2DComponent` keeps its sort key; without one, a new additive renderer overload queues it without a key, so it keeps its Z order. The blend applies to the whole sprite; ADR-0051's per-texel semi-transparency is not tied to it.
- **Sprite name resolution moves to `InitializeWithWorld`.** `Load` only keeps `spriteDataName`; an absent name means no sprite. The copy constructor carries the colour, the blend mode and the sprite reference, and a clone rebuilds its sprite, so inline and asset entities both draw.
- **Time, not frames.** Sprite motion and tint are measured in seconds and pixels per second and advance with the scaled game time each entity update, like `Wait`. The state lives on the component: a move follows a straight line at constant speed and lands exactly on its target; a tint is linear per channel, rounded to the nearest integer, and lands exactly on its target (duration 0 applies it at once). A new move or tint replaces the running one; showing or hiding the sprite stops both. A consumer that counts frames converts them to time.
- **Actions** (snake_case fields; numbers are JSON numbers read as `float`, `count` is an integer):
  - `ShowSprite`: `entity`, then optional `sprite`, `x` and `y` (together), `color` and `blend_mode`; makes the entity visible and keeps absent fields.
  - `HideSprite`: `entity`.
  - `MoveSprite`: `entity`, `x`, `y`, `speed` (strictly positive).
  - `TintSprite`: `entity`, `color`, `duration` (at least 0).
  - In actions, `color` follows the cutscene convention: `[r, g, b]` (alpha 255) or `[r, g, b, a]`.
  - The four sprite actions are instantaneous: the sequence continues in the same frame.
- **`Repeat`**: required integer `count` (0 = forever) and `actions`. Each lap plays the children in sequence. An endless lap that ends without a frame passing fails the play instead of freezing the game.
- **`ExitOnInput`**: `mapping` (a catalogued `.buttonsMapping`), `inputs` (entry names, at least one), `next_cutscene` (a catalogued cutscene). It arms a watcher, owned by the director, that keeps its own `InputMappingManager` limited to the listed entries and updated with the game's devices each frame. Its first update only records the state, so a key held at arming triggers only after a release and a new press. On a press of the frame, the director plays the next cutscene, which stops the current play (`Ended` with `Stopped`). The director stops the watcher before raising `Ended` on every end of a play (completed, failed, stopped or replaced).
- **Failures.** The validator refuses missing or malformed fields and out-of-range values. At run time, an unknown entity, an entity without a root `StaticSpriteComponent`, or a sprite, mapping, entry or cutscene that cannot be found fails the play with a message naming the field and the value, like `OpenScreen`.
- **Additive.** Existing projects, worlds, entities and cutscenes load and behave as before.

## Consequences

- A data-only project can script a looping sprite sequence and leave it on a key, with no game code.
- Motion is smooth and independent of the frame rate; it does not reproduce the stepping of a fixed-tick original. The gap, if any, is measured by the consumer.
- Motion and tint continue on the entity after the cutscene stops, since their state is on the component.
- The delay between a press and the stop depends on the order of a frame (input component, coroutines); it is measured and documented. The same press can also reach the UI navigation, for example a video skip.
- `StaticSpriteComponent.Color` now defaults to white instead of transparent black; code that read the unused default sees the change.
- Deferred: sprite frame animation, rotation and scale actions, multi-part sprites, frame-counted variants of the new actions, per-texel semi-transparency driven by an action, editing the new actions in the editor (the read-only builder shows them).
- The ADR number may change if `origin/main` gains ADRs before this branch is merged.
