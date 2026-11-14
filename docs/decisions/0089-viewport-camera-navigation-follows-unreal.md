# ADR-0089: Viewport camera navigation follows Unreal's perspective viewport, through one shared runtime class

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/unreal-viewport-camera-tasks.md` (decisions D1 to D10 and proposals P1, P3 to P8, settled with the author on 2026-11-14), branch `chantier/unreal-viewport-camera`. Control scheme: Epic Games, [Viewport Controls, Unreal Engine 5.9](https://dev.epicgames.com/documentation/unreal-engine/viewport-controls-in-unreal-engine), read on 2026-11-14.

## Context

- The demos drive their `ArcBallCameraComponent` with `ScriptArcBallCamera`. Its wheel moves the orbit target along the view (`Target += Direction * step`), so after a wheel notch the right-drag orbit turns around a point that is no longer on the object.
- The editor's 3D viewport drives its own copy of yaw, pitch, distance and target in `EditorViewportCameraController` (middle drag orbits, Shift + middle drag pans, right drag orbits, wheel changes the distance, arrows move the target). The two controllers share no code.
- Editor selection happens on left-button press (`EditorViewportGizmoController`), Ctrl adds, Alt removes; gizmo modes are on 1/2/3/4.
- `Camera3dComponent` recomputes `FieldOfView` from the viewport aspect ratio in `InitializeWithWorld` and `OnScreenResized`. Some demos write the initial camera state straight into the component (`WorldSpaceUIDemo.InitializeCamera`).

## Decision

- **Control scheme.** The editor's perspective viewport and `ScriptArcBallCamera` use Unreal's perspective viewport controls: left drag = move forward/back and yaw; right drag = look around the eye; left + right drag or middle drag = pan; wheel = move forward/back by steps; right held + wheel = fly speed; right held + W/S/A/D (arrows, numpad 8/2/4/6) = fly, + E/Q (numpad 9/7, Page Up/Down) = up/down in world space, + R/F = up/down in local space, + Z/C (numpad 1/3) = wider/narrower field of view; Alt + left drag = orbit around the pivot; Alt + right drag = dolly toward the pivot; Alt + middle drag = track; F = frame the selection. The previous editor gestures are removed.
- **Wheel and pivot.** The wheel moves the eye toward the pivot without moving the pivot; once the distance reaches its minimum the pivot is pushed ahead of the eye.
- **One shared class.** The navigation is a pure runtime class, `ViewportCameraNavigation` (`CasaEngine`), that holds only the gesture in progress and the fly speed. It reads a `ViewportNavigationInput` struct, updates a `ViewportCameraState` struct (pivot, distance, yaw, pitch) and returns a `ViewportNavigationResult` struct (left click released, frame request, field-of-view delta). The editor depends on it; the runtime does not depend on the editor.
- **Click versus drag.** A left press becomes a camera drag once the pointer has moved 4 pixels; a release below that is a click.
- **State ownership.** In the demos the component is the source of truth: the script reads the state from it at the start of each update and writes it back at the end. In the editor `EditorViewportCameraController` stays the source (focus, play-mode capture and restore) and writes the component. The field of view is never in the navigation state: Z/C change `Camera3dComponent.FieldOfView` directly.
- **F.** F frames only when the right button is not held; with the right button held it moves down in local space. In the demos, which have no selection, F puts the pivot back on the world origin.
- **Editor selection (perspective only).** Selection happens on a left click release: a click replaces the selection, Ctrl or Shift + click adds to it, Alt no longer removes. A left press on a gizmo handle manipulates the gizmo and moves no camera. Per frame the viewport reads the gizmo's hovered axis, runs the camera with that flag, then passes the camera result to the gizmo controller, which selects.
- **Gizmo modes.** W = translate, E = rotate, R = scale, replacing 1/2/3, ignored while the right button or Ctrl is held; 4 stays the uniform scale.
- **Unchanged.** `ArcBallCameraComponent` keeps its name, API and serialized format. The editor's 2D camera and front-2D views keep their navigation and their press-time selection (Ctrl adds, Alt removes). `ScriptArcBallCamera` keeps its public properties; default speeds and sensitivities come from the previous code.

## Consequences

- The orbit pivot no longer drifts in the demos, and the editor and demos behave the same way from one tested implementation.
- The left button now carries both selection and camera in the editor's perspective view, so selection waits for the release.
- **Known limits.** The gizmo hover used at press time is one frame old. A Z/C field-of-view change is lost when the viewport is resized or re-initialised (the aspect-based value wins), and play mode does not save it.
- Deferred: marquee selection, Space and Q of Unreal, vertex snapping, pivot offset with the middle button, and user-editable mouse and camera settings (next chantier requested by the author: an editor settings panel saved in an editor JSON file).
