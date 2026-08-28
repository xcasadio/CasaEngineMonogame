namespace CasaEngine.Framework.Assets.TileMap;

public readonly struct TileMapTileReference : IEquatable<TileMapTileReference>
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

    // Value equality is required so this struct can key a Dictionary without boxing (see
    // TileMapComponent's sorted overlay tile cache): without IEquatable<T>, EqualityComparer<T>.Default
    // falls back to the reflection-based ValueType comparer, which allocates on every lookup — not
    // acceptable on a path exercised every frame.
    public bool Equals(TileMapTileReference other)
    {
        return TileSetIndex == other.TileSetIndex && TileId == other.TileId;
    }

    public override bool Equals(object obj)
    {
        return obj is TileMapTileReference other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TileSetIndex, TileId);
    }
}