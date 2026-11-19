# SpriteCutsceneSample

A project with no gameplay DLL that drives sprites from a cutscene, from data alone (ADR-0129). `SpriteWorld.world` starts `Cutscenes/loop.cutscene` through its `startup_cutscene_asset_id` key. The project's `FirstScreenName` is empty: there is no screen.

## Objective

Show the sprite actions, `Repeat` and `ExitOnInput` with no code of the game:

1. `SpriteWorld.world` holds a 2D camera (320x180 design resolution), a light solid clear colour (`environment.background_color`), and four sprite entities hidden at load (`is_visible` false): `orb-left`, `orb-right`, `ring-left` and `ring-right`. None has a `DepthSortable2DComponent`, so they keep their order by Z. The light clear colour is what the blends act on: a subtractive sprite over a dark background shows nothing. There is no backdrop sprite, because the renderer draws the larger Z first and every sprite writes depth, so a sprite behind the others would be drawn last and fail the depth test under them.
2. `Cutscenes/loop.cutscene` arms `ExitOnInput` (mapping `sample-input`, input `exit`, next cutscene `SpriteExit`), then runs an endless `Repeat`. Each turn: `ShowSprite` puts the four sprites off screen at half their final tint, in additive (the orbs) or subtractive (the rings) mode, so they are visible while they move; `MoveSprite` brings them in from the edges at 120 pixels per second; `TintSprite` lights them up to their full tint, then turns them black, which makes them vanish in both modes; `HideSprite` hides them; `Wait` spaces the steps.
3. `Cutscenes/exit.cutscene` (the next cutscene of the exit) runs a `FadeScreen` to black, then a `ChangeWorld` to `EndWorld.world`.
4. `EndWorld.world` is a black world whose startup cutscene (`Cutscenes/end.cutscene`) waits 2 seconds, clears the exit overlay (a subtractive fade to black changes nothing), then changes back to `SpriteWorld.world`, so the sample runs for ever.
5. `Images/shapes.png` (an orb and a ring, 48x48 each) is generated, no external image is involved. The generator is `CasaEngine.Tests/Cutscenes/SpriteCutsceneSampleImages.cs`, and `SpriteCutsceneSampleDataTests` checks that the committed pixels equal its output. Two of the four entities name their sprite in the world, the other two get it from the `sprite` field of `ShowSprite`, so both ways are used.
6. `Cutscenes/stagger.cutscene` is not started by any world: open it in the editor. It shows the four sprites, then runs a `Parallel` of four `At` actions (0, 0.4, 0.8 and 1.2 seconds), each holding the `MoveSprite` of one sprite, so the sprites leave one after the other. In the cutscene timeline each `At` places its move at its own time; the `Seconds` field of the `At` in the Inspector sets that time.
7. `LiveWorld.world` and `Cutscenes/live.cutscene` show a world that lives in the cutscene editor preview. `LiveWorld.world` names `live.cutscene` through its `startup_cutscene_asset_id`, so the preview of that cutscene opens on it. It holds five entities besides the camera: `AnimatedOrb`, an `AnimatedSpriteComponent` playing `Animations/OrbPulse.anim2d` (a looping animation of 0.7 seconds, `orb` then `ring`, 0.35 seconds each); `Sparks`, a `ParticleSystemComponent` of `Particles/Sparks.particle` (continuous emission, a looping emitter, particles that live 2.5 to 3.5 seconds); `FallingBox`, a root `StaticSpriteComponent` with a dynamic `CollisionComponent` as child at a null local pose, 50 pixels above `Floor`, a static `CollisionComponent` placed 76 pixels under the box so that the box is still falling after 3 seconds (the project sets no gravity, so the default `(0, -9.87, 0)` applies); and `Marker`, a hidden `StaticSpriteComponent` with the `ring` sprite. `live.cutscene` waits 0.5 second, shows `Marker`, moves it across the screen at 120 pixels per second, then waits 0.5 second: it names `Marker` and no other entity, so everything else in the world moves by itself.

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

- Orbs (additive) light up the clear colour; rings (subtractive) darken it with the complement of their tint.
- Sprites enter from the left and right edges in a straight line at constant speed, and tint in a straight line between two colours.
- The sprite coordinates are world pixels with Y pointing up, the camera centred on the origin: x in -160..160 and y in -90..90.
- `Cutscenes/loop.cutscene`: the `ExitOnInput` armed before the `Repeat`, and the actions of one turn.
- `AssetInfos.json`: worlds, cutscenes, mapping, images, sheets and sprites are catalogued by name; `mapping`, `next_cutscene`, `sprite` and `ChangeWorld.target` refer to them.
