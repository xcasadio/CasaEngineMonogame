# ADR-0082: Screens run a command on open and mark the focus

- **Status**: Accepted
- **Date**: 2026-11-01
- **Source**: this chantier: `ai-agent/tasks/screen-open-and-focus-markers-tasks.md` (decisions D1 to D6, delegated to and settled by the author on 2026-11-01), branch `chantier/screen-open-and-focus-markers`. Amends ADR-0078 and ADR-0081.

## Context

- ADR-0081 accepts as a limit that no fade runs when a screen opens: nothing starts a command then.
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

- **It starts at the end of `OnWindowLoaded`,** because loading is the one place every way of opening a screen goes through, so the first frame drawn shows the fade whichever way the screen opened. A `wait_fade` then suspends it as in any list (ADR-0081), and `Update` resumes it.
  - No frame order asks for this on most screens. A first screen opens in the Update that `Game.Run` runs before its loop, and the first Update of the loop reaches it before the first Draw. A screen opened by `open_screen` gets its `Update` on the frame it opens, inside the loop of `ScreenStack.Update`. A trace of the Launcher showed both orders.
  - It matters for a screen that gets no `Update` on the frame it opens, see the known limits.
- **It accepts only `play_sound`, `fade_screen` and `wait_fade`.** The screen is not on the stack yet at that point, so anything that changes the stack or the world is refused at load, with the path of the envelope.
- **A world's time starts at its first frame.** The fade above is started inside the Update that loads the world, and that time would otherwise be replayed before anything is drawn (see [Measures](#measures)). So once a world is ready, its first screen included, `LoadTimeDiscard` drops the elapsed time over two frames, in the runtime only:
  1. The first frame runs at time zero, so a fade started with the world, by the `on_open_command` of its first screen, shows `from`.
  2. Under a fixed step, the second frame gets one normal step. Under a variable step, the default (`ProjectSettings.IsFixedTimeStep`), it gets what passed since the first frame was presented, which is a short gap (see [Measures](#measures)).
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
- **`Update` compares the focus with the last one it saw,** with no subscription and no allocation, and only then writes the visibility. A screen frozen under a modal one keeps its markers, because only `Update` writes them and it stops with the screen (`ScreenStack.Update`). The modal screen usually takes the focus, so the marker stays on the element that held it. When the focus reaches the frozen screen instead, the marker does not follow, see the known limits. The comparison then catches up on its own when the modal screen closes. It also reads the field `WatchFocusMove` reads anyway, and leaves nothing to release when the screen ends.
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
- A fade started with a world, by the `on_open_command` of its first screen, shows `from` on the first frame. Under a fixed step it shows its first step on the second, and a fade of N steps reaches `to` on frame N + 1, one frame later than a fade that shows one step on its first frame, such as one started by a confirm from the UI.
- A screen opened by `open_screen` is not at the start of a world: its frame has a normal elapsed time, so a fade of its `on_open_command` shows its first step on its first frame and reaches `to` on frame N.
- What follows `GameTime.ElapsedGameTime` in an Update does not advance during the start of a world: the Update that loads it, from the moment the world is ready, and on the first world the Update of the first frame, run at time zero, as MonoGame's first Update always did. That holds for `GameManager.UpdateWorld` and the `GameComponent`s, fades included. It does not hold for what follows:
  - `Game.ResetElapsedTime` leaves `GameTime.TotalGameTime` alone, and `Game.Tick` has already added the time of the frame to it. MGUI's clock comes from there: `CasaDesktopRuntime` derives `FrameElapsed` from it, which drives `MGDesktop.Animations`, `MGTimer`, `MGStopWatch` and `MGImage`. So do the repeat of `UINavigationInput` and `FrameTime.UnscaledTotalTime`. See [Measures](#measures) for what MGUI reads on frame 1.
  - `Game.Tick` writes the elapsed time again before `Draw`, so under a fixed step `Draw` reads one step on frame 1, and 0 under a variable step (measured).
  - A game class that reads `gameTime` in its `Update` before it calls `base.Update` runs before the reset. `DemosGame` does. This comes from reading the code.
  - A new `UIRoot` starts its clock from zero, so after a change of world its first `FrameElapsed` is all of `TotalGameTime` (see [Measures](#measures)). The discard has no part in this. A timer or an animation that runs on that frame takes the whole of it, which comes from reading the code.
- Markers are all hidden until a focus arrives. With `default_focus` that is the third `Update` after the load. Without it, no focus arrives by itself: `MGDesktop.ActiveInputMode` starts as Pointer, and MGUI gives no focus in that mode (`UIFocusNavigationService.QueueAutoFocusIfNeeded`). The markers of such a screen wait for the first key or gamepad press, or a click (see [Measures](#measures)). A screen opened while another one holds the focus takes it without any input, and so does a screen opened after a key press.
- Known limits, accepted:
  - A marker jumps with the focus: nothing slides it.
  - A marker follows one element. An element inside a focused group does not count as the group.
  - The mouse is outside this: a click that moves the focus moves the marker, and hover does not.
  - Only the start of a world is protected. A late frame after the second one still loses steps, and a first physics step that falls later than the second frame is such a frame. That happens with a variable step in `Projects/PixelUiSample`, where it is one long step: the fade jumps from 249 to 222 and keeps its real duration.
  - The editor is unchanged: it runs the loop and never runs its hosted game, so nothing stops a catch-up of its own loop. That catch-up has not been measured.
  - A `BelowUI` fade started by the `on_open_command` of a first screen stacks two quads on frame 1, which has two Updates: `ScreenEffectComponent` submits one per Update in that layer, and the sprite queue only empties when it is drawn. Measured in the Launcher on both kinds of step, two runs each: 2 quads on frame 1, then 1. From reading the code, a value that does not saturate is applied twice on that frame. `AboveUI` draws one quad.
  - A `play_sound` in the `on_open_command` of a first screen starts before the window is shown: the command runs in the Update before the loop, and `SdlGamePlatform.RunLoop` shows the window (read in its IL). The discard holds the game clock, not the audio. In the Launcher, two runs, the action ran 389 and 404 ms before frame 1 was presented. When the sound is heard has not been measured.
  - A project that suppresses or overrides drawing while a world starts leaves its time frozen: `_framesLeft` only falls in `FramePresented`, and until it does `UpdateStarting` resets the time on every Update. Measured in the Launcher with `Game.SuppressDraw` on the first 300 Updates: all of them read 0, the fade stayed at `from`, MGUI's clock moved, and time ran again from the first frame presented. An override of `BeginDraw` that returns false skips `EndDraw` in `Game.DoDraw` the same way, read in its IL and not run.
  - A frozen screen's markers do not follow a focus that reaches it, because only `Update` writes them. A click on the frozen screen brings the focus there, and a modal screen with nothing to focus leaves it there. Measured on a headless desktop: after the click the focus was on the second button and the first marker still showed.
  - `open_screen` and `close_screen` in one list, in either order, give the new screen no `Update` on that frame: the closing screen leaves the stack and the new one falls to an index the loop of `ScreenStack.Update` has passed. `close_screen` from a screen that is not at the top shifts the stack the same way, and the screen just above it skips one frame. Measured on a real `ScreenStack` with screens that count their Updates. This is the case the start at load serves: from reading the code the window is drawn on that frame, with its command already started.

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
- A variable step, the default, on the same copy with `"IsFixedTimeStep": false`, four runs, two of them with the fade `BelowUI`. The elapsed time of the Update of frame 2 read 1.7 to 3.0 ms, and that of frame 3 read 0.2 to 0.4 ms. The fade drew 255, 254, 254, then 250 or 251. One Update of 78 to 98 ms came on frame 4 or 5, the size of the first physics step above, though these runs did not trace physics.
- What MGUI read on frame 1, with the game's elapsed time at 0: 16.667 ms under a fixed step, two runs, and 46 to 63 ms under a variable step, the four runs above. From frame 2 the two clocks agree, because a reset made after a presentation empties the time before `Game.Tick` adds it to `TotalGameTime`.
- After a `change_world`, one run on each kind of step, the first `FrameElapsed` of the new `UIRoot` read 7967 ms and 8066 ms: all of `TotalGameTime`, about the length of the run.
- A popup opened by `open_screen`, with a fade of 51 steps in its `on_open_command`, under a fixed step, two runs: its first frame draws 250, then 5 by 5 down to 0 on frame 51 of the popup. In the run that traced it, its first `Update` came before that frame's Draw.
- When the focus arrives, in the Launcher on `Projects/PixelUiSample`, Debug build, with a temporary trace of each screen `Update`: with `default_focus`, on `Update` 3. On a copy with that field removed, the mode stayed Pointer and no focus came for 930 `Update`s, until the first key. `DataDrivenScreenTests` gives the same results on a headless desktop.
- Not measured: a Release build, and when the operating system first shows frame 1 on screen.
