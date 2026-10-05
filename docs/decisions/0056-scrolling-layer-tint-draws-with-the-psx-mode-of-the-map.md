# ADR-0056: The scrolling-layer tint draws with the PSX mode of the map

- **Status**: Accepted
- **Date**: 2026-10-06 (plan E19.g G2d read until READY on 2026-10-05; work in the engine authorized by the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19g2d-overlay-blend-tasks.md` (rule G2d-R1). Parent repository:
  `docs/plan-e19-opcodes.md`, step E19.g G2d and O-E19-58 (the author's rule "the binary decides"). Extends ADR-0053 to the tint overlay
  of the scrolling layers; replaces no ADR.

## Context

- In the original binary the byte `header+0x23` of a map sets a full-view overlay: 0 draws nothing, 1 to 4 draw a semi-transparent
  untextured `TILE` of 320 by 240 whose rate is `v - 1`, read in the same table as the layers. The port averaged it for every map. Of the
  330 maps, 15 have a mode 1 (average, right) and one, the burning Inoa (map 293), a mode 2 (additive): it was darkened by half instead of
  being reddened a little.
- The tint of `ScrollingLayerService` was submitted as one `AlphaBlend` entry with the colour of its definition; the DLL baked an alpha
  of 128 into the colour.
- The two-entry path of ADR-0051 and ADR-0053 does not suit a flat primitive: its white pixel has an alpha of 255, so the entry meant for
  the STP texels would never draw it and the opaque entry would draw it opaque.

## Decision

- `ScrollingTintDefinition` gets a `SpritePsxSemiTransparency PsxSemiTransparency` through a new three-argument constructor; the
  two-argument constructor gives `None`.
- `ScrollingLayerComponent.Submit` queues, for a mode other than `None`, **one** entry with the same sort key, the same z and the neutral
  alpha window, with the blend state of the mode: `Mode0` `AlphaBlend` with `(R, G, B, 128)`; `Mode1` `Additive` and `Mode2`
  `Subtractive` with `(R, G, B, 255)`; `Mode3` `Additive` with each channel times 64/255, rounded to nearest (the factor of `Mode3` for
  sprites and layers; unused in the data). `None` keeps today's entry: `AlphaBlend`, the colour as given.

## Consequences

- The game decides the mode and the colour; the engine keeps no knowledge of the original's table. A tint without a mode costs and draws
  exactly as before.
- The blend uses the PSX formulas on 8-bit colours, not the 5-bit arithmetic of the hardware; `Mode0` rewrites the back-buffer alpha (191),
  as for sprites.
- Whether the tint covers the whole view depends on its depth against the tiles of the same frame, unchanged by this decision.
- Tests without a graphics device work on the queue (`ScrollingLayerPsxSemiTransparencyTests`); the pixels are proved by the demos
  `Background tint PSX mode 1` and `Background tint PSX mode 0`.
