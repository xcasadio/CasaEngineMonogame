using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Logs;
using CasaEngine.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class AssetCatalog
{
    private readonly Dictionary<Guid, AssetInfo> _assetInfos = new();

    public bool IsLoaded { get; private set; }

    public IEnumerable<AssetInfo> AssetInfos => _assetInfos.Values;

    public void Add(Guid id, string name, string fileName)
    {
        var assetInfo = new AssetInfo();

        _assetInfos.Add(assetInfo.Id, assetInfo);
    }

    public void Add(AssetInfo assetInfo)
    {
        _assetInfos.Add(assetInfo.Id, assetInfo);

#if EDITOR
        LogManager.Instance.WriteTrace($"Add asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
        AssetAdded?.Invoke(this, assetInfo);
#endif
    }

    public AssetInfo? Get(Guid guid)
    {
        _assetInfos.TryGetValue(guid, out var assetInfo);
        return assetInfo;
    }

    public AssetInfo? Get(string name)
    {
        return _assetInfos.Values.FirstOrDefault(x => x.Name == name);
    }

    public AssetInfo? GetByFileName(string fileName)
    {
        return _assetInfos.Values.FirstOrDefault(x => x.FileName == fileName);
    }

    public void Load(string fileName)
    {
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));

        foreach (var assetInfoNode in jsonDocument.RootElement.GetProperty("asset_infos").EnumerateArray())
        {
            var assetInfo = new AssetInfo();
            assetInfo.Load(assetInfoNode);
            Add(assetInfo);
        }

        IsLoaded = true;
    }

#if EDITOR

    public event EventHandler<AssetInfo>? AssetAdded;
    public event EventHandler<AssetInfo>? AssetRemoved;
    public event EventHandler<EventArgs<AssetInfo, string>>? AssetRenamed;
    public event EventHandler? AssetCleared;

    public void Remove(Guid id)
    {
        _assetInfos.TryGetValue(id, out var assetInfo);
        LogManager.Instance.WriteTrace($"Remove asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
        _assetInfos.Remove(id);
        DeleteFile(assetInfo);
        Save();
        AssetRemoved?.Invoke(this, assetInfo);
    }

    private static void DeleteFile(AssetInfo assetInfo)
    {
        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);
        if (File.Exists(fullFileName))
        {
            File.Delete(fullFileName);
        }
    }

    public void Clear()
    {
        LogManager.Instance.WriteTrace("Clear all assets");

        _assetInfos.Clear();
        AssetCleared?.Invoke(this, EventArgs.Empty);
    }

    public bool CanRename(string newName)
    {
        return !_assetInfos.Any(x => string.Equals(x.Value.Name, newName, StringComparison.InvariantCultureIgnoreCase));
    }

    public bool Rename(Guid id, string newName)
    {
        var assetInfo = Get(id);

        if (assetInfo == null)
        {
            LogManager.Instance.WriteError($"Rename Entity : The id '{id}' is not present in the catalog. (new name is {newName})");
            return false;
        }

        var oldName = assetInfo.Name;
        assetInfo.Name = newName;

        AssetRenamed?.Invoke(this, new EventArgs<AssetInfo, string>(assetInfo, oldName));

        return true;
    }

    public void Rename(AssetInfo assetInfo, string newName)
    {
        var oldName = assetInfo.Name;
        assetInfo.Name = newName;

        AssetRenamed?.Invoke(this, new EventArgs<AssetInfo, string>(assetInfo, oldName));
    }

    public void Save()
    {
        Save(Path.Combine(EngineEnvironment.ProjectPath, "AssetInfos.json"));
    }

    public void Save(string fileName)
    {
        LogManager.Instance.WriteInfo($"Asset infos saved in {fileName}");

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
    }

#endif
}