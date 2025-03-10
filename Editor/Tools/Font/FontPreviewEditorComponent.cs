﻿using System.Text;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets.Fonts;
using CasaEngine.Framework.Game;
using Editor.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using CasaEngine.Framework.Graphics2D;

namespace Editor.Tools.Font
{
    /// <summary>
    /// 
    /// </summary>
    class FontPreviewEditorComponent
        : DrawableGameComponent
    {
        CasaEngine.Framework.Assets.Fonts.Font m_Font, m_NewFont;
        bool m_NeedChangeFont = false;
        SpriteBatch m_SpriteBatch;
        string m_Text;

        /// <summary>
        /// 
        /// </summary>
        public CasaEngine.Framework.Assets.Fonts.Font Font
        {
            get { return m_Font; }
            set
            {
                m_NewFont = value;
                m_NeedChangeFont = true;

                if (m_NewFont != null)
                {
                    StringBuilder strbld = new StringBuilder();
                    foreach (FontChar c in m_NewFont.Chars)
                    {
                        strbld.Append((char)c.Id);
                    }

                    m_Text = strbld.ToString();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="game_"></param>
        internal FontPreviewEditorComponent(Microsoft.Xna.Framework.Game game_)
            : base(game_)
        {
            game_.Components.Add(this);
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void LoadContent()
        {
            m_SpriteBatch = new SpriteBatch(Game.GraphicsDevice);

            base.LoadContent();

            foreach (var component in Game.Components)
            {
                if (component is Grid2DComponent grid2DComponent)
                {
                    grid2DComponent.Enabled = false;
                    grid2DComponent.Visible = false;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            if (m_NeedChangeFont == true
                && m_NewFont != null)
            {
                m_NeedChangeFont = false;
                m_Font = m_NewFont;
                m_NewFont = null;

                m_Font.LoadTexture(CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath, Game.GraphicsDevice);
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime)
        {
            if (m_Font != null)
            {
                m_SpriteBatch.Begin(SpriteSortMode.Immediate,
                    BlendState.NonPremultiplied, //AlphaBlend need texture to be compiled with some options
                    SamplerState.LinearClamp,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise);

                BmFontRenderer.DrawString(m_SpriteBatch, m_Font, m_Text, Vector2.One * 10, Color.White);

                m_SpriteBatch.End();
            }

            base.Draw(gameTime);
        }
    }
}
