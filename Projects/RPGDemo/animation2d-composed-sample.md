# Animation2D Composed Sample

`animation2d_composed_sample` is a sample entity kept in the RPGDemo assets to document the composed
animation format. It is **not** spawned by the game any more (it used to be spawned by
`ScriptWorld.OnBeginPlay`, which showed up as a large idle swordman in the middle of the map).

The entity uses `TileSets/swordman_composed_demo.anim2d`, a composed animation with two visible parts:

- `body`: existing swordman body sprite.
- `weapon`: existing sword sprite with position and draw-order tracks.

To see it, spawn it from a script (`world.SpawnEntity<Entity>("animation2d_composed_sample")`) and play
`swordman_composed_demo`; the `WeaponSwapLayer` event keyframe fires `AnimationEventTriggered` when crossed.
The asset is also loaded by `Animation2dAuthoringDataTests`.