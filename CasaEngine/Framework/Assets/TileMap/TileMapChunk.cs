using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.TileMap;

public sealed class TileMapChunk
{
    public TileMapChunk(int layerIndex, Point chunkIndex, Rectangle tileBounds)
    {
        if (tileBounds.Width <= 0 || tileBounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileBounds), "TileMap chunk bounds must be positive.");
        }

        LayerIndex = layerIndex;
        ChunkIndex = chunkIndex;
        TileBounds = tileBounds;
        DirtyVisual = true;
        DirtyCollision = true;
    }

    public int LayerIndex { get; }
    public Point ChunkIndex { get; }
    public Rectangle TileBounds { get; }
    public BoundingBox WorldBounds { get; private set; }
    public bool DirtyVisual { get; private set; }
    public bool DirtyCollision { get; private set; }
    public bool ContainsAnimatedTiles { get; set; }

    public bool IntersectsTileRange(int minTileX, int maxTileX, int minTileY, int maxTileY)
    {
        if (minTileX > maxTileX || minTileY > maxTileY)
        {
            return false;
        }

        return TileBounds.Left <= maxTileX
            && TileBounds.Right - 1 >= minTileX
            && TileBounds.Top <= maxTileY
            && TileBounds.Bottom - 1 >= minTileY;
    }

    public void UpdateWorldBounds(float mapPosX, float mapPosY, float tileWidth, float tileHeight, float minZ, float maxZ)
    {
        var left = mapPosX + tileWidth * TileBounds.Left;
        var right = mapPosX + tileWidth * TileBounds.Right;
        var top = mapPosY - tileHeight * TileBounds.Top;
        var bottom = mapPosY - tileHeight * TileBounds.Bottom;

        WorldBounds = new BoundingBox(
            new Vector3(Math.Min(left, right), Math.Min(top, bottom), Math.Min(minZ, maxZ)),
            new Vector3(Math.Max(left, right), Math.Max(top, bottom), Math.Max(minZ, maxZ)));
    }

    public void MarkDirty(bool visual = true, bool collision = true)
    {
        DirtyVisual |= visual;
        DirtyCollision |= collision;
    }

    public void MarkVisualClean()
    {
        DirtyVisual = false;
    }

    public void MarkCollisionClean()
    {
        DirtyCollision = false;
    }
}