# Animation2D editor surface notes

## Verified existing surface

- Runtime/editor shell: `CasaEngine.Editor/GameEditor.cs` is the concrete MGUI editor shell.
- Content browser: `ContentBrowserPanel` is created by `GameEditor` and file opening is routed through `TryOpenContentItem` / `TryOpen*Asset` methods.
- Existing asset inspectors: material, particle and cutscene assets each have a dedicated `*AssetInspectorPanel` under `CasaEngine.Editor/Controls` and are tracked by dictionaries in `GameEditor`.
- Asset saving path: `EditorAssetWriterService` delegates to `EditorAssetJsonSerializer`, which now serializes `Animation2dData` including frames, parts, tracks and events.
- `EditorViewType.Animation2d` exists, but there is no verified `GameEditorAnimation2d` class and no dedicated `.anim2d` inspector panel yet.

## Minimal entry point

- Add an `Animation2dAssetInspectorPanel` under `CasaEngine.Editor/Controls`.
- Register `.anim2d` opening in `GameEditor.TryOpenContentItem` near material, particle and cutscene handling.
- Track open animation inspectors with an `Animation2d` document panel prefix, mirroring particle/cutscene patterns.
- Phase 7.2 should start read-only: load an `Animation2dData` from JSON and show legacy frames, composed parts, tracks and events.

## Explicit non-goals for this pass

- Do not implement a timeline editor.
- Do not use conceptual classes from the old analysis as existing editor API.
- Do not add WPF UI.