# ADR-0005: Collision channels, responses and named profiles

- **Status**: Accepted
- **Date**: 2026-08
- **Source**: `docs/engine/collision-2d-3d-architecture.md:240-274`, `:176-177`, `:658-671`

## Context

The document states that collision semantics must be named project data (channels, profiles), not
backend enums or scattered booleans (`docs/engine/collision-2d-3d-architecture.md:176-177`).

## Decision

- D3: a reduced Unreal-style model — `CollisionResponse { Ignore, Overlap, Block }` and
  `CollisionProfile { Name, Channel, Responses[], DebugColor? }`. The channel table is defined by
  the project; the engine reserves a few (`WorldStatic`, `WorldDynamic`, `Pawn`, `Trigger`) with
  default profiles reproducing current behavior. Assets and components reference a profile by
  name — stable serialization, central editing
  (source: `docs/engine/collision-2d-3d-architecture.md:240-260`).
- Backend mapping: channel → group bit; broadphase mask = channels whose response ≠ `Ignore`. At
  the time of writing, Bullet could not do per-pair `Block` and `Overlap` on the same body — a
  body whose profile blocks nothing is a sensor (`NoContactResponse`); the mixed case is resolved
  by splitting fixtures into two bodies (cf. D2). Bepu handles this natively via the narrow-phase
  callback: the sensor is no longer a body flag but a decision made in
  `ConfigureContactManifold`, which returns `false` (no constraint) when either collidable is a
  sensor — the body still records its contacts and receives its `OnHit`/`OnHitEnded` events
  (source: `docs/engine/collision-2d-3d-architecture.md:262-270`).
- D3.a: `CollisionHitType` is removed — `Attack`/`Defense` become project channels
  (`AttackVolume`/`DamageableVolume`) plus a fixture `Tag`. Debug colors come from the profile.
  `Collision2d` carries `ProfileName` + `Tag` instead of the enum
  (source: `docs/engine/collision-2d-3d-architecture.md:272-274`).
- Forbidden (15, 18, 19): the semantics of collision must be named project data, not backend enums
  or scattered booleans (`:176-177`); do not encode gameplay semantics as booleans or colors —
  channels, profiles, tags (`:667`); do not assign hits without a stable fixture identity (`:671`)
  (source: `docs/engine/collision-2d-3d-architecture.md:176-177`, `:667`, `:671`).

## Consequences

- Implementation status verified in code: `CasaEngine/Engine/Physics/CollisionProfiles.cs` defines
  `enum CollisionResponse`, `static class CollisionChannels`, `class CollisionProfile`, and
  `class CollisionProfileTable`; `rg -n "CollisionHitType"` over `*.cs` returns no result —
  `CollisionHitType` is removed, matching D3.a.
- `CasaEngine/Framework/Physics/Bepu/BepuNarrowPhaseCallbacks.cs` defines `ConfigureContactManifold`
  (two overloads, lines 47 and 91), matching the sensor-as-callback-decision behavior described
  above.
