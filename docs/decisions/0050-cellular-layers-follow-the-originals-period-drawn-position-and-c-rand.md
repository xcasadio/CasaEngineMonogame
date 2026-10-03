# ADR-0050: Cellular layers follow the original's period, drawn position and C library rand

- **Status**: Accepted
- **Date**: 2026-10-03 (plan E19.m2 read until READY on 2026-10-03; work in the engine authorized by the author on 2026-10-03)
- **Source**: this chantier: `ai-agent/tasks/e19m2-cellular-binary-tasks.md` (rules M2-R1 to M2-R3). Parent repository:
  `docs/plan-e19-opcodes.md`, step E19.m2 and decision D-E19-66 (the original binary `ALUN_CD.EXE`, France, decides). Replaces
  the choice of the random stream made by decision D7 of `docs/plan-e9d-mode-cellulaire.md` of the parent repository; replaces no ADR.

## Context

- `CellularLayerService` ports the two cell routines of the original (type 0 `0x8005CB38`, type 2 `0x8005D05C`). Three rules differed
  from the binary, read in its disassembly:
  - the period step: the counter is incremented on every tick and the step applies when `|P|` is below the counter before the
    increment (`0x8005CC64`, `0x8005CCBC`, `0x8005D218`, `0x8005D278`), then the counter returns to 0: one step every `|P| + 2`
    ticks. The engine stepped every `|P|` ticks;
  - the drawn position: the routines draw `sx`/`sy` computed before the wraps and the respawn (`0x8005CDF0`, `0x8005D3AC`); the
    engine drew the position after them, so a wrapped cell showed one tick early at the opposite edge;
  - the respawn abscissa: the type 2 routine takes one value of the C library `rand()` (`0x80081E6C`: `s = s * 0x41C64E6D + 0x3039`,
    result `(s >> 16) & 0x7FFF`) and sets `posX = rand() / 102` with a signed, truncating divide (`0x8005D324`-`0x8005D340`), 0 to
    321. The engine computed `(u32 * 320) >> 32` from a 32-bit value of the game's own stream (decision D7).
- The scrolling routine keeps its own rhythm (`0x8005C7E0`, a step every `|P|` ticks): `ScrollingLayerService` is correct.
- The period reaches the engine as the raw signed byte, with no transformation on the way.

## Decision

- Period: a cell steps when its counter before the increment exceeds `|P|`, then the counter is cleared (every `|P| + 2` ticks), on
  both axes and for both `Normal` and `FallRespawn` cells.
- Drawn position: `DrawX` is the position before the X wrap, `DrawY` the position before the Y wrap or the respawn; the wraps and the
  respawn only move the stored position.
- Respawn: the delegate given to `CellularLayerService.Advance` (`CellularLayerComponent.RandomSource`) keeps its signature
  `Func<uint>` and its contract becomes "the next value of the C library `rand()`, 0 to 0x7FFF". The engine computes
  `PosX = (int)next() / 102`. The delegate is called once per respawn, as before. The engine still owns no generator.

## Consequences

- No signature change: callers that pass `() => 0u` or count calls keep working. Only the documented value domain of the delegate
  changes; a caller still passing a full 32-bit value gets an abscissa far outside the screen (a game must pass the C library value).
- Type 0 cells that move only by their period slow down (1.25 to 2 times for periods 2 to 8 in absolute value), the others by 1 % to
  29 %; a wrapping or respawning cell is absent for one tick; a respawn abscissa can be 320 or 321 (off screen, wrapped on the next
  tick).
- Other differences between the cell routines and the engine are not covered here (global wave counter, truncated type 0 parallax,
  draw order inside a layer; parent plan O-E19-51 to O-E19-53).
