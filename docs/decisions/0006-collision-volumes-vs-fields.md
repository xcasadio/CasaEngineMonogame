# ADR-0006: Collision volumes vs collision fields

- **Status**: Accepted
- **Date**: 2026-08
- **Source**: `docs/engine/collision-2d-3d-architecture.md:312-366`, `:669`

## Context

Every modern engine already specializes its heightfields; tile-based games resolve movement
against a grid by O(1) lookup, not against thousands of static boxes. Baking a map of 3,000+
cells into physics bodies would be both slow and wrong — slopes and step-up are rules, not
geometry (source: `docs/engine/collision-2d-3d-architecture.md:318-322`).

## Decision

- D5: two collider families — Volumes (discrete fixtures in the broadphase: entities, props,
  triggers, hitboxes) and Fields (dense environment data, queried analytically, NEVER baked into
  bodies: tile heightfields, walkability, slopes, walls)
  (source: `docs/engine/collision-2d-3d-architecture.md:312-317`).
- Delivered shape (phase F, 2026-08): `ICollisionField` with
  `TrySampleGround(worldPosition, maxDropDistance, out GroundSample sample)` and an additive
  overload taking a `walkabilityMask` (default implementation ignores it and forwards). Axis
  contract: "up" is the elevation axis declared by the field, matching the world's
  `SimulationSpacePolicy`; `GroundHeight` is measured along that axis
  (source: `docs/engine/collision-2d-3d-architecture.md:326-347`).
- Delivered implementation: `HeightGridCollisionField`, a regular grid on the two horizontal axes
  whose data (heights, walkability, surface tags) is all caller-supplied. Additive constructor
  parameter `Vector3? up` (default `null` = `Vector3.Up`, unchanged Y-up historical behavior);
  under `up = Vector3.UnitZ` (`TopDownElevation`) the horizontal axes become X and Y and height is
  read/returned on Z; any other value than `±UnitX`/`±UnitY`/`±UnitZ` throws `ArgumentException`. A
  world carries at most one field (`World.CollisionField`, nullable, not serialized)
  (source: `docs/engine/collision-2d-3d-architecture.md:348-353`).
- The natural consumer is the character mover: fields for terrain, sweeps for volumes;
  `TileCollisionManager` becomes an implementation detail of a field backed by the TileMap
  (source: `docs/engine/collision-2d-3d-architecture.md:355-357`).
- Forbidden (17): do not bake a dense terrain into static bodies — it is a field, not volumes
  (source: `docs/engine/collision-2d-3d-architecture.md:669`).

## Consequences

- D5's status per the source: the field family exists and is closed; its first consumer — ground
  and horizontal-blocking resolution for the mover — is delivered by
  `CharacterControllerComponent.UpdateGround`/`MoveWithCollisions`: when `World.CollisionField` is
  installed it replaces the ground snap sweep and filters horizontal movement axis by axis against
  `IsWalkable`/`GroundHeight`, under a `(up, h1, h2)` basis derived from
  `SimulationSpacePolicy.Up` (source: `docs/engine/collision-2d-3d-architecture.md:359-366`).
- Implementation status verified in code: `CasaEngine/Engine/Physics/ICollisionField.cs` defines
  `ICollisionField` (interface at line 64); `CasaEngine/Engine/Physics/HeightGridCollisionField.cs`
  defines `HeightGridCollisionField` (class at line 21).
