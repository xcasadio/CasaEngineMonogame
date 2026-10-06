# ADR-0047: Dynamic movement obstacles in the character controller field stage

- **Status**: Accepted
- **Date**: 2026-10-02 (decisions D1-D4 taken with the author on 2026-10-02; plan approved on 2026-10-02, without an engine demo)
- **Source**: this chantier: `ai-agent/tasks/field-movement-obstacles-tasks.md` (decisions D1-D4, points O1-O2), branch
  `chantier/field-movement-obstacles`. Parent repository: `docs/plan-e19-opcodes.md` (D-E19-27, D-E19-29, D-E19-36, step
  E19.d2b). Builds on ADR-0045 (a blocked field step advances to the contact) and ADR-0006 (collision volumes versus
  fields), which are not replaced.

## Context

- `CharacterControllerComponent.MoveWithCollisions` filters the horizontal displacement against the world's
  `ICollisionField` in `ResolveHorizontalDisplacementAgainstField` (axis 1, then axis 2), then sweeps the rigid
  obstacles on what is left. Since ADR-0045 a blocked axis advances to the contact. Nothing in that stage knows about
  other entities: a mover cannot be stopped by a character, a door or a crate that a game moves by itself.
- The rigid sweep ignores the bodies that have no contact response (`BepuPhysicsEngine`): the entities of the Alundra
  port are ghost kinematic bodies, invisible to the sweep and to `TryStepMove`. Blocking them in the sweep would change
  the physics layer for every game.
- The original Alundra tests an entity obstacle before the terrain cell at each candidate position of its dichotomic
  search (parent, D-E19-27). The port needs the same order, with the exact contact of ADR-0045, and the dialogue contact
  of the port is read from the controller (D-E19-29), so the controller must say which obstacle stopped a step.
- A step of 1e-3 px or less is dropped before the field stage (`MinMoveDistanceSquared`): it can neither bypass the
  stage nor be applied raw into an obstacle.

## Decision

- **D1** - The XY blocking between entities is an **optional extension point of the engine** in the field stage of
  the controller; the rule belongs to the game (D-E19-27 of the parent; a gap of the engine is fixed in the engine).
  The engine ships no obstacle registry.
- **D2** - The contact against an obstacle is **exact**, by the same bisection as ADR-0045. The advance stays per axis
  (h1, then h2); no slide along an obstacle (the gap with the original is accepted, step E19.h of the parent).
- **D3** - The contact report says, per axis, which obstacle shortened the step (D-E19-29 of the parent).
- **D4** - The branch starts from `chantier/animation-logical-end-clock`, not yet merged; the author merges that branch
  first, then this one.
- **Interface** `IMovementObstacleProbe`, in `CasaEngine.Framework.Physics` (its signature names `Entity`, which lives
  in `Framework`; placing it in `Engine` would create the first dependency from `Engine` to `Framework`):
  - `bool TryFindObstacle(Entity mover, in Vector3 candidateRootPosition, out Entity obstacle)` returns true when the
    obstacle found blocks the candidate root; `obstacle` is non-null exactly when the method returns true. The contract:
    O(number of obstacles), no allocation, callable several times per entity and per frame, changes nothing, no
    exception raised on each frame. It is consulted only in the field stage, for a non-zero axis, when the controller
    resolves its collision dependencies; it never returns the mover itself.
  - `void DrawDebug(IPhysicsDebugDrawer drawer) { }`: an empty default method, no allocation, in the simulation (logical)
    space of the world, the one of the physics debug view, not the projected render space.
- **Installation**: `World.MovementObstacleProbe`, public and not serialized, with the rules of `CollisionField`:
  `Clear()` resets it to null, `ClearEntities()` keeps it.
