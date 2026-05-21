namespace CasaEngine.Framework.Assets.TileMap;

public readonly struct TileMapTileReference
{
    public static TileMapTileReference Empty => new(0, TileMapData.EmptyTileId);

    public TileMapTileReference(int tileSetIndex, int tileId)
    {
        TileSetIndex = tileSetIndex;
        TileId = tileId;
    }

    public int TileSetIndex { get; }
    public int TileId { get; }
    public bool IsEmpty => TileId == TileMapData.EmptyTileId;
}