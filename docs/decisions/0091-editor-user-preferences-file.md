# ADR-0091: The editor keeps per-user mouse and camera preferences in one JSON file

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/archive/editor-mouse-preferences-tasks.md` (decisions D1 to D5 and proposals P1 to P8, settled with the author on 2026-11-14), branch `chantier/editor-mouse-preferences`. Follows ADR-0089. Numbered ADR-0091 because ADR-0090 is taken on branch `chantier/demos-onboarding`.

## Context

- ADR-0089 gave the editor viewport and the demos one Unreal-style navigation (`ViewportCameraNavigation`), with sensitivities fixed in code.
- The editor only stores per-project state, in `<project>/.casaeditor/` (`layout.editor.json`, `viewport.editor.json`). Nothing is stored per user.
- `WorldViewportPanel` is created in four places (world viewport, material viewports, animation 2D and entity asset previews), each with its own camera controllers; all four receive the same `HostedEditorGameAdapter`.
- `NumericField` raises `ValueChanged` on every keystroke and wheel or +/- step, and has no commit event.

## Decision

- **Per-user file.** Mouse and camera preferences live in `%APPDATA%/CasaEngine/editor.preferences.json` (`Environment.SpecialFolder.ApplicationData`), shared by all projects. Only the editor reads it; the runtime and the demos do not.
- **Format.** Snake_case keys, a `version` (1), a `camera_3d` block (`rotation_sensitivity`, `pan_sensitivity`, `wheel_step_ratio`, `fly_speed`, `fly_speed_wheel_factor`, `field_of_view_rate_degrees`, `drag_threshold_pixels`, `invert_y`) and a `camera_2d` block (`pan_speed`, `zoom_steps_per_notch`, `invert_y`). Keys are written in a stable order.
- **Tolerant reading.** A missing key takes its default (the values of ADR-0089 and of the 2D camera before this decision); an out-of-range or mistyped value is clamped or defaulted and reported once as a warning naming the key; an unreadable file loads the defaults with a warning and is not overwritten until a preference changes. Bounds: rotation [0.0005, 0.05] rad/px, pan [0.001, 0.1], wheel step [0.01, 0.5], fly speed [0.1, 1000], fly wheel factor [1.05, 3], field-of-view rate [5, 180] °/s, drag threshold [1, 20] px, 2D pan speed [0.1, 10], 2D zoom steps per notch [1, 5].
- **One store, applied live, written late.** `GameEditor` creates one `EditorPreferencesStore`, exposed by `HostedEditorGameAdapter.Preferences` (no static state). Every change (a panel field, Reset, a fly speed changed with right button + wheel in any viewport) is clamped and applied at once to every viewport, which subscribe on creation and unsubscribe on dispose. The file is written once, after one second without changes, by a cheap check in `GameEditor.Update`, and on editor exit; never per keystroke and never in the camera code.
- **Invert Y.** `ViewportCameraNavigation.InvertY` (additive) inverts look and Alt orbit in 3D; the 2D setting inverts the vertical pan.
- **Panel.** A dockable `Windows > Editor Preferences` panel edits the values live and offers `Reset to defaults`.

## Consequences

- Camera feel is the same in every project and survives restarts, including the fly speed.
- The runtime stays independent of editor data; the demos keep their code defaults.
- **Known limits.** Writes under `%APPDATA%` made by an editor started from inside the Claude desktop app are virtualised and invisible elsewhere. A value typed character by character is applied at each step (for example "0" is clamped to the minimum before "0.005" is complete); only the final value is written.
- Deferred: per-project preferences, keyboard shortcut customisation, other editor preferences (theme, grid, snapping).
