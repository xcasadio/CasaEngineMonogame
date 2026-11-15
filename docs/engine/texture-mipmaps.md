# Texture mipmaps from the loader

`Texture2DLoader` can build the mipmap chain of the textures it loads. Decision: ADR-0097. Related: ADR-0096 (a tiled detail texture needs mipmaps at a distance).

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
