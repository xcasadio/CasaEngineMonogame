using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game;

public class CasaEngineGame : Microsoft.Xna.Framework.Game
{
    public GameManager GameManager { get; }

    public CasaEngineGame()
    {
        GameManager = new GameManager(this);
    }

    public CasaEngineGame(IGraphicsDeviceService graphicsDeviceService)
    {
        GameManager = new GameManager(this, graphicsDeviceService);
    }

    protected override void Initialize()
    {
        GameManager.Initialize();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        GameManager.BeginLoadContent();
        base.LoadContent();
        GameManager.EndLoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        GameManager.BeginUpdate(gameTime);
        base.Update(gameTime);
        GameManager.EndUpdate(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GameManager.BeginDraw(gameTime);
        base.Draw(gameTime);
        GameManager.EndDraw(gameTime);
    }
}