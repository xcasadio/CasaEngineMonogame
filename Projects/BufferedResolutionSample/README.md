# BufferedResolutionSample

A project with no gameplay DLL whose worlds each declare their own virtual resolution (ADR-0086). Each world shows it a different way in a window that never changes size:

- `TitleWorld.world`: 320x180 in `BufferedIntegerFit`. The world and its title screen render into a 320x180 target, shown at the largest whole factor with point sampling and black bands. The title screen lays out and takes clicks in virtual pixels.
- `FitWorld.world`: 200x150 in `BufferedFit`. The target is shown at the largest factor, whole or not, that keeps its proportions, with linear sampling and black bands.
- `DirectWorld.world`: no `virtual_resolution` block, so the project's 320x180 `IntegerFit` applies: drawn straight into the window at a whole factor, as before ADR-0086.

## Run

```bash
dotnet run --project CasaEngine.Launcher -- Projects/BufferedResolutionSample/BufferedResolutionSample.json
```

## Controls

- Mouse: click a button. Tab, Shift+Tab, arrow keys and Enter move the focus and press.
- `SMOOTH 200 X 150` loads `FitWorld`, `DIRECT 320 X 180` loads `DirectWorld`, `QUIT` closes the window.
- Resize the window: the image follows, and the target keeps its size.

A screen only opens with the first world (`FirstScreenName`), so the other two worlds show their background colour alone, with no way back: close the window to leave.

## What to look at

- `TitleWorld.world` and `FitWorld.world`: the `virtual_resolution` object, with `width`, `height` and `mode`.
- `BufferedResolutionSample.json`: the project's `VirtualResolution`, used by any world without a block.
- `Fonts/`: the font of `Projects/PixelUiSample`, copied as is.

The format is described in `docs/engine/` (virtual resolution).
