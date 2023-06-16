using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Tiled Map")]
public class TiledMapComponent : Component
{
    public static readonly int ComponentId = (int)ComponentIds.TiledMap;

    public TiledMapData TiledMapData { get; set; }
#if EDITOR
    public string TiledMapDataFileName { get; set; }
#endif
    private List<TiledMapLayer> Layers { get; } = new();

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
            SpriteLoader.LoadFromFile(Path.Combine(GameSettings.ProjectSettings.ProjectPath, spriteSheetFileName), game.GameManager.AssetContentManager);
        }

        foreach (var tiledMapLayerData in TiledMapData.Layers)
        {
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
                            tile = TiledMapLoader.CreateAutoTile(x, y, autoTiles, tiledMapLayerData, TiledMapData.MapSize, TiledMapData.TileSize, game.GameManager.AssetContentManager);
                        }
                        else
                        {
                            tile = TiledMapLoader.CreateTile(tileData, game.GameManager.AssetContentManager);
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

    public override void Load(JsonElement element)
    {
        var fileName = element.GetProperty("tiledMapDataFileName").GetString();
        TiledMapData = TiledMapLoader.LoadMapFromFile(Path.Combine(GameSettings.ProjectSettings.ProjectPath, fileName));

#if EDITOR
        TiledMapDataFileName = fileName;
#endif
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        jObject.Add("tiledMapDataFileName", TiledMapDataFileName);
    }

#endif
}