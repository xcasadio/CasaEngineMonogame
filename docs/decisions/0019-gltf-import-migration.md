# ADR-0019: glTF import migration

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `ai-agent/tasks/gltf-import-migration-tasks.md:9-50`

## Context

`ai-agent/tasks/gltf-import-migration-tasks.md` states that existing demo assets in `CasaEngine.Demos/Content/SkinnedMesh/` are converted to `.glb` and their original non-glTF sources deleted (ai-agent/tasks/gltf-import-migration-tasks.md:9). Its "Decisions (confirmed with user)" table originally recorded, as the editor import flow, "Option B — move `StaticModelImporter`/`RiggedModelLoader`/`AssimpConverter` into the editor on AssimpNetter and read non-glTF directly (no intermediate glTF); runtime stays glTF-only via SharpGLTF" (ai-agent/tasks/gltf-import-migration-tasks.md:22). The document's "Cutover plan (EXECUTED as Option A)" section then records that, after de-risking, the user switched from Option B (adapt importers) to Option A: the editor converts non-glTF to `.glb` via AssimpNetter, and the shared SharpGLTF readers build the assets, with the old importers deleted; this is described as lower-risk and matching the original request (ai-agent/tasks/gltf-import-migration-tasks.md:26-28). Option A therefore supersedes and replaces Option B recorded earlier in the same document: Option B kept the direct Assimp-based importers, moved into the editor, reading non-glTF formats directly without an intermediate glTF step (ai-agent/tasks/gltf-import-migration-tasks.md:22); Option A instead routes every non-glTF import through a generated `.glb` consumed by the shared SharpGLTF readers, and removes the direct importers entirely (ai-agent/tasks/gltf-import-migration-tasks.md:26-28).

## Decision

- AssimpNetter (modern fork) replaces `AssimpNet` (source: ai-agent/tasks/gltf-import-migration-tasks.md:15).
- SharpGLTF scope is `SharpGLTF.Core` + `SharpGLTF.Toolkit` (source: ai-agent/tasks/gltf-import-migration-tasks.md:16).
- Runtime skinning is reimplemented via SharpGLTF, keeping the `RiggedModel` output structure (source: ai-agent/tasks/gltf-import-migration-tasks.md:17).
- Only `CasaEngine.Demos/Content/SkinnedMesh` assets are converted; the treejs reference folder is excluded (source: ai-agent/tasks/gltf-import-migration-tasks.md:18).
- Original non-glTF source files are deleted from the repo after conversion (source: ai-agent/tasks/gltf-import-migration-tasks.md:19).
- Conversion is automatic on editor import: dropping a non-glTF file makes the editor generate a `.glb`, and the runtime loads only `.glb` (source: ai-agent/tasks/gltf-import-migration-tasks.md:20).
- Output format is `.glb` (binary, self-contained) (source: ai-agent/tasks/gltf-import-migration-tasks.md:21).
- Editor import flow: Option A — the editor converts non-glTF sources to `.glb` via AssimpNetter, and the shared SharpGLTF readers build the assets from that `.glb`; the old direct-reading importers (`StaticModelImporter`, `RiggedModelLoader`, `AssimpConverter`) are deleted (source: ai-agent/tasks/gltf-import-migration-tasks.md:26-28, replacing the option recorded at :22).
- Legacy `.X` effect metadata is dropped on import, no longer parsed (source: ai-agent/tasks/gltf-import-migration-tasks.md:23).
- Assimp-typed tests are deleted or rewritten: `RiggedModelMorphImportTests` and `StaticModelImporterTests` deleted, `.X` importer tests rewritten against the moved editor importers (source: ai-agent/tasks/gltf-import-migration-tasks.md:24).

## Consequences

- This is a one-time atomic change: `AssimpNet` and `AssimpNetter` cannot coexist in any compilation unit (CS0433), and `CasaEngine.Tests` references both runtime and editor services, so the swap cannot be split across incremental compiling states (ai-agent/tasks/gltf-import-migration-tasks.md:30).
- An open risk was flagged needing runtime validation: whether AssimpNetter delivers node/bone matrices in the same orientation as AssimpNet 4.1, i.e. whether the existing `ToMonoGameTransposed()` still applies unchanged; validated by importing `Soldier.fbx`/`kid_*.FBX` in the editor and checking the rig (ai-agent/tasks/gltf-import-migration-tasks.md:48-50).
- Implementation status observed in code: `AssimpToGltfConverter` exists at `CasaEngine.EditorServices/Import/AssimpToGltfConverter.cs` (tested by `CasaEngine.Tests/EditorServices/AssimpToGltfConverterTests.cs`), and `EditorAssetImportService` (`CasaEngine.EditorServices/EditorAssetImportService.cs`) is rewired to convert non-glTF sources to a temporary `.glb` before reading them via the SharpGLTF readers, consistent with Option A as executed and with `ai-agent/audits/analysis-decisions-inventory.md:110-116` reporting these decisions as "Appliquée".
