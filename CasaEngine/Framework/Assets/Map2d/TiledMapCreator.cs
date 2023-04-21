using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class TiledMapCreator
{
    public static void LoadMapFromFile(string fileName, World.World world, AssetContentManager assetContentManager)
    {
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var rootElement = jsonDocument.RootElement;

        var tiledMapData = new TiledMapData();
        tiledMapData.TileSetFileName = rootElement.GetJsonPropertyByName("tile_set_file_name").Value.GetString();
        tiledMapData.AutoTileSetFileName = rootElement.GetJsonPropertyByName("auto_tile_set_file_name").Value.GetString();
        tiledMapData.Coordinates.Load(rootElement.GetJsonPropertyByName("coordinates").Value);
        tiledMapData.MapSize = rootElement.GetJsonPropertyByName("map_size").Value.GetVector2();

        foreach (var jObject in rootElement.GetJsonPropertyByName("layers").Value.EnumerateArray())
        {
            var tiledMapLayerData = new TiledMapLayerData();
            tiledMapLayerData.type = jObject.GetJsonPropertyByName("type").Value.GetEnum<TileType>();
            tiledMapLayerData.zOffset = jObject.GetJsonPropertyByName("z_offset").Value.GetSingle();
            tiledMapLayerData.tiles = jObject.GetJsonPropertyByName("tiles").Value.Deserialize<List<int>>();

            tiledMapData.Layers.Add(tiledMapLayerData);
        }

        TileSetData tileSetData = new TileSetData();
        jsonDocument = JsonDocument.Parse(File.ReadAllText(tiledMapData.TileSetFileName));
        rootElement = jsonDocument.RootElement;

        tileSetData.SpriteSheetFileName = rootElement.GetJsonPropertyByName("sprite_sheet_file_name").Value.GetString();
        tileSetData.TileSize = rootElement.GetJsonPropertyByName("tile_size").Value.GetVector2();
        //TODO : create template with factory
        foreach (var jObject in rootElement.GetJsonPropertyByName("tiles").Value.EnumerateArray())
        {
            var type = jObject.GetJsonPropertyByName("type").Value.GetEnum<TileType>();

            switch (type)
            {
                case TileType.Static: { var tile = new StaticTileData(); tile.Load(jObject); tileSetData.Tiles.Add(tile); } break;
                case TileType.Animated: { var tile = new AnimatedTileData(); tile.Load(jObject); tileSetData.Tiles.Add(tile); } break;
                default: throw new Exception("TiledMapCreator.LoadMapFromFile() : TileSetData, tile type");
            }
        }


        //{
        //	var file = Game::Instance().GetMediaManager().FindMedia(tiledMapData.tileSetFileName, false);
        //	ofstream os(file.Fullname());
        //	json j;
        //	to_json(j, tileSetData);
        //	os << setw(4) << j << endl;
        //}

        AutoTileSetData autoTileSetData = new();

        jsonDocument = JsonDocument.Parse(File.ReadAllText(tiledMapData.AutoTileSetFileName));
        rootElement = jsonDocument.RootElement;
        autoTileSetData.SpriteSheetFileName = rootElement.GetJsonPropertyByName("sprite_sheet_file_name").Value.GetString();
        autoTileSetData.TileSize = rootElement.GetJsonPropertyByName("tile_size").Value.GetVector2();

        foreach (var jObject in rootElement.GetJsonPropertyByName("sets").Value.EnumerateArray())
        {
            var autoTileTileSetData = new AutoTileTileSetData();
            autoTileTileSetData.Id = jObject.GetJsonPropertyByName("id").Value.GetInt32();

            foreach (var jObjectTile in jObject.GetJsonPropertyByName("tiles").Value.EnumerateArray())
            {
                var type = jObjectTile.GetJsonPropertyByName("type").Value.GetEnum<TileType>();

                switch (type)
                {
                    case TileType.Static:
                        {
                            var tile = new StaticTileData();
                            tile.Load(jObjectTile);
                            autoTileTileSetData.Tiles.Add(tile);
                        }
                        break;
                    case TileType.Animated:
                        {
                            var tile = new AnimatedTileData();
                            tile.Load(jObjectTile);
                            autoTileTileSetData.Tiles.Add(tile);
                        }
                        break;
                    default: throw new Exception("TiledMapCreator.LoadMapFromFile() : AutoTileTileSetData, tile type");
                }
            }

            autoTileSetData.Sets.Add(autoTileTileSetData);
        }

        //{
        //	var file = Game::Instance().GetMediaManager().FindMedia(tiledMapData.autoTileSetFileName, false);
        //	ofstream os(file.Fullname());
        //	json j;
        //	to_json(j, autoTileSetData);
        //	os << setw(4) << j << endl;
        //}

        Create(Path.GetFileNameWithoutExtension(fileName), autoTileSetData, tileSetData, tiledMapData, world, assetContentManager);
    }

    public static void Create(string prefixName, AutoTileSetData autoTileSetData, TileSetData tileSetData, TiledMapData tiledMapData, World.World world, AssetContentManager assetContentManager)
    {
        var mapEntity = new Entity();
        mapEntity.Name = "map_" + prefixName;
        mapEntity.Coordinates.LocalPosition = tiledMapData.Coordinates.LocalPosition;
        mapEntity.Coordinates.LocalRotation = tiledMapData.Coordinates.LocalRotation;
        mapEntity.Coordinates.LocalScale = tiledMapData.Coordinates.LocalScale;
        mapEntity.Coordinates.LocalCenterOfRotation = tiledMapData.Coordinates.LocalCenterOfRotation;
        // TODO : Add tile map component : does nothing, here only for saving purpose
        //mapEntity.IsPersistent(false);
        world.AddEntityImmediately(mapEntity);

        var physics_world = GameInfo.Instance.CurrentWorld.Physic2dWorld;
        var layerIndex = 0;

        foreach (var layer in tiledMapData.Layers)
        {
            var layerEntity = new Entity();
            layerEntity.Name = "layer_" + layerIndex + "_" + prefixName;
            var layerPos = layerEntity.Coordinates.LocalPosition;
            layerEntity.Coordinates.LocalPosition = new Vector3(layerPos.X, layerPos.Y, layerPos.Z + layer.zOffset);
#if EDITOR
            layerEntity.IsTemporary = true;
#endif
            layerEntity.Coordinates.Parent = mapEntity.Coordinates;
            world.AddEntityImmediately(layerEntity);
            int index = 0;

            foreach (var tileId in layer.tiles)
            {
                int x = index % (int)tiledMapData.MapSize.X;
                int y = index / (int)tiledMapData.MapSize.X;
                int posX = x * (int)tileSetData.TileSize.X;
                int posY = y * (int)tileSetData.TileSize.Y;

                var tileEntity = new Entity();
                tileEntity.Name = "tile_" + index + "_" + layerIndex + "_" + prefixName;
#if EDITOR
                tileEntity.IsTemporary = true;
#endif
                tileEntity.Coordinates.Parent = layerEntity.Coordinates;
                tileEntity.Coordinates.LocalPosition = new Vector3(posX, posY, 0.0f);
                Tile? tile;
                TileCollisionType collisionType = TileCollisionType.None;
                if (tileId == -1)
                {
                    tile = new EmptyTile();
                }
                else
                {
                    if (layer.type == TileType.Auto)
                    {
                        var autoTileTileSetData = autoTileSetData.GetTileSetById(tileId);
                        tile = CreateTile(autoTileTileSetData, tileSetData, layer, tiledMapData, x, y, assetContentManager);
                        collisionType = autoTileTileSetData.Tiles[0].CollisionType;
                    }
                    else
                    {
                        TileData? tileData = tileSetData.GetTileById(tileId);
                        tile = CreateTile(tileData, assetContentManager);
                        collisionType = tileData.CollisionType;

                        if (tileData == null)
                        {
                            LogManager.Instance.WriteLineWarning($"TileData with Id {tileId} is null");
                            continue;
                        }
                    }
                }

                //if (collisionType != TileCollisionType.None)
                //{
                //    var shape = new Rectangle(0, 0, (int)tileSetData.TileSize.X, (int)tileSetData.TileSize.Y);
                //    var position = new Vector3(posX + tileSetData.TileSize.X / 2.0f, posY + tileSetData.TileSize.Y / 2.0f, 0.0f);
                //    ICollisionObjectContainer* collisionShape = null;
                //    if (collisionType == TileCollisionType.Blocked)
                //    {
                //        collisionShape = physics_world.CreateCollisionShape(tileEntity, shape, position, CollisionHitType.Defense, CollisionFlags.Static);
                //    }
                //    else
                //    {
                //        collisionShape = physics_world.CreateSensor(tileEntity, shape, position, CollisionHitType.Defense);
                //    }
                //
                //    physics_world.AddCollisionObject(collisionShape);
                //}

                var tileComponent = new TileComponent(tileEntity);
                tileComponent.Tile = tile;
                tileEntity.ComponentManager.Components.Add(tileComponent);
                world.AddEntityImmediately(tileEntity);
                index++;
            }

            layerIndex++;
        }
    }

    private static Tile CreateTile(TileData tileData, AssetContentManager assetContentManager)
    {
        switch (tileData.Type)
        {
            case TileType.Static:
                {
                    var staticTileParams = tileData as StaticTileData;
                    SpriteData spriteData = assetContentManager.GetAsset<SpriteData>(staticTileParams.SpriteId);
                    return new StaticTile(Sprite.Create(spriteData, assetContentManager), staticTileParams);
                }
            case TileType.Animated:
                {
                    var animatedTileParams = tileData as AnimatedTileData;
                    Animation2dData animation = assetContentManager.GetAsset<Animation2dData>(animatedTileParams.Animation2dId);
                    return new AnimatedTile(new Animation2d(animation), animatedTileParams);
                }
        }

        LogManager.Instance.WriteLineError($"The type of tile {tileData.Type} is not supported");

        return null;
    }

    private static Tile CreateTile(AutoTileTileSetData autoTileTileSetData, TileSetData tileSetData, TiledMapLayerData layer, TiledMapData map, int x, int y, AssetContentManager assetContentManager)
    {
        List<Tile> tiles = new List<Tile>();

        foreach (var tileData in autoTileTileSetData.Tiles)
        {
            tiles.Add(CreateTile(tileData, assetContentManager));
        }

        var autoTile = new AutoTile(autoTileTileSetData.Tiles[0]);
        autoTile.SetTileInfo(tiles, tileSetData.TileSize, map.MapSize, layer, x, y);
        return autoTile;
    }
}