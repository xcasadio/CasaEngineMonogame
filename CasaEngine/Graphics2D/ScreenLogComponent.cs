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
        readonly List<LogText> _logText = new();
        Renderer2DComponent _renderer2DComponent = null;





        public ScreenLogComponent(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            if (game == null)
            {
                throw new ArgumentNullException("ScreenLogComponent : Game is null");
            }

            game.Components.Add(this);

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
                    GameHelper.RemoveGameComponent<ScreenLogComponent>(Game);
                }
            }

            base.Dispose(disposing);
        }

        public void AddText(string text)
        {
            AddText(text, Engine.Instance.DefaultSpriteFont, Color.White);
        }

        public void AddText(string text, SpriteFont spriteFont)
        {
            AddText(text, spriteFont, Color.White);
        }

        public void AddText(string text, Color color)
        {
            AddText(text, Engine.Instance.DefaultSpriteFont, color);
        }

        public void AddText(string text, SpriteFont spriteFont, Color color)
        {
            LogText log = new LogText();
            log.Color = color;
            log.Text = text;
            log.SpriteFont = spriteFont;
            _logText.Add(log);
        }


        protected override void LoadContent()
        {
            _renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Engine.Instance.Game);
            base.LoadContent();
        }

        public override void Update(GameTime gameTime)
        {
            List<LogText> toDelete = new List<LogText>();

            float elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

            for (int i = 0; i < _logText.Count; i++)
            {
                _logText[i].Time += elapsedTime;

                if (_logText[i].Time > 5)
                {
                    toDelete.Add(_logText[i]);
                }
            }

            foreach (LogText log in toDelete)
            {
                _logText.Remove(log);
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (_logText.Count == 0)
            {
                return;
            }

            Vector2 pos = new Vector2(10, (float)GraphicsDevice.Viewport.Height * 0.75f);

            for (int i = _logText.Count - 1; i >= 0; i--)
            {
                _renderer2DComponent.AddText2D(_logText[i].SpriteFont, _logText[i].Text, pos, 0.0f, Vector2.One, _logText[i].Color, 0.99f);
                pos.Y -= _logText[i].SpriteFont.MeasureString(_logText[i].Text).Y + 5;
            }

            base.Draw(gameTime);
        }


    }

    class LogText
    {
        public string Text = string.Empty;
        public SpriteFont SpriteFont = null;
        public Color Color = Color.White;
        public float Time = 0f;
    }
}
