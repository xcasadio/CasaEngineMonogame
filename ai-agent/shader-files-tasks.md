# Shader Files Analysis & Tasks

Analysis of `basicEffect.fx`, `skinEffect.fx`, `Lighting.fxh`, `Structures.fxh`, `Common.fxh`, `Macros.fxh` and related shader `UnlitTexture.fx`.
All files under `CasaEngine/Content/Shaders/`.

---

## Workflow Instructions

> **Important:** After completing each task below, the agent **must**:
> 1. Update this file to change the task's status icon from ⬜ to ✅.
> 2. Commit all changes (task work + status update) together:
>    ```
>    git add -A && git commit -m "Shader tasks: complete Task N — <short description>"
>    ```
> 3. Move on to the next task only after committing.

---

## File Structure Assessment

The engine currently has **7 shader files**. Here is the assessment of whether this split is justified:

| File | Lines | Consumers | Verdict |
|---|---|---|---|
| `Macros.fxh` | 38 | `basicEffect.fx` (+ future shaders) | ✅ **Keep** — essential shared macros (`TECHNIQUE`, `DECLARE_TEXTURE`, `SAMPLE_TEXTURE`, shader model defines). Every `.fx` should include this. |
| `Structures.fxh` | 226 | `basicEffect.fx` only | ⚠️ **Keep but trim aggressively** — 17 of 29 structs are dead code (all 9 `PSInput*`, 5 `*NoFog`, 3 dual-texture/env-map). Only 12 structs are actually used, all by `basicEffect.fx`. |
| `Common.fxh` | 45 | `basicEffect.fx` only | ❌ **Merge into `Lighting.fxh`** — only 45 lines, tightly coupled with `Lighting.fxh` (which depends on `CommonVSOutput` defined here). They are always included together and have only one consumer. Merging reduces the include graph. |
| `Lighting.fxh` | 94 | `basicEffect.fx` only | ✅ **Keep** — core Blinn-Phong lighting functions. Will absorb `Common.fxh` content. |
| `basicEffect.fx` | 369 | Main lit shader | ✅ **Keep** — 16 techniques covering basic/vertex-lighting/pixel-lighting × texture/vertex-color variants. |
| `skinEffect.fx` | 300 | Skinning shader | ✅ **Keep as separate file** — skinning logic is distinct enough to justify a separate file. However, it must be refactored to use the shared includes (`Macros.fxh`, `Lighting.fxh`) instead of duplicating everything. |
| `UnlitTexture.fx` | 69 | Unlit material | ✅ **Keep** — clean standalone unlit shader, correctly uses `Macros.fxh`. |

**Conclusion:** 7 files → **6 files** after merging `Common.fxh` into `Lighting.fxh` (added as step in Task 7). The remaining split is well-justified: shared infrastructure (Macros, Structures, Lighting) + per-effect files (basicEffect, skinEffect, UnlitTexture).

### Struct Usage Audit (Structures.fxh)

| Category | Total | Used | Unused |
|---|---|---|---|
| `VSInput*` | 11 | 8 | 3 (`VSInputTx2`, `VSInputTx2Vc`, `VSInputNmTxWeights`) |
| `VSOutput*` | 9 | 4 | 5 (`VSOutputNoFog`, `VSOutputTxNoFog`, `VSOutputTx2`, `VSOutputTx2NoFog`, `VSOutputTxEnvMap`) |
| `PSInput*` | 9 | 0 | 9 (all dead — `basicEffect.fx` reuses `VSOutput*` directly as PS inputs) |
| **Total** | **29** | **12** | **17 (59%)** |

---

## Summary of Current State

