namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapLayer
{
    public List<Tile> Tiles { get; } = new();
    public List<BulletSharp.CollisionObject?> CollisionObjects { get; } = new();
    public TileMapLayerData TileMapLayerData { get; }

    public TileMapLayer(TileMapLayerData tileTileMapLayerData)
    {
        TileMapLayerData = tileTileMapLayerData;
    }
}