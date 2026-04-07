# Editor / Runtime Separation Audit Report

## Executive summary

The repository has separate executable entry points for editor mode and runtime mode, but the separation is not clean inside the shared CasaEngine assembly.

The strongest positive signal is that runtime applications such as CasaEngine.Launcher and CasaEngine.Demos can start from their own composition roots, while editor applications such as CasaEngine.Editor and CasaEngine.EditorUI also have independent startup paths.

The strongest negative signal is that editor concerns are still embedded in the shared source tree through a combination of:

- a second build variant, CasaEngine.WithEditor.csproj, that compiles the same source with `EDITOR` enabled
- editor-only APIs compiled directly into runtime domain types such as World, Entity, SceneComponent, AssetContentManager, AssetCatalog, ProjectSettingsHelper, and CasaEngineGame
- editor rendering and gizmo concepts living in the shared framework namespace and, in some cases, being consumed by non-editor runtime applications
- authoring concerns such as project creation, asset saving, asset catalog mutation, and import metadata handling staying in the shared assembly instead of an editor-only assembly or service layer

Conclusion: the repository currently has separate applications, but not a clean runtime/editor architecture. The split happens partly at the project level and partly deep inside shared code through `#if EDITOR`. The next refactor should focus first on dependency direction and authoring/runtime service boundaries, not on superficial directive cleanup.

Static inspection only was performed for this audit. No large refactor was attempted, and no full solution build or runtime validation was executed as part of this audit.

## Repository and project overview

### Project inventory

| Project | Type | Apparent responsibility | Notable references / dependencies | Separation assessment |
| --- | --- | --- | --- | --- |
| CasaEngine/CasaEngine.csproj | Library | Shared runtime engine | References MGUI.Core and MGUI.Shared; no `EDITOR` symbol | Runtime-capable, but source contains many editor branches |
| CasaEngine/CasaEngine.WithEditor.csproj | Library | Same shared engine source compiled for editor mode | Defines `EDITOR` on all configurations, removes `Engine/Graphics/**`, references GizmoTools at line 40 | Mixed responsibility; key architectural problem |
| CasaEngine.Editor/CasaEngine.Editor.csproj | WinExe | New MonoGame-based editor host | References CasaEngine.WithEditor and GizmoTools at lines 19-20 | Editor-only and appropriately separate at app level |
| CasaEngine.EditorUI/CasaEngine.EditorUI.csproj | WPF WinExe | New WPF editor shell and viewport host | Defines `EDITOR`, references CasaEngine.WithEditor and GizmoTools at lines 74-75 | Editor-only and appropriately separate at app level |
| Editor/Editor.csproj | WinExe | Legacy WinForms editor | Defines `EDITOR`, but references CasaEngine.csproj at line 24 instead of CasaEngine.WithEditor | Legacy ambiguity; build setup is inconsistent |
| CasaEngine.Launcher/CasaEngine.Launcher.csproj | WinExe | Runtime game launcher | References CasaEngine.csproj at line 20 | Clean runtime app boundary |
| CasaEngine.Demos/CasaEngine.Demos.csproj | WinExe | Runtime demos / sandbox app | References CasaEngine.csproj at line 20 | Runtime app, but source imports editor-named components |
| Projects/SandBoxGame/SandBoxGame.csproj | WinExe | Runtime sandbox app | References CasaEngine.csproj at line 20 | Runtime app, but source imports editor-named components |
| Projects/CasaEngine.RPGDemo/CasaEngine.RPGDemo.csproj | Library | Runtime gameplay/demo library | References CasaEngine.csproj at line 16 | Runtime-side, relatively clean |
| CasaEngine.Compiler/CasaEngine.Compiler.csproj | Library | Compiler abstractions | No project refs | Neutral / shared tooling |
| CasaEngine.DotNetCompiler/CasaEngine.DotNetCompiler.csproj | Library | Roslyn-backed compiler implementation | References CasaEngine.Compiler | Tooling-side, acceptable |
| CasaEngine.Shaders/CasaEngine.Shaders.csproj | Library | Shader build utilities | MonoGame shader dependencies | Ambiguous, but not central to runtime/editor split |
| CasaEngine.WpfControls/CasaEngine.WpfControls.csproj | Library | WPF editor controls | References CasaEngine.WithEditor at line 18 | Editor-only but coupled to mixed engine assembly |
| GizmoTool/GizmoTools.csproj | Library | Gizmo manipulation/editor tooling | MonoGame, FontStashSharp | Editor-only; should not leak into shared runtime source |
| MonoGame.Framework.Wpf.Core/MonoGame.Framework.Wpf.Core.csproj | Library | WPF hosting infrastructure for MonoGame | WPF + MonoGame | Editor infrastructure, acceptable |
| MGUI/MGUI.Core/MGUI.Core.csproj | Library | UI runtime core | References MGUI.Shared, has `UseWPF` / `OS_WINDOWS` symbols | Shared external infrastructure |
| MGUI/MGUI.Shared/MGUI.Shared.csproj | Library | UI shared types | MonoGame.Extended and Prism | Shared external infrastructure |
| MGUI/MGUI.FontStashSharp/MGUI.FontStashSharp.csproj | Library | MGUI text rendering integration | References MGUI.Shared | Shared external infrastructure |
| MGUI/MGUI.Samples/MGUI.Samples.csproj | WinExe | Upstream/sample project | MGUI samples | Not part of CasaEngine architecture boundary |
| MGUI/MGUI.Tests/MGUI.Tests.csproj | Test project | MGUI tests | xUnit | Not part of CasaEngine architecture boundary |

### First impression of separation quality

- Application-level separation exists.
- Assembly-level separation is incomplete because editor and runtime still share the same source tree and diverge through `CasaEngine.csproj` versus `CasaEngine.WithEditor.csproj`.
- Editor-only dependencies are not fully contained inside editor-only projects.
- Several shared domain types expose editor-only save, mutation, selection, or view concepts.

## Startup and composition roots

### Runtime / in-game startup paths

1. CasaEngine.Launcher

