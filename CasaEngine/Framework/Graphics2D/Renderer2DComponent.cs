﻿using System.Runtime.CompilerServices;
using CasaEngine.Core.Shapes;
using CasaEngine.Framework.Assets.Sprites;
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
        public float ZOrder; // 0 being the front layer, and 1 being the back layer.
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

    public bool IsDrawSpriteOriginEnabled = false;
    public bool IsDrawSpriteBorderEnabled = false;
    public bool IsDrawSpriteSheetEnabled = false;
    public bool IsDrawCollisionsEnabled = false;
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

        Game.GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        Game.GraphicsDevice.DepthStencilState = DepthStencilState.Default;

        SpriteBatch.Begin(
            GameSettings.GraphicsSettings.SpriteSortMode,
            GameSettings.GraphicsSettings.BlendState,
            GameSettings.GraphicsSettings.SamplerState,
            GameSettings.GraphicsSettings.DepthStencilState,
            GameSettings.GraphicsSettings.RasterizerState);

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

            if (IsDrawSpriteOriginEnabled)
            {
                DrawCross(sprite.Position, 6, Color.Violet);
            }

            if (IsDrawSpriteBorderEnabled)
            {
                Rectangle temp = new Rectangle
                {
                    X = (int)(sprite.Position.X - hotspot.X * sprite.Scale.X),
                    Y = (int)(sprite.Position.Y - hotspot.Y * sprite.Scale.Y),
                    Width = (int)(sprite.PositionInTexture.Width * sprite.Scale.X),
                    Height = (int)(sprite.PositionInTexture.Height * sprite.Scale.Y)
                };
                DrawRectangle(ref temp, Color.BlueViolet);
            }

            if (IsDrawSpriteSheetEnabled)
            {
                var position = sprite.Position - new Vector2(
                    sprite.PositionInTexture.Left + hotspot.X,
                    sprite.PositionInTexture.Top + hotspot.Y) * sprite.Scale;

                SpriteBatch.Draw(sprite.Texture2D, position, sprite.Texture2D.Bounds, Color.FromNonPremultiplied(255, 255, 255, SpriteSheetTransparency),
                    0.0f, Vector2.Zero, sprite.Scale, sprite.SpriteEffect, sprite.ZOrder);
            }

            Rectangle? rect = sprite.PositionInTexture;

            //Rectangle temp = new Rectangle();
            //temp.X = (int)(sprite.position.X - hotspot.X);
            //temp.Y = (int)(sprite.position.Y - hotspot.Y);
            //temp.Width = sprite.PositionInTexture.Width;
            //temp.Height = sprite.PositionInTexture.Height;
            //
            //if (temp.Intersects(sprite.ScissorRectangle) == true)
            //{
            //    temp.X = System.System.Math.Max(temp.X, sprite.ScissorRectangle.X);
            //    temp.Y = System.System.Math.Max(temp.Y, sprite.ScissorRectangle.Y);
            //    temp.Width = System.System.Math.Min(
            //        sprite.ScissorRectangle.X + sprite.ScissorRectangle.Width, 
            //        (int)sprite.position.X + sprite.PositionInTexture.Width) - temp.X;
            //    temp.Height = System.System.Math.Min(
            //        sprite.ScissorRectangle.Y + sprite.ScissorRectangle.Height,
            //        (int)sprite.position.Y + sprite.PositionInTexture.Height) - temp.Y;
            //
            //    temp.X += sprite.PositionInTexture.X - (int)(sprite.position.X - sprite.Origin.X);
            //    temp.Y += sprite.PositionInTexture.Y - (int)(sprite.position.Y - sprite.Origin.Y);
            //
            //    temp.Width = (int)((float)temp.Width / sprite.Scale.X);
            //    temp.Height = (int)((float)temp.Height / sprite.Scale.Y);
            //
            //    rect = temp;
            //
            //    hotspot = Vector2.Zero;
            //}
            //else
            //{
            //    continue;
            //}

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

        Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, SpriteData spriteData, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects = SpriteEffects.None)
    {
        DrawSprite(sprite, spriteData, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, SpriteData spriteData, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects, Rectangle scissorRectangle)
    {
        DrawSprite(sprite.Texture.Resource, spriteData.PositionInTexture, spriteData.Origin, pos, rot, scale, color, zOrder, effects, scissorRectangle);

        if (IsDrawCollisionsEnabled)
        {
            foreach (var collision2d in spriteData.CollisionShapes)
            {
                DrawCollision(collision2d, new Vector2(pos.X - spriteData.Origin.X * scale.X, pos.Y - spriteData.Origin.Y * scale.Y), scale, zOrder);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D tex, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects)
    {
        DrawSprite(tex, tex.Bounds, Point.Zero, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D tex, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects, Rectangle scissorRectangle)
    {
        DrawSprite(tex, tex.Bounds, Point.Zero, pos, rot, scale, color, zOrder, effects, scissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D tex, Rectangle src, Point origin, Vector2 pos, float rot,
        Vector2 scale, Color color, float zOrder, SpriteEffects effects)
    {
        DrawSprite(tex, src, origin, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D tex, Rectangle src, Point origin, Vector2 pos, float rot,
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawText(SpriteFont font, string text, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder)
    {
        DrawText(font, text, pos, rot, scale, color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawText(SpriteFont font, string text, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, Rectangle scissorRectangle)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawLine(Vector2 start, Vector2 end, Color color, float zOrder, Rectangle scissorRectangle)
    {
        var line2D = GetLine2dDisplayData();
        line2D.Start = start;
        line2D.End = end;
        line2D.Color = color;
        line2D.ZOrder = zOrder;
        line2D.ScissorRectangle = scissorRectangle;

        _lines.Add(line2D);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawLine(Vector2 start, Vector2 end, Color color, float zOrder = 0.0f)
    {
        DrawLine(start, end, color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawLine(float startX, float startY, float endX, float endY, Color color, float zOrder, Rectangle scissorRectangle)
    {
        DrawLine(new Vector2(startX, startY), new Vector2(endX, endY), color, zOrder, scissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawLine(float startX, float startY, float endX, float endY, Color color, float zOrder = 0.0f)
    {
        DrawLine(new Vector2(startX, startY), new Vector2(endX, endY), color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawRectangle(ref Rectangle rectangle, Color color, float zOrder = 0.0f)
    {
        DrawRectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawRectangle(float x, float y, float width, float height, Color color, float zOrder = 0.0f)
    {
        DrawRectangle(x, y, width, height, color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawRectangle(float x, float y, float width, float height, Color color, float zOrder, Rectangle scissorRectangle)
    {
        DrawLine(x, y, x + width, y, color, zOrder, GraphicsDevice.ScissorRectangle);
        DrawLine(x, y, x, y + height, color, zOrder, GraphicsDevice.ScissorRectangle);
        DrawLine(x + width, y, x + width, y + height, color, zOrder, GraphicsDevice.ScissorRectangle);
        DrawLine(x, y + height, x + width, y + height, color, zOrder, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawCross(Vector2 pos, int size, Color color)
    {
        DrawLine(pos.X - size, pos.Y, pos.X + size - 1, pos.Y, color);
        DrawLine(pos.X, pos.Y - size, pos.X, pos.Y + size, color);
    }

    public void DrawCollision(Collision2d collision2d, Vector2 position, Vector2 scale, float z)
    {
        var color = collision2d.CollisionHitType == CollisionHitType.Attack ? Color.Red : Color.Green;

        switch (collision2d.Shape.Type)
        {
            case Shape2dType.Compound:
                break;
            case Shape2dType.Polygone:
                break;
            case Shape2dType.Rectangle:
                var rectangle = collision2d.Shape as ShapeRectangle;
                DrawRectangle(position.X + rectangle.Position.X * scale.X, position.Y + rectangle.Position.Y * scale.X, rectangle.Width * scale.X, rectangle.Height * scale.Y, color, z);
                break;
            case Shape2dType.Circle:
                break;
            case Shape2dType.Line:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SpriteDisplayData GetSpriteDisplayData()
    {
        return _freeSpritesData.Count > 0 ? _freeSpritesData.Pop() : new SpriteDisplayData();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Text2dDisplayData GetText2dDisplayData()
    {
        return _freeTextsData.Count > 0 ? _freeTextsData.Pop() : new Text2dDisplayData();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
}
