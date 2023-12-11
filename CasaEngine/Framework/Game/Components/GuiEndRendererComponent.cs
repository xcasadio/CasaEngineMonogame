using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Game.Components;

public class GuiEndRendererComponent : DrawableGameComponent
{
    private readonly CasaEngineGame _game;

    public GuiEndRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        _game = Game as CasaEngineGame;
        game.Components.Add(this);

        UpdateOrder = (int)ComponentUpdateOrder.GUI;
        DrawOrder = (int)ComponentDrawOrder.GUIEnd;
    }

    public override void Draw(GameTime gameTime)
    {
        _game.GameManager.UiManager.EndDraw();
    }
}