# ADR-0051: PSX semi-transparency of sprites as two disjoint passes

- **Status**: Accepted
- **Date**: 2026-10-03 (plan E19.g G2a read until READY on 2026-10-03; work in the engine authorized by the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19g2a-psx-semi-tasks.md` (rules G2a-R1 to G2a-R3). Parent repository:
  `docs/plan-e19-opcodes.md`, step E19.g G2a and decision D-E19-52 (the author: per-texel semi-transparency also applies to entity
  sprites). Replaces no ADR.

## Context

- The sprite sheets of the Alundra port carry the PSX semi-transparency of each texel as alpha: 0 transparent, 128 for the "STP"
  texels, 255 opaque (no other value in the exported sheets). The mode of the blend (average, additive, subtractive, quarter) is a
  property of the sprite (the primitive's ABR bits in the original binary), not of a texel.
- The sorted sprite path drew every sprite with one blend state per entry (`SpriteBlendMode`), and `SpriteData` had no blend field:
  entity sprites drew opaque, the STP texels included.
- The sprite shader rejected a texel whose product `texel * Color` had an alpha of 0.01 or less, and nothing else. The effect is shared
  by the sorted draw, the static batch and `DrawDirectly`, and its parameters persist from one draw to the next.
- The queue held at most 10 000 entries: `UpdateBuffer` wrote the vertices of every entry into a 40 000 vertex array and threw
  `IndexOutOfRangeException` on the 10 001st entry; the vertex buffer had the same size.

## Decision

- `SpriteData` gets an optional field `PsxSemiTransparency` (`SpritePsxSemiTransparency`: `None`, `Mode0` to `Mode3`), serialized as
  `psx_semi_transparency` by name, written only when it is not `None` and read as `None` when absent (or unknown, with an error log).
  Existing sprite files keep their bytes.
- The sprite shader gets a parameter `AlphaWindow` (float2): a texel is rejected when its **raw** alpha is outside `(min ; max]`, in
  addition to the existing product test. The neutral window `(-1 ; 2]` rejects nothing, so a draw that does not use the window is
  unchanged. Point sampling only (a filtered sample would blend the alpha of neighbours; documented in the shader).
- A part with a mode other than `None` is queued by a new overload of `SpriteRendererComponent.DrawSprite` as two entries of the same
  sort key on two disjoint windows: opaque texels `(0.75 ; 1]` with the opaque blend state, then STP texels `(0.25 ; 0.75]` with the
  blend state of the mode: `Mode0` non-premultiplied alpha blend, `Mode1` additive, `Mode2` subtractive, `Mode3` additive with the
  colour (64, 64, 64) **replacing** the component colour (the original binary has no tint per entity). The doubling depends only on the
  PSX mode, never on `SpriteBlendMode`; a part with no mode stays one entry on the neutral window. `AnimatedSpriteComponent` passes the
  mode on its sorted path only; the path by `zOrder` stays opaque. No existing `DrawSprite` overload changes.
- Every path that draws with the sprite effect (`Draw` before the loop and per entry, `DrawStaticBatch`, `DrawDirectly`, and the
  shader reload) sets its window, so no window leaks into the next draw. A shader without the parameter (a project copy) is tolerated.
- The queue capacity is open: the vertex staging array grows (to the larger of the needed size and twice its length) and the vertex buffer
  is recreated at that size when it is outgrown; neither shrinks. The index buffer (six indices, base vertex per entry) does not change.
  The filling of the vertices is a separate internal step (`FillVertices`) from the upload.

## Consequences

- Sprites that carry a mode draw their STP texels blended and their opaque texels opaque; the cost is one more queue entry per such
  sprite, and one more draw call when the two entries are not adjacent in the sorted order.
- The blend uses the PSX formulas on 8-bit colours, not the 5-bit arithmetic of the hardware (a few levels of difference). `Mode0`
  rewrites the back-buffer alpha (191 instead of 255), so an in-process capture saved as PNG shows those pixels translucent.
- Quads with four free vertices are still drawn as rectangles (a later tranche of the port). Entities without a depth-sortable component
  stay opaque.
- Growing the buffers allocates during the draw, once, when a frame exceeds the previous maximum queue length.
- Tests without a graphics device work on the queue and on the internal seams (`FillVertices`, `AlphaWindowWriter`); the pixels are
  proved on a device by the demos `PSX sprite semi-transparency` and `Sprite queue capacity`.
