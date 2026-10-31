# Data-driven screens

A project with no gameplay DLL can open a XAML screen, wire its buttons to actions and change world from its data alone. The decision is ADR-0078. `Projects/SampleProject` is the working example.

A project with a DLL keeps every existing path: a `XamlUIScreenBase` subclass reads none of the fields below. `FirstScreenName` applies to every project, though: a value left in a project with a DLL now opens that screen, and one that names no screen stops the game when the first world loads.

## Opening the first screen

Name the screen in the project file, by its catalogue name or its id:

```json
"FirstScreenName": "Main Menu"
```

The engine opens it once, after the first `WorldLoaded`, on the active view. In the runtime, a name with no view hosting a UI throws. The Launcher provides that UI. The editor, in edit mode and in Play-in-Editor, opens nothing.

## The envelope

A `.uiscreen` gains three optional fields. A field left out keeps its default and is not written back.

```json
{
  "id": "5b65a744-7adb-4d93-bca0-6afe15a6d6ec",
  "name": "Main Menu",
  "source_xaml_file": "main-menu.xaml",
  "is_modal": false,
  "default_focus": "btnPlay",
  "ui_commands": [
    {
      "name": "Play",
      "actions": [
        { "type": "play_sound", "target": "menu_click" },
        { "type": "change_world", "target": "NextWorld.world" }
      ]
    }
  ]
}
```

- `is_modal`: the screen blocks the screens below it. They stay drawn and receive no `Update`. A click on a data-driven screen below is dropped; a screen with its own class still handles its clicks.
- `default_focus`: the name of the XAML element that takes the keyboard focus. Left out, MGUI decides, except when another screen holds the focus: the first button able to take it does.
- `ui_commands`: the commands the screen offers. A command name is unique within a screen, and two screens may use the same name.

A button reaches a command with its existing XAML property, also shown in the screen editor's inspector:

```xml
<Button Name="btnPlay" CommandName="Play" Content="Play" />
```

A button with no `CommandName` is legal and does nothing.

## Actions

A command runs its actions in order, on the `Update` that follows the click or the Enter key. A list has neither conditions nor waits.

| `type` | `target` | Effect |
|---|---|---|
| `open_screen` | Screen asset name or id | Opens a new screen on top of the others |
| `close_screen` | None | Closes the screen running the action |
| `change_world` | World file, as written in the catalogue | Loads that world. Its screens close with the old view |
| `play_sound` | Sound asset name or id | Plays the sound. It goes on after the screen closes |
| `quit` | None | Quits the game |

## Errors

Invalid data throws an `InvalidOperationException` when the screen opens, naming the envelope and the problem:

- a malformed field, or an action of an unknown type;
- a missing target, or a target on `close_screen` or `quit`;
- a screen, world or sound the project does not have, or a target of another asset type;
- a command with no name, or declared twice;
- a `default_focus` that names nothing, or an element that cannot take the focus;
- a button whose `CommandName` resolves to no command.

An unknown action type stays in the asset with its JSON, so saving an envelope keeps it.

## Limits

- Actions are edited by hand in the JSON: the editor has no panel for them.
- A world does not open a screen when it loads. `FirstScreenName` runs once.
- A list cannot wait, so a fade before `change_world` cannot be expressed.
- The focus is given once. When a screen on top closes, MGUI chooses it: in the sample it returned to the button that opened the popup.
- A click on a screen under another one is dropped, even when that screen is not modal. It still raises the window, which can hide a non-modal screen above it. It also moves the keyboard focus, and only the mouse can then reach the screen on top.
- Closing a window with MGUI's close button does not close its screen. Set `IsCloseButtonVisible="False"` on the window: `CanCloseWindow="False"` alone leaves the button shown.
- A window's exit effect does not show: the window is removed at once.
- A screen with a button takes the keyboard from the game. Unless a control is hovered, MGUI's auto-focus gives the focus to its first navigable control at the first key press, or at once if the last input was a key, and `PlayerInput` then stops reading the keyboard. These screens are menus: do not use one with a button as a HUD over a playable world.
- A keyboard press is ignored while the game window is inactive.
- Gamepad input has not been tested.
