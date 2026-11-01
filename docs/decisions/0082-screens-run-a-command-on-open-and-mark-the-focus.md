# ADR-0082: Screens run a command on open and mark the focus

- **Status**: Accepted
- **Date**: 2026-11-01
- **Source**: this chantier: `ai-agent/tasks/screen-open-and-focus-markers-tasks.md` (decisions D1 to D6, delegated to and settled by the author on 2026-11-01), branch `chantier/screen-open-and-focus-markers`. Amends ADR-0078 and ADR-0081.

## Context

- ADR-0081 accepts as a limit that no fade runs when a screen opens: nothing starts a command then, and a project's first screen, which `UpdateWorld` opens after the views have updated, draws one frame before its first `Update`, so a fade in started from `Update` would show the screen once, unfaded. A screen opened by `open_screen` is not in that case: the action runs inside the loop of `ScreenStack.Update`, which reaches the new screen on the same frame.
- A menu drawn as text rows needs a cursor beside the focused row. MGUI's focus visuals are colors of the element itself, and an element has no property that says it holds the focus: `HasKeyboardFocus` is an explicit interface method, and `VisualState` says Focused only outside the Pointer input mode. The desktop does report every change, through `MGDesktop.FocusedKeyboardHandlerChanged`, but that event knows nothing of screens: it also fires for a screen frozen under a modal one, which has to keep its marker. That is why the screen polls, see the decision on `Update` below.
- A cursor can be taller than a row, so two cursors in their own buttons would overlap the neighbouring button.

## Decision

- **`on_open_command` names an entry of `ui_commands` that starts when the screen loads.**

  ```json
  {
    "on_open_command": "Open",
    "ui_commands": [
      {
        "name": "Open",
        "actions": [
          { "type": "fade_screen", "layer": "AboveUI", "blend_mode": "Subtractive", "from": [255, 255, 255], "to": [0, 0, 0], "duration_seconds": 0.5 },
          { "type": "wait_fade" }
        ]
      }
    ]
  }
  ```

- **It starts at the end of `OnWindowLoaded`,** so the first frame drawn already shows the fade. A `wait_fade` then suspends it as in any list (ADR-0081), and `Update` resumes it.
- **It accepts only `play_sound`, `fade_screen` and `wait_fade`.** The screen is not on the stack yet at that point, so anything that changes the stack or the world is refused at load, with the path of the envelope.
- **A world's time starts at its first frame.** The fade above is started inside the Update that loads the world, and that time would otherwise be replayed before anything is drawn (see [Measures](#measures)). So once a world is ready, its first screen included, `LoadTimeDiscard` drops the elapsed time over two frames, in the runtime only:
  1. The first frame runs at time zero, so a fade started at open shows `from`.
  2. The second frame gets one normal step.
  3. What each of them took is dropped once it is presented, with `Game.ResetElapsedTime`. Then time runs normally.

  This applies to every world, not only to one with a first screen.
- **`focus_markers` lists pairs of XAML names.**

  ```json
  {
    "focus_markers": [
      { "element": "btnStart", "marker": "cursorStart" },
      { "element": "btnQuit", "marker": "cursorQuit" }
    ]
  }
  ```

  `marker` is visible only while `element` holds the keyboard focus. The pair is explicit because a marker can be a sibling of its element on a `Canvas`, outside the button it follows.
- **A hidden marker keeps its place.** It uses `Visibility.Hidden`, so the layout does not move with the focus.
- **`Update` compares the focus with the last one it saw,** with no subscription and no allocation, and only then writes the visibility. A screen frozen under a modal one keeps its marker: the modal screen takes the focus, and only `Update` stops with the screen (`ScreenStack.Update`). The comparison then catches up on its own when the modal screen closes. It also reads the field `WatchFocusMove` reads anyway, and leaves nothing to release when the screen ends.
- **A name the XAML does not declare, or an `element` that cannot take the focus, is refused at load.**
- **A `marker` named by two pairs is refused at load.** `ShowFocusMarkers` writes the visibility pair by pair, so only the last pair would drive it. Measured before the refusal: with the focus on the first element, the marker stayed hidden. One element can still have several markers.
- **A `marker` that is, or contains, the `element` of any pair is refused at load,** its own pair or another one. `Visibility.Hidden` draws nothing of an element and what it contains (`MGElement.Draw`, read in the code and not run) and takes the keyboard input from them (`MGElement.Update`).
  - On its own element, the focus that would show the marker could never arrive. Measured before the refusal: 120 frames and three key presses left the element without the focus, the marker hidden and no error.
  - On the element of another pair, the marker is shown by its own element's focus, and hides when the focus moves to that other element. Measured on a headless desktop, before the refusal: the focus reaches the element on the frame of the key, the marker hides on that frame, the next frame clears the focus and the one after gives it back to the first button. The element stays out of reach of the keyboard.
