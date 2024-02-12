
using CasaEngine.Core.Design;
using CasaEngine.Core.Log;
using CasaEngine.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public static class AssetCatalog
{
    private static readonly Dictionary<Guid, AssetInfo> _assetInfos = new();

    public static bool IsLoaded { get; private set; }

    public static IEnumerable<AssetInfo> AssetInfos => _assetInfos.Values;

    public static void Add(Guid id, string name, string fileName)
    {
        var assetInfo = new AssetInfo();

        _assetInfos.Add(assetInfo.Id, assetInfo);
    }

    public static void Add(AssetInfo assetInfo)
    {
        _assetInfos.Add(assetInfo.Id, assetInfo);

#if EDITOR
        Logs.WriteTrace($"Add asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
        AssetAdded?.Invoke(null, assetInfo);
#endif
    }

    public static AssetInfo? Get(Guid guid)
    {
        _assetInfos.TryGetValue(guid, out var assetInfo);
        return assetInfo;
    }

    public static AssetInfo? Get(string name)
    {
        return _assetInfos.Values.FirstOrDefault(x => x.Name == name);
    }

    public static AssetInfo? GetByFileName(string fileName)
    {
        return _assetInfos.Values.FirstOrDefault(x => x.FileName == fileName);
    }

    public static void Load(string fileName)
    {
        var rootElement = JObject.Parse(File.ReadAllText(fileName));

        foreach (var assetInfoNode in rootElement["asset_infos"])
        {
            var assetInfo = new AssetInfo();
            assetInfo.Load((JObject)assetInfoNode);
            Add(assetInfo);
        }

        IsLoaded = true;
    }

#if EDITOR

    public static event EventHandler<AssetInfo>? AssetAdded;
    public static event EventHandler<AssetInfo>? AssetRemoved;
    public static event EventHandler<EventArgs<AssetInfo, string>>? AssetRenamed;
    public static event EventHandler? AssetCleared;

    public static void Remove(Guid id)
    {
        _assetInfos.TryGetValue(id, out var assetInfo);
        Logs.WriteTrace($"Remove asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
        _assetInfos.Remove(id);
        DeleteFile(assetInfo);
        Save();
        AssetRemoved?.Invoke(null, assetInfo);
    }

    private static void DeleteFile(AssetInfo assetInfo)
    {
        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);
        if (File.Exists(fullFileName))
        {
            File.Delete(fullFileName);
        }
    }

    public static void Clear()
    {
        Logs.WriteTrace("Clear all assets");

        _assetInfos.Clear();
        AssetCleared?.Invoke(null, EventArgs.Empty);
    }

    public static bool CanRename(string newName)
    {
        return !_assetInfos.Any(x => string.Equals(x.Value.Name, newName, StringComparison.InvariantCultureIgnoreCase));
    }

    public static bool Rename(Guid id, string newName)
    {
        var assetInfo = Get(id);

        if (assetInfo == null)
        {
            Logs.WriteError($"Rename Entity : The id '{id}' is not present in the catalog. (new name is {newName})");
            return false;
        }

        var oldName = assetInfo.Name;
        assetInfo.Name = newName;

        AssetRenamed?.Invoke(null, new EventArgs<AssetInfo, string>(assetInfo, oldName));

        return true;
    }

    public static void Rename(AssetInfo assetInfo, string newName)
    {
        var oldName = assetInfo.Name;
        assetInfo.Name = newName;

        AssetRenamed?.Invoke(null, new EventArgs<AssetInfo, string>(assetInfo, oldName));
    }

    public static void Save()
    {
        string fileName = Path.Combine(EngineEnvironment.ProjectPath, "AssetInfos.json");
        JObject root = new();
        var assetInfoJArray = new JArray();

        foreach (var assetInfo in _assetInfos)
        {
            var entityObject = new JObject(
                new JProperty("id", assetInfo.Value.Id),
                new JProperty("file_name", assetInfo.Value.FileName));

            assetInfoJArray.Add(entityObject);
        }

        root.Add("asset_infos", assetInfoJArray);

        using StreamWriter file = File.CreateText(fileName);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        root.WriteTo(writer);

        Logs.WriteInfo($"Asset infos saved in {fileName}");
    }

#endif
}