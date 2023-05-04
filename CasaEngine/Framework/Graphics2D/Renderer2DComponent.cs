using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Graphics2D;

public class Renderer2dComponent : DrawableGameComponent
{
    private struct SpriteDisplayData
    {
        public Vector2 Position;
        public float Rotation;
        public Texture2D Texture2D;
        public Rectangle PositionInTexture;
        public Point Origin;
        public Vector2 Scale;
        public Color Color;
        public float ZOrder; // 0 being the frontmost layer, and 1 being the backmost layer.
        public SpriteEffects SpriteEffect;
        public Rectangle ScissorRectangle;
    }

    private struct Text2dDisplayData
    {
        public string Text;
        public SpriteFont SpriteFont;
        public Vector2 Position;
        public float Rotation;
        public Point Origin;
        public Vector2 Scale;
        public Color Color;
        public float ZOrder;
        public SpriteEffects SpriteEffect;
        public Rectangle ScissorRectangle;
    }

    private struct Line2dDisplayData
    {
        public Vector2 Start, End;
        public Color Color;
        public float ZOrder;
        public Rectangle ScissorRectangle;
    }

    public bool DrawSpriteOrigin = false;
    public bool DrawSpriteBorder = false;
    public bool DrawSpriteSheet = false;
    public int SpriteSheetTransparency = 124;

    private readonly List<SpriteDisplayData> _spritesData = new(50);
    //used to create a resource pool
    private readonly Stack<SpriteDisplayData> _freeSpritesData = new(50);

    private readonly List<Text2dDisplayData> _textsData = new(50);
    //used to create a resource pool
    private readonly Stack<Text2dDisplayData> _freeTextsData = new(50);

    private readonly List<Line2dDisplayData> _lines = new(50);
    //used to create a resource pool
    private readonly Stack<Line2dDisplayData> _linesData = new(50);

    //List<RoundLine> _RoundLines = new List<RoundLine>();		
    //RoundLineManager _RoundLineManager = null;

    private readonly Line2dRenderer _line2dRenderer = new();

    public SpriteBatch SpriteBatch { get; set; }

    public Renderer2dComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        game.Components.Add(this);

        //_RoundLineManager = new RoundLineManager();

