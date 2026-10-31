# PixelUiSample

A project with no gameplay DLL whose screen is written in design pixels and stays pixel-exact at any window size, with a bitmap font the screen declares (ADR-0079).

## Run

```bash
dotnet run --project CasaEngine.Launcher -- Projects/PixelUiSample/PixelUiSample.json
```

## Controls

- Mouse: click a button.
- Keyboard: the arrow keys move the focus, Enter activates the focused button.
- Resize the window: the screen takes the largest whole factor that fits, and the rest of the window is black.

## What to look at

- `PixelUiSample.json`: `VirtualResolution` declares 320x180, and `ScaleUI` makes the screens follow it. `DebugWidth` and `DebugHeight` set the first window size.
- `Screens/title.xaml`: everything is in design pixels, including `Left` and `Top`. A text element names the font in its `FontFamily`, and a button holds a `TextBlock` to carry it.
- `Screens/title.uiscreen`: `fonts` holds the font for the screen's lifetime.
- `Fonts/`: the font, drawn for this sample. `pixel-block.glyphs.txt` is its source, and `make-pixel-block.ps1` writes `pixel-block.png` and `pixel-block.fnt` from it. Lower-case letters reuse the capitals. It has no glyphs beyond those of the source file.

The format is described in `docs/engine/data-driven-screens.md`.
