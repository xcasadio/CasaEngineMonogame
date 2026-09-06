# ADR-0011: Tilemap render spaces

- **Status**: Accepted
- **Date**: 2026-09-06 (backfill)
- **Source**: `docs/engine/rendering-2d-3d-spaces.md:8-11,31,47-50,53-54,80-84,133-140,180-182,225-226,240`, `ai-agent/audits/analysis-tilemap-render-spaces.md:142`

## Context

CasaEngine needed one consistent rule for how 2D tilemap data reaches the screen, instead of ad hoc per-mode logic. `docs/engine/rendering-2d-3d-spaces.md` documents the resulting design: the tilemap data, the camera's projection, the sprite renderer's interpretation, and the render pipeline's pass ordering are kept as four separate responsibilities, and the same `TileMapData`/`TileSetData`/`TileMapComponent`/renderer serve four display modes depending only on which camera (or intermediate render target) the view uses.

## Decision

- A tilemap is a world-space object; its display space is decided exclusively by the view's camera (orthographic = 2D pixel-perfect, perspective = 3D object) or by an intermediate render target — never by the tilemap itself (source: `docs/engine/rendering-2d-3d-spaces.md:8-11`).
- No space or projection logic may enter `TileMapComponent` or the TileMap assets (source: `docs/engine/rendering-2d-3d-spaces.md:10-11`).
- Screen-space 2D (`Renderer2DComponent`, MGUI) is reserved for UI/HUD; a tilemap must never be routed through it, since it would lose culling, static chunks, per-layer depth and cohabitation with world entities (source: `docs/engine/rendering-2d-3d-spaces.md:31`; confirmed in `ai-agent/audits/analysis-tilemap-render-spaces.md:142`).
- `Camera2dComponent.PixelSnap` rounds the camera position onto the texel grid (step `1 / Zoom`) only when computing the view matrix; the stored `Target` is never modified (source: `docs/engine/rendering-2d-3d-spaces.md:47-48`).
- The orthographic projection is `Matrix.CreateOrthographic(viewport.Width / Zoom, viewport.Height / Zoom, viewport.MinDepth, viewport.MaxDepth)`, recomputed on resize (source: `docs/engine/rendering-2d-3d-spaces.md:50`).
- With the default near/far (`1`/`1000`) and the fixed internal camera distance of 500, the usable depth window is `[Target.Z − 500, Target.Z + 499]` (source: `docs/engine/rendering-2d-3d-spaces.md:53-54`).
- `TileMapComponent.Draw` selects its draw path once per draw from `IsAxisAlignedWorldMatrix(WorldMatrixWithScale)`: an unchanged fast path for identity rotation (position/scale arithmetic, range-based tile culling), and a rotation path that transforms chunk local geometry by the full world matrix and culls chunk by chunk against a reused `BoundingFrustum` (source: `docs/engine/rendering-2d-3d-spaces.md:80-84`).
- Mode 4 (render-to-texture, mixing a pixel-perfect 2D map into a 3D scene) is implemented by `TileMapSurfaceComponent`, modeled on `WorldUIComponent`: an unregistered, non-serialized `IDisposable` registered on the world via `World.RegisterTileMapSurface`/`UnregisterTileMapSurface`, drawn by `World.DrawTileMapSurfacesToTextures()` from `RenderPipeline.Render` (source: `docs/engine/rendering-2d-3d-spaces.md:133-140`).
- A tilemap routed to a `TileMapSurfaceComponent` must be axis-aligned (identity rotation), because the surface's ortho frame is built only from the map's position and scale; an oriented display must rotate the quad that consumes the resulting texture, not the map (source: `docs/engine/rendering-2d-3d-spaces.md:180-182`).
- `PixelPerfectDiagnostics` evaluates the pixel-perfect contract and returns a `PixelPerfectDegradation` (`ResolutionScale`, `NonIntegerZoom`) (source: `docs/engine/rendering-2d-3d-spaces.md:225-226`).
- `Camera3dIn2dAxisComponent` was removed from the engine in favor of `Camera2dComponent`, which reproduces the same framing without its fragilities (FOV recalculated on every resize, distance depending on global screen size rather than the view, perspective distortion off the target plane, no zoom/snap) (source: `docs/engine/rendering-2d-3d-spaces.md:240`).

## Consequences

- Changing a tilemap's display space only requires assigning a different camera to the `RenderView`; no change to `TileMapComponent` or the TileMap assets is needed (source: `docs/engine/rendering-2d-3d-spaces.md:8-14`).
- Content outside the depth window `[Target.Z − 500, Target.Z + 499]` under `Camera2dComponent` is clipped; layer `zOffset` values expressed in pixels comfortably fit within it (source: `docs/engine/rendering-2d-3d-spaces.md:53-54`).
- The rotation draw path adds per-chunk `BoundingBox`-vs-frustum culling instead of the fast path's range-based tile culling, at no added cost when rotation is identity (source: `docs/engine/rendering-2d-3d-spaces.md:80-84`).
- A serialized world still carrying `"type": "Camera3dIn2dAxisComponent"` fails to load (`ElementFactory` resolves components by type name) and must be migrated by hand to `Camera2dComponent`, dropping `fieldOfView` and adding `target`/`zoom`/`pixel_snap` (source: `docs/engine/rendering-2d-3d-spaces.md:254-264`).
- Direct writes to `TileMapData.SetTile*` bypass `TileMapComponent`'s revision tracking, so a `TileMapSurfaceComponent` will not repaint; `Invalidate()` must be called explicitly in that case (source: `docs/engine/rendering-2d-3d-spaces.md:170-176`, `TileMapSurfaceComponent.cs`).
- Implementation status verified in code: `CasaEngine/Framework/Rendering/PixelPerfectDiagnostics.cs` and `CasaEngine/Framework/Rendering/TileMapSurfaceComponent.cs` both exist; `Camera2dComponent.cs` implements the documented `Zoom`/`PixelSnap`/orthographic-projection behavior (`CasaEngine/Framework/Scene/Entities/Components/Camera2dComponent.cs:20-100`); a repository-wide search found no `Camera3dIn2dAxisComponent` class, only a historical comment referencing its removal in `CasaEngine.Tests/Scene/Camera2dComponentTests.cs:132`.
