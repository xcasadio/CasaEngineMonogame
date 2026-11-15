# ADR-0095: The 3d camera keeps its vertical field of view

- **Status**: Accepted
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/demos-camera-help-keyboard-tasks.md`, decisions D1 and D8, approved by the author on 2026-11-15

## Context

`Camera3dComponent` replaced its `FieldOfView` at every `InitializeWithWorld` and `OnScreenResized` with `pi/4 * 1.7778 / aspect ratio`. At 16:9 that is 45 degrees vertically; at 1024x768 (4:3, the demos' debug window) 60 degrees, about 75 degrees horizontally, which stretches objects near the edges of the screen (reported by the author on the Hello world demo). The value loaded from a saved camera or typed in the inspector was overwritten, so `fieldOfView` in `.entity` files had no effect.

Unity (`Camera.fieldOfView`, vertical) and Godot (`Camera3D.keep_aspect = KEEP_HEIGHT` by default) keep the vertical angle and let a wider window see more on the sides ("Hor+"); Unreal keeps the horizontal angle by default.

## Decision

- `FieldOfView` is the vertical field of view and is kept whatever the viewport: no recalculation at initialisation or resize. A wider viewport shows more on the sides.
- Default: `Camera3dComponent.DefaultFieldOfView` = pi/4 (45 degrees), the angle every camera had at 16:9. A camera whose loaded angle is 0 gets the default when it joins a world.
- The two saved cameras that stored 0.8377581 (48 degrees, written while the old value was in force), `CasaEngine.Demos/Content/EditorWorld/Entities/camera.entity` and `Projects/SampleProject/Entities/camera.entity`, are set back to 0.7853982 (pi/4), the angle they showed at 16:9.

## Consequences

- At 16:9 nothing changes. At narrower viewports (4:3, portrait) the camera sees less on the sides than before and less distortion at the edges; a scene framed at 16:9 can lose its sides at 4:3.
- The field of view saved in an entity, or set in the inspector or by code, now takes effect.
- Tests: `Camera3dFieldOfViewTests`.
