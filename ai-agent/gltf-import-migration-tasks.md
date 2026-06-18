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
| Editor import flow (confirmed) | **Option B** — move `StaticModelImporter`/`RiggedModelLoader`/`AssimpConverter` into the editor on **AssimpNetter** and read non-glTF **directly** (no intermediate glTF). Runtime stays glTF-only via SharpGLTF. |
| Legacy `.X` effect metadata | **Drop** on import (no longer parsed) |
| Assimp-typed tests | **Delete** `RiggedModelMorphImportTests`; **rewrite** the `.X` importer tests against the moved editor importers |

## Cutover plan (confirmed Option B)

This replaces the original Phase C/B4 "convert→glTF" tasks. It is necessarily **one atomic change** (AssimpNet and AssimpNetter cannot coexist in any compilation — CS0433 — and `CasaEngine.Tests` references both runtime and editor-services):

1. **Move** `CasaEngine/Framework/Assets/Loaders/StaticModelImporter.cs`, `CasaEngine/Framework/Assets/Animations/RiggedModelLoader.cs`, and `CasaEngine/Engine/Animations/AssimpConverter.cs` into `CasaEngine.EditorServices` (new editor-side namespace).
2. **Adapt** them from AssimpNet 4.1 to AssimpNetter 6.0.4 (`System.Numerics`): `Node.Transform`/`Bone.OffsetMatrix` → `System.Numerics.Matrix4x4` (fields `M11..M44`, faithful map from old `A1..D4`); `Material.Color*` → `System.Numerics.Vector4` (`.X/.Y/.Z/.W` instead of `.R/.G/.B/.A`); `Mesh.Vertices/Normals/Tangents/BiTangents` → `List<System.Numerics.Vector3>`; `VectorKey.Value`/`QuaternionKey.Value` → `System.Numerics`. Strip the legacy `.X` `EffectInstance` parsing.
3. Add `AssimpNetter` `PackageReference` to `CasaEngine.EditorServices`; **remove** `AssimpNet` from `CasaEngine` (runtime) and from `Directory.Packages.props`.
4. Repoint `EditorAssetImportService` to the moved editor importers; route `.gltf`/`.glb` through the SharpGLTF readers (B1/B2), other formats through the moved Assimp importers.
5. **Delete** `RiggedModelMorphImportTests`; **rewrite** `StaticModelImporterTests` + the `.X` cases of `EditorAssetImportServiceTests` against the moved editor importers (no `.X` effect-metadata assertions).

**AssimpNetter API (verified):** `Assimp.Vector3D`/`Matrix4x4`/`Quaternion`/`Color4D` no longer exist — replaced by `System.Numerics`. The `ToMonoGame*` converters in `AssimpConverter.cs` are the main adaptation surface (map old `A1..D4` field access to `M11..M44`, keeping the same transpose so behaviour matches AssimpNet 4.1).

**Open risk (needs runtime validation):** whether AssimpNetter delivers the node/bone matrices in the same orientation as AssimpNet 4.1 (i.e. the existing `ToMonoGameTransposed()` still applies unchanged). Validate the moved importers by importing `Soldier.fbx`/`kid_*.FBX` in the editor and checking the rig.

## Status legend

- ✅ Done
- 🚧 In progress
- ⏳ Todo
- 🧪 Needs testing
- ⚠️ Blocked

## ⚠️ Sequencing correction (discovered during execution)

Two facts force a change to the original ordering:

1. **AssimpNetter is not a drop-in for AssimpNet 4.1.** AssimpNetter 6.0.4 replaced Assimp's own math types (`Vector3D`, `Matrix4x4`, `Quaternion`, `Color4D`) with `System.Numerics`. The existing runtime Assimp code (`RiggedModelLoader`, `StaticModelImporter`, `AssimpConverter`) does **not** compile against AssimpNetter without a rewrite — and that code is being deleted anyway, so it must not be adapted.
2. **Only one Assimp package can be in a compilation.** AssimpNet and AssimpNetter both own the `Assimp` namespace, so any project that sees both (e.g. `CasaEngine.Tests`, which references runtime **and** editor-services) fails with CS0433.

