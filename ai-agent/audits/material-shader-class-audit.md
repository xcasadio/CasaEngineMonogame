# Material And Shader Class Audit

This audit classifies the current material/shader architecture types by runtime role.

## Status legend

- `active`: current production path
- `migration target`: should remain but needs architectural refactor
- `transition`: active overlap or compatibility layer
- `suspect dead code`: appears unused or redundant today
- `a optimiser`: active but has a concrete quality or maintainability issue

## CasaEngine/Framework/Materials

| File | Main type | Role | Status | Notes |
| --- | --- | --- | --- | --- |
| `MaterialAsset.cs` | `MaterialAsset` | authoring asset with inheritance and values | active | root authoring source |
| `CompiledMaterial.cs` | `CompiledMaterial` | compiled snapshot for cache/runtime inspection | transition | not yet threaded through draw path |
| `MaterialBase.cs` | `MaterialBase` | runtime bindable material base | active | `GetFeatures()` overlaps current resolver path |
| `LitDiffuseMaterial.cs` | `LitDiffuseMaterial` | lit runtime material | active | imperative technique selection still embedded |
| `UnlitTextureMaterial.cs` | `UnlitTextureMaterial` | unlit runtime material | active | same issue as lit path, smaller surface |
| `Material.cs` | `Material` | legacy multi-texture runtime material | transition | still reachable through legacy definition |
| `MaterialCompiler.cs` | `MaterialCompiler` | compiles authoring data into runtime forms | migration target | hardcoded switch on `definition.Id` |
| `MaterialDefinition.cs` | `MaterialDefinition` | definition schema | active | stable schema concept |
| `MaterialDefinitionRegistry.cs` | `MaterialDefinitionRegistry` | built-in definition lookup | migration target | static closed registry; `TryGetByRuntimeType` appears unused |
| `MaterialPropertyDefinition.cs` | `MaterialPropertyDefinition` | property schema | active | core authoring metadata |
| `MaterialPropertyFlags.cs` | `MaterialPropertyFlags` | schema flags | active | used by compiler/editor metadata |
| `MaterialPropertyGroup.cs` | `MaterialPropertyGroup` | editor grouping | active | presentation metadata |
| `MaterialPropertyOption.cs` | `MaterialPropertyOption` | editor option metadata | active | lightweight metadata |
| `MaterialPropertyType.cs` | `MaterialPropertyType` | serialized property types | active | stable enum |
| `MaterialValue.cs` | `MaterialValue` | typed authoring value wrapper | active | serialization boundary |
| `MaterialValueJsonSerializer.cs` | serializer | json IO for material values | active | infrastructure |
| `MaterialAssetJsonSerializer.cs` | serializer | json IO for material assets | active | infrastructure |
| `MaterialInstanceData.cs` | `MaterialInstanceData` | per-instance authoring overrides | active | core override payload |
| `MaterialInstanceDataJsonSerializer.cs` | serializer | json IO for instance overrides | active | infrastructure |
| `MaterialPropertyBlock.cs` | `MaterialPropertyBlock` | per-draw runtime overrides | active | last-mile override layer |
| `MaterialInstancePropertyBlockMapper.cs` | mapper | maps authoring overrides to property blocks | migration target | hardcoded switch on definition ids |
| `MaterialRenderStateResolver.cs` | resolver | normalizes queue/transparency/GPU states | active | single useful policy point |
| `MaterialRuntimeResolver.cs` | resolver | loads runtime materials via cache/compiler | active | central runtime entry point |
| `MaterialCache.cs` | `MaterialCache` | compiled/runtime cache | active | critical hot reload/cache layer |
| `MaterialAuthoringAssetCache.cs` | cache | cached authoring assets | active | critical hot reload/cache layer |
| `MaterialDependencyIndex.cs` | index | tracks parent-child invalidation graph | active | required by hot reload |
| `MaterialSlotOverride.cs` | `MaterialSlotOverride` | per-slot material swap + instance overrides | active | static model override path |
| `MaterialSlotOverrideJsonSerializer.cs` | serializer | json IO for slot overrides | active | infrastructure |
| `LegacyMaterialAssetAdapter.cs` | adapter | upgrades legacy material payloads | transition | explicit migration helper |

