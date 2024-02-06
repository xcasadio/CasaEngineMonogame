using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement.Components;

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

    public AActor? Owner => _tileMapComponent.Owner;

    public PhysicsType PhysicsType { get; }

    public HashSet<Collision> Collisions { get; } = new();

    public void RemoveTile()
    {
        _tileMapComponent.RemoveTile(_layer, _x, _y);
    }

    public TileData GetTileData()
    {
        int tileId = _tileMapComponent.TileMapData.Layers[_layer].tiles[_x + _y * _tileMapComponent.TileMapData.MapSize.Width];
        var tileData = _tileMapComponent.TileSetData.GetTileData(tileId);
        return tileData;
    }
}