**Corrected order:**
- Keep `AssimpNet` 4.1 in the **runtime** until the cutover (existing code keeps compiling).
- Add the SharpGLTF readers **alongside** the existing Assimp code (solution stays green).
- Introduce **AssimpNetter** only at the **cutover**, which is necessarily coupled: remove runtime Assimp (B4) **+** move importers to the editor on AssimpNetter (C) **+** migrate the Assimp-typed test (E1) land together so the solution never has both Assimp packages at once.
- The cutover may need a small number of tightly-ordered commits rather than one-per-leaf-task; intermediate full-solution builds are kept green by doing the swap in the right micro-order.

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
- ⏳ **A3 (DEFERRED to cutover) — Editor AssimpNetter reference.** Initially added `AssimpNetter` to `CasaEngine.EditorServices`, but this made `CasaEngine.Tests` see both Assimp packages (CS0433). Reverted; the editor AssimpNetter reference is now introduced at the cutover (with C), once the runtime is Assimp-free. `AssimpNetter` 6.0.4 version stays registered in central package management (A1).

## Phase B — Runtime SharpGLTF readers

- ✅ **B1 — Static glTF reader.** Added `GltfStaticModelReader` (SharpGLTF) at `CasaEngine/Framework/Assets/Loaders/GltfStaticModelReader.cs` building `StaticModel` (geometry, node hierarchy, PBR material metadata, external texture paths) with the same `StaticModelImportResult` contract. Triangle winding reversed to match the legacy `FlipWindingOrder`; glTF UVs kept (no flip). Unit tests in `GltfStaticModelReaderTests.cs` (3 passing). Lives alongside the legacy importer until the cutover. Legacy `.X`/RacingGame effect metadata is intentionally out of scope here (handled editor-side in C2).
- ✅ **B2 — Rigged glTF reader.** Added `GltfRiggedModelReader` (SharpGLTF) at `CasaEngine/Framework/Assets/Animations/GltfRiggedModelReader.cs`. Populates `RiggedModel`'s raw structures (node tree, flat node/bone lists with dummy bone 0, inverse-bind `OffsetMatrixMg`, skinned `VertexPositionTextureNormalTangentWeights` meshes with 4-influence weights, `OriginalAnimations`) then calls `InitializeRuntimeAnimation()` to reuse the existing skeleton/clip builders. Structural smoke test in `CasaEngine.Tests/Animation/GltfRiggedModelReaderTests.cs` (2 passing). **Skinning fidelity (matrix convention, winding, bind pose, root scale/orientation) still requires runtime validation in the GPU demos at the cutover — not verifiable headlessly.** Morph-target import is deferred (follow-up); the modern animation path does not require it for the locomotion demos.

### B2 implementation design (verified against the existing code)

**Key insight — the runtime bridge.** `RiggedModel.InitializeRuntimeAnimation()` builds the modern `SkeletonDefinition` + `AnimationClip`s **from the RiggedModel's own raw structures** (`FlatListToAllNodes`, each node's `BindLocalTransformMg` / `Parent` / `IsThisARealBone` / `OffsetMatrixMg` / `BoneShaderFinalTransformIndex`, and `OriginalAnimations`), via `BuildSkeletonDefinition()` + `BuildAnimationClip()`. So the SharpGLTF reader only needs to populate the **same raw structures** the Assimp loader does, then call `InitializeRuntimeAnimation()`. The skeleton/clip construction (already unit-tested) is reused unchanged.

