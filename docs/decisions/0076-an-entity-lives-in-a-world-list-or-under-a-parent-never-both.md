# ADR-0076: An entity lives in a world's entity list or under a parent entity, never both

- **Status**: Accepted
- **Date**: 2026-10-17
- **Source**: this chantier: discussion with the author on 2026-10-17 (choice "RPGDemo + engine refusal" among: fix RPGDemo only,
  RPGDemo plus a null-owner guard in `AnimatedSpriteComponent`, RPGDemo plus an engine refusal, or `AddChild` taking the entity out of
  the world's list), branch `chantier/rpgdemo-sword-reload-crash`. Tests: `CasaEngine.Tests/Application/WorldChildEntityInWorldListTests.cs`.

## Context

- `Entity.Update` and `Entity.Draw` run their children. `World.Update` updates every entity of its list, and `World.Draw` draws the
  visible entities of its spatial index, which holds the entities of that list.
- The world observes the tree of its entities: a child added to one of them is initialized with the world (`World.OnEntityChildAdded`).
  The editor puts a child only under its parent and a root entity only in the list (`EditorWorldEditingService.AttachEntity`).
- `World.SpawnEntity` queues the entity for the world's list (`World.AddEntity`). Nothing prevented the same entity from also being
  made a child of another entity.
- RPGDemo did both: `ScriptPlayer.OnBeginPlay` spawned `weapon_sword` into the world, then `MeleeWeapon.Initialize` made it a child of
  `character_link`. The sword was updated twice per world update (measured by a test) and drawn twice. When the world was cleared for a
  reload after the player died, `World.ClearEntities` detached the player with its children (the recursive teardown of ADR-0037), then
  met the sword further down its list and destroyed it again: `Entity.Destroy` on a detached root threw a `NullReferenceException` in
  `AnimatedSpriteComponent.OnEnabledValueChange`.

## Decision

- An entity lives either in a world's entity list (queued for it or listed in it) or under a parent entity, never in both places.
- The engine refuses a call that would put an entity in both places, with an `InvalidOperationException` that names the entities and
  the world, and leaves the state unchanged:
  - `Entity.AddChild` of an entity that a world queues or lists;
  - `World.AddEntity` (and so `World.SpawnEntity`) and the editor's immediate add (`World.AddEntityReferenceImmediate`) of an entity
    that is in the children of its `Parent`.
- The world records which world queues or lists an entity (internal `Entity.ListingWorld`) when it queues or lists it, and forgets it
  when the entity leaves: removal of a destroyed entity in `World.Update`, `World.ClearEntities` (list and queue), the editor's
  `World.RemoveEntityImmediate`, and an entity whose initialization failed when it left the queue.
- RPGDemo creates its sword without adding it to the world (`AssetContentManager.LoadCopy` then `Clone`, as `SpawnEntity` does before
  queuing), and `MeleeWeapon` makes it a child of the player; the world initializes it.

## Consequences

- A child entity is updated and drawn once per frame, through its parent, and a world change no longer destroys it twice. RPGDemo plays
  through repeated deaths and world reloads without an exception.
- Behavior change: code that spawned an entity into a world and then made it a child now throws at `AddChild`, with a message saying to
  create the child without adding it to a world. In this repository only RPGDemo did it; the Alundra port has no `Entity.AddChild` call
  (checked on 2026-10-17). Every editor path already respected the rule.
- The refusal holds in both orders and whatever the state of the parent, since an entity is recorded as soon as it is queued.
- Out of scope, unchanged: adding the same entity twice to a world's list, or making an entity the child of two parents. The copy
  constructor of `Entity` copies `Parent` without putting the clone in the parent's children; the `AddEntity` check looks at the
  parent's children, so such a clone can still become a root entity.
- No serialized format changes.
