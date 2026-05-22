using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public sealed class TileMapObjectLayerData
{
    public string? Name { get; set; }
    public float zOffset;
    public List<TileMapObjectData> Objects { get; } = new();
    public Dictionary<string, string> CustomProperties { get; } = new(StringComparer.Ordinal);
    public TileMapDepthSettings Depth { get; private set; } = TileMapDepthSettings.CreateDefault(TileMapDepthRole.ObjectSource);

    public void Load(JObject element)
    {
        Name = element.ContainsKey("name") ? element["name"]?.GetString() : null;
        zOffset = element["z_offset"].GetSingle();
        TileMapData.LoadCustomProperties(element["custom_properties"], CustomProperties);
        Depth = TileMapDepthSettings.FromCustomProperties(CustomProperties, TileMapDepthRole.ObjectSource);
        Objects.Clear();

        foreach (var objectToken in element.GetElements("objects", token => token))
        {
            var objectData = new TileMapObjectData();
            objectData.Load((JObject)objectToken);
            Objects.Add(objectData);
        }
    }
}

public sealed class TileMapObjectData
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public Dictionary<string, string> CustomProperties { get; } = new(StringComparer.Ordinal);
    public TileMapDepthSettings Depth { get; private set; } = TileMapDepthSettings.CreateDefault(TileMapDepthRole.ObjectSource);

    public void Load(JObject element)
    {
        Id = element["id"].GetInt32();
        Name = element.ContainsKey("name") ? element["name"]?.GetString() : null;
        Type = element.ContainsKey("type") ? element["type"]?.GetString() : null;
        X = element["x"].GetSingle();
        Y = element["y"].GetSingle();
        Width = element["width"].GetSingle();
        Height = element["height"].GetSingle();
        TileMapData.LoadCustomProperties(element["custom_properties"], CustomProperties);
        Depth = TileMapDepthSettings.FromCustomProperties(CustomProperties, TileMapDepthRole.ObjectSource);
    }
}