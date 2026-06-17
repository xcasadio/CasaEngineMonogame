# glTF Import Migration — SharpGLTF (runtime) + AssimpNetter (editor)

## Goal

Change the 3D model import/loading stack:

- **Runtime (`CasaEngine`)**: load **only** glTF/GLB via **SharpGLTF** (`SharpGLTF.Core` + `SharpGLTF.Toolkit`). No Assimp in runtime.
- **Editor (`CasaEngine.EditorServices`)**: additionally use **AssimpNetter** to **convert any non-glTF format → `.glb`**, then feed the `.glb` through the shared SharpGLTF readers.
- **Existing demo assets** in `CasaEngine.Demos/Content/SkinnedMesh/` are converted to `.glb`; original non-glTF sources are **deleted**.

## Decisions (confirmed with user)

| Topic | Decision |
|---|---|
| Assimp package | **AssimpNetter** (modern fork) replaces `AssimpNet` |
| SharpGLTF scope | **SharpGLTF.Core + SharpGLTF.Toolkit** |
| Runtime skinning | **Yes** — reimplement skinned mesh + animations via SharpGLTF, keeping the `RiggedModel` output structure |
| Assets to convert | **Only** `CasaEngine.Demos/Content/SkinnedMesh` (treejs reference folder excluded) |
| Source files after conversion | **Delete** originals from the repo |
| Conversion trigger | **Automatic on editor import** (drop a non-glTF file → editor generates `.glb`; runtime loads `.glb`) |
| Output format | **`.glb`** (binary, self-contained) |

## Status legend

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## Working rules for the agent

- **One commit per task** (atomic, buildable). Commit only the files touched by that task; never stage the pre-existing unrelated working-tree changes (MGUI submodule, `Projects/RPGDemo/*.dll/.pdb`, `artifacts/validation/*.png`, untracked `treejs/`).
- Update the task's status icon in this file as part of the same task (and commit the doc update with the task).
- No workarounds. If a task is genuinely blocked, mark it ⚠️ and stop to ask.
- Keep public APIs stable where possible; `RiggedModel` / `StaticModel` output shapes must stay compatible.
- Hot paths (Update/Draw) untouched; import/load is cold path.

## Current-state facts (verified)

- `Directory.Packages.props` references `AssimpNet` 4.1.0; no SharpGLTF.
- `AssimpNet` is referenced only by `CasaEngine/CasaEngine.csproj` (runtime).
- Runtime Assimp loaders:
  - `CasaEngine/Framework/Assets/Loaders/StaticModelImporter.cs` (static; **editor-only usage** but located in runtime project).
  - `CasaEngine/Framework/Assets/Animations/RiggedModelLoader.cs` (skinned + animation + morph).
  - `CasaEngine/Framework/Assets/Loaders/ModelLoader.cs` — `IAssetLoader` registered for `RiggedModel` in `AssetLoaderRegistry.cs`; wraps `RiggedModelLoader`.
- Editor import entry: `CasaEngine.EditorServices/EditorAssetImportService.cs` (uses `StaticModelImporter` + `RiggedModelLoader`).
- Runtime direct FBX loads:
  - `CasaEngine.Demos/Demos/SoldierLocomotionModelFactory.cs` → `SkinnedMesh\Soldier.fbx` (`Soldier.glb` already present).
  - `CasaEngine.Demos/Demos/AnimationBlendDemo.cs` → `kid_idle.FBX`, `kid_walk.FBX`, `kid_run.FBX`.
  - `CasaEngine.Demos/Demos/SkinnedMeshDemo.cs` → serialized `Content\SkinnedMesh\kid_idle.model` (`SkinnedMesh`).
- Tests using Assimp types directly: `CasaEngine.Tests/Animation/RiggedModelMorphImportTests.cs`; pipeline test `CasaEngine.Tests/Graphics/StaticModelImporterTests.cs`; `CasaEngine.Tests/EditorServices/EditorAssetImportServiceTests.cs` (references a non-existent `Projects/SampleProject/Skinned/kid_idle.FBX`).
- Real raw 3D assets (non-treejs): `CasaEngine.Demos/Content/SkinnedMesh/` → `Soldier.fbx`, `kid_walk.FBX`, `kid_run.FBX`, `kid_idle.FBX`, `dude.fbx`, plus existing `Soldier.glb`.

---

## Phase A — Packages & project boundaries

- ✅ **A1 — Central package versions.** In `Directory.Packages.props`, added `SharpGLTF.Core` 1.0.6, `SharpGLTF.Toolkit` 1.0.6, and `AssimpNetter` 6.0.4. `AssimpNet` 4.1.0 kept temporarily (removed in E3).
- ✅ **A2 — Runtime SharpGLTF reference.** Added `SharpGLTF.Core` + `SharpGLTF.Toolkit` `PackageReference` to `CasaEngine/CasaEngine.csproj`. Restore + build green.
- ✅ **A3 — Editor AssimpNetter reference.** Added `AssimpNetter` 6.0.4 (`net6.0`, ships as `AssimpNetter.dll`) `PackageReference` to `CasaEngine.EditorServices`. Build green. SharpGLTF flows transitively via the `CasaEngine` project reference. Note: AssimpNetter keeps the `Assimp` namespace, so the transitive `AssimpNet` (4.1.0, `AssimpNet.dll`) must be removed in B4 **before** any direct `Assimp` usage is added in the editor (C1), otherwise `Assimp.*` types are ambiguous (CS0433). No file-name conflict (distinct DLL names).

## Phase B — Runtime SharpGLTF readers

