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

    private void Add(AssetInfo assetInfo)
    {
        _assetInfos.Add(assetInfo.Id, assetInfo);
    }

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
    public event EventHandler AssetCleared;

    public void Save()
    {
        Save(Path.Combine(EngineEnvironment.ProjectPath, "AssetInfos.json"), SaveOption.Editor);
    }

    public void Save(string fileName, SaveOption option)
    {
        JObject root = new();
        var assetInfoJArray = new JArray();

        foreach (var assetInfo in _assetInfos)
        {
            JObject entityObject = new();
            assetInfo.Value.Save(entityObject, option);
            assetInfoJArray.Add(entityObject);
        }

        root.Add("asset_infos", assetInfoJArray);

        using StreamWriter file = File.CreateText(fileName);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        root.WriteTo(writer);
    }

    public void AddAndSave(AssetInfo assetInfo)
    {
        LogManager.Instance.WriteLineTrace($"Add asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");

        Add(assetInfo);
        Save();
        AssetAdded?.Invoke(this, assetInfo);
    }

    public void Remove(long id)
    {
        _assetInfos.TryGetValue(id, out var assetInfo);
        LogManager.Instance.WriteLineTrace($"Remove asset Id:{assetInfo.Id}, Name:{assetInfo.Name}, FileName:{assetInfo.FileName}");
        _assetInfos.Remove(id);
        Save();
        AssetRemoved?.Invoke(this, assetInfo);
    }

    public void Clear()
    {
        LogManager.Instance.WriteLineTrace("Clear all assets");

        _assetInfos.Clear();
        AssetCleared?.Invoke(this, EventArgs.Empty);
    }

#endif
}