# ADR-0100: The texture loader can build a mipmap chain

- **Status**: Accepted
- **Date**: 2026-11-14
- **Source**: this chantier: `ai-agent/tasks/lighting-ambient-shadow-tasks.md` (task E8), approved with the RacingGame plan `ai-agent/tasks/rgce-3d-materials-base-tasks.md` (decision D5, amendments A1 and A2)
- Renumbered from ADR-0092 on 2026-11-15 when origin/main was merged (its ADR-0092 is another decision)

## Context

`Texture2DLoader.LoadAsset` returned `Texture2D.FromStream(...)`: a texture with a single level. No 2D loader of the engine reads or builds mipmaps. A 3D scene that samples such a texture at a distance shows aliasing noise, and a detail texture tiled many times (ADR-0099) shows moire. The RacingGame original builds every one of its textures with a mipmap chain and filters between the levels; its conversion cannot match that through the engine's loader. Some games also paint a texture at runtime (RacingGame repaints its car body textures) and need to refresh the levels of such a texture after changing level 0.

MonoGame creates a texture with its levels when asked (`new Texture2D(device, w, h, mipMap: true, format)`), with the convention that level `i` measures `max(1, dimension >> i)` on each axis and that the chain holds `floor(log2(max(w, h))) + 1` levels; it does not compute their content.

## Decision

- `Texture2DLoader` gets a public instance property `GenerateMipmaps`, **false by default**: the default behaviour is exactly the former one (one level, same object returned by `Texture2D.FromStream`). When true, `LoadAsset` returns a `SurfaceFormat.Color` texture created with the full chain, level 0 being the pixels `FromStream` decoded (bit-identical) and each following level the 2 x 2 box average of the previous one.
- The level computation is a public pure type, `CasaEngine.Framework.Assets.Loaders.MipmapChain` (static, no graphics device), rather than a method of the loader, so that a caller that has no file to load can use it:
  - `GetLevelCount(width, height)` and `GetLevelSize(size, level)`: the MonoGame convention above.
  - `ComputeNextLevel(source, width, height)` (and a span overload that writes into a caller's buffer): one level from the previous one.
  - `ComputeLevels(level0Pixels, width, height)`: the whole chain, entry 0 being the level 0 array itself.
  - `UploadChain(texture, level0Pixels)`: writes level 0 and every computed level into an existing `Color` texture created with `mipMap: true`. A game that repaints a texture calls it again after the repaint to refresh the levels.
- Averaging: per channel, the four channels alike, rounded to the nearest value (halves up). Alpha is **straight, not premultiplied**: a transparent pixel weighs as much as an opaque one, which is also how the original builds its levels (its content pipeline does not premultiply). On an axis of length 1 the block is one pixel wide; on an odd axis the last source row or column belongs to no block.
- The computation runs at load time (or at an explicit `UploadChain` call), never per frame. A format other than `Color` is refused with a message naming the file.

## Consequences

- **Default off**, because switching it on by default would change the textures of every project that uses the loader, the user interface included, and cost a third more video memory for all of them. A project turns it on for the textures that need it (3D world textures), by creating its own loader or setting the property.
- Cost of the option: the load decodes the image, reads it back from the device (`GetData`), computes the chain on the CPU and uploads it, plus about +33 % memory (not measured as a duration).
- The sampler of the material must filter between levels to benefit (the engine's default samplers do).
- The loader does not read mipmaps stored in a file (DDS levels); it only builds them from level 0.
- The API is additive: no existing member changes.
- See `docs/engine/texture-mipmaps.md`.
