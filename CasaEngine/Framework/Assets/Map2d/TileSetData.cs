using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class TileSetData
{
    public string SpriteSheetFileName { get; set; }
    public Vector2 TileSize { get; set; }
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
};