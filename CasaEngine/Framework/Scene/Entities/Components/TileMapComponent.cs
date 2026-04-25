using System.ComponentModel;
using BulletSharp;

using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Tile Map")]
public class TileMapComponent : SceneComponent, ICollideableComponent, IConditionalEntityUpdateSource
{
    private List<CollisionObject> _collisionObjects = new();
    private List<TileMapLayer> Layers { get; } = new();
    private bool _hasAnimatedTiles;
    private bool _needsAutoTileRefresh;

    public Guid TileMapDataAssetId { get; set; } = Guid.Empty;
    public TileMapData TileMapData { get; set; }
    public TileSetData TileSetData { get; set; }
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

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        Layers.Clear();
        _hasAnimatedTiles = false;
        _needsAutoTileRefresh = false;

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
                                    _needsAutoTileRefresh = true;
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
                                var physicsWorldContext = Owner.World.PhysicsWorldContext;
                                var worldMatrix = WorldMatrixNoScale;
                                worldMatrix.Translation += new Vector3(
                                    x * tileSize.Width + tileSize.Width / 2f,
                                    -y * tileSize.Height - tileSize.Height / 2f,
                                    0f);
                                var box = new BoxShape(tileSize.Width / 2f, tileSize.Height / 2f, 0.5f);
                                box.LocalScaling = LocalScale;
                                box.UserObject = this;
                                var tileCollisionManager = new TileCollisionManager(this, layerIndex, x, y);
                                if (tileData.CollisionType == TileCollisionType.NoContactResponse)
                                {
                                    var collisionObject = physicsWorldContext.AddGhostObject(box, ref worldMatrix, tileCollisionManager);
                                    _collisionObjects.Add(collisionObject);
                                }
                                else
                                {
                                    var rigidBody = physicsWorldContext.AddStaticObject(box, LocalScale, ref worldMatrix, tileCollisionManager,
                                        new PhysicsDefinition { Friction = 0f });
                                    _collisionObjects.Add(rigidBody);
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

        if (_needsAutoTileRefresh)
        {
            Owner?.Policies.RequestConditionalUpdate();
        }
    }

    public override TileMapComponent Clone()
    {
        return new TileMapComponent(this);
    }

    public bool ShouldUpdateWhenConditional(Entity owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return _hasAnimatedTiles || _needsAutoTileRefresh;
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

        _needsAutoTileRefresh = false;

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

        return new BoundingBox(min, max).Transform(WorldMatrixWithScale);
    }

    public override void Draw(float elapsedTime)
    {
        if (TileMapData == null)
        {
            return;
        }

        var translation = Position;
        var scale = Scale.ToVector2();

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
        _needsAutoTileRefresh = true;
        Owner?.Policies.RequestConditionalUpdate();
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        TileMapDataAssetId = element["tile_map_data_asset_id"].GetGuid();
    }

}