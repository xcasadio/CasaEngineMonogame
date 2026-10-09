# ADR-0074: The editor cameras have an infinite far plane

- **Status**: Accepted
- **Date**: 2026-10-09
- **Source**: this chantier: `ai-agent/tasks/archive/editor-infinite-view-distance-tasks.md`, decisions D1 to D4 (author answers, 2026-10-09), proposals P1 to P7, tasks T1.1 and T1.2

## Context

- The editor's 3D cameras kept the engine's default depth range: `CameraComponent.InitializeWithWorld` sets `MinDepth` 1 and `MaxDepth` 1000, the near and far planes, and nothing in the editor changed them. The model preview set its own far plane of 50. A large world, such as RacingGameCasaEngine's race track (about 1,500 units), was clipped in the world viewport. The edit camera could also move at most 1,000 units from its target.
- The author decided that the world viewport's edit camera and the asset preview cameras draw everything in front of them, that the edit camera may move up to 100,000 units from its target, that play-mode cameras keep their far plane, and that an editor factory, not a new engine camera property, sets this up.
- Measured on MonoGame: `Matrix.CreatePerspectiveFieldOfView(fov, aspect, near, float.PositiveInfinity)` returns the exact infinite perspective (`M33 = M34 = -1`, `M43 = -near`, `M44 = 0`). A `BoundingFrustum` built from it has a NaN far plane, but still culls with its five other planes, and its far corners are not finite.
- Three paths broke with a far plane at infinity:
  - the cubemap sky shader unprojected clip depth 1 and divided by `w`, which is 0 at infinity;
  - the picking rays (`RayHelper`, the gizmo) unprojected screen depths 0 and 1 and subtracted the two world points. `Viewport.Unproject` normalizes the depth by the viewport's `MinDepth`/`MaxDepth`, so the direction became NaN. The same subtraction of two points about 0.001 apart also lost precision with the camera far from the origin (0.88 error at 100,000 units);
  - the visible tile range of an axis-aligned tile map clipped the frustum edges to the map slab from its eight corners.

## Decision

- **Infinite far plane through the existing API.** `EditorCameraFactory` (`CasaEngine.Editor/Runtime`) creates and configures the editor's own 3D cameras: the world viewport's edit camera and the cameras of the model, material, sprite, particle and animation clip previews. It sets `FarPlane = float.PositiveInfinity`, after every `InitializeWithWorld`, since the initialization resets the depth range. No camera property is added to the engine.
- **Edit camera distance.** `EditorViewportCameraController` allows up to 100,000 units from the target, for the wheel and for framing.
- **Picking rays in view space.** `RayHelper.CalculateRayFromScreenCoordinate`, and `GizmoRay.FromScreen` for the gizmo (GizmoTool does not reference CasaEngine), unproject clip depths 0 and 0.5 through the inverse projection in view space, then move the origin and the direction to world space through the inverse view. They read the viewport position and size only, not its depth range. They stay defined without a far plane, keep their precision far from the origin (at most about 1e-7 measured), and work for perspective and orthographic projections.
- **Sky.** `SkyCubemap.fx` computes `normalize(worldPosition.xyz - EyePosition * worldPosition.w)`: the same direction as before for any finite projection, and still defined at infinity.
- **Tile maps.** `TileMapComponent.TryGetFrustumSlabBounds` returns false when a frustum corner is not finite: the caller keeps the full tile range, as when the frustum misses the slab.

## Consequences

- In the editor, a world is drawn to the horizon in the world viewport and in the previews. Frustum culling by planes still applies.
- The sun of the procedural sky (`SkyBackgroundViewPipeline`, 2,048 units from the camera) is now drawn in a view with an infinite far plane, where a far plane of 1,000 rejected it.
- Picking is more precise for every camera, not only the editor ones.
- With an infinite far plane, an axis-aligned tile map is not culled tile by tile any more; entity-level culling still applies. Restoring it would need a finite frustum for that computation only.
- Play-mode and runtime cameras keep their far plane: nothing in the runtime sets an infinite one.
- A scene camera set to an infinite far plane by hand (for example in the inspector) would have an infinite `ViewDistance` and `max_depth`; saving a non-finite value is not supported. The editor cameras are never saved.
- Depth precision depends on the near plane: within the first 1,000 units, it is the same as with a far plane of 1,000.
- The thumbnail cameras of the content browser are unchanged.
