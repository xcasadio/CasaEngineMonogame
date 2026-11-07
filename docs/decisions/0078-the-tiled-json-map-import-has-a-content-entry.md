# ADR-0078: The Tiled JSON map import has a content entry beside the path entry

- **Status**: Accepted
- **Date**: 2026-11-07
- **Source**: this chantier: `ai-agent/tasks/project-generator-tasks.md` of the parent repository (decisions D1 and D5, task T2.1)

## Context

- `EditorAssetImportService.ImportTiledMap(sourceFilePath, destinationFilePath)` reads the `.tmj` with `File.ReadAllText`,
  each external `.tsj` with `File.ReadAllText` (`TiledMapImporter.ReadTilesetReferenceJson`), and copies each tileset image
  with `File.Copy` (`EditorAssetImportService.ImportTexture`). Every input is a file on disc.
- The Alundra project generator (parent repository) reads a disc image and must import the Tiled maps it builds without
  writing intermediate files (parent plan D1, D5). Its map is a `.tmj` text, its tilesets and images exist only in memory.
- The existing caller (the Alundra converter, `TileMapWriter`) works from files and must keep working unchanged.

## Decision

- New public API, additive, for Tiled JSON maps (`.tmj`) only; TMX is unchanged:
  - `TiledMapImporter.ImportJson(string mapJsonText, string mapFilePath, Func<TiledExternalTilesetRequest, TiledTilesetReference> resolveExternalTileset)`
    returns a `TiledMapImportDocument`. `mapFilePath` is the logical path of the map: it fills `SourceFilePath` and resolves
    the relative `source` and `image` paths; no file is read.
  - `TiledExternalTilesetRequest` carries what the resolver needs: `Source` (the raw `source` string), `FilePath` (the
    resolved logical path), `FirstGid`, `MapTileWidth`, `MapTileHeight`.
  - `TiledMapImporter.ReadTilesetJson(tilesetJsonText, tilesetFilePath, firstGid, mapTileWidth, mapTileHeight)` parses the
    text of an external tileset into a `TiledTilesetReference`.
  - `EditorAssetImportService.ImportTiledMapContent(mapJsonText, mapFilePath, resolveExternalTileset, readImage, destinationFilePath)`
    imports from content; `readImage` receives `TiledTilesetReference.ImageFilePath` and returns the bytes, or null for a
    missing image.
- The resolver returns the engine import model (`TiledTilesetReference`), never text: turning a tileset into text in memory
  only to parse it again would be an intermediate datum.
- One code path. The path entry for a `.tmj` now reads the map text, then calls the same `ImportJson` with a resolver that
  reads the `.tsj` from disc and calls `ReadTilesetJson`; `ImportTiledMap` and `ImportTiledMapContent` share one core
  (`ImportTiledMapDocument`). They differ only by where the map text, the external tilesets and the image bytes come from
  (the image is copied from disc by the path entry, skipped when its path is the destination path; written from the returned
  bytes by the content entry, always, overwriting an existing file, including when the logical image path is the destination path).
- Resolver contract: it returns a non-null `TiledTilesetReference` whose tile size and `FirstGid` match the request.
  After it returns, the importer checks the tile size against the map's as it does for a parsed tileset, with the same
  `NotSupportedException` message, so a hand-built reference cannot bypass it. A null return throws `InvalidOperationException`
  ("The resolver returned no tileset for '<source>'."); a `FirstGid` different from the request's throws `InvalidOperationException`
  naming the source and both values (it can never fire on the path entry, which passes the request's `FirstGid` to
  `ReadTilesetJson`). An exception thrown by the resolver propagates unchanged: a caller that wants the path entry's
  `FileNotFoundException` for a missing tileset throws it itself.

## Consequences

- Given the same map, tilesets and image bytes, both entries write the same `.tileMap`, `.tileset` and `.texture` files and the
  same image; this is covered by `CasaEngine.Tests/EditorServices/TiledMapContentImportTests.cs` (byte-identical outputs for
  several maps, the content side running from memory only, with a source folder that is never written).
- The path entry keeps its behaviour: same reads in the same order, same exceptions, same results.
- Nothing is removed or renamed; the new members are public API to keep stable.
- Limits: `.tmx` has no content entry; the content entry does not read anything itself, so its caller owns the lookup of
  tilesets and images and the validity of the logical paths it hands out.
