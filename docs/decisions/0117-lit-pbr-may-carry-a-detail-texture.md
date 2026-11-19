# ADR-0117: lit-pbr may carry a detail texture

- **Status**: Proposed
- **Date**: 2026-11-16
- **Source**: this chantier: the RacingGame plan `ai-agent/tasks/rgce-pbr-race-world-tasks.md` (arbitration P1, task E1), approved by the author on 2026-11-16

## Context

`LitDiffuseMaterial` can repeat a second, finer texture over a large surface (ADR-0100): the base texture is multiplied by `2 * detail(texCoord * DetailTiling)`. `LitPbrMaterial` (ADR-0109) had no such texture. The RacingGame conversion draws its terrain with a detail texture tiled 24 times, and moving the race world to `lit-pbr` needs the terrain in `lit-pbr` too: at equal light intensity a `lit-pbr` diffuse is pi times darker than a `lit-diffuse` one (`docs/engine/pbr-rendering.md`), and the exposure is global, so a world that mixes both materials cannot be balanced by the exposure alone. ADR-0100 had foreseen that a PBR material would want the same albedo detail.

`LitPbr.fx` reads the base colour through one function, `SamplePbrBaseColor`, used by all six techniques. It used 14 of the 16 pixel samplers (t0 to t13).

## Decision

- `LitPbrMaterial` gets `DetailTexture` (`Texture2D`), `DetailTextureAssetId` (`Guid`, default empty) and `DetailTiling` (`float`, default 1; a value that is not finite or not above zero is read as 1), the names and the semantics of `LitDiffuseMaterial`.
- In `LitPbr.fx`, `SamplePbrBaseColor` multiplies the base texture sample by `2 * detail(texCoord * DetailTiling).rgb` on the colour channels, alpha untouched, then by `BaseColor`, exactly as `SampleBaseTexture` of `LitForward.fx` does for its own base colour. The product is a display colour: the decoding to linear light stays in `ShadePbr`, after it, once per pixel, never on the detail sample alone (mid-grey detail is neutral in display space only). The sampler is `DetailTexture` on slot t14 (15 of 16 samplers used); the constants are `HasDetailTexture` and `DetailTiling`, the names of `LitForward.fx` (`ShaderParameterNames` already holds them) without its slots (c35.w is `HasEmissiveTexture` in the PBR layout), placed by the compiler like `ClearCoat`.
- `LitPbrMaterial.Bind` writes `HasDetailTexture` and `DetailTiling` on every bind and binds the texture only when it is set, so nothing leaks from a previous draw. The detail texture is bound only together with a base colour texture, like the detail texture of `lit-diffuse`.
- **No change without a detail texture.** With no detail texture the added code is one uniform branch not taken, and the output is the same as before the change (checked on the baseline captures of the engine demos, the linear colour bench and the PBR demo, and by a GPU test).
- **Serialization, additive.** The `lit-pbr` definition gains `detail_texture` (texture asset reference, default empty) and `detail_tiling` (float, default 1, minimum 0), the keys of the `lit-diffuse` definition, without the legacy alias of `lit-diffuse`. `MaterialCompiler` reads them one by one like the other keys. Both are omitted from a file while they hold their default, so a file written before this change loads and compiles as it did. Neither is mapped by `MaterialInstancePropertyBlockMapper` (as for the other texture keys and `normal_scale`).
- **Skinned meshes** (`skinEffect.fx`, ADR-0111) are not modified and ignore the detail texture, as they ignore the other PBR textures they do not declare. The shadow pass ignores it too (the detail does not change the alpha).

## Consequences

- A detailed `lit-pbr` material costs one more texture fetch per pixel; a material without one costs a uniform branch. `LitPbr.fx` now uses 15 of the 16 pixel samplers: one is left.
- The detail sampler uses the device's default sampler state under DesktopGL (linear, mip filtering, wrap), as in ADR-0100: the texture should have mipmaps when the tiling is high.
- `PbrShaderCoverageTests` no longer requires `HasDetailTexture` and `DetailTiling` to sit in the slots of `LitForward.fx`; it pins their unslotted declaration instead.
- Not covered: a blend other than `2 * detail`, a per-material sampler for the detail texture, a detail normal map, skinned meshes, a per-instance override of the two keys. A detail texture on a material with no base colour texture is ignored.
- The RacingGame terrain (24 repeats, ADR-0100's use case) can move to `lit-pbr`.