- ⏳ **B1 — Static glTF reader.** Add `GltfStaticModelReader` (SharpGLTF) under `CasaEngine/Framework/Assets/Loaders/` building `StaticModel` (geometry, node hierarchy, materials, texture paths). Match `StaticModelImporter` output (legacy `.X`/RacingGame effect metadata path documented; handled in C2 since it is a non-glTF source). Unit-test against a generated probe glb. Commit.
- ⏳ **B2 — Rigged glTF reader.** Add `GltfRiggedModelReader` (SharpGLTF) building `RiggedModel`: node tree, bind/offset matrices, skin/bones, vertex weights, animation clips, morph targets — feature parity with `RiggedModelLoader`, preserving `RiggedModel` structure and `SkeletonDefinition` / `AnimationClip` outputs. Commit.
- ⏳ **B3 — Rewire `ModelLoader`.** Replace Assimp usage in `ModelLoader.cs` with `GltfRiggedModelReader`; restrict `IsFileSupported` to `.gltf`/`.glb`. Commit.
- ⏳ **B4 — Remove Assimp from runtime.** Delete/relocate Assimp usage in `RiggedModelLoader.cs` and `StaticModelImporter.cs`; remove `AssimpNet` `PackageReference` from `CasaEngine.csproj`. Move any still-needed Assimp conversion logic to the editor (Phase C). Build runtime without Assimp. Commit.

## Phase C — Editor conversion (AssimpNetter → glb)

- ⏳ **C1 — Converter.** Add `AssimpToGltfConverter` in `CasaEngine.EditorServices` using AssimpNetter: import any supported non-glTF format and **export `.glb`** (deterministic; embed textures). Unit-test with a small generated source. Commit.
- ⏳ **C2 — Static import wiring.** Update `EditorAssetImportService.ImportFile` so a non-glTF source is converted to a temp/sibling `.glb` (C1), then read by `GltfStaticModelReader` (B1) and serialized to `.staticmodel`. Preserve legacy `.X`/RacingGame effect metadata handling. Commit.
- ⏳ **C3 — Skinned/animation import wiring.** Update `TryImportSeparatedAnimationAssets` / `ImportSeparatedAnimationAssets` to convert non-glTF → `.glb` then read via `GltfRiggedModelReader` (B2) and serialize skeleton/clips. Commit.

## Phase D — Convert & rewire demo assets

- ⏳ **D1 — Convert kid + dude.** Using the editor converter (C1), produce `kid_idle.glb`, `kid_walk.glb`, `kid_run.glb`, `dude.glb` in `CasaEngine.Demos/Content/SkinnedMesh/`. (Verify `dude.fbx` usage; if unused, flag.) Commit generated `.glb` files.
- ⏳ **D2 — Soldier → glb.** Repoint `SoldierLocomotionModelFactory` to existing `Soldier.glb`; validate skinning/animation parity through the SharpGLTF reader. Commit.
- ⏳ **D3 — Regenerate `kid_idle.model`.** Inspect `kid_idle.model`; if it references the FBX, regenerate/repoint to `kid_idle.glb`. Commit.
- ⏳ **D4 — Rewire demos.** Update `SoldierLocomotionModelFactory.cs`, `AnimationBlendDemo.cs`, `SkinnedMeshDemo.cs` (and XML docs mentioning Assimp 4.1/FBX) to load `.glb`. Commit.
- ⏳ **D5 — Content includes.** Update `CasaEngine.Demos.csproj` content copy rules for the new `.glb` (and `.model`) assets. Commit.
- ⏳ **D6 — Delete originals.** Remove `Soldier.fbx`, `kid_idle.FBX`, `kid_walk.FBX`, `kid_run.FBX`, `dude.fbx` from the repo. Commit.

## Phase E — Tests, cleanup, validation

- ⏳ **E1 — Migrate Assimp tests.** Rework `RiggedModelMorphImportTests.cs` (currently builds `Assimp.Scene`) to use SharpGLTF or move to an editor-converter test. Commit.
- ⏳ **E2 — Pipeline tests.** Update `StaticModelImporterTests.cs` and `EditorAssetImportServiceTests.cs` for the convert→SharpGLTF pipeline; fix the missing `Projects/SampleProject/Skinned/kid_idle.FBX` fixture (provide a small source or retarget). Commit.
- ⏳ **E3 — Drop AssimpNet.** Remove the `AssimpNet` entry from `Directory.Packages.props` (now unreferenced). Restore/build. Commit.
- ⏳ **E4 — Full validation.** Build `CasaEngine.MonoGame.sln` and `CasaEngine.Editor.MonoGame.sln`; run skinned demos (`SkinnedMeshDemo`, `AnimationBlendDemo`, soldier locomotion) and the editor import path once. Record results. Commit any fixes.

---

## Open risks / to confirm during execution

- **R1** — SharpGLTF rigged-model parity (bind pose, offset matrices, winding/UV flips, morph targets) must match the current Assimp behavior used by existing animation assets/tests. Validate against `Soldier.glb`.
- **R2** — Legacy `.X` / RacingGame effect metadata is a non-glTF concern; after migration it only flows through the editor convert path. Confirm those scenery assets still import.
- **R3** — `dude.fbx` may be unused; confirm before converting/deleting.
- **R4** — AssimpNetter (6.0.4) keeps the `Assimp` namespace, same as the legacy AssimpNet (4.1.0). While both are in the graph (A3 → B4 window) there is no compile error because no editor code references `Assimp.*` yet. The first direct `Assimp` usage (C1) must come **after** B4 removes AssimpNet from the runtime, else `Assimp.*` is ambiguous (CS0433). Verified at A3.
