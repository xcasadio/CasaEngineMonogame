# Detail texture on the lit diffuse material

`LitDiffuseMaterial` can multiply its base colour by a second, tiled texture. Decision: ADR-0097.

## Formula

Where a detail texture is bound, `SampleBaseTexture` in `LitForward.fx` returns:

```
base.rgb * 2 * detail(texCoord * DetailTiling).rgb      (alpha unchanged)
```

- Mid grey (128/255) is the neutral value: it leaves the base colour as it is. Darker detail texels darken, lighter ones lighten (the factor reaches 2 at white).
- The product is made **before** lighting: the direct light, the ambient term and the specular all see the detailed colour (see `lit-ambient-and-ambient-source.md` for the lit formula, where `texture` becomes `texture * 2 * detail`).
- `DetailTiling` is the number of repeats of the detail over the base texture coordinates. A value that is not finite or not above 0 is read as 1.

## Use

```csharp
var material = new LitDiffuseMaterial
{
    BasColor = terrainTexture,
    DetailTexture = detailTexture,
    DetailTiling = 24.0f,
};
```

In a material asset (`lit-diffuse` definition): `detail_texture` (texture asset, legacy alias `texture_detail_asset_id`, default none) and `detail_tiling` (float, default 1). The material inspector of the editor shows both. An asset that has neither loads exactly as before.

## Limits

- The detail texture is read only together with a base texture. Without a base texture it is ignored.
- Skinned meshes (`skinEffect.fx`) do not support it. The shadow pass ignores it.
- The detail sampler uses the device's default sampler state under DesktopGL (linear with mip filtering, wrap). The texture should have mipmaps when the tiling is high, otherwise the distance shows moire. `Texture2DLoader.GenerateMipmaps` (ADR-0098) builds them when loading from a file.
- The detail texture costs one more texture fetch per pixel and one sampler slot (slot 7) of `LitForward.fx`.

## Measuring it

`StaticShadowValidationDemo` in its measurement configuration (`CASAENGINE_SHADOW_DEMO_VALIDATION=1`, see `lit-ambient-and-ambient-source.md`) binds a solid detail texture of value 64/255 with a tiling of 4 to the receiver when `CASAENGINE_SHADOW_DEMO_DETAIL=1`. Expected: the receiver's colour is multiplied by `2 * 64/255 = 0.502`; in the shadow, the ambient-only pixel goes from 64 to 32 per channel.
