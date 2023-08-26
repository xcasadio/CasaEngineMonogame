using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Size = CasaEngine.Core.Maths.Size;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Tiled Map")]
public class TileMapComponent : Component, IBoundingBoxComputable, ICollideableComponent
{
    public static readonly int ComponentId = (int)ComponentIds.TileMap;

    public TileMapData TileMapData { get; set; }
    private List<TileMapLayer> Layers { get; } = new();

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

    public TileMapComponent(Entity entity) : base(entity, ComponentId)
    {
        //Do nothing
    }

    public override void Initialize(CasaEngineGame game)
    {
#if EDITOR
        if (TileMapData == null)
        {
            return;
        }
#endif

        var texture = new Texture(TileMapData.TileSetData.SpriteSheetFileName, game.GameManager.AssetContentManager);
        var tileSize = TileMapData.TileSetData.TileSize;

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
                    Tile? tile = null;

                    if (tileId == -1)
                    {
                        tile = new EmptyTile();
                    }
                    else
                    {
                        var tileData = TileMapData.TileSetData.GetTileData(tileId + (tileId < 2000 ? 2906 : 0));

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
                            case TileCollisionType.Blocked:
                                var physicsEngineComponent = game.GetGameComponent<PhysicsEngineComponent>();
                                var worldMatrix = Owner.Coordinates.WorldMatrix;
                                worldMatrix.Translation += new Vector3(
                                    x * tileSize.Width + tileSize.Width / 2f,
                                    -y * tileSize.Height - tileSize.Height / 2f,
                                    0f);
                                var rectangle = new ShapeRectangle(0, 0, tileSize.Width,
                                    tileSize.Height);
                                var tileCollisionManager = new TileCollisionManager(this, layerIndex, x, y);
                                var rigidBody = physicsEngineComponent.AddStaticObject(rectangle, ref worldMatrix, tileCollisionManager,
                                    new PhysicsDefinition { Friction = 0f });
                                break;
                        }
                    }

                    tile.Initialize(game);
                    tileMapLayer.Tiles.Add(tile);
                }
            }
        }
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
        if (TileMapData == null)
        {
            return;
        }
#endif

        var translation = Owner.Coordinates.WorldMatrix.Translation;
        var scale = new Vector2(Owner.Coordinates.Scale.X, Owner.Coordinates.Scale.Y);
        var mapWidth = TileMapData.MapSize.Width;
        var mapHeight = TileMapData.MapSize.Height;
        var tileWidth = TileMapData.TileSetData.TileSize.Width * Owner.Coordinates.Scale.X;
        var tileHeight = TileMapData.TileSetData.TileSize.Height * Owner.Coordinates.Scale.Y;
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
        Layers[layer].Tiles[x + y * TileMapData.MapSize.Width] = new EmptyTile();
    }

    public override Component Clone(Entity owner)
    {
        var component = new TileMapComponent(owner);

        component.Layers.AddRange(Layers);
        component.TileMapData = TileMapData;
#if EDITOR
        component.TileMapDataFileName = TileMapDataFileName;
#endif

        return component;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        var fileName = element.GetProperty("tileMapDataFileName").GetString();
        TileMapData = TileMapLoader.LoadMapFromFile(fileName);

#if EDITOR
        TileMapDataFileName = fileName;
#endif
    }

#if EDITOR
    public string TileMapDataFileName { get; set; }

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        jObject.Add("tileMapDataFileName", TileMapDataFileName);
    }


    public BoundingBox BoundingBox
    {
        get
        {
            var min = Vector3.One * int.MaxValue;
            var max = Vector3.One * int.MinValue;

            if (TileMapData != null)
            {
                min = Vector3.Min(min, new Vector3(0, 0, TileMapData.Layers.Min(x => x.zOffset)));
                max = Vector3.Max(max, new Vector3(
                    TileMapData.MapSize.Width * TileMapData.TileSetData.TileSize.Width,
                    -TileMapData.MapSize.Height * TileMapData.TileSetData.TileSize.Height,
                    TileMapData.Layers.Max(x => x.zOffset)));
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