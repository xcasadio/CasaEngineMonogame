using CasaEngine.Framework.Assets;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CasaEngine.EditorUI.DragAndDrop;

/// <summary>
/// Central registry that resolves the correct <see cref="IAssetDropHandler"/>
/// for a given <see cref="AssetInfo"/>. Use <see cref="Instance"/> to access
/// the application-wide singleton.
/// </summary>
public class AssetDropHandlerRegistry
{
    private static AssetDropHandlerRegistry? _instance;

    /// <summary>Application-wide singleton instance.</summary>
    public static AssetDropHandlerRegistry Instance => _instance ??= new AssetDropHandlerRegistry();

    private readonly List<IAssetDropHandler> _handlers = new();

    private AssetDropHandlerRegistry() { }

    /// <summary>Registers a handler into the registry.</summary>
    public void Register(IAssetDropHandler handler)
    {
        _handlers.Add(handler);
    }

    /// <summary>
    /// Returns the first handler that can process the given asset,
    /// or <c>null</c> if none matches.
    /// </summary>
    public IAssetDropHandler? FindHandler(AssetInfo assetInfo)
    {
        var extension = Path.GetExtension(assetInfo.FileName);
        foreach (var handler in _handlers)
        {
            if (handler.SupportedExtensions.Contains(extension) && handler.CanHandle(assetInfo))
                return handler;
        }
        return null;
    }

    /// <summary>Returns true if any registered handler can process the asset.</summary>
    public bool CanHandle(AssetInfo assetInfo) => FindHandler(assetInfo) != null;
}
