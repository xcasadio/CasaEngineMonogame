namespace CasaEngine.Framework.Assets.Map2d;

public class TiledMapLayerData
{
    public string? Name { get; set; }
    public List<int> tiles = new();
    //public List<TileData> tiles = new();
    public float zOffset;
    public TileType type;
};