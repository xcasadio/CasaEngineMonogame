# Data-driven screens

A project with no gameplay DLL can open a XAML screen, wire its buttons to actions and change world from its data alone. The decisions are ADR-0078, ADR-0079, ADR-0080 and ADR-0081. `Projects/SampleProject` is the working example, and `Projects/PixelUiSample` adds a design resolution, a bitmap font, its own keys, a screen fade and a subtractive image.

A project with a DLL keeps every existing path: a `XamlUIScreenBase` subclass reads none of the fields below. `FirstScreenName` applies to every project, though: a value left in a project with a DLL now opens that screen, and one that names no screen stops the game when the first world loads.

## Opening the first screen

Name the screen in the project file, by its catalogue name or its id:

```json
"FirstScreenName": "Main Menu"
```

The engine opens it once, after the first `WorldLoaded`, on the active view. In the runtime, a name with no view hosting a UI throws. The Launcher provides that UI. The editor, in edit mode and in Play-in-Editor, opens nothing.

## The envelope

A `.uiscreen` gains nine optional fields. A field left out keeps its default and is not written back.

```json
{
  "id": "5b65a744-7adb-4d93-bca0-6afe15a6d6ec",
  "name": "Main Menu",
  "source_xaml_file": "main-menu.xaml",
  "is_modal": false,
  "default_focus": "btnPlay",
  "fonts": [ "pixel-block" ],
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
- `fonts`: the catalogue names or ids of the `.fnt` assets the screen holds for its lifetime. See [Pixel screens](#pixel-screens).
- `ui_commands`: the commands the screen offers. A command name is unique within a screen, and two screens may use the same name.
- `focus_wrap`, `cancel_command`, `move_command`: how the screen reacts to navigation. See [Navigation](#navigation).
- `on_open_command`: the name of a command that starts when the screen loads, before its first frame. It accepts only `play_sound`, `fade_screen` and `wait_fade`: the screen is not on the stack yet, so anything else is refused at load (ADR-0082). A `wait_fade` here blocks the screen until the fade ends, like any wait. Such a fade shows `from` on its first frame, see [Fades](#fades).
- `focus_markers`: pairs `{ "element": "btnStart", "marker": "cursorStart" }` of XAML names. The marker is visible only while the element holds the keyboard focus, and it keeps its place when hidden. A marker can sit beside its button on a `Canvas` or inside the button, but it cannot be the element or contain it: hidden, it would keep the focus from the element for good, so the load refuses it (ADR-0082).

A button reaches a command with its existing XAML property, also shown in the screen editor's inspector:

```xml
<Button Name="btnPlay" CommandName="Play" Content="Play" />
```

A button with no `CommandName` is legal and does nothing.

## Actions

A command runs its actions in order, on the `Update` that follows the click or the Enter key. A list has no conditions, and the only thing it can wait for is a screen fade.

| `type` | `target` | Effect |
|---|---|---|
| `open_screen` | Screen asset name or id | Opens a new screen on top of the others |
| `close_screen` | None | Closes the screen running the action |
| `change_world` | World file, as written in the catalogue | Loads that world. Its screens close with the old view |
| `play_sound` | Sound asset name or id | Plays the sound. It goes on after the screen closes |
| `quit` | None | Quits the game |
| `fade_screen` | None | Starts a screen fade. See [Fades](#fades) |
| `wait_fade` | None | Holds the rest of the list until no fade runs |

## Fades

```json
{
  "name": "Start",
  "actions": [
    { "type": "fade_screen", "layer": "AboveUI", "blend_mode": "Subtractive", "from": [ 0, 0, 0 ], "to": [ 255, 255, 255 ], "duration_seconds": 1.5 },
    { "type": "wait_fade" },
    { "type": "change_world", "target": "BlackWorld.world" }
  ]
}
```

`fade_screen` drives the service described in [screen-effects.md](screen-effects.md). All its fields are required, and none has a default:

- `layer`: `AboveUI` darkens the UI with the world, `BelowUI` leaves the UI on top.
- `blend_mode`: `Opaque`, `AlphaBlend`, `Additive` or `Subtractive`.
- `from`, `to`: `[r, g, b]` or `[r, g, b, a]`, whole numbers from 0 to 255. Without the fourth value the alpha is 255. A subtractive fade to black goes from `[0, 0, 0]`, which changes nothing on screen, to `[255, 255, 255]`. An alpha fade goes from `[0, 0, 0, 0]` to `[0, 0, 0, 255]` with `AlphaBlend`.
- `duration_seconds`: zero or more. Zero applies `to` at once, and a `wait_fade` after it does not wait.

`wait_fade` waits for any fade of the service, not only the one of its own list. With no fade running it goes straight on.

Only `play_sound` and `fade_screen` can come before a `wait_fade`, along with another `wait_fade`. What comes after it is free. The other actions take the screen out of the loop that would resume the list, so the file is refused rather than the list dying with no sign.

While a list waits:

- no command runs: a second confirm, a Cancel and a focus move are dropped;
- the keyboard and the gamepad do nothing, and the focus stays where it is, for as long as the waiting screen is at the top;
- a key or a button held through the wait fires nothing when the wait ends, until it is released, with one exception listed in [Limits](#limits) for a change of world;
- a screen that closes drops the rest of its list, a change of world from code included.

The frames, counting the frame of `fade_screen` as the first: the service updates after the UI, so the first step of the fade shows on that frame. The list goes on during the frame after the one that reached `to`, so that frame is drawn at `to` before `change_world` runs. The first frame of the next world still shows the overlay at `to`.

A fade started when the world starts, by `on_open_command` on the first screen for instance, counts one frame later, because a world's time starts at its first frame (ADR-0082): the first frame shows `from`, the second shows the first step, and a fade of N steps reaches `to` on frame N + 1. Under a fixed step, 17 steps of 15 draw 255, 240, then 15 by 15 down to 0 on frame 18.

Nothing of what the world took to start is replayed: without that, the 30 Updates MonoGame runs after a load are enough to end a fade of 17 frames before the first frame is drawn. A late frame later on is not protected, see [screen-effects.md](screen-effects.md), section 5.2.

A world used with a fade above the UI declares an entity with a `Camera2dComponent`, like `Projects/PixelUiSample/TitleWorld.world`: the veil is only placed for that camera. A world with no camera gets a 3D one. The sample without its camera was still fully covered at 960x540, but that is one measure and nothing guarantees it (ADR-0033).

A step of exactly so much per frame is described in [screen-effects.md](screen-effects.md), section 5.2.

## Subtractive images

An `Image` takes a `BlendType` (MGUI's ADR-0024). `Subtractive` takes the color of each texel from what is below it, saturates at 0, and leaves the destination alpha alone. The attribute is optional, and left out the image draws as before.

```xml
<OverlayPanel>
  <Image SourceName="cursor-body" Stretch="None" />
  <Image SourceName="cursor-shade" Stretch="None" BlendType="Subtractive" />
