using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Fonts;
using MGUI.FontStashSharp;
using MGUI.Shared.Text;

namespace CasaEngine.Framework.UI;

/// <summary>
/// The game-level registry of bitmap fonts for the UI, owned by <see cref="Application.CasaEngineGame"/>
/// next to its TTF <c>FontSystem</c> (ADR-0036).
/// <para/>
/// A UI text engine is rebuilt with every world, while a font must survive the change. So a font is
/// not registered on a text engine by whoever uses it: it is held through <see cref="Acquire"/>, and the
/// registry gives it, by reference, to every UI text engine attached while it is held. Every
/// <see cref="UIRoot"/> attaches its text engine when it is created and detaches it when it is disposed.
/// UI text elements then name the family, as in <c>FontFamily="font3"</c> in XAML.
/// <para/>
/// When the last holder gives a font back, it stays pending in the asset manager and stays registered on
/// the text engines, so acquiring it again costs nothing. When the asset manager frees it
/// (<see cref="AssetContentManager.CollectUnreferenced"/>), the registry removes it from the text engines.
/// Two fonts with the same family replace each other on the text engines.
/// </summary>
public sealed class UIFontRegistry
{
    private readonly AssetContentManager _assetContentManager;
    private readonly List<FontStashSharpTextEngine> _textEngines = new();
    private readonly Dictionary<Guid, Entry> _entries = new();

    public UIFontRegistry(AssetContentManager assetContentManager)
    {
        ArgumentNullException.ThrowIfNull(assetContentManager);
        _assetContentManager = assetContentManager;
    }

    /// <summary>
    /// Holds the bitmap font asset <paramref name="bitmapFontAssetId"/> (a <c>.fnt</c> catalog entry) and makes
    /// its family resolvable on every attached UI text engine. Dispose the result to give the hold back.
    /// </summary>
    /// <exception cref="InvalidOperationException">The asset is not in the catalog or cannot be loaded.</exception>
    public IDisposable Acquire(Guid bitmapFontAssetId)
    {
        if (!_entries.TryGetValue(bitmapFontAssetId, out var entry))
        {
            entry = new Entry();
            _entries.Add(bitmapFontAssetId, entry);
        }

        if (entry.Holders == 0)
        {
            AssetHandle<BitmapFont> handle;
            try
            {
                handle = _assetContentManager.Acquire<BitmapFont>(bitmapFontAssetId);
            }
            catch
            {
                if (entry.Font == null)
                {
                    _entries.Remove(bitmapFontAssetId);
                }

                throw;
            }

            entry.Handle = handle;
            if (!ReferenceEquals(entry.Font, handle.Asset))
            {
                entry.Font = handle.Asset;
                entry.Font.Disposed += OnFontDisposed;
            }

            for (var i = 0; i < _textEngines.Count; i++)
            {
                Register(_textEngines[i], entry.Font);
            }
        }

        entry.Holders++;
        return new Hold(this, bitmapFontAssetId);
    }

    /// <summary>Gives <paramref name="textEngine"/> every font currently held, and every font held later
    /// until <see cref="Detach"/>.</summary>
    public void Attach(FontStashSharpTextEngine textEngine)
    {
        ArgumentNullException.ThrowIfNull(textEngine);
        if (_textEngines.Contains(textEngine))
        {
            return;
        }

        _textEngines.Add(textEngine);
        foreach (var entry in _entries.Values)
        {
            if (entry.Holders > 0)
            {
                Register(textEngine, entry.Font);
            }
        }
    }

    /// <summary>Stops giving fonts to <paramref name="textEngine"/>.</summary>
    public void Detach(FontStashSharpTextEngine textEngine)
    {
        _textEngines.Remove(textEngine);
    }

    private static void Register(FontStashSharpTextEngine textEngine, BitmapFont font)
    {
        textEngine.AddStaticFont(font.Family, CustomFontStyles.Normal, font.Font);
    }

    private void Release(Guid bitmapFontAssetId)
    {
        if (!_entries.TryGetValue(bitmapFontAssetId, out var entry) || entry.Holders == 0)
        {
            return;
        }

        entry.Holders--;
        if (entry.Holders == 0)
        {
            // The font becomes pending in the asset manager; it stays on the text engines until it is freed.
            entry.Handle.Dispose();
            entry.Handle = null;
        }
    }

    private void OnFontDisposed(object sender, EventArgs e)
    {
        var font = (BitmapFont)sender;
        font.Disposed -= OnFontDisposed;

        for (var i = 0; i < _textEngines.Count; i++)
        {
            _textEngines[i].RemoveStaticFont(font.Family, CustomFontStyles.Normal);
        }

        Guid freedId = Guid.Empty;
        foreach (var pair in _entries)
        {
            if (ReferenceEquals(pair.Value.Font, font))
            {
                freedId = pair.Key;
                break;
            }
        }

        if (freedId != Guid.Empty && _entries[freedId].Holders == 0)
        {
            _entries.Remove(freedId);
        }
    }

    private sealed class Entry
    {
        public AssetHandle<BitmapFont> Handle;
        public BitmapFont Font;
        public int Holders;
    }

    private sealed class Hold : IDisposable
    {
        private UIFontRegistry _registry;
        private readonly Guid _bitmapFontAssetId;

        public Hold(UIFontRegistry registry, Guid bitmapFontAssetId)
        {
            _registry = registry;
            _bitmapFontAssetId = bitmapFontAssetId;
        }

        public void Dispose()
        {
            var registry = _registry;
            if (registry == null)
            {
                return;
            }

            _registry = null;
            registry.Release(_bitmapFontAssetId);
        }
    }
}
