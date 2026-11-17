# ADR-0125: The static mesh draw order is fully deterministic

- **Status**: Proposed
- **Date**: 2026-11-17
- **Source**: this chantier: `ai-agent/tasks/draw-order-emissive-shadow-tasks.md`, decisions D1 to D3 (point O16 of `ai-agent/tasks/rgce-pbr-handover-tasks.md` of the RacingGame plan, authorised by the author on 2026-11-17)

## Context

- `StaticMeshRendererComponent.Flush` sorted its `RenderItem` list with `List<T>.Sort(Comparison<T>)`, which is not a stable sort, on `SortKey` alone. Items of equal key are common: the same mesh and material at several transforms, the sub-meshes of one mesh, keys that collide once the three hashes are cut to 16 bits (`SortKeyGenerator`), and transparent items at the same quantised distance (steps of 0.1).
- The mesh part of the key was `VertexBuffer.GetHashCode()`, an identity hash unless the type overrides it, which differs from one run to the next. The material part was `MaterialBase.Id.GetHashCode()`, and `MaterialBase.Id` is `Guid.NewGuid()` by default: the materials the static model resolver generates keep it, so that part differs from one run to the next as soon as a scene has a generated material (only the materials compiled from an asset copy the asset id).
- `OpaqueAndTransparentPasses` finds the start of the transparent items by a binary search on `SortKey`, so the list must stay ascending by `SortKey`.

## Decision

- **D1.** `RenderItem` has an internal field `SubmissionIndex`: the rank of the item in the list of the frame, set when it is added. The comparison (`RenderItemOrder.Comparison`, a cached static delegate: no allocation per sort) compares `SortKey`, then `SubmissionIndex`. The order is therefore total, and the list stays ascending by `SortKey`.
- **D2.** The mesh and material parts of the key are no longer hashes of objects. `RenderItemOrder` gives each distinct vertex buffer (by reference) and each distinct material id (by value, so two instances that share an id keep sharing a key, as before) an id by order of first appearance in the frame, from two tables that `Reset()` empties at the start of `Flush` and that allocate nothing once warm. The shader part is unchanged (hash of the effective shader id, which is stable). `MaterialBase.Id` itself does not change. Two runs that submit the same meshes and materials in the same order get the same keys.
- **D3.** `SceneDepthPrepassGpuTests`, which sorted its own list, sorts it with the same comparison.

## Consequences

- With the same submission, the draw order, and so the image of every item that depends on it (coplanar surfaces, transparent items at the same distance step), no longer depends on identity hashes, on random material ids or on the sort algorithm.
- The state grouping is unchanged: items that share a buffer or a material id still share its part of the key. What changes is the order of the groups inside a queue and a shader: first appearance instead of hash order. An image that depended on the hash order of equal-depth items can change once; nothing in the baseline fingerprints did (see the plan, T1).
- The submission order is now part of the contract: a producer that adds its meshes in a different order gets a different order for equal keys.
- Ids are cut to 16 bits by the key layout. A frame with more than 65536 distinct buffers or materials wraps its ids and shares keys between some of them, which only affects grouping: the order stays deterministic through `SubmissionIndex`.
- The skinned mesh and the 2D renderers have their own ordering and are not changed.
- See `docs/engine/materials-workflow.md`.
