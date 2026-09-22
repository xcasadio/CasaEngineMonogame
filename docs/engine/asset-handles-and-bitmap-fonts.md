# Counted asset handles, deferred release, and bitmap fonts for the UI

Decisions: see [ADR-0036](../decisions/0036-reference-counted-asset-handles-and-bitmap-fonts.md) and
[ADR-0037](../decisions/0037-counted-handles-replace-load-and-categories.md).

## Summary

- **One shared instance per asset, held through a handle.** `AssetContentManager.Acquire<T>(id)` returns an
  `AssetHandle<T>`. Every handle on the same id shares one instance, which is loaded once. This is the only
  way to use a shared asset: there is no `Load<T>`, no category and no lookup by name any more.
- **Deferred release.** Disposing a handle gives the hold back. An asset nobody holds any more stays
  *pending* in memory. Acquiring it again returns the same instance without reloading it.
- **When pending assets are freed.** `AssetContentManager.CollectUnreferenced()` frees them. The engine calls
  it at the very start of every world change, before the old world is cleared, and logs how many it freed; a
  game may call it too.
- **Copies for templates.** `LoadCopy<T>(id)` reads a fresh object from the asset's file. Each use of a
  template gets its own copy: entities, worlds, cutscenes, authoring materials, hot-reload reads. The copy is
  neither cached nor counted, and belongs to the caller.
- **Objects made at run time.**
  - `Register<T>(id, asset)` stores an object made at run time (the default texture, a generated cubemap)
    and returns a hold on it.
  - `Replace<T>(id, asset)` swaps the shared instance of an id (hot reload, editor saves) and keeps its
    holders.
- **Bitmap fonts are assets.** A BMFont `.fnt` catalog entry loads as a `BitmapFont`: its family is the
  file's `face`, and its page textures are held as dependencies.
- **Fonts for the UI.** `CasaEngineGame.UIFonts` (a `UIFontRegistry`) gives the bitmap fonts the game holds,
  by reference, to every UI text engine. UI text elements name the family, as in `FontFamily="font3"` in
  XAML.
- **Uncatalogued files.** `LoadFromFile<T>(path)` reads a file that is not in the catalog, for tools and
  demos. The result is not cached.

## Why

The engine rebuilds the UI runtime of every view when a world changes: new `UIRoot`, new `MGDesktop`, new
text engine. A font registered on one text engine did not reach the next one, and a game that registered its
bitmap font once lost it after the first map change. Holding the font at the game level, and giving it to
each new text engine by reference, keeps it across worlds without reloading or re-parsing anything.

The same mechanism makes per-map freeing automatic. Every asset is held by the objects that use it and given
back when they go away. An asset that only one map used is therefore freed at the next world change, while
one the next map also uses survives, because the next map takes it again before that change.

## Usage

Hold an asset for as long as you use it, and give it back when its owner goes away:

```csharp
public sealed class MyComponent : EntityComponent
{
    private AssetHandle<SoundAsset> _sound;

    public override void InitializeWithWorld(World world)
    {
        base.InitializeWithWorld(world);
        _sound = world.Game.AssetContentManager.Acquire<SoundAsset>(SoundAssetId);
    }

    public override void Detach()
    {
        _sound?.Dispose();
        _sound = null;
        base.Detach();
    }
}
```

Read a template, then own the copy:

```csharp
Entity enemy = game.AssetContentManager.LoadCopy<Entity>(enemyTemplateId);
```

Store an object made at run time, and swap an instance on hot reload:

```csharp
_cubemap = assets.Register(cubemapId, generatedCubemap);   // the maker holds it
assets.Replace(spriteId, reloadedSpriteData);               // holders are kept
```

Hold a bitmap font for the UI, from the object that displays it:

```csharp
public sealed class InventoryScreen : XamlUIScreenBase, IDisposable
{
    private IDisposable _font;

    public InventoryScreen(UIFontRegistry fonts) : base(EmbeddedXaml())
    {
        _font = fonts.Acquire(fontAssetId);   // a .fnt catalog entry
    }

    public void Dispose()
    {
        _font?.Dispose();
        _font = null;
    }
}
```

```xml
<TextBlock Name="WeaponNameText" FontFamily="font3" />
```

The object that built the screen disposes it when its world ends (for a gameplay proxy, in `OnEndPlay`).
The font then stays pending, and the next world's screen takes the same instance again.

## Lifetime rules

- **Holding.** An asset is held while at least one handle on it is not disposed. `AssetHandle<T>.Dispose` is
  idempotent. Reading `Asset` after `Dispose` throws `ObjectDisposedException`.
- **Pending assets.** A pending asset is freed by the next `CollectUnreferenced()`: disposed once when it is
  `IDisposable` or `IAssetable`, then dropped from the cache. An asset it held as a dependency is freed in the
  same call if nobody else holds it, for example a sprite's sheet texture, a texture's image, a font's pages
  or a clip's skeleton.
- **When collection runs.** It runs when `GameManager.UpdateWorld` sees a pending world change, on both
  `SetWorldToLoad` paths, before `World.Clear()`. The old world's holders therefore still hold what they use.
  What they give back while clearing stays pending until the next world change. `RestoreWorld` does not
  collect. Each collection is logged at Info level: `World change: N unreferenced asset(s) freed.`
- **Where components give assets back.** `EntityComponent.Detach` is the release hook of components. When
  the world discards an entity, it detaches the entity's whole tree, each component once:
  - its root component, whose `Detach` cascades to the scene components under it;
  - its other components;
  - its child entities, recursively.
  This happens when the world is cleared and when an entity destroyed during play is removed.
- **Shared dependencies.**
  - A `Texture` loaded from its asset holds its `Texture2D`, and a `Sprite` holds its sheet `Texture`: dispose
    the sprites you create.
  - `StaticModel`, `SkinnedMesh`, `AnimationClip` and `RetargetProfile` hold what they reference, and give it
    back when they are freed.
- **UI fonts.** A `UIRoot` attaches its text engine to `UIFonts` when it is created and detaches it in
  `Dispose`.
  - A newly attached text engine receives the fonts currently held, not the pending ones. A pending font is
    given again to the attached text engines when someone acquires it again.
  - When the asset manager frees a font, the registry removes it from the attached text engines
    (`FontStashSharpTextEngine.RemoveStaticFont`, MGUI).

## Limits

- **Entities.** `Acquire<Entity>` throws: entities are instantiated per use, with `LoadCopy<Entity>`.
- **`Replace` does not retarget handles.** A handle keeps returning the instance it was given. Whoever
  replaces an instance refreshes its users, as the hot-reload code does.
- **Game-lifetime holders.** Audio clips (held by the audio service) and environment assets (held by the
  environment lookup and the cubemap generators) are held for the whole game. Freeing them per map is a
  follow-up.
- **Resolved fonts.** Removing a font from a text engine only changes later resolutions. A text element
  that already resolved the family keeps the font it got, so whoever displays a font must hold it.
- **Silent fallback.** MGUI ignores a XAML `FontFamily` it cannot resolve, without any message. A window
  built before its font is held falls back to the default font silently. This gap is recorded in
  `ai-agent/audits/mgui-gaps-from-xaml-screens.md` (G6).
- **Threading.** The asset manager is single-threaded.

## Next steps

- Freeing audio clips and environment assets per map.
- A UI root that survives world changes (ADR-0036, deferred).
- Asynchronous loading.
