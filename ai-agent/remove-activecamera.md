# Task plan: Remove GameManager.ActiveCamera, use ViewManager.ActiveView

## Goal
Replace the global mutable `GameManager.ActiveCamera` with `ViewManager.ActiveView.Camera`.
All camera access (editor overlays, resize, UI interaction) goes through the active view.

## Architecture

```
ViewManager
├── Views : IReadOnlyList<RenderView>
├── ActiveView : RenderView?                 // primary view for overlays / interaction
│   └── .Camera                              // camera of that view
├── Add(view, setActive?)                    // auto-sets ActiveView if first or explicit
├── Clear()                                  // resets ActiveView to null
```

- Renderers continue to receive RenderFrame via Flush (no change).
- Editor components (Axis, Grid, Gizmo) and UI code read ViewManager.ActiveView.Camera.
- CasaEngineGame.OnScreenResized resizes the ActiveView camera + updates the view surface.

## Prerequisites
- Multi-view pipeline is the only path (UseRenderPipeline removed) ✅
- SetCameraWithEditor already registers a RenderView ✅

---

## Tasks

### Task 1 — Add ActiveView property to ViewManager
- Add `RenderView? ActiveView { get; private set; }` to `ViewManager`.
- In `Add(RenderView view)`: if `ActiveView == null`, auto-set `ActiveView = view`.
- Add overload `Add(RenderView view, bool setActive)` or a `SetActive(RenderView view)` method.
- In `Clear()`: set `ActiveView = null`.
- In `Remove(RenderView view)`: if removed view was ActiveView, set ActiveView to first remaining or null.
- **Files**: `ViewManager.cs`
- **Build**: Debug + DebugEditor, 0 errors.

