# ADR-0008: Compatibility posture — replace rather than duplicate

- **Status**: Accepted
- **Date**: 2026-08
- **Source**: `docs/engine/collision-2d-3d-architecture.md:12-17`, `:665`; `ai-agent/audits/analysis-bepuphysics2-migration.md:113-116`

## Context

No project uses CasaEngine outside the demos, the editor, the tests and the Alundra converter
(which references no physics type — verified) (source: `docs/engine/collision-2d-3d-architecture.md:12-14`).

## Decision

- Posture de compatibilité (project decision, 2026-08): API and serialized-asset backward
  compatibility is not a goal — replace, don't duplicate; every phase removes what it replaces.
  Two invariants only: the repository compiles and demos/tests pass at every phase; regenerable
  assets (Alundra export) are revalidated by a full export
  (source: `docs/engine/collision-2d-3d-architecture.md:14-17`).
- Forbidden (14): do not keep a legacy dual path — every phase removes what it replaces
  (source: `docs/engine/collision-2d-3d-architecture.md:665`).
- The Bepu migration audit restates the same project posture explicitly and applies it to the
  physics backend: no API or asset backward compatibility, "replace, don't duplicate", the same
  two invariants (repository compiles, demos/tests pass at every phase). The same document notes
  that per-fixture attribution and per-body filtering are explicitly called out as "backend
  implementation details" in the architecture doc, and that Bepu lifts them
  (source: `ai-agent/audits/analysis-bepuphysics2-migration.md:113-116`).

## Consequences

- Six assets under `Projects/` serialize `physics_definition` with the Bullet-era fields:
  `SampleProject/Entities/Box.entity`, `SampleProject/DefaultWorld.world`,
  `RPGDemo/Entities/{weapon_rock,character_octopus,character_link}.entity`,
  `RPGDemo/DefaultWorld.world` (source: `ai-agent/audits/analysis-bepuphysics2-migration.md:106-110`).
  Per the replace-not-duplicate posture, no compatibility shim is kept for them.
- Implementation status verified in code: `rg -i bulletsharp` over `*.cs`/`*.csproj` returns no
  result; `CasaEngine/Engine/Physics/PhysicsDefinition.cs` no longer declares the Bullet-specific
  fields (`AdditionalDamping*`, `RollingFriction`, `LinearSleepingThreshold`/`AngularSleepingThreshold`,
  `LocalInertia`) listed in the audit — the posture was applied to `PhysicsDefinition`.
