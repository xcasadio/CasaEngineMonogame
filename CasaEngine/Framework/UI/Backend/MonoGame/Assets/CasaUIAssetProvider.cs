using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Configuration;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Assets;
using MGUI.Shared.Text;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Assets;

public sealed class CasaUIAssetProvider : IUIAssetProvider, IDisposable
{
    private static readonly string SpriteAssetType = Constants.FileNameExtensions.Sprite.TrimStart('.');

    private readonly AssetContentManager _assetContentManager;
    private readonly HashSet<string> _warnedNames = new();
    private readonly List<AssetHandle<SpriteData>> _heldSpriteData = new();
    private readonly List<Sprite> _heldSprites = new();
    private bool _disposed;

    public ContentManager Content { get; }
    public FontManager FontManager { get; }

    /// <param name="assetContentManager">The game's asset manager, used to resolve <see cref="TryResolveImage"/>
    /// names that are catalogued sprite assets (ADR-0038, ADR-0016). Optional: when null, <see cref="TryResolveImage"/>
    /// resolves nothing, same as before this parameter existed.</param>
    public CasaUIAssetProvider(ContentManager content, FontManager fontManager, AssetContentManager assetContentManager = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fontManager);
        Content = content;
        FontManager = fontManager;
        _assetContentManager = assetContentManager;
    }

    public IUIImageResource LoadImage(string assetName)
        => new CasaMonoGameImageResource(LoadTexture(assetName));

    public bool TryLoadImage(string assetName, out IUIImageResource image)
    {
        if (TryLoadTexture(assetName, out Texture2D texture))
        {
            image = new CasaMonoGameImageResource(texture);
            return true;
        }

        image = null!;
        return false;
    }

    public Texture2D LoadTexture(string assetName)
        => Content.Load<Texture2D>(assetName);

    public bool TryLoadTexture(string assetName, out Texture2D texture)
    {
        try
        {
            texture = LoadTexture(assetName);
            return true;
        }
        catch
        {
            texture = null!;
            return false;
        }
    }

    /// <summary>Resolves <paramref name="name"/> as a catalogued sprite asset id (a GUID string) or asset name
    /// (ADR-0038, "Images are named by asset"; MGUI ADR-0016). The sprite's sheet texture and its
    /// <see cref="SpriteData.PositionInTexture"/> are held through counted handles until this provider is
    /// disposed. Never throws: an unknown name, a non-sprite asset, a missing asset manager, or a load failure
    /// all return false, logging one warning per name.</summary>
    public bool TryResolveImage(string name, out IUIImageResource image, out Rectangle? sourceRect)
    {
        image = null;
        sourceRect = null;

        if (_assetContentManager == null)
        {
            WarnOnce(name, "no AssetContentManager was provided to this UI asset provider");
            return false;
        }

        var assetInfo = Guid.TryParse(name, out var assetId) ? AssetCatalog.Get(assetId) : AssetCatalog.Get(name);
        if (assetInfo == null)
        {
            WarnOnce(name, "no catalogued asset matches this name or id");
            return false;
        }

        if (!string.Equals(assetInfo.AssetType, SpriteAssetType, StringComparison.OrdinalIgnoreCase))
        {
            WarnOnce(name, $"asset '{assetInfo.Name}' ({assetInfo.Id}) is a '{assetInfo.AssetType}' asset, not a sprite");
            return false;
        }

        AssetHandle<SpriteData> spriteDataHandle;
        try
        {
            spriteDataHandle = _assetContentManager.Acquire<SpriteData>(assetInfo.Id);
        }
        catch (Exception exception)
        {
            WarnOnce(name, $"failed to load sprite data for asset '{assetInfo.Name}' ({assetInfo.Id}): {exception.Message}");
            return false;
        }

        Sprite sprite;
        try
        {
            sprite = Sprite.Create(spriteDataHandle.Asset, _assetContentManager);
        }
        catch (Exception exception)
        {
            spriteDataHandle.Dispose();
            WarnOnce(name, $"failed to load the sheet texture for sprite asset '{assetInfo.Name}' ({assetInfo.Id}): {exception.Message}");
            return false;
        }

        _heldSpriteData.Add(spriteDataHandle);
        _heldSprites.Add(sprite);

        image = new CasaMonoGameImageResource(sprite.Texture.Resource);
        sourceRect = sprite.SpriteData.PositionInTexture;
        return true;
    }

    private void WarnOnce(string name, string reason)
    {
        if (_warnedNames.Add(name))
        {
            Logs.WriteWarning($"CasaUIAssetProvider: cannot resolve UI image '{name}' ({reason}).");
        }
    }

    /// <summary>Gives back every sprite data and sheet texture handle held by <see cref="TryResolveImage"/>.
    /// Idempotent. After this call, the held assets are freed at the next
    /// <see cref="AssetContentManager.CollectUnreferenced"/> like any other unheld asset.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var sprite in _heldSprites)
        {
            sprite.Dispose();
        }
        _heldSprites.Clear();

        foreach (var handle in _heldSpriteData)
        {
            handle.Dispose();
        }
        _heldSpriteData.Clear();
    }
}
