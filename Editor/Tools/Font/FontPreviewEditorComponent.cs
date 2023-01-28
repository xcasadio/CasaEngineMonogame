
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Editor.Game;
using Microsoft.Xna.Framework;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Asset.Fonts;
using CasaEngine.Game;
using CasaEngine.CoreSystems.Game;
using Color = Microsoft.Xna.Framework.Color;

namespace Editor.Tools.Font
{
    /// <summary>
    /// 
    /// </summary>
    class FontPreviewEditorComponent
        : CasaEngine.Game.DrawableGameComponent
    {
        CasaEngine.Asset.Fonts.Font m_Font, m_NewFont;
        bool m_NeedChangeFont = false;
        SpriteBatch m_SpriteBatch;
        string m_Text;

        /// <summary>
        /// 
        /// </summary>
        public CasaEngine.Asset.Fonts.Font Font
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
                        strbld.Append((char)c.ID);
                    }

                    m_Text = strbld.ToString();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="game_"></param>
        internal FontPreviewEditorComponent(CustomGameEditor game_)
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

            foreach (CasaEngine.Game.GameComponent component in Game.Components)
            {
                if (component is Grid2DComponent)
                {
                    component.Enabled = false;
                    (component as CasaEngine.Game.DrawableGameComponent).Visible = false;
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

                m_Font.LoadTexture(Engine.Instance.ProjectManager.ProjectPath, Game.GraphicsDevice);
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
