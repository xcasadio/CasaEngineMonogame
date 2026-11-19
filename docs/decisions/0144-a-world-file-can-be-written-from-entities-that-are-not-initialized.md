# ADR-0144: A world file can be written from entities that are not initialized

- **Status**: Proposed
- **Date**: 2026-11-18
- **Source**: this chantier: `ai-agent/tasks/offline-world-writer-tasks.md`, decision D1 (the author's answer of 2026-11-18: a small public engine API rather than reflection on a private engine field from a game tool) and propositions P1 to P4; requested by the RacingGame plan `ai-agent/tasks/rgce-worlds-as-assets-tasks.md` (decision D11)
- Renumbered from ADR-0128 on 2026-11-19 when origin/main was merged (its ADR-0128 is another decision)

## Context

Facts verified on 2026-11-18, at `da32906e2`:

- `EditorWorldWriter.SaveWorld(World)` writes only the entity references a world holds in its private list `_entityReferences`, which `EditorEntityJsonSerializer` reads by reflection.
- That list is filled by `World.Load` and by `World.AddEntityReferenceImmediate` (internal), which initializes the entity at once; the public routes `EditorWorldEditingService.AddEntity` and `AddEntityReference` go through it.
- A component such as `StaticModelComponent` cannot initialize without a running game (it reads `world.Game.AssetContentManager`). A tool that builds a world in code, without a game, therefore had no public way to write it.

## Decision

- **`EditorWorldWriter.SaveWorld(World world, IReadOnlyList<Entity> entities)`** (`CasaEngine.EditorServices`) writes `world.FileName` with the world-level keys `SaveWorld(World)` writes, in the same order, and one inline entity reference (empty `asset_id`) per entity of the list, in list order.
- It initializes nothing and adds nothing to the world: the world and the entities are left untouched.
- It throws `ArgumentNullException` for a null world, list or entity, and `InvalidOperationException` when the world already holds entity references: the two sources are not mixed.
- Both overloads go through one code path (`EditorEntityJsonSerializer.SaveWorld(World, IReadOnlyList<EntityReference>, JObject)`): the format is written in one place.

## Consequences

- The file format, the loading and the editor are unchanged. Measured by the independent verification: the previous and the new serializer write identical text for the 14 `.world` files of the repository's projects and for worlds built in code (script state, startup cutscene, virtual resolution, inline and asset references).
- A written world loads with `World.Load`, and `SaveWorld(World)` saves it back with the same text.
- Limits: only inline entities are written, not references to `.entity` assets. The caller sets the ids and names; a component whose type has no id constructor gets a random id. The folder of `world.FileName` must exist, as for `SaveWorld(World)`. An entity whose saved state changes when it is initialized (for example a gameplay proxy created by `Initialize`) is written in its state before initialization.
- Tests: `CasaEngine.Tests/EditorServices/EditorWorldWriterTests.cs` (6 tests, in the non-parallel collection `ProjectEnvironmentCollection`); 6475 tests, 0 failed.
- Documentation: `docs/editor/asset-documents.md`, section "Writing a world from code".
