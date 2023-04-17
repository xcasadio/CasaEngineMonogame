using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class TiledMapData
{
    public Coordinates coordinates;
    public Vector2 mapSize;
    public List<TiledMapLayerData> layers;
    public string tileSetFileName;
    public string autoTileSetFileName;
};