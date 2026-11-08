# VideoSample

A project with no gameplay DLL that plays a synthetic MDEC clip from data alone (ADR-0089). `VideoWorld.world` starts a cutscene through its `startup_cutscene_asset_id` key. The project's `FirstScreenName` is empty: the only screen comes from the cutscene.

## Objective

Show the whole chain of the video feature with no code of the game:

1. `Cutscenes/play.cutscene` runs a `PlayVideo` action on `ClipScreen`, then a `ChangeWorld` action on `VideoWorld.world` itself, so the sample loops.
2. `Screens/clip.xaml` holds one `<Video SourceName="clip" IsSkippable="True" />`. `PlayVideo` opens the screen, waits until the video has ended or has been skipped, then removes the screen.
3. `Videos/clip.mdecvideo` is the clip: 160x112 pixels, 60 frames at 15 frames per second (4 seconds), no sound. It is generated, no game data is involved: moving diagonal luma bars, drifting chroma and a few AC coefficients. The generator is `CasaEngine.Tests/Video/VideoSampleClip.cs`, and `VideoSampleDataTests` checks that the committed file equals its output byte for byte.

## Controls

The project declares the `ui-input` mapping (`UIInputMapping`).

| Action | Keyboard | Game pad |
|---|---|---|
| Submit: skip the clip, then the sample loops | Z | A |
| Cancel: skip the clip | X | B |

The mouse is bound to nothing: `ui-input.buttonsMapping` gives each action a keyboard key and a game pad button only, so a click does not skip the clip. Close the window to leave.

## Run

```bash
dotnet run --project CasaEngine.Launcher -- Projects/VideoSample/VideoSample.json
```

## What to look at

- `Cutscenes/play.cutscene`: the `PlayVideo` and `ChangeWorld` actions.
- `Screens/clip.xaml`: the `Video` tag and its `IsSkippable` attribute.
- `AssetInfos.json`: the clip is catalogued with the `mdecvideo` asset type; `SourceName` is its catalogue name.