| File | Lines | Role |
|---|---|---|
| `Macros.fxh` | 38 | Shader model defines, `TECHNIQUE`, `DECLARE_TEXTURE`, `SAMPLE_TEXTURE` macros |
| `Structures.fxh` | 226 | All VS input/output and PS input structs (29 structs, only 12 used) |
| `Common.fxh` | 45 | `AddSpecular()`, `ComputeCommonVSOutput()`, `SetCommonVSOutputParams` macro |
| `Lighting.fxh` | 94 | `ComputeLights()` (Blinn-Phong), `ComputeCommonVSOutputPixelLighting()` |
| `basicEffect.fx` | 369 | 16 techniques (basic/vertex-lighting/pixel-lighting × texture/vertex-color variants) |
| `skinEffect.fx` | 300 | Skeletal animation: `RiggedModelDraw`, `SkinedDebugModelDraw`, `RiggedModelNormalDraw` |
| `UnlitTexture.fx` | 69 | Standalone unlit shader, 2 techniques (`Unlit_Textured`, `Unlit_Colored`) |

---

## Bugs Found

### BUG-1: `skinEffect.fx` — `PixelShaderRiggedModelDraw` ignores lighting computation
**Severity: High**
**File:** `skinEffect.fx`, line ~156
**Description:** The pixel shader carefully computes diffuse and specular contributions into `result`, then completely discards them and returns the raw texture:
```hlsl
float4 result = (texelColor * AmbientAmt) + (texelColor * diffuse) + (...specular...);
return tex2D(TextureSamplerA, input.TexureCoordinateA); // ← should be: return result;
```
The same bug exists in `PixelShaderRiggedModelNormalDraw` (line ~187): computes `result` but returns raw texture.

### BUG-2: `skinEffect.fx` — normal transform uses `World` instead of `WorldInverseTranspose`
**Severity: Medium**
**File:** `skinEffect.fx`, lines ~130, ~218
**Description:** In both `VertexShaderRiggedModelDraw` and `VertexShaderDebugSkinnedDraw`:
```hlsl
norm = normalize(mul(norm, World));
```
For non-uniform scaling, normals must be transformed by the inverse-transpose of the world matrix. This should use a `WorldInverseTranspose` parameter (not declared in `skinEffect.fx`).

### BUG-3: `basicEffect.fx` — `BasicEffect_PixelLighting_VertexColor` uses wrong VS
**Severity: Medium**
**File:** `basicEffect.fx`, line 362
**Description:** Technique declaration is:
```hlsl
TECHNIQUE(BasicEffect_PixelLighting_VertexColor, VSBasicPixelLighting, PSBasicPixelLighting);
```
It should use `VSBasicPixelLightingVc` (the vertex-color variant), not `VSBasicPixelLighting`. As written, vertex colors are silently ignored.

### BUG-4: `basicEffect.fx` — pixel-lighting techniques hardcode `numLights=3`
**Severity: Medium**
**File:** `basicEffect.fx`, lines 313, 330
**Description:** Both `PSBasicPixelLighting` and `PSBasicPixelLightingTx` call:
```hlsl
ColorPair lightResult = ComputeLights(eyeVector, worldNormal, 3);
```
The `3` is a compile-time `uniform int`, so even when the C# side zeros out light slots, the GPU still evaluates all 3 loops. This wastes GPU cycles and may produce slight precision artefacts with zeroed lights. There is no `PixelLighting_OneLight` technique variant.

### BUG-5: `Lighting.fxh` — `ComputeLights` uses `float3x3` as array storage (fragile)
**Severity: Low**
**File:** `Lighting.fxh`, lines 21-33
**Description:** Light data is accessed via matrix indexing:
```hlsl
lightDirections[i] = float3x3(DirLight0Direction, DirLight1Direction, DirLight2Direction)[i];
```
This pattern is correct for `numLights ≤ 3` but the construct `float3x3(…)[i]` creates a temporary matrix and extracts a row. On some drivers/profiles this may not optimise away and costs extra registers. It also prevents extending beyond 3 lights without rewriting the entire function.