- Entry point: CasaEngine.Launcher/Program.cs
- Flow: `Main(string[] args)` sets `EngineEnvironment.ProjectPath`, constructs `new CasaEngineGame(args[0])`, then calls `Run()`.
- Shared initialization then happens inside CasaEngine/Framework/Game/CasaEngineGame.cs `Initialize()`.
- World loading happens later in CasaEngine/Framework/Game/GameManager.cs: `EndLoadContent()` and `UpdateWorld()` bootstrap the first world and runtime views under `#if !EDITOR`.

2. CasaEngine.Demos

- Entry point: CasaEngine.Demos/Program.cs
- Flow: `new DemosGame().Run()`.
- DemosGame derives from CasaEngineGame, manually loads AssetCatalog, creates a World, and explicitly manipulates ViewManager.
- This is a distinct runtime composition root and not project-file driven.

3. SandBoxGame

- Startup type: Projects/SandBoxGame/SandBoxGame.cs
- Flow: derives from CasaEngineGame and manually creates world, camera, and primitives.
- This is another runtime composition root.

### Editor startup paths

1. CasaEngine.Editor

- Entry point: CasaEngine.Editor/Program.cs
- Flow: `new CasaEngine.Editor.Game1().Run()`.
- Game1 builds the editor UI with MGUI, subscribes to `ProjectSettingsHelper.ProjectLoaded`, and shows a project launcher.
- The split is clean at the executable boundary, but the engine object graph still relies on the shared CasaEngine.WithEditor build.

2. CasaEngine.EditorUI

- App.xaml uses `StartupUri="ProjectLauncherWindow.xaml"` at line 5.
- App.xaml.cs sets `D3D11Host.UseASingleSharedGraphicsDevice = true` before any viewport is created.
- ProjectLauncherWindow opens MainWindow; MainWindow then calls `ProjectSettingsHelper.Load(projectFileName)` at line 64.
- EngineHost creates `new CasaEngineGame(null, service)` at line 110, sets `_game.IsRunningInGameEditorMode = true` at line 111, then calls `_game.InitializeWithEditor()` at line 116 and `_game.LoadContentWithEditor()` at line 133.
- Input routing is also registered from the editor shell via `RegisterViewInput` at line 211.

3. Legacy Editor

- Editor/Program.cs creates a WinForms `MainForm`.
- The legacy editor defines `EDITOR`, but references CasaEngine.csproj instead of CasaEngine.WithEditor.csproj.
- This is evidence that old and new editor startup paths are not aligned on the same engine composition strategy.

### Composition root assessment

- Separate composition roots do exist for editor and runtime applications.
- The separation is not enforced at the shared-engine boundary because CasaEngineGame, GameManager, World, Entity, ProjectSettingsHelper, and asset classes still change behaviour internally through `#if EDITOR` and `IsRunningInGameEditorMode`.
- The split therefore happens both at startup and deep inside shared code, which is the main architectural risk.

## Inventory of conditional compilation usages

### Summary

`EDITOR` is used pervasively inside the shared CasaEngine project. The architectural pattern is not “editor apps reference editor extensions”; it is “shared runtime types compile in different forms depending on build symbol”.

The most important hotspots are:

| Location | Type / member | What is conditionally compiled | Initial classification |
| --- | --- | --- | --- |
| CasaEngine/CasaEngine.WithEditor.csproj:15,19,23,26 | project properties | Enables `EDITOR` for all configs | Acceptable boundary use |
| CasaEngine/CasaEngine.WithEditor.csproj:29 | project item removal | Removes `Engine/Graphics/**` only in editor variant | Suspicious mixed responsibility |
| CasaEngine/CasaEngine.WithEditor.csproj:40 | project reference | Adds GizmoTools to shared engine variant | Likely architectural issue |
| CasaEngine/Framework/Game/CasaEngineGame.cs:79,94 | `ScreenSizeWidth`, `ScreenSizeHeight` | editor-specific sizing path for shared graphics device | Suspicious mixed responsibility |
| CasaEngine/Framework/Game/CasaEngineGame.cs:350,372,512-538 | update loop and editor wrappers | editor-only events and `InitializeWithEditor` / `DrawWithEditor` methods | Likely architectural issue |
| CasaEngine/Framework/Game/GameManager.cs:32,62,67,105,111 | world/view lifecycle | runtime view bootstrap suppressed in editor builds; editor-only world changed event | Likely architectural issue |
| CasaEngine/Framework/World/World.cs:15,108,144,156,209,281,322,431 | world lifecycle, selection, save | gameplay lifecycle, entity events, selectable pool, save path | Likely architectural issue |
| CasaEngine/Framework/Entities/Entity.cs:33,46,121,144,172,182,192,202,281,349 | entity events and script lifecycle | editor-only events, runtime-only script init, editor save | Likely architectural issue |
| CasaEngine/Framework/Assets/AssetCatalog.cs:25,63 | asset catalog mutation/events | editor-only notifications and mutating API | Likely architectural issue |
| CasaEngine/Framework/Assets/AssetContentManager.cs:29,197 | asset manager | editor-only rename subscription and asset enumeration | Suspicious mixed responsibility |
| CasaEngine/Framework/Project/ProjectSettingsHelper.cs:18,52,58 | project loader/creator | editor-only clear, create, events | Likely architectural issue |
| CasaEngine/Framework/Project/ProjectSettings.cs:45 | settings model | editor-only `ProjectFileOpened` and tool dir state | Suspicious mixed responsibility |
| CasaEngine/Core/Serialization/ISerializable.cs:10 | serialization contract | `Save` exists only in editor builds | Likely architectural issue |
| CasaEngine/Core/Serialization/JsonHelper.cs:185 | serialization helpers | all save helpers are editor-only | Likely architectural issue |
| CasaEngine/Framework/Game/Components/Physics/PhysicsEngineComponent.cs:31,118 | component update and rigid body flags | editor mode disables simulation / changes collision flags | Suspicious mixed responsibility |
| CasaEngine/Framework/Input/InputComponent.cs:91 | input component | editor-only state exposure | Acceptable boundary use |
| CasaEngine/Framework/Game/Components/Editor/GizmoComponent.cs:1 | whole type | editor-only gizmo component | Acceptable boundary use if moved out of shared runtime |
| CasaEngine.EditorUI/Controls/EngineHost.cs:1 | whole type | editor host compiled only for editor | Acceptable boundary use |
| Projects/SandBoxGame/SandBoxGame.cs:19 | shader include path | runtime sample conditionally imports editor-side shaders | Suspicious mixed responsibility |

