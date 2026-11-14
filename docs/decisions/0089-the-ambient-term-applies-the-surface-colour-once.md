# ADR-0089: The ambient term applies the surface colour once

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/lighting-ambient-shadow-tasks.md` (decision D7, task E1), approved with the RacingGame plan `ai-agent/tasks/rgce-3d-lighting-shadows-tasks.md`

## Context

The lit forward shaders compose a pixel as `ComposeLitSurfaceColor(albedo, directDiffuse, ambientTerm, emissive) = albedo * (directDiffuse + ambientTerm) + emissive` (`Lighting.fxh`), where `albedo` is the base (texture) colour. The ambient term handed to it was already multiplied by the base colour:

- `ComputeBaseAmbientTerm(baseColor, worldNormal)` in `LitForward.fx` returned `baseColor * DiffuseColor.rgb * ambient`, called with `color.rgb` (the sampled texture) from six pixel shaders (`PSBasicPixelLighting`, `PSBasicPixelLightingTx`, `PSBasicPixelLightingOneLight`, `PSBasicPixelLightingTxOneLight`, `PSBasicPixelLightingTxNorm`, `PSBasicPixelLightingTxNormReflection`);
- `ComputeSkinnedAmbientContribution(baseColor, worldNormal)` in `skinEffect.fx` did the same for `PixelShaderRiggedModelDraw`.

The ambient light reaching the screen was therefore `texture^2 * DiffuseColor * ambient`, while the direct light was `texture * DiffuseColor * N.L * light`. Measured on the validation demo (a receiver made of a solid 128/255 texture, white diffuse colour, material ambient 1, world ambient 0.5, no light on the pixel): 32 per channel, where the intended `texture * ambient` is 64 (`128/255 * 0.5 * 255`). The output is linear (no gamma, no tone mapping), so the prediction is exact. The error grows as the texture darkens (a texture value of 0.25 loses three quarters of its ambient).

Other composers of the term were checked: `Lighting.fxh` only defines `ComputeAmbientTerm` and `ComputeEnvironmentDiffuseTerm` (colour times colour, no surface term); the vertex-lighting and one-light vertex techniques of `LitForward.fx` compute no ambient at all; `LitDiffuseMaterial` only binds `MaterialAmbientColor`, `DiffuseColor` and `EmissiveColor`; no other shader or CPU path in the repository reads the ambient uniforms.

## Decision

- `ComputeBaseAmbientTerm(worldNormal)` and `ComputeSkinnedAmbientContribution(worldNormal)` no longer take or apply the base colour. They return `DiffuseColor.rgb * ambient`. `DiffuseColor` stays in the ambient term because it is the material's base tint (it already multiplies the direct light in `AccumulateLight`): removing it would desaturate the ambient of untextured materials.
- `ComposeLitSurfaceColor` stays the only place that applies the surface colour. For every lit technique the result is `texture * DiffuseColor * (N.L * light + ambient_world * ambient_material) + emissive`.
- This is treated as a bug fix applying to every caller, with no switch.
- `LightingShaderCoverageTests` asserts, on both shaders, that the ambient helper takes only the normal and that no caller passes it the surface colour.

## Consequences

- **Visible impact on existing scenes.** The ambient part of every textured lit material becomes brighter by a factor `1 / texture` (per channel): a mid-grey texture doubles its ambient, a dark texture gains much more. Materials without a base texture and without vertex colour (surface colour 1) are unchanged. Skinned meshes follow the same rule. Direct light, specular, emissive and reflections are unchanged.
- **Migration note.** Scenes, demos and sample games tuned against the old (too dark) ambient may look flatter or brighter in shadow; lower the world ambient (`AmbientColor`, `AmbientIntensity`) or the material ambient (`LitDiffuseMaterial.AmbientColor`) to restore the previous look. Content built from the shaders (`Content.mgcb`, `Shaders/LitForward.fx`, `Shaders/skinEffect.fx`) must be rebuilt; the build of the `CasaEngine` project does it. A game that ships its own compiled copy of these effects must rebuild it.
- **Follow-up.** Re-tuning the existing engine scenes (demos, sample games, editor defaults) is not part of this work; it is a separate task per scene.
- The same fix is the precondition of ADR-0090 (flat ambient source) and of the RacingGame lighting conversion, which maps the original game's `texture * (ambient + diffuse * N.L)` onto the engine's material constants.
