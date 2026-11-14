# Editor preferences

`Windows > Editor Preferences` edits the per-user mouse and camera preferences of the editor's viewports. Decision: [ADR-0091](../decisions/0091-editor-user-preferences-file.md). The controls themselves are described in [viewport-camera-controls.md](viewport-camera-controls.md).

## Panel

- Every change applies at once to every viewport: the world viewport, material viewports and asset previews.
- `Reset to defaults` restores the values below.
- The panel follows the fly speed changed with right button + wheel in any viewport.
- The bottom line shows the file in use.

## File

`%APPDATA%/CasaEngine/editor.preferences.json`, shared by all projects. Only the editor reads it; the demos and the runtime keep their own defaults.

```json
{
  "version": 1,
  "camera_3d": {
    "rotation_sensitivity": 0.005,
    "pan_sensitivity": 0.01,
    "wheel_step_ratio": 0.1,
    "fly_speed": 10.0,
    "fly_speed_wheel_factor": 1.25,
    "field_of_view_rate_degrees": 45.0,
    "drag_threshold_pixels": 4.0,
    "invert_y": false
  },
  "camera_2d": {
    "pan_speed": 1.0,
    "zoom_steps_per_notch": 1,
    "invert_y": false
  }
}
```

| Key | Meaning | Default | Bounds |
| --- | --- | --- | --- |
| `camera_3d.rotation_sensitivity` | Look, turn and orbit, radians per pixel. | 0.005 | 0.0005 – 0.05 |
| `camera_3d.pan_sensitivity` | Pan, track and left-drag move, fraction of the pivot distance per pixel. | 0.01 | 0.001 – 0.1 |
| `camera_3d.wheel_step_ratio` | Wheel step, fraction of the pivot distance. | 0.1 | 0.01 – 0.5 |
| `camera_3d.fly_speed` | Fly speed (right button + keys), units per second. Saved when right button + wheel changes it. | 10 | 0.1 – 1000 |
| `camera_3d.fly_speed_wheel_factor` | Fly speed factor per wheel notch with the right button held. | 1.25 | 1.05 – 3 |
| `camera_3d.field_of_view_rate_degrees` | Z / C field-of-view change, degrees per second. | 45 | 5 – 180 |
| `camera_3d.drag_threshold_pixels` | Pointer travel that turns a left press into a camera drag. | 4 | 1 – 20 |
| `camera_3d.invert_y` | Inverts look and Alt orbit. | false | — |
| `camera_2d.pan_speed` | 2D pan multiplier (1 = the world follows the cursor). | 1 | 0.1 – 10 |
| `camera_2d.zoom_steps_per_notch` | 2D zoom steps per wheel notch. | 1 | 1 – 5 |
| `camera_2d.invert_y` | Inverts the vertical 2D pan. | false | — |

- A missing key takes its default.
- A value out of its bounds is clamped; a value of the wrong type takes its default. Both are reported once as a warning naming the key.
- A file that is not valid Json loads the defaults with a warning, and is left as it is until a preference changes.
- The file is written once, a second after the last change, and when the editor closes. Typing a value does not write the file at each keystroke.

## Limits

- An editor started from inside the Claude desktop app writes `%APPDATA%` in a virtualised location, invisible elsewhere. Start the editor outside the app to edit the real file.
- Editor automation runs use a temporary file (`%TEMP%/CasaEngine/editor.preferences.automation.json`) and leave the user's file untouched.
- A value is applied while it is typed: "0.005" goes through "0", clamped to the minimum, before the last digit. Only the final value is saved.
- A Z / C field-of-view change is not a preference: it is lost when the viewport is resized.
