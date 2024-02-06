using System.Text.Json;
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public class AutoTileData : TileData
{
    public int AutoTileIndex;
    public Rectangle[] Locations { get; } = new Rectangle[6];

    public AutoTileData() : base(TileType.Auto)
    { }

    public override void Load(JsonElement element)
    {
        base.Load(element);
        AutoTileIndex = element.GetProperty("auto_tile_index").GetInt32();

        var index = 0;
        foreach (var locationNode in element.GetProperty("locations").EnumerateArray())
        {
            Locations[index] = locationNode.GetRectangle();
            index++;
        }
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        jObject.Add("auto_tile_index", AutoTileIndex);

        var jArray = new JArray();

        foreach (var location in Locations)
        {
            var locationNode = new JObject();
            location.Save(locationNode);
            jArray.Add(locationNode);
        }

        jObject.Add("locations", jArray);
    }

#endif

}