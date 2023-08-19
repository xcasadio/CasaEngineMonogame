using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Tiled Map")]
public class TiledMapComponent : Component, IBoundingBoxComputable, ICollideableComponent
{
    public static readonly int ComponentId = (int)ComponentIds.TiledMap;

    public TiledMapData TiledMapData { get; set; }
    private List<TiledMapLayer> Layers { get; } = new();

    [Browsable(false)]
    public PhysicsType PhysicsType { get; }

    [Browsable(false)]
    public HashSet<Collision> Collisions { get; } = new();

    public void OnHit(Collision collision)
    {
        Owner.Hit(collision, this);
    }

    public void OnHitEnded(Collision collision)
    {
        Owner.HitEnded(collision, this);
    }

    public TiledMapComponent(Entity entity) : base(entity, ComponentId)
    {
        //Do nothing
    }

    public override void Initialize(CasaEngineGame game)
    {
#if EDITOR
        if (TiledMapData == null)
        {
            return;
        }
#endif

        foreach (var spriteSheetFileName in TiledMapData.SpriteSheetFileNames)
        {
            var fileName = Path.Combine(GameSettings.ProjectSettings.ProjectPath, spriteSheetFileName);
            SpriteLoader.LoadFromFile(fileName, game.GameManager.AssetContentManager, SaveOption.Editor);
        }

        for (var layerIndex = 0; layerIndex < TiledMapData.Layers.Count; layerIndex++)
        {
            var tiledMapLayerData = TiledMapData.Layers[layerIndex];
            var tiledMapLayer = new TiledMapLayer(tiledMapLayerData);
            Layers.Add(tiledMapLayer);
            var mapWidth = TiledMapData.MapSize.Width;
            var mapHeight = TiledMapData.MapSize.Height;

            for (var y = 0; y < mapHeight; y++)
            {
                for (var x = 0; x < mapWidth; x++)
                {
                    var tileId = tiledMapLayerData.tiles[x + y * mapWidth];
                    Tile? tile = null;

                    if (tileId == -1)
                    {
                        tile = new EmptyTile();
                    }
                    else
                    {
                        var tileData = TiledMapData.TileDefinitions.Single(x => x.Id == tileId);

                        if (tileData.Type == TileType.Auto)
                        {
                            var autoTiles = GetAutoTiles(tileData as AutoTileData);
                            tile = TiledMapLoader.CreateAutoTile(x, y, autoTiles, tiledMapLayerData,
                                TiledMapData.MapSize, TiledMapData.TileSize, game.GameManager.AssetContentManager);
                        }
                        else
                        {
                            tile = TiledMapLoader.CreateTile(tileData, game.GameManager.AssetContentManager);
                        }

                        switch (tileData.CollisionType)
                        {
                            case TileCollisionType.Blocked:
                                var physicsEngineComponent = game.GetGameComponent<PhysicsEngineComponent>();
                                var worldMatrix = Owner.Coordinates.WorldMatrix;
                                worldMatrix.Translation += new Vector3(
                                    x * TiledMapData.TileSize.Width + TiledMapData.TileSize.Width / 2f,
                                    -y * TiledMapData.TileSize.Height - TiledMapData.TileSize.Height / 2f,
                                    0f);
                                var rectangle = new ShapeRectangle(0, 0, TiledMapData.TileSize.Width,
                                    TiledMapData.TileSize.Height);
                                var tileCollisionManager = new TileCollisionManager(this, layerIndex, x, y);
                                var rigidBody = physicsEngineComponent.AddStaticObject(rectangle, ref worldMatrix, tileCollisionManager,
                                    new PhysicsDefinition { Friction = 0f });
                                break;
                        }
                    }

                    tile.Initialize(game);
                    tiledMapLayer.Tiles.Add(tile);
                }
            }
        }
    }

    private List<TileData>? GetAutoTiles(AutoTileData? autoTileData)
    {
        foreach (var autoTileSetData in TiledMapData.AutoTileSetDatas)
        {
            foreach (var autoTileTileSetData in autoTileSetData.Sets)
            {
                if (autoTileData.AutoTileSetId == autoTileTileSetData.Id)
                {
                    return autoTileTileSetData.Tiles;
                }
            }
        }

        return null;
    }

