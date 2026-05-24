# Physics world architecture refactor tasks

Date: 2026-05-24

## Goal

Clarify physics ownership so the MonoGame component schedules physics work, each `World` owns its physics API, and BulletSharp types stop leaking beyond the Bullet backend layer.

Target architecture:

- `PhysicsSystemComponent`: global `GameComponent` scheduler and registry for active physics worlds.
- `PhysicsWorld`: per-`World` physics facade/API, owns one backend instance.
- `BulletPhysicsEngine`: BulletSharp-backed implementation detail. BulletSharp types must not appear in gameplay, scene components, world services, or public engine-facing physics contracts.

## Constraints

- Keep runtime behavior compatible while reducing API leaks.
- Avoid allocations in physics update/step paths.
- Keep `World.PhysicsWorldContext` as a compatibility property during the first migration pass if needed, but its concrete value should be the per-world physics facade.
- Do not include unrelated generated binaries in commits.

## Tasks

### T01 - Rename ownership layers

Status: Done

- Rename `PhysicsEngineComponent` to `PhysicsSystemComponent`.
- Rename `PhysicsWorldContext` to `PhysicsWorld`.
- Rename `PhysicsEngine` to `BulletPhysicsEngine`.
- Keep namespace placement initially stable to reduce churn.
- Update direct references and tests.

Validation:

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug --no-restore`

### T02 - Remove world API from system component

Status: Done

- Stop implementing `IPhysicsWorldContext` on `PhysicsSystemComponent`.
- Remove implicit current-world delegation and bootstrap physics context.
- Keep only context registry, release, and scheduled updates.
- Route call sites through `Owner.World.PhysicsWorldContext` or explicit `World.PhysicsWorldContext`.

Validation:

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug --no-restore`

### T03 - Introduce engine-owned physics contracts

Status: Done

- Add backend-neutral physics types under `CasaEngine.Engine.Physics`:
  - `PhysicsCollisionFilterGroups`
  - `PhysicsShape` descriptors/factories
  - `PhysicsBody` handle
  - optional debug/body query helpers needed by current callers
- Keep BulletSharp conversion inside `BulletPhysicsEngine` / `PhysicsWorld`.

Validation:

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug --no-restore`

### T04 - Hide BulletSharp from physics world interface

Status: Done

- Remove `BulletPhysicsEngine` from `IPhysicsWorldContext`.
- Replace `CollisionObject`, `RigidBody`, `CollisionShape`, `ConvexShape`, and `CollisionFilterGroups` in `IPhysicsWorldContext` with engine-owned types.
- Add higher-level methods for AABB refresh and debug draw instead of exposing `BulletPhysicsEngine.World`.

Validation:

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug --no-restore`
- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter "FullyQualifiedName~Physics" --no-restore`

### T05 - Remove BulletSharp from scene/gameplay code

Status: Done

- Update collision components to create `PhysicsShape` descriptors instead of BulletSharp shapes.
- Store `PhysicsBody` handles in scene, sprite, and tilemap code.
- Move all BulletSharp object lifetime/disposal details into `PhysicsWorld` / `BulletPhysicsEngine`.
- Update character controller settings/tests to use engine filter groups.

Validation:

- `dotnet test CasaEngine.Tests/CasaEngine.Tests.csproj -c Debug --filter "FullyQualifiedName~Physics" --no-restore`
- `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`

Notes:

- Physics test command currently stops during test-project compilation on unrelated rendering/editor test errors (`DualQuaternion`, `PreviewEnvironmentFactory`) before executing the physics filter.

### T06 - Documentation and final validation

Status: Done

- Update this task file statuses.
- Record final architecture notes if any compatibility compromises remain.
- Run full local build as required by repo instructions.

Validation:

- `dotnet build CasaEngine.MonoGame.sln -c Debug --no-restore`

Notes:

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug --no-restore` succeeds.
- Full solution build currently fails outside this refactor in `SandBoxGame` and `CasaEngine.Demos` because `CasaEngine.Framework.Application.Components.DebugTools` is missing.

### T07 - Restore physics debug draw mode parity

Status: Done

- Match engine-owned `PhysicsDebugDrawModes` values to the BulletSharp debug modes consumed by the backend adapter.
- Restore the old default debug mode semantics: oriented wireframe collider rendering with Bullet activation-state colors, not broadphase AABB rendering.
- Keep BulletSharp enum usage private to `BulletPhysicsEngine`; public/debug renderer code must stay on `PhysicsDebugDrawModes`.

Validation:

- `dotnet build CasaEngine/CasaEngine.csproj -c Debug --no-restore`

### T08 - Validate physics debug draw isolation

Status: Done

- Verify no `using BulletSharp` / `BulletSharp.` leaks were added outside the Bullet backend source.
- Record validation results and any known blockers in this task file.

Validation:

- `rg "using BulletSharp|BulletSharp\." CasaEngine CasaEngine.Tests/Physics --glob "*.cs" --glob "!CasaEngine/Framework/Physics/BulletPhysicsEngine.cs"`
- `dotnet build CasaEngine/CasaEngine.csproj -c Debug --no-restore`

Notes:

- No BulletSharp source leaks were found outside `BulletPhysicsEngine.cs`.
- `dotnet build CasaEngine/CasaEngine.csproj -c Debug --no-restore` succeeds with existing warnings.

## Notes for follow-up agents

- Do not reintroduce BulletSharp in `CasaEngine.Framework.Scene`, `CasaEngine.Framework.Assets`, or public physics contracts.
- If a feature needs native Bullet access, add a method on `PhysicsWorld` or `BulletPhysicsEngine` and expose an engine-owned type to callers.
- Longer term, split `IPhysicsWorldContext` into smaller contracts such as body creation, queries, debug draw, and simulation update.