# ADR-0071: The engine draws the effect quads the game pushes

- **Status**: Accepted
- **Date**: 2026-10-07 (plan E19.g G2e read until READY on two lenses; work in the engine authorized by the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19g2e-effect-quads-tasks.md` (tasks G2e-1 to G2e-3). Parent repository:
  `docs/plan-e19-opcodes.md`, step E19.g G2e (rules G2E-R1 to R5); prediction and comparator in `docs/plan-e19-g3c-annexe/`
  (`g2e_predict.py`, `g2e_expected.json`, `g2e_compare.py`). Builds on ADR-0068 (`DrawPsxQuad`) and ADR-0037 (counted asset handles);
  replaces no ADR.

## Context

- The effects of the Alundra port (auras, rays, falling pieces) are free PS1 quads taken from a sheet texture. ADR-0068 gives the engine
  the entry to draw one (`SpriteRendererComponent.DrawPsxQuad`), but nothing lets a game hand the engine a frame of such quads: the
  engine services that exist push backdrops (`ScrollingLayerService`, `CellularLayerService`) or screen fades, not free quads.
- A game DLL pushes its state from `GameManager.UpdateWorld`, which runs before every `GameComponent.Update`; the sibling components
  (`ScrollingLayerComponent`, `CellularLayerComponent`) submit from their own `Update` for that reason.
- A component that resolves textures through the asset catalog cannot be given synthetic textures by a demo unless it has a seam, and the
  seam must not be overwritten by the next `Update` (the case that `CellularLayerComponent.ResolveTextures` leaves open).

## Decision

- A value `EffectQuad(SheetIndex, Window, TopLeft, TopRight, BottomLeft, BottomRight, Mode, SortKey)`: the raw window of the PS1 data (a
  mirrored axis one texel back, so an origin of -1 is allowed), the four world corners (y up) in the order of the data, the PSX mode and
  the sort key.
- `EffectQuadService`, free of any GPU type: `SetSheets` (texture asset ids as `Guid`, `Guid.Empty` for an absent sheet, copies, raises
  `SheetsVersion` on every call), `SetFrame` (copies; **the frame persists until the next `SetFrame` or `Clear`**), `Clear` (sheets and
  frame emptied, version raised).
- `EffectQuadComponent : GameComponent`, on the pattern of `CellularLayerComponent`: on a sheets-version change it resolves the sheets
  through `AssetContentManager.Acquire<Texture>` (the handles are released at the next resolution and at `Dispose`), then submits every
  quad from its own `Update` through `DrawPsxQuad(texture, window, TL, TR, BL, BR, Color.White, 0f, in key, mode, scissor)`, the scissor
  resolved like `CellularLayerComponent` (explicit overload, testable without a GPU). Color is `Color.White` and z is 0: the polygon of
  the original is drawn raw. The corners go through unchanged.
- The public seam `ResolveTextures(Func<Guid, Texture2D>)` **records the sheets version it resolves**, so the next `Update` does not
  resolve that version again through the catalog.
- A sheet that fails to load (a null texture or an exception, a missing catalog entry included) is one warning at resolution and its quads
  are skipped; a sheet index out of range or an absent sheet skips the quad. The component calls `Texture.Load` only when the texture asset
  does not hold its image yet (`Resource` is null).
- `ComponentUpdateOrder.EffectQuads` is appended after `CellularLayers`; `CasaEngineGame.EffectQuadComponent` is created next to the
  cellular layers.
- **The component does not sort**: the order is the one of the sprite pass, from `RenderSortKey2D`, which the producer fixes.
- Proof: tests at the queue level (`EffectQuadServiceTests`, `EffectQuadComponentSubmissionTests`) and the demo `EffectQuadDemo`, whose
  back-buffer dump is compared with an exact prediction of the parent repository (four scenes, factors 1 and 3: opaque pixels exact,
  pixels blended by a PSX mode within one level per channel, outside the noise zone).

## Consequences

- A game (the Alundra DLL, step G3c) can draw any number of free quads per frame by pushing them; nothing in the engine knows what an
  effect is.
- Because the frame persists, a producer that wants nothing drawn must push an empty frame (or `Clear`); a frame in which it forgets to
  push draws the previous one again.
- The submission is once per `Update`: a fixed time step running several updates per drawn frame would queue the frame several times. The
  Alundra project does not use a fixed time step; a game that does must push per update or accept the repeat.
- One service holds one frame and one list of sheets: several producers need several services.
- The guard that skips `Texture.Load` when the image is already there is local to the component: `Texture.Load` and its other callers do not change.
