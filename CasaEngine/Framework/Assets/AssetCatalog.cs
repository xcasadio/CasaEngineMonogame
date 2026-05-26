using CasaEngine.Core.Logging;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public static class AssetCatalog
{
    private static readonly Dictionary<Guid, AssetInfo> _assetInfos = new();
    private static readonly Dictionary<string, AssetInfo> _assetInfosByName = new();
    private static readonly Dictionary<string, AssetInfo> _assetInfosByFileName = new();

    public static bool IsLoaded { get; private set; }

    public static IEnumerable<AssetInfo> AssetInfos => _assetInfos.Values;

    internal static void AddInternal(AssetInfo assetInfo)
    {
        NormalizeAssetInfo(assetInfo);
        _assetInfos.Add(assetInfo.Id, assetInfo);
        _assetInfosByName[assetInfo.Name] = assetInfo;
        _assetInfosByFileName[assetInfo.FileName] = assetInfo;

        Logs.WriteTrace($"Add asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
    }

    public static AssetInfo Get(Guid guid)
    {
        _assetInfos.TryGetValue(guid, out var assetInfo);
        return assetInfo;
    }

    public static AssetInfo Get(string name)
    {
        _assetInfosByName.TryGetValue(name, out var assetInfo);
        return assetInfo;
    }

    public static AssetInfo GetByFileName(string fileName)
    {
        _assetInfosByFileName.TryGetValue(fileName, out var assetInfo);
        return assetInfo;
    }

    public static void Load(string fileName)
    {
        ClearInternal();

        var rootElement = JObject.Parse(File.ReadAllText(fileName));

        var assetInfosNode = rootElement["asset_infos"] as JArray;
        if (assetInfosNode == null)
        {
            IsLoaded = true;
            return;
        }

        foreach (var assetInfoNode in assetInfosNode)
        {
            var assetInfo = new AssetInfo();
            assetInfo.Load((JObject)assetInfoNode);
            AddInternal(assetInfo);
        }

        IsLoaded = true;
    }

    internal static void AddInternal(Guid id, string name, string fileName)
    {
        var assetInfo = new AssetInfo(id)
        {
            Name = name,
            FileName = fileName,
            AssetType = AssetInfo.InferAssetType(fileName),
        };

        AddInternal(assetInfo);
    }

    internal static bool RemoveInternal(Guid id, out AssetInfo assetInfo)
    {
        if (!_assetInfos.TryGetValue(id, out var existingAssetInfo))
        {
            assetInfo = null;
            return false;
        }

        assetInfo = existingAssetInfo;

        Logs.WriteTrace($"Remove asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
        _assetInfos.Remove(id);
        _assetInfosByName.Remove(assetInfo.Name);
        _assetInfosByFileName.Remove(assetInfo.FileName);
        return true;
    }

    internal static void ClearInternal()
    {
        Logs.WriteTrace("Clear all assets");

        _assetInfos.Clear();
        _assetInfosByName.Clear();
        _assetInfosByFileName.Clear();
        IsLoaded = false;
    }

    internal static bool CanRenameInternal(string newName)
    {
        return !_assetInfos.Any(x => string.Equals(x.Value.Name, newName, StringComparison.InvariantCultureIgnoreCase));
    }

    internal static bool RenameInternal(Guid id, string newName, out AssetInfo assetInfo, out string oldName)
    {
        assetInfo = Get(id);
        oldName = null;

        if (assetInfo == null)
        {
            Logs.WriteError($"Rename Entity : The id '{id}' is not present in the catalog. (new name is {newName})");
            return false;
        }

        oldName = assetInfo.Name;
        _assetInfosByName.Remove(oldName);
        assetInfo.Name = newName;
        _assetInfosByName[newName] = assetInfo;

        return true;
    }

    internal static string RenameInternal(AssetInfo assetInfo, string newName)
    {
        var oldName = assetInfo.Name;
        _assetInfosByName.Remove(oldName);
        assetInfo.Name = newName;
        _assetInfosByName[newName] = assetInfo;

        return oldName;
    }

    private static void NormalizeAssetInfo(AssetInfo assetInfo)
    {
        assetInfo.Name = string.IsNullOrWhiteSpace(assetInfo.Name)
            ? Path.GetFileNameWithoutExtension(assetInfo.FileName)
            : assetInfo.Name;

        if (string.IsNullOrWhiteSpace(assetInfo.AssetType))
        {
            assetInfo.AssetType = AssetInfo.InferAssetType(assetInfo.FileName);
        }
    }
}