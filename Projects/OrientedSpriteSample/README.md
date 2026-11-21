# OrientedSpriteSample

A world that shows what a `StaticSpriteComponent` is drawn with: the full world transform of its component, position, orientation and scale, parents included (ADR-0151). A sprite that is a child component of a `PerspectiveCameraComponent` follows the camera and faces it.

## Objective

1. `camera` is an entity whose **root component** is a `PerspectiveCameraComponent`: vertical field of view of 45 degrees, no picture offset, posed at (800, 300, 650) with yaw, pitch and roll, so that no axis of the camera is an axis of the world. It is far from the origin of the world, which is outside its field of view (ADR-0152: the bounds of a camera and of its children stand on the camera).
2. `FarPicture` and `NearPicture` are two sprites in the `children_component` of the camera, at the local positions (0, 0, -600) and (0, 0, -240). They face the camera and follow it, whatever its orientation. Both are 960x720 pixels, larger than the 640x480 frame. `far.png` is a grid with markers; `near.png` is an opaque frame with a transparent cut-out in its middle (alpha 0, no blending: the sprite shader discards a transparent texel).
3. `box` is a 60 unit static model standing 400 units in front of the camera along its viewing axis, between the two pictures. It shows through the cut-out and hides part of the far picture.
4. `turned-sprite` is a sprite that is the root component of its own entity, with no link to the camera. Its transform holds a rotation, and it is drawn turned: the general case. It stands 330 units in front of the camera and is oriented to be seen from the front, in three quarter view.
5. `CasaEngine.PerspectiveCameraSample.PictureOffsetKeys` (the gameplay DLL of `Projects/CasaEngine.PerspectiveCameraSample`, reused: its post-build step copies the DLL next to this project file) writes `PictureOffset` of the camera from the arrow keys. The pictures, the box and the turned sprite all shift together: the offset is in the projection.
6. The images are generated, no external image is involved. The generator is `CasaEngine.Tests/Scene/OrientedSpriteSampleImages.cs`, and `OrientedSpriteSampleDataTests` checks that the committed pixels equal its output and that the data hangs together.

## Run

Build the solution first, so that the gameplay DLL is copied next to the project file, then, from the repository root:

```bash
dotnet build CasaEngine.MonoGame.sln
dotnet run --project CasaEngine.Launcher -- Projects/OrientedSpriteSample/OrientedSpriteSample.json
```

The project asks for a 640x480 window. The scale of the pictures is computed for that height (see below): resize the window and the pictures no longer cover one pixel per pixel.

## Controls

- Right / Left: move the picture to the right / to the left (`PictureOffset.X`, half a viewport per second, from -0.5 to 0.5).
- Down / Up: move the picture down / up (`PictureOffset.Y`).
- Space: no offset.

## What to look at

- With no key pressed, the frame shows the near picture (an orange checker) with the box and the turned sprite in its cut-out, and the far picture (a blue grid, with a white cross at its centre and a coloured square in each corner) behind them.
- The pictures are tilted on the screen like the camera, roll included: they are attached to it, so they never leave their place in the frame.
- The box is a world object: it shows several faces, since the camera looks at it from the side. The turned sprite shows a yellow arrow on a teal tile, seen in three quarter view; the red square marks its top-left corner.
- Hold an arrow key: everything slides together, and the box and the turned sprite stay at the same place against the pictures.

## How the scale of the pictures was computed

At the distance d in front of a camera whose vertical field of view is `fov`, the frame is `2 * d * tan(fov / 2)` world units high. To give one pixel of the picture per pixel of the frame, a sprite of the height of the frame in pixels (480) must cover exactly that, so every unit of the sprite (one picture pixel) is scaled by

```text
scale = 2 * d * tan(fov / 2) / frame height
```

With `fov` = 0.7853982 and a frame height of 480: d = 600 gives 1.035534 (far picture), d = 240 gives 0.4142136 (near picture). The origin of each sprite is the centre of its image (the hotspot), so that it is centred on the viewing axis. The scale is data, written once in `OrientedWorld.world`: the engine offers no help for it, and it does not follow a field of view that changes at run time or a frame of another height.

## Limits

- A sprite seen from behind is not drawn: the renderer culls the back faces. The turned sprite is oriented so that it is seen from the front.
- The camera must be the root component of its entity. The pictures hang below it; a camera that is not the root component does not draw its children.
- Blended sprites that are children of a turned camera can be sorted the wrong way round (the sort uses the z of the world). This sample uses opaque sprites and a cut-out, which do not depend on the sort.
- The pictures cover the frame only for small offsets: at an offset of 0.5 the edge of the pictures comes into view.
