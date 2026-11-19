# ADR-0094: A rigged model imported from a non-glTF source keeps its converted glb

- **Status**: Accepted
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/gltf-import-migration-tasks.md`, question Q-E4, answered by the author on 2026-11-15

## Context

Since the glTF import migration (ADR-0019, Option A), the editor converts a non-glTF source (FBX, X, COLLADA…) to a temporary `.glb`, reads it with the SharpGLTF readers, then deletes it. For a rigged model, `EditorAssetImportService.ImportSeparatedAnimationAssets` wrote the skinned mesh asset (`.model`) with `GeometryAssetId` set to the copied source file. The runtime loads that geometry with SharpGLTF only, so a rigged model imported from FBX did not load (`SchemaException` on `Soldier.fbx`, E4 second run of 2026-11-15). Static models are not affected: their geometry is serialized in the `.staticModel` itself.

## Decision

- When a rigged source needs conversion, the converted `.glb` is kept next to the copied source as `<source name>.glb` (overwritten on a re-import), registered in the asset catalog, and used as the `GeometryAssetId` of the `.model`.
- A `.glb` or `.gltf` source is still its own geometry: no second file is written.
- The `.model` format does not change; only the asset its `geometry_asset_id` points to does.

## Consequences

- Each rigged import from a non-glTF source adds one `.glb` to the project; the copied source stays as the authoring original.
- A source and an unrelated `<name>.glb` in the same folder collide: the import overwrites the `.glb`. Same rule as the `.model` and `.staticModel` written next to the source (skeletons and clips get a unique `_2` suffix instead).
- Models imported before this change still point at their source file; they must be imported again (no migration of existing `.model` files).
- Test: `EditorAssetImportServiceTests.ImportFile_ConvertedRiggedModelKeepsTheGlbAsSkinnedMeshGeometry`.
