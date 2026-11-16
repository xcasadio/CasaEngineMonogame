# ADR-0111: A skinned mesh may carry a lit-pbr material

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-rendering-tasks.md` (arbitration P11, tasks T8.1 and T8.2, open point O7)

## Context

Phase 8 of the PBR plan brings the metallic-roughness shading of `LitPbr.fx` to skinned meshes. Verified on the code (task T8.1):

- A skinned mesh reaches the renderer through `SkinnedMeshComponent.Material`, typed `LitDiffuseMaterial` and not serialized, then `SkinnedMeshRendererComponent.AddMesh`. `LitPbrMaterial` and `LitDiffuseMaterial` are sibling classes of `MaterialBase`, so a `LitPbrMaterial` cannot go through that member as it is typed.
- The rigged model carries no material of its own, only the base colour texture and a few flags per mesh.
- `EffectiveShaderResolver.Resolve` returned the skinning shader as soon as `ShaderFeature.Skinned` was present, before it looked at the material family. `skinEffect.fx` had one Blinn-Phong pixel shader behind `RiggedModelDraw` and `RiggedModelDrawDualQuaternion` and did not include `Pbr.fxh`.
- `skinEffect.fx` declares no image-based-lighting constant (`HasIbl`) and no IBL texture, so a skinned draw never makes `EnvironmentShaderBinder` request a generation (arbitration P9).

## Decision

- **A skinned mesh may carry a lit-pbr material, additively.** `SkinnedMeshComponent` and `SkinnedMeshRendererComponent.AddMesh` keep their `LitDiffuseMaterial` member and parameter unchanged. A new property `LitPbrMaterial PbrMaterial` (null by default, not serialized, copied by the copy constructor) and a new `AddMesh` overload with a trailing `LitPbrMaterial` parameter carry the PBR material; the old overload forwards a null. When `PbrMaterial` is not null it wins over `Material` and over the default material of the renderer; when it is null nothing changes. No format, no serialized field and no existing call changes.
- **Two techniques in the existing effect.** `skinEffect.fx` gains `RiggedModelDrawPbr` (linear blend skinning) and `RiggedModelDrawPbrDualQuaternion` (dual quaternion skinning). They reuse the vertex shaders of the Blinn-Phong techniques and a new pixel shader that includes `Pbr.fxh` and shades like `LitPbr.fx` (`ComputePbrSurface`, `EvaluatePbrLights`, `ApplyShadowTint`). `LitPbr.fx` is not modified. The effect gains four constants written by `LitPbrMaterial.Bind`: `BaseColor`, `Metallic`, `Roughness` and `HasBaseColorTexture`.
- **Two stable shader ids, same effect.** `LinearBlendSkinnedPbrEffectShaderId` (`3d4cb6e8-9404-4634-8097-2fef7fd8f963`) and `DualQuaternionSkinnedPbrEffectShaderId` (`a60ccce5-5a8d-4bb8-a5cb-f284a6e75e36`) are registered at the same place as the two existing skinning ids, on the same `skinEffect` effect, each with an alias table (`ShaderVariantLibrary.BuildSkinnedPbrEffectAliases`, `BuildDualQuaternionSkinnedPbrEffectAliases`) that sends every canonical technique name to its PBR technique. `EffectiveShaderResolver.Resolve` returns them (through `SkinningModeShaderResolver.ResolvePbr`) for skinned + lit-pbr family; every other family keeps the Blinn-Phong ids. `LitPbrMaterial.SelectTechnique` is not used on this path (its names are those of `LitPbr.fx`); when the selector has not chosen the technique, the renderer names the PBR technique itself.
- **The mesh texture is borrowed around the bind.** A rigged mesh carries its base colour texture, not the material. When `PbrMaterial.BaseColorTexture` is null the renderer binds the texture of the mesh in its place (`HasBaseColorTexture = 1`) for the duration of `LitPbrMaterial.Bind` and gives the material back with a null texture, as the Blinn-Phong path does with `BasColor`. No allocation per frame.
- **Lighting scope (assumed limit).** Direct lights of `Pbr.fxh` (the same lights as the Blinn-Phong technique), shadows received as the Blinn-Phong technique does (`ReceiveShadows`, shadow tint), a flat diffuse ambient (the world ambient colour, or the diffuse sample of the environment cube when one is bound and the world does not ask for the flat source), the emissive colour. **No image-based lighting, no reflection probes, no normal map (arbitration P11), no metallic-roughness, occlusion or emissive texture, no transparency blending of its own.** The render states (blend, depth, rasterizer, sampler) are those of the `PbrMaterial`, applied by `RenderStateCache` like any material. The shadow casting pass is unchanged.
- **The Blinn-Phong techniques are untouched.** A test pins that `skinEffect.fx` never declares `HasIbl`, `IrradianceCubeTexture` or `PrefilteredSpecularAtlasTexture`.

## Consequences

- A character can be drawn in PBR in the same scene as static PBR meshes, with the same light model, but it is lit without the image-based ambient: against a static PBR mesh in the same environment it lacks the specular ambient and the irradiance of the cube. Adding them later means declaring `HasIbl` and the textures in `skinEffect.fx`, which fits two more samplers in an effect that has five today (a separate decision).
- The glTF PBR channels (metallic, roughness, occlusion, emissive) of a rigged model are not read yet (`GltfRiggedModelReader` reads the base colour only); a `PbrMaterial` supplies one set of factors for the whole model. Reading them per mesh is the subject of the import tasks (ADR-0112 for static models).
- The shader selector and the variant library hold two more ids on the same effect; each draw of a lit-pbr skinned mesh still costs one resolver call and one selector lookup, like the Blinn-Phong draw.
- `SkinnedMeshRendererComponent.AddMesh` has a second overload; existing callers compile unchanged and keep their binary signature.
- The existing test that pinned "a lit-pbr material on a skinned mesh keeps the skinning shader until phase 8" is replaced by the new resolution (both modes).
- Measured on `Skinned mesh demo` frozen (`CASAENGINE_SKINNED_DEMO_FREEZE=1`, 1024 x 768): see the validation note of task T8.2 in `ai-agent/tasks/pbr-rendering-tasks.md`.
