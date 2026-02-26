using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using System.Windows;

namespace CasaEngine.EditorUI.DragAndDrop;

/// <summary>
/// Reusable helper that centralises the common drag-and-drop asset logic
/// (extraction, validation, entity creation) so any drop-target control
/// can simply delegate to these methods instead of duplicating the logic.
/// </summary>
public static class AssetDropHelper
{
    /// <summary>
    /// Extracts an <see cref="AssetInfo"/> from the drag data, or returns
    /// <c>null</c> if the data does not carry an asset.
    /// </summary>
    public static AssetInfo? ExtractAssetInfo(DragEventArgs e)
    {
        var formats = e.Data.GetFormats();
        if (formats.Length > 0 && formats[0] == typeof(AssetInfo).FullName)
            return e.Data.GetData(typeof(AssetInfo)) as AssetInfo;
        return null;
    }

    /// <summary>
    /// Handles a <c>DragOver</c> event: sets <see cref="DragEventArgs.Effects"/>
    /// to <see cref="DragDropEffects.Copy"/> when the registry supports the asset,
    /// or <see cref="DragDropEffects.None"/> when unsupported.
    /// Always sets <c>e.Handled = true</c> for asset payloads.
    /// </summary>
    public static void HandleDragOver(DragEventArgs e)
    {
        var assetInfo = ExtractAssetInfo(e);
        if (assetInfo == null)
            return;

        e.Effects = AssetDropHandlerRegistry.Instance.CanHandle(assetInfo)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Handles a <c>Drop</c> event for asset payloads: resolves the handler from
    /// the registry, creates the entity, and returns it. Returns <c>null</c> when
    /// no handler supports the asset or when the drag data is not an asset.
    /// </summary>
    public static Entity? HandleDrop(DragEventArgs e, CasaEngineGame game)
    {
        var assetInfo = ExtractAssetInfo(e);
        if (assetInfo == null)
            return null;

        var handler = AssetDropHandlerRegistry.Instance.FindHandler(assetInfo);
        return handler?.CreateEntity(assetInfo, game);
    }
}
