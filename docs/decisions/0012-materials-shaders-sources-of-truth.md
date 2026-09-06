# ADR-0012: Materials and shaders sources of truth matrix

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/engine/materials-sources-of-truth.md:3-54`

## Context

The material and shader pipeline has several overlapping types that could each be read as the "true" value for a given piece of information (schema, persisted values, per-instance overrides, GPU state, runtime parameters, technique choice, shader features, resolved textures). `docs/engine/materials-sources-of-truth.md` freezes, for each type, its current role and a status (`canonical`, `derived`, `cache`, `transitional`), and freezes an information map naming the authoritative source for each kind of information.

## Decision

- The source-of-truth matrix in `docs/engine/materials-sources-of-truth.md` is frozen as the current map for the material and shader pipeline: `MaterialDefinition`, `MaterialAsset`, `MaterialInstanceData`, `MaterialDefinitionRegistry`, `MaterialCompiler`, `MaterialRenderStateResolver`, `MaterialBase`, `MaterialPropertyBlock` are `canonical`; `CompiledMaterial` is `transitional`; `MaterialCache`, `MaterialAuthoringAssetCache`, `MaterialDependencyIndex` are `cache`; `MaterialRuntimeResolver`, `RenderFeatureResolver`, `EffectiveShaderResolver`, `ShaderVariantLibrary`, `ShaderWrapper` are `derived` (source: `docs/engine/materials-sources-of-truth.md:14-30`).
- The information map is frozen as the authoritative source per kind of information: material schema/defaults → `MaterialDefinition`; persisted material values → `MaterialAsset` + parent chain (resolved in `MaterialCompiler`); persisted per-instance overrides → `MaterialInstanceData`; queue/transparency/explicit GPU state → `MaterialAsset` resolved by `MaterialRenderStateResolver`; runtime parameter upload → `MaterialBase.Bind(...)`; runtime technique choice → `MaterialBase.SelectTechnique(...)` plus `ShaderVariantLibrary`; shader features → `RenderFeatureResolver` in practice; effective shader id → `EffectiveShaderResolver` in practice; resolved textures → `MaterialBase` at draw time; hot reload invalidation scope → `MaterialDependencyIndex` plus caches (source: `docs/engine/materials-sources-of-truth.md:34-44`).

## Consequences

- `MaterialCompiler` compiles a `MaterialAsset` into both a `CompiledMaterial` and a `MaterialBase`; the static draw path, editor preview, and hot reload all read this matrix to know which type to trust for a given question rather than re-deriving it ad hoc (source: `docs/engine/materials-sources-of-truth.md:16,22`).
- The following gaps are documented in the source as "Current gaps" but are open recommendations, not decided architecture, and are not part of this decision: replacing `CompiledMaterial`'s cache/test role with a single compiled descriptor used by the static draw path; resolving the `MaterialBase.GetFeatures()` vs. `RenderFeatureResolver` overlap; resolving the `ShaderVariantLibrary` vs. `MaterialBase.SelectTechnique(...)` overlap for technique selection; and representing cubemap reflection state fully in `CompiledMaterial.Textures` (source: `docs/engine/materials-sources-of-truth.md:49-54`). No implementation status is claimed for these four items.
