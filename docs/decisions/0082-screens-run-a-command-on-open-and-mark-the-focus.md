# ADR-0082: Screens run a command on open and mark the focus

- **Status**: Accepted
- **Date**: 2026-11-01
- **Source**: this chantier: `ai-agent/tasks/screen-open-and-focus-markers-tasks.md` (decisions D1 to D6, delegated to and settled by the author on 2026-11-01), branch `chantier/screen-open-and-focus-markers`. Amends ADR-0078 and ADR-0081.

## Context

- ADR-0081 accepts as a limit that no fade runs when a screen opens: nothing starts a command then, and a screen draws one frame before its first `Update`, so a fade in started from `Update` would show the screen once, unfaded.
- A menu drawn as text rows needs a cursor beside the focused row. MGUI has no data binding to the focus: `HasKeyboardFocus` is an explicit interface member with no change notification. Its focus visuals are colors of the element itself.
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
- **`Update` compares the focus with the last one it saw,** with no subscription and no allocation, and only then writes the visibility. A screen frozen under a modal one keeps its marker.
- **A name the XAML does not declare, or an `element` that cannot take the focus, is refused at load.**

## Consequences

- A project with no DLL can fade a menu in and show a cursor on its focused row.
- The format is additive: envelopes without these fields load and save unchanged. An older engine ignores both fields, so the screen would open without its fade and with every marker showing.
- The first step of a fade started at open shows on the first frame, because the service updates after the UI.
- Markers are all hidden until a focus arrives, which takes a frame or two after the load.
- Known limits, accepted:
  - A marker jumps with the focus: nothing slides it.
  - A marker follows one element. An element inside a focused group does not count as the group.
  - The mouse is outside this: a click that moves the focus moves the marker, and hover does not.