- **Field stage**: a private `IsCandidateBlocked` replaces the direct call at the five test sites (whole step of h1 and
  of h2, pre-probe, bisection and final check of `AdvanceBlockedAxisToContact`). It asks the probe **first**, with the
  candidate root (the entity before the cell, as in the binary), **then** the field, only when installed, with the
  center that each site already computes. `IsHorizontalMoveBlocked` is untouched: without a probe the behavior is
  identical bit for bit. The stage is no longer short-circuited when a probe is installed (`field == null && probe ==
  null`); with the probe alone, only the obstacle blocks.
- **Candidate root**: the one the bisection already builds (`rootPosition + axis * amount`; for the whole step,
  `rootPosition + h * amount`), never a center converted back (one ULP of error, 4 to 8 units of 16.16 beyond 512 px,
  which would end the step inside the obstacle).
- **Reported obstacle**: per axis, the obstacle of the **last blocked test** (whole step, pre-probe, bisection or final
  check); null when that last test was blocked by the field or when the axis was not shortened. It is the obstacle
  closest to the contact, the one the binary names when two obstacles follow each other. No extra call. The `out` of
  `AdvanceBlockedAxisToContact` starts with the obstacle of the whole-step test and is replaced only by that of one of
  its own blocked tests.
- **Report**: `CharacterControllerContactReport.H1Obstacle` and `H2Obstacle` (`Entity`, read-only). They are cleared with
  the displacement half (entry of `Move` and `Update`, `Stop`, `Teleport`, `RestoreStateSnapshot`). `H1Curtailed` and
  `H2Curtailed` keep their meaning (D-E19-11). In `Update`, which calls `MoveWithCollisions` twice (ground-inherited
  displacement, then velocity), the obstacle of an axis is that of the velocity call when it shortened that axis,
  otherwise that of the ground call (a moving ground is not tested: the port has none).
- **Debug draw**: under `DisplayPhysics`, after `DrawDebugWorld`, `PhysicsDebugViewRendererComponent` calls an internal
  static helper that calls `DrawDebug` of the installed probe (nothing when the world or the probe is null). The public
  extension `PhysicsDebugDrawerExtensions.DrawAabb` (12 lines) serves the implementations.

## Consequences

- Without a probe nothing changes: same results to the bit, same cost.
- The advance stays per axis and there is no slide along an obstacle of the game's own making; a diagonal walk into an
  obstacle differs from the original until step E19.h of the parent.
- A mover that overlaps an obstacle at the start leaves it only by a step that leaves the overlap entirely, as the
  binary does (D-E19-36 of the parent): a smaller step is stopped at 0 and reports the obstacle.
- The report is additive: two new members, a new internal constructor, no behavior change for existing readers. The
  composition rule in `Update` is written above; the case of a moving ground is not tested.
- Cost, per non-zero axis: one probe call when the axis is free; two when the mover pushes an established contact (whole
  step and pre-probe blocked); 26 to 30 at the single tick where the contact is established (whole step, pre-probe, 24
  iterations, final check). By ADR-0045 (P5) a mover can stall up to 1e-3 px before the obstacle and then pay 26 to 30
  calls at every pushing tick (rare). No pre-filter.
- Several movers see the obstacles in their state at the time of their own call: the result follows the deterministic
  order in which the movers move (registration order of the system, or the caller's order for `Move`). Under a fixed
  time step each sub-step sees the state of its own moment (O2 of the plan); no effect for Alundra (fixed step 0).
- Written limits of the contract:
  - the probe filters the **field stage only**; the rigid sweep and the step-up that follow do not consult it;
  - only the **arrival point** of each trial is tested, so an obstacle thinner than the step can be crossed, in a free
    step as in a step already blocked by a farther obstacle (the predicate is not monotone); no effect for Alundra
    (steps of at most 3 px, boxes of at least 14 px, ghost bodies);
  - the debug coordinates are in the simulation space of the world.
- No engine demo (O1, decided by the author on 2026-10-02): the debug draw and the Alundra recipe of the parent stand in
  for a sample.