    public override void Update(float elapsedTime)
    {
        foreach (var layer in Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                tile.Update(elapsedTime);
            }
        }
    }

    public override void Draw()
    {
#if EDITOR
        if (TiledMapData == null)
        {
            return;
        }
#endif

        var translation = Owner.Coordinates.WorldMatrix.Translation;
        var scale = new Vector2(Owner.Coordinates.Scale.X, Owner.Coordinates.Scale.Y);
        var mapWidth = TiledMapData.MapSize.Width;
        var mapHeight = TiledMapData.MapSize.Height;
        var tileWidth = TiledMapData.TileSize.Width * Owner.Coordinates.Scale.X;
        var tileHeight = TiledMapData.TileSize.Height * Owner.Coordinates.Scale.Y;
        var mapPosX = translation.X;
        var mapPosY = translation.Y;

        foreach (var layer in Layers)
        {
            var layerZ = layer.TiledMapLayerData.zOffset;

            for (var y = 0; y < mapHeight; y++)
            {
                for (var x = 0; x < mapWidth; x++)
                {
                    layer.Tiles[x + y * mapWidth].Draw(mapPosX + tileWidth * x, mapPosY - tileHeight * y, translation.Z + layerZ, scale);
                }
            }
        }
    }

    public void RemoveTile(int layer, int x, int y)
    {
        Layers[layer].Tiles[x + y * TiledMapData.MapSize.Width] = new EmptyTile();
    }

    public override Component Clone(Entity owner)
    {
        var component = new TiledMapComponent(owner);

        component.Layers.AddRange(Layers);
        component.TiledMapData = TiledMapData;
#if EDITOR
        component.TiledMapDataFileName = TiledMapDataFileName;
#endif

        return component;
    }

    public override void Load(JsonElement element)
    {
        var fileName = element.GetProperty("tiledMapDataFileName").GetString();
        TiledMapData = TiledMapLoader.LoadMapFromFile(fileName);

#if EDITOR
        TiledMapDataFileName = fileName;
#endif
    }

#if EDITOR
    public string TiledMapDataFileName { get; set; }

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        jObject.Add("tiledMapDataFileName", TiledMapDataFileName);
    }


    public BoundingBox BoundingBox
    {
        get
        {
            var min = Vector3.One * int.MaxValue;
            var max = Vector3.One * int.MinValue;

            if (TiledMapData != null)
            {
                min = Vector3.Min(min, new Vector3(0, 0, TiledMapData.Layers.Min(x => x.zOffset)));
                max = Vector3.Max(max, new Vector3(
                    TiledMapData.MapSize.Width * TiledMapData.TileSize.Width,
                    -TiledMapData.MapSize.Height * TiledMapData.TileSize.Height,
                    TiledMapData.Layers.Max(x => x.zOffset)));
            }
            else // default box
            {
                const float length = 0.5f;
                min = Vector3.One * -length;
                max = Vector3.One * length;
            }

            min = Vector3.Transform(min, Owner.Coordinates.WorldMatrix);
            max = Vector3.Transform(max, Owner.Coordinates.WorldMatrix);

            return new BoundingBox(min, max);
        }
    }

#endif
}


public class TileCollisionManager : ICollideableComponent
{
    private readonly TiledMapComponent _tiledMapComponent;
    private readonly int _layer;
    private readonly int _x;
    private readonly int _y;

    public TileCollisionManager(TiledMapComponent tiledMapComponent, int layer, int x, int y)
    {
        _tiledMapComponent = tiledMapComponent;
        _layer = layer;
        _x = x;
        _y = y;
    }

    public Entity Owner => _tiledMapComponent.Owner;

    public PhysicsType PhysicsType { get; }

    public HashSet<Collision> Collisions { get; } = new();

    public void OnHit(Collision collision)
    {
        Owner.Hit(collision, _tiledMapComponent);
    }

    public void OnHitEnded(Collision collision)
    {
        Owner.HitEnded(collision, _tiledMapComponent);
    }

    public void RemoveTile()
    {
        _tiledMapComponent.RemoveTile(_layer, _x, _y);
    }
}
