# ADR-0018: Play-in-editor session model

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `ai-agent/tasks/play-in-editor-tasks.md:29-49`, `ai-agent/audits/analysis-play-in-editor.md:120-135,180-195`, `docs/editor/play-in-editor.md:19-47`

## Context

`ai-agent/tasks/play-in-editor-tasks.md` records "Décisions d'architecture (fixées par l'analyse, ne pas rediscuter en cours de tâche)" (ai-agent/tasks/play-in-editor-tasks.md:29). The underlying analysis (`ai-agent/audits/analysis-play-in-editor.md`) recommends a Unity-style "serialize → copy" model so Play always runs a copy of the edited world, never the document itself, keeping the edited world aside without clearing it (ai-agent/audits/analysis-play-in-editor.md:128-135), and specifies a deloadable assembly host (`ScriptAssemblyHost`) using a collectible `AssemblyLoadContext` with an `AssemblyDependencyResolver`, where any `CasaEngine.*`/MonoGame type must resolve to the default ALC (return null in `Load()`) so that a script's `GameplayProxy` remains assignable to the engine's `GameplayProxy` (ai-agent/audits/analysis-play-in-editor.md:183-189). `docs/editor/play-in-editor.md` describes the resulting runtime behavior: on Play the edited world is serialized in memory, a play world is created from that JSON and run under `GameplayExecutionPolicies.EditorSimulation`; on Stop the play world is destroyed and the untouched edited world is reinstalled via `GameManager.RestoreWorld`, with editor camera and undo intact (docs/editor/play-in-editor.md:19-30).

## Decision

- Play a copy: at Play, the edited world is serialized to a `JObject` in memory (`EditorEntityJsonSerializer.SaveWorld`), a play world is created via `new World()` + `Load(JObject)` then `GameManager.SetWorldToLoad(World)` (the `_isNewWorld` path → `LoadContent` + `BeginPlay`); the edited world is set aside without `Clear()` (source: ai-agent/tasks/play-in-editor-tasks.md:31-35).
- Restoration uses a new additive API `GameManager.RestoreWorld(World)` that reinstalls a world without going through `LoadContent`/`BeginPlay` (which `SetWorldToLoad` would do, duplicating the edited world's entities) and notifies `WorldChanged` (source: ai-agent/tasks/play-in-editor-tasks.md:36-38).
- Policy: Play runs under `GameplayExecutionPolicies.EditorSimulation`, editing under `EditorPreview`; the switch happens before the world swap, at the loading gates (source: ai-agent/tasks/play-in-editor-tasks.md:39).
- Camera: in Play, the viewport's RenderView is driven by the first `CameraComponent` of the played world (same rule as `DefaultRuntimeViewBootstrapper`), otherwise `CreateDefaultCamera()`; on Stop, the editor camera is restored (source: ai-agent/tasks/play-in-editor-tasks.md:40-43).
- Scripts: engine types (`CasaEngine.*`, MonoGame) must always resolve in the default ALC; only the gameplay DLL (and its private dependencies) lives in the collectible ALC; `ElementFactory` must be able to unregister an assembly (source: ai-agent/tasks/play-in-editor-tasks.md:44-46).
- A scripts build failure keeps the editor in edit mode, with errors logged, and no Play (source: ai-agent/tasks/play-in-editor-tasks.md:47).
- A script exception during Play triggers a clean stop of Play plus a logged error (fail-stop, no silent per-frame catch) (source: ai-agent/tasks/play-in-editor-tasks.md:48-49).

## Consequences

- During a Play session: project save is refused, undo/redo is suspended (`EditorHistoryService.IsSuspended`), and project switching is blocked (docs/editor/play-in-editor.md:19 area; ai-agent/audits/analysis-play-in-editor.md:132-135).
- The gameplay DLL is loaded via a shadow copy in the collectible `ScriptAssemblyHost` so the original file is never locked and stays rebuildable while the project is open (docs/editor/play-in-editor.md:41-44).
- Implementation status observed in code: `EditorWorldPlaySnapshot` (`CasaEngine.EditorServices/PlayMode/EditorWorldPlaySnapshot.cs`, tested by `CasaEngine.Tests/EditorServices/EditorWorldPlaySnapshotTests.cs`), `GameManager.RestoreWorld` (tested by `CasaEngine.Tests/Application/GameManagerRestoreWorldTests.cs`), and `ScriptAssemblyHost` (`CasaEngine/Engine/Plugins/ScriptAssemblyHost.cs`, tested by `CasaEngine.Tests/Scripting/ScriptAssemblyHostTests.cs` and `CasaEngine.Tests/Scripting/ElementFactoryReloadTests.cs`) all exist in the current tree, consistent with `ai-agent/audits/analysis-decisions-inventory.md:104-109` reporting these decisions as "Appliquée".
