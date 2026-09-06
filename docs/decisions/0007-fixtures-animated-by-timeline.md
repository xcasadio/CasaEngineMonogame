# ADR-0007: Fixtures animated by the animation timeline

- **Status**: Accepted
- **Date**: 2026-08
- **Source**: `docs/engine/collision-2d-3d-architecture.md:368-383`

## Context

Generalization of fighting-game frame data, valid for any melee genre
(source: `docs/engine/collision-2d-3d-architecture.md:370`).

## Decision

- D6: an animation asset can carry a timeline of fixture sets (`Step` keyframes, emitted only on
  change — the "constant hitbox" case costs a single keyframe). A runtime component (possible
  name: `AnimatedColliderComponent`) swaps the active set on keyframe change: bodies pre-built and
  pooled per distinct set (grouped by profile, cf. D2), zero allocation in steady state. Events
  carry the fixture's `Tag` on contact, so gameplay attributes "the sword hit the head", not
  "entity A hit entity B". This path replaces `AnimatedSpriteComponent`'s sprite-id collision,
  which is removed — its granularity was wrong (the sprite is shared across animations) and its
  selection fragile (`GetPrimarySpriteId`)
  (source: `docs/engine/collision-2d-3d-architecture.md:372-383`).

## Consequences

- Implementation status verified in code: `CasaEngine/Framework/Assets/Animations/Animation2dData.cs`
  exposes `CollisionKeyframes`, exercised by
  `CasaEngine.Tests/Animation/Animation2dCollisionTimelineTests.cs` (round-trip through the editor
  serializer, sorted-by-time-on-load, `ColliderFixture`-based keyframes with `Tag`).
- `rg -n "GetPrimarySpriteId"` over `*.cs` returns no result — the sprite-id selection is removed.
  No separate `AnimatedColliderComponent` class exists; the timeline is consumed instead by
  `AnimatedSpriteComponent` itself (`CasaEngine/Framework/Scene/Entities/Components/AnimatedSpriteComponent.cs`,
  which implements `ICollideableComponent`) rather than by a dedicated component as the source's
  "possible name" suggested.
