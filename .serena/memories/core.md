# CasaEngineMonogame — core

- Authoritative agent rules: `AGENTS.md` (French). Path rules: `.claude/rules/` + twin `.github/instructions/`. Never duplicate them here.
- C# / MonoGame 3.8 game engine: runtime + MGUI-based editor. Tech/versions: `mem:tech_stack`.
- Source map:
  - `CasaEngine/` runtime library. `Core/` (math, collections, serialization, logging), `Engine/` (low-level: Physics types, Input, Geometry, Plugins), `Framework/` (Scene/World/Entities, Rendering, Physics/Bepu, Animations, Audio, UI, Gameplay, Cutscenes, SaveGames, Scripting, AI).
  - `CasaEngine.EditorServices/` editor-side save/export/import/authoring (no UI). `CasaEngine.Editor/` MGUI editor app (WinExe).
  - `CasaEngine.Compiler/`, `CasaEngine.DotNetCompiler/` gameplay script compilation; `CasaEngine.Shaders/` shader sources/build; `GizmoTool/`; `CasaEngine.AposShapes/` shape drawing.
  - `CasaEngine.Launcher/`, `CasaEngine.Demos/` executables; `Projects/` sample game projects (SandBoxGame, RPGDemo, SampleProject, ...).
  - `CasaEngine.Tests/` single xunit project for runtime + editor services, folders mirror engine domains.
  - `MGUI/`, `NvgSharp/` git submodules (own rules, own commits).
- Invariants: runtime never references editor projects; asset save/export lives only in EditorServices/Editor; player save games are the one runtime write path (`CasaEngine/Framework/SaveGames`).
- Physics: bepuphysics2 behind `IPhysicsWorld` (`CasaEngine/Framework/Application/Components/Physics/`), backend in `CasaEngine/Framework/Physics/Bepu/`; `PhysicsBody` wraps internal `IPhysicsBodyBackend`.
- Docs: `docs/engine/`, `docs/editor/`, ADRs `docs/decisions/` (cited as ADR-00xx in code comments). Agent plans: `ai-agent/tasks/`.
- Related: commands `mem:suggested_commands`, style `mem:conventions`, done-checklist `mem:task_completion`.
