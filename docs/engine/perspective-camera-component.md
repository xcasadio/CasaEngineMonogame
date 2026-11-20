# PerspectiveCameraComponent

A perspective camera posed by the transform of its component (ADR-0144). A world declares it with no code: where the component is, the camera is; how the component is turned, the camera looks, roll included.

It is the first slice of the chantier "simple cameras, behaviours as components" (`ai-agent/tasks/simple-cameras-and-behaviours-tasks.md`). The other cameras (`ArcBallCameraComponent`, `CameraLookAtComponent`, `CameraTargeted2dComponent`, `Camera2dComponent`) are unchanged.

## View

- The view matrix is the inverse of `WorldMatrixNoScale`: the local transform of the component, the transform of its parent component, then the root transform of the parent entity. The scale is ignored.
- With no rotation the camera looks down -Z with +Y up, like `Matrix.CreateLookAt`.
- The view follows the transform with no call to `Update`: each read of `ViewMatrix` composes the world matrix and compares it with the one of the last computation (two matrices, no allocation). Moving a parent entity is seen the same way.
- `SetPositionAndTarget(position, target)` writes the local position and a local orientation aimed at the target with `Vector3.Up` for up, without roll. For a camera with no parent this equals `Matrix.CreateLookAt(position, target, Vector3.Up)`. Under a parent both values are in the parent's frame. A target at the position, or straight above or below it, throws `ArgumentException`.

## Projection

- `FieldOfView` is the vertical angle in radians, kept whatever the shape of the viewport (ADR-0095). Default: 45 degrees.
- `PictureOffset` (a `Vector2`) shifts the picture without moving the point of view; the projection becomes off-centre. It is in fractions of the whole width and height of the viewport: a point drawn at pixel `p`, y down, is drawn at `p + (X * width, Y * height)`. A positive X moves the picture to the right, a positive Y moves it down. It can be changed at any time; the projection is recomputed at the next read.
- With an offset of (0, 0) the projection is the one of every 3d camera, bit for bit.
- Measured on a 320x240 viewport: every offset from -320 to 320 whole pixels moves the picture by the number of pixels asked, to less than 0.01 pixel, at depths 2, 50 and 900.
- The culling frustum comes from the view and the projection, so it follows the offset.

## Data

```json
{
  "name": "PerspectiveCameraComponent",
  "type": "PerspectiveCameraComponent",
  "coordinates": {
    "position": { "x": 0, "y": 0, "z": 289.7056 },
    "scale": { "x": 1, "y": 1, "z": 1 },
    "rotation": { "x": 0, "y": 0, "z": 0, "w": 1 }
  },
  "children_component": [],
  "view_distance": 999,
  "viewport": { "x": 0, "y": 0, "w": 960, "h": 720, "min_depth": 1, "max_depth": 1000 },
  "fieldOfView": 0.7853982,
  "picture_offset": { "x": 0, "y": 0 }
}
```

- `picture_offset` is optional: a missing key means (0, 0). It is read and written through `[SerializedProperty]` (ADR-0118).
- `view_distance` and `viewport` are required, as for every camera. When the camera joins a world, the size of the viewport becomes the size of the screen and the depths become 1 and 1000, whatever the file says: a scene must fit between 1 and 1000 units from the camera.
- The editor offers the class in the Add Component dialog as "Perspective Camera", and its inspector shows the field of view and the two depths.

## Limits

- Sprites stay in a plane parallel to XY. A fixed picture faces the camera only when the camera looks along Z.
- No cutscene action drives the offset.
- The inspector does not show the offset: its generic section edits no `Vector2`. The offset is set in the file or by code.
- The offset shifts the whole picture: it is not a per-layer parallax.

## Sample

`Projects/PerspectiveCameraSample`: a backdrop wider than the window, a box standing on it, and the arrow keys scrolling the picture under a camera that never moves.

## What comes next

- Slice 2 (done, ADR-0145) moved the orbit and the 2d follow into components that write the transform of this camera: see [camera-behaviour-components.md](camera-behaviour-components.md).
- Slice 3 removes `ArcBallCameraComponent`, `CameraLookAtComponent` and `CameraTargeted2dComponent`, and decides how the data that names them is read. `PerspectiveCameraComponent` keeps its name and its keys.

## Tests

`PerspectiveCameraComponentTests`, `PerspectiveCameraSampleDataTests`.