### Full directive index scope

The appendix contains a file-level index of `EDITOR` directives and symbol definitions for first-party source projects. Generated `obj` files were intentionally excluded from the architectural analysis.

## Findings by category

### Project structure

#### F1. Dual-build shared engine instead of explicit runtime/editor extension assemblies

- Severity: Critical
- Category: Project structure
- Evidence:
  - CasaEngine/CasaEngine.csproj compiles the shared source without `EDITOR`.
  - CasaEngine/CasaEngine.WithEditor.csproj enables `EDITOR` on every configuration at lines 15, 19, 23, and 26.
  - CasaEngine/CasaEngine.WithEditor.csproj removes `Engine/Graphics/**` at line 29.
  - CasaEngine/CasaEngine.WithEditor.csproj adds a GizmoTools dependency at line 40.
- What exists now:
  - The same source tree is compiled into two behavioural variants rather than keeping runtime code in one assembly and editor extensions in another.
- Why this is a separation problem:
  - It makes build output responsible for architectural separation.
  - It obscures which APIs are truly runtime-safe.
  - It encourages shared types to accumulate `#if EDITOR` instead of moving editor behaviour behind explicit boundaries.
- Recommended refactor direction:
  - Keep CasaEngine as runtime-safe shared code.
  - Create one or more editor extension assemblies for editor overlays, gizmos, authoring services, and editor serialization/write paths.

#### F2. Legacy editor and new editors are not aligned on the same engine dependency strategy

- Severity: Medium
- Category: Build/configuration
- Evidence:
  - CasaEngine.Editor references CasaEngine.WithEditor at line 19.
  - CasaEngine.EditorUI references CasaEngine.WithEditor at line 74.
  - Editor/Editor.csproj defines `EDITOR` but references CasaEngine.csproj at line 24.
- Why this matters:
  - The repository currently has at least three editor-era compositions with inconsistent assumptions about which shared engine variant they need.
- Recommended refactor direction:
  - Pick one editor extension model and migrate legacy editor code toward it or freeze it explicitly as legacy.

### Dependency direction

#### F3. GizmoTools leaks into shared world/entity abstractions

- Severity: Critical
- Category: Dependency direction
- Evidence:
  - CasaEngine/Framework/Entities/Components/SceneComponent.cs uses `using GizmoTools;` at line 12 and implements `ITransformable` at line 22.
  - CasaEngine/Framework/World/World.cs uses `using GizmoTools;` at line 16 and exposes `GetSelectableComponents()` at line 437.
  - CasaEngine/Framework/Game/Components/Editor/GizmoComponent.cs is editor-only, but the transform contract it consumes already leaked into shared runtime types.
- What exists now:
  - Runtime entities and components are shaped around an editor gizmo library interface.
- Why this is a separation problem:
  - Dependency direction is backwards: the runtime model depends on the editor manipulation contract.
  - This blocks a clean runtime assembly that is independent of editor tooling.
- Recommended refactor direction:
  - Move the transform/selection contract into a runtime-neutral abstraction owned by CasaEngine, or adapt editor code to runtime-owned scene transforms via an adapter layer.

#### F4. Editor-only UI libraries depend on mixed shared engine assembly rather than runtime-safe core + editor extensions

- Severity: High
- Category: Dependency direction
- Evidence:
  - CasaEngine.EditorUI -> CasaEngine.WithEditor at line 74.
  - CasaEngine.WpfControls -> CasaEngine.WithEditor at line 18.
  - CasaEngine.Editor -> CasaEngine.WithEditor at line 19 and GizmoTools at line 20.
- Why this matters:
  - Editor frontends cannot depend on a runtime-safe engine core plus separate editor services; they depend on a special mixed assembly instead.
- Recommended refactor direction:
  - Split the shared engine from editor-side extension assemblies, then re-point editor frontends to those explicit layers.

### Asset pipeline

#### F5. Asset loading, asset catalog authoring, and project authoring live in the same shared assembly

- Severity: High
- Category: Asset pipeline
- Evidence:
  - CasaEngine/Framework/Assets/AssetContentManager.cs subscribes to `AssetCatalog.AssetRenamed` at line 30.
  - CasaEngine/Framework/Assets/AssetCatalog.cs exposes editor-only mutation events at lines 65-68 and deletes files during `Remove`.
  - CasaEngine/Framework/Project/ProjectSettingsHelper.cs loads projects for runtime but also clears state, creates projects, saves settings, and raises editor events.
  - CasaEngine/Framework/Assets/AssetSaver.cs is entirely editor-only file-writing logic in the shared runtime assembly.
- What exists now:
  - Runtime consumption and editor authoring are colocated in the same namespace and assembly.
- Why this is a separation problem:
  - Runtime code should only require read-only asset resolution and loading.
  - Editor code should own project creation, file deletion, asset save, rename, and authoring catalog events.
- Recommended refactor direction:
  - Keep a runtime read-only asset catalog and asset loader service in CasaEngine.
  - Move authoring mutation, file write/delete, import/reimport, and project creation into editor-only services.

#### F6. Asset and world model classes own editor-only save logic

- Severity: High
- Category: Serialization
- Evidence:
  - CasaEngine/Core/Serialization/ISerializable.cs line 10 makes `Save` exist only under `EDITOR`.
  - CasaEngine/Core/Serialization/JsonHelper.cs line 185 gates all write helpers behind `#if EDITOR`.
  - Shared data classes expose `Save` only in editor builds, for example SpriteData at line 46, Animation2dData at line 23, StaticModelMesh at line 137, World at line 519, Entity at line 357, and many collision/material/shape/component types.
