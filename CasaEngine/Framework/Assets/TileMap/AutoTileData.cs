
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public class AutoTileData : TileData
{
    public int AutoTileIndex;
    public Rectangle[] Locations { get; } = new Rectangle[6];

    public AutoTileData() : base(TileType.Auto)
    { }

    public override void Load(JObject element)
    {
        base.Load(element);
        AutoTileIndex = element["auto_tile_index"].GetInt32();

        var index = 0;
        foreach (var locationNode in element["locations"])
        {
            Locations[index] = locationNode.GetRectangle();
            index++;
        }
    }

}