</OverlayPanel>
```

`Subtractive` ignores the alpha of the texture: every texel with a color subtracts, whatever its alpha. So an image that mixes opaque and subtractive texels is two sprites stacked as above, each with the other's texels transparent black. In `Projects/PixelUiSample`, a shade of 112 on a background of (208, 176, 128) gives (96, 64, 16) exactly, under a window scaled by 3.

With a `BlendType` other than `AlphaBlend`, a `Brightness` above 1 draws as at 1.

## Pixel screens

A project can write its screens in design pixels and have them follow the window at a whole factor. It sets `ScaleUI` on its virtual resolution (ADR-0048). The key is optional, and left out nothing changes:

```json
"VirtualResolution": { "Width": 320, "Height": 180, "Mode": "IntegerFit", "ScaleUI": true }
```

The factor `k` is the one of the virtual resolution: the largest whole number that fits, and at least 1. It is derived from the window and never written in data. Every data-driven screen then:

- sets its window's `Scale` to `k`, and sets it again when the window changes size;
- reads `Left` and `Top` in the XAML as design pixels and places the window at `Left * k`, `Top * k`. A `Scale` in the XAML is overwritten.

Everything else in the XAML is in design pixels too, and the mouse follows. A window under `k` has its sizes, its fonts and its 1-pixel borders drawn as blocks of `k` by `k` pixels:

```xml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        Left="40" Top="20" Width="240" Height="140" WindowStyle="None" IsCloseButtonVisible="False" CanCloseWindow="False">
```

A window placed by alignment, with `ScreenHorizontalAlignment` or `ScreenVerticalAlignment`, is refused under this scale: declare its `Left` and `Top`.

### Bitmap fonts

A screen names its fonts in `fonts`, by catalogue name or id, and a text element names the `face` of the `.fnt` in its `FontFamily`:

```xml
<TextBlock FontFamily="pixel-block" Foreground="White" Text="PIXEL UI SAMPLE" />
```

The screen takes its holds before its XAML loads and gives them back when it closes, through the game's `UIFontRegistry` (ADR-0036). Catalogue the `.fnt` with the type `fnt`, and its page PNG next to it: a `.fnt` names its pages relative to itself. A bitmap font keeps its own size, so `FontSize` has no effect on it.

Things measured on `Projects/PixelUiSample`, in the Launcher:

- A `TextBlock` has a default padding of 1 design pixel, which moves the text by `k` pixels. Set `Padding="0"` to place text exactly.
- The `lineHeight` of the `.fnt` is the height of a line, and a glyph with no `yoffset` starts at the top of it.
- Each button of the sample holds a `TextBlock` that names the font. A button whose text is its `Content`, or a font set by a style, has not been tried.

## Navigation

Without any of this, MGUI reads the keyboard and the gamepad with its fixed tables: arrows, Enter, Space, Escape, the D-pad, A and B.

### The input mapping

A project names a `.buttonsMapping` asset, by catalogue name or id. The key is optional, and left out nothing changes:

```json
"UIInputMapping": "ui-input"
```

The UI then reads that file instead of MGUI's tables. An entry whose name is that of a UI action of `InputAction` binds that action. Any other entry is left alone, so one file can serve gameplay as well.

```json
{
  "id": "90f95427-c427-4c9a-bc21-a357ece175cd",
  "name": "ui-input",
  "buttons": [
    {
      "id": "d589f4f5-9c2a-4ca1-9d38-bf8fd787dbe2",
      "name": "Submit",
      "game_pad_number": "One",
      "button_behavior": "DigitalInput",
      "analog_axis": "MouseX",
      "invert": false,
      "dead_zone": 0.75,
      "key_button": { "key": "Z", "game_pad_button": "None", "mouse_button": "LeftButton", "input_device": "Keyboard" },
      "alternative_key_button": { "key": "None", "game_pad_button": "A", "mouse_button": "LeftButton", "input_device": "GamePad" }
    }
  ],
  "repeat_initial_delay_ms": 300,
  "repeat_interval_ms": 150
}
```

- `Submit`, `Cancel`, `NavigateUp`, `NavigateDown`, `NavigateLeft` and `NavigateRight` are required. The other UI actions, such as `NavigateNext` or `OpenContext`, are optional.
- An entry has two inputs, `key_button` and `alternative_key_button`, usually a key and a gamepad button.
- Tab, Home, End, PageUp and PageDown keep their usual meaning when the mapping does not bind them, Shift+Tab included.
- While an editable text box has the focus, a key MGUI's tables do not know stays text: a letter bound to Cancel does not cancel. A key the focused text box reserves stays its own too: a space bound to Cancel is a space.

### Repeat

A direction moves when it is pressed, then repeats. `repeat_initial_delay_ms` is the wait before the first repeat, 350 by default, and `repeat_interval_ms` the time between two repeats, 120 by default. Each is written back only when set.

Everything else fires once per press, so a held confirm does not run a command again.

There is one timer for all inputs, started again whenever the set of held inputs changes. Pressing or releasing anything during a hold, the second input of the same action included, delays the next repeat by the full initial delay.

Repeat follows the game time, checked once a frame, so a real interval is rounded up to a whole number of frames.

### What a screen declares

```json
"default_focus": "btnStart",
"focus_wrap": "vertical",
"cancel_command": "Back",
"move_command": "Move",
"ui_commands": [
  { "name": "Back", "actions": [ { "type": "close_screen" } ] },
  { "name": "Move", "actions": [ { "type": "play_sound", "target": "cursor_move" } ] }
]
```

- `focus_wrap`, `vertical` or `horizontal`: the first and the last focusable element are joined, up and down or left and right. The order is MGUI's: `TabIndex`, then top, then left. It works with or without a mapping.
- `cancel_command`: the entry of `ui_commands` that runs when Cancel reaches the screen at the top of the stack and nothing consumed it. An open combo box closes first. It needs a project with a `UIInputMapping`.
- `move_command`: the entry of `ui_commands` that runs when the keyboard or the gamepad moves the focus between two elements of this window, a wrap included. A move that stops at an edge moves nothing, so it runs nothing. It does not run for a default focus, for a focus that arrives from another screen, or in pointer mode. It works with or without a mapping.

A command keeps its usual rules: the top of the stack only, run in `Update`, one at a time. A click, a Submit or a Cancel already waiting on that frame takes precedence over a move, which is then dropped.

## Errors

Invalid data throws an `InvalidOperationException` when the screen opens, naming the envelope and the problem:

- a malformed field, or an action of an unknown type;
- a missing target, or a target on `close_screen` or `quit`;
- a screen, world or sound the project does not have, or a target of another asset type;
- a command with no name, or declared twice;
- a `default_focus` that names nothing, or an element that cannot take the focus;
- a button whose `CommandName` resolves to no command;
- a font the project does not have, an asset of another type, or a font that cannot be loaded, such as one whose page is missing;
- a window placed by alignment under a design resolution;
- a `focus_wrap` that is neither `vertical` nor `horizontal`;
- a `cancel_command` or a `move_command` that names no entry of `ui_commands`;
- a `cancel_command` in a project with no `UIInputMapping`;
- a `fade_screen` with a field missing or malformed, a negative or endless `duration_seconds`, or a target;
- a `wait_fade` with a target, or after anything other than `play_sound`, `fade_screen` or `wait_fade`.

An unknown action type stays in the asset with its JSON, so saving an envelope keeps it.

A project's mapping stops the game when it loads, for one of these reasons:

- `UIInputMapping` names no `.buttonsMapping` of the catalogue, or the file cannot be read;
- a required action is missing, or binds no input;
- a UI action is an analog input, is declared twice, even with an entry that binds no input, binds a mouse button, or reads a gamepad other than the first;
- `repeat_initial_delay_ms` is negative, or `repeat_interval_ms` is not positive.

## Limits

- Actions are edited by hand in the JSON: the editor has no panel for them.
- A world does not open a screen when it loads. `FirstScreenName` runs once.
- A marker of `focus_markers` jumps with the focus, nothing slides it, and it stays hidden until a focus arrives. With `default_focus`, that is the third `Update` after the load. Without it, a first screen gets no focus before the first key or gamepad press or a click: MGUI's input mode starts as Pointer, and MGUI gives no focus in that mode.
- Without a mapping, a button held through a `change_world` counts as a new press on the next world: the world builds a new `UIRoot`, whose input tracker starts from nothing. Reaching this needs a screen pushed by code on that world, because `FirstScreenName` runs once. A mapping ignores a held input at its creation.
- No action removes a fade. Its overlay outlives `change_world` and stays on the last value, so a list that fades out ends on a world that stays hidden. `Projects/PixelUiSample` goes to a black world for that reason. `layer` also stays on the service.
- The mouse is not blocked during a wait: hover goes on, and a click can move the focus. Commands stay refused.
- A focused control that reads keys itself, such as a text box, still sees them during a wait: only navigation stops. This comes from reading the code and has not been run.
- During a wait, a screen pushed by code on top of the waiting one gets the keyboard and the gamepad at once, with or without a mapping. The waiting screen is no longer protected meanwhile, and its commands stay refused.
- A modal screen pushed that way freezes the waiting one: its list goes on only once that screen has closed, however long ago the fade ended. Pushing one needs code, so this is proved on a real `ScreenStack` in a test, not in the Launcher.
- A wait has not been tried with a real gamepad: both paths are proved on injected states.
- The focus is given once. When a screen on top closes, MGUI chooses it: in the sample it returned to the button that opened the popup.
- A click on a screen under another one is dropped, even when that screen is not modal. It still raises the window, which can hide a non-modal screen above it. It also moves the keyboard focus, and only the mouse can then reach the screen on top.
- Closing a window with MGUI's close button does not close its screen. Set `IsCloseButtonVisible="False"` on the window: `CanCloseWindow="False"` alone leaves the button shown.
- A window's exit effect does not show: the window is removed at once.
- A screen with a button takes the keyboard from the game. Unless a control is hovered, MGUI's auto-focus gives the focus to its first navigable control at the first key press, or at once if the last input was a key, and `PlayerInput` then stops reading the keyboard. These screens are menus: do not use one with a button as a HUD over a playable world.
- A keyboard press is ignored while the game window is inactive.
- Gamepad input has not been tested on a real gamepad. Repeat was proved on injected states, and on a held key in the Launcher.
- `focus_wrap` joins the first and the last element. It covers one row or one column, not a grid.
- `focus_wrap` takes the list once, at the first frame the focus is in the window. An element shown or enabled later is not in it, and an end disabled later stops wrapping there.
- Without `default_focus`, the first key gives the focus, so that key does not wrap yet.
- A `ListBox` does not wrap: the control clamps its own index, and `focus_wrap` only joins elements.
- A move that comes from nothing runs no `move_command`: without `default_focus`, the first key is silent.
- The Apps key, and gamepad buttons the mapping does not name, do nothing under a mapping. X and the shoulders are examples.
- Under a mapping, Ctrl+Tab does not exist, and a focused control that reads a key itself sees the key as well.
- Without a mapping, a held A repeats Submit, so a held button can run a command again, except across a wait. That is MGUI's reading, and a mapping is what changes it. It is proved on injected states, not on a real gamepad.
- The threshold of a stick direction such as `LeftThumbstickUp` comes from MonoGame's `GamePadState.IsButtonDown`, and has not been read.
- Nothing edits a `.buttonsMapping` for now: it is written by hand.
- A mistyped `FontFamily` falls back to the default font without a message: MGUI does not report it, and the layout moves with the wider font. Telling it apart needs a change in MGUI.
- A project has one `VirtualResolution`, so all its screens share one design resolution.
- `ScaleUI` scales the window of a data-driven screen only. A screen with its own class, and the view's `UIScale`, do not change.
- In a window smaller than the design resolution, `k` is 1 and the view shows the center of the image, while a screen draws from the view's top-left corner, so the right and the bottom of the screen are cut.
- A window moved by a drag goes back to `Left * k`, `Top * k` at the next change of size.
- Tooltips, context menus and windows nested in a XAML window have not been tried under this scale.
- The screen editor's preview takes no hold on `fonts`, so, from reading the code, a bitmap family would show the default font there. This has not been run.
