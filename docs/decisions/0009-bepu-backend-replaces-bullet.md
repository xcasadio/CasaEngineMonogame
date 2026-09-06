# ADR-0009: Physics backend bepuphysics2 replacing BulletSharp

- **Status**: Accepted
- **Date**: 2026-08-22
- **Source**: `docs/engine/collision-2d-3d-architecture.md:36-42`, `:664`; `ai-agent/audits/analysis-bepuphysics2-migration.md:119`, `:245-253`

## Context

`docs/engine/collision-2d-3d-architecture.md:36-42` records, as a later addendum (2026-08) to the
document, that the backend described throughout as Bullet has since been replaced by bepuphysics2.
`ai-agent/audits/analysis-bepuphysics2-migration.md` (dated 2026-08-22) is the migration analysis
that took the concrete backend decisions.

## Decision

- Backend Bepu (2026-08) replaces Bullet. Consequences for the points touched by the collision
  architecture document: Bepu has no shape scale — local scale is baked into dimensions and
  compound offsets at creation time; the sensor is no longer a body flag but a decision made in
  the narrow-phase callback (`ConfigureContactManifold` returns `false`, no constraint); contacts
  carry the child index on both sides of a pair, including for compounds — an improvement over the
  vendored BulletSharp, which only gave it on the sweep/raycast side, never on the contact side
  (source: `docs/engine/collision-2d-3d-architecture.md:36-42`).
- Version: `2.5.0-beta.29` — the only actively maintained line, and the one Stride consumes in
  production; accepting a pre-release with no corresponding stable
  (source: `ai-agent/audits/analysis-bepuphysics2-migration.md:119`, `:245-246`).
- `LinearFactor`: velocity cancellation inside the integrator (simple, bounded drift), not a servo
  constraint; a servo constraint only if the cancellation approach's test fails
  (source: `ai-agent/audits/analysis-bepuphysics2-migration.md:247-249`).
- `PhysicsDefinition`: the Bullet-specific fields are removed (project posture), rather than kept
  inert (source: `ai-agent/audits/analysis-bepuphysics2-migration.md:250`).
- Multithreading: out of scope for this migration (the flag is already present, buffer structure
  ready from tranche 2, but not exercised)
  (source: `ai-agent/audits/analysis-bepuphysics2-migration.md:253`).
- Forbidden (13): do not expose Bullet types in gameplay APIs
  (source: `docs/engine/collision-2d-3d-architecture.md:664`).

## Consequences

- Static sensors for trigger tiles (decision point 4 in the same "before starting" list,
  `ai-agent/audits/analysis-bepuphysics2-migration.md:251-252`, recommended for "tranche 5") were
  not left deferred: `CasaEngine/Framework/Scene/Entities/Components/TileMapComponent.cs:1078` and
  `:1137` carry the comment "A trigger tile is a static sensor: the Trigger profile blocks nothing
  (IsSensor)" — implemented.
- Implementation status verified in code: `Directory.Packages.props:9` pins
  `<PackageVersion Include="BepuPhysics" Version="2.5.0-beta.29" />`;
  `CasaEngine/Framework/Physics/Bepu/BepuPhysicsEngine.cs` is the new engine;
  `CasaEngine/Framework/Physics/Bepu/BepuPoseIntegratorCallbacks.cs` implements the per-body
  `LinearFactor` handling described above (velocity zeroing per locked axis, lines 65-75);
  `CasaEngine/Framework/Physics/Bepu/BepuNarrowPhaseCallbacks.cs` implements
  `ConfigureContactManifold`; `CasaEngine/Engine/Physics/PhysicsDefinition.cs` no longer declares
  the Bullet-specific fields (`AdditionalDamping*`, `RollingFriction`,
  `LinearSleepingThreshold`/`AngularSleepingThreshold`, `LocalInertia`); `rg -i bulletsharp` over
  `*.cs`/`*.csproj` returns no result.
- Multithreading: no multithread-specific code path was found in
  `CasaEngine/Framework/Physics/Bepu/*.cs` in this pass, consistent with "out of scope, not
  exercised" — unverified beyond that absence (not a proof of intent, only of current state).
