# Effect quads (E19.g G2e)

The engine side of the effects of the Alundra port (ADR-0071): a game pushes, every drawn frame, a list of **free PS1 quads** with their
sheet, raw texture window, four world corners, PSX mode and sort key, and the engine draws them through
`SpriteRendererComponent.DrawPsxQuad` ([sprite-psx-semi-transparency.md](sprite-psx-semi-transparency.md), ADR-0068). **Engine = mechanism,
game DLL = policy**: the service knows nothing of what an effect is, how its quads are built or where its sheets come from.

```text
EffectQuadService      sheet texture ids + the last pushed frame of EffectQuad values, no GPU type
        ^
EffectQuadComponent    GameComponent: resolves the sheets, submits every quad of the frame
```

Entry point from the game: `game.EffectQuadComponent.Service`.

## `EffectQuad`

A plain value (`CasaEngine.Framework.Rendering.EffectQuads.EffectQuad`):

| Field | Meaning |
|---|---|
| `SheetIndex` | Index into the sheets given to `SetSheets`. |
| `Window` | The raw texture window of the PS1 data (`u, v, w, h`, texels). A mirrored axis names its window one texel back, so the origin may be -1: the clamped sampler reads it as column or row 0. |
| `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight` | World positions (units = PS1 pixels, **y up**), in the order of the PS1 data. The mirror is in the corners, the window is never flipped. |
| `Mode` | `SpritePsxSemiTransparency`: `None` is opaque; `Mode0` to `Mode3` draw the opaque texels, then the STP texels with the blend of the mode (ADR-0051). |
| `SortKey` | `RenderSortKey2D` of the quad in the sprite pass. |

## `EffectQuadService`

No GPU type, so it is tested headless.

- `SetSheets(ReadOnlySpan<Guid>)` copies the texture asset ids, `Guid.Empty` for a sheet the producer does not have, and raises
  `SheetsVersion` (even for the same ids: the caller decides when its sheets change).
- `SetFrame(ReadOnlySpan<EffectQuad>)` copies the quads (the internal array only grows). **The frame persists** until the next `SetFrame` or
  `Clear`: a frame in which the game pushes nothing draws the previous one again, so a producer that wants nothing drawn pushes an empty frame.
- `Clear()` empties the sheets and the frame and raises `SheetsVersion`.
- Reads: `SheetsVersion`, `SheetCount`, `GetSheetId(i)`, `QuadCount`, `GetQuad(i)`.

## `EffectQuadComponent`

Same house pattern as `CellularLayerComponent`; `ComponentUpdateOrder.EffectQuads` is appended after `CellularLayers`.

- Each `Update`: if `SheetsVersion` changed, the sheets are resolved through `AssetContentManager.Acquire<Texture>` (the counted handles are
  released at the next resolution and at `Dispose`, ADR-0037); then every quad of the frame is submitted.
- The submission is from `Update`, not `Draw`: the game pushes from `GameManager.UpdateWorld`, which runs before every component `Update`. The
  frame persists, so each drawn frame submits it once; a fixed time step that ran several updates per drawn frame would queue it several
  times (the Alundra project sets `IsFixedTimeStep` to false).
- `ResolveTextures(Func<Guid, Texture2D>)` is the public seam of a demo or a test. It **records the version it resolves**, so the next `Update`
  does not resolve that version again through the catalog (the synthetic texture is not overwritten).
- A sheet that fails to load (a null texture or an exception of the loader, a missing catalog entry included) is logged once at resolution and
  its quads are skipped. A sheet index out of range, an absent sheet (`Guid.Empty`) or a failed one: the quad is skipped, silently.
- `Submit(SpriteRendererComponent, Rectangle scissor)` calls `DrawPsxQuad(texture, window, TL, TR, BL, BR, Color.White, 0f, in key, mode,
  scissor)` for each quad, in the order of the frame: `Color.White` and z = 0 because the polygon of the original is drawn raw, the corners
  go through unchanged (no flip). The scissor is the device scissor, resolved like `CellularLayerComponent`, never read inside `Submit`.
- **The component does not sort.** The order is the one of the sprite pass, from `RenderSortKey2D`: the producer fixes it with the key (the
  Alundra DLL gives image `k` of `N` the local offset `N - 1 - k`, so that image 0 is drawn last, on top).

## Demo and proof

`CasaEngine.Demos/Demos/PsxSemiTransparency/EffectQuadDemo.cs` ("Effect quads"): a synthetic 512 x 1105 address texture (one colour per
texel, STP texels and holes), four scenes (`CASAENGINE_EFFECTQUAD_SCENE`: `a_aura_real`, `a_aura_mix`, `b_mirror_clamp`, `c_ties_tall`), the
camera mapping one PS1 pixel to `k` screen pixels (`CASAENGINE_EFFECTQUAD_ZOOM`, 1 or 3). It dumps its back-buffer in process
(`CASAENGINE_EFFECTQUAD_DUMP_PATH`, int32 width, height, k, then RGBA) for the comparison with the prediction of the parent repository
(`docs/plan-e19-g3c-annexe/g2e_compare.py`): opaque pixels exact, pixels blended by a PSX mode within one level per channel, outside the
noise zone. Run it from the `CasaEngine.Demos` folder with `CASAENGINE_START_DEMO="Effect quads"`.

## Limits

- Like every free quad, the quads are resampled at the output resolution (ADR-0068): exact at k = 1 and for 1:1 quads at any factor.
- The service holds one frame and one list of sheets; several producers would need several services.
