# ADR-0045: A blocked step on the cell collision field advances to the contact

- **Status**: Accepted
- **Date**: 2026-09-29 (decisions D-E19-8 and D-E19-11 taken with the author on 2026-09-29; plan approved on 2026-09-29)
- **Source**: this chantier: `ai-agent/tasks/field-move-to-contact-tasks.md` (decisions D1-D4, points P1-P5), branch
  `chantier/field-move-to-contact`. Parent repository: `docs/plan-e19-opcodes.md` (D-E19-8, D-E19-11, step E19.a2) and
  `docs/plan-e3-collisions.md` (decision C5, the "no partial displacement" rule this ADR refines). Refines ADR-0006
  (collision volumes versus fields), which is not replaced.

## Context

- `CharacterControllerComponent.MoveWithCollisions` first filters the horizontal displacement against the world's
  `ICollisionField` (`ResolveHorizontalDisplacementAgainstField`), then sweeps the rigid obstacles on the displacement
  that is left. The field tests horizontal axis 1, then axis 2. For each axis, when one of the four corners of the
  footprint at the candidate position is blocked, the axis displacement was set to **zero**. That "no partial
  displacement" rule comes from decision C5 of the parent's `docs/plan-e3-collisions.md`, which presented it as a
  simplification of the original game's dichotomic search. No ADR states it: it lives in the XML docs of the method and of
  `CharacterControllerContactReport`. No production code reads `H1Curtailed` / `H2Curtailed`.
- ADR-0006 only says that the field filters the displacement axis by axis. The rigid sweep already goes to the
  contact (minus `SkinWidth`) and slides.
- The original Alundra (`ComputeXYPosition`, `0x80037730` of the binary) halves the whole force, retries from the last
  accepted position and accumulates the accepted half steps, down to the limit of its precision. A blocked step advances
  to the contact.
- Defect found in game (recipe of step E19.a of the parent): in the cabin of map 390 a script walks the hero exactly
  80 px north against a stop. The hero stopped 1.34 px before it, and the script waited forever. Put on the original's
  contact position (y = 215.0), the hero finishes the scene.
- The field's only production implementation (Alundra) has 24x16 px cells, samples whole pixels and computes slope
  heights per pixel. `ICollisionField` exposes only `TrySampleGround`: there is no cell size to compute an exact
  boundary from.

## Decision

- **D1** - On the cell collision field, a step blocked on an axis advances to the farthest unblocked position, with **no
  margin** on the grid (D-E19-8 of the parent).
- **D2** - The axis order is kept (axis 1, then axis 2 from the advanced position). The original's slide along a wall
  when a single corner touches (`didAdjustForObstacle`) is not ported here (D-E19-9, step E19.h of the parent).
- **D3** - `H1Curtailed` / `H2Curtailed` mean "the requested step was shortened on this axis", whether the remaining
  displacement is zero or not (D-E19-11). The signature does not change; the documentation is reworded.
- **D4** - The engine does not compute a "blocked" flag for the game. The Alundra DLL keeps its own rule (a shortfall of
  more than 0.01 px), which raises its flag one tick earlier than the original does; the gap is recorded in the parent
  (D-E19-10).
- **Algorithm** (P1): a bisection per axis on the candidate position of the **root** (`root + axis * amount * t`, the
  center recomputed from it exactly as `ResolveFootprint` does), `t` in [0, 1], **24 fixed iterations**, no allocation.
  The whole step is tested first, so a free step costs one set of 4 samples, as before. A 1-ULP pre-probe (the coordinate
  moved by one representable float in the direction of the step, `MathF.BitIncrement` / `MathF.BitDecrement`) keeps the
  amount at 0 when the entity already pushes on a wall, for 8 samples. The returned displacement is exactly the tested
  amount, so `start + displacement` falls back on the validated candidate root bit for bit, and a final check
  recomputes the center from the retained root and steps back one ULP if it is blocked.
- The search variable is the root, not the center: a bisection on `center + amount` can end 1 to 2 ULP inside the wall
  when the start root lies in [255.5, 256), [511.5, 512) or [1023.5, 1024).

## Consequences

- Every entity with a controller now stops against walls up to one step closer than before (1.25 to 2.44 px for
  Alundra's steps), which is closer to the original. Only the Alundra port installs a field today.
- The change applies to every caller of `MoveWithCollisions` with a field: `Move`, and both calls of `Update` (the
  displacement inherited from the ground and the velocity displacement). On the `Update` path, the recomputed
  velocity (`actual / dt`) keeps a partial value on the frame of the contact.
- No slide along walls (D2): a diagonal walk into a corner still differs from the original until step E19.h.
- Accepted limits (P5): the rigid sweep drops a residual advance of less than 1e-3 px, and a requested step of less than
  1e-3 px is never tested against the field (existing behavior). The contact is therefore exact to 1 ULP or 1e-3 px.
- Tests, reference traces and comments of the Alundra port that the contact moves are re-measured in step E19.a2 of the
  parent, after its submodule pointer moves.
