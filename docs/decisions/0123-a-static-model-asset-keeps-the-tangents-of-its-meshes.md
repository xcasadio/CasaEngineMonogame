# ADR-0123: A static model asset keeps the tangents of its meshes

- **Status**: Accepted
- **Date**: 2026-11-16
- **Source**: this chantier: `ai-agent/tasks/static-model-tangents-tasks.md`, decisions D1 to D3 (slice S1d of the RacingGame plan `ai-agent/tasks/rgce-pbr-handover-tasks.md`, envelope approved by the author on 2026-11-16)

## Context

- A `StaticModelMesh` holds its vertices twice: without tangents and, when it has them, with a `Vector4` tangent per vertex (`CasaEngine/Framework/Rendering/Models/StaticModelMesh.cs`). The glTF reader fills the tangents from the `TANGENT` accessor (`GltfStaticModelReader.ReadTangents`).
- The `.staticModel` asset did not store them: `EditorAssetJsonSerializer.SaveStaticModelMesh` wrote `vertices` and `indices` only, and `StaticModelMesh.Load` emptied the tangents.
- A mesh without tangents is drawn without its normal map: `RenderFeatureResolver.AdjustToMesh` removes the `NormalMap` feature.
- So a model read from a glTF with tangents lost its normal map once saved as a `.staticModel` and loaded again. RacingGameCasaEngine cooks its models into `.staticModel` assets and loads them by id; its materials name a normal map.

## Decision

- A mesh of a `.staticModel` may carry a `tangents` array next to `vertices`: one entry per vertex, in the same order, an object `{ "x", "y", "z", "w" }`. `w` is the sign of the bitangent, as in glTF.
- The array is optional. Absent, the mesh has no tangents: an asset written before this decision loads as before.
- The editor serializer writes it only for a mesh that has tangents. A mesh without tangents gains no field.
- An array whose number of entries differs from `vertices` is a data error: it is reported with the mesh name and both numbers, and the mesh loads without tangents.
- `StaticModelMesh.GetTangentVertices()` gives read access to the vertices with their tangents.

## Consequences

- A model imported from a glTF with tangents keeps them through the asset, and its materials draw their normal map.
- An asset with tangents is larger: four more numbers per vertex, in JSON.
- The engine does not compute missing tangents. The import or the conversion provides them.
- The vertices are still written once in `vertices`. The loader rebuilds the vertices with tangents from the two arrays.
- Skinned models and their format are not concerned.
- No migration: existing assets are valid as they are. To gain tangents, a model is imported or cooked again from a source that has them.
