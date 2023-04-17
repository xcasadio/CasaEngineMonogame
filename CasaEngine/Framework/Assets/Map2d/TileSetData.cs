using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class TileSetData
{
    public string spriteSheetFileName;
    public Vector2 tileSize;
    public List<TileData> tiles;

    public TileData? GetTileById(int id)
    {
        foreach (var tile in tiles)
        {
            if (tile.id == id)
            {
                return tile;
            }
        }
        return null;
    }
};