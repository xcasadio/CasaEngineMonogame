﻿using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class AutoTileSetData
{
    public string SpriteSheetFileName { get; set; }
    public Vector2 TileSize { get; set; }
    public List<AutoTileTileSetData> Sets { get; } = new();

    public AutoTileTileSetData? GetTileSetById(int id)
    {
        foreach (var set in Sets)
        {
            if (set.Id == id)
            {
                return set;
            }
        }
        return null;
    }

}