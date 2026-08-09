# Input Routing Target Architecture

## Objective

Define a single input model shared by in-game views, editor viewports, and MGUI.

## Canonical Flow

1. Window host acquires a single raw snapshot per frame.
   - keyboard state
   - mouse buttons
   - mouse screen position
   - wheel deltas
   - optional gamepad states

2. Engine translates the raw snapshot into per-view input contexts.
   - `ViewManager` owns screen-space to view-local conversion
   - `InputRouter` decides which view receives input
   - the result is a `ViewInputContext` bound to one `RenderView`

3. UI runtime exposes capture state but does not own gameplay logic.
   - `IUIViewRuntime.IsPointerOverUI`
   - `IUIViewRuntime.IsKeyboardCaptured`
   - `IUIViewRuntime.HasModalInput`

4. Runtime consumers read the routed context.
   - gameplay systems consume `InputComponent`
   - editor camera controller consumes the routed context for its view
   - editor gizmo controller consumes the routed context for its view

5. UI panels stay visual.
   - `WorldViewportPanel` creates and displays the `RenderView`
   - it may request focus or activation
   - it must not poll Win32 or own camera/gizmo rules

## Responsibility Split

### Window host

Responsible for:
- acquiring one raw snapshot
- exposing it to MGUI and engine routing

Must not:
- decide gameplay focus
- decide which editor tool reacts to input

### ViewManager

Responsible for:
- tracking active views
- input capture ownership
- screen to view-local coordinate conversion

Must not:
- implement editor camera or gizmo behavior

### InputRouter

Responsible for:
- modal / capture / pointer / keyboard-focus arbitration
- producing one coherent `ViewInputContext`
- routing the same context model in editor and in-game

Must not:
- read UI panel state directly
- rely on editor-only workarounds

### IUIViewRuntime / MGUI

Responsible for:
- pointer-over-ui state
- keyboard capture state
- modal state
- widget-level consumption inside the view

Must not:
- own gameplay camera rules
- own editor gizmo rules
- decide engine-side focus policy outside the contract it exposes

### Editor runtime controllers

Responsible for:
- editor camera navigation
- gizmo hover / selection / drag
- editor-only hotkeys

Must not:
- poll window input directly
- duplicate routing decisions already made by `InputRouter`

### WorldViewportPanel

Responsible for:
- creating the panel content
- binding the rendered texture
- resizing the surface
- requesting activation / focus when appropriate

Must not:
- call `Keyboard.GetState()` or `Mouse.GetState()` for behavior decisions
- intercept Win32 wheel messages
- own gizmo update logic
- own camera movement logic

## Target Runtime Objects

### WindowInputSnapshot
A raw host-level snapshot for one frame.

Suggested fields:
- `KeyboardState KeyboardState`
- `MouseState MouseState`
- `int VerticalWheelDelta`
- `int HorizontalWheelDelta`

### ViewInputContext
A routed view-local input payload.

Suggested fields:
- `ViewId ViewId`
- `KeyboardState KeyboardState`
- `MouseState MouseState`
- `Point ScreenPosition`
- `Point LocalPosition`
- `int VerticalWheelDelta`
- `int HorizontalWheelDelta`
- `InputRoutingState RoutingState`

## Migration Rules

1. Introduce the shared raw snapshot source first.
2. Make `InputRouter` carry wheel delta and local coordinates.
3. Move editor camera logic into a runtime controller.
4. Move editor gizmo logic into a runtime controller.
5. Remove panel-local polling and Win32 hooks last.

## Success Criteria

The architecture is considered correct when:
- editor and in-game use the same routing model
- MGUI only exposes UI capture state and consumes UI events
- `WorldViewportPanel` is no longer an input orchestrator
- wheel, capture, focus, and hover behavior are explained by one central routing path
