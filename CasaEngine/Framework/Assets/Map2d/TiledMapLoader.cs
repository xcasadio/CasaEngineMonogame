using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using Size = CasaEngine.Core.Maths.Size;

namespace CasaEngine.Framework.Assets.Map2d;

public class TiledMapLoader
{
    public static TiledMapData LoadMapFromFile(string fileName)
    {
        fileName = Path.Combine(GameSettings.ProjectSettings.ProjectPath, fileName);
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var rootElement = jsonDocument.RootElement;
        var tiledMapData = new TiledMapData();
        tiledMapData.MapSize = rootElement.GetJsonPropertyByName("map_size").Value.GetSize();
        tiledMapData.TileSize = rootElement.GetJsonPropertyByName("tile_size").Value.GetSize();

        foreach (var jObject in rootElement.GetJsonPropertyByName("auto_tile_set_file_names").Value.EnumerateArray())
        {
            tiledMapData.AutoTileSetFileNames.Add(jObject.GetString());
        }

        foreach (var jObject in rootElement.GetJsonPropertyByName("sprite_sheet_file_names").Value.EnumerateArray())
        {
            tiledMapData.SpriteSheetFileNames.Add(jObject.GetString());
        }

        foreach (var jObject in rootElement.GetJsonPropertyByName("tilesDefinition").Value.EnumerateArray())
        {
            var type = jObject.GetJsonPropertyByName("type").Value.GetEnum<TileType>();
            TileData tileData;
            switch (type)
            {
                case TileType.Static:
                    tileData = new StaticTileData();
                    break;
                case TileType.Animated:
                    tileData = new AnimatedTileData();
                    break;
                case TileType.Auto:
                    tileData = new AutoTileData();
                    break;
                default: throw new Exception("TiledMapCreator.LoadMapFromFile() : TileSetData, tile type");
            }

            tileData.Load(jObject);
            tiledMapData.TileDefinitions.Add(tileData);
        }

        foreach (var jObject in rootElement.GetJsonPropertyByName("layers").Value.EnumerateArray())
        {
            tiledMapData.Layers.Add(LoadTiledMapLayerData(jObject));
        }

        foreach (var autoTileSetFileName in tiledMapData.AutoTileSetFileNames)
        {
            var autoTileSetData = LoadAutoTileSetData(autoTileSetFileName);
            tiledMapData.AutoTileSetDatas.Add(autoTileSetData);
        }

        return tiledMapData;
    }

    private static TiledMapLayerData LoadTiledMapLayerData(JsonElement jObject)
    {
        var tiledMapLayerData = new TiledMapLayerData();
        tiledMapLayerData.type = jObject.GetJsonPropertyByName("type").Value.GetEnum<TileType>();
        tiledMapLayerData.zOffset = jObject.GetJsonPropertyByName("z_offset").Value.GetSingle();
        tiledMapLayerData.tiles = jObject.GetJsonPropertyByName("tiles").Value.Deserialize<List<int>>();
        return tiledMapLayerData;
    }

