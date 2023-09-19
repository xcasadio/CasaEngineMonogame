using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Logger;
using CasaEngine.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class AssetInfoManager
{
    private readonly Dictionary<long, AssetInfo> _assetInfos = new();

    public bool IsLoaded { get; private set; }

    public IEnumerable<AssetInfo> AssetInfos => _assetInfos.Values;

    public void Add(AssetInfo assetInfo)
    {
        _assetInfos.Add(assetInfo.Id, assetInfo);

#if EDITOR
        LogManager.Instance.WriteLineTrace($"Add asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
        AssetAdded?.Invoke(this, assetInfo);
#endif
    }

    [Obsolete]
    public AssetInfo GetOrAdd(string fileName)
    {
        foreach (var assetInfo in _assetInfos.Values)
        {
            if (assetInfo.FileName == fileName)
            {
                return assetInfo;
            }
        }

        var newAssetInfo = new AssetInfo();
        newAssetInfo.Name = Path.GetFileNameWithoutExtension(fileName);
        newAssetInfo.FileName = fileName;
        Add(newAssetInfo);
        return newAssetInfo;
    }

    public AssetInfo? Get(long assetId)
    {
        _assetInfos.TryGetValue(assetId, out var assetInfo);
        return assetInfo;
    }

    public AssetInfo? Get(string name)
    {
        return _assetInfos.Values.FirstOrDefault(x => x.Name == name);
    }

    public void Load(string fileName, SaveOption option)
    {
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));

        foreach (var assetInfoNode in jsonDocument.RootElement.GetProperty("asset_infos").EnumerateArray())
        {
            var assetInfo = new AssetInfo(false);
            assetInfo.Load(assetInfoNode, option);
            Add(assetInfo);
        }

        IsLoaded = true;
    }

#if EDITOR

    public event EventHandler<AssetInfo> AssetAdded;
    public event EventHandler<AssetInfo> AssetRemoved;
    public event EventHandler<EventArgs<AssetInfo, string>> AssetRenamed;
    public event EventHandler AssetCleared;

    public void Save()
    {
        Save(Path.Combine(EngineEnvironment.ProjectPath, "AssetInfos.json"), SaveOption.Editor);
    }

    public void Save(string fileName, SaveOption option)
    {
        LogManager.Instance.WriteLineInfo($"Asset infos saved in {fileName}");

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

    public void Remove(long id)
    {
        _assetInfos.TryGetValue(id, out var assetInfo);
        LogManager.Instance.WriteLineTrace($"Remove asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
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
        LogManager.Instance.WriteLineTrace("Clear all assets");

        _assetInfos.Clear();
        AssetCleared?.Invoke(this, EventArgs.Empty);
    }

    public bool CanRename(AssetInfo assetInfo, string newName)
    {
        return !_assetInfos.Any(x => string.Equals(x.Value.Name, newName, StringComparison.CurrentCultureIgnoreCase));
    }

    public void Rename(AssetInfo assetInfo, string newName)
    {
        var oldName = assetInfo.Name;
        assetInfo.Name = newName;

        AssetRenamed?.Invoke(this, new EventArgs<AssetInfo, string>(assetInfo, oldName));
    }

#endif
}