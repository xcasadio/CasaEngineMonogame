# ADR-0149: The old camera classes are removed and their data is migrated once

- **Status**: Accepted
- **Date**: 2026-11-19
- **Source**: this chantier: `ai-agent/tasks/simple-cameras-and-behaviours-tasks.md`, slice 3, decisions D14 and D15, answers of the author on 2026-11-19; ADR-0008 for the compatibility posture

## Context

ADR-0147 and ADR-0148 moved every user of the three old cameras to `PerspectiveCameraComponent`, alone or posed by an `OrbitCameraComponent`. A survey of the repository on 2026-11-19 found:

- Two data files still name an old camera: `CasaEngine.Demos/Content/EditorWorld/Entities/camera.entity` and `Projects/SampleProject/Entities/camera.entity`, both with an `ArcBallCameraComponent` as root component. No data names `CameraLookAtComponent` or `CameraTargeted2dComponent`.
- Outside the tests, `ArcBallCameraComponent` is only reached through two methods of `ViewportCameraState`, the path of `ScriptArcBallCamera` for an entity with no orbit, one case of the editor entity writer and one demo.
- `Camera3dComponent` is abstract and, once the three old classes are gone, has `PerspectiveCameraComponent` for only derived class. No data names it: an abstract type cannot be loaded.

Two rules of the repository pulled in opposite directions for the two data files: a serialized format does not change without a migration path, and ADR-0008 states that backward compatibility of serialized assets is not a goal ("replace, don't duplicate"; every phase removes what it replaces).

## Decision

- **Removal.** `ArcBallCameraComponent`, `CameraLookAtComponent` and `CameraTargeted2dComponent` are removed, with `ViewportCameraState.From(ArcBallCameraComponent)`, `ViewportCameraState.ApplyTo(ArcBallCameraComponent)`, the path of `ScriptArcBallCamera` for an entity with no orbit, the case of the editor entity writer and the two cases of `EntityPolicyResolver`. `ViewportCameraState` (ADR-0089) is now read from and written to an `OrbitCameraComponent` only.
- **Data is migrated once (ADR-0008).** The two files of the repository are rewritten; the loader receives no translation path. The migrated form is a `PerspectiveCameraComponent` root that keeps its `id`, `name`, `coordinates`, `view_distance`, `viewport` and `fieldOfView`, and an `OrbitCameraComponent` in `components` that receives `target`, `distance`, `yaw` and `pitch` unchanged.
- **Fusion.** `Camera3dComponent` is merged into `PerspectiveCameraComponent`, which now derives from `CameraComponent` and owns `FieldOfView`, `DefaultFieldOfView`, the perspective projection and the `fieldOfView` key. The type name written in the data, `PerspectiveCameraComponent`, does not change, nor do the keys or their order.
- `ScriptArcBallCamera` keeps its name (ADR-0148). On an entity with no orbit component it does nothing.

## Consequences

- **Public API breaks**, asked for by the author: the four classes and the two methods of `ViewportCameraState` listed above. Code that took a `Camera3dComponent` takes a `PerspectiveCameraComponent`.
- **A file that was not migrated.** A component whose `type` is one of the three old names is no longer resolved: it is reported in the log with its name and id and kept as an unresolved component (ADR-0119), so the entity still loads and saves without loss, but it has no camera.
- **Migrating a file by hand.** Change the `type` of the camera to `PerspectiveCameraComponent`, move `target`, `distance`, `yaw` and `pitch` to a new node of `components` with `"type": "OrbitCameraComponent"`, an `id` and a `name`. A `CameraLookAtComponent` becomes a `PerspectiveCameraComponent` whose `coordinates` hold its pose.
- The proofs that compared the new components with the old classes are kept: before the removal each one received a second reference that does not depend on the old class (the formulas of the old class written in the tests, or values recorded from it).
- Tests: `LegacyCameraEntityDataTests`, `OrbitCameraComponentTests`, `OrbitCameraPlacementTests`, `ScriptArcBallCameraOrbitTests`, `ViewportCameraNavigationTests`, `Camera3dFieldOfViewTests`.
