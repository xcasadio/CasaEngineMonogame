using System.Text.Json;
using CasaEngine.Core.Design;
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
    }

    public void Remove(long id)
    {
        _assetInfos.Remove(id);
    }

    public void Clear()
    {
        _assetInfos.Clear();
    }

    public AssetInfo Get(long assetId)
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

#endif
}