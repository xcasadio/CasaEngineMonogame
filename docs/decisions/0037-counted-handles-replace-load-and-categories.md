# ADR-0037: Counted handles replace Load and asset categories

- **Status**: Accepted
- **Date**: 2026-09-21
- **Source**: this chantier: `ai-agent/tasks/asset-handles-migration-tasks.md`, branch
  `chantier/asset-handles-migration`. Decided with the author on 2026-09-21, right after
  [ADR-0036](0036-reference-counted-asset-handles-and-bitmap-fonts.md) was delivered.

## Context

ADR-0036 added counted handles (`AssetContentManager.Acquire<T>`) and deferred release
(`CollectUnreferenced` at the start of every world change). To stay compatible, it kept the older API:
`Load<T>` and `AddAsset` pin what they put in the default category, so those assets are never freed.

After that chantier, every caller but one still used the older API. A read-only inventory on 2026-09-21
counted about 110 calls outside the tests:
- about 40 in the engine runtime (`CasaEngine/`);
- 12 in the editor and its services;
- 7 in the engine demos;
- 7 in the Alundra gameplay DLL.

The tests hold 33 more calls. `Acquire` was used only for the Alundra inventory's bitmap font.

The inventory found three kinds of use.

**Shared assets, used for as long as their user lives.**
- **Users:** tile sets and sprite sheets, sprite data, sounds, models, animation data, effects, textures.
- **Where users take and drop them:** components take them in `InitializeWithWorld` and drop them in
  `Detach`. When the world is cleared, `World.ClearEntities` detaches only the components in an entity's
  component list (`Entity.AttachedComponents`).
- **What is never detached:**
  - an entity's root component and the scene components under it (in the Alundra port, the tile map is
    exactly such a root component);
  - child entities ;
  - any component of an entity removed during play through `Entity.Destroy` (`World.Update`, the
    `ToBeRemoved` path).
  Whatever those components hold is therefore never released.
- **`IAssetable` is not `IDisposable`:** it declares its own `Dispose()` without deriving from
  `IDisposable`, while ADR-0036's collector only disposes `IDisposable` assets. A collected `Texture` or
  `RiggedModel` would therefore never be disposed.

**Templates.**
- **Fresh copy per use:** each use needs its own copy — entities (`Load<Entity>` already reads the file on
  every call), worlds (`cache: false`), cutscenes, authoring materials, hot-reload reads.
- **Derived instance:** some are read once to derive an instance, such as a tile map's working copy or a
  particle effect's runtime.

**Objects made at run time and stored with `AddAsset`.**
- **Made by the game:** the default texture, which is also looked up by name; the generated environment
  cubemaps; hot-reloaded versions of assets.
- **Made by the editor:** sprites saved by the editor.

**Categories and name lookups.**
- **Categories:** no caller uses any category other than the default one. `Unload` and `UnloadAll` have no
  caller.
- **Lookups by name:** `GetAsset<T>(name)` is ambiguous, since several assets share a name — a font's
  `.png`, `.texture` and `.fnt` are all named `font3`.

## Decision

- **Every shared asset is held through a handle.** Its owner acquires it and disposes the handle when it
  goes away:
  - a component in `InitializeWithWorld` / `Detach`;
  - a world object in `World.Clear`;
  - a game service for its own lifetime;
  - an editor panel in its `Dispose`.
  An asset that nobody holds is freed by the next `CollectUnreferenced`. Nothing is pinned any more.
- **`AssetContentManager.LoadCopy<T>(id)`** returns a fresh object read from the asset's file. It is not
  cached, not counted, and owned by the caller alone. It replaces `Load<T>(id, cache: false)` and the
  entity/world template reads.
- **`AssetContentManager.Register<T>(id, asset)`** stores an object made at run time under an id and
  returns a handle. Its maker holds it, and it is freed like any other asset.
- **`AssetContentManager.Replace<T>(id, asset)`** swaps the shared instance of an id, keeping its holders.
  It serves hot reload and editor saves. A handle keeps returning the instance it was given: the code that
  hot-reloads pushes the new version to its users itself, as it already does.
- **Dependencies are held, not borrowed.** An engine `Texture` holds its `Texture2D` through a handle and
  gives it back on `Dispose`, instead of disposing a shared GPU texture. A `Sprite` holds its `Texture`
  the same way.
- **A discarded entity is detached as a whole**, whether the world is cleared or the entity is removed during
  play: its root component (whose `Detach` cascades to the scene components under it), its other
  components, and its child entities, each once. A destroyed entity thus gives back what it holds.
- **The collector also disposes `IAssetable` assets.** Shared assets that hold handles on their
  dependencies (`StaticModel`, `SkinnedMesh`) become `IDisposable` and give them back when they are freed.
- **Removed, without an `[Obsolete]` step**, on the author's instruction:
  - `Load<T>(Guid, category, cache)` and `Load<T>(JObject)`;
  - `LoadDirectly<T>`;
  - both `AddAsset` overloads;
  - both `GetAsset<T>` overloads and `GetAssets<T>`;
  - `Unload` and `UnloadAll`;
  - `DefaultCategory` and every category parameter.
- **Kept:** `LoadFromFile<T>` (uncatalogued files, for tools and demos), `Acquire<T>`,
  `CollectUnreferenced` and the handles.
- **Delivery:** one chantier, sliced by area, each slice buildable. The removal comes last, once nothing
  calls the removed members.

## Consequences

- **Breaking change.** This is a breaking change of the engine's public API for every consumer: games,
  editor, demos, tests. They are migrated in the same chantier, and a game outside this repository has to
  follow the same migration.
- **Per-map freeing becomes real.** An asset that only one map's objects held is freed at the next world
  change. One shared by the next map survives, because the next map acquires it before that change.
- **ADR-0036's compatibility clause is superseded.** `Load<T>` pinning no longer exists; the rest of
  ADR-0036 stands.
- **Discarded entities are detached as a whole.** An entity destroyed during play, and the root and child
  parts of every entity when a world is cleared, now have their components detached. Their GPU, sound and
  physics resources are therefore released. This is a behaviour change that fixes leaks, among them a tile
  map placed as a root component.
- **Looking up by name** becomes a catalog lookup followed by `Acquire`. The default texture becomes a
  game property instead of a name in the cache.
- **Asset categories disappear.** Per-owner release replaces them: whoever holds a set of assets gives
  them back together.