### BUG-6: `skinEffect.fx` — uses `tex2D()` instead of `SAMPLE_TEXTURE()` macro
**Severity: Low**
**File:** `skinEffect.fx`, multiple lines
**Description:** The effect uses `tex2D(TextureSamplerA, uv)` everywhere instead of the engine-standard `SAMPLE_TEXTURE()` macro defined in `Macros.fxh`. Additionally, it doesn't `#include "Macros.fxh"` except for the shader model defines which it redefines locally. This creates an inconsistency and will break if the sampler abstraction changes.

### BUG-7: `skinEffect.fx` — redundant `View` and `Projection` separate matrices
**Severity: Low**
**File:** `skinEffect.fx`, lines 52-53
**Description:** The shader declares `View` and `Projection` separately and multiplies them in the vertex shader (`mul(View, Projection)`), while `basicEffect.fx` uses a pre-multiplied `WorldViewProj`. This is wasteful (extra matrix multiply per vertex) and inconsistent with the engine's C# side which sends `WorldViewProj`.

---

## Structural Issues & Improvement Opportunities

### STRUCT-1: `Structures.fxh` is bloated with unused structs
Many structs (`VSInputTx2`, `VSOutputTx2`, `PSInputTx2`, `VSInputTx2Vc`, `VSOutputTxEnvMap`, `PSInputTxEnvMap`, etc.) are never referenced by any shader file. This adds cognitive load and compile time.

**Task:** Audit all structs, remove any not referenced by any `.fx` file. Keep only those actually used by `basicEffect.fx`, `skinEffect.fx`, or `UnlitTexture.fx`.

### STRUCT-2: `skinEffect.fx` is completely separate from the shared include system
The skinned mesh shader duplicates its own vertex structures, doesn't use `Structures.fxh`, `Common.fxh`, or `Lighting.fxh`, and redefines shader model macros already in `Macros.fxh`. It is essentially a copy-pasted standalone file.

**Task:** Refactor `skinEffect.fx` to share common infrastructure:
- `#include "Macros.fxh"` and remove the local `#if OPENGL` block
- Use `DECLARE_TEXTURE` / `SAMPLE_TEXTURE` macros
- Use shared light parameters from the same cbuffer layout as `basicEffect.fx`
- Use `Lighting.fxh::ComputeLights()` for Blinn-Phong instead of the hand-rolled lighting

### STRUCT-3: No cbuffer separation for per-frame vs per-object data
`basicEffect.fx` puts all constants in a single `cbuffer Parameters : register(b0)`. This means every draw call that changes even one parameter (e.g. `World`) dirties the entire cbuffer, forcing a full upload.

**Task:** Split into at least two cbuffers:
- `cbPerFrame` (b0): `EyePosition`, directional light arrays, `AmbientColor`
- `cbPerObject` (b1): `World`, `WorldInverseTranspose`, `WorldViewProj`, `DiffuseColor`, material properties

### STRUCT-4: Fog system was removed but dead references remain
`Common.fxh` defines `SetCommonVSOutputParamsNoFog` and `Structures.fxh` defines `VSOutputNoFog`, `VSOutputTxNoFog`, `PSInputNoFog`, `PSInputTxNoFog`, `VSOutputTx2NoFog`, `PSInputTx2NoFog` — all relate to a removed fog system. These are dead code.

**Task:** Remove all `*NoFog` structs and macros that are no longer used.

### STRUCT-5: `skinEffect.fx` uses a single-light model, rest of engine uses 3 directional lights
The skinned shader only supports one `WorldLightPosition` (positional, not directional), while the rest of the engine uses 3 directional lights via `LightingContext`. Skinned meshes are visually inconsistent with the rest of the scene.

**Task:** Align `skinEffect.fx` with the engine's directional light model: same 3 lights as `basicEffect.fx`, same parameters, same `Lighting.fxh` functions.

