using CasaEngine.Framework.Assets.Graphics2D;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Graphics2D.Tile;

public class TileScrolling
    : TileLayer
{

    private List<Sprite2D> _sprites = new();
    private List<Sprite2D> _displaySprites = new();
    private Rectangle _visibleTiles;





    public TileScrolling(GraphicsDeviceManager graphicsComponent/*, Renderer2DComponent Renderer2DComponent_*/)
        : base(Vector2.Zero, graphicsComponent/*, Renderer2DComponent_*/)
    {
    }



    public void AddTile(Sprite2D sprite)
    {
        _sprites.Add(sprite);
    }

    protected override void DetermineVisibility()
    {
        base.DetermineVisibility();

        _visibleTiles.X = (int)CameraPosition.X;
        _visibleTiles.Y = (int)CameraPosition.Y;
        _visibleTiles.Width = (int)(DisplaySize.X / CameraZoom);
        _visibleTiles.Height = (int)(DisplaySize.Y / CameraZoom);

        //PooItem released in Renderer2DComponent
        _displaySprites.Clear();

        throw new NotImplementedException();
        /*foreach (Sprite2D s in _Sprites)
        {
            Rectangle rect = new Rectangle();

            rect.X = (int)s.position.X;
            rect.Y = (int)s.position.Y;
            rect.Width = (int)((float)s.PositionInTexture.Width);
            rect.Height = (int)((float)s.PositionInTexture.Height);

            if (visibleTiles.Intersects(rect))
            {
                PoolItem<Sprite2D> sprite = Graphics2DPool.ResourcePoolSprite2D.GetItem();
                sprite.Resource.Clone(s);
                // = GameInfo.Instance.Asset2DManager.GetSprite2DByID(s.Id);
                sprite.Resource.position = (s.position - CameraPosition) * CameraZoom;
                sprite.Resource.HotSpot = new Point((int)((float)s.HotSpot.X * CameraZoom), (int)((float)s.HotSpot.Y * CameraZoom));
                sprite.Resource.Scale = new Vector2(CameraZoom);
                sprite.Resource.Color = s.Color;
                _DisplaySprites.Add(sprite);
            }
        }*/
    }

    protected override void DrawTiles(SpriteBatch batch)
    {
        foreach (var s in _displaySprites)
        {
            //SpriteRenderer.Draw(batch, s);
        }
    }

}