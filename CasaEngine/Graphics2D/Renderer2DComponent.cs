using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using CasaEngine.Game;
using CasaEngineCommon.Pool;
using CasaEngine.Graphics2D;
using CasaEngineCommon.Logger;
using CasaEngine.CoreSystems.Game;
using CasaEngine.Assets.Graphics2D;



namespace CasaEngine.Graphics2D
{
    /// <summary>
    /// 
    /// </summary>
    public class Renderer2DComponent
        : Microsoft.Xna.Framework.DrawableGameComponent
    {
        /// <summary>
        /// Use to remember how to draw sprite
        /// And to avoid GC
        /// </summary>
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

        /// <summary>
        /// Use to remember how to draw text
        /// And to avoid GC
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
        private struct Line2DDisplayData
        {
            public Vector2 Start, End;
            public Color Color;
            public float ZOrder;
            public Rectangle ScissorRectangle;
        }


        static public bool DrawDebug = false;

        List<SpriteDisplayData> m_ListSprite2D = new List<SpriteDisplayData>(50);
        //used to create a resource pool
        Stack<SpriteDisplayData> m_ListFreeSpriteDisplayData = new Stack<SpriteDisplayData>(50);

        List<Text2DDisplayData> m_ListText2D = new List<Text2DDisplayData>(50);
        //used to create a resource pool
        Stack<Text2DDisplayData> m_ListFreeTextDisplayData = new Stack<Text2DDisplayData>(50);

        List<Line2DDisplayData> m_ListLine2D = new List<Line2DDisplayData>(50);
        //used to create a resource pool
        Stack<Line2DDisplayData> m_ListFreeLine2DDisplayData = new Stack<Line2DDisplayData>(50);

        //List<RoundLine> m_RoundLines = new List<RoundLine>();		
        //RoundLineManager m_RoundLineManager = null;

        Line2DRenderer m_Line2DRenderer = new Line2DRenderer();



        /// <summary>
        /// 
        /// </summary>
        public SpriteBatch SpriteBatch
        {
            get;
            set;
        }

#if EDITOR

        /// <summary>
        /// Gets
        /// </summary>
        public bool CanSetVisible
        {
            get { return true; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public bool CanSetEnable
        {
            get { return true; }
        }

#endif



        /// <summary>
        /// 
        /// </summary>
        /// <param name="game_"></param>
        public Renderer2DComponent(Microsoft.Xna.Framework.Game game_)
            : base(game_)
        {
            if (game_ == null)
            {
                throw new ArgumentNullException("Renderer2DComponent() : Game is null");
            }

            game_.Components.Add(this);

            //m_RoundLineManager = new RoundLineManager();

            UpdateOrder = (int)ComponentUpdateOrder.Renderer2DComponent;
            DrawOrder = (int)ComponentDrawOrder.Renderer2DComponent;
        }




        /// <summary>
        /// 
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            m_Line2DRenderer.Init(this.GraphicsDevice);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing == true)
            {
                lock (this)
                {
                    // Remove self from the service container.
                    GameHelper.RemoveGameComponent<Renderer2DComponent>(this.Game);
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            Clear();
            base.Update(gameTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime)
        {
            if (m_ListSprite2D.Count == 0
                && m_ListText2D.Count == 0
                && m_ListLine2D.Count == 0)
            {
                return;
            }

            //RasterizerState r = new RasterizerState() { ScissorTestEnable = true };
            //GraphicsDevice.RasterizerState = r;

            Vector2 tmpVec2 = Vector2.Zero;
            Vector2 hotspot = Vector2.Zero;

            SpriteBatch.Begin(
                SpriteSortMode.BackToFront,
                BlendState.NonPremultiplied, //AlphaBlend need texture to be compiled with some options
                SamplerState.LinearWrap,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise);

            //sprite2D
            foreach (SpriteDisplayData sprite in m_ListSprite2D)
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

                if (DrawDebug == true)
                {
                    DrawCross(sprite.Position, 6, Color.Violet);
                }

                Rectangle? rect = sprite.PositionInTexture;

                /*Rectangle temp = new Rectangle();
                temp.X = (int)(sprite.Position.X - hotspot.X);
                temp.Y = (int)(sprite.Position.Y - hotspot.Y);
                temp.Width = sprite.PositionInTexture.Width;
                temp.Height = sprite.PositionInTexture.Height;

                if (temp.Intersects(sprite.ScissorRectangle) == true)
                {
                    temp.X = System.Math.Max(temp.X, sprite.ScissorRectangle.X);
                    temp.Y = System.Math.Max(temp.Y, sprite.ScissorRectangle.Y);
                    temp.Width = System.Math.Min(
                        sprite.ScissorRectangle.X + sprite.ScissorRectangle.Width, 
                        (int)sprite.Position.X + sprite.PositionInTexture.Width) - temp.X;
                    temp.Height = System.Math.Min(
                        sprite.ScissorRectangle.Y + sprite.ScissorRectangle.Height,
                        (int)sprite.Position.Y + sprite.PositionInTexture.Height) - temp.Y;

                    temp.X += sprite.PositionInTexture.X - (int)(sprite.Position.X - sprite.Origin.X);
                    temp.Y += sprite.PositionInTexture.Y - (int)(sprite.Position.Y - sprite.Origin.Y);

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
            foreach (Text2DDisplayData text2D in m_ListText2D)
            {
                tmpVec2.X = (int)text2D.Position.X;
                tmpVec2.Y = (int)text2D.Position.Y;

                //GraphicsDevice.ScissorRectangle = text2D.ScissorRectangle;

                SpriteBatch.DrawString(text2D.SpriteFont, text2D.Text, tmpVec2, text2D.Color,
                    0.0f, Vector2.Zero, text2D.Scale, text2D.SpriteEffect, text2D.ZOrder);
            }

            //Line2D
            foreach (Line2DDisplayData line2D in m_ListLine2D)
            {
                m_Line2DRenderer.DrawLine(SpriteBatch, line2D.Color, line2D.Start, line2D.End, line2D.ZOrder);
            }

            SpriteBatch.End();

            //RoundLine
            //m_RoundLineManager.Draw(m_RoundLines, 1.0f, Color.Red, GameInfo);

            //GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            base.Draw(gameTime);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite_"></param>
        /// <param name="pos_"></param>
        /// <param name="rot_"></param>
        /// <param name="scale_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        /// <param name="effects_"></param>
        public void AddSprite2D(int spriteId_, Vector2 pos_, float rot_, Vector2 scale_, Color color_, float ZOrder_, SpriteEffects effects_)
        {
            AddSprite2D(spriteId_, pos_, rot_, scale_, color_, ZOrder_, effects_, GraphicsDevice.ScissorRectangle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spriteId_"></param>
        /// <param name="pos_"></param>
        /// <param name="rot_"></param>
        /// <param name="scale_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        /// <param name="effects_"></param>
        /// <param name="scissorRectangle"></param>
        public void AddSprite2D(int spriteId_, Vector2 pos_, float rot_, Vector2 scale_, Color color_, float ZOrder_, SpriteEffects effects_, Rectangle scissorRectangle)
        {
            Sprite2D sprite = (Sprite2D)Engine.Instance.ObjectManager.GetObjectByID(spriteId_);
            AddSprite2D(sprite.Texture2D, sprite.PositionInTexture, sprite.HotSpot, pos_, rot_, scale_, color_, ZOrder_, effects_, scissorRectangle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tex_"></param>
        /// <param name="pos_"></param>
        /// <param name="rot_"></param>
        /// <param name="scale_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        /// <param name="effects_"></param>
        public void AddSprite2D(Texture2D tex_, Vector2 pos_, float rot_, Vector2 scale_, Color color_, float ZOrder_, SpriteEffects effects_)
        {
            AddSprite2D(tex_, tex_.Bounds, Point.Zero, pos_, rot_, scale_, color_, ZOrder_, effects_, GraphicsDevice.ScissorRectangle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tex_"></param>
        /// <param name="pos_"></param>
        /// <param name="rot_"></param>
        /// <param name="scale_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        /// <param name="effects_"></param>
        /// <param name="scissorRectangle"></param>
        public void AddSprite2D(Texture2D tex_, Vector2 pos_, float rot_, Vector2 scale_, Color color_, float ZOrder_, SpriteEffects effects_, Rectangle scissorRectangle)
        {
            AddSprite2D(tex_, tex_.Bounds, Point.Zero, pos_, rot_, scale_, color_, ZOrder_, effects_, scissorRectangle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tex_"></param>
        /// <param name="src_"></param>
        /// <param name="origin"></param>
        /// <param name="pos_"></param>
        /// <param name="rot_"></param>
        /// <param name="scale_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        /// <param name="effects_"></param>
        public void AddSprite2D(Texture2D tex_, Rectangle src_, Point origin, Vector2 pos_, float rot_,
            Vector2 scale_, Color color_, float ZOrder_, SpriteEffects effects_)
        {
            AddSprite2D(tex_, src_, origin, pos_, rot_, scale_, color_, ZOrder_, effects_, GraphicsDevice.ScissorRectangle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tex_"></param>
        /// <param name="pos_"></param>
        /// <param name="rot_"></param>
        /// <param name="scale_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        public void AddSprite2D(Texture2D tex_, Rectangle src_, Point origin, Vector2 pos_, float rot_,
            Vector2 scale_, Color color_, float ZOrder_, SpriteEffects effects_, Rectangle scissorRectangle_)
        {
            if (tex_ == null)
            {
                throw new ArgumentException("Graphic2DComponent.AddSprite2D() : Texture2D is null");
            }

            if (tex_.IsDisposed == true)
            {
                throw new ArgumentException("Graphic2DComponent.AddSprite2D() : Texture2D is disposed");
            }

            SpriteDisplayData sprite = GetSpriteDisplayData();
            sprite.Texture2D = tex_;
            sprite.PositionInTexture = src_;
            sprite.Position = pos_;
            sprite.Rotation = rot_;
            sprite.Scale = scale_;
            sprite.Color = color_;
            sprite.ZOrder = ZOrder_;
            sprite.SpriteEffect = effects_;
            sprite.Origin = origin;
            sprite.ScissorRectangle = scissorRectangle_;

            m_ListSprite2D.Add(sprite);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="text2D_"></param>
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

			m_ListText2D.Add(text2D_);
		}*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="font_"></param>
        /// <param name="text_"></param>
        /// <param name="pos_"></param>
        /// <param name="rot_"></param>
        /// <param name="scale_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        public void AddText2D(SpriteFont font_, string text_, Vector2 pos_, float rot_, Vector2 scale_, Color color_, float ZOrder_)
        {
            AddText2D(font_, text_, pos_, rot_, scale_, color_, ZOrder_, GraphicsDevice.ScissorRectangle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="font_"></param>
        /// <param name="text_"></param>
        /// <param name="pos_"></param>
        /// <param name="rot_"></param>
        /// <param name="scale_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        /// <param name="scissorRectangle_"></param>
        public void AddText2D(SpriteFont font_, string text_, Vector2 pos_, float rot_, Vector2 scale_, Color color_, float ZOrder_, Rectangle scissorRectangle_)
        {
            if (font_ == null)
            {
                throw new ArgumentException("Graphic2DComponent.AddText2D() : SpriteFont is null");
            }

            if (string.IsNullOrEmpty(text_) == true)
            {
                throw new ArgumentException("Graphic2DComponent.AddText2D() : text is null");
            }

            Text2DDisplayData text2D = GetText2DDisplayData();
            text2D.SpriteFont = font_;
            text2D.Text = text_;
            text2D.Position = pos_;
            text2D.Rotation = rot_;
            text2D.Scale = scale_;
            text2D.Color = color_;
            text2D.ZOrder = ZOrder_;
            text2D.ScissorRectangle = scissorRectangle_;

            m_ListText2D.Add(text2D);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="start_"></param>
        /// <param name="end_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        /// <param name="scissorRectangle_"></param>
        public void AddLine2D(Vector2 start_, Vector2 end_, Color color_, float ZOrder_, Rectangle scissorRectangle_)
        {
            Line2DDisplayData line2D = GetLine2DDisplayData();
            line2D.Start = start_;
            line2D.End = end_;
            line2D.Color = color_;
            line2D.ZOrder = ZOrder_;
            line2D.ScissorRectangle = scissorRectangle_;

            m_ListLine2D.Add(line2D);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="start_"></param>
        /// <param name="end_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        public void AddLine2D(Vector2 start_, Vector2 end_, Color color_, float ZOrder_ = 0.0f)
        {
            AddLine2D(start_, end_, color_, ZOrder_, GraphicsDevice.ScissorRectangle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="startX_"></param>
        /// <param name="startY_"></param>
        /// <param name="endX_"></param>
        /// <param name="endY_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        /// <param name="scissorRectangle_"></param>
        public void AddLine2D(float startX_, float startY_, float endX_, float endY_, Color color_, float ZOrder_, Rectangle scissorRectangle_)
        {
            AddLine2D(new Vector2(startX_, startY_), new Vector2(endX_, endY_), color_, ZOrder_, scissorRectangle_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="startX_"></param>
        /// <param name="startY_"></param>
        /// <param name="endX_"></param>
        /// <param name="endY_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        public void AddLine2D(float startX_, float startY_, float endX_, float endY_, Color color_, float ZOrder_ = 0.0f)
        {
            AddLine2D(new Vector2(startX_, startY_), new Vector2(endX_, endY_), color_, ZOrder_, GraphicsDevice.ScissorRectangle);
        }

        /// <summary>
        /// Draw a box.
        /// x_ and y_ are the coordinates of the top left corner.
        /// </summary>
        /// <param name="x_">Coordinates X of the top left corner</param>
        /// <param name="y_">Coordinates Y of the top left corner</param>
        /// <param name="width_">Width of the box</param>
        /// <param name="height_">Height of the box</param>
        /// <param name="color_">Color of the lines</param>
        /// <param name="ZOrder_">Depth of the line. 0 is the front and 1 is the back</param>
        public void AddBox(float x_, float y_, float width_, float height_, Color color_, float ZOrder_ = 0.0f)
        {
            AddBox(x_, y_, width_, height_, color_, ZOrder_, GraphicsDevice.ScissorRectangle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x_"></param>
        /// <param name="y_"></param>
        /// <param name="width_"></param>
        /// <param name="height_"></param>
        /// <param name="color_"></param>
        /// <param name="ZOrder_"></param>
        /// <param name="scissorRectangle_"></param>
        public void AddBox(float x_, float y_, float width_, float height_, Color color_, float ZOrder_, Rectangle scissorRectangle_)
        {
            AddLine2D(x_, y_, x_ + width_, y_, color_, ZOrder_, GraphicsDevice.ScissorRectangle);
            AddLine2D(x_, y_, x_, y_ + height_, color_, ZOrder_, GraphicsDevice.ScissorRectangle);
            AddLine2D(x_ + width_, y_, x_ + width_, y_ + height_, color_, ZOrder_, GraphicsDevice.ScissorRectangle);
            AddLine2D(x_, y_ + height_, x_ + width_, y_ + height_, color_, ZOrder_, GraphicsDevice.ScissorRectangle);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos_"></param>
        /// <param name="size_"></param>
        /// <param name="color_"></param>
        private void DrawCross(Vector2 pos_, int size_, Color color_)
        {
            AddLine2D(pos_.X - size_, pos_.Y, pos_.X + size_, pos_.Y, color_, 0.0f);
            AddLine2D(pos_.X, pos_.Y - size_, pos_.X, pos_.Y + size_, color_, 0.0f);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private SpriteDisplayData GetSpriteDisplayData()
        {
            if (m_ListFreeSpriteDisplayData.Count > 0)
            {
                return m_ListFreeSpriteDisplayData.Pop();
            }

            return new SpriteDisplayData();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Text2DDisplayData GetText2DDisplayData()
        {
            if (m_ListFreeTextDisplayData.Count > 0)
            {
                return m_ListFreeTextDisplayData.Pop();
            }

            return new Text2DDisplayData();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Line2DDisplayData GetLine2DDisplayData()
        {
            if (m_ListFreeLine2DDisplayData.Count > 0)
            {
                return m_ListFreeLine2DDisplayData.Pop();
            }

            return new Line2DDisplayData();
        }


        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            foreach (SpriteDisplayData sprite in m_ListSprite2D)
            {
                m_ListFreeSpriteDisplayData.Push(sprite);
            }

            m_ListSprite2D.Clear();

            foreach (Text2DDisplayData t in m_ListText2D)
            {
                m_ListFreeTextDisplayData.Push(t);
            }

            m_ListText2D.Clear();

            foreach (Line2DDisplayData t in m_ListLine2D)
            {
                m_ListFreeLine2DDisplayData.Push(t);
            }

            m_ListLine2D.Clear();
        }

    }
}
