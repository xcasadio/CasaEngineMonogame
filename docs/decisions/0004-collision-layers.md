# ADR-0004: Collision layers Shape / Fixture / Body / World, Shape3d as the only public volume vocabulary, no pose on shapes

- **Status**: Accepted
- **Date**: 2026-08
- **Source**: `docs/engine/collision-2d-3d-architecture.md:194-218`, `:658-665`

## Context

At the time of writing, the "Fixture" layer is missing (`docs/engine/collision-2d-3d-architecture.md:200`
marks it "MANQUANT — le trou central"), `PhysicsBody` is mono-shape, and `Shape2d` still carries
`Position`/`Rotation`.

## Decision

- D2: four layers — Shape (pure, immutable, shareable geometry, no pose; `Shape3d`, already
  present), Fixture (shape + local pose + semantics; missing at the time), Body (movement type +
  simulation pose + N fixtures; `PhysicsBody`, mono-shape at the time), World (broadphase, dispatch,
  queries, events, policies; `IPhysicsWorld` / `BulletPhysicsEngine`)
  (source: `docs/engine/collision-2d-3d-architecture.md:194-199`).
- D2.a: `Shape3d` becomes the only public vocabulary for volumes; it is already serialized
  (`SaveShape3d` handles Box/Capsule/Cylinder/Sphere) and already consumed by components.
  `PhysicsShape` is removed — the backend lowers `Shape3d` directly to backend shapes; public
  queries (sweeps) take `Shape3d` + pose; no public intermediary between the shape vocabulary and
  the backend (source: `docs/engine/collision-2d-3d-architecture.md:205-210`).
- D2.b: `Shape2d` remains the 2D authoring vocabulary (sprites, tiles) and lowers to `Shape3d` via
  the world's space policy (D4): `Rectangle → Box(w, h, extrusion depth)`, `Circle → Cylinder` on
  the axis normal to the plane, `Polygon → extruded hull` (v2); the `0.5f` of `Physics2dHelper`
  becomes a named policy parameter (source: `docs/engine/collision-2d-3d-architecture.md:210-213`).
- D2.c: no shape carries a pose — the pose belongs to the fixture. `Shape3d` is already clean;
  `Shape2d` loses `Position`/`Rotation` (its historical defect), which migrate to the authoring
  attachment — `Collision2d` becomes the 2D authoring fixture: pure shape + pose + profile + tag,
  the exact mirror of `ColliderFixture` (source: `docs/engine/collision-2d-3d-architecture.md:214-218`).
- Forbidden (12): do not give `Shape3d` a pose — the pose belongs to the fixture
  (source: `docs/engine/collision-2d-3d-architecture.md:663`).

## Consequences

- A backend constraint assumed at the time of writing (Bullet filters per body, not per shape):
  the runtime groups fixtures by profile — an entity carrying both a hurtbox and an attack hitbox
  needs several bodies. This is a backend implementation detail, not a data-model constraint, and
  it remains true with Bepu, which also filters per collidable (body or static) via
  `AllowContactGeneration`, not per compound child
  (source: `docs/engine/collision-2d-3d-architecture.md:220-225`; confirmed for Bepu by
  `ai-agent/audits/analysis-bepuphysics2-migration.md:80-90`).
- Implementation status verified in code: `CasaEngine/Engine/Physics/ColliderFixture.cs` defines
  `ColliderFixture`; no `class PhysicsShape` matches in the repository (`rg -n "class PhysicsShape\b"`
  returns nothing) — `PhysicsShape` is removed; `CasaEngine/Engine/Geometry/Shape2d.cs` declares
  only `Type` and `BoundingBox`, no `Position`/`Rotation` member — D2.b/D2.c applied.
