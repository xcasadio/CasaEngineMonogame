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

## Cutover plan (EXECUTED as Option A)

**Note:** after de-risking, the user switched from Option B (adapt importers) to **Option A** (editor converts non-glTF → `.glb` via AssimpNetter, then the shared SharpGLTF readers build the assets; old importers deleted). This was lower-risk and matched the original request. Executed:
- Deleted `StaticModelImporter.cs`, `RiggedModelLoader.cs`, `AssimpConverter.cs`; preserved `StaticModelImportResult`/`StaticModelImportedMaterial` in a new file; moved `DefaultTexture` to `RiggedModel`.
- Added `CasaEngine.EditorServices/Import/AssimpToGltfConverter.cs` (AssimpNetter glb2 → SharpGLTF texture-embed → self-contained glb). Removed `AssimpNet` from runtime + central versions; added `AssimpNetter` to EditorServices.
- Rewired `EditorAssetImportService` to convert non-glTF to a temp glb then read via the SharpGLTF readers.
- Readers use `ValidationMode.Skip` (Assimp output is non-conformant but usable); `GltfRiggedModelReader` uses the single top-level node as root and has a `Content/`/`Assets/` path fallback.
- Deleted `RiggedModelMorphImportTests` + `StaticModelImporterTests`; added a self-contained OBJ-based `AssimpToGltfConverterTests`.

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
- ✅ **A3 (DEFERRED to cutover) — Editor AssimpNetter reference.** Initially added `AssimpNetter` to `CasaEngine.EditorServices`, but this made `CasaEngine.Tests` see both Assimp packages (CS0433). Reverted; the editor AssimpNetter reference is now introduced at the cutover (with C), once the runtime is Assimp-free. `AssimpNetter` 6.0.4 version stays registered in central package management (A1). Done by the cutover (Option A, commit `726b2f5a`); status reconciled with the code on 2026-11-14.

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
- ✅ **B4 — Remove Assimp from runtime.** Delete/relocate Assimp usage in `RiggedModelLoader.cs` and `StaticModelImporter.cs`; remove `AssimpNet` `PackageReference` from `CasaEngine.csproj`. Move any still-needed Assimp conversion logic to the editor (Phase C). Build runtime without Assimp. Commit. Done by the cutover (Option A, commit `726b2f5a`); status reconciled with the code on 2026-11-14.

## Phase C — Editor conversion (AssimpNetter → glb)

- ✅ **C1 — Converter.** Add `AssimpToGltfConverter` in `CasaEngine.EditorServices` using AssimpNetter: import any supported non-glTF format and **export `.glb`** (deterministic; embed textures). Unit-test with a small generated source. Commit. Done by the cutover (Option A, commit `726b2f5a`); status reconciled with the code on 2026-11-14.
- ✅ **C2 — Static import wiring.** Update `EditorAssetImportService.ImportFile` so a non-glTF source is converted to a temp/sibling `.glb` (C1), then read by `GltfStaticModelReader` (B1) and serialized to `.staticmodel`. Preserve legacy `.X`/RacingGame effect metadata handling. Commit. Done by the cutover (Option A, commit `726b2f5a`); status reconciled with the code on 2026-11-14.
- ✅ **C3 — Skinned/animation import wiring.** Update `TryImportSeparatedAnimationAssets` / `ImportSeparatedAnimationAssets` to convert non-glTF → `.glb` then read via `GltfRiggedModelReader` (B2) and serialize skeleton/clips. Commit. Done by the cutover (Option A, commit `726b2f5a`); status reconciled with the code on 2026-11-14.

## Phase D — Convert & rewire demo assets

- ✅ **D1 — Convert kid.** Generated `kid_idle.glb`, `kid_walk.glb`, `kid_run.glb` in `CasaEngine.Demos/Content/SkinnedMesh/` via the AssimpNetter converter (textures embedded). `dude.fbx` was unused → deleted, not converted.
- ✅ **D2 — Soldier → glb.** `SoldierLocomotionModelFactory` repointed to the existing `Soldier.glb`.
- ✅ **D3 — kid_idle.model.** `AssetInfos.json` geometry GUID `e6a898ce…` repointed from `kid_idle.FBX` to `kid_idle.glb`; the `.model` wrapper is unchanged.
- ✅ **D4 — Rewire demos.** `SoldierLocomotionModelFactory.cs`, `AnimationBlendDemo.cs` load `.glb`; `SkinnedMeshDemo` loads via the repointed catalog.
- ✅ **D5 — Content includes.** `Content.mgcb` copies the kid `.glb` files; FBX copy entries removed.
- ✅ **D6 — Delete originals.** Removed `kid_idle.FBX`, `kid_walk.FBX`, `kid_run.FBX`, `Soldier.fbx`, `dude.fbx`.