## CasaEngine/Framework/Rendering/Shaders

| File | Main type | Role | Status | Notes |
| --- | --- | --- | --- | --- |
| `RenderFeatureResolver.cs` | `RenderFeatureResolver` | computes features for a draw | migration target | still switches on concrete runtime types |
| `EffectiveShaderResolver.cs` | `EffectiveShaderResolver` | resolves effective shader id/content | migration target | same type-driven policy issue |
| `ShaderVariantLibrary.cs` | `ShaderVariantLibrary` | variant/technique routing | a optimiser | cache determinism and incomplete feature modeling |
| `ShaderVariantKey.cs` | `ShaderVariantKey` | variant key value object | active | correct foundational type |
| `ShaderWrapper.cs` | `ShaderWrapper` | mutable runtime effect wrapper | active | shared mutable technique state requires care |
| `ShaderManager.cs` | `ShaderManager` | effect wrapper registry/loader | active | core shader registry |
| `ShaderBindCache.cs` | `ShaderBindCache` | reduces redundant parameter/state uploads | active | useful render-path optimization |
| `ShaderFeature.cs` | `ShaderFeature` | feature flags | active | central feature vocabulary today |
| `ShaderParameterNames.cs` | constants | shared parameter naming | active | reduces string scatter |
| `RenderShaderSelector.cs` | `RenderShaderSelector` | renderer-facing shader selection helper | transition | overlaps `EffectiveShaderResolver` + variant library |

## CasaEngine.Shaders

| File | Main type | Role | Status | Notes |
| --- | --- | --- | --- | --- |
| `ShaderCompiler.cs` | `ShaderCompiler` | wraps `mgfxc` compilation | a optimiser | error/warning parsing is brittle |
| `ShaderCompiled.cs` | `ShaderCompiled` | compilation result payload | active | stable transport object |
| `ProcessLauncher.cs` | `ProcessLauncher` | external process helper | active | infrastructure |
| `TargetPlatform.cs` | `TargetPlatform` | compiler target enum | active | infrastructure |
| `EffectProcessorDebugMode.cs` | enum | debug/optimize mode for compiler | active | infrastructure |

## Related renderers

| File | Main type | Role | Status | Notes |
| --- | --- | --- | --- | --- |
| `StaticMeshRendererComponent.cs` | renderer | main static draw path | migration target | still resolves from `MaterialBase` instead of compiled descriptor |
| `SkinnedMeshRendererComponent.cs` | renderer | skinned draw path | migration target | separate pipeline, hardcoded shader/technique/lighting |
| `StaticModelComponent.cs` | entity component | material override integration point | active | important override refresh call site |
| `CasaEngineGame.cs` | game runtime | material hot reload integration | active | invalidates caches and refreshes loaded views |

## Explicit transition or suspect dead-code items

- `MaterialBase.GetFeatures(...)`: present on runtime materials, but the renderer currently trusts `RenderFeatureResolver` instead.
- `MaterialDefinitionRegistry.TryGetByRuntimeType(...)`: no current call site found in the engine.
- `Material` plus `legacy-multi-texture` definition: still active, but clearly a legacy compatibility lane.
- `RenderShaderSelector`: useful wrapper today, but its policy surface still overlaps `EffectiveShaderResolver` and `ShaderVariantLibrary`.

## Immediate refactor priority from this audit

1. Make `ShaderVariantLibrary` deterministic and complete enough to reduce imperative `SelectTechnique(...)` usage.
2. Introduce a stable capability contract so `RenderFeatureResolver` and `EffectiveShaderResolver` stop switching on concrete material types.
3. Open `MaterialDefinitionRegistry`, `MaterialCompiler`, and `MaterialInstancePropertyBlockMapper` for extensibility.
4. Merge the skinned path into the same material/shader policy surface or explicitly isolate it behind the same contracts.