- What exists now:
  - Runtime model types own both load shape and authoring write shape.
- Why this is a separation problem:
  - The authoring format is not isolated from the runtime domain model.
  - Every shared type becomes editor-aware once it implements save.
- Recommended refactor direction:
  - Introduce editor-side writers or DTO mappers for authoring persistence.
  - Keep runtime-side load contracts minimal and independent from editor write logic.

#### F7. Import-time authoring metadata remains on runtime model types

- Severity: Medium
- Category: Asset pipeline
- Evidence:
  - CasaEngine/Framework/Assets/Loaders/StaticModelImporter.cs sets `StaticModelMesh.DiffuseTextureFilePath` during import.
  - CasaEngine/Framework/Graphics/StaticModelMesh.cs documents `DiffuseTextureFilePath` as editor-only import data used by ContentBrowserControl.
- Why this matters:
  - Import bookkeeping is leaking into the runtime mesh type.
- Recommended refactor direction:
  - Move import metadata into an editor-side import result or import context object.

### Rendering and runtime loop

#### F8. CasaEngineGame contains explicit editor execution mode and editor host entry points

- Severity: High
- Category: Rendering
- Evidence:
  - `IsRunningInGameEditorMode` at CasaEngine/Framework/Game/CasaEngineGame.cs:516.
  - `InitializeWithEditor`, `LoadContentWithEditor`, `UpdateWithEditor`, and `DrawWithEditor` at lines 523, 528, 533, and 538.
  - Screen sizing branches under `#if EDITOR` at lines 79 and 94.
  - EngineHost drives those methods directly from CasaEngine.EditorUI/Controls/EngineHost.cs lines 110-133.
- What exists now:
  - The shared runtime game class owns both runtime and editor-hosted execution paths.
- Why this is a separation problem:
  - Composition should differ in the host, not in the shared game class API surface.
- Recommended refactor direction:
  - Move editor host orchestration into an editor-specific bootstrapper or adapter around a runtime-safe game core.

#### F9. World and Entity lifecycle branches are editor-aware

- Severity: High
- Category: Runtime loop
- Evidence:
  - World skips player controller initialization and begin play differently under `EDITOR` around lines 108, 144, 156, and 209.
  - Entity only initializes gameplay proxies in non-editor builds at lines 121 and 144.
  - World also exposes editor-only add/remove/select/save APIs starting around line 431.
- Why this is a separation problem:
  - Shared domain objects change gameplay semantics depending on editor mode.
- Recommended refactor direction:
  - Extract gameplay lifecycle policy from World/Entity into injected services or separate world loaders used by editor versus runtime.

#### F10. Physics and input behaviour are altered inside shared components for editor use

- Severity: Medium
- Category: Input
- Evidence:
  - PhysicsEngineComponent short-circuits update in editor mode at line 31 and changes rigid-body collision flag behaviour at line 118.
  - InputComponent exposes editor-only direct state access at line 91.
  - EngineHost registers per-view input sources through the shared InputRouter at line 211.
- Why this matters:
  - Shared runtime components are taking on editor policies instead of being configured by editor-side services.
- Recommended refactor direction:
  - Inject simulation policy and input source policy from the host instead of branching in shared components.

### UI and tooling

#### F11. Editor rendering pipelines and overlay components live in shared runtime namespaces and are consumed by runtime apps

- Severity: Medium
- Category: UI
- Evidence:
  - EditorViewPipeline is always compiled in CasaEngine/Framework/Rendering/EditorViewPipeline.cs.
  - PreviewPipeline is a shared stub explicitly described as “inspector asset previews”.
  - GridComponent lives in CasaEngine/Framework/Game/Components/Editor/GridComponent.cs with no `#if EDITOR` guard.
  - AxisComponent lives in CasaEngine/Framework/Game/Components/Editor/AxisComponent.cs under `#if !FINAL`, not `#if EDITOR`.
  - CasaEngine.Demos/DemosGame.cs imports `CasaEngine.Framework.Game.Components.Editor` at line 10 and instantiates `new AxisComponent(this)` at line 49.
  - Projects/SandBoxGame/SandBoxGame.cs imports the same namespace at line 8 and instantiates `new GridComponent(this)` / `new AxisComponent(this)` at lines 39-40.
- Why this is a separation problem:
  - Editor-named concepts are usable from runtime applications because they live in the shared engine assembly and are not consistently guarded.
- Recommended refactor direction:
  - Move editor overlays into an editor assembly or rename/reclassify the pieces that are genuinely runtime-safe debugging aids.

#### F12. EngineHost is a valid editor composition root, but it currently reaches deep into shared runtime internals

- Severity: Medium
- Category: Services/DI
- Evidence:
  - EngineHost directly constructs CasaEngineGame and configures editor mode at lines 110-116.
  - EngineHost disables physics at line 137 and manages per-view input registration at line 211.
  - EngineHost also creates per-view editor pipelines, gizmos, grids, axes, and camera proxy initialization.
- Why this matters:
  - The editor host already acts as a composition root, which is good, but it must compensate for missing abstractions by calling editor-specific methods and state on the shared game object.
- Recommended refactor direction:
  - Preserve EngineHost as a composition root, but reduce direct knowledge of shared runtime internals by introducing editor services / adapters.

### Services and abstractions

#### F13. Service boundaries exist for runtime views and UI composition, but not for editor authoring and editor overlays

- Severity: Medium
- Category: Services/DI
- Evidence:
  - Runtime-facing abstractions already exist: `IUIViewRuntimeFactory`, `IUICompositionService`, `IRuntimeViewBootstrapper` in CasaEngineGame.
  - No equivalent explicit abstractions exist for asset writing, project authoring, editor selection, gizmo hosting, or editor overlay rendering.
- Why this matters:
  - The codebase already shows a viable pattern for isolating host-specific concerns, but it is not applied to editor responsibilities.
- Recommended refactor direction:
  - Add editor-only service abstractions where behaviour currently depends on `EDITOR` or `IsRunningInGameEditorMode`.

### Naming and organization

