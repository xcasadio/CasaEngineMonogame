using CasaEngine.Framework.Assets.Map2d;

namespace CasaEngine.Framework.Entities.Components;

public class TiledMapLayer
{
    public List<Tile> Tiles { get; } = new();
    public TiledMapLayerData TiledMapLayerData { get; }

    public TiledMapLayer(TiledMapLayerData tiledTiledMapLayerData)
    {
        TiledMapLayerData = tiledTiledMapLayerData;
    }
}