**Structures to populate (mirror `RiggedModelLoader.CreateModel` order):**
1. `RootNodeOfTree` + recursive `RiggedModelNode` tree (`Name`, `Parent`, `Children`, `BindLocalTransformMg`, `LocalTransformMg`).
2. `FlatListToAllNodes` (ALL nodes, depth-first) and `FlatListToBoneNodes`. Index 0 of the bone list is a **dummy "DummyBone0"** (`BoneShaderFinalTransformIndex = 0`); real bones get palette indices 1+. `ApplyModernPoseToNodes` forces `GlobalShaderMatrixs[0] = Identity`.
3. Per real bone: `IsThisARealBone = true`, `OffsetMatrixMg` = glTF **inverse bind matrix** for that joint, `BoneShaderFinalTransformIndex` = its slot in `FlatListToBoneNodes`.
4. `Meshes` (`RiggedModelMesh[]`): `VertexPositionTextureNormalTangentWeights[]` (Position/Normal/Tangent/BiTangent/TexCoord/Color + `BlendIndices`/`BlendWeights` as Vector4 = up to 4 influences), `Indices` (int[]), textures, `MaterialIndex`, `NodeRefContainingAnimatedTransform`, bounds (Min/Max/Centroid).
5. `OriginalAnimations` (`RiggedAnimation`): per glTF animation, `AnimationName`, `DurationInSeconds`, and `AnimatedNodes` (`RiggedAnimationNodes` with `NodeRef` + raw `Position/PositionTime`, `Rotation/RotationTime`, `Scale/ScaleTime` keyframe lists). The modern path only consumes the raw keyframes; `SetAnimationFpsCreateFrames` (interpolated frames) is optional/legacy.
6. `NumberOfBonesInUse`, `NumberOfNodesInUse`.
7. Then `model.InitializeRuntimeAnimation()`.

**Coordinate conventions (highest risk — R1):**
- **Matrices: direct `System.Numerics.Matrix4x4` → XNA `Matrix` field copy (M11→M11 … M44→M44), NO transpose.** SharpGLTF/System.Numerics already use the XNA row-vector convention; the Assimp path's `ToMonoGameTransposed()` was only needed because Assimp uses column-vector matrices.
- **Winding: reverse each triangle once** (the Assimp rigged path used `PostProcessSteps.FlipWindingOrder`). Use `primitive.GetTriangleIndices()` and emit `(A, C, B)`.
- **UVs: no flip** (glTF top-left origin matches XNA), same as B1.
- **Bind local transform** from `node.LocalMatrix` (direct copy); **inverse bind** from `skin.GetInverseBindMatrix(jointIndex)` (or `skin.GetJoint(i).InverseBindMatrix`).
- **Skin joints → flat bone index**: build a map `glTF joint Node → FlatListToBoneNodes index` so vertex `JOINTS_0` (local joint indices) map to engine palette indices (offset by the dummy bone 0).

**Vertex weights:** read `JOINTS_0` (Vector4 of joint indices) + `WEIGHTS_0` (Vector4) per vertex; map each joint index through the skin's joint list → node → flat bone palette index; pack up to 4 into `BlendIndices`/`BlendWeights`. Meshes without a skin → weight 1.0 on bone 0.

**Open risks needing the user's runtime validation (cannot be checked headlessly):** matrix transpose decision, winding vs the engine's rasterizer state, bind-pose orientation, and root scale/orientation (e.g. Soldier.glb imported lying along Z at ~183 cm in the old path). Validate with `SkinnedMeshDemo`, `AnimationBlendDemo`, and the soldier locomotion demo after the cutover.
- ✅ **B3 — Rewire `ModelLoader`.** `ModelLoader.cs` now uses `GltfRiggedModelReader` and `IsFileSupported` accepts only `.gltf`/`.glb`; the `Assimp` using and `AssimpContext` were removed. Runtime `LoadDirectly<RiggedModel>` of `.fbx` will no longer resolve (demos are repointed to `.glb` in Phase D).
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
- **R4** — AssimpNetter (6.0.4) keeps the `Assimp` namespace but switched math types to `System.Numerics`, so it is **not** a drop-in for the legacy AssimpNet 4.1 runtime code, and the two packages cannot coexist in one compilation (CS0433). Handled by the Sequencing correction above. Verified at A3/B1.
