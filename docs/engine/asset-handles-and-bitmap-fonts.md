# Counted asset handles, deferred release, and bitmap fonts for the UI

Decisions: see [ADR-0036](../decisions/0036-reference-counted-asset-handles-and-bitmap-fonts.md).

## Summary

- **One shared instance per asset.** `AssetContentManager.Acquire<T>(id)` returns an `AssetHandle<T>`. Every
  handle on the same id shares one instance, which is loaded once.
- **Deferred release.** Disposing a handle gives the hold back. An asset nobody holds any more stays
  *pending* in memory. Acquiring it again returns the same instance without reloading it.
- **When pending assets are freed.** `AssetContentManager.CollectUnreferenced()` frees them. The engine calls
  it at the very start of every world change, before the old world is cleared; a game may call it too.
- **Bitmap fonts are assets.** A BMFont `.fnt` catalog entry loads as a `BitmapFont`: its family is the
  file's `face`, and its page textures are held as dependencies.
- **Fonts for the UI.** `CasaEngineGame.UIFonts` (a `UIFontRegistry`) gives the bitmap fonts the game holds,
  by reference, to every UI text engine. UI text elements name the family, as in `FontFamily="font3"` in
  XAML.

## Why

The engine rebuilds the UI runtime of every view when a world changes: new `UIRoot`, new `MGDesktop`, new
text engine. A font registered on one text engine did not reach the next one, and a game that registered its
bitmap font once lost it after the first map change. Holding the font at the game level, and giving it to
each new text engine by reference, keeps it across worlds without reloading or re-parsing anything.

## Usage

Hold an asset for as long as you use it:

```csharp
using AssetHandle<Texture2D> page = game.AssetContentManager.Acquire<Texture2D>(pageId);
Texture2D texture = page.Asset;   // shared instance
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
- **Pending assets.** A pending asset is freed by the next `CollectUnreferenced()`: disposed when it is
  `IDisposable`, then dropped from the cache. An asset it held as a dependency (a font's page texture) is
  freed in the same call if nobody else holds it.
- **When collection runs.** It runs when `GameManager.UpdateWorld` sees a pending world change, on both
  `SetWorldToLoad` paths, before `World.Clear()`. The old world's holders therefore still hold what they use.
  What they give back while clearing stays pending until the next world change. `RestoreWorld` does not
  collect.
- **UI fonts.** A `UIRoot` attaches its text engine to `UIFonts` when it is created and detaches it in
  `Dispose`.
  - A newly attached text engine receives the fonts currently held, not the pending ones. A pending font is
    given again to the attached text engines when someone acquires it again.
  - When the asset manager frees a font, the registry removes it from the attached text engines
    (`FontStashSharpTextEngine.RemoveStaticFont`, MGUI).

## Compatibility and limits

- **Existing callers.** `Load<T>(id)` with the default category and `cache: true`, and `AddAsset`, **pin**
  what they put in the default category: a pinned asset is never collected, so every existing caller keeps
  its behaviour. `cache: false`, `LoadFromFile<T>` and non-default categories are unchanged. `Unload` and
  `UnloadAll` never dispose an asset that still has live handles.
- **Entities.** `Acquire<Entity>` throws: entities are instantiated per use.
- **Resolved fonts.** Removing a font from a text engine only changes later resolutions. A text element
  that already resolved the family keeps the font it got, so whoever displays a font must hold it.
- **Silent fallback.** MGUI ignores a XAML `FontFamily` it cannot resolve, without any message. A window
  built before its font is held falls back to the default font silently. This gap is recorded in
  `ai-agent/audits/mgui-gaps-from-xaml-screens.md` (G6).
- **Threading.** The asset manager is single-threaded, as before.

## Next steps

- Per-map scopes: load a map's assets under its own owner and release them when the map is left, then move
  the `Load<T>` callers where freeing matters to handles.
- A UI root that survives world changes (ADR-0036, deferred).
- Asynchronous loading.
