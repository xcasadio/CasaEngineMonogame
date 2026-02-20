# Remove UseRenderPipeline Flag — Make RenderPipeline the Only Path

## Context

The multi-view `RenderPipeline` is now functional (split-screen, render-to-texture).
`CasaEngineGame.UseRenderPipeline` still gates a legacy code path (`DrawWorld` + `base.Draw`).
This task removes the flag and the legacy path, making the pipeline the **only** rendering path.

### Key files and current state

| File | What needs to change |
|------|---------------------|
| `CasaEngine/Framework/Game/CasaEngineGame.cs` | Remove `UseRenderPipeline` property, remove legacy `else` branch in `Draw()`, always use pipeline path |
| `CasaEngine/Framework/Game/GameManager.cs` | Remove `DrawWorld(GameTime)` method, update `ActiveCamera` doc |
| `CasaEngine/Framework/Game/Components/StaticMeshRendererComponent.cs` | Remove `Draw(GameTime)` fallback that reads `ActiveCamera` |
| `CasaEngine/Framework/Game/Components/SpriteRendererComponent.cs` | Remove `Draw(GameTime)` fallback that reads `ActiveCamera` |
| `CasaEngine/Framework/Game/Components/Line3dRendererComponent.cs` | Remove `Draw(GameTime)` fallback that reads `ActiveCamera` |
| `CasaEngine/Framework/Game/Components/SkinnedMeshRendererComponent.cs` | Remove `Draw(GameTime)` fallback that reads `ActiveCamera` |
| `CasaEngine.Demos/Demos/SplitScreenDemo.cs` | Remove `game.UseRenderPipeline = true/false` lines |
| `CasaEngine.Demos/Demos/RenderToTextureDemo.cs` | Remove `game.UseRenderPipeline = true/false` lines |
| `CasaEngine/Framework/World/World.cs` | Auto-register a default `RenderView` from `ActiveCamera` (replaces `Game.GameManager.ActiveCamera = camera`) |

### Important rules
- Each task = 1 small commit
- Build must pass (`dotnet build CasaEngine.Demos -c Debug --no-restore -v minimal` AND `dotnet build CasaEngine\CasaEngine.WithEditor.csproj -c DebugEditor --no-restore -v minimal`) after every commit
- Do **not** touch editor-only components (`AxisComponent`, `GridComponent`, `GizmoComponent`) — they still read `ActiveCamera` and that is OK for now (separate future task)
- Keep `ActiveCamera` property alive as it is still used by editor components and implicit fallback view
- Write commit messages in English
- Write code comments in English

---

## Tasks

### Task 1 — Remove `UseRenderPipeline` flag from `CasaEngineGame`

**File:** `CasaEngine/Framework/Game/CasaEngineGame.cs`

1. Delete the `UseRenderPipeline` property (line 52).
2. Delete the comment referencing it above `_renderPipeline` field (line 181).
3. In `Draw(GameTime)` method (starts around line 306):
   - Remove the `if (UseRenderPipeline && _renderPipeline != null)` condition — the pipeline block becomes **unconditional** (keep the `_renderPipeline != null` null check as safety).
   - **Delete the entire `else` block** (the legacy path: `GraphicsDevice.Clear(Color.Black)`, `GameManager.DrawWorld(gameTime)`, `base.Draw(gameTime)` / EDITOR manual component loop).
   - The remaining code should be: null-check `_renderPipeline`, then Phase 1 / Phase 2 / Phase 3 (pipeline + components, as currently implemented in the `if` block).
4. Build both configs. Commit: `refactor(rendering): remove UseRenderPipeline flag, pipeline is now the only path`

---

### Task 2 — Remove `GameManager.DrawWorld()` method

**File:** `CasaEngine/Framework/Game/GameManager.cs`

1. Delete the `DrawWorld(GameTime)` method (around line 122-129), including its XML doc comment.
2. Update the XML doc on `ActiveCamera` property: remove the sentence mentioning `UseRenderPipeline`. Replace with: `Used by editor components and as a fallback when ViewManager has no views.`
3. Build both configs. Commit: `refactor(rendering): remove legacy DrawWorld() from GameManager`

---

### Task 3 — Remove `Draw(GameTime)` fallback from `StaticMeshRendererComponent`

**File:** `CasaEngine/Framework/Game/Components/StaticMeshRendererComponent.cs`

1. Delete the `Draw(GameTime)` override (lines ~96-107). This removes the `ActiveCamera` fallback.
2. The pipeline now guarantees `Flush(in RenderFrame)` is called — the `Draw(GameTime)` from `DrawableGameComponent` base class will do nothing (no override = no-op).
3. Build both configs. Commit: `refactor(rendering): remove Draw(GameTime) fallback from StaticMeshRendererComponent`

---

### Task 4 — Remove `Draw(GameTime)` fallback from `SpriteRendererComponent`

**File:** `CasaEngine/Framework/Game/Components/SpriteRendererComponent.cs`

1. Delete the `Draw(GameTime)` override (lines ~106-113). This removes the `ActiveCamera` fallback.
2. Build both configs. Commit: `refactor(rendering): remove Draw(GameTime) fallback from SpriteRendererComponent`

---

