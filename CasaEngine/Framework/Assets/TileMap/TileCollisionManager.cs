using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Physics;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileCollisionManager : ICollideableComponent
{
    private readonly TileMapComponent _tileMapComponent;
    private readonly int _layer;
    private readonly int _x;
    private readonly int _y;

    public TileCollisionManager(TileMapComponent tileMapComponent, int layer, int x, int y)
    {
        _tileMapComponent = tileMapComponent;
        _layer = layer;
        _x = x;
        _y = y;
    }

    public Entity Owner => _tileMapComponent.Owner;

    public PhysicsType PhysicsType { get; }

    public HashSet<Collision> Collisions { get; } = new();

    /// <summary>
    /// False once the physics body backing this manager has been removed (chunk rebuild, tile removal, ...).
    /// A detached manager still exists in flight (e.g. queued in a collision-dispatch batch) but no longer
    /// owns a live cell: <see cref="GetTileData"/> answers null instead of touching stale/rebuilt map data.
    /// </summary>
    public bool IsAttached { get; private set; } = true;

    /// <summary>
    /// Marks this manager as no longer backed by a physics body. Called by <see cref="TileMapComponent"/>
    /// when the body is removed (chunk rebuild, tile replacement, component teardown).
    /// </summary>
    internal void Detach()
    {
        IsAttached = false;
    }

    public void RemoveTile()
    {
        if (!IsAttached)
        {
            return;
        }

        var tileId = _tileMapComponent.TileMapData.Layers[_layer].tiles[_x + _y * _tileMapComponent.TileMapData.MapSize.Width];
        if (tileId == TileMapData.EmptyTileId)
        {
            return;
        }

        _tileMapComponent.RemoveTile(_layer, _x, _y);
    }

    /// <summary>
    /// Returns the tile data for the cell this manager was created for, or null when the manager is
    /// detached, the cell is empty, or the tile id is unknown to the tileset. Never throws.
    /// </summary>
    public TileData GetTileData()
    {
        if (!IsAttached)
        {
            return null;
        }

        var tileId = _tileMapComponent.TileMapData.Layers[_layer].tiles[_x + _y * _tileMapComponent.TileMapData.MapSize.Width];
        if (tileId == TileMapData.EmptyTileId)
        {
            return null;
        }

        _tileMapComponent.TileSetData.TryGetTileData(tileId, out var tileData);
        return tileData;
    }
}
