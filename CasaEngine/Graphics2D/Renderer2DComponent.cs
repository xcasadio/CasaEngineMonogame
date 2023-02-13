using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using CasaEngine.Game;
using CasaEngine.Assets.Graphics2D;
using CasaEngine.Core_Systems.Game;


namespace CasaEngine.Graphics2D
{
    public class Renderer2DComponent
        : Microsoft.Xna.Framework.DrawableGameComponent
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
            public float ZOrder;
            public SpriteEffects SpriteEffect;
            public Rectangle ScissorRectangle;
        }

        private struct Text2DDisplayData
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

        private struct Line2DDisplayData
        {
            public Vector2 Start, End;
            public Color Color;
            public float ZOrder;
            public Rectangle ScissorRectangle;
        }


        public static bool DrawDebug = false;

        readonly List<SpriteDisplayData> _listSprite2D = new(50);
        //used to create a resource pool
        readonly Stack<SpriteDisplayData> _listFreeSpriteDisplayData = new(50);

        readonly List<Text2DDisplayData> _listText2D = new(50);
        //used to create a resource pool
        readonly Stack<Text2DDisplayData> _listFreeTextDisplayData = new(50);

        readonly List<Line2DDisplayData> _listLine2D = new(50);
        //used to create a resource pool
        readonly Stack<Line2DDisplayData> _listFreeLine2DDisplayData = new(50);

        //List<RoundLine> _RoundLines = new List<RoundLine>();		
        //RoundLineManager _RoundLineManager = null;

        readonly Line2DRenderer _line2DRenderer = new();



        public SpriteBatch SpriteBatch
        {
            get;
            set;
        }

#if EDITOR

        public bool CanSetVisible => true;

        public bool CanSetEnable => true;