- **That refusal is a simple rule, and it also refuses one case that would work.** `CanHandleInputsWhileHidden` keeps the keyboard input through a hidden element, and it changes the two cases differently. Both were measured on a headless desktop, with the refusal removed:
  - A marker that is its element stays out of the navigation: `MGDesktop.IsNavigationTarget` reads the element's own `Visibility`. Key presses never reached it, and `default_focus` gave it the focus once, which it lost for good at the first move.
  - A marker that is an ancestor of its element works: the element's own `Visibility` stays `Visible`, so it stays a navigation target. The focus went in and out and came back, and the marker followed. It is refused all the same: from reading `MGElement.Draw`, the element is not drawn until it holds the focus, and one rule with no exception is easier to state and to check. No project in this repository has this shape.

## Consequences

- A project with no DLL can fade a menu in and show a cursor on its focused row.
- The format is additive: envelopes without these fields load and save unchanged. An older engine ignores both fields, so the screen would open without its fade and with every marker showing.
- A fade started at open shows `from` on the first frame and its first step on the second. A fade of N steps reaches `to` on frame N + 1, one frame later than a fade that shows one step on its first frame, such as one started by a confirm from the UI.
- Nothing that follows the elapsed time advances during the start of a world: the Update that loads it and the first frame's Update run at time zero, as MonoGame's first Update always did.
- Markers are all hidden until a focus arrives. With `default_focus` that is the third `Update` after the load. Without it, no focus arrives by itself: `MGDesktop.ActiveInputMode` starts as Pointer, and MGUI gives no focus in that mode (`UIFocusNavigationService.QueueAutoFocusIfNeeded`). The markers of such a screen wait for the first key or gamepad press, or a click (see [Measures](#measures)). A screen opened while another one holds the focus takes it without any input, and so does a screen opened after a key press.
- Known limits, accepted:
  - A marker jumps with the focus: nothing slides it.
  - A marker follows one element. An element inside a focused group does not count as the group.
  - The mouse is outside this: a click that moves the focus moves the marker, and hover does not.
  - Only the start of a world is protected. A late frame after the second one still loses steps, and a first physics step that falls later than the second frame is such a frame. That happens with a variable step in `Projects/PixelUiSample`, where it is one long step: the fade jumps from 249 to 222 and keeps its real duration.
  - The editor is unchanged: it runs the loop and never runs its hosted game, so nothing stops a catch-up of its own loop. That catch-up has not been measured.

## Measures

The Launcher, Debug build, MonoGame 3.8.5.1, no input, a copy of `Projects/PixelUiSample` with `"IsFixedTimeStep": true` and a fade of `0.85` seconds from 255 to 0 started by `on_open_command`: 51 steps of 5. The values are one channel of the overlay as drawn, frame by frame. Each row was run twice, with the same values and the same Updates.

| Code | Values drawn from frame 1 | Updates before each frame |
|---|---|---|
| No discard | 110, then 0: the fade shows on one frame | 30, 26, then 1 |
| One reset when the world is ready | 245, 115, 110, then 5 by 5 down to 0 on frame 25 | 3, 26, then 1 |
| Discard until the first frame is presented | 255, 250, 225, 220, then 5 by 5 down to 0 on frame 48 | 2, 1, 5, then 1 |
| Discard over two frames, this decision | 255, 250, then 5 by 5 down to 0 on frame 52 | 2, then 1 |

Frame 1 has 2 Updates because `Game.Run` runs one Update before its loop, on a `GameTime` of its own with no elapsed time, and that Update is the one that loads the first world. The other Update is the first of the loop.

Where the time went. This table comes from an earlier measure on another project. The eight runs above gave the same order, with the load at 994 to 1096 ms, the first Update of the views at 64 to 71 ms, the first Draw, presentation included, at 252 to 257 ms, and the first physics step at 78 to 88 ms, then 0.04 to 0.09 ms for the next one.

| Delay | Measured |
|---|---|
| The load, in the Update before the loop | about 977 ms |
| The first Update of the views, in the loop | about 65 ms |
| The first Draw | about 248 ms |
| The first physics step | 81.55 ms, then 0.04 ms for the next one |

- `Game.MaxElapsedTime` reads 500 ms, which caps a catch-up at 30 Updates of 1/60 second. With no discard, those 30 Updates and the 26 of the second frame cover a fade of 51 steps.
- Physics skips a zero elapsed time, so its first step lands on the first Update that receives time. This is what the second frame of the discard covers. Why that step is slow has not been measured.
- A fade out to `to` followed by `change_world`, also of `0.85` seconds, draws 5, 10, then 5 by 5 up to 255 on 51 frames, and 255 is drawn before the world changes. The first frame of the next world still draws 255. With no discard the sequence is the same, and the only difference is one frame with 10 Updates just after the change. With the discard every frame has 1. Each case was run twice.
- A reset made from inside an Update ends the catch-up of the running `Game.Tick`, which reads the accumulated time on each pass of its loop. With a world change delayed by 1200 ms, one frame after the change had 29 Updates before the discard. With it, every frame has 1. `LoadTimeDiscardGameLoopTests` holds this against the package.
- When the focus arrives, in the Launcher on `Projects/PixelUiSample`, Debug build, with a temporary trace of each screen `Update`: with `default_focus`, on `Update` 3. On a copy with that field removed, the mode stayed Pointer and no focus came for 930 `Update`s, until the first key. `DataDrivenScreenTests` gives the same results on a headless desktop.
- Not measured: a Release build, and when the operating system first shows frame 1 on screen.
