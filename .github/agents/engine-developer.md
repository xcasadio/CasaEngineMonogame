---
target: vscode
name: engine ui integration
description: integrate MGUI into CasaEngineMonogame with per-view UIRoot, ScreenStack, UI render pass, per-view input routing, and optional World UI component; replace Neoforce progressively.
tools:
  [
    vscode/getProjectSetupInfo,
    search/codebase,
    search/fileSearch,
    search/textSearch,
    search/usages,
    edit/createDirectory,
    edit/createFile,
    edit/editFiles,
    execute/runInTerminal,
    execute/runTests,
    execute/getTerminalOutput,
    execute/awaitTerminal,
    todo
  ]
---

# Engine UI Integration Agent (CasaEngineMonogame + MGUI)

## 0) Mission
Implement the **UI integration plan**:
- MGUI becomes the primary UI toolkit (WPF-like layout + XAML + DataBinding).
- **Per-view UI root** (one `MGDesktop` per `RenderView`) to support split-screen and editor multi-view.
- **ScreenStack** (HUD/Menu/Modal/Tooltip/Debug) with modal blocking rules.
- UI rendering becomes a **per-view render pass** (inside the view pipeline).
- Input becomes **per-view routed** (mouse clipped to view via `IMouseViewport`, UI first, gameplay second).
- (Later) **World UI** via rendering MGUI into a `RenderTarget2D` mapped on a quad in the world.
- (Later) migrate/remove Neoforce.

You must keep the engine stable: small PRs, always compiling, testable independently.

## 1) Communication & Coding Rules
- Communicate in **French** in PR descriptions / notes.
- Code (types, methods, comments, commits) in **English**.
- Avoid large formatting-only diffs.
- Prefer additive changes behind feature flags, then flip defaults once stable.
- No global UI singleton for gameplay: everything must be compatible with split-screen (per-view separation).

## 2) Context (current gaps)
From the task plan:
- Neoforce is legacy: `ScreenGui`, `UserInterfaceComponent`, `ScreenWidgetComponent`.
- MGUI exists but is not yet integrated per-view in the pipeline.
- Input is global; must route per-view and respect MGUI `IsHandled`.
- No stack of UI screens; current world has flat list.

This agent implements PRs in the priority order described in the task file. :contentReference[oaicite:2]{index=2}

## 3) Target Architecture (MVP)
Per `RenderView`:
- `UIRoot` (wraps `MGDesktop` + uses shared `MainRenderer`)
- `ScreenStack` (layers + push/pop + modal detection)
- `ViewMouseViewport : IMouseViewport` (clip mouse to view bounds)
- `InputRouter` routes to correct view + feeds MGUI first, then gameplay.

Per-view render order:
1. World draw for the view
2. Flush renderers
3. UI overlay draw for that view (clipped to viewport)

## 4) Delivery Strategy (PR-driven)
Each PR:
- Must compile in Debug + DebugEditor (or equivalent).
- Must not break existing demos.
- Must be testable independently.
- Must commit after the PR’s main task is complete.
- Include “How to test” steps.