using System.ComponentModel;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Tile Map")]
public class TileMapComponent : SceneComponent, ICollideableComponent
{
    public Guid TileMapDataAssetId { get; set; } = Guid.Empty;
    public TileMapData TileMapData { get; set; }
    public TileSetData TileSetData { get; set; }
    private List<TileMapLayer> Layers { get; } = new();
    public PhysicsType PhysicsType { get; }
    public HashSet<Collision> Collisions { get; } = new();

    public TileMapComponent()
    {
        //Do nothing
    }

    public TileMapComponent(TileMapComponent other) : base(other)
    {
        Layers.AddRange(other.Layers);
        TileMapData = other.TileMapData;
        TileMapDataAssetId = other.TileMapDataAssetId;
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        Layers.Clear();

        if (TileMapDataAssetId != Guid.Empty)
        {
            TileMapData = Owner.World.Game.AssetContentManager.Load<TileMapData>(TileMapDataAssetId);
        }

        if (TileMapData == null)
        {
            return;
        }

        TileSetData = Owner.World.Game.AssetContentManager.Load<TileSetData>(TileMapData.TileSetDataAssetId);
        var tileSize = TileSetData.TileSize;

        var texture = Owner.World.Game.AssetContentManager.Load<Texture>(TileSetData.SpriteSheetAssetId);
        texture.Load(Owner.World.Game.AssetContentManager);

        for (var layerIndex = 0; layerIndex < TileMapData.Layers.Count; layerIndex++)
        {
            var tileMapLayerData = TileMapData.Layers[layerIndex];
            var tileMapLayer = new TileMapLayer(tileMapLayerData);
            Layers.Add(tileMapLayer);
            var mapWidth = TileMapData.MapSize.Width;
            var mapHeight = TileMapData.MapSize.Height;

            for (var y = 0; y < mapHeight; y++)
            {
                for (var x = 0; x < mapWidth; x++)
                {
                    var tileId = tileMapLayerData.tiles[x + y * mapWidth];
                    Tile? tile;

                    if (tileId == -1)
                    {
                        tile = new EmptyTile();
                    }
                    else
                    {
                        var tileData = TileSetData.GetTileData(tileId);

                        switch (tileData.Type)
                        {
                            case TileType.Auto:
                                {
                                    AutoTileData autoTileData = tileData as AutoTileData;
                                    var autoTile = new AutoTile(texture.Resource, autoTileData);
                                    autoTile.SetTileInfo(tileSize, TileMapData.MapSize, tileMapLayerData, x, y);
                                    tile = autoTile;
                                    break;
                                }
                            case TileType.Static:
                                {
                                    tile = new StaticTile(texture.Resource, tileData as StaticTileData);
                                    break;
                                }
                            //case TileType.Animated:
                            //    {
                            //        var animatedTileParams = tileData as AnimatedTileData;
                            //        var animation = assetContentManager.GetAsset<Animation2dData>(animatedTileParams.Animation2dId);
                            //        return new AnimatedTile(new Animation2d(animation), animatedTileParams);
                            //    }
                            default:
                                throw new ArgumentException($"tile type not supported {tileData.Type}");
                        }

                        switch (tileData.CollisionType)
                        {
                            case TileCollisionType.NoContactResponse:
                            case TileCollisionType.Blocked:
                                var physicsEngineComponent = Owner.World.Game.GetGameComponent<PhysicsEngineComponent>();
                                var worldMatrix = WorldMatrixNoScale;
                                worldMatrix.Translation += new Vector3(
                                    x * tileSize.Width + tileSize.Width / 2f,
                                    -y * tileSize.Height - tileSize.Height / 2f,
                                    0f);
                                var rectangle = new ShapeRectangle(0, 0, tileSize.Width,
                                    tileSize.Height);
                                var tileCollisionManager = new TileCollisionManager(this, layerIndex, x, y);
                                if (tileData.CollisionType == TileCollisionType.NoContactResponse)
                                {
                                    var collisionObject = physicsEngineComponent.AddGhostObject(rectangle, LocalScale, ref worldMatrix, tileCollisionManager);
                                }
                                else
                                {
                                    var rigidBody = physicsEngineComponent.AddStaticObject(rectangle, LocalScale, ref worldMatrix, tileCollisionManager,
                                        new PhysicsDefinition { Friction = 0f });
                                }

                                break;
                        }
                    }

                    tile.Initialize(Owner.World.Game);
                    tileMapLayer.Tiles.Add(tile);
                }
            }
        }

        IsBoundingBoxDirty = true;
    }

    public override TileMapComponent Clone()
    {
        return new TileMapComponent(this);
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

        base.Update(elapsedTime);
    }

    public override BoundingBox GetBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        if (TileMapData != null)
        {
            min = Vector3.Min(min, new Vector3(0, 0, TileMapData.Layers.Min(x => x.zOffset)));
            max = Vector3.Max(max, new Vector3(
                TileMapData.MapSize.Width * TileSetData.TileSize.Width,
                -TileMapData.MapSize.Height * TileSetData.TileSize.Height,
                TileMapData.Layers.Max(x => x.zOffset)));
        }
        else // default box
        {
            const float length = 0.5f;
            min = Vector3.One * -length;
            max = Vector3.One * length;
        }

        min = Vector3.Transform(min, WorldMatrixWithScale);
        max = Vector3.Transform(max, WorldMatrixWithScale);

        return new BoundingBox(min, max);
    }

    public override void Draw(float elapsedTime)
    {
        if (TileMapData == null)
        {
            return;
        }

        var translation = Position;
        var scale = Scale.ToVector2();//new Vector2(Owner.Coordinates.Scale.X, Owner.Coordinates.Scale.Y);

        var mapWidth = TileMapData.MapSize.Width;
        var mapHeight = TileMapData.MapSize.Height;
        var tileWidth = TileSetData.TileSize.Width * scale.X;
        var tileHeight = TileSetData.TileSize.Height * scale.Y;
        var mapPosX = translation.X;
        var mapPosY = translation.Y;

        foreach (var layer in Layers)
        {
            var layerZ = layer.TileMapLayerData.zOffset;

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
        var tile = Layers[layer].Tiles[x + y * TileMapData.MapSize.Width];

        //TODO : remove the physics and other stuff

        Layers[layer].Tiles[x + y * TileMapData.MapSize.Width] = new EmptyTile();
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        TileMapDataAssetId = element["tile_map_data_asset_id"].GetGuid();
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject.Add("tile_map_data_asset_id", TileMapDataAssetId);
    }

#endif
}