using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Assets.Map2d;

public class TiledMapData
{
    public Coordinates Coordinates { get; } = new();
    public Vector2 MapSize { get; set; }
    public List<TiledMapLayerData> Layers { get; } = new();
    public string TileSetFileName { get; set; }
    public string AutoTileSetFileName { get; set; }
};