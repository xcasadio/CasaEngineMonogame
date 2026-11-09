# SpriteCutsceneSample

A project with no gameplay DLL that drives sprites from a cutscene, from data alone (ADR-0090). `SpriteWorld.world` starts `Cutscenes/loop.cutscene` through its `startup_cutscene_asset_id` key. The project's `FirstScreenName` is empty: there is no screen.

## Objective

Show the sprite actions, `Repeat` and `ExitOnInput` with no code of the game:

1. `SpriteWorld.world` holds a 2D camera (320x180 design resolution), one visible opaque `backdrop` sprite, and four sprite entities hidden at load (`is_visible` false): `orb-left`, `orb-right`, `ring-left` and `ring-right`. None has a `DepthSortable2DComponent`, so they keep their order by Z. The backdrop is only there so that a subtractive sprite has a light picture to take colour from: a subtractive sprite over a dark background shows nothing.
2. `Cutscenes/loop.cutscene` arms `ExitOnInput` (mapping `sample-input`, input `exit`, next cutscene `SpriteExit`), then runs an endless `Repeat`. Each turn: `ShowSprite` puts the four sprites off screen, black, in additive (the orbs) or subtractive (the rings) mode; `MoveSprite` brings them in from the edges at 120 pixels per second; `TintSprite` lights them up, then turns them black again; `HideSprite` hides them; `Wait` spaces the steps.
3. `Cutscenes/exit.cutscene` (the next cutscene of the exit) runs a `FadeScreen` to black, then a `ChangeWorld` to `EndWorld.world`.
4. `EndWorld.world` is a black world whose startup cutscene (`Cutscenes/end.cutscene`) waits 2 seconds, then changes back to `SpriteWorld.world`, so the sample runs for ever.
5. `Images/shapes.png` (an orb and a ring, 48x48 each) and `Images/backdrop.png` (320x180) are generated, no external image is involved. The generator is `CasaEngine.Tests/Cutscenes/SpriteCutsceneSampleImages.cs`, and `SpriteCutsceneSampleDataTests` checks that the committed pixels equal its output. Two of the four entities name their sprite in the world, the other two get it from the `sprite` field of `ShowSprite`, so both ways are used.

## Controls

The `sample-input` mapping holds one input, `exit`.

| Action | Keyboard | Game pad |
|---|---|---|
| Leave: fade to black, then the end world | Space | Start |

A key held while the cutscene starts does not trigger the exit: release it, then press it again. After the end world the sample starts again. Close the window to quit.

## Run

From the repository root:

```bash
dotnet run --project CasaEngine.Launcher -- Projects/SpriteCutsceneSample/SpriteCutsceneSample.json
```

## What to look at

- Orbs (additive) light up the backdrop; rings (subtractive) darken it with the complement of their tint.
- Sprites enter from the left and right edges in a straight line at constant speed, and tint in a straight line between two colours.
- The sprite coordinates are world pixels with Y pointing up, the camera centred on the origin: x in -160..160 and y in -90..90.
- `Cutscenes/loop.cutscene`: the `ExitOnInput` armed before the `Repeat`, and the actions of one turn.
- `AssetInfos.json`: worlds, cutscenes, mapping, images, sheets and sprites are catalogued by name; `mapping`, `next_cutscene`, `sprite` and `ChangeWorld.target` refer to them.
