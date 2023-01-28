
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Game;

namespace Editor.Game
{
    /// <summary>
    /// 
    /// </summary>
    public class Grid2DComponent
        : CasaEngine.Game.DrawableGameComponent
    {

        private Line2DRenderer m_Line2DRenderer;
        private SpriteBatch m_SpriteBatch;
        private Microsoft.Xna.Framework.Color m_ColorLine = new Microsoft.Xna.Framework.Color(1.0f, 1.0f, 1.0f, 0.5f);



        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Microsoft.Xna.Framework.Color ColorLine
        {
            get { return m_ColorLine; }
            set { m_ColorLine = value; }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="game_"></param>
        public Grid2DComponent(CustomGame game_)
            : base(game_)
        {
            game_.Components.Add(this);
        }



        /// <summary>
        /// 
        /// </summary>
        public override void Initialize()
        {
            m_Line2DRenderer = new Line2DRenderer();
            base.Initialize();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void LoadContent()
        {
            m_SpriteBatch = new SpriteBatch(Game.GraphicsDevice);
            m_Line2DRenderer.Init(Game.GraphicsDevice);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        /*public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime)
        {
            m_SpriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.Default, RasterizerState.CullNone, null);

            int dist = 50;
            int dx = (int)((float)GraphicsDevice.Viewport.Width / 50.0f) + 2;
            int dy = (int)((float)GraphicsDevice.Viewport.Height / 50.0f) + 2;

            for (int x = 0; x < dx; x++)
            {
                for (int y = 0; y < dy; y++)
                {
                    m_Line2DRenderer.DrawLine(m_SpriteBatch, m_ColorLine, new Vector2(x * dist, 0), new Vector2(x * dist, y * dist));
                    m_Line2DRenderer.DrawLine(m_SpriteBatch, m_ColorLine, new Vector2(0, y * dist), new Vector2(x * dist, y * dist));
                }
            }

            m_SpriteBatch.End();
        }

    }
}
