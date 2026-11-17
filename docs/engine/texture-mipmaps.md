# Texture mipmaps from the loader

`Texture2DLoader` can build the mipmap chain of the textures it loads. Decision: ADR-0101, and ADR-0124 for the `.texture` asset field. Related: ADR-0100 (a tiled detail texture needs mipmaps at a distance).

## Loading with mipmaps

```csharp
var loader = new Texture2DLoader { GenerateMipmaps = true };
var texture = (Texture2D)loader.LoadAsset(path, assetContentManager);
// texture.LevelCount == floor(log2(max(width, height))) + 1
```

`GenerateMipmaps` is **false by default**: nothing changes for a project that does not set it (one level, as `Texture2D.FromStream` returns it). When true:

- level 0 is the decoded image, bit for bit;
- level `i` has `max(1, width >> i)` x `max(1, height >> i)` pixels (the MonoGame convention), down to 1 x 1;
- each level is the 2 x 2 box average of the previous one, per channel, rounded to the nearest value; alpha is straight (not premultiplied) and averaged like the colour channels; on an odd dimension the last row or column is not sampled; on an axis of length 1 the block is 1 pixel wide;
- the format is `SurfaceFormat.Color`;
- the cost is load time (read back, CPU average, upload) and about +33 % video memory.

## Asking for the chain from a `.texture` asset

A game that loads its textures by id, from assets, sets `generate_mipmaps` in the `.texture` file (ADR-0124):

```json
{
  "id": "0e470a8a-1582-5839-9a0c-96d2adc604eb",
  "name": "Car.Paint.Texture",
  "texture_asset_id": "a4f6260f-45cc-58b4-9d69-de59ac8e43ef",
  "generate_mipmaps": true,
  "sampler_state": { }
}
```

- The field is optional and false when absent: a file without it loads as before, and `Texture.Resource` is the shared image at one level.
- When true, `Texture.GenerateMipmaps` is true and `Texture.Load(AssetContentManager)` builds the chain once: it reads the level 0 pixels of the shared image, creates its own `Color` texture with the full chain (`MipmapChain.UploadChain`) and gives the handle on the shared image back. `Texture.Resource` is that owned texture; later `Load` calls (materials, meshes, sprites, tile maps share a `Texture`) leave it as it is.
- The shared image itself is not modified: its other users still see one level, and `AssetContentManager.CollectUnreferenced` frees it when nobody holds it.
- `Texture.Dispose` frees the owned texture. After a device loss, `OnDeviceReset` rebuilds the chain from the current image.
- The image must be `SurfaceFormat.Color`; otherwise `Load` throws an `InvalidOperationException` naming the texture asset and the image asset.
- The serializer (`EditorAssetJsonSerializer`) writes the field only when it is true. The `.texture` document of the editor does not edit it: set it in the file; saving the document keeps it.
- Cost: the same as `Texture2DLoader.GenerateMipmaps` (read back, CPU average, upload, about +33 % video memory), paid once per texture at load time.

## Refreshing the levels of a texture painted at runtime

Create the texture with `mipMap: true`, paint level 0 into a `Color[]`, then upload the chain:

```csharp
var texture = new Texture2D(device, w, h, mipMap: true, SurfaceFormat.Color);
Color[] pixels = new Color[w * h];
// ... paint pixels ...
MipmapChain.UploadChain(texture, pixels);   // level 0 and every level below
```

`MipmapChain` (namespace `CasaEngine.Framework.Assets.Loaders`) also exposes the pure part, usable without a graphics device:

| Member | Role |
|---|---|
| `GetLevelCount(width, height)` | `floor(log2(max(width, height))) + 1` |
| `GetLevelSize(size, level)` | `max(1, size >> level)` |
| `ComputeNextLevel(source, width, height)` / span overload | one level from the previous one |
| `ComputeLevels(level0Pixels, width, height)` | the whole chain (entry 0 is the array passed in) |
| `UploadChain(texture, level0Pixels)` | writes the chain into a `Color` texture made with `mipMap: true` |

The array-returning methods allocate; call them at load time or on an occasional repaint, not every frame.

## Limits

- Only `SurfaceFormat.Color` textures. Mipmaps stored in files (DDS) are not read by this loader.
- The cube map loader is not concerned.
- The sampler state of the material decides whether the levels are used (linear mip filtering, anisotropic).

## Checking it on a real device

`StaticShadowValidationDemo` with `CASAENGINE_SHADOW_DEMO_MIPMAP_CHECK=1` writes a 37 x 19 PNG and a 64 x 32 TGA to a temporary folder, loads them with `GenerateMipmaps = true`, logs the level count and every level size, compares level 0 with `Texture2D.FromStream` and every level with the CPU chain read back from the device, logs `verdict=PASS|FAIL` and deletes the files. Start it with `CASAENGINE_START_DEMO="Static shadow validation demo"`; the lines are in the demo's `log.txt`.
