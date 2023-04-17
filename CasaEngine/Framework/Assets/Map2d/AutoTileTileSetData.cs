namespace CasaEngine.Framework.Assets.Map2d;

public class AutoTileTileSetData
{
    public int id;
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