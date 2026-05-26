using CasaEngine.Engine.Physics;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapLayer
{
    public List<Tile> Tiles { get; } = new();
    public List<PhysicsBody> CollisionObjects { get; } = new();
    public List<TileMapChunk> Chunks { get; } = new();
    public List<TileMapCollisionChunk> CollisionChunks { get; } = new();
    public TileMapLayerData TileMapLayerData { get; }
    public int ChunkColumns { get; set; }
    public int ChunkRows { get; set; }

    public TileMapLayer(TileMapLayerData tileTileMapLayerData)
    {
        TileMapLayerData = tileTileMapLayerData;
    }
}