#### F14. “Editor” concepts are physically inside the shared framework namespace, which hides real coupling

- Severity: Low
- Category: Naming/organization
- Evidence:
  - Shared source folder CasaEngine/Framework/Game/Components/Editor contains AxisComponent, GridComponent, EditorViewContext, EditorViewType, and GizmoComponent.
  - PreviewPipeline and EditorViewPipeline are in shared rendering namespaces.
- Why this matters:
  - Folder naming suggests separation, but the assembly boundary does not actually enforce it.
- Recommended refactor direction:
  - Align namespace and project boundaries so names reflect real dependency direction.

## Severity summary

| Finding | Severity | Category | Short summary |
| --- | --- | --- | --- |
| F1 | Critical | Project structure | Dual-build shared engine uses compilation symbols instead of assembly boundaries |
| F3 | Critical | Dependency direction | GizmoTools contract leaks into shared runtime types |
| F4 | High | Dependency direction | Editor frontends depend on mixed engine assembly |
| F5 | High | Asset pipeline | Runtime loading and editor authoring services are colocated |
| F6 | High | Serialization | Shared model classes own editor-only save logic |
| F8 | High | Rendering | CasaEngineGame exposes editor execution API and mode flags |
| F9 | High | Runtime loop | World and Entity lifecycle semantics vary by editor mode |
| F2 | Medium | Build/configuration | Legacy and new editors do not use the same engine dependency strategy |
| F7 | Medium | Asset pipeline | Import metadata remains on runtime model types |
| F10 | Medium | Input | Physics/input policies are embedded in shared components |
| F11 | Medium | UI | Editor overlays and pipelines are compiled in shared runtime namespaces |
| F12 | Medium | Services/DI | EngineHost compensates for missing abstractions by reaching into runtime internals |
| F13 | Medium | Services/DI | Runtime abstractions exist, editor authoring abstractions do not |
| F14 | Low | Naming/organization | Folder and namespace names hide actual coupling |

## Recommended target architecture

The target architecture should be incremental, not a rewrite.

### Recommended separation

1. Keep CasaEngine as the runtime-safe shared engine.

- Runtime world/entity/component model
- Runtime asset loading and read-only asset metadata lookup
- Runtime rendering, input, physics, UI hosting abstractions
- Runtime serialization/loading only

2. Move editor-only behaviour into one or more editor assemblies.

- Asset/project authoring services
- Save/write/export/import/reimport paths
- Gizmo, selection, editor overlays, per-view editor context
- Editor-specific rendering pipelines
- Editor metadata and editor-only view types

3. Preserve separate composition roots.

- Runtime: launcher and game/demo apps
- Editor: WPF/MonoGame editor hosts

### Shared projects that should stay shared

- CasaEngine.Compiler
- CasaEngine.DotNetCompiler
- MGUI.Core, MGUI.Shared, MGUI.FontStashSharp
- Runtime-safe portions of CasaEngine

### Responsibilities that should move to editor-only assemblies or services

- AssetSaver
- project creation and project save flows from ProjectSettingsHelper
- mutating AssetCatalog APIs and file delete/rename/write behaviour
- editor serialization/write helpers currently embedded in `ISerializable` / `JsonHelper`
- GizmoTools-based selection and transformation integration
- EditorViewContext, EditorViewType, EditorViewPipeline, preview/inspector authoring views
- grid/axis/gizmo overlay components unless explicitly reclassified as runtime debug overlays

### Where service interfaces should be introduced

- `IAssetCatalogWriter` or editor-side asset authoring service
- `IProjectAuthoringService`
- `IAssetWriter` / `IAssetImporter`
- `IEditorSelectionService` or runtime-neutral transform adapter consumed by editor gizmos
- `IPhysicsSimulationPolicy` or host-supplied simulation mode abstraction
- `IInputSourcePolicy` or editor-side per-view input host integration

### Where conditional compilation is still acceptable

- Editor application projects and editor-only source files at the executable boundary
- platform- or build-specific packaging concerns in project files
- thin boundary glue where a source file is wholly editor-only and clearly belongs in an editor assembly

### Where conditional compilation should be reduced or removed

- shared domain models such as World, Entity, SceneComponent, AssetInfo, ProjectSettings
- shared serialization contracts
- shared game loop and shared asset services

## Refactor action list for implementation agent

### 1. Introduce a runtime-safe transform/selection abstraction and remove GizmoTools from shared domain types

- Primary type: dependency cleanup
- Scope: remove direct `GizmoTools` dependency from shared runtime-facing types
- Expected code area: SceneComponent, World, any selection contracts, GizmoComponent adapter
- Why it matters: this is the clearest wrong-way dependency in the codebase
- Suggested validation: build CasaEngine.csproj, CasaEngine.WithEditor.csproj, CasaEngine.EditorUI.csproj; verify gizmo selection still works in editor
- Suggested commit message: `Extract runtime transform abstraction from gizmo integration`

### 2. Split AssetCatalog into runtime read-only catalogue and editor authoring facade

- Primary type: asset pipeline separation
- Scope: keep read-only lookup in shared runtime code; move mutation/events/save/delete to editor-side service
- Expected code area: AssetCatalog, AssetContentManager, editor content browser code
- Why it matters: catalog mutation is currently a shared static authoring concern
- Suggested validation: runtime app can still load assets; editor can add, remove, rename assets and content browser stays in sync
- Suggested commit message: `Separate runtime asset catalog from editor catalog mutations`

### 3. Extract AssetSaver and file-writing logic from the shared CasaEngine assembly

- Primary type: service extraction
- Scope: move file write/export helpers to editor-side assembly or service
- Expected code area: AssetSaver, ProjectSettingsHelper.CreateProject, content browser save flows
- Why it matters: authoring persistence should not live in the runtime assembly
- Suggested validation: create a project in editor; save a world or asset; runtime app still loads saved data
- Suggested commit message: `Move asset writing services out of shared runtime assembly`

### 4. Replace `ISerializable.Save` and editor-only `JsonHelper.Save` with editor-side writers or mappers

