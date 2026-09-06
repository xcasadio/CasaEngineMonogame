# ADR-0025: Kinematic character controller V1

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/engine/character-controller-features.md:84-115`

## Context

The document proposes an architecture decision for the V1 character controller, contrasting a kinematic gameplay controller against a rigid-body-driven dynamic controller.

## Decision

- V1 must be a kinematic gameplay controller: the character is not driven by physical forces; the controller computes a desired movement, queries physics, resolves collisions, then applies a final position to the entity's transform. This fits a modern playable character's needs: precision, stability, slope control, reproducible jumps, and clear synchronization with animation. The rigid-body-driven dynamic controller is not V1; it may remain useful later for ragdoll, physical objects, strong knockback, or vehicles, but does not give the level of control expected for main locomotion (source: `docs/engine/character-controller-features.md:88-94`).
- `CharacterControllerComponent` is created as a gameplay component, with `EntityComponent` as its recommended V1 base — not `PhysicsBaseComponent` — because `EntityComponent`'s stated purpose fits abstract behaviors like movement and it has no transform of its own; the controller must drive the entity's `RootComponent` or an explicitly configured `SceneComponent`, not become a physics collider itself (source: `docs/engine/character-controller-features.md:98-102`).
- Startup conditions to validate: `Owner` non-null; `Owner.RootComponent` non-null; a compatible collision component exists on the entity (e.g. `CapsuleCollisionComponent` for V1 3D); `Owner.World.PhysicsWorldContext` available; the physics queries needed by the controller are available (source: `docs/engine/character-controller-features.md:104-109`).

## Consequences

- The rigid-body-driven dynamic controller is deferred; it remains a candidate later for ragdoll, physical objects, knockback, or vehicles, not for main locomotion.
- The V1 controller must at minimum expose debug data; dedicated rendering of the capsule, sweeps, normals, and ground can be a separate task (source: `docs/engine/character-controller-features.md:84`).
- Implementation status observed in code: `CharacterControllerComponent` is declared as `public class CharacterControllerComponent : EntityComponent, IEntityPolicyDefaultsProvider, IWorldSystemDrivenComponent` in `CasaEngine/Framework/Scene/Entities/Components/CharacterControllerComponent.cs`, matching the `EntityComponent` base decision.
