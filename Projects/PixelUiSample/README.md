# PixelUiSample

A project with no gameplay DLL whose screens are written in design pixels and stay pixel-exact at any window size, with a bitmap font the screen declares (ADR-0079), and whose navigation comes from a buttons mapping (ADR-0080).

## Run

```bash
dotnet run --project CasaEngine.Launcher -- Projects/PixelUiSample/PixelUiSample.json
```

## Controls

- Arrow keys or D-pad: move the focus. Held, they repeat. The focus wraps at the ends: up and down on the title, left and right on the options.
- `Z` or gamepad A: confirm. Enter does nothing here: the mapping replaces it.
- `X` or gamepad B: cancel, on the options screen.
- Tab, Shift+Tab: move the focus. The mapping does not bind Tab, so it keeps working.
- Mouse: click a button.
- Resize the window: the screen takes the largest whole factor that fits, and the rest of the window is black.

## What to look at

- `PixelUiSample.json`: `VirtualResolution` declares 320x180, and `ScaleUI` makes the screens follow it. `DebugWidth` and `DebugHeight` set the first window size. `UIInputMapping` names the mapping.
- `ui-input.buttonsMapping`: the UI actions, each with a key and a gamepad button, and the repeat in milliseconds.
- `Screens/title.uiscreen`: `fonts` holds the font for the screen's lifetime, `focus_wrap` joins the ends, `move_command` plays a sound when the focus moves.
- `Screens/options.uiscreen`: a modal screen with `focus_wrap` on a row and a `cancel_command`.
- `Screens/title.xaml`: everything is in design pixels, including `Left` and `Top`. A text element names the font in its `FontFamily`, and a button holds a `TextBlock` to carry it.
- `Fonts/`: the font, drawn for this sample. `pixel-block.glyphs.txt` is its source, and `make-pixel-block.ps1` writes `pixel-block.png` and `pixel-block.fnt` from it. Lower-case letters reuse the capitals. It has no glyphs beyond those of the source file.
- `Audio/`: the sound comes from `Projects/SampleProject/Audio`.

The format is described in `docs/engine/data-driven-screens.md`.