### Task 5 — Remove `Draw(GameTime)` fallback from `Line3dRendererComponent`

**File:** `CasaEngine/Framework/Game/Components/Line3dRendererComponent.cs`

1. Delete the `Draw(GameTime)` override (lines ~86-93). This removes the `ActiveCamera` fallback.
2. Build both configs. Commit: `refactor(rendering): remove Draw(GameTime) fallback from Line3dRendererComponent`

---

### Task 6 — Remove `Draw(GameTime)` fallback from `SkinnedMeshRendererComponent`

**File:** `CasaEngine/Framework/Game/Components/SkinnedMeshRendererComponent.cs`

1. Delete the `Draw(GameTime)` override (lines ~83-90). This removes the `ActiveCamera` fallback.
2. Build both configs. Commit: `refactor(rendering): remove Draw(GameTime) fallback from SkinnedMeshRendererComponent`

---

### Task 7 — Clean up demos: remove `UseRenderPipeline` references

**Files:**
- `CasaEngine.Demos/Demos/SplitScreenDemo.cs`
- `CasaEngine.Demos/Demos/RenderToTextureDemo.cs`

1. In `SplitScreenDemo.InitializeCamera()`: delete the line `game.UseRenderPipeline = true;`
2. In `SplitScreenDemo.Clean()`: delete the line `_game.UseRenderPipeline = false;`
3. In `RenderToTextureDemo.InitializeCamera()`: delete the line `game.UseRenderPipeline = true;`
4. In `RenderToTextureDemo.Clean()`: delete the line `_game.UseRenderPipeline = false;`
5. Build both configs. Commit: `refactor(demos): remove UseRenderPipeline references from demos`

---

### Task 8 — Auto-register default `RenderView` in `World.LoadContent` (implicit single-view)

**Context:** Currently, `World.LoadContent()` sets `Game.GameManager.ActiveCamera = camera` (line ~153, inside `#if !EDITOR`). When the pipeline runs with zero views in `ViewManager`, it creates an implicit fallback `RenderView` from `ActiveCamera`. This is acceptable for now but should also register the view explicitly in `ViewManager` so the fallback logic in `CasaEngineGame.Draw()` can be simplified later.

**File:** `CasaEngine/Framework/World/World.cs`

1. After the existing `Game.GameManager.ActiveCamera = camera;` line (inside `#if !EDITOR`), add:
   ```csharp
   // Register a default full-screen RenderView so the pipeline always has at least one view.
   var pp = Game.GraphicsDevice.PresentationParameters;
   var fullScreen = new Microsoft.Xna.Framework.Rectangle(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
   Game.GameManager.ViewManager.Add(new Rendering.RenderView(this, camera, new Rendering.BackBufferSurface(fullScreen))
   {
       Name = "Default view",
       ClearColor = Microsoft.Xna.Framework.Color.CornflowerBlue,
   });
   ```
2. Build both configs. Commit: `feat(rendering): auto-register default RenderView in World.LoadContent`

---

### Task 9 — Remove implicit fallback view from `CasaEngineGame.Draw()`

**Context:** After Task 8, the ViewManager always has at least one view when a world is loaded. The `views.Count == 0` fallback block in `CasaEngineGame.Draw()` is no longer needed for normal gameplay. Keep a minimal safety net (just `GraphicsDevice.Clear(Color.Black)` if views is empty).

**File:** `CasaEngine/Framework/Game/CasaEngineGame.cs`

1. In the pipeline block of `Draw(GameTime)`, replace the `views.Count == 0` block:
   - Remove the `ActiveCamera` / `BackBufferSurface` implicit view creation.
   - Replace with just `GraphicsDevice.Clear(Color.Black);` (safety if no world loaded yet).
2. Build both configs. Commit: `refactor(rendering): simplify Draw(), remove implicit fallback view creation`

---

### Task 10 — Update documentation and clean up comments

**Files:** multiple

1. In `CasaEngine/Framework/Game/Components/IViewFlushableRenderer.cs`: update doc comment that mentions `ActiveCamera` — replace with mention of `RenderPipeline`.
2. In `CasaEngine/Framework/Game/GameManager.cs`: update `ActiveCamera` doc to remove the "TODO: Deprecate" line — it is now used only by editor components and as a convenience setter.
3. In `CasaEngine/Framework/Entities/Components/SkinnedMeshComponent.cs`: remove or update the TODO comment about unused camera data if still present.
4. In `CasaEngine/Framework/Game/Components/SpriteRendererComponent.cs`: update TODO comment on `DrawDirectly()` — now that pipeline is the only path, this method needs refactoring (leave TODO but reword).
5. Build both configs. Commit: `docs(rendering): update comments after UseRenderPipeline removal`

---

## Verification checklist (after all tasks)
- [ ] `UseRenderPipeline` does not appear anywhere in the codebase (except in git history and tasks.md)
- [ ] `DrawWorld` does not appear in `GameManager.cs`
- [ ] No renderer component has a `Draw(GameTime)` override
- [ ] All existing demos still work (single view, split-screen, render-to-texture)
- [ ] DebugEditor build compiles without errors
- [ ] Editor-only components (`AxisComponent`, `GridComponent`, `GizmoComponent`) still work via `ActiveCamera`
