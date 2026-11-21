# ADR-0151: A sprite is drawn with the world transform of its component

- **Status**: Accepted
- **Date**: 2026-11-21
- **Source**: `ai-agent/tasks/oriented-sprites-tasks.md`, decisions D1 to D5, answers of the author on 2026-11-21; ADR-0147 for the camera posed by its transform and its picture offset

## Context

The need, in generic terms: show a fixed picture larger than the frame under a perspective camera oriented freely, roll included, in several layers at different depths between which 3d objects pass, and scroll the whole with the picture offset of the camera (ADR-0147).

A survey of the repository on 2026-11-21 found:

- `StaticSpriteComponent.Submit` passed to the renderer a position, a scale and a rotation of zero written in the code. It never read its orientation. A sprite could not be turned, and a sprite under a turned parent ignored the turn.
- The bounds of the same sprite already used its full world matrix: the box followed the transform, the drawing did not.
- The sprite renderer already keeps one world matrix per sprite and already has a public overload that takes a world transform; the tiles of the tile maps use it.
- No existing mechanism covers the need: the layer components are created by code and are told a camera position by the game, the full-view overlays ignore the depth, and no world background mode takes a picture.
- Of the 20 sprites saved in the data of the repository, none has a rotation and none is the child of another component.

In other engines a sprite is a scene object like any other: it has a full transform and follows its parent; facing the camera is an option, not the rule.

## Decision

- **A static sprite is drawn with the world transform of its component**: position, orientation and scale, parents included (`SceneComponent.WorldMatrixWithScale`). The renderer receives the local quad of the sprite and that matrix.
- **Attaching a sprite to a camera is done by the transform hierarchy.** A sprite that is a child component of a camera follows the camera. At the local position (x, y, -d) it lies in the plane z = -d of the camera space and faces the camera, which looks down its local -z. There is no dedicated "picture attached to the camera" component.
- **The size is computed by the data.** At the distance d, one world unit covers `viewport height / (2 * d * tan(field of view / 2))` pixels; the scale that gives one pixel of the picture per pixel of the frame follows from it. The engine offers no help for it.
- **The renderer gains overloads** that take a world transform together with the blend mode, the draw order, the scissor rectangle and, optionally, a sort key. Additive: no existing signature changes.
- **Scope**: `StaticSpriteComponent`. `AnimatedSpriteComponent` and `VideoSpriteComponent` are not changed.

## Consequences

- **Unchanged for what exists.** For a sprite with no rotation, alone or under parents that carry a position only, the corners of the quad are the ones of before, to the rounding in x and y and exactly in z. The matrix itself is not identical bit for bit: the order of the operations changes, and its z scale is the z scale of the component instead of 1, with no effect on a quad whose local z is zero. The z of the translation is still the z of the component: the sort does not change.
- **Changed, on purpose.** A turned sprite is seen turned. A sprite under a turned or scaled parent follows that parent, as its bounds already did. The sort key of a `DepthSortable2DComponent` is still computed from the summed position.
- **A sprite carried by a dynamic body follows the rotation of that body.** A dynamic physics component writes the orientation of its parent at each frame; when that parent is a sprite, the rotation was ignored by the drawing before and is followed now. In a world whose simulation space is planar (`Planar2d`), a body whose angular factor is left at its default can only turn about z (`SimulationSpacePolicy.ApplyDefaultConstraints`): its sprite then turns in its own plane and stays visible, where it stayed upright before. In a world with no such constraint, a body that tips over turns its sprite in 3d and, once seen from behind, the sprite is no longer drawn. One entity of the repository has a sprite with a dynamic body as a child, in `Projects/SpriteCutsceneSample/LiveWorld.world`; the effect on it was not measured, no simulation was run. A sprite that must not turn takes a body whose `angular_factor` is zero on the axes to lock: the physics backend then locks those axes of the inertia of the body.
- **A sprite seen from behind is not drawn**: the renderer culls back faces, except for the free quads.
- **The sort of blended sprites uses the world z.** Under a turned camera, two blended sprites that are children of the camera can be sorted the wrong way round. Opaque sprites and sprites with a cut-out do not depend on the sort: the shader discards a transparent texel, which then writes no depth.
- **If the field of view changes at run time, the scale computed by the data does not follow.**
- The picture offset of the camera shifts the sprites with everything else: it is in the projection.
- Tests: `StaticSpriteWorldTransformTests`. Sample: `Projects/OrientedSpriteSample`.
