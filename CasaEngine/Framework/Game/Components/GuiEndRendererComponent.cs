using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Game.Components;

public class GuiEndRendererComponent : DrawableGameComponent
{
    private readonly CasaEngineGame _game;
    private SpriteRendererComponent? _spriteRendererComponent;

    public GuiEndRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        _game = Game as CasaEngineGame;
        game.Components.Add(this);

        UpdateOrder = (int)ComponentUpdateOrder.GUI;
        DrawOrder = (int)ComponentDrawOrder.GUIEnd;
    }

    protected override void LoadContent()
    {
        base.LoadContent();

        _spriteRendererComponent = Game.GetDrawableGameComponent<SpriteRendererComponent>();
    }

    public override void Draw(GameTime gameTime)
    {
        if (!_game.UiManager.DeviceReset)
        {
            _game.UiManager.EndDraw();
            //_spriteRendererComponent.DrawDirectly(_Game.UiManager.RenderTarget);
        }
    }
}