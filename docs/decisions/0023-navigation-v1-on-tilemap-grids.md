# ADR-0023: Navigation V1 on tilemap grids

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/engine/navigation-engine-features.md:120-216`

## Context

The document records V1 decisions for precondition items: navigation data storage for tilemaps, the audit of `AStarSearch`/`PathPlanner`, the integration with the character controller, required unit tests, and debug draw.

## Decision

- V1 does not create a new navigation asset format immediately. The navigation grid is built from `TileMapData`: `TileMapLayerData.CustomProperties` with `navigation.role=grid` marks the navigation layer; `TileData.CustomProperties` carries per-tile navigation properties (`navigation.walkable`, `navigation.cost`, `navigation.layers`); `TileData.CollisionType` is only a fallback when no explicit navigation property exists. V1 does not infer navigation from tile name or appearance; a missing `navigation.role=grid` layer must fail grid generation cleanly or require an explicit caller fallback (source: `docs/engine/navigation-engine-features.md:124-148`).
- V1 does not use `PathPlanner<T>` for `NavigationGrid2D`; a dedicated grid pathfinder (`GridPathfinder2D`) is created instead, with unit tests from introduction. `AStarSearch<T, TK>` may remain as a generic legacy building block but must not be the core of the V1 TileMap pathfinding without dedicated tests (source: `docs/engine/navigation-engine-features.md:150-164`).
- Navigation does not drive `CharacterControllerComponent` through `Move(...)` directly. It produces a path and sends it to `CharacterControllerNavigationDriverComponent`, following the chain `NavigationGrid2D -> GridPathfinder2D -> NavigationPath -> CharacterControllerNavigationDriverComponent.SetPath(...) -> CharacterControllerComponent.SetMoveIntent(Vector2) -> CharacterControllerComponent.Update(...)`. `CharacterControllerSteeringBridgeComponent` remains the integration point for `SteeringAgentComponent`-driven movement (source: `docs/engine/navigation-engine-features.md:166-180`).
- V1 requires unit tests at minimum for: navigation-layer walkability building, tile navigation properties overriding the collision fallback, cost-aware pathfinding, diagonal corner-cutting blocking, unreachable-goal handling, and path-to-move-intent conversion (source: `docs/engine/navigation-engine-features.md:182-192`).
- Navigation debug draw must be a thin adapter over existing renderers (`Renderer2DComponent` for 2D, `Line3dRendererComponent` for 3D), not a new general-purpose renderer; for dense tilemaps the debug draw must cull by viewport, limit text, and avoid redrawing the whole grid every frame (source: `docs/engine/navigation-engine-features.md:194-216`).

## Consequences

- `PathPlanner<T>` is treated as legacy code to be repaired in a separate task; it is not used by V1 TileMap navigation. The source documents concrete bugs in `PathPlanner<T>` (`ClosestNodeToPosition` returning a local index, `NodesToPositions` indexing, `GetNowPathOfPositionsToPosition`/`GetNowPathOfEdgesToPosition` search direction, `PathManager<T>.UpdateWithTime` using `DateTime.Today.Ticks`) that remain unresolved by this decision.
- Debug draw is constrained by known limits of `Renderer2DComponent` (low initial capacity, unapplied scissor, unused rotation) and `Line3dRendererComponent` (internal 5000-line limit not matched by the requested-vs-clamped line count, state changes without local restoration); dense NavMesh debug rendering needs further work before it is safe.
- Implementation status observed in code: `GridPathfinder2D` (`CasaEngine/Framework/AI/Navigation/GridPathfinder2D.cs`), `NavigationGrid2D` (`CasaEngine/Framework/AI/Navigation/NavigationGrid2D.cs`), and `CharacterControllerNavigationDriverComponent` (`CasaEngine/Framework/AI/Navigation/CharacterControllerNavigationDriverComponent.cs`) all exist, matching the decision.
