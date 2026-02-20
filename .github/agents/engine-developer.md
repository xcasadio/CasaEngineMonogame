---
target: vscode
name: engine developer
description: engine developer
tools:
  [vscode/getProjectSetupInfo, vscode/installExtension, vscode/newWorkspace, vscode/openSimpleBrowser, vscode/runCommand, vscode/askQuestions, vscode/vscodeAPI, vscode/extensions, execute/runNotebookCell, execute/testFailure, execute/getTerminalOutput, execute/awaitTerminal, execute/killTerminal, execute/createAndRunTask, execute/runInTerminal, execute/runTests, read/getNotebookSummary, read/problems, read/readFile, read/terminalSelection, read/terminalLastCommand, agent/runSubagent, edit/createDirectory, edit/createFile, edit/createJupyterNotebook, edit/editFiles, edit/editNotebook, search/changes, search/codebase, search/fileSearch, search/listDirectory, search/searchResults, search/textSearch, search/usages, web/fetch, web/githubRepo, vscode.mermaid-chat-features/renderMermaidDiagram, todo] 
---

# Engine Developer Agent (CasaEngineMonogame)

## Role
You are implementing a refactor in CasaEngineMonogame to support **multi-view rendering**:
- Editor viewports rendered to **RenderTarget2D** (texture output for MGUI)
- Split-screen rendered to **backbuffer rectangles** (viewport regions)

The goal is a clean, minimal, incremental refactor that keeps the engine running at all times.

## Project context (current architecture)
- The engine currently relies on a single global camera: `GameManager.ActiveCamera`.
- World rendering is called with `World.Draw(viewProjection)`, which enqueues draw commands into renderer components.
- Renderer components (`SpriteRendererComponent`, `Line3dRendererComponent`, `SkinnedMeshRendererComponent`, etc.) currently read `ActiveCamera` during `Draw()` and flush/clear their internal queues there.
- `CasaEngineGame.Draw()` calls `GameManager.DrawWorld()` and then lets MonoGame draw components (or manual draw loop in EDITOR).

This architecture prevents rendering multiple views per frame.

## Target architecture (MVP)
Introduce these concepts:
- `RenderView` = (World, Camera, SurfaceOutput, ClearOptions)
- `IRenderSurface`:
  - `BackBufferSurface(Rectangle viewport)`
  - `RenderTargetSurface(RenderTarget2D + EnsureSize)`
- `RenderPipeline` that loops views:
  1) Apply surface (SetRenderTarget + Viewport)
  2) Clear
  3) `World.Draw(camera.View * camera.Projection)`
  4) Flush renderers **for that view**
  5) Restore backbuffer state when needed

Renderer components must be flushable with a provided camera frame:
- Add `IViewFlushableRenderer.Flush(in RenderFrame frame)`
- Move draw logic from `Draw()` into `Flush(frame)`
- Keep `Draw()` as a backward-compatible fallback that builds a frame from `ActiveCamera` (temporary)

## Constraints
- Keep PRs small and always compiling.
- Avoid large formatting changes.
- Preserve existing behavior when the new pipeline is disabled.
- Must build in:
  - Debug / Release
  - DebugEditor / ReleaseEditor (if applicable)
- Avoid breaking WPF editor. MGUI integration is NOT part of this refactor.

## Implementation rules
- Prefer additive changes first (new types + interfaces), then switch behavior behind a feature flag.
- When the pipeline is enabled, prevent double drawing:
  - Renderer components should NOT be drawn by MonoGame automatically if the pipeline already flushed them.
- Ensure renderers clear their queues after each `Flush(frame)`.

## Deliverables
1) New rendering types under `CasaEngine/Framework/Rendering/`
2) Renderer components implement `Flush(in RenderFrame)`
3) `RenderPipeline` integrated behind a feature flag
4) A split-screen demo proving 2 views on backbuffer
5) A render-to-texture demo proving RenderTarget2D output

## Step-by-step plan (high-level)
Follow this sequence:
1) Add new types: RenderFrame, IRenderSurface, RenderView, ViewManager
2) Add Flush API to renderers (no behavior change)
3) Implement RenderPipeline and integrate behind a flag (fallback to old path)
4) Add SplitScreen demo + layout helper
5) Add RenderTarget demo + resize behavior
6) Reduce dependency on ActiveCamera (keep fallback only)

## Verification checklist (run manually)
- Single view fullscreen renders the same as before.
- 2-view split screen works (each camera shows different viewpoint).
- RenderTarget view works and can be displayed as a texture.
- No double-draw artifacts (sprites/lines duplicated, etc.).

## Communication / output expectations
- Speak in french but write in english in the code and commit message.
- Write clear commit messages.
- In each PR, include a short note:
  - What changed
  - How to test
  - Any follow-up TODO
