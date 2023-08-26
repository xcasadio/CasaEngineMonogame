using CasaEngine.Core.Design;
using CasaEngine.Framework.Game;
using Newtonsoft.Json.Linq;
using SharpDX.Direct2D1;
using System.Text.Json;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapLayerData
{
    public string? Name { get; set; }
    public List<int> tiles = new();
    public float zOffset;

    public void Load(JsonElement element)
    {
        zOffset = element.GetProperty("z_offset").GetSingle();
        tiles = element.GetProperty("tiles").Deserialize<List<int>>();
    }

#if EDITOR

    public void Save(JObject jObject)
    {
        jObject.Add("z_offset", zOffset);
        jObject.Add("tiles", new JArray(tiles));
    }

#endif
}