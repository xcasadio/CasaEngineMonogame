using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;


namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapLayerData
{
    public string? Name { get; set; }
    public List<int> tiles = new();
    public float zOffset;

    public void Load(JObject element)
    {
        zOffset = element["z_offset"].GetSingle();
        tiles = element["tiles"].Values<int>().ToList();
    }

#if EDITOR

    public void Save(JObject jObject)
    {
        jObject.Add("z_offset", zOffset);
        jObject.Add("tiles", new JArray(tiles));
    }

#endif
}