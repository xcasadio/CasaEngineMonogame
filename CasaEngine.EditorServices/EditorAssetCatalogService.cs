using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices;

public static class EditorAssetCatalogService
{
    public static IEnumerable<AssetInfo> AssetInfos => AssetCatalog.AssetInfos;

    public static event EventHandler<AssetInfo>? AssetAdded;
    public static event EventHandler<AssetInfo>? AssetRemoved;
    public static event EventHandler<CasaEngine.Core.Design.EventArgs<AssetInfo, string>>? AssetRenamed;
    public static event EventHandler? AssetCleared;

    public static void Add(AssetInfo assetInfo)
    {
        AssetCatalog.AddInternal(assetInfo);
        AssetAdded?.Invoke(null, assetInfo);
    }

    public static void Add(ObjectBase objectBase)
    {
        AssetCatalog.AddInternal(objectBase.Id, objectBase.Name, objectBase.FileName);
        var assetInfo = AssetCatalog.Get(objectBase.Id)!;
        AssetAdded?.Invoke(null, assetInfo);
    }

    public static void Add(Guid id, string name, string fileName)
    {
        AssetCatalog.AddInternal(id, name, fileName);
        var assetInfo = AssetCatalog.Get(id)!;
        AssetAdded?.Invoke(null, assetInfo);
    }

    public static bool TryRemoveEntry(Guid id, out AssetInfo? assetInfo)
        => AssetCatalog.RemoveInternal(id, out assetInfo);

    public static void RestoreEntry(AssetInfo assetInfo)
    {
        ArgumentNullException.ThrowIfNull(assetInfo);
        AssetCatalog.AddInternal(assetInfo);
    }

    public static void Remove(Guid id)
    {
        if (!AssetCatalog.RemoveInternal(id, out var assetInfo) || assetInfo == null)
        {
            return;
        }

        DeleteFile(assetInfo);
        Save();
        AssetRemoved?.Invoke(null, assetInfo);
    }

    public static bool CanRename(string newName) => AssetCatalog.CanRenameInternal(newName);

    public static bool Rename(Guid id, string newName)
    {
        if (!AssetCatalog.RenameInternal(id, newName, out var assetInfo, out var oldName)
            || assetInfo == null
            || oldName == null)
        {
            return false;
        }

        AssetRenamed?.Invoke(null, new CasaEngine.Core.Design.EventArgs<AssetInfo, string>(assetInfo, oldName));
        return true;
    }

    public static void Rename(AssetInfo assetInfo, string newName)
    {
        var oldName = AssetCatalog.RenameInternal(assetInfo, newName);
        AssetRenamed?.Invoke(null, new CasaEngine.Core.Design.EventArgs<AssetInfo, string>(assetInfo, oldName));
    }

    public static void Save()
    {
        string fileName = Path.Combine(EngineEnvironment.ProjectPath, "AssetInfos.json");
        JObject root = new();
        var assetInfoJArray = new JArray();

        foreach (var assetInfo in AssetCatalog.AssetInfos)
        {
            var entityObject = new JObject();
            EditorJsonSaveHelper.SaveAssetInfo(assetInfo, entityObject);
            assetInfoJArray.Add(entityObject);
        }

        root.Add("asset_infos", assetInfoJArray);

        using StreamWriter file = File.CreateText(fileName);
        using JsonTextWriter writer = new(file) { Formatting = Formatting.Indented };
        root.WriteTo(writer);
    }

    public static void Clear()
    {
        AssetCatalog.ClearInternal();
        AssetCleared?.Invoke(null, EventArgs.Empty);
    }

    private static void DeleteFile(AssetInfo assetInfo)
    {
        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);
        if (File.Exists(fullFileName))
        {
            File.Delete(fullFileName);
        }
    }
}