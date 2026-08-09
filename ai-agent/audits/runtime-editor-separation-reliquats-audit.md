# Runtime/Editor separation audit

Date: 2026-03-29

Scope: find remaining runtime/editor separation relics in `CasaEngine` that do not involve `Save(JObject)`.

## Cleaned in this pass

- Renamed `World.GetTransformableComponentsForEditor()` to `GetTransformableObjects()` because it is runtime-owned data exposed to editor services, not editor logic.
- Removed dead commented `#if EDITOR` markers from `ScriptArcBallCamera`.
- Removed dead commented `#if !EDITOR` markers from `ProjectSettingsHelper`.

## Remaining items

### Keep as runtime abstractions

- `InternalsVisibleTo("CasaEngine.EditorServices")` in `CasaEngine`: intentional bridge while editor services still adapt runtime internals.
- `ITransformableObject`: runtime transform contract intentionally consumable by editor tooling.
- `OverlayViewPipeline`, `PickingBuffer`, `IViewHost`, `RenderTargetSurface`, editor-related metadata/comments on `RenderView` and `ViewManager`: these are runtime extensibility points enabling hosted/editor views without taking a direct editor assembly dependency.
- `GameplayExecutionPolicies.EditorPreview` / `EditorSimulation`: legitimate runtime execution modes.

### Keep for API compatibility

- `EditorViewPipeline : OverlayViewPipeline`: obsolete compatibility alias, currently unused but safe to keep.
- `CasaEngine.Framework.Game.Components.Editor.AxisComponent` and `GridComponent`: obsolete wrappers around debug tool implementations, still referenced by editor projects.
- `ComponentUpdateOrder.CasaEngineEditor`: currently unused but public enum value, removing it would be an API break.

### Candidate follow-up cleanup

- `StaticModelMesh.DiffuseTextureFilePath`: no usages found; likely stale import/editor metadata. Remove only after confirming no external tooling depends on it.
- `MaterialBase` editor-facing render-state name helpers: likely used by inspectors; if the goal is stricter separation, move to an editor adapter/helper later.
- Comments mentioning editor/WPF compatibility across rendering code: not a structural dependency, but wording can be normalized in a documentation cleanup pass.

## Recommendation

Next safe cleanup batch should target unused editor metadata and comments first, then revisit public compatibility shims only if you accept minor API churn.