using Size = CasaEngine.Core.Maths.Size;

namespace CasaEngine.Framework.Assets.Map2d;

public class TiledMapData
{
    public Size MapSize { get; set; }
    public Size TileSize { get; set; }
    public List<string> SpriteSheetFileNames { get; } = new();
    public List<string> AutoTileSetFileNames { get; } = new();
    public List<TiledMapLayerData> Layers { get; } = new();
    public List<AutoTileSetData> AutoTileSetDatas { get; } = new();
    public List<TileData> TileDefinitions { get; } = new();
}