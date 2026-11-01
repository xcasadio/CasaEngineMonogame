# ADR-0081: Screen fades and waits in data-driven commands

- **Status**: Accepted. Amended by ADR-0082 (`on_open_command` lifts the limit on a fade when a screen opens)
- **Date**: 2026-10-31
- **Source**: this chantier: `ai-agent/tasks/screen-fade-actions-tasks.md` (decisions D1 to D10, delegated to and settled by the author on 2026-10-31, complements C1 to C8), branch `chantier/screen-fade-actions`. Amends ADR-0078.

## Context

- ADR-0078 lists five actions and states that a list has neither conditions nor waits, and it names the fade of a menu as the first thing that cannot be expressed.
- `ScreenEffectService` already fades the screen, above the UI as well (ADR-0033, ADR-0034), with a subtractive mode, and its overlay outlives a change of world. No data reaches it: only code and cutscenes do.
- `DataDrivenScreen.Run` ends the list in one `Update`, so nothing can follow the end of a fade.
- A confirm pressed during a fade would run a second command, and a key would move the focus.
- MGUI could not subtract. MGUI's ADR-0024 adds `BlendType.Subtractive`, and `BlendType` on an `Image`, on the MGUI branch this one points at.
- MGUI's gamepad tracker repeats a held button whoever reads it, and A is Submit, so a raw path that is simply turned back on takes a button held through a wait for a new press. MGUI's ADR-0025 adds the pause this needs.
- A `BlendType` needs an entry in `CasaMonoGameRenderInterop`: its table throws on a value it lacks.
- Frame order: the UI updates before `GameManager.UpdateWorld`, which runs before the `GameComponent`s, `ScreenEffectComponent` being among the last (`CasaEngineGame.Update`, `ComponentOrder.cs`).

## Decision

- **Two actions join the closed list of ADR-0078.**

  ```json
  {
    "name": "Start",
    "actions": [
      { "type": "fade_screen", "layer": "AboveUI", "blend_mode": "Subtractive", "from": [0, 0, 0], "to": [255, 255, 255], "duration_seconds": 0.5 },
      { "type": "wait_fade" },
      { "type": "change_world", "target": "Black.world" }
    ]
  }
  ```

- **`fade_screen` has five required fields and no default.** `layer` and `blend_mode` are exact names of `ScreenEffectLayer` and `SpriteBlendMode`. `from` and `to` are `[r, g, b]` or `[r, g, b, a]`, integers from 0 to 255: without the fourth, the alpha is 255, as with the color-only overloads of the service. `duration_seconds` is finite and not negative, and 0 applies `to` at once. A malformed value goes to `CommandFormatErrors`, a missing field fails in `PrepareAction`, and both end as the same exception when the screen opens. `fade_screen` takes no `target`.
- **`wait_fade` suspends the list while a fade runs,** any fade, on the same service. It takes no target. No fade running means no wait. The list goes on in the first `Update` that finds the fade over.
- **Only `play_sound` and `fade_screen` may come before a `wait_fade`,** along with another `wait_fade`, and anything else is refused at load. A modal `open_screen` freezes the screen, `close_screen` and `change_world` take it off the stack, and `quit` ends the game: the list would die with no sign. A non-modal `open_screen` would not stop the list and is refused all the same: the check reads the type of the action, not the screen it opens.
- **`DataDrivenScreen` resumes a list with two fields,** the running command and the index of the next action, and no iterator, closure or list. `_isRunningCommand` stays limited to the resuming call, so a `Hide` during a wait disposes at once, and `Dispose` clears the running command. `Remember` refuses while a command is running, which also settles a double confirm.
- **While the screen at the top waits, keyboard and gamepad navigation stops, and a focus that exists stays.** With nothing focused, a key or a button still gives the focus, because MGUI's input mode and auto-focus run outside both paths (`MGDesktop.Update`). Measured with a key on a headless desktop, on both paths: the first key gives the focus to the first button and shows its marker, the next ones move nothing, and a confirm runs nothing. `ScreenStack.IsNavigationSuspended` says so, and `UIRoot` works it out every frame. No screen holds this state, because one frozen under a modal screen runs nothing and could not give it back. `UINavigationInput` keeps reading the inputs and counts a held input as one held at its creation. On MGUI's own path, `UIRoot` sets `Desktop.IsNavigationInputSuspended` at the end of its update (MGUI's ADR-0025), which leaves `UseRawNavigationInput` alone and forgets what is held when it lifts. So on both paths a key or a button held through a wait neither fires nor repeats when the wait ends, until it is released. `IsEnabled` and `IsHitTestVisible` are not used: they clear the focus, so a cursor tied to it would vanish, and `IsEnabled` also changes the look of the window.
- **`StartFade` and `IsFadeRunning` are required members of `UIScreenActionContext`.** `GameManager` wires them to `_game.ScreenEffectComponent.Service`, setting `Layer` and then calling `StartFade`.
- **The service keeps no fixed step.** A project that wants an exact step per frame sets `IsFixedTimeStep` and writes a duration in frames over the rate. A second mode in the service was rejected: it would bring a frame cadence into a mechanism that its own comment leaves to its callers.
- **A cursor that mixes opaque and subtractive texels is two sprites stacked in XAML,** the second with `BlendType="Subtractive"`, each with the other's texels transparent black. No shader and no new sprite format.
- **A world used with a fade above the UI declares an entity with a `Camera2dComponent`.** This is data, and no engine code follows from it.

## Consequences

- A project with no DLL can run a menu's exit to the end: fade, wait, change world.
- The format is additive: envelopes without these actions load and save unchanged. An older engine refuses these actions at load, as ADR-0078 provides for an unknown action.
- `UIScreenActionContext` is public, and `StartFade` and `IsFadeRunning` are `required`: this is a source break for a host outside this repository that builds one, which stops compiling until it sets both. `GameManager` is the only host in the repository.
- Frame offsets, relative to the frame `fade_screen` runs on:
  - the first step shows on that frame, because the service updates after the UI;
  - the list resumes on the frame after the one that reached `to`, so that frame is drawn at `to` before `change_world` runs.
- A step per frame is exact only when it divides 255: the service interpolates from the elapsed time and rounds, so a step that does not divide 255 gives values that are not its multiples.
- Known limits, accepted:
  - No fade runs when a screen opens. ADR-0082 lifts this limit with `on_open_command`.
  - Without a mapping, a button held through a `change_world` counts as a new press on the next world, because that world builds a new `UIRoot` and its tracker starts from nothing. Reaching this needs a screen pushed by code.
  - No action removes the overlay. It outlives `change_world`, so a list that fades out ends on a world that stays hidden, which is why the sample goes to a black one.
  - `Layer` stays on the service once set, as it did for any caller.
  - The mouse is not blocked during a wait: hover goes on, a click can move the focus, and commands stay refused.
  - A focused control that reads keys itself still sees them: only navigation stops. This comes from reading the code.
  - A screen pushed by code on top of a waiting one gets the keyboard and the gamepad at once, on both paths, since only the top counts. The waiting screen is no longer protected meanwhile, and its commands stay refused.
  - A modal screen pushed that way freezes the waiting one, so its list goes on only in the first `Update` after that screen closes, however long ago the fade ended. If the fade still runs then, the stop comes back.
- MGUI's branch must reach `develop` before this one reaches `main`: until then the pointer names a commit that is only on a chantier branch. That branch is `chantier/navigation-input-suspension`, which contains `chantier/image-blend-mode`.