#endif



        public Renderer2DComponent(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            if (game == null)
            {
                throw new ArgumentNullException("Renderer2DComponent() : Game is null");
            }

            game.Components.Add(this);

            //_RoundLineManager = new RoundLineManager();

            UpdateOrder = (int)ComponentUpdateOrder.Renderer2DComponent;
            DrawOrder = (int)ComponentDrawOrder.Renderer2DComponent;
        }




        public override void Initialize()
        {
            base.Initialize();
            _line2DRenderer.Init(GraphicsDevice);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (this)
                {
                    // Remove self from the service container.
                    GameHelper.RemoveGameComponent<Renderer2DComponent>(Game);
                }
            }

            base.Dispose(disposing);
        }

        public override void Update(GameTime gameTime)
        {
            Clear();
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (_listSprite2D.Count == 0
                && _listText2D.Count == 0
                && _listLine2D.Count == 0)
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
            foreach (var sprite in _listSprite2D)
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

                if (DrawDebug)
                {
                    DrawCross(sprite.Position, 6, Color.Violet);
                }

                Rectangle? rect = sprite.PositionInTexture;

                /*Rectangle temp = new Rectangle();
                temp.X = (int)(sprite.position.X - hotspot.X);
                temp.Y = (int)(sprite.position.Y - hotspot.Y);
                temp.Width = sprite.PositionInTexture.Width;
                temp.Height = sprite.PositionInTexture.Height;

                if (temp.Intersects(sprite.ScissorRectangle) == true)
                {
                    temp.X = System.Math.Max(temp.X, sprite.ScissorRectangle.X);
                    temp.Y = System.Math.Max(temp.Y, sprite.ScissorRectangle.Y);
                    temp.Width = System.Math.Min(
                        sprite.ScissorRectangle.X + sprite.ScissorRectangle.Width, 
                        (int)sprite.position.X + sprite.PositionInTexture.Width) - temp.X;
                    temp.Height = System.Math.Min(
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

                SpriteBatch.Draw(sprite.Texture2D, sprite.Position, rect, sprite.Color,
                    0.0f, hotspot, sprite.Scale, sprite.SpriteEffect, sprite.ZOrder);
            }

            //Text2D
            foreach (var text2D in _listText2D)
            {
                tmpVec2.X = (int)text2D.Position.X;
                tmpVec2.Y = (int)text2D.Position.Y;

                //GraphicsDevice.ScissorRectangle = text2D.ScissorRectangle;

                SpriteBatch.DrawString(text2D.SpriteFont, text2D.Text, tmpVec2, text2D.Color,
                    0.0f, Vector2.Zero, text2D.Scale, text2D.SpriteEffect, text2D.ZOrder);
            }

            //Line2D
            foreach (var line2D in _listLine2D)
            {
                _line2DRenderer.DrawLine(SpriteBatch, line2D.Color, line2D.Start, line2D.End, line2D.ZOrder);
            }

            SpriteBatch.End();

            //RoundLine
            //_RoundLineManager.Draw(_RoundLines, 1.0f, Color.Red, GameInfo);

            //GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            base.Draw(gameTime);
        }



        public void AddSprite2D(int spriteId, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects)
        {
            AddSprite2D(spriteId, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
        }

        public void AddSprite2D(int spriteId, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects, Rectangle scissorRectangle)
        {
            var sprite = (Sprite2D)Engine.Instance.ObjectManager.GetObjectById(spriteId);
            AddSprite2D(sprite.Texture2D, sprite.PositionInTexture, sprite.HotSpot, pos, rot, scale, color, zOrder, effects, scissorRectangle);
        }

        public void AddSprite2D(Texture2D tex, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects)
        {
            AddSprite2D(tex, tex.Bounds, Point.Zero, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
        }

        public void AddSprite2D(Texture2D tex, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects, Rectangle scissorRectangle)
        {
            AddSprite2D(tex, tex.Bounds, Point.Zero, pos, rot, scale, color, zOrder, effects, scissorRectangle);
        }

        public void AddSprite2D(Texture2D tex, Rectangle src, Point origin, Vector2 pos, float rot,
            Vector2 scale, Color color, float zOrder, SpriteEffects effects)
        {
            AddSprite2D(tex, src, origin, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
        }

        public void AddSprite2D(Texture2D tex, Rectangle src, Point origin, Vector2 pos, float rot,
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

            _listSprite2D.Add(sprite);
        }



        /*public void AddText2D(PoolItem<Text2D> text2D_)
		{
			if (text2D_ == null)
			{
				throw new ArgumentException("Graphic2DComponent.AddText2D() : Text2D is null");
			}

			if (text2D_.Resource.SpriteFont == null)
			{
				throw new ArgumentException("Graphic2DComponent.AddText2D() : SpriteFont is null");
			}

			_ListText2D.Add(text2D_);
		}*/

        public void AddText2D(SpriteFont font, string text, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder)
        {
            AddText2D(font, text, pos, rot, scale, color, zOrder, GraphicsDevice.ScissorRectangle);
        }

        public void AddText2D(SpriteFont font, string text, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, Rectangle scissorRectangle)
        {
            if (font == null)
            {
                throw new ArgumentException("Graphic2DComponent.AddText2D() : SpriteFont is null");
            }

            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Graphic2DComponent.AddText2D() : text is null");
            }

            var text2D = GetText2DDisplayData();
            text2D.SpriteFont = font;
            text2D.Text = text;
            text2D.Position = pos;
            text2D.Rotation = rot;
            text2D.Scale = scale;
            text2D.Color = color;
            text2D.ZOrder = zOrder;
            text2D.ScissorRectangle = scissorRectangle;

            _listText2D.Add(text2D);
        }



        public void AddLine2D(Vector2 start, Vector2 end, Color color, float zOrder, Rectangle scissorRectangle)
        {
            var line2D = GetLine2DDisplayData();
            line2D.Start = start;
            line2D.End = end;
            line2D.Color = color;
            line2D.ZOrder = zOrder;
            line2D.ScissorRectangle = scissorRectangle;

            _listLine2D.Add(line2D);
        }

        public void AddLine2D(Vector2 start, Vector2 end, Color color, float zOrder = 0.0f)
        {
            AddLine2D(start, end, color, zOrder, GraphicsDevice.ScissorRectangle);
        }

        public void AddLine2D(float startX, float startY, float endX, float endY, Color color, float zOrder, Rectangle scissorRectangle)
        {
            AddLine2D(new Vector2(startX, startY), new Vector2(endX, endY), color, zOrder, scissorRectangle);
        }

        public void AddLine2D(float startX, float startY, float endX, float endY, Color color, float zOrder = 0.0f)
        {
            AddLine2D(new Vector2(startX, startY), new Vector2(endX, endY), color, zOrder, GraphicsDevice.ScissorRectangle);
        }

        public void AddBox(float x, float y, float width, float height, Color color, float zOrder = 0.0f)
        {
            AddBox(x, y, width, height, color, zOrder, GraphicsDevice.ScissorRectangle);
        }

        public void AddBox(float x, float y, float width, float height, Color color, float zOrder, Rectangle scissorRectangle)
        {
            AddLine2D(x, y, x + width, y, color, zOrder, GraphicsDevice.ScissorRectangle);
            AddLine2D(x, y, x, y + height, color, zOrder, GraphicsDevice.ScissorRectangle);
            AddLine2D(x + width, y, x + width, y + height, color, zOrder, GraphicsDevice.ScissorRectangle);
            AddLine2D(x, y + height, x + width, y + height, color, zOrder, GraphicsDevice.ScissorRectangle);
        }



        private void DrawCross(Vector2 pos, int size, Color color)
        {
            AddLine2D(pos.X - size, pos.Y, pos.X + size, pos.Y, color);
            AddLine2D(pos.X, pos.Y - size, pos.X, pos.Y + size, color);
        }



        private SpriteDisplayData GetSpriteDisplayData()
        {
            if (_listFreeSpriteDisplayData.Count > 0)
            {
                return _listFreeSpriteDisplayData.Pop();
            }

            return new SpriteDisplayData();
        }

        private Text2DDisplayData GetText2DDisplayData()
        {
            if (_listFreeTextDisplayData.Count > 0)
            {
                return _listFreeTextDisplayData.Pop();
            }

            return new Text2DDisplayData();
        }

        private Line2DDisplayData GetLine2DDisplayData()
        {
            if (_listFreeLine2DDisplayData.Count > 0)
            {
                return _listFreeLine2DDisplayData.Pop();
            }

            return new Line2DDisplayData();
        }


        public void Clear()
        {
            foreach (var sprite in _listSprite2D)
            {
                _listFreeSpriteDisplayData.Push(sprite);
            }

            _listSprite2D.Clear();

            foreach (var t in _listText2D)
            {
                _listFreeTextDisplayData.Push(t);
            }

            _listText2D.Clear();

            foreach (var t in _listLine2D)
            {
                _listFreeLine2DDisplayData.Push(t);
            }

            _listLine2D.Clear();
        }

    }
}
