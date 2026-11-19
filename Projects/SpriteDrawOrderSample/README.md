# SpriteDrawOrderSample

A project with no gameplay DLL that stacks sprites of equal depth in an order chosen by the data (ADR-0131). The project's `FirstScreenName` is empty: there is no screen, and no cutscene.

## Objective

Show the `draw_order` field of `StaticSpriteComponent` with no code of the game:

1. `DrawOrderWorld.world` holds a 2D camera (320x180 design resolution), a light solid clear colour, and three sprite entities that overlap, all at Z 0 and all in `AlphaBlend` mode: `disc-red` (x -24), `disc-green` (x 0) and `disc-blue` (x 24). None has a `DepthSortable2DComponent`.
2. The entities are listed red, green, blue in the `.world`, and their `draw_order` values are the reverse: 2, 1 and 0. The sprite of the smallest `draw_order` is drawn first, so the blue disc is drawn first, then the green one, then the red one on top. The stacking follows `draw_order`, not the order of the data.
3. `Images/discs.png` (a red, a green and a blue translucent disc, 48x48 each) is generated, no external image is involved. The generator is `CasaEngine.Tests/Cutscenes/SpriteDrawOrderSampleImages.cs`, and `SpriteDrawOrderSampleDataTests` checks that the committed pixels equal its output, and that the references and the `draw_order` values hang together.

## Run

From the repository root:

```bash
dotnet run --project CasaEngine.Launcher -- Projects/SpriteDrawOrderSample/SpriteDrawOrderSample.json
```

## What to look at

- The red disc covers the green one, which covers the blue one. The Z of the three sprites is the same, so only `draw_order` defines the stacking.
- Change a `draw_order` in `DrawOrderWorld.world` and reload: the stacking follows. With equal `draw_order` values, the order of submission of the frame decides.
- The sprite coordinates are world pixels with Y pointing up, the camera centred on the origin.
- `AssetInfos.json`: the world, the image, the sheet and the sprites are catalogued by name.
