# CasaEngine extensible MonoGame backend

## Target layering

The extensible UI backend keeps a single concrete MonoGame backend owned by CasaEngine and splits responsibilities into explicit layers:

1. Runtime orchestration
   - `CasaDesktopRuntime` owns host integration, view registration, per-frame input refresh, and the backend composition root.
2. Draw orchestration
   - `CasaDrawTransaction` coordinates draw settings, batching context switches, clip requests, and render-target transitions.
3. Primitive rendering
   - `IShapeRenderer2D` hides the concrete primitive implementation.
   - The default implementation preserves the current CasaEngine behavior.
   - `CasaMonoGameBackendOptions.CreateAposShapeRendererFactory()` enables the optional `Apos.Shapes` implementation without changing MGUI code.
   - `Apos.Shapes` lives behind that contract in the optional `CasaEngine.AposShapes` backend assembly.
4. Clipping
   - Logical clip requests stay expressed through `ClipDefinition` and `ClipStrategy`.
   - Backend executors own scissor, stencil, and mask application details.
5. Surface and render-target services
   - UI surfaces keep the MGUI compatibility shim `GetRenderTarget()`.
   - CasaEngine adds an explicit surface target descriptor so backbuffer and offscreen targets are modeled without relying on an implicit `null!` convention.
6. Asset and backend adapters
   - Image and render-target bridging uses an explicit CasaEngine adapter registry.
   - No reflection-based property probing is required in the nominal path.
7. Editor vector overlay canvas
   - `IEditorVectorCanvas` and `IVectorCanvasSession` isolate advanced editor overlays from MGUI.
   - `NvgSharp` lives behind that contract in editor code only.
   - `OverlayViewPipeline` exposes a dedicated vector overlay stage before MGUI composition.

## Boundary rules

- `MGUI.Shared` and `MGUI.Core` remain free of `Apos.Shapes` and `NvgSharp` references.
- `Apos.Shapes` is only allowed in the optional `CasaEngine.AposShapes` backend implementation code.
- `NvgSharp` is only allowed in editor-facing implementation code.
- MGUI contracts remain widget/layout/input oriented and do not become a general vector API.

## Upstream legacy backend split

- The legacy upstream `MainRenderer` implementation now lives in a dedicated `MGUI.MonoGame.LegacyRenderer` project.
- `MGUI.MonoGame.Integration` keeps only the MonoGame contracts and shared text/image infrastructure needed by `MGUI.Core`, `MGUI.FontStashSharp`, and CasaEngine.
- This keeps the main CasaEngine runtime free of a direct `Apos.Shapes` dependency, so the editor path does not inherit the `MonoGame.Extended` version required by the optional Apos backend.

## Surface target model

MGUI still consumes `IUISurface.GetRenderTarget()` as a compatibility shim:

- backbuffer surfaces expose no render target through the legacy API,
- render-target surfaces expose an opaque `IUIRenderTarget` handle,
- CasaEngine backend code resolves the real surface mode through `CasaSurfaceTargetDescriptor`.

This removes semantic ambiguity from CasaEngine code while preserving the current shared MGUI contract.

## Validation matrix

### Automated

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug --no-restore`
- `dotnet build CasaEngine.Editor/CasaEngine.Editor.csproj -c Debug --no-restore`
- `dotnet build CasaEngine.Demos/CasaEngine.Demos.csproj -c Debug --no-restore`
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter CasaMguiBackendOwnershipTests --no-restore`

### Manual

- `UIOverlayDemo`: nominal desktop UI rendering and clipping.
- `WorldSpaceUIDemo`: offscreen surface routing through `CasaRenderSurfaceAdapter`.
- CasaEngine editor shell: viewport overlays, gizmos, and MGUI composition order.
- Editor vector overlay sample: validates the dedicated vector canvas path through the viewport focus overlay pilot.

## Acceptance checkpoints

- Primitive rendering can be swapped without modifying `MGUI.Core`.
- Editor vector overlays can be added without abusing `IUIDrawContext`.
- Backbuffer and render-target surfaces are explicit in CasaEngine code.
- GPU state restoration remains owned by backend services and overlay implementations.