- Primary type: conditional compilation reduction
- Scope: runtime models keep load contracts; editor writers handle persistence
- Expected code area: ISerializable, JsonHelper, ObjectBase, World, Entity, asset data classes, material classes
- Why it matters: the current save contract forces editor awareness into almost every shared type
- Suggested validation: editor save/load round-trips for representative assets and worlds; CasaEngine.csproj builds without writer APIs
- Suggested commit message: `Decouple runtime models from editor save contracts`

### 5. Extract project authoring from ProjectSettingsHelper into editor-only service(s)

- Primary type: service extraction
- Scope: keep runtime `Load`; move `CreateProject`, `Save`, `ProjectLoaded/Closed` authoring orchestration out of shared static helper
- Expected code area: ProjectSettingsHelper, MainWindow, ProjectLauncherWindow, Game1
- Why it matters: project creation and UI events are editor workflow, not runtime engine responsibility
- Suggested validation: editor open/create project flows still work; launcher/runtime can load existing projects without editor-only events
- Suggested commit message: `Split project loading from editor project authoring`

### 6. Remove editor-mode wrapper methods from CasaEngineGame and replace them with host adapters

- Primary type: composition root split
- Scope: move editor-specific orchestration out of CasaEngineGame public API
- Expected code area: CasaEngineGame, EngineHost, any editor-specific startup helpers
- Why it matters: the game class should not expose `InitializeWithEditor`-style alternate lifecycle methods
- Suggested validation: CasaEngine.EditorUI still boots the hidden engine host and renders views; launcher/runtime startup remains unchanged
- Suggested commit message: `Move editor game host lifecycle out of CasaEngineGame`

### 7. Replace World/Entity editor lifecycle branches with explicit host policy or editor service hooks

- Primary type: composition root split
- Scope: stop embedding editor mode semantics inside gameplay lifecycle
- Expected code area: World, Entity, GameManager, gameplay proxy initialization flow
- Why it matters: current branches alter gameplay semantics inside shared domain types
- Suggested validation: runtime still initializes controllers and gameplay proxies; editor preview world still avoids unwanted play behaviour without compile-time branching
- Suggested commit message: `Extract editor lifecycle policy from world and entity runtime logic`

### 8. Move editor overlay components and editor pipelines out of shared runtime namespaces

- Primary type: naming/organization cleanup
- Scope: relocate or reclassify GridComponent, AxisComponent, GizmoComponent, EditorViewContext, EditorViewType, EditorViewPipeline, PreviewPipeline
- Expected code area: Framework/Game/Components/Editor, Framework/Rendering
- Why it matters: editor concepts are currently compiled into shared runtime assemblies and reused by runtime apps
- Suggested validation: editor viewports still show grid/axis/gizmo; runtime demos either use a renamed runtime debug overlay or no longer reference editor namespaces
- Suggested commit message: `Move editor view overlays into editor-only layer`

### 9. Isolate editor-specific input and simulation policies behind services

- Primary type: service extraction
- Scope: stop branching inside InputComponent and PhysicsEngineComponent for editor-specific behaviour
- Expected code area: InputComponent, PhysicsEngineComponent, EngineHost, editor viewport input providers
- Why it matters: host policy should drive runtime behaviour, not compile-time conditionals inside shared components
- Suggested validation: editor multi-viewport routing still works; runtime input and physics still behave unchanged
- Suggested commit message: `Inject editor input and simulation policies instead of branching in components`

### 10. Retire CasaEngine.WithEditor as the long-term separation mechanism

- Primary type: dependency cleanup
- Scope: once prior tasks land, replace the dual-build variant with explicit runtime + editor extension references
- Expected code area: CasaEngine.csproj, CasaEngine.WithEditor.csproj, editor frontend project references
- Why it matters: true separation should be expressed in assemblies and services, not alternate compilation of the same source tree
- Suggested validation: build runtime apps and editor apps independently; verify editor-only dependencies are absent from the runtime engine build graph
- Suggested commit message: `Replace mixed editor build variant with explicit editor extension assemblies`

## Suggested commit strategy

1. Start with dependency direction fixes before behavioural moves.

- First isolate the GizmoTools dependency and any editor-only contracts leaking into shared types.
- Stop and reassess if the runtime-safe transform abstraction causes broad API fallout.

2. Then split authoring services from runtime services.

- Asset catalog mutation, asset writing, and project authoring should be isolated in small commits.
- Validate editor open/create/save flows after each change.

3. After that, simplify shared model contracts.

- Remove editor-only save responsibilities from runtime models incrementally.
- Validate representative asset save/load round-trips after each type family move.

4. Only then refactor composition and lifecycle boundaries.

- CasaEngineGame editor wrappers, World/Entity lifecycle policy, and editor input/physics policies should be changed after dependency cleanup.
- These changes are riskier and should each live in dedicated commits.

5. Finish by cleaning names, namespaces, and project references.

- Move editor overlay classes out of shared runtime namespaces.
- Replace CasaEngine.WithEditor only after earlier tasks prove the new boundaries.

6. Use one commit per small task.

- Each commit should leave the repo buildable for the touched projects.
- If a task requires touching both runtime and editor hosts, isolate only one architectural concern per commit.

## Risks and validation checklist

### Main risks

- editor project creation and world save flows currently rely on shared static helpers and may break if extracted too aggressively
- gizmo selection and transform editing currently assume runtime entities implement editor-facing contracts
- multi-view editor rendering and input routing currently depend on direct access to CasaEngineGame internals from EngineHost
- runtime demos currently use editor-named overlay classes, so moving them may affect sample behaviour
- the legacy Editor project may hide assumptions not shared by the newer editor hosts

### Validation checklist for the future implementation agent

- Build CasaEngine/CasaEngine.csproj.
- Build CasaEngine.Editor/CasaEngine.Editor.csproj.
- Build CasaEngine.EditorUI/CasaEngine.EditorUI.csproj.
- Build CasaEngine.Launcher/CasaEngine.Launcher.csproj.
- Build CasaEngine.Demos/CasaEngine.Demos.csproj.
- Open an existing project in CasaEngine.EditorUI.
- Create a new project from CasaEngine.EditorUI.
- Load a world in runtime launcher.
- Save a world or asset from the editor and reload it.
- Rename an asset and confirm runtime lookup still resolves correctly.
- Verify editor viewports still route input to the active view and gizmo interaction still works.

