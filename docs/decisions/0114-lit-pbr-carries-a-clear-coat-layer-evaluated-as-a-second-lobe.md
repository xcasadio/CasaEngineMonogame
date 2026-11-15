# ADR-0114: lit-pbr carries a clear coat layer evaluated as a second lobe

- **Status**: Proposed
- **Date**: 2026-11-15
- **Source**: this chantier: `ai-agent/tasks/pbr-clearcoat-ssao-fxaa-tasks.md` (decision D2, arbitrations P1, P2, P4, P5, task T2.1 and its closing review)

## Context

The author asked for car paint (D2): a clear varnish over a coloured base. ADR-0112 moved the clear coat into the PBR scope, which ADR-0102 had left out. The `lit-pbr` material (`LitPbr.fx`, `Pbr.fxh`, ADR-0108) has one GGX lobe over a Lambert diffuse; its ambient is the split-sum image-based lighting of ADR-0109. `Pbr.fxh` is also included by `skinEffect.fx` (ADR-0110), which declares none of the material constants and has no clear coat (P5).

## Decision

- **Properties.** Two scalars on `lit-pbr`: `clear_coat` (0 to 1, default 0) and `clear_coat_roughness` (default 0.1), both overridable per instance (`MaterialInstancePropertyBlockMapper`). They reach the shader as the constants `ClearCoat` and `ClearCoatRoughness`, written by `LitPbrMaterial.Bind` on every bind. A material file without the keys reads the defaults; a coat of 0 renders exactly like a material that never had one. No coat texture, no coat normal map: `LitPbr.fx` keeps its 14 samplers.
- **Direct lobe (closed form).** Per light, with `N_g` the geometric (interpolated vertex) normal, never the normal-mapped one, `V` the eye vector, `H = normalize(V + L)`, `NoH_c = saturate(N_g . H)`, `VoH = saturate(V . H)` (equal to `LoH`), and `irradiance = lightRadiance * NoL * attenuation` (the very value of the base lobes of the slot, `NoL` from the surface normal):
  - `alpha_c = max(ClearCoatRoughness, 0.045)^2`
  - `F_d = 0.04 + 0.96 * (1 - VoH)^5`, `Fc = ClearCoat * F_d`
  - `V_c = 0.25 / max(VoH^2, 0.0001)` (Kelemen visibility; the floor only keeps it finite)
  - `D_c = alpha_c^2 / (pi * (NoH_c^2 * (alpha_c^2 - 1) + 1)^2)` (GGX)
  - coat lobe `D_c * V_c * Fc * irradiance`, added to the specular part, so a transparent surface does not scale it by its alpha;
  - base (diffuse and specular) of the same light multiplied by `1 - Fc`.
  The model is the clear-coat layer of Filament (https://google.github.io/filament/Filament.html#materialsystem/clearcoatmodel). The use of the slot irradiance, the absence of a coat normal map and the `0.045` clamp are the author's arbitration (P4).
- **Ambient with IBL.** `E = 0.04 * A + B`, `A` and `B` read from the BRDF table at `(NoV_c, ClearCoatRoughness)` with `NoV_c = max(dot(N_g, V), 0.0001)`. Coat reflection = global prefiltered atlas read along `reflect(-V, N_g)` at `ClearCoatRoughness`, times `E * ClearCoat * EnvironmentSpecularIntensity * occlusion`; it is added to the specular part. The base ambient, diffuse and specular, is multiplied by `1 - ClearCoat * E`. The coat reflection does not read the local reflection probes (limit).
- **Flat ambient** (`HasIbl = 0`): no coat reflection; the base ambient is multiplied by `1 - ClearCoat * (0.04 + 0.96 * (1 - NoV_c)^5)`.
- **Zero coat is the old shader.** With `ClearCoat = 0` no clear-coat code runs (uniform branches in `ShadePbr`), and the base walk over the lights, `EvaluatePbrLights` and `AccumulatePbrLight`, has no clear-coat code at all. The demo captures of the PBR demo (`LinearHdr` and `Legacy`) and of the baseline demos are bit-identical to the ones before the change.
- **Structure of the evaluation.** The coat is a second walk over the same light slots, run after the base walk when `ClearCoat > 0`: `AccumulatePbrClearCoatLight` adds to the sums the correction `coat - Fc * (base diffuse + base specular)` of one light, and `EvaluatePbrClearCoatDirectional` and `EvaluatePbrClearCoatPointAndSpot` walk the directional (8), point (8) and spot (8) slots with explicit slot lists. The attenuation formulas of the point and spot slots are restated a second time, identical to the base slots, and `ClearCoatShaderCoverageTests` pins them. The shadow factor of the shadow-casting directional light, which scales the base lobes, scales the coat lobe and its correction too.
- **Keeping the skinned shader out (P5).** The clear-coat code of `Pbr.fxh` is compiled only under `#define PBR_CLEAR_COAT`, which `LitPbr.fx` sets before including the file and `skinEffect.fx` does not. `skinEffect.fx` is not modified, declares no `ClearCoat` constant, and its compiled `skinEffect.xnb` is byte-identical to the one before this change. Tests pin the mechanism (text) and the absence of both constants in the compiled skinned effect.

## Consequences

- A car-paint material is a `lit-pbr` material with `clear_coat` near 1: a tinted base under a sharp white reflection. The coat follows the geometric normal, so a normal-mapped base keeps a smooth varnish.
- Cost for a coated draw only: a second walk over the active lights (one normalisation, one GGX term and a handful of multiply-adds per active light), one more BRDF table read and one more atlas read (two bands) for the ambient. An uncoated draw pays two uniform tests. `LitPbr.xnb` grows from 888 091 to 1 722 697 bytes (the second walk is written out for the directional, point and spot slots).
- Limits written: no coat reflection of the local probes; no coat texture; no coat in `skinEffect.fx`; the absorption of the base colour under the coat and the multiple scattering between the layers are not modelled; the coat roughness is not widened by the base roughness.
- Measured on the OpenGL chain (`ps_3_0` compiled by `mgfxc`, cause not found, same family as plan open point O10): the same coat code written inside `AccumulatePbrLight`, even reduced to one extra dot product per slot, made every direct light of the shader vanish, and a second walk written as the `[unroll]` loop with a `break` of `EvaluatePbrLights` silently did nothing; the explicit slot lists above are the form that works. A future change to these functions must be checked by a render, not only by the compiler.
- `docs/engine/pbr-rendering.md` and `docs/engine/materials-workflow.md` describe the feature; ADR-0102's "clear coat out of scope" line is superseded by ADR-0112.
