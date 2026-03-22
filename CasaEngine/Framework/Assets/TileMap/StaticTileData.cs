
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public class StaticTileData : TileData
{
    public Rectangle Location { get; set; }

    public StaticTileData() : base(TileType.Static)
    { }

    public override void Load(JObject element)
    {
        base.Load(element);
        Location = element["location"].GetRectangle();
    }

}