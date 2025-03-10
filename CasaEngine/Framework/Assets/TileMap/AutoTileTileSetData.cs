﻿namespace CasaEngine.Framework.Assets.TileMap;

public class AutoTileTileSetData
{
    public int Id { get; set; }
    public List<TileData> Tiles { get; } = new();

    public TileData? GetTileById(int id)
    {
        foreach (var tile in Tiles)
        {
            if (tile.Id == id)
            {
                return tile;
            }
        }
        return null;
    }

}