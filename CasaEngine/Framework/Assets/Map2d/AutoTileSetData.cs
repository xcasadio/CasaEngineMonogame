using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class AutoTileSetData
{
    public string spriteSheetFileName;
    public Vector2 tileSize;
    public List<AutoTileTileSetData> sets;

    public AutoTileTileSetData? GetTileSetById(int id)
    {
        foreach (var set in sets)
        {
            if (set.id == id)
            {
                return set;
            }
        }
        return null;
    }

}