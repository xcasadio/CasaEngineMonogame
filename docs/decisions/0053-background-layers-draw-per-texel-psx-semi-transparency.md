# ADR-0053: Background layers draw per-texel PSX semi-transparency

- **Status**: Accepted
- **Date**: 2026-10-05 (plan E19.g G2c read until READY on 2026-10-05; work in the engine authorized by the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19g2c-backdrop-stp-tasks.md` (rule G2c-R2). Parent repository:
  `docs/plan-e19-opcodes.md`, step E19.g G2c and decision D-E19-68 (the author's rule "the binary decides": the backdrops render the
  per-texel semi-transparency like the original). Extends ADR-0051 (the same two disjoint passes, for the sprites of entities); replaces
  no ADR.

## Context

- The sheets of the backdrop layers of the Alundra port are the content of the PSX video memory: the "STP" texels (bit 15 of the
  colour word) are exported at alpha 128, the other drawn texels at alpha 255, the null word at alpha 0. In the original binary a
  primitive of a layer is semi-transparent when the blend mode of the layer is not 0, with the rate `BlendMode - 1`; the GPU then blends
  only the STP texels of that primitive, the others are drawn opaque.
- `ScrollingLayerComponent` and `CellularLayerComponent` drew each covering quad or cell with one blend state per layer
  (`definition.Blend`), as a single queue entry on the neutral alpha window: the STP texels of a layer were blended or not as a whole,
  so a layer whose texels are not all STP (the rain of two maps, the first layer of eight others) was drawn wrong.
- Since ADR-0051 the sprite shader tests the raw alpha of a texel against a window `(min ; max]`, and
  `SpriteRendererComponent` queues a sprite with a PSX mode as two entries of the same sort key on two disjoint windows.

## Decision

- `ScrollingLayerDefinition` and `CellularLayerDefinition` get a public field `PsxSemiTransparency` (`SpritePsxSemiTransparency`,
  default `None`).
- A new internal overload of `SpriteRendererComponent.DrawSprite` takes a `Texture2D`, a source rectangle, an origin, a position, a
  rotation, a scale, a colour, a z, a sort key, effects, a scissor rectangle and a mode. `None` queues one opaque entry on the neutral
  window, like the overload without a mode; any other mode queues the two entries of ADR-0051 (opaque texels on `(0.75 ; 1]` with the
  opaque state, then STP texels on `(0.25 ; 0.75]` with the state of the mode, the colour (64, 64, 64) for `Mode3`) with the same sort key
  and z. The `Sprite` overloads are not redirected to it (they keep their debug drawing).
- `ScrollingLayerComponent.Submit` (each covering quad) and `CellularLayerComponent.Submit` (each cell) call it when the field is not
  `None`; the `Blend` of the layer is then ignored (the mode alone decides). With `None` they call the existing overload with
  `Blend`, unchanged. The per-cell sort key of ADR-0052 is kept (both entries of cell `c` carry the offset `-c`); the tint overlay of the
  scrolling layers is not touched.

## Consequences

- A layer that carries a mode costs one more queue entry per quad or cell and, when the two entries are not adjacent in the sorted
  order, one more draw call; a layer without a mode costs nothing more.
- The blend uses the PSX formulas on 8-bit colours, not the 5-bit arithmetic of the hardware; `Mode0` rewrites the back-buffer alpha
  (191), as for sprites.
- The window tests the raw alpha with point sampling: a layer sheet is sampled by the renderer's sampler, the same limit as ADR-0051.
- The game decides the mode of each layer; the engine keeps no knowledge of the original's blend table. A texture loaded from a PNG
  keeps its alpha of 128 (`Texture2D.FromStream`, proved on a device by the demo `Background layers PSX semi-transparency`).
- Tests without a graphics device work on the queue (`ScrollingLayerPsxSemiTransparencyTests`, `CellularLayerPsxSemiTransparencyTests`);
  the pixels are proved by the demo.
