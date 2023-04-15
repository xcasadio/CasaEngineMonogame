using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.Game;
using CasaEngine.Core.Helper;

namespace CasaEngine.Framework.Graphics2D;

public class ScreenLogComponent
    : DrawableGameComponent
{
    private readonly List<LogText> _logText = new();
    private Renderer2dComponent _renderer2dComponent;
    private SpriteFont? _font;

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
        if (disposing)
        {
            lock (this)
            {
                // IsRemoved self from the service container.
                Game.RemoveGameComponent<ScreenLogComponent>();
            }
        }

        base.Dispose(disposing);
    }

    public void AddText(string text)
    {
        AddText(text, _font, Color.White);
    }

    public void AddText(string text, SpriteFont spriteFont)
    {
        AddText(text, spriteFont, Color.White);
    }

    public void AddText(string text, Color color)
    {
        AddText(text, _font, color);
    }

    public void AddText(string text, SpriteFont spriteFont, Color color)
    {
        var log = new LogText();
        log.Color = color;
        log.Text = text;
        log.SpriteFont = spriteFont;
        _logText.Add(log);
    }

    protected override void LoadContent()
    {
        _font = ((CasaEngineGame)Game).GameManager.DefaultSpriteFont;
        _renderer2dComponent = Game.GetGameComponent<Renderer2dComponent>();
        base.LoadContent();
    }

    public override void Update(GameTime gameTime)
    {
        var toDelete = new List<LogText>();

        var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

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
            _renderer2dComponent.AddText2d(_logText[i].SpriteFont, _logText[i].Text, pos, 0.0f, Vector2.One, _logText[i].Color, 0.99f);
            pos.Y -= _logText[i].SpriteFont.MeasureString(_logText[i].Text).Y + 5;
        }

        base.Draw(gameTime);
    }

}

internal class LogText
{
    public string Text = string.Empty;
    public SpriteFont SpriteFont;
    public Color Color = Color.White;
    public float Time;
}