# ADR-0130: A world video component plays under sprites, and an input entry can react to any key

- **Status**: Accepted
- **Date**: 2026-11-09
- **Source**: this chantier: `ai-agent/tasks/world-video-and-any-key-tasks.md` (decisions D1 to D8, settled with the author on 2026-11-09, and choices C1 to C9, locked by the approval of the plan on 2026-11-09), branch `chantier/world-video-and-any-key`. Follows ADR-0034, ADR-0080, ADR-0128 and ADR-0129.
- Renumbered from ADR-0091 on 2026-11-19 when origin/main was merged (its ADR-0091 is another decision)

## Context

- A data-only project needs a cutscene to play a film with a sprite fading in above it, and to leave the cutscene when the player presses any key of the keyboard.
- `PlayVideo` (ADR-0128) shows a film in an MGUI screen. A view's UI is drawn after its scene, so the screen covers every sprite of the world.
- `MdecVideoPlayer` owns a `Texture2D` of the displayed size and updates it with one `SetData` per new frame. A frame-0 decode error does not throw: the player ends at once and logs once.
- Sorted 2D sprites test and write depth with `LessEqual`, and the larger Z is drawn first; the 2D camera looks at its target from a larger Z, so a larger Z is nearer. A film that wrote depth would hide a sprite of smaller Z drawn after it. An entry flagged `IgnoresDepth` (ADR-0034) runs with no depth state, but the only overload that sets it also forces a sort key.
- A `.buttonsMapping` entry binds a keyboard key, a gamepad button or a mouse button. There is no "any key". `KeyButton.Load` reads its four fields unconditionally; `UINavigationInput` binds only keyboard and gamepad devices.
- `World.InternalAddEntities` logs an exception raised by an entity's `InitializeWithWorld` and does not add the entity; other add paths let the exception propagate.

## Decision

- **`VideoSpriteComponent`**, a scene component of a world entity.
  - JSON: `video` (required, name or id of a catalogued `mdecvideo`) and optional `color` (the entity object format, white when absent). A malformed field fails the load with an `InvalidDataException` naming the component, the field and the value.
  - `Load` keeps the name. `InitializeWithWorld` acquires the asset, creates an `MdecVideoPlayer`, and hides the entity whatever `is_visible` says: only `PlayVideoSprite` shows it.
  - Creation fails, with the player and the asset handle released by the component itself and an `InvalidDataException` naming the component, the field and the value, when the name is not catalogued, the asset is not `mdecvideo`, the asset cannot be acquired, the player constructor throws, or the player is already ended after construction (frame 0 cannot be decoded). From a `.world` file the entity is then absent from the world; on the other add paths the exception propagates.
  - The film is drawn with the whole texture as source, its centre as origin, `Position.Z` as Z and the colour as given, through a new additive key-less overload of `SpriteRendererComponent.DrawSprite` that also takes `ignoresDepth`; the component passes `true`. The film neither tests nor writes depth and is ordered by Z like a key-less sprite: with a larger Z than a sprite, it is drawn first and the sprite, drawn after, blends over it.
  - `Play()` restarts the film, `Stop()` stops it; `Update` advances it by the received scaled game time only while it plays. At the end the last frame stays drawn while the entity is visible. No allocation in `Update` or in the draw.
  - The copy constructor carries the name and the colour; each clone creates its own player. The editor save path writes `video`, and `color` when not white.
  - `MdecVideoPlayer` gains an internal accessor to its texture; no public API is added to it.
- **`PlayVideoSprite`** (`{"type": "PlayVideoSprite", "entity": "<name>"}`): shows the entity and plays the film from the start, waits for its end, then hides the entity. If the play stops first (stop, replacement by `ExitOnInput`, failure), the cleanup stops the film and hides the entity, except when the world is cleared. It fails the play (`Failed`, message naming the field and the value) for an unknown entity, an entity whose root component is not a `VideoSpriteComponent`, or a component that was never initialised (an entity listed before its `InitializeWithWorld` failed, as the editor's immediate entity reference can leave it; a `.world` load drops such an entity instead). The validator refuses a missing or blank `entity`.
- **`InputDevices.AnyKey`**, appended to the enum.
  - JSON is unchanged: the four `KeyButton` fields stay required; for `AnyKey`, `key`, `game_pad_button` and `mouse_button` are read and ignored, written as `"key": "None", "game_pad_button": "None", "mouse_button": "LeftButton", "input_device": "AnyKey"`.
  - It is pressed on the frame a key goes down that was up on the previous frame of the `KeyboardManager`, when that key is not Shift, Control, Alt or Windows (left and right) and no Alt or Windows key is held. So Alt+Tab, Alt+F4 or Windows+D do not count, while Shift+A or Ctrl+A do.
  - The `KeyboardManager` reads it with `GetPressedKeyCount` and `GetPressedKeys` on a reused array: no allocation.
  - An `AnyKey` entry reports a press, not a hold; two new presses on consecutive frames give one edge. `ExitOnInput` uses it like any entry, with its arming rule unchanged. `UINavigationInput` binds nothing to it.
- **Additive.** Existing projects, worlds, mappings and cutscenes load and behave as before.

## Consequences

- A data-only project can play a film under blended sprites and leave on any key, with no game code.
- The film is paced by the scaled game time, like the sprite actions of ADR-0129.
- Blended sprites of different Z still write depth in larger-Z-first order: sprites that must blend over each other keep one Z. Only the film is taken out of that case.
- The world film has no skip of its own; leaving goes through `ExitOnInput`.
- The gamepad is still read while the window has no focus (O5 of ADR-0129).
- Deferred: an "any button" of the gamepad or the mouse, sound, editing the new data in the editor (the read-only builder and the save path are done).
- The ADR number may change if `origin/main` gains ADRs before this branch is merged.
