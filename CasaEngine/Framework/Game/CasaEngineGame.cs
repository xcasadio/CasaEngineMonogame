using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game;

public class CasaEngineGame : Microsoft.Xna.Framework.Game
{
    private readonly string? _projectFileName;
    public GameManager GameManager { get; }

    public CasaEngineGame(string? projectFileName = null, IGraphicsDeviceService? graphicsDeviceService = null)
    {
        _projectFileName = projectFileName;
        GameManager = new GameManager(this, graphicsDeviceService);
    }

    protected override void Initialize()
    {
        GameManager.Initialize();

        if (!string.IsNullOrWhiteSpace(_projectFileName))
        {
            GameSettings.ProjectManager.Load(_projectFileName);
        }

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