### STRUCT-6: No normal-mapping support anywhere
None of the shaders sample a normal map. The `VSInputNmTx` struct only carries a vertex normal — there is no tangent/bitangent for TBN matrix construction.

**Task (future):** Add a `VSInputNmTxTan` struct with tangent + sign for bitangent reconstruction. Add a `NormalTexture` sampler. Add a `PixelLighting_NormalMap` technique to `basicEffect.fx`.

---

## Detailed Task List

Each task is designed for autonomous execution by an AI agent. Tasks are ordered by priority (bugs first, then structural improvements).

---

### ✅ Task 1 — Fix `skinEffect.fx` return value bug
**Priority:** P0 (Critical bug)
**Files:** `CasaEngine/Content/Shaders/skinEffect.fx`
**Description:**
In `PixelShaderRiggedModelDraw()` (around line 156), the function computes a full lighting result into `float4 result` but then returns `tex2D(TextureSamplerA, ...)` — discarding all lighting. The same bug exists in `PixelShaderRiggedModelNormalDraw()` (around line 187).

**Steps:**
1. In `PixelShaderRiggedModelDraw()`, change `return tex2D(TextureSamplerA, input.TexureCoordinateA);` to `return result;`
2. In `PixelShaderRiggedModelNormalDraw()`, change `return tex2D(TextureSamplerA, input.TexureCoordinateA);` to `return result;`
3. Build the content pipeline / recompile shaders to verify no errors.

---

### ✅ Task 2 — Fix `basicEffect.fx` PixelLighting_VertexColor technique
**Priority:** P0 (Bug)
**Files:** `CasaEngine/Content/Shaders/basicEffect.fx`
**Description:**
The `BasicEffect_PixelLighting_VertexColor` technique on line 362 uses `VSBasicPixelLighting` instead of `VSBasicPixelLightingVc`. This causes vertex colours to be silently ignored.

**Steps:**
1. Change line 362 from:
   ```
   TECHNIQUE(BasicEffect_PixelLighting_VertexColor, VSBasicPixelLighting, PSBasicPixelLighting);
   ```
   to:
   ```
   TECHNIQUE(BasicEffect_PixelLighting_VertexColor, VSBasicPixelLightingVc, PSBasicPixelLighting);
   ```
2. Verify the technique compiles without errors.

---

### ✅ Task 3 — Fix `skinEffect.fx` normal transformation
**Priority:** P1 (Visual bug under non-uniform scale)
**Files:** `CasaEngine/Content/Shaders/skinEffect.fx`
**Description:**
Both vertex shaders transform normals using `mul(norm, World)` which is wrong for non-uniform scaling. They should use `WorldInverseTranspose`.

**Steps:**
1. Add a `float3x3 WorldInverseTranspose;` uniform declaration next to `matrix World;`.
2. In `VertexShaderRiggedModelDraw()`, change `norm = normalize(mul(norm, World));` to `norm = normalize(mul(norm, (float3x3)WorldInverseTranspose));`
3. Do the same in `VertexShaderDebugSkinnedDraw()`.
4. Update the C# side (`SkinnedMeshRendererComponent.cs`) to compute and set `WorldInverseTranspose` before drawing.

---

### ✅ Task 4 — Integrate `skinEffect.fx` with the shared include system
**Priority:** P1 (Consistency)
**Files:** `CasaEngine/Content/Shaders/skinEffect.fx`, `Macros.fxh`
**Description:**
`skinEffect.fx` is a standalone file that duplicates macro definitions, texture declarations, and vertex structures. Integrate it with the shared includes.

**Steps:**
1. Add `#include "Macros.fxh"` at the top (after the header comment).
2. Remove the local `#if OPENGL` / `#define VS_SHADERMODEL` / `#define PS_SHADERMODEL` block (lines 26-30).
3. Replace `Texture2D TextureA;` and `sampler TextureSamplerA = sampler_state { ... };` with `DECLARE_TEXTURE(TextureA, 0);`
4. Replace all `tex2D(TextureSamplerA, uv)` calls with `SAMPLE_TEXTURE(TextureA, uv)`.
5. Verify compilation.