### Audit completeness check

- Repository/project inventory included: yes
- Startup/composition analysis included: yes
- Conditional compilation inventory included: yes, with appendix index
- Asset pipeline analysis included: yes
- Dependency direction analysis included: yes
- Concrete findings with severity included: yes
- Recommended target architecture included: yes
- Actionable next-step task list included: yes
- Suggested commit strategy included: yes

Every Critical and High finding above has at least one concrete follow-up action in the action list.

## Appendix: dependency map and directive index

### Dependency map

#### Runtime-facing applications

- CasaEngine.Launcher -> CasaEngine : runtime launcher, acceptable
- CasaEngine.Demos -> CasaEngine : runtime demos, acceptable
- Projects/SandBoxGame -> CasaEngine : runtime sandbox, acceptable
- Projects/CasaEngine.RPGDemo -> CasaEngine : runtime gameplay library, acceptable

#### Editor-facing applications and libraries

- CasaEngine.Editor -> CasaEngine.WithEditor : editor host depends on mixed shared engine, high risk
- CasaEngine.Editor -> GizmoTools : editor-only dependency, acceptable in editor app
- CasaEngine.EditorUI -> CasaEngine.WithEditor : editor WPF host depends on mixed shared engine, high risk
- CasaEngine.EditorUI -> GizmoTools : editor-only dependency, acceptable in editor app
- CasaEngine.WpfControls -> CasaEngine.WithEditor : editor controls depend on mixed shared engine, high risk
- Editor -> CasaEngine : legacy editor references runtime build while defining `EDITOR`, ambiguous risk

#### Shared tooling / support

- CasaEngine.DotNetCompiler -> CasaEngine.Compiler : acceptable
- CasaEngine -> MGUI.Core / MGUI.Shared : acceptable shared runtime UI dependency
- CasaEngine.WithEditor -> MGUI.Core / MGUI.Shared : acceptable shared UI dependency
- CasaEngine.WithEditor -> GizmoTools : unacceptable long-term for shared engine variant

### Directive index

This index is file-level and focuses on first-party source files under active audit scope. Generated `obj` files are excluded.

#### Project files and build symbols

- CasaEngine/CasaEngine.WithEditor.csproj: 15, 19, 23, 26, 29, 40
- CasaEngine.EditorUI/CasaEngine.EditorUI.csproj: 9, 13, 17, 20, 21
- CasaEngine.Editor/CasaEngine.Editor.csproj: 19, 20
- Editor/Editor.csproj: 8, 11, 13, 14, 17

#### Core serialization and math helpers

- CasaEngine/Core/Serialization/ISerializable.cs: 10
- CasaEngine/Core/Serialization/JsonHelper.cs: 185
- CasaEngine/Framework/ObjectBase.cs: 52
- CasaEngine/Core/Maths/Coordinates.cs: 39, 52, 65, 110

#### Parser / utility write paths

- CasaEngine/Core/Parser/Calculator.cs: 70
- CasaEngine/Core/Parser/CalculatorToken.cs: 35
- CasaEngine/Core/Parser/CalculatorTokenBinaryOperator.cs: 115
- CasaEngine/Core/Parser/CalculatorTokenFunction.cs: 43
- CasaEngine/Core/Parser/CalculatorTokenKeyword.cs: 33
- CasaEngine/Core/Parser/CalculatorTokenSequence.cs: 35
- CasaEngine/Core/Parser/CalculatorTokenValue.cs: 38
- CasaEngine/Core/Parser/Parser.cs: 218

#### Asset pipeline and project files

- CasaEngine/Framework/Assets/AssetCatalog.cs: 25, 63
- CasaEngine/Framework/Assets/AssetContentManager.cs: 29, 197
- CasaEngine/Framework/Assets/AssetInfo.cs: 77
- CasaEngine/Framework/Assets/AssetSaver.cs: 10
- CasaEngine/Framework/Assets/ElementFactory.cs: 32, 55
- CasaEngine/Framework/Project/ProjectSettings.cs: 45
- CasaEngine/Framework/Project/ProjectSettingsHelper.cs: 18, 52, 58

#### Asset data models

- CasaEngine/Framework/Assets/Animations/Animation.cs: 118
- CasaEngine/Framework/Assets/Animations/Animation2dData.cs: 21
- CasaEngine/Framework/Assets/Animations/AnimationData.cs: 17
- CasaEngine/Framework/Assets/Animations/FrameData.cs: 21
- CasaEngine/Framework/Assets/Fonts/Font.cs: 13, 143, 194
- CasaEngine/Framework/Assets/Sprites/Collision2d.cs: 19
- CasaEngine/Framework/Assets/Sprites/Socket.cs: 18
- CasaEngine/Framework/Assets/Sprites/SpriteData.cs: 44
- CasaEngine/Framework/Assets/Sprites/SpriteLoader.cs: 28
- CasaEngine/Framework/Assets/Textures/Texture.cs: 126
- CasaEngine/Framework/Assets/TileMap/AutoTileData.cs: 28
- CasaEngine/Framework/Assets/TileMap/StaticTileData.cs: 20
- CasaEngine/Framework/Assets/TileMap/TileData.cs: 38
- CasaEngine/Framework/Assets/TileMap/TileMapData.cs: 29
- CasaEngine/Framework/Assets/TileMap/TileMapLayerData.cs: 19
- CasaEngine/Framework/Assets/TileMap/TileSetData.cs: 57

#### World, entity, gameplay, and rendering lifecycle

- CasaEngine/Framework/World/World.cs: 15, 108, 144, 156, 209, 281, 322, 431
- CasaEngine/Framework/Entities/Entity.cs: 33, 46, 121, 144, 172, 182, 192, 202, 281, 349
- CasaEngine/Framework/Entities/EntityReference.cs: 52
- CasaEngine/Framework/Game/CasaEngineGame.cs: 79, 94, 350, 372, 512
- CasaEngine/Framework/Game/GameManager.cs: 32, 62, 67, 105, 111
- CasaEngine/Framework/GameFramework/GameMode.cs: 430

