# Animation2D Composed Sample

This RPGDemo sample spawns `animation2d_composed_sample` when `DefaultWorld.world` begins play.

The entity uses `TileSets/swordman_composed_demo.anim2d`, a composed animation with two visible parts:

- `body`: existing swordman body sprite.
- `weapon`: existing sword sprite with position and draw-order tracks.

Run the RPGDemo project normally and enter `DefaultWorld.world`. The sample entity appears near the play area and writes `Animation2D composed sample event: WeaponSwapLayer` to the trace log when the event keyframe is crossed.