using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Game;
using CasaEngine.CoreSystems.Game;
using CasaEngineCommon.Helper;

namespace CasaEngine.Graphics2D
{
    public class ScreenLogComponent
        : Microsoft.Xna.Framework.DrawableGameComponent
    {
        readonly List<LogText> m_LogText = new List<LogText>();
        Renderer2DComponent m_Renderer2DComponent = null;





        public ScreenLogComponent(Microsoft.Xna.Framework.Game game_)
            : base(game_)
        {
            if (game_ == null)
            {
                throw new ArgumentNullException("ScreenLogComponent : Game is null");
            }

            game_.Components.Add(this);

            UpdateOrder = (int)ComponentUpdateOrder.Renderer2DComponent;
            DrawOrder = (int)ComponentDrawOrder.Renderer2DComponent;
        }



        protected override void Dispose(bool disposing)
        {
            if (disposing == true)
            {
                lock (this)
                {
                    // Remove self from the service container.
                    GameHelper.RemoveGameComponent<ScreenLogComponent>(this.Game);
                }
            }

            base.Dispose(disposing);
        }

        public void AddText(string text_)
        {
            AddText(text_, Engine.Instance.DefaultSpriteFont, Color.White);
        }

        public void AddText(string text_, SpriteFont spriteFont_)
        {
            AddText(text_, spriteFont_, Color.White);
        }

        public void AddText(string text_, Color color_)
        {
            AddText(text_, Engine.Instance.DefaultSpriteFont, color_);
        }

        public void AddText(string text_, SpriteFont spriteFont_, Color color_)
        {
            LogText log = new LogText();
            log.color = color_;
            log.text = text_;
            log.spriteFont = spriteFont_;
            m_LogText.Add(log);
        }


        protected override void LoadContent()
        {
            m_Renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Engine.Instance.Game);
            base.LoadContent();
        }

        public override void Update(GameTime gameTime)
        {
            List<LogText> ToDelete = new List<LogText>();

            float elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

            for (int i = 0; i < m_LogText.Count; i++)
            {
                m_LogText[i].time += elapsedTime;

                if (m_LogText[i].time > 5)
                {
                    ToDelete.Add(m_LogText[i]);
                }
            }

            foreach (LogText log in ToDelete)
            {
                m_LogText.Remove(log);
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (m_LogText.Count == 0)
            {
                return;
            }

            Vector2 pos = new Vector2(10, (float)GraphicsDevice.Viewport.Height * 0.75f);

            for (int i = m_LogText.Count - 1; i >= 0; i--)
            {
                m_Renderer2DComponent.AddText2D(m_LogText[i].spriteFont, m_LogText[i].text, pos, 0.0f, Vector2.One, m_LogText[i].color, 0.99f);
                pos.Y -= m_LogText[i].spriteFont.MeasureString(m_LogText[i].text).Y + 5;
            }

            base.Draw(gameTime);
        }


    }

    class LogText
    {
        public string text = string.Empty;
        public SpriteFont spriteFont = null;
        public Color color = Color.White;
        public float time = 0f;
    }
}
