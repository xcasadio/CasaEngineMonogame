# Camera behaviour components

What moves a camera is a component placed on the entity of the camera (ADR-0148). The camera itself stays simple: a [`PerspectiveCameraComponent`](perspective-camera-component.md), posed by its transform. A behaviour component writes that transform.

## OrbitCameraComponent

Makes the camera orbit a target, without roll.

- `Target`, `Distance`, `Yaw`, `Pitch`: the values and conventions of the arc ball camera it replaced (removed, ADR-0149). The camera stands at `Target - Direction * Distance` with the orientation `AxisAngle(Up, -Yaw) * AxisAngle(Right, Pitch)`; `Direction` is (sin Yaw cos Pitch, sin Pitch, -cos Yaw cos Pitch).
- `SetCamera(position, target)` deduces the distance, the yaw and the pitch from two points. A position equal to the target only moves the target.
- `Direction` and `Right` give the axes of the camera.
- It writes the local transform of the camera of its entity at each write of one of its values, when its entity is initialised and when it joins a world. It does nothing per frame.
- **It owns the pose.** A pose written directly on the camera (`SetPositionAndTarget`, the transform) is overwritten at its next write. Frame an orbited camera through `SetCamera`.
- For the same values, the view equals the view the removed arc ball camera gave: `OrbitCameraComponentTests` checks it against the formulas of that class on 360 combinations.

```json
"root_component": { "type": "PerspectiveCameraComponent", "...": "..." },
"components": [
  {
    "name": "OrbitCameraComponent",
    "type": "OrbitCameraComponent",
    "target": { "x": 0, "y": 1, "z": 0 },
    "distance": 12,
    "yaw": 3.1415927,
    "pitch": -0.4
  }
]
```

The saved transform of the camera is replaced by the pose of the orbit when the entity is initialised.

### Navigation

`ViewportCameraState.From(orbit)` and `ApplyTo(orbit)` read and write the four values, so the viewport navigation (ADR-0089) drives an orbit. `ScriptArcBallCamera` keeps its name: on an entity that has an orbit component it drives the orbit and changes the field of view of the camera; on an entity with no orbit it does nothing (ADR-0149). The 3d viewport of the editor and the default camera of the demos are a posed camera, an orbit and, in the demos, this script.

## Follow2dCameraComponent

Makes the camera follow an entity in the XY plane. The targeted 2d camera it replaces (removed, ADR-0149) followed nothing; this component is specified anew.

- `target_entity_name`: the name of an entity of the world. `Target` can also be set by code.
- `dead_zone` (`x`, `y`): width and height of the zone, in world units, centred on the camera. While the target is inside, the camera does not move.
- `limits` (`x`, `y`, `w`, `h`, integers): the rectangle the x, y position of the camera stays in. A zero width leaves x free, a zero height leaves y free. Optional.
- At each update, on each axis, when the target is beyond the edge of the dead zone the camera advances by the excess; the position is then clamped to the limits. The z of the camera never changes.
- A name no entity bears is reported once; the camera then stays still. A destroyed target is dropped.

```json
{
  "name": "Follow2dCameraComponent",
  "type": "Follow2dCameraComponent",
  "target_entity_name": "hero",
  "dead_zone": { "x": 100, "y": 60 },
  "limits": { "x": -400, "y": -200, "w": 800, "h": 400 }
}
```

## Aim

There is no aim component. `PerspectiveCameraComponent.SetPositionAndTarget(position, target)` places a camera looking at a point; the previews of the editor, the default camera of a world and the demos that fix their camera use it.

## Limits

- Both components write the local transform of the camera and expect a camera with no parent.
- The follow component reads the world position of its target. It drives a `Camera2dComponent` as well as a perspective camera: both are posed by their transform (ADR-0150).
- No repository world uses the follow component: it is proven by its tests only.
- `ArcBallCameraComponent`, `CameraLookAtComponent` and `CameraTargeted2dComponent` are removed (ADR-0149); [perspective-camera-component.md](perspective-camera-component.md) shows how to migrate a file that names one.

## Tests

`OrbitCameraComponentTests`, `OrbitCameraPlacementTests`, `LegacyCameraEntityDataTests`, `ScriptArcBallCameraOrbitTests`, `Follow2dCameraComponentTests`, `EditorViewportCameraPoseTests`, `DemoCameraFramingTests`.
