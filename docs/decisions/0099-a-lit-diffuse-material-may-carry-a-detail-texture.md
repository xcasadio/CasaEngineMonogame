# ADR-0099: A lit diffuse material may carry a detail texture

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/lighting-ambient-shadow-tasks.md` (task E7), approved with the RacingGame plan `ai-agent/tasks/rgce-3d-materials-base-tasks.md` (decisions D1 and D3, amendments A3 and A8)
- Renumbered from ADR-0091 on 2026-11-15 when origin/main was merged (its ADR-0091 is another decision)

## Context

`LitDiffuseMaterial` had one colour texture and an optional normal map, and no way to repeat a second, finer texture over a large surface. The RacingGame conversion needs it for its terrain: the original game draws it with `texture * (ambient + diffuse * relief) * (2 * detail)`, the detail being sampled at the texture coordinates multiplied by 24. Without an engine feature the only workarounds were tiling the detail into the colour texture at conversion time (a texture of tens of thousands of pixels on a side, or a loss of fine detail). The feature is generic: a PBR material will want the same albedo detail.

All the textured pixel shaders of `LitForward.fx` read the base colour through one function, `SampleBaseTexture`. Texture slots 0 to 6 of that effect are taken (`Texture`, `NormalTexture`, the reflection and environment cubes, the two probes, `ShadowMapTexture`). `skinEffect.fx` has its own texture reads.

## Decision

- `LitDiffuseMaterial` gets `DetailTexture` (`Texture2D`), `DetailTextureAssetId` (`Guid`, default empty) and `DetailTiling` (`float`, default 1; a value that is not finite or not above zero is read as 1).
- In `LitForward.fx`, `SampleBaseTexture` returns `texture.rgb * 2 * detail(texCoord * DetailTiling).rgb` when a detail texture is bound, alpha untouched; with none it returns what it returned before. The neutral detail value is mid grey (128/255 gives a factor of 1.004). The detail multiplies the **base colour before lighting**, so the lighting (direct, ambient, specular) sees the detailed colour, exactly as the original terrain does. New uniforms `HasDetailTexture` and `DetailTiling`, and a new sampler `DetailTexture` on texture slot 7. No new technique and no new shader feature flag: a material without a detail texture produces the same output as before (checked bit for bit on the static shadow demo).
- Skinned meshes do not support it: `skinEffect.fx` is not modified and ignores the material's detail texture. The shadow pass is not modified either (the detail does not change the alpha).
- The detail texture is bound only together with a base texture (the shader never reads it otherwise), like the normal map.
- Serialization: the `lit-diffuse` definition gains `detail_texture` (texture asset reference, default empty, legacy alias `texture_detail_asset_id`) and `detail_tiling` (float, default 1, minimum 0). `MaterialCompiler` reads them as it reads `normal_texture`. The runtime material's JSON keys are `texture_detail_asset_id` and `detail_tiling`, read by `LitDiffuseMaterial.Load` and written by the editor's `SaveLitDiffuseMaterial`. Both are optional on reading: an asset without them loads as before. Nothing that exists is renamed.
- Editor impact: the material inspector is driven by the definition, so it shows the two new properties (an asset picker and a number box, in the "Surface" section after the existing ones) without any interface code change. The editor's material tests that count the properties of `lit-diffuse` are updated.
- Sampler state (amendment A3): the engine only sets the sampler of slot 0 (`RenderStateCache`), and under DesktopGL `DECLARE_TEXTURE` ignores its slot index and declares no sampler state (`Macros.fxh`). The detail sampler therefore uses the device's default sampler state: linear filtering with mip filtering, and wrap addressing. The repetition the feature relies on thus depends on that default; a game that changes the device's default sampler state for that slot changes it. Without mipmaps on the detail texture a strong tiling shows moire (see ADR-0100 for the loader option that builds them).

## Consequences

- The `LitForward.fx` effect gains one sampler (eight in total) and two constants. It compiles for DesktopGL with the engine's content pipeline; the demos and games pick the rebuilt effect up through the `CasaEngine` project build.
- A detailed material costs one more texture fetch per pixel; a material without one costs a uniform branch.
- Formula equivalence with the original terrain: its `D * (A + relief * Dc) * (2 * detail)` equals lighting the colour `texture * 2 * detail`, provided there is no specular and no reflection on the terrain.
- Not covered: a different blend (additive, overlay), a per-material sampler for the detail texture, detail normal maps, skinned meshes. A detail texture on a material with no base texture is ignored.
- See `docs/engine/lit-detail-texture.md`.