#### Components, input, physics, and materials

- CasaEngine/Framework/Entities/Components/AnimatedSpriteComponent.cs: 190, 205, 369
- CasaEngine/Framework/Entities/Components/ArcBallCameraComponent.cs: 289
- CasaEngine/Framework/Entities/Components/ArrowComponent.cs: 19
- CasaEngine/Framework/Entities/Components/Box2dCollisionComponent.cs: 46
- CasaEngine/Framework/Entities/Components/BoxCollisionComponent.cs: 48
- CasaEngine/Framework/Entities/Components/Camera3dComponent.cs: 66
- CasaEngine/Framework/Entities/Components/CameraComponent.cs: 102
- CasaEngine/Framework/Entities/Components/CapsuleCollisionComponent.cs: 45
- CasaEngine/Framework/Entities/Components/CircleCollisionComponent.cs: 47
- CasaEngine/Framework/Entities/Components/CylinderCollisionComponent.cs: 45
- CasaEngine/Framework/Entities/Components/EntityComponent.cs: 63
- CasaEngine/Framework/Entities/Components/PhysicsBaseComponent.cs: 68, 111, 233
- CasaEngine/Framework/Entities/Components/PlayerStartComponent.cs: 37
- CasaEngine/Framework/Entities/Components/PrimitiveComponent.cs: 39
- CasaEngine/Framework/Entities/Components/SceneComponent.cs: 11, 21, 378
- CasaEngine/Framework/Entities/Components/SkinnedMeshComponent.cs: 95
- CasaEngine/Framework/Entities/Components/SphereCollisionComponent.cs: 47
- CasaEngine/Framework/Entities/Components/StaticModelComponent.cs: 105
- CasaEngine/Framework/Entities/Components/StaticModelSubMeshComponent.cs: 109
- CasaEngine/Framework/Entities/Components/StaticSpriteComponent.cs: 193
- CasaEngine/Framework/Entities/Components/TileMapComponent.cs: 239
- CasaEngine/Framework/Game/Components/Physics/PhysicsEngineComponent.cs: 31, 118
- CasaEngine/Framework/Input/ButtonsMapping.cs: 24
- CasaEngine/Framework/Input/InputComponent.cs: 91
- CasaEngine/Framework/Input/InputMapping.cs: 79
- CasaEngine/Framework/Materials/LitDiffuseMaterial.cs: 100
- CasaEngine/Framework/Materials/Material.cs: 73
- CasaEngine/Framework/Materials/MaterialBase.cs: 96, 147, 180
- CasaEngine/Framework/Materials/UnlitTextureMaterial.cs: 68

#### Graphics and geometry data

- CasaEngine/Framework/Graphics/SkinnedMesh.cs: 14, 26, 37
- CasaEngine/Framework/Graphics/StaticModel.cs: 25, 73, 116, 149
- CasaEngine/Framework/Graphics/StaticModelMesh.cs: 136
- CasaEngine/Framework/Graphics/StaticModelNode.cs: 49
- CasaEngine/Framework/Graphics/SubMesh.cs: 38
- CasaEngine/Framework/Graphics/Shapes/Box.cs: 57
- CasaEngine/Framework/Graphics/Shapes/Capsule.cs: 56
- CasaEngine/Framework/Graphics/Shapes/Cone.cs: 52
- CasaEngine/Framework/Graphics/Shapes/Cylinder.cs: 60
- CasaEngine/Framework/Graphics/Shapes/Shape2d.cs: 25
- CasaEngine/Framework/Graphics/Shapes/Shape2dCompound.cs: 50
- CasaEngine/Framework/Graphics/Shapes/Shape3d.cs: 18
- CasaEngine/Framework/Graphics/Shapes/Shape3dCompound.cs: 31
- CasaEngine/Framework/Graphics/Shapes/ShapeCircle.cs: 60
- CasaEngine/Framework/Graphics/Shapes/ShapeLine.cs: 54
- CasaEngine/Framework/Graphics/Shapes/ShapePolygone.cs: 12, 72, 83, 91
- CasaEngine/Framework/Graphics/Shapes/ShapeRectangle.cs: 62
- CasaEngine/Framework/Graphics/Shapes/Sphere.cs: 53
- CasaEngine/Engine/Primitives3D/BoxPrimitive.cs: 7, 13
- CasaEngine/Engine/Primitives3D/CapsulePrimitive.cs: 7, 23
- CasaEngine/Engine/Primitives3D/GeometricPrimitive.cs: 77
- CasaEngine/Engine/Primitives3D/PlanePrimitive.cs: 7, 24
- CasaEngine/Engine/Primitives3D/SpherePrimitive.cs: 7, 24

#### Editor integration and shared editor folders inside CasaEngine

- CasaEngine/Framework/Game/Components/Editor/EditorViewContext.cs: 1
- CasaEngine/Framework/Game/Components/Editor/EditorViewType.cs: 1
- CasaEngine/Framework/Game/Components/Editor/GizmoComponent.cs: 1
- CasaEngine/Framework/Game/ComponentOrder.cs: 21
- CasaEngine/Framework/Scripting/IGameplayProxy.cs: 25
- CasaEngine/Framework/Debugger/FpsCounter.cs: 21
- CasaEngine/Framework/Debugger/TimeRuler.cs: 18

#### Editor hosts and editor-specific source files outside CasaEngine

- CasaEngine.EditorUI/Controls/EngineHost.cs: 1
- CasaEngine.EditorUI/Controls/ViewportControl.cs: 1
- CasaEngine.EditorUI/Inputs/RawKeyboardProvider.cs: 1
- CasaEngine.EditorUI/Inputs/RawMouseProvider.cs: 1
- CasaEngine.EditorUI/Inputs/ViewportBoundsCache.cs: 1
- Projects/SandBoxGame/SandBoxGame.cs: 19