---

### ✅ Task 5 — Unify `skinEffect.fx` lighting with the engine's directional light model
**Priority:** P1 (Visual consistency)
**Files:** `CasaEngine/Content/Shaders/skinEffect.fx`, `CasaEngine/Framework/Game/Components/SkinnedMeshRendererComponent.cs`
**Description:**
The skinned shader uses a single positional `WorldLightPosition` while the rest of the engine uses 3 directional lights via the same cbuffer layout as `basicEffect.fx`. The skinned meshes look completely different from static meshes in the same scene.

**Steps:**
1. In `skinEffect.fx`:
   - Remove `WorldLightPosition`, `LightColor`, `AmbientAmt`, `DiffuseAmt`, `SpecularAmt`, `SpecularSharpness`, `SpecularLightVsTexelInfluence` parameters.
   - Add the same directional-light + material parameters as `basicEffect.fx` (or `#include "Lighting.fxh"` and declare matching cbuffer entries).
   - Rewrite the pixel shader to call `ComputeLights()` from `Lighting.fxh` (or inline equivalent code using the 3 directional lights).
2. In C# `SkinnedMeshRendererComponent.cs`:
   - Stop setting `WorldLightPosition` / `LightColor`.
   - Pass `DefaultLighting` and call `Bind(shader)` to set the same directional-light parameters.
3. After this change, skinned and static meshes will share the same visual lighting model.

---

### ✅ Task 6 — Add `PixelLighting_OneLight` techniques to `basicEffect.fx`
**Priority:** P2 (Performance)
**Files:** `CasaEngine/Content/Shaders/basicEffect.fx`, `Lighting.fxh`
**Description:**
Pixel-lighting techniques always compile `ComputeLights` with `numLights=3`. For scenes with 1 active light, this wastes GPU work (2 lights worth of dot/pow with zero contributions). Add one-light pixel-lighting variants and have `LitDiffuseMaterial.Bind()` / `ShaderVariantLibrary` select the optimal technique.

**Steps:**
1. In `basicEffect.fx`, add new pixel shaders identical to `PSBasicPixelLighting` / `PSBasicPixelLightingTx` but calling `ComputeLights(…, 1)`.
2. Add four new techniques:
   ```
   BasicEffect_PixelLighting_OneLight
   BasicEffect_PixelLighting_OneLight_VertexColor
   BasicEffect_PixelLighting_OneLight_Texture
   BasicEffect_PixelLighting_OneLight_Texture_VertexColor
   ```
3. In C# `LitDiffuseMaterial.Bind()`, check `context.Lighting.ActiveDirectionalLightCount` and select `_OneLight` variants when count == 1.
4. Alternatively, add a `ShaderFeature.OneLight` flag and let `ShaderVariantLibrary` handle selection.

---

### ✅ Task 7 — Remove dead fog-related code & merge `Common.fxh` into `Lighting.fxh`
**Priority:** P2 (Cleanup)
**Files:** `Structures.fxh`, `Common.fxh`, `Lighting.fxh`, `basicEffect.fx`
**Description:**
The fog system has been removed but vestiges remain: `VSOutputNoFog`, `VSOutputTxNoFog`, `PSInputNoFog`, `PSInputTxNoFog`, `VSOutputTx2NoFog`, `PSInputTx2NoFog`, and the macro `SetCommonVSOutputParamsNoFog`.
Additionally, `Common.fxh` (45 lines) is tightly coupled with `Lighting.fxh` and has only one consumer — merge it into `Lighting.fxh` to simplify the include graph (7 files → 6 files).

