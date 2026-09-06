# ADR-0003: Single 3D simulation, simulation space as a world policy

- **Status**: Accepted
- **Date**: 2026-08
- **Source**: `docs/engine/collision-2d-3d-architecture.md:19-28`, `:183-192`, `:276-310`, `:658-668`

## Context

The document poses the founding question "does a 2D game need 3D shapes?" and reframes it: the
dimensionality of a game is a configuration of the world, not an architecture
(`docs/engine/collision-2d-3d-architecture.md:4-6`). At the time of writing, `IsPhysics2dActivated`
exists as a flag rather than a policy (`:183-192`).

## Decision

- Central rule: a single physical simulation, in 3D, for every game. The "2D" of a game is a
  world policy — simulation plane, lowering of authoring shapes, simulation → render mapping —
  never a second physics stack, and never a hidden assumption in components or assets
  (source: `docs/engine/collision-2d-3d-architecture.md:21-24`).
- D1: the engine never creates a second "2D" stack. A 2D game uses the 3D simulation with a plane
  lock; a 2.5D game adds a space mapping; a 3D game is the identity case. `LinearFactor = (1,1,0)`
  + `AngularFactor = (0,0,1)` lock a game to the XY plane; the policy only supplies these defaults.
  `IsPhysics2dActivated` is absorbed or removed (source: `docs/engine/collision-2d-3d-architecture.md:183-192`).
- D4: the simulation space is a world policy (`ISimulationSpacePolicy`); components and assets know
  nothing about it. Three canonical instances: `Identity3d` (simulation = render, identity, the
  absolute default), `Planar2d(plane, extrusion)` (locked plane, extruded shapes, identity render),
  `TopDownElevation` (`X = east, Y = ground depth, Z = elevation`; render `X = X, Y = -(Y - Z)`)
  (source: `docs/engine/collision-2d-3d-architecture.md:276-300`).
- D4.a: under a non-identity policy, the physics body reads the logical pose, never
  `WorldMatrixNoScale`. The entity owns a canonical logical pose; the render pose is derived from
  it (source: `docs/engine/collision-2d-3d-architecture.md:304-308`).
- Forbidden (10, 11, 16): do not create a second "2D" physics stack (duplicated API, orphaned
  2.5D — the Unity pitfall); do not put space or projection logic in components or assets (mirror
  of the rendering rule); do not couple the physics pose to the render pose under a non-identity
  policy (source: `docs/engine/collision-2d-3d-architecture.md:661-668`).

## Consequences

- The cost of 3D for a 2D game stays marginal: same broadphase, trivial box-box narrowphase
  (source: `docs/engine/collision-2d-3d-architecture.md:186`).
- Bullet's `Convex2D` pipeline may return one day only as an internal optimization of the planar
  policy, never as public API (source: `docs/engine/collision-2d-3d-architecture.md:189-191`).
- V1 is pragmatic: a gameplay-side projection system writes sprite render transforms from the
  logical pose; full pipeline integration is long-term (source: `docs/engine/collision-2d-3d-architecture.md:306-309`).
- Implementation status verified in code: `CasaEngine/Engine/Physics/SimulationSpacePolicy.cs`
  defines the policy and its instances; `CasaEngine/Framework/Scene/Entities/Components/RenderProjectionComponent.cs`
  implements the D4.a projection (its own doc comment states it places itself at
  `SimulationSpacePolicy.DeriveRenderPosition` while collision bodies stay in logical space);
  `rg -i bulletsharp` over `*.cs`/`*.csproj` returns no result — BulletSharp is removed.
