# Oriented sprites

A `StaticSpriteComponent` is drawn with the world transform of its component: position, orientation and scale, parents included (ADR-0151). A turned sprite is seen turned, and a sprite follows what it is attached to.

## What a sprite follows

- Its own transform: `coordinates` (or `local_transform`) in the data, `LocalPosition`, `LocalOrientation` and `LocalScale` in code.
- The component it is a child of (`children_component` in the data, `AddChildComponent` in code), then the root component of the parent entity of its entity.
- The quad of the sprite is sized by its source rectangle and placed by its origin, in the local xy plane, facing the local +z.

For a sprite with no rotation, alone or under parents that carry a position only, nothing changed: the corners of the quad are the ones of before.

## Attaching a picture to a camera

There is no dedicated component: make the sprite a child component of the camera.

```json
"root_component": {
  "type": "PerspectiveCameraComponent",
  "fieldOfView": 0.7853982,
  "...": "...",
  "children_component": [
    {
      "type": "StaticSpriteComponent",
      "spriteDataName": "far",
      "coordinates": {
        "position": { "x": 0, "y": 0, "z": -600 },
        "scale": { "x": 1.035534, "y": 1.035534, "z": 1 },
        "rotation": { "x": 0, "y": 0, "z": 0, "w": 1 }
      },
      "...": "..."
    }
  ]
}
```

- The camera looks down its local -z: at the local position (x, y, -d) the sprite lies in the plane z = -d of the camera and faces it, whatever the orientation of the camera, roll included.
- Several pictures at several distances occlude one another and the 3d objects between them through the depth buffer. The sprite shader discards a transparent texel, which writes no depth: a cut-out needs no blending and does not depend on the draw order.
- The picture offset of the camera (`picture_offset`, ADR-0147) shifts the pictures with everything else: scrolling a picture larger than the frame is a write of that offset.
- **The camera must be the root component of its entity.** An entity draws its root component, which goes down into its children; a camera that is not the root does not draw its children.

## One pixel of the picture per pixel of the frame

At the distance d in front of a camera of vertical field of view `fov`, the frame is `2 * d * tan(fov / 2)` world units high. A sprite whose every picture pixel must cover one pixel of a frame `H` pixels high takes the scale

```text
scale = 2 * d * tan(fov / 2) / H
```

The scale is data: the engine offers no help for it, and it does not follow a field of view or a frame height that changes at run time.

## Visibility

The world draws an entity when its bounds meet the frustum. The bounds of an entity count its child components, and the bounds of a camera stand on the camera (ADR-0152): a picture attached to a camera is drawn wherever the camera stands. A child that is a physics component, and what hangs under it, do not count; what hangs under a root that is itself a physics component does.

## Limits

- **A sprite seen from behind is not drawn**: the renderer culls back faces.
- **A sprite carried by a dynamic body follows the rotation of that body**, since the body writes the orientation of its parent. In a planar simulation space the body only turns about z, and the sprite turns in its own plane; elsewhere a body that tips over turns its sprite in 3d, which can end up seen from behind. A zero in `angular_factor` locks the matching axis of the body.
- **Blended sprites are sorted by the z of the world.** Under a turned camera, two blended sprites attached to the camera can be sorted the wrong way round. Opaque sprites and cut-outs do not depend on the sort.
- `AnimatedSpriteComponent` and `VideoSpriteComponent` are not oriented: they still ignore the orientation of their component.
- The sort key of a `DepthSortable2DComponent` is still computed from the summed position of the sprite.
- The debug shapes of a sprite (border, origin, collisions) are flat: they take the place and the size the transform gives, not its rotation.

## Sample and tests

- `Projects/OrientedSpriteSample`: a tilted camera far from the origin, two pictures attached to it, a box between them, a turned sprite of the world; the arrow keys change the picture offset.
- `StaticSpriteWorldTransformTests` (corners queued on the renderer), `EntityBoundsWithChildrenTests` (visibility), `OrientedSpriteGpuTests` (pixels read back from a real device), `OrientedSpriteSampleDataTests`.
