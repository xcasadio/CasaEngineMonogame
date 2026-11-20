# ADR-0148: Camera behaviours are components that pose a simple camera

- **Status**: Accepted
- **Date**: 2026-11-19
- **Source**: this chantier: `ai-agent/tasks/simple-cameras-and-behaviours-tasks.md`, slice 2, decisions D10 to D13 and open points O9 and O10, approved by the author on 2026-11-19
- Renumbered from ADR-0145 on 2026-11-20 when origin/main was merged (its ADR-0145 is another decision)

## Context

ADR-0147 decided that what moves a camera leaves the camera classes. A survey of the repository on 2026-11-19 found:

- `ArcBallCameraComponent` (target, distance, yaw, pitch) is used by the viewport navigation, by `ScriptArcBallCamera`, by the 3d viewport of the editor, by the default camera of the demos and by two saved entities.
- `CameraLookAtComponent` only computes `CreateLookAt(Position, Target, Up)`. Its users (seven editor previews and thumbnail renderers, eight demos, the default camera of a world) call `SetPositionAndTarget` and nothing else.
- `CameraTargeted2dComponent` is named by no editor code, no demo, no project and no data file, and it follows nothing: it moves its offset only when the target is inside the dead zone, then tests whether the target left it, which is then always false.
- `EntityPolicyResolver` gives `DynamicDefault` to `CameraLookAtComponent` explicitly; a component it does not list leaves the builder untouched, and the builder falls back on `DynamicDefault`.

## Decision

- **Orbit.** `OrbitCameraComponent` is an `EntityComponent` placed on the entity of a camera. It holds `Target`, `Distance`, `Yaw` and `Pitch`, exposes `Direction`, `Right` and `SetCamera(position, target)`, and keeps the conventions and the JSON keys of `ArcBallCameraComponent` (`target`, `distance`, `yaw`, `pitch`). It writes the local transform of the camera component of its entity: position `Target - Direction * Distance`, orientation `AxisAngle(Up, -Yaw) * AxisAngle(Right, Pitch)`.
- **When it writes.** At each write of one of its properties, when its entity is initialised and when it joins a world. Nothing per frame. It keeps no reference to the camera and looks it up through its owner at each write.
- **It owns the pose.** `SetPositionAndTarget` called directly on a camera it drives is overwritten at its next write. A framing is asked of the orbit, through `SetCamera`.
- **No aim component.** The users of `CameraLookAtComponent` move to `PerspectiveCameraComponent.SetPositionAndTarget`, which gives the same view.
- **2d follow.** `Follow2dCameraComponent` is specified anew, since the old class has no working behaviour to preserve. It sits on the entity of a simple camera and reads `target_entity_name` (the name of an entity of the world), `dead_zone` (width and height in world units, centred on the camera) and `limits` (a rectangle of the world; a zero width or height means no limit). At each update, on each axis, when the target is beyond the edge of the dead zone the camera advances by the excess; its x, y position is then brought back inside the limits; its z does not change. A missing target is reported once and the component does nothing. It does not drive `Camera2dComponent`.
- **Script.** `ScriptArcBallCamera` keeps its name. It drives the orbit component of its entity when there is one, and an `ArcBallCameraComponent` otherwise.
- **Entity policy.** `EntityPolicyResolver` is not changed: an entity whose only policy-relevant component is a `PerspectiveCameraComponent` already resolves to `DynamicDefault`, the policy `CameraLookAtComponent` asked for.

## Consequences

- A world declares an orbiting camera with two components and no code; the navigation of the editor viewport and of the demos writes to the orbit.
- The old classes stay until slice 3. Data that names `ArcBallCameraComponent` still loads as before.
- An entity that mixed a `CameraLookAtComponent` with a component of a static policy relied on the explicit `DynamicDefault` of the camera; no such entity exists in the repository, and such an entity moved to the simple camera would follow the policy of its other component.
- `PerspectiveCameraComponent.SetPositionAndTarget` refuses a vertical aim, which `CameraLookAtComponent` accepted with whatever `Up` its orientation gave.
- The follow component has no user in the repository: it is proven by its tests only.
- Tests: `OrbitCameraComponentTests`, `Follow2dCameraComponentTests`.
