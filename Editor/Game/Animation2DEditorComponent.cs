
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Editor.UndoRedo;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Graphics2D;
using CasaEngineCommon.Helper;
using CasaEngine.Game;
using CasaEngine.CoreSystems.Game;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.Assets.Graphics2D;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Editor.Game
{
    class Animation2DEditorComponent
        : CasaEngine.Game.DrawableGameComponent
    {

        Animation2D m_CurrentAnimation2D;
        Animation2D m_OriginalAnimation2D;
        Dictionary<int, Sprite2D> m_Sprites = new();
        Vector2 m_SpritePosition, m_Zoom;
        SpriteBatch m_SpriteBatch;
        Line2DRenderer m_Line2DRenderer;
        string m_ObjectPath;

        MouseWindowed m_Mouse;
        Point m_MouseRightDownPosition = new();
        bool m_MouseRightDown = false;

        Animation2D m_ChangeCurrentAnimation;
        bool m_NeedChangeAnimation2D = false;

        bool m_PlayAnimation = false;

        public event EventHandler CurrentAnimationSetted;



        /// <summary>
        /// Gets/Sets
        /// </summary>
        public UndoRedoManager UndoRedoManager
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Animation2D CurrentAnimation
        {
            get { return m_CurrentAnimation2D; }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool PlayAnimation
        {
            get { return m_PlayAnimation; }
            set { m_PlayAnimation = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        public bool DisplayPreviousFrame
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool DisplayOrigin
        {
            get;
            set;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="game_"></param>
        internal Animation2DEditorComponent(CustomGameEditor game_)
            : base(game_)
        {
            DisplayPreviousFrame = false;

            game_.Components.Add(this);

            m_Zoom = Vector2.One;
        }




        /// <summary>
        /// 
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void LoadContent()
        {
            m_SpriteBatch = new SpriteBatch(Game.GraphicsDevice);
            m_Line2DRenderer = new Line2DRenderer();
            m_Line2DRenderer.Init(Game.GraphicsDevice);
            m_Mouse = new MouseWindowed(Game.GraphicsDevice.PresentationParameters.DeviceWindowHandle);

            base.LoadContent();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void UnloadContent()
        {
            base.UnloadContent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            ChangeCurrentAnimation2DIfNeeded();

            if (m_MouseRightDown == true)
            {
                int delatX = m_MouseRightDownPosition.X - m_Mouse.X;
                int delatY = m_MouseRightDownPosition.Y - m_Mouse.Y;

                m_SpritePosition.X -= delatX;
                m_SpritePosition.Y -= delatY;
            }

            //Right
            if (m_Mouse.RightButton == true)
            {
                m_MouseRightDownPosition.X = m_Mouse.X;
                m_MouseRightDownPosition.Y = m_Mouse.Y;
            }

            m_MouseRightDown = m_Mouse.RightButton;

            if (m_CurrentAnimation2D != null
                && m_PlayAnimation == true)
            {
                m_CurrentAnimation2D.Update(GameTimeHelper.GameTimeToMilliseconds(gameTime));
            }

            base.Update(gameTime);

            m_Mouse.ScrollWheelValueReset();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime)
        {
            float elpasedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

            if (m_CurrentAnimation2D != null)
            {
                m_SpriteBatch.Begin(SpriteSortMode.Immediate,
                        BlendState.NonPremultiplied, //AlphaBlend need texture to be compiled with some options
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullCounterClockwise);

                if (m_CurrentAnimation2D.CurrentFrameIndex > 0
                    && DisplayPreviousFrame == true)
                {
                    DrawSprite2D(
                        m_CurrentAnimation2D.GetFrames()[m_CurrentAnimation2D.CurrentFrameIndex - 1].SpriteId,
                        new Color(1.0f, 0.5f, 1.0f, 0.5f),
                        0.6f, Color.LightYellow);
                }

                DrawSprite2D(m_CurrentAnimation2D.CurrentSpriteId,
                    Color.White, 0.5f, Color.Yellow);

                m_SpriteBatch.End();
            }

            base.Draw(gameTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spriteID_"></param>
        private void DrawSprite2D(int spriteID_, Color color_, float layer_, Color Origincolor_)
        {
            Sprite2D sprite2D = m_Sprites[spriteID_];

            if (sprite2D != null
                && sprite2D.Texture2D != null)
            {
                Vector2 hotspot = Vector2.Zero;
                hotspot.X = sprite2D.HotSpot.X;
                hotspot.Y = sprite2D.HotSpot.Y;
                Rectangle? rect = sprite2D.PositionInTexture;

                m_SpriteBatch.Draw(sprite2D.Texture2D, m_SpritePosition, rect, color_,
                    0.0f, hotspot, m_Zoom, SpriteEffects.None, layer_);

                if (DisplayOrigin == true)
                {
                    float x = m_SpritePosition.X;
                    float y = m_SpritePosition.Y;
                    m_Line2DRenderer.DrawLine(m_SpriteBatch, Color.Yellow,
                        new Vector2(x, 0), new Vector2(x, Game.GraphicsDevice.Viewport.Height), 0.9f);

                    m_Line2DRenderer.DrawLine(m_SpriteBatch, Color.Yellow,
                        new Vector2(0, y), new Vector2(Game.GraphicsDevice.Viewport.Width, y), 0.9f);
                }
            }
        }


        /// <summary>
        /// Use to change the current Sprite2D in
        /// the thread of XNA
        /// </summary>
        private void ChangeCurrentAnimation2DIfNeeded()
        {
            if (m_NeedChangeAnimation2D == false)
            {
                return;
            }

            m_Sprites.Clear();

            m_OriginalAnimation2D = m_ChangeCurrentAnimation;
            m_CurrentAnimation2D = (Animation2D)m_ChangeCurrentAnimation.Clone();
            m_SpritePosition.X = Game.GraphicsDevice.Viewport.Width / 2;
            m_SpritePosition.Y = Game.GraphicsDevice.Viewport.Height / 2;

            foreach (Frame2D f in m_CurrentAnimation2D.GetFrames())
            {
                BaseObject b = Engine.Instance.ObjectManager.GetObjectById(f.SpriteId);

                if (b == null)
                {
                    throw new InvalidOperationException("Animation2DEditorComponent.ChangeCurrentAnimation2DIfNeeded() : can't find the Sprite2D with he ID " + f.SpriteId);
                }

                Sprite2D sprite = b.Clone() as Sprite2D; //use as operator, else do an other copy !                
                sprite.LoadTextureFile(Game.GraphicsDevice);
                m_Sprites.Add(f.SpriteId, sprite);
            }

            m_NeedChangeAnimation2D = false;
            m_ChangeCurrentAnimation = null;

            if (CurrentAnimationSetted != null)
            {
                CurrentAnimationSetted.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectPath_"></param>
        /// <param name="anim2D_"></param>
        public void SetCurrentAnimation2D(string objectPath_, Animation2D anim2D_)
        {
            m_ChangeCurrentAnimation = anim2D_;
            m_ObjectPath = objectPath_;
            m_NeedChangeAnimation2D = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsAnimation2DChange()
        {
            if (m_CurrentAnimation2D != null)
            {
                return !(m_CurrentAnimation2D.CompareTo(m_OriginalAnimation2D));
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ApplyAnimation2DChanges()
        {
            if (m_CurrentAnimation2D != null)
            {
                Engine.Instance.ObjectManager.Replace(m_ObjectPath, m_CurrentAnimation2D);
            }
        }

    }
}
