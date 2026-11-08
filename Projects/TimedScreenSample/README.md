# TimedScreenSample

A project with no gameplay DLL whose two worlds chain timed screens from data alone (ADR-0088). Each world starts a cutscene when it begins to play, through the `startup_cutscene_asset_id` key of its `.world`. The project's `FirstScreenName` is empty: both screens come from the cutscenes.

Each cutscene is the same sequence, counted in frames (one frame is one `Game.Update`):

1. `FadeScreenFrames`, 0 frames: the screen goes opaque black at once, above the UI.
2. `OpenScreen`: the world's screen opens, under the black.
3. `FadeScreenFrames`, 30 frames: from opaque black to transparent black.
4. `WaitFrames`, 90 frames.
5. `FadeScreenFrames`, 30 frames: to opaque black.
6. `ChangeWorld`: the other world loads, and its own cutscene does the same. The sample loops.

- `FirstWorld.world`: 200x150 in `BufferedFit`. The fade is drawn above the UI of the buffered target.
- `SecondWorld.world`: no `virtual_resolution` block, so the project's 320x180 `IntegerFit` applies. The fade is drawn straight into the window.

## Run

```bash
dotnet run --project CasaEngine.Launcher -- Projects/TimedScreenSample/TimedScreenSample.json
```

Close the window to leave: there is no button.

## What to look at

- `FirstWorld.world` and `SecondWorld.world`: the `startup_cutscene_asset_id` key.
- `Cutscenes/`: the `WaitFrames`, `FadeScreenFrames`, `OpenScreen` and `ChangeWorld` actions. A `target` is a screen name for `OpenScreen` and a world file name for `ChangeWorld`.
- `Fonts/`: the font of `Projects/PixelUiSample`, copied as is.