### Task 2 — Set ActiveView explicitly in all registration sites
Update every place that registers views to designate the active one:
- `World.LoadContent` (#if !EDITOR): the default full-screen view → active.
- `GameManager.SetCameraWithEditor`: the editor view → active.
- `SplitScreenDemo.InitializeCamera`: first view (cam1) → active.
- `RenderToTextureDemo.InitializeCamera`: main backbuffer view → active.
- `SandBoxGame`: its view → active.
- `DemosGame.ChangeDemo`: after `InitializeCamera`, no special handling needed (demo's first Add is auto-active).
- **Files**: `World.cs`, `GameManager.cs`, `SplitScreenDemo.cs`, `RenderToTextureDemo.cs`, `SandBoxGame.cs`
- **Build**: Debug + DebugEditor, 0 errors. Behavior unchanged.

### Task 3 — Replace editor component reads (Axis, Grid, Gizmo)
Replace `_game.GameManager.ActiveCamera` with `_game.GameManager.ViewManager.ActiveView?.Camera`:
- `AxisComponent.Draw()` — read ViewProj from ActiveView camera.
- `GridComponent.Draw()` — read ViewProj from ActiveView camera.
- `GizmoComponent.Update()` — read View/Proj/Position from ActiveView camera.
- Guard with null check (if ActiveView is null → skip draw/update).
- **Files**: `AxisComponent.cs`, `GridComponent.cs`, `GizmoComponent.cs`
- **Build**: Debug + DebugEditor, 0 errors.

### Task 4 — Replace SpriteRendererComponent.DrawDirectly()
`DrawDirectly()` reads `ActiveCamera` for ViewProjection.
- Change signature to accept a `Matrix viewProjection` parameter, or read from `ViewManager.ActiveView.Camera`.
- Update all callers of `DrawDirectly()`.
- **Files**: `SpriteRendererComponent.cs`, callers of `DrawDirectly`
- **Build**: Debug + DebugEditor, 0 errors.

### Task 5 — Replace SkinnedMeshComponent.Draw() dead camera read
`SkinnedMeshComponent.Draw()` reads `ActiveCamera` but the values are unused by `Flush(RenderFrame)`.
- Remove the camera read and pass dummy/zero values, OR simplify `AddMesh` signature to not take view/proj/cameraPos at all (since Flush overrides them).
- **Files**: `SkinnedMeshComponent.cs`, `SkinnedMeshRendererComponent.cs` (AddMesh signature)
- **Build**: Debug + DebugEditor, 0 errors.

### Task 6 — Replace CasaEngineGame.OnScreenResized()
Currently reads `ActiveCamera` to call `SetViewport()`.
- Replace with: resize `ViewManager.ActiveView.Camera?.OnScreenResized(w, h)`.
- Update BackBufferSurface viewport rect of the active view if it is a backbuffer view (optional — or leave for later resize handling task).
- **Files**: `CasaEngineGame.cs`
- **Build**: Debug + DebugEditor, 0 errors.

### Task 7 — Replace editor UI reads (GameEditor, EntitiesControl, GameEditorWorld)
- `GameEditor.OnScreenResized`: replace `ActiveCamera?.OnScreenResized` with `ViewManager.ActiveView?.Camera?.OnScreenResized`.
- `EntitiesControl.FocusOnEntity`: replace `ActiveCamera.Forward` / `SetPositionAndTarget` with `ViewManager.ActiveView.Camera`.
- `GameEditorWorld.OnDrop`: replace `ActiveCamera` raycast with `ViewManager.ActiveView.Camera`.
- **Files**: `GameEditor.cs`, `EntitiesControl.xaml.cs`, `GameEditorWorld.cs`
- **Build**: DebugEditor, 0 errors.

### Task 8 — Remove ActiveCamera setter usages
Remove all writes to `GameManager.ActiveCamera`:
- `World.LoadContent` (#if !EDITOR): remove `Game.GameManager.ActiveCamera = camera;` (view is already registered).
- `DemosGame.ChangeDemo`: remove `GameManager.ActiveCamera = camera;`.
- `SandBoxGame.LoadContent`: remove `GameManager.ActiveCamera = camera;`.
- `GameManager.SetCameraWithEditor`: remove `ActiveCamera = cameraEditor;` (view is already registered).
- **Files**: `World.cs`, `DemosGame.cs`, `SandBoxGame.cs`, `GameManager.cs`
- **Build**: Debug + DebugEditor, 0 errors.

### Task 9 — Remove ActiveCamera property and SetViewport
- Delete `_activeCamera` field, `ActiveCamera` property, and `SetViewport()` call from `GameManager.cs`.
- Delete `SetViewport()` method from `CasaEngineGame.cs` if it becomes unused.
- Fix any remaining compile errors.
- **Files**: `GameManager.cs`, `CasaEngineGame.cs`
- **Build**: Debug + DebugEditor, 0 errors.

### Task 10 — Update comments and docs
- Remove all TODO/NOTE comments referencing ActiveCamera.
- Update XML doc on ViewManager.ActiveView.
- Update `remove-activecamera.md` status.
- **Build**: Debug + DebugEditor, 0 errors.
- **Commit**: final cleanup commit.

---

## Status: ✅ ALL TASKS COMPLETE

Commits: cd644e6d (T1), 1b65a24f (T3–T6), b6c55930 (T7), 178e7149 (T8), c142b24e (T9), final (T10)

## Verification checklist
- [ ] Editor: load a world → scene renders, grid/axis/gizmo visible and functional.
- [ ] Editor: resize window → scene rescales correctly.
- [ ] Editor: focus on entity → camera moves to entity.
- [ ] Editor: drag-drop entity → placed at correct position.
- [ ] SplitScreenDemo: both views render correctly.
- [ ] RenderToTextureDemo: main view + RT thumbnail render correctly.
- [ ] Single-view standalone: same as before refactor.
- [x] No remaining references to `ActiveCamera` in live engine code.

## Commit strategy
One commit per task. Each commit must compile in both Debug and DebugEditor.
