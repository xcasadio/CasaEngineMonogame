# ADR-0144: Cameras become simple, their behaviours become components

- **Status**: Accepted
- **Date**: 2026-11-19
- **Source**: this chantier: `ai-agent/tasks/simple-cameras-and-behaviours-tasks.md`, decisions D1, D3, D4, D7 and D8 and open points O2, O3 and O8, approved by the author on 2026-11-19

## Context

The engine has four concrete cameras. Three of them derive from the abstract `Camera3dComponent` and each one mixes a projection with a behaviour: `ArcBallCameraComponent` orbits a target from a distance, a yaw and a pitch, `CameraLookAtComponent` aims at a target set by code, `CameraTargeted2dComponent` follows an entity. Each computes its own view matrix from its own fields. None takes its pose from the transform of its component, so a world cannot declare a perspective camera at a given position with a given orientation, roll included, without code.

The projection of a 3d camera is always centred: a picture larger than the screen cannot be scrolled under a fixed point of view.

`CameraComponent.ViewMatrix` is not virtual and only recomputes the view when `_needToComputeViewMatrix` is set. A change of transform sets no camera flag, and a move of a parent entity raises nothing on the child component.

## Decision

- **Target.** The engine ends with one 3d camera and one 2d camera, both simple: the pose is the transform of the component, the projection comes from properties. What moves a camera (orbit, aim, follow) leaves the camera classes and becomes engine components, declared in a world without a game assembly; scripts come on top.
- **Three slices.** (1) the simple 3d camera, added beside the existing ones; (2) the behaviours moved to components, used by the editor, the demos and the samples; (3) the three old classes removed and the data that names them handled. Each slice ships alone.
- **Slice 1 adds a class and changes no existing camera.** `PerspectiveCameraComponent` derives from `Camera3dComponent`. It is the final name: once the three old classes are gone, `Camera3dComponent` has a single derived class and can be merged into it without changing the data.
- **View.** The view of `PerspectiveCameraComponent` is the inverse of `WorldMatrixNoScale`: the world transform of the component, parents included, without scale. The camera looks down its local -Z axis with +Y up, as `Matrix.CreateLookAt` does.
- **Refresh.** `CameraComponent` gains one extension point, `protected virtual bool IsViewOutOfDate()`, which returns `false` and is consulted by the `ViewMatrix` getter beside the existing flag. `PerspectiveCameraComponent` overrides it and compares `WorldMatrixNoScale` with the matrix of its last computation. No other camera overrides it.
- **Field of view.** The opening is the vertical angle of `Camera3dComponent.FieldOfView` (ADR-0095), key `fieldOfView`.
- **Picture offset.** `PerspectiveCameraComponent.PictureOffset` shifts the picture without moving the point of view (off-centre projection). It is a `Vector2` in fractions of the whole width and height of the viewport: a point drawn at pixel `p`, y down, is drawn at `p + (x * width, y * height)`. A positive x moves the picture to the right, a positive y moves it down.
- **Serialized form.** Key `picture_offset`, an object with `x` and `y`, written and read through `[SerializedProperty]` (ADR-0118). A missing key means (0, 0).

## Consequences

- A world can declare a posed perspective camera with no code; the existing cameras, their data and their users are unchanged by slice 1.
- A new component type name and a new key enter the saved data. Renaming either later needs a migration (AGENTS.md §9.7).
- Reading `ViewMatrix` on a `PerspectiveCameraComponent` composes its world matrix and compares two matrices at each read, without allocation.
- `Position` and `Orientation` of a component do not compose a parent the way `WorldMatrixNoScale` does; `SetPositionAndTarget` on the new camera therefore writes the local transform, and equals a look-at in world space only for a camera with no parent.
- Sprites stay in a plane parallel to XY: a fixed picture faces the camera only when the camera looks along Z. An orientable sprite is out of this chantier.
- No cutscene action drives the offset in this chantier.
- Slice 3 changes what saved data means for the three old classes; it needs its own decision on whether the loader translates them or the files are migrated.
- Tests: `PerspectiveCameraComponentTests`.