**Steps:**
1. In `Structures.fxh`, delete: `VSOutputNoFog`, `VSOutputTxNoFog`, `PSInputNoFog`, `PSInputTxNoFog`, `VSOutputTx2NoFog`, `PSInputTx2NoFog`.
2. In `Common.fxh`, delete the `#define SetCommonVSOutputParamsNoFog` macro.
3. Move all remaining content of `Common.fxh` (`AddSpecular()`, `ComputeCommonVSOutput()`, `SetCommonVSOutputParams` macro) into `Lighting.fxh` (before the `ComputeLights` function).
4. Delete `Common.fxh`.
5. In `basicEffect.fx`, remove `#include "Common.fxh"` (now provided by `Lighting.fxh`).
6. Search all `.fx` files for references to deleted structs/macros. If any are found, update them.
7. Build and verify.

---

### ✅ Task 8 — Remove unused structs from `Structures.fxh`
**Priority:** P2 (Cleanup)
**Files:** `Structures.fxh`, all `.fx` files
**Description:**
Many input/output structs are dead code (dual-texture, env-map, etc.). They were carried over from the original XNA BasicEffect and are never referenced.

**Steps:**
1. Grep all `.fx` files for each struct name in `Structures.fxh`.
2. Remove any struct that has zero references outside its own declaration.
3. Expected removals (verified by audit — all 9 `PSInput*` are dead, plus `VSInputTx2`, `VSInputTx2Vc`, `VSInputNmTxWeights`, `VSOutputTx2`, `VSOutputTxEnvMap`). Note: fog-related `*NoFog` structs should already be removed in Task 7.
4. Build and verify no shader breaks.

---

### ✅ Task 9 — Split cbuffers for per-frame vs per-object data
**Priority:** P3 (Performance optimisation)
**Files:** `basicEffect.fx`, `Macros.fxh`, `CasaEngine/Framework/Rendering/Shaders/ShaderWrapper.cs`, `CasaEngine/Framework/Rendering/Shaders/ShaderBindCache.cs`
**Description:**
Currently all shader constants live in a single `cbuffer Parameters : register(b0)`. Every material/object switch dirties all parameters, causing a full GPU constant upload. Splitting into separate cbuffers lets the GPU reuse unchanged data.

**Steps:**
1. In `Macros.fxh`, define new macros:
   ```hlsl
   #define BEGIN_PER_FRAME  cbuffer cbPerFrame : register(b0) {
   #define END_PER_FRAME    };
   #define BEGIN_PER_OBJECT cbuffer cbPerObject : register(b1) {
   #define END_PER_OBJECT   };
   ```
2. In `basicEffect.fx`, move light/eye parameters into `cbPerFrame` and transform/material parameters into `cbPerObject`.
3. In C#, update `ShaderBindCache` to only update the per-frame cbuffer once per flush, and per-object cbuffer per draw call.
4. **Warning:** MonoGame's `Effect` parameter system may not directly support multiple cbuffers — research the MojoShader / mgfxc pipeline first. If it doesn't, wrap with manual constant buffer management.

---

### ✅ Task 10 — Add normal-mapping support
**Priority:** P3 (Feature)
**Files:** `Structures.fxh`, `basicEffect.fx`, `Lighting.fxh`, `ShaderParameterNames.cs`, `LitDiffuseMaterial.cs`
**Description:**
No shader currently supports normal maps. Add tangent-space normal-map support to the pixel-lighting path.

**Steps:**
1. In `Structures.fxh`, add `VSInputNmTxTan` (Position, Normal, TexCoord, Tangent float4 where `.w` = bitangent sign).
2. Add matching `VSOutputPixelLightingTxTan` and `PSInputPixelLightingTxTan` with tangent/bitangent interpolators.
3. In `basicEffect.fx`:
   - Add `DECLARE_TEXTURE(NormalTexture, 1)` (register t1).
   - Add VS that passes tangent to PS.
   - Add PS `PSBasicPixelLightingTxNorm` that samples `NormalTexture`, constructs TBN, perturbs normal, then calls `ComputeLights()`.
   - Add techniques: `BasicEffect_PixelLighting_Texture_NormalMap`, `BasicEffect_PixelLighting_Texture_NormalMap_VertexColor`.