    private static AutoTileSetData LoadAutoTileSetData(string fileName)
    {
        AutoTileSetData autoTileSetData = new();

        var jsonDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(GameSettings.ProjectSettings.ProjectPath, fileName)));
        var rootElement = jsonDocument.RootElement;
        autoTileSetData.SpriteSheetFileName = rootElement.GetJsonPropertyByName("sprite_sheet_file_name").Value.GetString();

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

        return autoTileSetData;
    }

    public static void Create(TiledMapData tiledMapData, AssetContentManager assetContentManager)
    {
        var layerIndex = 0;

        foreach (var layer in tiledMapData.Layers)
        {
            var index = 0;

            foreach (var tileId in layer.tiles)
            {
                var x = index % tiledMapData.MapSize.Width;
                var y = index / tiledMapData.MapSize.Width;
                var posX = x * tiledMapData.TileSize.Width;
                var posY = y * tiledMapData.TileSize.Height;

                Tile? tile;
                var collisionType = TileCollisionType.None;

                if (tileId == -1)
                {
                    tile = new EmptyTile();
                }
                else
                {
                    var tileData = GetTileById(tiledMapData.TileDefinitions, tileId);

                    if (tileData.Type == TileType.Auto)
                    {
                        var autoTileTileSetData = GetAutoTileTileSetData(tiledMapData, (tileData as AutoTileData).AutoTileSetId);
                        if (autoTileTileSetData == null)
                        {
                            LogManager.Instance.WriteLineWarning($"Can't find autoTileTileSetData with Id {tileId}");
                        }

                        tile = CreateTile(x, y, autoTileTileSetData.Tiles, layer, tiledMapData.MapSize, tiledMapData.TileSize, assetContentManager);
                        collisionType = autoTileTileSetData.Tiles[0].CollisionType;
                    }
                    else
                    {
                        tile = CreateTile(tileData, assetContentManager);
                        collisionType = tileData.CollisionType;

                        if (tileData == null)
                        {
                            LogManager.Instance.WriteLineWarning($"Can't find tileData with Id {tileId}");
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
                //        collisionShape = world.Physic2dWorld.CreateCollisionShape(tileEntity, shape, position, CollisionHitType.Defense, CollisionFlags.Static);
                //    }
                //    else
                //    {
                //        collisionShape = world.Physic2dWorld.CreateSensor(tileEntity, shape, position, CollisionHitType.Defense);
                //    }
                //
                //    world.Physic2dWorld.AddCollisionObject(collisionShape);
                //}

                index++;
            }

            layerIndex++;
        }
    }

    private static TileData? GetTileById(List<TileData> tileDefinitions, int tileId)
    {
        foreach (var tileDefinition in tileDefinitions)
        {
            if (tileDefinition.Id == tileId)
            {
                return tileDefinition;
            }
        }

        return null;
    }

    private static AutoTileTileSetData? GetAutoTileTileSetData(TiledMapData tiledMapData, int tileId)
    {
        foreach (var autoTileSetData in tiledMapData.AutoTileSetDatas)
        {
            var autoTileTileSetData = autoTileSetData.GetTileSetById(tileId);

            if (autoTileTileSetData != null)
            {
                return autoTileTileSetData;
            }
        }

        return null;
    }

    public static Tile CreateAutoTile(int x, int y, List<TileData> autoTiles, TiledMapLayerData layer, Size mapSize, Size tileSize, AssetContentManager assetContentManager)
    {
        var tiles = new List<Tile>();

        foreach (var tileData in autoTiles)
        {
            tiles.Add(CreateTile(tileData, assetContentManager));
        }

        var autoTile = new AutoTile(autoTiles[0]);
        autoTile.SetTileInfo(tiles, tileSize, mapSize, layer, x, y);
        return autoTile;
    }

    public static Tile? CreateTile(TileData tileData, AssetContentManager assetContentManager)
    {
        switch (tileData.Type)
        {
            case TileType.Static:
                {
                    var staticTileParams = tileData as StaticTileData;
                    var spriteData = assetContentManager.GetAsset<SpriteData>(staticTileParams.SpriteId);
                    return new StaticTile(Sprite.Create(spriteData, assetContentManager), staticTileParams);
                }
            case TileType.Animated:
                {
                    var animatedTileParams = tileData as AnimatedTileData;
                    var animation = assetContentManager.GetAsset<Animation2dData>(animatedTileParams.Animation2dId);
                    return new AnimatedTile(new Animation2d(animation), animatedTileParams);
                }
        }

        LogManager.Instance.WriteLineError($"The type of tile {tileData.Type} is not supported");

        return null;
    }

    private static Tile CreateTile(int x, int y, List<TileData> autoTiles, TiledMapLayerData layer, Size mapSize, Size tileSize, AssetContentManager assetContentManager)
    {
        var tiles = new List<Tile>();

        foreach (var tileData in autoTiles)
        {
            tiles.Add(CreateTile(tileData, assetContentManager));
        }

        var autoTile = new AutoTile(autoTiles[0]);
        autoTile.SetTileInfo(tiles, tileSize, mapSize, layer, x, y);
        return autoTile;
    }

#if EDITOR
    public static void Save(string file, TiledMapData tiledMapData)
    {

    }
#endif
}