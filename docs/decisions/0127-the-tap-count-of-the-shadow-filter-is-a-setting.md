# ADR-0127: The tap count of the shadow filter is a setting

- **Status**: Accepted
- **Date**: 2026-11-18
- **Source**: this chantier: `ai-agent/tasks/shadow-filter-taps-tasks.md`, decisions D1 to D5 (authorised by the author on 2026-11-18 for the RacingGame plan `ai-agent/tasks/rgce-shadow-colour-decor-tasks.md` of the game repository, slice G1; origin: point O4 of `ai-agent/tasks/rgce-road-tunnel-shadow-tasks.md` of that plan)

## Context

- `SampleDirectionalShadowPcf` (`Lighting.fxh`) filters the edge of the directional shadow with 4 point reads at +-0.5 texel when `ShadowFilterRadius` is 0 and, above 0, with 12 reads on a fixed Poisson disc scaled by `ShadowMapTexelSize * ShadowFilterRadius` (ADR-0102). The disc is neither rotated nor offset per pixel and the path has no noise.
- On a surface lit at a grazing angle the penumbra of a shadow edge is wide, and 12 fixed taps show it as a fan of bands. A trial in the game on 2026-11-18 (engine shader changed for the trial then restored, outputs in the game repository under `artifacts/pbr-handover/s23/`) replaced the 12 taps by a Vogel disc computed inside an unrolled loop: 32 taps gave thinner bands that were still visible, 64 taps a smooth gradient. Both compiled and ran in the game.
- `ShadowSettings.FilterRadius` is the model for a filter setting: a normalising property, an optional key written only when it differs from the default, `ResetToDefaults`, `CopyFrom`, an editor row, a uniform set by `ForwardLightBinder` and declared in `LitPbr.fx`, `LitForward.fx` and `skinEffect.fx`.

## Decision

- New setting `ShadowSettings.FilterTaps` (key `filter_taps`): the number of shadow-map reads of the soft-edge filter, 12 (default), 32 or 64. Any other value is brought back to the nearest of the three (12 below 22, 32 from 22 to 47, 64 from 48). It acts only when `FilterRadius` is above 0. The key is optional and written only when it differs from 12; an absent key reads as 12. No existing serialized format changes.
- The count reaches the shaders as the float uniform `ShadowFilterTaps` (`_ps(c107)` / `_cb(c97.y)`), set by `ForwardLightBinder` with the other shadow uniforms (12 when no shadow is bound). There is no shader permutation: `SampleDirectionalShadowPcf` has three branches in one shader.
- For a uniform value of 64 or 32 (within half a unit) the taps are those of a Vogel disc (tap `i` of `N` at radius `sqrt((i + 0.5) / N)` and angle `i * 2.39996323` rad on the unit disc), scaled like today by `ShadowMapTexelSize * ShadowFilterRadius`; comparison and average are unchanged. For every other uniform value, 0 included (nothing set it), the shader runs the code of earlier versions with the same Poisson table, so images are identical bit for bit at radius 0 and at a positive radius.
- The editor shows the setting as a combo box (12, 32, 64) under `Filter Radius` in the "Directional Shadows" section of the world panel.

## Consequences

- Nothing changes for an existing world or demo: the default is 12 and the 12-tap code is kept as it was (checked on the deterministic demo captures, at radius 0 and at radius 3).
- At 32 and 64 the cost grows in proportion to the number of reads per shadowed pixel; it is documented as a count of reads and has not been measured in frame time.
- The Vogel offsets are computed in the shader (`sin`, `cos`, `sqrt` per tap inside an unrolled loop); the unrolled 32-tap and 64-tap loops add to the size of the three lit shaders. The disc is not rotated per pixel, so a large radius can still show its pattern.
- A world saved with `filter_taps` and read by an older engine loses the key (unknown keys are ignored) and falls back to 12 taps.
- A rotation or noise per pixel, a screen-space blur of the shadow and hardware or bilinear comparison remain out of scope.
