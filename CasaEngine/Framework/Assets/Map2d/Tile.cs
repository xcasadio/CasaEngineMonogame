namespace CasaEngine.Framework.Assets.Map2d;

public abstract class Tile
{
    //private SpriteRenderer _spriteRenderer;
    public TileData? TileData { get; }

    public Tile(TileData? tileData)
    {
        TileData = tileData;
    }

    public virtual void Initialize()
    {
        //_spriteRenderer = Framework.Game::Instance().GetGameComponent<SpriteRenderer>();
        //CA_ASSERT(_spriteRenderer != null, "Initialize SpriteRenderer is null")
    }

    public abstract void Update(float elapsedTime);
    public abstract void Draw(float x, float y, float z);
    public abstract void Draw(float x, float y, float z, Rectangle uvOffset);

    protected void Draw(Sprite sprite, float x, float y, float z, Rectangle uvOffset)
    {
        //Rectangle texUV = new Rectangle(
        //    sprite.GetSpriteData().GetPositionInTexture().Left() + uvOffset.Left,
        //    sprite.GetSpriteData().GetPositionInTexture().Top() + uvOffset.Top,
        //    uvOffset.Width,
        //    uvOffset.Height);
        //
        //_spriteRenderer.AddSprite(
        //    sprite.GetTexture2D(),
        //    texUV,
        //    sprite.GetSpriteData().GetOrigin(),
        //    new Vector2(x, y),
        //    0.0f,
        //    Vector2.One,
        //    Color::White,
        //    z);
    }
}