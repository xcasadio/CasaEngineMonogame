# PerspectiveCameraSample

A world whose camera is a `PerspectiveCameraComponent` (ADR-0144): a perspective camera posed by the transform of its component, with a picture offset that scrolls the picture without moving the point of view.

## Objective

1. `CameraWorld.world` declares the camera with no code: its position is the transform of the component (0, 0, 289.7056), it looks down -Z with no rotation, and its vertical field of view is 45 degrees. At that distance the picture is 240 world units high on the plane Z = 0: one unit per pixel of the backdrop.
2. `backdrop` is a 640x240 sprite on the plane Z = 0, twice as wide as what a 4:3 window shows. Four coloured columns of 160 pixels tell how far the picture has scrolled.
3. `box` is a 30 unit box (a static model with an unlit material) that stands on the yellow square drawn in the backdrop, at (100, -40).
4. The camera entity names the script `CasaEngine.PerspectiveCameraSample.PictureOffsetKeys`, from the small gameplay DLL `Projects/CasaEngine.PerspectiveCameraSample`. The script only writes `PictureOffset` from the arrow keys: no data-only sample can read the keyboard to act on a component.
5. `Images/backdrop.png` and `Images/box.png` are generated, no external image is involved. The generator is `CasaEngine.Tests/Scene/PerspectiveCameraSampleImages.cs`, and `PerspectiveCameraSampleDataTests` checks that the committed pixels equal its output and that the data hangs together.

## Run

Build the solution first, so that the gameplay DLL is copied next to the project file, then, from the repository root:

```bash
dotnet build CasaEngine.MonoGame.sln
dotnet run --project CasaEngine.Launcher -- Projects/PerspectiveCameraSample/PerspectiveCameraSample.json
```

## Controls

- Right / Left: move the picture to the right / to the left (`PictureOffset.X`, half a viewport per second, from -0.5 to 0.5).
- Down / Up: move the picture down / up (`PictureOffset.Y`).
- Space: no offset.

## What to look at

- With no key pressed, the two middle columns of the backdrop fill a 4:3 window, and the box stands on the yellow square, right of the centre.
- Hold Left: the picture slides to the left and the two right columns come into view. The box slides with the backdrop and stays on the yellow square.
- The box keeps the same faces visible during the whole scroll: the point of view does not move, only the window through which the picture is seen. Moving the camera itself would turn the box and slide it against the backdrop.
- At an offset of 0.5 the point that was in the centre of the window is on its edge.
- Change `picture_offset` or `fieldOfView` in `CameraWorld.world` and reload: the framing follows. Add a rotation to the camera `coordinates` and the view turns, roll included; the backdrop sprite then no longer faces the camera, since sprites stay in a plane parallel to XY.
