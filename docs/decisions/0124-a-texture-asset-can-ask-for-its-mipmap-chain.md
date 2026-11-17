# ADR-0124: A texture asset can ask for its mipmap chain

- **Status**: Proposed
- **Date**: 2026-11-17
- **Source**: this chantier: `ai-agent/tasks/texture-asset-mipmaps-tasks.md`, decisions D1 to D4 (slice S1m of the RacingGame plan `ai-agent/tasks/rgce-pbr-handover-tasks.md`, envelope approved by the author on 2026-11-17)

## Context

- `Texture2DLoader.GenerateMipmaps` (ADR-0101) builds a mipmap chain, but it is a property of the whole loader, false by default, and only code can set it.
- `AssetContentManager` keeps one shared instance per asset id (`Acquire<T>`, ADR-0036): an image is loaded once, without mipmaps, for every user. A `Texture` loaded from a `.texture` asset holds that shared image through a handle (ADR-0037).
- `Texture.Load(JObject)` read `sampler_state` and `texture_asset_id` only, and `EditorAssetJsonSerializer.SaveTexture` wrote those two fields. A game that loads its textures by id, from assets, had no way to get mipmaps without code of its own.

## Decision

- **D1.** A `.texture` asset has an optional boolean field `generate_mipmaps`, false when absent. The serializer writes it only when true, so a file that does not use it is unchanged. `Texture.GenerateMipmaps` exposes it.
- **D2.** A `Texture` that asks for mipmaps owns its own `Texture2D`, created with the full chain at load time from the level 0 pixels of the shared image (`MipmapChain.UploadChain`). The shared image is not modified and stays at one level for its other users; the handle on it is given back once the copy is made. `Texture.Resource` is the owned texture. `Load` does nothing when the owned texture already exists and is not disposed, because every user of a shared `Texture` calls it. `Dispose` frees the owned texture. An image that is not `SurfaceFormat.Color` is refused with a message naming the texture asset and the image asset.
- **D3.** After a device loss, a `Texture` with mipmaps rebuilds its chain the same way from the current image: the owned texture is recreated when it was disposed, and the image is acquired for the copy and given back.
- **D4.** The work is on the branch `chantier/texture-asset-mipmaps`, cut from `chantier/static-model-tangents`.

## Consequences

- The change is additive: a `.texture` file without the field loads as before, with the shared image at one level and the same instance as `Acquire<Texture2D>`.
- A texture with mipmaps costs, at load time, a read back of the image and the CPU chain, and keeps about a third more video memory than the image alone, plus the shared image while another user holds it (it is freed by `CollectUnreferenced` once nobody does). It is meant for the textures that need it, not for all of them.
- The asset content manager and the material compiler are unchanged: the compiler already calls `Texture.Load(AssetContentManager)`.
- The `.texture` document of the editor does not edit the field; it is set in the file, and an edit of the sampler keeps it (the serializer writes it back).
- Textures that a game repaints in code still call `MipmapChain.UploadChain` themselves (ADR-0101).
- DDS levels stored in a file are still not read.
- See `docs/engine/texture-mipmaps.md`.
