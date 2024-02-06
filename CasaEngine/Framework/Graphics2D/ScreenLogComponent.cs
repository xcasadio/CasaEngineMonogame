using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.Game;
using FontStashSharp;

namespace CasaEngine.Framework.Graphics2D;

public class ScreenLogComponent : DrawableGameComponent
{
    private class LogText
    {
        public string Text = string.Empty;
        public SpriteFontBase SpriteFont;
        public Color Color = Color.White;
        public float Time;
    }

    private readonly List<LogText> _logText = new();
    private Renderer2dComponent _renderer2dComponent;
    private SpriteFontBase? _font;

    public ScreenLogComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }


        game.Components.Add(this);

        UpdateOrder = (int)ComponentUpdateOrder.ScreenLogComponent;
        DrawOrder = (int)ComponentDrawOrder.ScreenLogComponent;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (this)
            {
                Game.RemoveGameComponent<ScreenLogComponent>();
            }
        }

        base.Dispose(disposing);
    }

    public void AddText(string text)
    {
        AddText(text, _font, Color.White);
    }

    public void AddText(string text, SpriteFontBase spriteFont)
    {
        AddText(text, spriteFont, Color.White);
    }

    public void AddText(string text, Color color)
    {
        AddText(text, _font, color);
    }

    public void AddText(string text, SpriteFontBase spriteFont, Color color)
    {
        var log = new LogText
        {
            Color = color,
            Text = text,
            SpriteFont = spriteFont
        };
        _logText.Add(log);
    }

    protected override void LoadContent()
    {
        _font = ((CasaEngineGame)Game).FontSystem.GetFont(10);
        _renderer2dComponent = Game.GetGameComponent<Renderer2dComponent>();
        base.LoadContent();
    }

    public override void Update(GameTime gameTime)
    {
        var toDelete = new List<LogText>();

        var elapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);

        for (var i = 0; i < _logText.Count; i++)
        {
            _logText[i].Time += elapsedTime;

            if (_logText[i].Time > 5)
            {
                toDelete.Add(_logText[i]);
            }
        }

        foreach (var log in toDelete)
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

        var pos = new Vector2(10, GraphicsDevice.Viewport.Height * 0.75f);

        for (var i = _logText.Count - 1; i >= 0; i--)
        {
            _renderer2dComponent.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _renderer2dComponent.SpriteBatch.DrawString(_logText[i].SpriteFont, _logText[i].Text, pos, _logText[i].Color);
            _renderer2dComponent.SpriteBatch.End();

            pos.Y -= _logText[i].SpriteFont.MeasureString(_logText[i].Text).Y + 5;
        }

        base.Draw(gameTime);
    }

}
