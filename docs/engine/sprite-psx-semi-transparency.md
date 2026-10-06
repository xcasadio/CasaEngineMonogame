# Sprite PSX semi-transparency

Decisions: see ADR-0051 and, for the background layers, ADR-0053; for the quads with four free vertices, ADR-0068.

A sprite can carry a PSX semi-transparency mode, so that the semi-transparent texels of its sheet (the PSX "STP" texels, stored at
alpha 128) blend with the scene the way the PSX GPU blends them, while the opaque texels of the same sprite stay opaque.

## Data

`SpriteData.PsxSemiTransparency` (`SpritePsxSemiTransparency`): `None` (default), `Mode0`, `Mode1`, `Mode2`, `Mode3`. In the `.sprite`
file the field is `psx_semi_transparency`, written by name and **only when it is not `None`**: a sprite file without the field reads
as `None`, so existing files keep their bytes. An unknown name is logged and read as `None`.

| Mode | PSX rate | Blend of the STP texels |
|---|---|---|
| `Mode0` | ABR 0, average | `0.5 * back + 0.5 * front` (non-premultiplied alpha blend at alpha 128) |
| `Mode1` | ABR 1, additive | `back + front`, saturated |
| `Mode2` | ABR 2, subtractive | `back - front`, saturated at zero |
| `Mode3` | ABR 3, quarter | `back + front / 4`: additive blend with the colour (64, 64, 64), which replaces the component colour |

## Drawing

The sorted sprite path of `AnimatedSpriteComponent` (entities with a `DepthSortable2DComponent`) passes the mode of each part to
`SpriteRendererComponent.DrawSprite(Sprite, ..., SpritePsxSemiTransparency)`. A part with a mode is queued as **two entries of the
same sort key**, on two disjoint windows of the texel's raw alpha (shader parameter `AlphaWindow`, the window is `(min ; max]`):

1. opaque texels, window `(0.75 ; 1]`, opaque blend state;
2. STP texels, window `(0.25 ; 0.75]`, blend state of the mode.

A part without a mode stays one entry on the neutral window `(-1 ; 2]`, which rejects nothing: nothing changes for it. The doubling
depends only on the PSX mode, never on `SpriteBlendMode`. The window tests the raw alpha with point sampling (the renderer's
sampler); every draw path that uses the sprite effect sets its window, so none leaks into the next draw. The path by `zOrder` (an
entity without `DepthSortable2DComponent`) stays opaque.

## Background layers

`ScrollingLayerDefinition` and `CellularLayerDefinition` also carry a `PsxSemiTransparency` (default `None`, ADR-0053). When it is not
`None`, `ScrollingLayerComponent.Submit` (each covering quad) and `CellularLayerComponent.Submit` (each cell) call an internal overload
of `SpriteRendererComponent.DrawSprite` (`Texture2D`, source rectangle, origin, position, rotation, scale, colour, z, sort key, effects,
scissor, mode) that queues the same two entries as above, with the same sort key and z, and the `Blend` of the layer is ignored; the
per-cell sort offset of ADR-0052 is kept. With `None` the layers draw one entry with their `Blend`, as before. The `Sprite` overloads
of `DrawSprite` are not redirected to it.

The tint overlay of the scrolling layers (`ScrollingTintDefinition`) takes a mode through a three-argument constructor (ADR-0066). A
flat primitive has no per-texel STP, so the tint stays **one** entry on the neutral window with the blend state of the mode: `Mode0`
`AlphaBlend` with `(R, G, B, 128)`, `Mode1` `Additive` and `Mode2` `Subtractive` with `(R, G, B, 255)`, `Mode3` `Additive` with each
channel times 64/255 (rounded to nearest). The two-argument constructor and `None` keep the colour as given, `AlphaBlend`.

## Free quads

`SpriteRendererComponent.DrawPsxQuad` (ADR-0068) draws a PS1 textured quad: four vertices in the order of the PS1 data (top-left,
top-right, bottom-left, bottom-right of the texture rectangle) placed anywhere in the world (units = PS1 pixels, y up), so a scaled,
mirrored, sheared or arbitrary quad is drawn as the PS1 draws it.

```csharp
renderer.DrawPsxQuad(texture, new Rectangle(sx, sy, w, h), topLeft, topRight, bottomLeft, bottomRight,
    Color.White, z, in sortKey, SpritePsxSemiTransparency.None, scissorRectangle);
```

- The rectangle is the **raw** texture window of the PS1 data in texels (`u, v, w, h`). The texture coordinates are its corners, never
  flipped: a mirror is in the corners. A mirrored axis names its window one texel back; the producer gives it that way.
- The quad is split along the TR-BL diagonal (the PS1 split). The entry is drawn without face culling, so a mirrored quad shows.
- A PSX mode queues two entries of the same key, z and corners, as for a sprite (opaque texels, then STP texels).
- Overloads: with or without a sort key (the `z` path), explicit scissor or the device scissor.
- Drawing uses the effect `Shaders/PsxQuad.fx`, loaded on the first quad entry. At the integer factor k (screen pixels per PS1 pixel,
  read from the projection and the viewport): coverage by the top-left corner of the screen pixel, texel `floor(u(p) + 1/2)` with
  p = ((sx + 1/2) / k - 1/2, (sy + 1/2) / k - 1/2). Exact at k = 1; a 1:1 quad equals the rectangle path at every factor; a
  deformed quad at k > 1 is smoother than the PS1 enlarged.
- Demo: `PSX free quads` (`CASAENGINE_PSXQUAD_ZOOM` 1 or 3, `CASAENGINE_PSXQUAD_DUMP_PATH` for the back-buffer dump), run from the
  `CasaEngine.Demos` folder.

## Limits

- The formulas are the PSX ones on 8-bit colours, not the 5-bit arithmetic of the hardware (differences up to a few levels).
- The back-buffer alpha is rewritten by `Mode0` (191 instead of 255): an in-process capture saved as PNG shows those pixels
  translucent.
- A shader of a project that predates the `AlphaWindow` parameter draws every texel (the parameter is skipped when absent).
- Quads with four free vertices are drawn by `DrawPsxQuad` (next section), not by the sprite overloads.

## Capacity

The sprite queue of `SpriteRendererComponent` is no longer capped at 10 000 entries: the vertex staging array and the vertex buffer
grow with the queue (they never shrink). The index buffer (six indices) does not change.

## Demos

`PSX sprite semi-transparency`, `Sprite queue capacity` and `Background layers PSX semi-transparency` (the last one draws scrolling and
cellular layers from PNG sheets in `CasaEngine.Demos/Content/PsxBackdropLayers/`; `CasaEngine.Demos/Demos/PsxSemiTransparency/`) read their own back-buffer
in process and compare it with the expected pixels to within one level per channel. From the `CasaEngine.Demos` folder:

```
set CASAENGINE_START_DEMO=PSX sprite semi-transparency
set CASAENGINE_CAPTURE_SCREENSHOT_PATH=psx-modes.png
set CASAENGINE_DEMO_PIXELS_PATH=psx-modes.txt
bin\Debug\net9.0-windows\CasaEngine.Demos.exe
```
