# Viewport camera controls

The perspective view of the editor's world viewport and the default camera of `CasaEngine.Demos` (`ScriptArcBallCamera`) share the controls of Unreal Engine's perspective viewport ([Viewport Controls, UE 5.9](https://dev.epicgames.com/documentation/unreal-engine/viewport-controls-in-unreal-engine)). Both run the same code, `ViewportCameraNavigation` (`CasaEngine/Framework/Scene/Cameras`). Decision: [ADR-0089](../decisions/0089-viewport-camera-navigation-follows-unreal.md).

## Mouse

| Control | Action |
| --- | --- |
| Left drag | Move forward/back (mouse up/down) and turn left/right. |
| Right drag | Look around from the camera position. |
| Middle drag, or left + right drag | Pan up, down, left and right. |
| Wheel | Move toward or away from the pivot by steps (10 % of the distance, at least 0.5). The pivot does not move; at the minimum distance (0.5) it is pushed ahead of the camera. |
| Right held + wheel | Change the fly speed (×1.25 per notch, 0.1 to 1000 units/s). |
| Alt + left drag | Orbit around the pivot. |
| Alt + right drag | Move toward or away from the pivot. |
| Alt + middle drag | Track (pan). |

A left press becomes a camera drag once the pointer has moved 4 pixels. Below that it is a click.

## Keyboard (right button held)

| Keys | Action |
| --- | --- |
| W / S, Up / Down, numpad 8 / 2 | Forward / back along the view. |
| A / D, Left / Right, numpad 4 / 6 | Left / right. |
| E / Q, Page Up / Page Down, numpad 9 / 7 | Up / down in world space. |
| R / F | Up / down in camera space. |
| Z / C, numpad 1 / 3 | Wider / narrower field of view (45° per second). |

Without the right button these keys do not move the camera. In the editor, letter keys are ignored while Ctrl is held, so Ctrl shortcuts (Ctrl+S, Ctrl+D) never move the camera.

## Other keys

| Key | Editor | Demos |
| --- | --- | --- |
| F (right button up) | Frame the selected entity. | Put the pivot on `ScriptArcBallCamera.FocusPoint` (world origin by default). |
| W / E / R (right button up, no Ctrl) | Gizmo mode: translate / rotate / scale. 4 stays the uniform scale. | — |

## Selection in the editor's perspective view

- A left click (press and release without dragging) selects the entity under the cursor and replaces the selection. Ctrl or Shift + click adds to it.
- Selection happens on release, so a left drag never changes the selection.
- A left press on a gizmo handle manipulates the gizmo; the camera does not move.

The 2D camera and the front-2D view keep their own navigation (see [editor-2d-viewport.md](editor-2d-viewport.md)) and select on press: Ctrl adds, Alt removes.

## Limits

- The gizmo handle under the cursor is the one hovered at the previous frame.
- A Z/C field-of-view change is lost when the viewport is resized: the camera recomputes its field of view from the aspect ratio. Play mode does not save it.
- In the demos, the camera reads and writes its `ArcBallCameraComponent` each frame, so a demo can set the initial camera on the component.
- Not implemented: marquee selection, Space and Q of Unreal, vertex snapping, moving the pivot with the middle button, user-editable sensitivities.
