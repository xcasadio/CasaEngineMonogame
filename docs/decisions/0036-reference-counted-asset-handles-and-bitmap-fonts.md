# ADR-0036: Reference-counted asset handles with deferred release, and bitmap fonts as assets

- **Status**: Accepted
- **Date**: 2026-09-21
- **Source**: this chantier: `ai-agent/tasks/asset-handles-tasks.md`, branch `chantier/asset-handles`. Decided
  with the author on 2026-09-21, after the Alundra port's E13.d in-game check found that the inventory texts
  lost their bitmap font after a map change.

## Context

**The finding.** The Alundra gameplay DLL registered its bitmap font `font3` on the UI text engine once per
process. After a map change, every text of its inventory screen fell back to the default TTF font. This was
reproduced in game: opened on map 389, the texts use `font3`; through a portal to map 390 and opened again,
they report `FontFamily = 'Arial'`.

**Why: the UI runtime is rebuilt with every world.** `GameManager.UpdateWorld` clears the old world, then
`ViewManager.Clear()` (`CasaEngine/Framework/Application/GameManager.cs:73-121`). Removing a view disposes its
UI runtime (`CasaEngineGame.OnViewRemovedDisposeUIRuntime`, `CasaEngineGame.cs:653-657`). Each new view then
gets a new `UIRoot` (`OnViewAddedCreateUIRuntime`, `CasaEngineGame.cs:643-648`), which builds a new
`MGDesktop` and a new `FontStashSharpTextEngine` (`CasaEngine/Framework/UI/UIRoot.cs:75-106`). A font added
with `FontStashSharpTextEngine.AddStaticFont` (`MGUI/MGUI.FontStashSharp/FontStashSharpTextEngine.cs:270`)
lives on one text engine only. Only the TTF `FontSystem` is game-level (`CasaEngineGame.cs:41`, created at
`:360`); every `UIRoot` shares it.

**The asset manager has no notion of who uses an asset.**
- `AssetContentManager` caches per category (`Dictionary<string, AssetDictionary>`,
  `CasaEngine/Framework/Assets/AssetContentManager.cs:16`). `Load<T>` only looks in the category it is given
  (`:72-120`), so one id loaded under two categories gives two instances.
- `Unload(category)` disposes every `IDisposable` asset in the category, whoever still uses it (`:195-211`).
  Nothing in the engine calls it.
- There is no reference count, no handle type, and no asynchronous loading.
- The engine has no loader for BMFont `.fnt` files (`AssetLoaderRegistry.cs:25-57`), although the Alundra
  export catalogues `UI\font3.fnt` as an asset of type `fnt`. The DLL parsed it itself.

**MGUI.**
- `FontStashSharpTextEngine` can add a static font but has no API to remove one.
- For an unknown family, `ResolveFont` silently returns a fallback (`FontStashSharpTextEngine.cs:535-556`), and
  `MGTextBlock.TrySetFont` returns `false` (`MGUI/MGUI.Core/UI/MGTextBlock.cs:109-122`). A XAML
  `FontFamily` naming an unregistered family is therefore ignored without any message.

**What established engines do.** The author asked for an architecture in line with Godot, Unreal and Unity.
Their official documentation was read, and each claim below was checked against its source by an
independent reviewer.
- **Godot:** a `Resource` is `RefCounted`; the engine keeps a global cache of loaded resources by path and
  frees a resource once its last reference is released
  (<https://docs.godotengine.org/en/stable/classes/class_resource.html>).
- **Unity Addressables:** loading increments a reference count and releasing decrements it; the release is
  explicit and not tied to a scene change
  (<https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/MemoryManagement.html>).
- **Unreal:** assets stay in memory while their streamable handle is active
  (<https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Engine/FStreamableHandle>). An actor
  ended by a level transition is freed at the next garbage collection
  (<https://dev.epicgames.com/documentation/en-us/unreal-engine/unreal-engine-actor-lifecycle>).
- **Fonts:** all three reference a font asset from their text elements (Godot `FontFile`, Unity
  `TMP_Text.font`, Unreal Font assets in UMG).

## Decision

- **One shared instance per asset id.** `AssetContentManager.Acquire<T>(Guid id)` returns an `AssetHandle<T>`
  (`IDisposable`). Every handle on an id shares the same instance, and the asset is loaded once.
- **Reference counting, with dependencies.** Disposing a handle decrements the count. A loader that needs
  other assets acquires handles on them, and its asset releases them when it is freed.
- **Deferred release.** An asset whose count reaches 0 is *pending*: it stays in memory, and acquiring it
  again returns the same instance without reloading it. Pending assets are freed only by
  `AssetContentManager.CollectUnreferenced()`:
  - the engine calls it at the very start of every world change, in `GameManager.UpdateWorld`, before the
    old world is cleared, so the old world's holders still hold what they use;
  - a game may also call it explicitly.
  A released asset therefore stays in memory at most until the next world change.
- **No session scope.** The holders are the objects that use an asset. Deferred release bridges the gap
  between an old world's holders and the new world's.
- **Compatibility.** `Load<T>(id)` with the default category and `cache: true` shares the same instances as
  `Acquire<T>`, and marks them *pinned*: a pinned asset is never collected, which is today's behaviour for
  every existing caller. The following are unchanged:
  - `cache: false`;
  - `LoadFromFile<T>`;
  - non-default categories;
  - `Unload(category)` and `UnloadAll()`, except that they never dispose an asset that still has live
    handles.
- **Bitmap fonts are assets.** A `BitmapFont` asset type and a loader for `.fnt` files. The loader acquires
  the font's page textures as dependencies, resolving each page file through the asset catalog relative to
  the `.fnt` file.
- **A game-level UI font registry, resolved by family name.** The registry lives on `CasaEngineGame`, next to
  the TTF `FontSystem`.
  - Acquiring a bitmap font through it registers the font's family (the `.fnt` `face`) in every live UI text
    engine, and in every text engine created while the font is held.
  - A `UIRoot` attaches its text engine to the registry when it is created and detaches it when it is
    disposed.
  - When a font is collected, the registry removes it from the live text engines through a new, additive MGUI
    API: `FontStashSharpTextEngine.RemoveStaticFont`.
  - XAML names the family, as in `FontFamily="font3"`.
- **Deferred to separate chantiers:**
  - per-map scopes (loading a map's assets under its own owner and releasing them when the map is left);
  - a UI root that survives world changes;
  - asynchronous loading.

## Consequences

- A resource used across a world change is never reloaded, provided its holder in the new world acquires it
  before the next world change. A game declares what it uses by holding handles, and frees it by disposing
  them.
- A handle that is never disposed keeps its asset for the whole run, as every asset does today.
- Assets loaded through the existing `Load<T>` stay pinned, so this record changes nothing for existing
  callers. Moving them to handles, where freeing matters, is follow-up work, together with per-map scopes.
- A text element can only use a bitmap family that is held when its window is built. MGUI ignores an unknown
  `FontFamily` silently; this gap is recorded in `ai-agent/audits/mgui-gaps-from-xaml-screens.md`, not
  worked around.
- The asset manager stays single-threaded, as today.
- MGUI gains one additive API. The author's MGUI checkout was on `develop` at `fbd6280` while this repository
  recorded `b8765bc`; on the author's instruction, the MGUI change is based on `fbd6280`, so the recorded
  reference moves to include it.
- The engine has no bitmap font in its demo content. On the author's decision, the visible proof is the
  Alundra port's in-game check rather than an engine demo.
