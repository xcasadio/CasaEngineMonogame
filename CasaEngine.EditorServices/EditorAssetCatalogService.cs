using CasaEngine.Framework;
using CasaEngine.Framework.Assets;

namespace CasaEngine.EditorServices;

public static class EditorAssetCatalogService
{
    public static IEnumerable<AssetInfo> AssetInfos => AssetCatalog.AssetInfos;

    public static void Add(AssetInfo assetInfo) => AssetCatalog.Add(assetInfo);

    public static void Add(ObjectBase objectBase) => AssetCatalog.Add(objectBase);

    public static void Add(Guid id, string name, string fileName) => AssetCatalog.Add(id, name, fileName);

    public static void Remove(Guid id) => AssetCatalog.Remove(id);

    public static bool CanRename(string newName) => AssetCatalog.CanRename(newName);

    public static bool Rename(Guid id, string newName) => AssetCatalog.Rename(id, newName);

    public static void Rename(AssetInfo assetInfo, string newName) => AssetCatalog.Rename(assetInfo, newName);

    public static void Save() => AssetCatalog.Save();

    public static void Clear() => AssetCatalog.Clear();
}