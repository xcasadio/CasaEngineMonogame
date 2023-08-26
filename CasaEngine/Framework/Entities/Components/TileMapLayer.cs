using CasaEngine.Framework.Assets.TileMap;

namespace CasaEngine.Framework.Entities.Components;

public class TileMapLayer
{
    public List<Tile> Tiles { get; } = new();
    public TileMapLayerData TileMapLayerData { get; }

    public TileMapLayer(TileMapLayerData tileTileMapLayerData)
    {
        TileMapLayerData = tileTileMapLayerData;
    }
}