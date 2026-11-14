# ADR-0093: Shadow settings: application mode and colour, filter radius, fade distance and depth range

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/lighting-ambient-shadow-tasks.md` (decisions D1, D2, D3; tasks E3, E4, E5, E5b), executed on the author's request of 2026-11-14 to run the tasks following the material plan while away; the RacingGame plan `ai-agent/tasks/rgce-3d-lighting-shadows-tasks.md` (amendments B2, B3, C4, C6 to C9, D3, F6, F7, F8)

## Context

`ShadowSettings` held `Enabled`, `Resolution`, `DepthBias`, `NormalBias` and `MaxDistance`. The forward receiver (`Lighting.fxh`, `ComputeDirectionalShadowFactor`) takes 4 point samples at +-0.5 texel; the shadow factor only scales the direct diffuse and specular of the shadowed light (`AccumulateLight`), so the ambient, emissive and reflection terms are never shadowed and a fully shadowed pixel is lit by the ambient term alone. Outside the shadow map the surface is lit without a transition. The shadow map is an orthographic square of half-side `MaxDistance` centred on the camera, and the same value places the light (`MaxDistance` upstream) and the far plane (`2 * MaxDistance`) (`ShadowPass.BuildDirectionalShadowViewProjection`). The RacingGame original multiplies the whole image by `lerp(1, ShadowColor, shadow)` with `ShadowColor = (0.25, 0.26, 0.27)`, blurs the shadow and fades it with the distance.

Both lit shaders consume `Lighting.fxh` (`LitForward.fx` and `skinEffect.fx`), so every uniform it reads is declared in both.

## Decision

One decision record covers the shadow settings added by tasks E3, E4, E5 and E5b. All the new fields are optional in the serialized world (absent means the default, which keeps today's behaviour), written only when they differ from the default so that existing worlds keep their format, and are handled by `ResetToDefaults`, `Load`, `Save` and `CopyFrom` of `ShadowSettings` (and so by `WorldEnvironmentSettings.Clone` / `CopyFrom`). The editor's world panel shows each of them next to the existing shadow fields.

- **Mode and colour (E3).** New enum `ShadowMode` and fields `ShadowSettings.Mode` (key `mode`) and `ShadowSettings.ShadowColor` (key `shadow_color`, an `{x, y, z}` object, each component kept in [0, 1], non-finite becomes 0, default black).
  - `OccludeDirectLight` (default): unchanged behaviour. The shadow factor scales the direct diffuse and specular of the shadowed light.
  - `MultiplyLitColor`: the shadowed light is not occluded. The final colour of the pixel excluding the emissive colour (lit colour, specular and reflection contribution) is multiplied by `lerp(1, ShadowColor, 1 - shadowFactor)`. The emissive colour is never tinted and the alpha is untouched. The shadow factor is computed once per pixel, outside the light loop (`ComputeLights`), and handed to the pixel shaders in `ColorPair.ShadowFactor`.
  - In the three reflection pixel shaders of `LitForward.fx` the tint is applied after `ApplyReflectionContribution`. That function multiplies the surface colour (emissive included) by `base + reflection * factor` and adds `reflection * add`; `ApplyReflectionContributionWithShadowTint` tints only the non-emissive part of this linear form (the result minus `emissive * (base + reflection * factor)`), so the emissive colour is never tinted and nothing is tinted twice. In the default mode the output is exactly the former one.
  - New uniforms `ShadowApplyMode` and `ShadowTintColor`, bound by `ForwardLightBinder` from the settings of the visible shadow light, declared in `LitForward.fx` and `skinEffect.fx` (`_ps(c101)`/`_cb(c91.w)` and `_ps(c106)`/`_cb(c96)`; the register annotations are documentation only, `Macros.fxh` expands them to nothing).
- **Filter radius (E4).** Recorded with its task.
- **Fade distance (E5).** Recorded with its task.
- **Depth range (E5b).** Recorded with its task.

## Consequences

- **Vertex-lighting techniques have no shadows in either mode** (`ComputeLightsNoShadows`, `Lighting.fxh`): their results never get a tint. Only the pixel-lighting techniques and the skinned shader receive shadows.
- **With several lights, `MultiplyLitColor` tints the contributions of every light**, and the ambient term, by the factor of the single shadowed light: with a second, unshadowed light a shadowed pixel is darkened for both lights. Use it for a scene with one dominant shadow-casting light (the sun); keep `OccludeDirectLight` when other lights must keep lighting the shadowed area.
- `ReceiveShadows = false` on a component, or no shadowed light, give a tint of 1 (the colour is unchanged). A one-light technique whose shadow-casting light is not the light it shades gets no tint either.
- In `MultiplyLitColor` a shadowed pixel is the lit pixel times the shadow colour, so it keeps the specular and the reflections of a lit pixel, darkened; there is no longer a "specular highlight in the shadow" artefact.
- The default mode is bit-identical to the former output (captured demo images compared byte for byte).
- Older engine versions reading a world saved with the new keys ignore them.
- See `docs/engine/shadow-settings.md` (task E6).
