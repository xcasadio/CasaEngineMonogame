# ADR-0126: A lit-pbr material can ask for its emissive to receive the shadow

- **Status**: Proposed
- **Date**: 2026-11-17
- **Source**: this chantier: `ai-agent/tasks/draw-order-emissive-shadow-tasks.md`, decision D4 (point O17 of `ai-agent/tasks/rgce-pbr-handover-tasks.md` of the RacingGame plan, authorised by the author on 2026-11-17)

## Context

- In `LitPbr.fx` (`ShadePbr`) the emissive colour is added after the lighting and handed to `ApplyShadowTint` as the term to leave out (`Lighting.fxh`). In the default shadow mode (`ShadowApplyMode` 0) the shadow factor only scales the directional light at `ShadowedDirectionalLightIndex` (`EvaluatePbrLights`, `Pbr.fxh`). No material parameter changed either: an emissive was never shadowed.
- The shadow factor of a pixel is 1 unless a directional light is active at `ShadowedDirectionalLightIndex`, a shadow is active (`ActiveShadowLightCount` above 0) and the material receives shadows (`ReceiveShadows`): `ComputeDirectionalShadowFactor` returns 1 otherwise. `EvaluatePbrLights` returns it in `ColorPair.ShadowFactor`, and 1 when the shadowed light is not one of the lights this variant shades.
- The scalar parameters of `lit-pbr` added after the first version (ADR-0115 clear coat, ADR-0117 detail texture) follow one pattern: a definition key (`BuiltInMaterialDefinitions`), a name in `ShaderParameterNames`, a property of `LitPbrMaterial` written by `Bind` every bind, a line in `MaterialCompiler.CreateLitPbrMaterial`, a constant of `LitPbr.fx` without a reserved slot, and no shader permutation.

## Decision

- **Key.** `lit-pbr` gains `emissive_shadow_factor`: a float from 0 to 1, default 0, group Lighting. It has no per-instance override (the override mapper does not map it, as for `normal_scale`). `LitPbrMaterial.EmissiveShadowFactor` carries it, `MaterialCompiler` reads it, `Bind` writes the uniform `EmissiveShadowFactor` every bind. The uniform has no reserved `_cb` slot and there is no permutation: the technique and the shader features do not change.
- **Shader.** In `ShadePbr`, right after `EvaluatePbrLights` and before the sum of the terms, when `EmissiveShadowFactor > 0`: `emissive *= lerp(1, s, saturate(EmissiveShadowFactor))`, with `s` the shadow factor of the pixel in the active mode: `lightResult.ShadowFactor` (the visibility, a scalar) when `ShadowApplyMode` is 0, `ComputeShadowTint(lightResult.ShadowFactor)` (the per-channel tint, `lerp(1, ShadowTintColor, 1 - visibility)`) in the `MultiplyLitColor` mode. The scaled emissive is the one added to the sum and the one `ApplyShadowTint` leaves out, so the tint is applied once, not twice. At 0 the branch does not run: the emissive is the one of today.
- **Scope.** `lit-diffuse`, the other materials and the skinned PBR path are unchanged.
- **Documentation.** `docs/engine/pbr-rendering.md` and `docs/engine/materials-workflow.md`.

## Consequences

- Additive: a material file without the key reads 0 and renders as before. The baseline fingerprints (`pbrdemo.sh` default, `capture-v2.sh`, `lin-v2.sh`) are unchanged by the shader change (see the plan, T2).
- The value is only meaningful where a shadow-casting directional light is active and the surface receives shadows; elsewhere the emissive stays whole whatever the value, as the shadow factor is then 1.
- The shadow acts on the emissive by the factor of the one shadow-casting directional light, not by a separate evaluation: it follows what the rest of the pixel gets.
- The mid values interpolate between unshadowed and fully shadowed; they are not a physical model.
- The editor lists the key under the Emission category of the lit-pbr material, as a slider (`MaterialDefinitionEditorRegistry`), without code of its own.
