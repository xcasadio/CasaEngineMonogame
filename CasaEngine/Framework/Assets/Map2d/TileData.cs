namespace CasaEngine.Framework.Assets.Map2d;

public class TileData
{
    public int id;
    public TileType type;
    public TileCollisionType collisionType;
    public bool isBreakable = false;

    protected TileData(TileType type)
    {
        id = 0;
        this.type = type;
        collisionType = TileCollisionType.None;
    }
}