# ADR-0052: One global wave counter, a truncated type-0 parallax factor and cell 0 drawn on top in cellular layers

- **Status**: Accepted
- **Date**: 2026-10-03 (plan E19.m3 read until READY, review 2, on 2026-10-03; work in the engine authorized by the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19m3-cellular-order-tasks.md` (rules M3-R1 to M3-R3). Parent repository:
  `docs/plan-e19-opcodes.md`, section 1.2s.4 (E19.m3), audit of 2026-10-03 (binary `ALUN_CD.EXE`, France). The author's rule
  "the binary decides" applies. Amends ADR-0049 for the wave tick only; complements ADR-0050.

## Context

- The wave counter of the original is one 32-bit word (`0x800C48C4`) shared by both background layers. Three instructions touch
  it: the increment in the backdrop driver (`0x8005B6D8`, `0x8005B6E8`), done once per main-loop tick after the map's
  backdrop-enabled test and before the palette program and the two layer-mask tests, and the read of the wave cells
  (`0x8005D448`). Nothing resets it (not the map load, not the scrolling-mode routine) and it is 0 at program start. Only its low
  8 bits reach a wave index. So a masked layer does not freeze it, and a map without a backdrop does not advance it.
- The engine kept one counter per layer, zeroed on load and on every reset and frozen with a masked layer (inherited from the
  decompilation). ADR-0049 and the E19.k2 plan wrote that the wave tick is inside the guarded call: wrong for the wave counter
  only; the cadence, the cell positions and period counters, the random draws, the scrolling layers' per-tick state and the
  "not drawn" rule are inside the guarded call and stay as ADR-0049 says.
- A type-0 cell (the engine's `Normal`) computes its camera factor once at initialisation, `Num / Den` in signed integer
  division (`0x8005C0AC`-`0x8005C158`), then multiplies the camera by it every tick (`0x8005CB78`, `0x8005CBB8`). A type-2 cell
  (`FallRespawn`) computes `camera * Num / Den` every tick (`0x8005D0C4`-`0x8005D14C`). The engine used the second formula for
  both. On maps 123 and 124 (layer 1) this leaves 30 cells per map scrolling at half the camera speed instead of staying fixed.
- Every cell type inserts its primitive at the head of the same single-entry ordering-table slot, then the layer's mode primitive
  on top of them: cell 0 is drawn last, over the others. The engine gave one sort key to the whole layer and relied on
  `List.Sort`: up to 16 entries it keeps the submission order (the last cell on top, the reverse), above that the order of equal
  keys is unspecified.

## Decision

- **Wave counter.** `CellularLayerService` owns one byte. `Advance` adds 1 to it at every tick, before the layer loop, as long as
  `SetLayers` was called since the last `Clear` (even with an empty list: the game calls it for every map with a backdrop, the
  engine-side equivalent of the original's enabled flag). A new service and `Clear` close this gate. `SetLayers`, `Clear` and
  `ResetLayerRuntimeState` never reset the byte, and a masked layer does not freeze it (a masked layer still skips the rest of its
  per-tick state, as ADR-0049 says). `TryGetLayerState` reports it. No new public API; the public struct is unchanged, only its
  meaning (the service's counter). This amends ADR-0049's "wave tick" only.
- **Type-0 parallax.** `Normal` cells compute `den != 0 ? camera * (num / den) : 0` on each axis, with a truncated integer
  division. `FallRespawn` cells keep `camera * num / den`.
- **Cell order.** Each cell has its own sort key, `LocalSortOffset = -cellIndex` (the same device as the animated sprite
  parts), so cell 0 sorts last and is drawn on top, whatever the stability of the queue sort. The offset follows the cell index,
  not the submission count. The layer-level key fields still compare first, so layers with distinct (pass, `SortingLayer`,
  `OrderInLayer`) keep their order. Precondition: layers that share all three fields would have their cells interleaved by
  index (the comparison reads `LocalSortOffset` before `StableId`); the Alundra game does not build such layers (`OrderInLayer`
  is 1 for layer 0 and 0 for layer 1).
- No stable sort of the sprite queue: it would change every equal-key sprite of the engine.

## Consequences

- The wave phase on entering a map now depends on the ticks counted since the service was created, like the original's depends on
  the ticks since boot; the exact original phase is not reachable (the game also pushes ticks during warp fades).
- Cells that scroll at a fraction of the camera below 1 in absolute value now stay fixed, as in the original.
- One more key per cell entry in a cellular layer (the same cost as animated sprite parts).
- The service outlives a return to the title in the same process, so its counter keeps running there; whether the original
  restarts its executable at that point (resetting the word) is not verified.
