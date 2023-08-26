using System.Text.Json;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public class StaticTileData : TileData
{
    public Rectangle Location { get; set; }

    public StaticTileData() : base(TileType.Static)
    { }

    public override void Load(JsonElement element)
    {
        base.Load(element);
        Location = element.GetProperty("location").GetRectangle();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        var newNode = new JObject();
        Location.Save(newNode);
        jObject.Add("location", newNode);
    }

#endif
}