        UpdateOrder = (int)ComponentUpdateOrder.Renderer2dComponent;
        DrawOrder = (int)ComponentDrawOrder.Renderer2dComponent;
    }

    public override void Initialize()
    {
        base.Initialize();
        _line2dRenderer.Init(GraphicsDevice);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (this)
            {
                Game.RemoveGameComponent<Renderer2dComponent>();
            }
        }

        base.Dispose(disposing);
    }

    public override void Draw(GameTime gameTime)
    {
        if (_spritesData.Count == 0
            && _textsData.Count == 0
            && _lines.Count == 0)
        {
            return;
        }

        //RasterizerState r = new RasterizerState() { ScissorTestEnable = true };
        //GraphicsDevice.RasterizerState = r;

        var tmpVec2 = Vector2.Zero;
        var hotspot = Vector2.Zero;

        SpriteBatch.Begin(
            SpriteSortMode.BackToFront,
            BlendState.NonPremultiplied, //AlphaBlend need texture to be compiled with some options
            SamplerState.LinearWrap,
            DepthStencilState.None,
            RasterizerState.CullCounterClockwise);

        //sprite2D
        foreach (var sprite in _spritesData)
        {
            switch (sprite.SpriteEffect)
            {
                case SpriteEffects.None:
                    hotspot.X = sprite.Origin.X;
                    hotspot.Y = sprite.Origin.Y;
                    break;

                case SpriteEffects.FlipHorizontally:
                    hotspot.X = sprite.PositionInTexture.Width - sprite.Origin.X;
                    hotspot.Y = sprite.Origin.Y;
                    break;

                case SpriteEffects.FlipVertically:

                    break;
            }

            if (DrawSpriteOrigin)
            {
                DrawCross(sprite.Position, 6, Color.Violet);
            }

            if (DrawSpriteBorder)
            {
                Rectangle temp = new Rectangle
                {
                    X = (int)(sprite.Position.X - hotspot.X * sprite.Scale.X),
                    Y = (int)(sprite.Position.Y - hotspot.Y * sprite.Scale.Y),
                    Width = (int)(sprite.PositionInTexture.Width * sprite.Scale.X),
                    Height = (int)(sprite.PositionInTexture.Height * sprite.Scale.Y)
                };
                AddBox(ref temp, Color.BlueViolet);
            }

            if (DrawSpriteSheet)
            {
                var position = sprite.Position - new Vector2(
                    sprite.PositionInTexture.Left + hotspot.X,
                    sprite.PositionInTexture.Top + hotspot.Y) * sprite.Scale;

                SpriteBatch.Draw(sprite.Texture2D, position, sprite.Texture2D.Bounds, Color.FromNonPremultiplied(255, 255, 255, SpriteSheetTransparency),
                    0.0f, Vector2.Zero, sprite.Scale, sprite.SpriteEffect, sprite.ZOrder);
            }

            Rectangle? rect = sprite.PositionInTexture;

            /*Rectangle temp = new Rectangle();
            temp.X = (int)(sprite.position.X - hotspot.X);
            temp.Y = (int)(sprite.position.Y - hotspot.Y);
            temp.Width = sprite.PositionInTexture.Width;
            temp.Height = sprite.PositionInTexture.Height;

            if (temp.Intersects(sprite.ScissorRectangle) == true)
            {
                temp.X = System.System.Math.Max(temp.X, sprite.ScissorRectangle.X);
                temp.Y = System.System.Math.Max(temp.Y, sprite.ScissorRectangle.Y);
                temp.Width = System.System.Math.Min(
                    sprite.ScissorRectangle.X + sprite.ScissorRectangle.Width, 
                    (int)sprite.position.X + sprite.PositionInTexture.Width) - temp.X;
                temp.Height = System.System.Math.Min(
                    sprite.ScissorRectangle.Y + sprite.ScissorRectangle.Height,
                    (int)sprite.position.Y + sprite.PositionInTexture.Height) - temp.Y;

                temp.X += sprite.PositionInTexture.X - (int)(sprite.position.X - sprite.Origin.X);
                temp.Y += sprite.PositionInTexture.Y - (int)(sprite.position.Y - sprite.Origin.Y);

                temp.Width = (int)((float)temp.Width / sprite.Scale.X);
                temp.Height = (int)((float)temp.Height / sprite.Scale.Y);

                rect = temp;

                hotspot = Vector2.Zero;
            }
            else
            {
                continue;
            }*/

            //GraphicsDevice.ScissorRectangle = sprite.ScissorRectangle;

            SpriteBatch.Draw(sprite.Texture2D, sprite.Position, rect, sprite.Color, 0.0f, hotspot, sprite.Scale, sprite.SpriteEffect, sprite.ZOrder);
        }

        //Text2D
        foreach (var text2D in _textsData)
        {
            tmpVec2.X = (int)text2D.Position.X;
            tmpVec2.Y = (int)text2D.Position.Y;

            //GraphicsDevice.ScissorRectangle = text2D.ScissorRectangle;

            SpriteBatch.DrawString(text2D.SpriteFont, text2D.Text, tmpVec2, text2D.Color, 0.0f, Vector2.Zero, text2D.Scale, text2D.SpriteEffect, text2D.ZOrder);
        }

        //Line2D
        foreach (var line2D in _lines)
        {
            _line2dRenderer.DrawLine(SpriteBatch, line2D.Color, line2D.Start, line2D.End, line2D.ZOrder);
        }

        SpriteBatch.End();

        //RoundLine
        //_RoundLineManager.Draw(_RoundLines, 1.0f, Color.Red, GameInfo);

        //GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

        base.Draw(gameTime);

        Clear();
    }

    //public void AddSprite(int spriteId, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects)
    //{
    //    AddSprite(spriteId, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
    //}

    //public void AddSprite(int spriteId, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects, Rectangle scissorRectangle)
    //{
    //    //var sprite = (Sprite2D)((CasaEngineGame)Game).GameManager.AssetContentManager.GetAsset<>(SpriteId);
    //    //AddSprite2D(sprite.Texture2D, sprite.PositionInTexture, sprite.HotSpot, pos, rot, scale, color, zOrder, effects, scissorRectangle);
    //}
    //
    public void AddSprite(Texture2D tex, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects)
    {
        AddSprite(tex, tex.Bounds, Point.Zero, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
    }

    public void AddSprite(Texture2D tex, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects, Rectangle scissorRectangle)
    {
        AddSprite(tex, tex.Bounds, Point.Zero, pos, rot, scale, color, zOrder, effects, scissorRectangle);
    }

    public void AddSprite(Texture2D tex, Rectangle src, Point origin, Vector2 pos, float rot,
        Vector2 scale, Color color, float zOrder, SpriteEffects effects)
    {
        AddSprite(tex, src, origin, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
    }

    public void AddSprite(Texture2D tex, Rectangle src, Point origin, Vector2 pos, float rot,
        Vector2 scale, Color color, float zOrder, SpriteEffects effects, Rectangle scissorRectangle)
    {
        if (tex == null)
        {
            throw new ArgumentException("Graphic2DComponent.AddSprite2D() : Texture2D is null");
        }

        if (tex.IsDisposed)
        {
            throw new ArgumentException("Graphic2DComponent.AddSprite2D() : Texture2D is disposed");
        }

        var sprite = GetSpriteDisplayData();
        sprite.Texture2D = tex;
        sprite.PositionInTexture = src;
        sprite.Position = pos;
        sprite.Rotation = rot;
        sprite.Scale = scale;
        sprite.Color = color;
        sprite.ZOrder = zOrder;
        sprite.SpriteEffect = effects;
        sprite.Origin = origin;
        sprite.ScissorRectangle = scissorRectangle;

        _spritesData.Add(sprite);
    }

    /*public void AddText2d(PoolItem<Text2D> text2D_)
    {
        if (text2D_ == null)
        {
            throw new ArgumentException("Graphic2DComponent.AddText2d() : Text2D is null");
        }

        if (text2D_.Resource.SpriteFont == null)
        {
            throw new ArgumentException("Graphic2DComponent.AddText2d() : SpriteFont is null");
        }

        _ListText2D.Add(text2D_);
    }*/

    public void AddText2d(SpriteFont font, string text, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder)
    {
        AddText2d(font, text, pos, rot, scale, color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    public void AddText2d(SpriteFont font, string text, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, Rectangle scissorRectangle)
    {
        if (font == null)
        {
            throw new ArgumentException("Graphic2DComponent.AddText2d() : SpriteFont is null");
        }

        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Graphic2DComponent.AddText2d() : text is null");
        }

        var text2D = GetText2dDisplayData();
        text2D.SpriteFont = font;
        text2D.Text = text;
        text2D.Position = pos;
        text2D.Rotation = rot;
        text2D.Scale = scale;
        text2D.Color = color;
        text2D.ZOrder = zOrder;
        text2D.ScissorRectangle = scissorRectangle;

        _textsData.Add(text2D);
    }

    public void AddLine2d(Vector2 start, Vector2 end, Color color, float zOrder, Rectangle scissorRectangle)
    {
        var line2D = GetLine2dDisplayData();
        line2D.Start = start;
        line2D.End = end;
        line2D.Color = color;
        line2D.ZOrder = zOrder;
        line2D.ScissorRectangle = scissorRectangle;

        _lines.Add(line2D);
    }

    public void AddLine2d(Vector2 start, Vector2 end, Color color, float zOrder = 0.0f)
    {
        AddLine2d(start, end, color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    public void AddLine2d(float startX, float startY, float endX, float endY, Color color, float zOrder, Rectangle scissorRectangle)
    {
        AddLine2d(new Vector2(startX, startY), new Vector2(endX, endY), color, zOrder, scissorRectangle);
    }

    public void AddLine2d(float startX, float startY, float endX, float endY, Color color, float zOrder = 0.0f)
    {
        AddLine2d(new Vector2(startX, startY), new Vector2(endX, endY), color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    public void AddBox(ref Rectangle rectangle, Color color, float zOrder = 0.0f)
    {
        AddBox(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    public void AddBox(float x, float y, float width, float height, Color color, float zOrder = 0.0f)
    {
        AddBox(x, y, width, height, color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    public void AddBox(float x, float y, float width, float height, Color color, float zOrder, Rectangle scissorRectangle)
    {
        AddLine2d(x, y, x + width, y, color, zOrder, GraphicsDevice.ScissorRectangle);
        AddLine2d(x, y, x, y + height, color, zOrder, GraphicsDevice.ScissorRectangle);
        AddLine2d(x + width, y, x + width, y + height, color, zOrder, GraphicsDevice.ScissorRectangle);
        AddLine2d(x, y + height, x + width, y + height, color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    private SpriteDisplayData GetSpriteDisplayData()
    {
        return _freeSpritesData.Count > 0 ? _freeSpritesData.Pop() : new SpriteDisplayData();
    }

    private Text2dDisplayData GetText2dDisplayData()
    {
        return _freeTextsData.Count > 0 ? _freeTextsData.Pop() : new Text2dDisplayData();
    }

    private Line2dDisplayData GetLine2dDisplayData()
    {
        return _linesData.Count > 0 ? _linesData.Pop() : new Line2dDisplayData();
    }

    public void Clear()
    {
        foreach (var sprite in _spritesData)
        {
            _freeSpritesData.Push(sprite);
        }

        _spritesData.Clear();

        foreach (var t in _textsData)
        {
            _freeTextsData.Push(t);
        }

        _textsData.Clear();

        foreach (var t in _lines)
        {
            _linesData.Push(t);
        }

        _lines.Clear();
    }


    private void DrawCross(Vector2 pos, int size, Color color)
    {
        AddLine2d(pos.X - size, pos.Y, pos.X + size - 1, pos.Y, color);
        AddLine2d(pos.X, pos.Y - size, pos.X, pos.Y + size, color);
    }
}