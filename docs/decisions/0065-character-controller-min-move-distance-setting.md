# ADR-0065: The character controller minimum move distance is a per-controller setting

- **Status**: Accepted
- **Date**: 2026-10-06 (decision taken with the author on 2026-10-06 when the parent plan E19.h1b3 was approved)
- **Source**: this chantier: `ai-agent/tasks/e19h1b3-min-move-distance-tasks.md`, branch `chantier/e19h1b3-min-move-distance`. Parent
  repository: `docs/plan-e19-opcodes.md` (section E19.h1b3, decisions D-E19-94 and D-E19-98) and its annex `docs/plan-e19-op22-annexe/`.
  Builds on ADR-0047, which names the constant this ADR replaces.

## Context

- `CharacterControllerComponent` dropped any requested displacement of 0.001 px or less (`MinMoveDistanceSquared`, a `const` of 1e-6):
  in `Move`, in the inherited ground displacement and the velocity displacement of `Update`, at the head of `MoveWithCollisions`, in its
  sweep loop and in the field stage. ADR-0047 relies on it to keep a tiny step from bypassing the field stage.
- A game that owns the vertical motion itself can legitimately step by one fixed-point unit (1/65536 px). The Alundra port does:
  the last, clamped step of the descent of a platform at the original's wait-for-height opcode is -1 unit. The engine threw that step
  away, so the wait never ended (measured on the real DLL, map 115: the wait never finished and the hero never regained control).
- A gap of the engine is fixed in the engine, not worked around in the game.

## Decision

- `CharacterControllerSettings.MinMoveDistance` (JSON key `min_move_distance`, default `0.001`, the historical value) replaces the constant
  at its six usages. The comparison is unchanged: a displacement whose squared length is at most `MinMoveDistance` squared is dropped.
- At `0`, every non-zero displacement is applied; a zero displacement is still dropped.
- The setting is cloned, loaded, validated (negative is rejected) and saved by the editor serializer like the other controller settings.
- The default keeps every existing controller as it was; a game opts in per controller.

## Consequences

- No behaviour change unless a game sets the value: the engine suite is unchanged apart from the new tests.
- At `0` the field stage and the sweeps see steps of any size, including horizontal ones and inherited ground displacements; the
  bisection of ADR-0045 and ADR-0047 handles them like any other step. The cost is a few more sweeps per frame for such a controller.
- The Alundra port sets `0` on its script-driven entities (parent plan E19.h1b3).
