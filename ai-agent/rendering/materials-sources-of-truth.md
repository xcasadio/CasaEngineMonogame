# Material And Shader Sources Of Truth

This note freezes the current source-of-truth map for the material and shader pipeline.

## Status legend

- `canonical`: data that should be treated as the authoritative source today
- `derived`: recomputed from canonical data for a later pipeline step
- `cache`: persistent acceleration layer, not a semantic source of truth
- `transitional`: active but overlaps another source and should be reduced later

## Matrix

| Type | Current role | Main consumers | Status |
| --- | --- | --- | --- |
| `MaterialDefinition` | Material schema: property keys, defaults, editor metadata | `MaterialCompiler`, editor registries | canonical |
| `MaterialAsset` | Authoring asset: local values, inheritance, render-state hints | `MaterialCompiler`, editor preview, asset save/hot reload | canonical |
| `MaterialInstanceData` | Authoring per-instance overrides without duplicating the asset | `MaterialInstancePropertyBlockMapper`, `StaticModelComponent` | canonical |
| `CompiledMaterial` | Runtime snapshot of resolved values, textures, queue, render states, shader id/features | `MaterialCache`, tests, partial runtime inspection | transitional |
| `MaterialBase` | Runtime bindable object that pushes parameters and selects techniques | renderers, `MaterialRuntimeResolver`, static model override resolution | canonical |
| `MaterialPropertyBlock` | Last-mile per-draw runtime overrides | `StaticMeshRendererComponent`, override mapper | canonical |
| `MaterialDefinitionRegistry` | Built-in definition lookup by id/runtime/legacy type | `MaterialCompiler`, serializers, editor metadata | canonical |
| `MaterialCompiler` | Compiles `MaterialAsset` into `CompiledMaterial` and `MaterialBase` | editor preview, runtime loading, caches | canonical |
| `MaterialRenderStateResolver` | Normalizes queue/transparency/explicit GPU states | `MaterialCompiler` | canonical |
| `MaterialCache` | Cache of compiled/runtime materials keyed by asset id | `MaterialRuntimeResolver`, hot reload invalidation | cache |
| `MaterialAuthoringAssetCache` | Cache of authoring material assets | `MaterialRuntimeResolver`, hot reload invalidation | cache |
| `MaterialDependencyIndex` | Tracks parent-child material relationships for invalidation | `CasaEngineGame.ReloadMaterialAsset` | cache |
| `MaterialRuntimeResolver` | Loads runtime materials through caches and compiler | static model/material load paths | derived |
| `RenderFeatureResolver` | Resolves draw features from runtime material + mesh | static and skinned renderers | derived |
| `EffectiveShaderResolver` | Chooses the effective shader content id for a draw | renderers, compiler | derived |
| `ShaderVariantLibrary` | Maps shader + feature set to a concrete technique/wrapper pair | `StaticMeshRendererComponent` | derived |
| `ShaderWrapper` | Mutable runtime wrapper around an `Effect` and technique binding cache | renderers, materials | derived |

## Information map

| Information | Authoritative source today | Notes |
| --- | --- | --- |
| Material schema and defaults | `MaterialDefinition` | Static registry today; not extensible yet |
| Persisted material values | `MaterialAsset` + parent chain | Resolved in `MaterialCompiler` |
| Persisted per-instance overrides | `MaterialInstanceData` | Safe subset mapped to `MaterialPropertyBlock` |
| Queue / transparency / explicit GPU state | `MaterialAsset` resolved by `MaterialRenderStateResolver` | Reflected into both `CompiledMaterial` and `MaterialBase` |
| Runtime parameter upload | `MaterialBase.Bind(...)` | Still the real draw-time source |
| Runtime technique choice | `MaterialBase.SelectTechnique(...)` plus `ShaderVariantLibrary` | Overlapping/transitional |
| Shader features | `RenderFeatureResolver` in practice | `CompiledMaterial.Features` and `MaterialBase.GetFeatures()` overlap |
| Effective shader id | `EffectiveShaderResolver` in practice | `CompiledMaterial.ShaderContentName` mirrors it |
| Resolved textures | `MaterialBase` at draw time | `CompiledMaterial.Textures` is partial for reflection |
| Hot reload invalidation scope | `MaterialDependencyIndex` + caches | `CasaEngineGame.ReloadMaterialAsset` is the integration point |

## Current gaps

- `CompiledMaterial` is useful for cache/tests, but the static draw path still relies on `MaterialBase`, resolvers, and property blocks instead of a single compiled descriptor.
- `MaterialBase.GetFeatures()` is transitional because the renderer currently trusts `RenderFeatureResolver`.
- `ShaderVariantLibrary` and `MaterialBase.SelectTechnique(...)` overlap for technique selection.
- Reflection state is not fully represented in `CompiledMaterial.Textures` because cubemap handling is still split from the main texture map.