4. In C# `LitDiffuseMaterial`, add `NormalMap` property and select the `_NormalMap` technique when set.
5. In `ShaderParameterNames`, add `NormalTexture` constant.

---

### ⬜ Task 11 — Replace `Lighting.fxh` matrix-indexing pattern with arrays
**Priority:** P3 (Maintainability)
**Files:** `Lighting.fxh`, `basicEffect.fx`
**Description:**
`ComputeLights()` accesses per-light parameters via `float3x3(...)[i]` temporary matrix construction. This is clever but fragile — it breaks if a 4th light is added and relies on compiler optimisation to avoid allocating a real matrix.

**Steps:**
1. Declare light parameters as structured arrays in the cbuffer:
   ```hlsl
   float3 DirLightDirections[3];
   float3 DirLightDiffuseColors[3];
   float3 DirLightSpecularColors[3];
   ```
2. Update `ComputeLights()` to index normally: `DirLightDirections[i]` etc.
3. Update the C# `LightingContext.Bind()` to set array-style parameters (e.g. `shader.SetParameter("DirLightDirections", array)`).
4. **Warning:** MonoGame `EffectParameter.SetValue(Vector3[])` support must be verified. May need to set elements individually: `DirLightDirections[0]`, etc.

---

### ⬜ Task 12 — `skinEffect.fx` — replace separate `View * Projection` with `WorldViewProj`
**Priority:** P3 (Performance micro-optimisation)
**Files:** `CasaEngine/Content/Shaders/skinEffect.fx`, `CasaEngine/Framework/Game/Components/SkinnedMeshRendererComponent.cs`
**Description:**
The skinned VS computes `mul(View, Projection)` per vertex. The C# side should send a pre-multiplied `WorldViewProj` (or `ViewProjection` + per-vertex `World`) to avoid the per-vertex multiply.

**Steps:**
1. Remove `View` and `Projection` matrix declarations from `skinEffect.fx`.
2. Add `float4x4 WorldViewProj;` (or `float4x4 ViewProjection;`).
3. Replace `float4x4 vp = mul(View, Projection); output.Position = mul(pos, vp);` with `output.Position = mul(pos, WorldViewProj);`
   - Note: `pos` is already in world space at that point, so actually `ViewProjection` is needed: `output.Position = mul(pos, ViewProjection);`
4. Update `SkinnedMeshRendererComponent.cs` to send `ViewProjection` instead of `View` and `Projection` separately.

---

## Dependency Graph

```
Task 1 (skin return bug)          — standalone
Task 2 (PL vertex color bug)      — standalone
Task 3 (skin normals)             — standalone, but naturally done with Task 4/5
Task 4 (skin includes)            — standalone, prerequisite for Task 5
Task 5 (skin lighting unify)      — depends on Task 4, relates to Task 3
Task 6 (PL onelight techniques)   — standalone
Task 7 (fog cleanup + merge Common.fxh) — standalone
Task 8 (remove unused structs)    — depends on Task 7 (fog structs removed there)
Task 9 (cbuffer split)            — standalone, research MonoGame first
Task 10 (normal mapping)          — standalone, benefits from Task 11
Task 11 (array light params)      — standalone
Task 12 (skin ViewProjection)     — naturally done with Task 4/5
```

**Recommended execution order:**
1. Tasks 1, 2 (critical bug fixes, independent)
2. Task 3 (skin normals)
3. Tasks 4 → 5 → 12 (skin refactor chain)
4. Tasks 7 → 8 (cleanup chain)
5. Task 6 (one-light optimisation)
6. Task 11 (array lighting)
7. Task 9 (cbuffer split — needs research)
8. Task 10 (normal mapping — feature)
