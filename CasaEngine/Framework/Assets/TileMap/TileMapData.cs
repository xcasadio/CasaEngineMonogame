using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Engine;
using Newtonsoft.Json.Linq;
using Size = CasaEngine.Core.Maths.Size;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapData : AssetInfo
{
    public Size MapSize { get; set; }
    public TileSetData TileSetData { get; set; }
    public List<TileMapLayerData> Layers { get; } = new();

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);

        MapSize = element.GetProperty("map_size").GetSize();

        TileSetData = LoadTileSetData(element.GetProperty("tile_set").GetString());

        foreach (var jObject in element.GetProperty("layers").EnumerateArray())
        {
            var tileMapLayerData = new TileMapLayerData();
            tileMapLayerData.Load(jObject);
            Layers.Add(tileMapLayerData);
        }
    }

    private static TileSetData LoadTileSetData(string fileName)
    {
        fileName = Path.Combine(EngineEnvironment.ProjectPath, fileName);
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var tileSetData = new TileSetData();
        tileSetData.Load(jsonDocument.RootElement, SaveOption.Editor);

        return tileSetData;
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);
    }

#endif
}