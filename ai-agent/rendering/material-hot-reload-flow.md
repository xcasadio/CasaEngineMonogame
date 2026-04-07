# Material Hot Reload Flow

This note maps the current end-to-end hot reload path for material authoring changes.

## Runtime hot reload path

1. A `.material` file is saved through `EditorAssetWriterService`.
2. `EditorAssetWriterService.AssetSaved` publishes `EditorAssetSavedEventArgs` with relative path, full path, asset id, and save source.
3. `CasaEngine.Editor.Game1.OnEditorAssetSaved(...)` filters the event to `.material` files only.
4. The editor tries to recover the saved `MaterialAsset` instance directly when the save originated from `MaterialInspectorPanel`.
5. Matching material inspector panels are refreshed from disk when the save did not originate from that panel itself.
6. `Game1` resolves the material asset id and forwards the reload request to `_editorRuntime.ReloadMaterialAsset(...)`.
7. `CasaEngineGame.ReloadMaterialAsset(...)` refreshes the dependency graph through `MaterialDependencyIndex` and collects all affected material ids.
8. Each affected material id invalidates the runtime `MaterialCache` entry.
9. The authoring cache is updated in place when the saved `MaterialAsset` instance is already available; otherwise `MaterialAuthoringAssetCache` is invalidated for the root asset.
10. Every loaded `StaticModelComponent` is asked to `RefreshResolvedMaterialsDetailed(...)` for the affected material set.
11. `StaticModelComponent` and `StaticModel` recompute resolved slot materials and property-block overrides only where needed.
12. All render views are invalidated through `InvalidateAllViews()` so the next frame redraws with the refreshed runtime materials.
13. Metrics are emitted to logs and the editor diagnostics buffer.

## Preview path in the editor

- `MaterialPreviewViewport` is isolated from the main runtime hot reload loop.
- The preview owns a local `MaterialCompiler` instance and recompiles the currently edited `MaterialAsset` directly with `CompileRuntimeMaterial(...)`.
- After compilation, the preview reapplies the runtime material to its preview meshes and invalidates only the preview render view.
- Practical consequence: preview feedback is immediate even before the saved asset propagates through the runtime caches.

## Key integration points

| Stage | Main code |
| --- | --- |
| Save event emission | `CasaEngine.EditorServices/EditorAssetWriterService.cs` |
| Editor event handling | `CasaEngine.Editor/Game1.cs` |
| Runtime cache invalidation | `CasaEngine/Framework/Game/CasaEngineGame.cs` |
| Static model refresh | `CasaEngine/Framework/Entities/Components/StaticModelComponent.cs` |
| Preview recompilation | `CasaEngine.Editor/Controls/MaterialPreviewViewport.cs` |

## Invariants not to break

- Saving one material must invalidate all dependent child materials through `MaterialDependencyIndex`.
- Runtime refresh must touch both the runtime material cache and the authoring material cache when appropriate.
- Loaded static models must recalculate slot overrides without requiring a project reload.
- Render views must be invalidated after refresh so the new material state is visible without manual intervention.
- The editor preview must stay isolated: it should not depend on the runtime cache invalidation path to show the newly edited values.

## Current gap: shader source refresh

- The save hook is material-specific today; it does not listen for `.fx` or `.fxh` saves.
- `ShaderCompiler` exists as an offline compile wrapper around `mgfxc`, but there is no equivalent editor/runtime hot reload path that recompiles shader source, invalidates loaded `Effect` instances, and refreshes dependent views.
- Result: material parameter edits have a defined hot reload path, shader source edits do not.