using CasaEngine.Framework;
using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorServices;

public static class EditorAssetCatalogService
{
    static EditorAssetCatalogService()
    {
        AssetCatalog.AssetAdded += OnAssetAdded;
        AssetCatalog.AssetRemoved += OnAssetRemoved;
        AssetCatalog.AssetRenamed += OnAssetRenamed;
        AssetCatalog.AssetCleared += OnAssetCleared;
    }

    public static IEnumerable<AssetInfo> AssetInfos => AssetCatalog.AssetInfos;

    public static event EventHandler<AssetInfo>? AssetAdded;
    public static event EventHandler<AssetInfo>? AssetRemoved;
    public static event EventHandler<CasaEngine.Core.Design.EventArgs<AssetInfo, string>>? AssetRenamed;
    public static event EventHandler? AssetCleared;

    public static void Add(AssetInfo assetInfo) => AssetCatalog.Add(assetInfo);

    public static void Add(ObjectBase objectBase) => AssetCatalog.Add(objectBase);

    public static void Add(Guid id, string name, string fileName) => AssetCatalog.Add(id, name, fileName);

    public static void Remove(Guid id) => AssetCatalog.Remove(id);

    public static bool CanRename(string newName) => AssetCatalog.CanRename(newName);

    public static bool Rename(Guid id, string newName) => AssetCatalog.Rename(id, newName);

    public static void Rename(AssetInfo assetInfo, string newName) => AssetCatalog.Rename(assetInfo, newName);

    public static void Save() => AssetCatalog.Save();

    public static void Clear() => AssetCatalog.Clear();

    private static void OnAssetAdded(object? sender, AssetInfo assetInfo)
    {
        AssetAdded?.Invoke(sender, assetInfo);
    }

    private static void OnAssetRemoved(object? sender, AssetInfo assetInfo)
    {
        AssetRemoved?.Invoke(sender, assetInfo);
    }

    private static void OnAssetRenamed(object? sender, CasaEngine.Core.Design.EventArgs<AssetInfo, string> eventArgs)
    {
        AssetRenamed?.Invoke(sender, eventArgs);
    }

    private static void OnAssetCleared(object? sender, EventArgs eventArgs)
    {
        AssetCleared?.Invoke(sender, eventArgs);
    }
}