## Phase E — Tests, cleanup, validation

- ✅ **E1 — Migrate Assimp tests.** Rework `RiggedModelMorphImportTests.cs` (currently builds `Assimp.Scene`) to use SharpGLTF or move to an editor-converter test. Commit. Closed another way by the cutover (`726b2f5a`): `RiggedModelMorphImportTests` was deleted, not migrated. No test covers morph targets through SharpGLTF (see E4, "Not checked").
- ✅ **E2 — Pipeline tests.** Update `StaticModelImporterTests.cs` and `EditorAssetImportServiceTests.cs` for the convert→SharpGLTF pipeline; fix the missing `Projects/SampleProject/Skinned/kid_idle.FBX` fixture (provide a small source or retarget). Commit. Closed on 2026-11-14: `StaticModelImporterTests` was deleted by the cutover; `EditorAssetImportServiceTests` now imports an OBJ (conversion path) and `Soldier.glb` (rigged path) through `ImportFile`, and no test refers to the missing `kid_idle.FBX` fixture any more.
- ✅ **E3 — Drop AssimpNet.** Remove the `AssimpNet` entry from `Directory.Packages.props` (now unreferenced). Restore/build. Commit. Done by the cutover (Option A, commit `726b2f5a`); status reconciled with the code on 2026-11-14.
- 🧪 **E4 — Full validation.** Build `CasaEngine.MonoGame.sln` and `CasaEngine.Editor.MonoGame.sln`; run skinned demos (`SkinnedMeshDemo`, `AnimationBlendDemo`, soldier locomotion) and the editor import path once. Record results. Commit any fixes. Run on 2026-11-14, results below; stays 🧪 until the author imports a model by hand in the editor and looks at the rigs.

### E4 results (2026-11-14, branch `chantier/gltf-import-validation`)

Checked:

- Build: `CasaEngine.MonoGame.sln` and `CasaEngine.Editor.MonoGame.sln`, 0 error. `CasaEngine.Tests`: 5359 passed.
- Demos, one capture each, looked at: `Skinned mesh demo` (kid, three columns), `Animation blend demo` (kid) and `Skeletal animation blending` (soldier). Meshes are whole, limbs in place, textures bound. The soldier is lit very bright; this is lighting, not the rig.
- Editor import, through `EditorAssetImportService.ImportFile`, the function the content browser calls (`FileOperationService`), on the sources deleted by the cutover and taken back from the git history into a temporary folder:
  - `Soldier.fbx`: skeleton, 4 clips (`Idle`, `Run`, `TPose`, `Walk`) and `.model` written.
  - `kid_idle.FBX`, `kid_walk.FBX`: skeleton, 1 clip (`Take_001`) and `.model` written, with their PNG textures beside the source.
  - `Car.x` (RacingGame, deleted by `1ccc9238`): **failed** with `InvalidOperationException: Needs to be in SRT representation` in `GltfStaticModelReader.BuildNode`. Cause: the converted glb stores its nodes as matrices and the reader read their rotation without decomposing them. Fixed in both readers (`GetDecomposed()`), with a test; `Car.staticModel` and 6 `.material` are now written. This answers R2 for this file.

Found, not fixed (outside this task):

- A source whose texture file is missing does not import: the converter throws `Image Content is missing` when it saves the glb. Seen on the kid FBX without their PNG files.
- `TryImportSeparatedAnimationAssets` swallows every exception of the rigged read (`catch { return false; }`), so the error reported is the one of the static path that runs next.

Not checked:

- The gesture itself in the editor (dropping a file in the content browser): only the service it calls was run.
- R1 in full: no comparison with the old Assimp output (bind pose, offset matrices); the captures show a correct rig at one instant only. Morph targets have no test.
- The imported assets were not loaded back and drawn; only their files were written.

---

## Open risks / to confirm during execution

- **R1** — SharpGLTF rigged-model parity (bind pose, offset matrices, winding/UV flips, morph targets) must match the current Assimp behavior used by existing animation assets/tests. Validate against `Soldier.glb`.
- **R2** — Legacy `.X` / RacingGame effect metadata is a non-glTF concern; after migration it only flows through the editor convert path. Confirm those scenery assets still import.
- **R3** — `dude.fbx` may be unused; confirm before converting/deleting.
- **R4** — AssimpNetter (6.0.4) keeps the `Assimp` namespace but switched math types to `System.Numerics`, so it is **not** a drop-in for the legacy AssimpNet 4.1 runtime code, and the two packages cannot coexist in one compilation (CS0433). Handled by the Sequencing correction above. Verified at A3/B1.
