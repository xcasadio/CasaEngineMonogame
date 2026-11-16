# ADR-0109: lit-pbr is a built-in material definition that names its own technique

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-rendering-tasks.md` (decisions D1 and D2, arbitration P8, tasks T3.1, T3.3, T4.1 and T4.2)

## Context

ADR-0013 chose a PBR material that is selected per material, next to `lit-diffuse`, with the metallic-roughness workflow of glTF. Phase 3 of the plan added `Pbr.fxh` (GGX BRDF over the light slots of `Lighting.fxh`), `LitPbr.fx` (four techniques) and its registration (`MaterialShaderFamily.LitPbr`, `EffectiveShaderResolver.LitPbrShaderId`, a `ShaderVariantLibrary` alias table). Nothing emitted that family yet.

Verified on the code:

- `lit-diffuse` is a built-in `MaterialDefinition` (`BuiltInMaterialDefinitions`), compiled by `MaterialCompiler` through a factory registered under its id, with per-instance overrides mapped by `MaterialInstancePropertyBlockMapper` and pushed after `MaterialBase.Bind`.
- The alias table of `LitPbr` names only `LitPbr` and `LitPbr_AlphaTest`. The `LitPbr_NormalMap` techniques sample `NormalTexture` unconditionally and need a mesh with tangents, so they are named by the material, as `LitDiffuseMaterial` names its normal-map technique.
- `LitPbr.fx` defaults nothing: a constant that a bind does not write keeps the value left by the previous draw of the same effect.
- `RenderFeatureResolver.ResolveMaterialFeatures` only raises `NormalMap` when the material also has a base color texture (the `lit-diffuse` rule).
- `Lighting.fxh` multiplies the Lambert term by the light color without a `1/π`; `Pbr.fxh` divides the diffuse lobe by π.

## Decision

- **Definition.** `lit-pbr` is a built-in definition (`BuiltInMaterialDefinitions`), runtime type `LitPbrMaterial`, compiled by a factory registered under `lit-pbr` in `MaterialCompiler`, with a new override mapper. Its keys: `base_color_texture`, `normal_texture`, `metallic_roughness_texture`, `occlusion_texture`, `emissive_texture` (texture assets), `base_color` (color, flagged `AffectsTransparency`), `metallic`, `roughness`, `occlusion_strength`, `normal_scale` (floats), `emissive_color` (Vector3), `alpha_cutoff` (float, Rendering group). It has no legacy aliases (no pre-existing file uses it). Defaults: white, metallic 0, roughness 0.5, no emission, occlusion strength 1, normal scale 1, alpha cutoff 0.5. The metallic and roughness defaults describe a plain dielectric, not the glTF defaults of 1 and 1; the glTF import (T9.2) writes every value explicitly.
- **Family and shader.** `LitPbrMaterial.GetShaderCapabilities` reports `MaterialShaderFamily.LitPbr`, which `EffectiveShaderResolver` resolves to `LitPbr.fx`. A transparent or alpha-tested draw is derived from the queue and the alpha of `base_color`, by the same rule as `lit-diffuse`.
- **The material names its technique.** `LitPbrMaterial.GetTechniqueName(features)` returns `LitPbr[_NormalMap][_AlphaTest|_Transparent]` (`_AlphaTest` wins when both features are set, the alpha-test queue drops `Transparent`). `RequiresMaterialTechniqueSelection` is true when the selector chose nothing or when the `NormalMap` or `Transparent` feature is set (the alias table cannot express the first and keeps its two names for the second); otherwise the alias table result stands. For the `LitPbr` family only, `RenderFeatureResolver` raises `NormalMap` as soon as a normal texture is present, with or without a base color texture; the `lit-diffuse` rule is unchanged. The tangent check (`RenderFeatureResolver.Resolve` with a mesh) only exists on the non-compiled path: `MaterialCompiler` computes the features without the mesh.
- **Transparent blending (T5.1).** The `_Transparent` techniques output a premultiplied colour: `(direct diffuse + ambient diffuse) x alpha + specular + emissive`, alpha of the output = alpha of the base colour, the diffuse attenuation applied before the shadow tint. The specular highlight and the emission are not attenuated by the alpha, so a glass surface keeps its highlights (the glTF alpha-blend model with premultiplied output; no transmission, no refraction). The blend state and the depth state are not new: a compiled `lit-pbr` in the `Transparent` queue already gets `BlendState.AlphaBlend` (One, InverseSourceAlpha in MonoGame) and `DepthStencilState.DepthRead` from `MaterialRenderStateResolver`. The opaque and alpha-test techniques are unchanged (they pass an opacity of 1 to the shared shading).
- **Every bind writes every constant.** `LitPbrMaterial.Bind` writes `BaseColor`, `Metallic`, `Roughness`, `EmissiveColor`, `OcclusionStrength`, `NormalScale`, `AlphaCutoff` (0 outside the `AlphaTest` queue) and the four `Has...Texture` flags on every call, because the shader defaults nothing. `DiffuseColor`, `SpecularColor` and `SpecularPower` are declared for `Lighting.fxh` but never read by the PBR shading, and are not written.
- **Per-instance overrides.** `base_color`, `metallic`, `roughness` and `emissive_color` are mapped to `BaseColor`, `Metallic`, `Roughness` and `EmissiveColor` in the property block, applied after `Bind`. They change neither the queue, the blend state nor the technique; an override of the alpha of `base_color` does not change the transparency of the draw.
- **Radiance rule.** Both PBR lobes, diffuse and specular, are driven by the diffuse color of the light, already multiplied by its intensity. The specular color of a light, an authoring field of the Blinn-Phong model, is ignored by the PBR shading. Consequence of the BRDF: at equal light intensity a `lit-pbr` surface is π times darker in its diffuse term than a `lit-diffuse` surface; the flat ambient term is not divided by π.

## Consequences

- No change for existing worlds and assets: the definition is new, `lit-diffuse` and `unlit-texture` compile and render as before, and the captures of the legacy demos keep their hashes.
- Scenes that mix both materials need a PBR light intensity about π times higher to look as bright; a project that wants matching looks sets the intensity per material family, or waits for the exposure control of the HDR pipeline.
- A normal map on a mesh without tangents is only caught on the non-compiled path (limit to document with the material documentation of T10.3). On the compiled path the `NormalMap` technique would still be selected for such a mesh.
- A new material key is a change of the `.material` format: it is additive (an absent key reads its default), and a later renaming needs a legacy alias.
- Skinned meshes keep the skinning shader until phase 8, and a transparent PBR blend is the subject of task T5.1.
