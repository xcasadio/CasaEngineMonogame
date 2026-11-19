# ADR-0150: The 2d camera is posed by its transform

- **Status**: Accepted
- **Date**: 2026-11-19
- **Source**: this chantier: `ai-agent/tasks/simple-cameras-and-behaviours-tasks.md`, slice 4, decision D16 and open point O11, answers of the author on 2026-11-19; ADR-0008 for the compatibility posture; ADR-0147 for the 3d camera

## Context

ADR-0147 made the 3d camera a simple camera: its pose is the transform of its component. `Camera2dComponent` was already simple, but it placed itself from a `Target` property and a `target` key: its view was `CreateLookAt(target + (0, 0, 500), target, Up)`, and the transform saved with it had no effect on the view. A survey of the repository on 2026-11-19 found 14 worlds with a 2d camera, all with a zero target, a zoom of 1 and no pixel snap, each one the root component of an entity with no parent.

## Decision

- **Pose.** The view of `Camera2dComponent` is the inverse of `SceneComponent.WorldMatrixNoScale`, parents included, like `PerspectiveCameraComponent`. It looks down its local -Z axis; a roll turns the picture. `Target` is removed.
- **Pixel snap.** When `PixelSnap` is true, the x and y of the world translation are rounded to the `1 / Zoom` grid for the computation of the view. The saved transform is not modified.
- **Default distance.** `Camera2dComponent.DefaultDistance` is 500, the distance the camera kept from its target. A camera created in code starts at (0, 0, 500): it sees the plane z = 0 as it did with a zero target.
- **Framing in code.** `CentreOn(point)` places the camera at (point.X, point.Y, point.Z + `DefaultDistance`), which is what a write of `Target` did. `SetPositionAndTarget(position, target)` calls `CentreOn(target)`.
- **Data.** The camera no longer reads `target`; the editor no longer writes it. The pose comes from `coordinates` (or `local_transform`). The 14 worlds of the repository are migrated once (ADR-0008): their camera position becomes (0, 0, 500) and the `target` key is removed.
- **A `target` key left in a file** is reported once per load in the log, with the name and the id of the component, and ignored: the pose is read from the transform. No exception, so that an old world still opens in the editor.
- **Screen effects.** The below-UI effect quad is centred on the world position of the 2d camera, at the depth z - `DefaultDistance`: the values it had for a camera with no rotation.
- **Follow.** `Follow2dCameraComponent` writes the local position of the camera of its entity, so it now also drives a `Camera2dComponent`.

## Consequences

- **Public API break**, asked for by the author: `Camera2dComponent.Target`. Code that wrote it calls `CentreOn`.
- **A world that was not migrated** shows what its saved transform gives, which is not what it showed before, and logs the `target` key. To migrate it by hand: write (target.x, target.y, target.z + 500) in the position of the camera, remove `target`.
- A 2d camera can be parented, rotated and moved by any component that writes a transform, without code of its own.
- The projection (`Zoom`, viewport size) does not change.
- Tests: `Camera2dComponentTests`, `Camera2dWorldDataTests`, `Follow2dCameraComponentTests`.
