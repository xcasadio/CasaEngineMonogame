using CasaEngine.Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.GUI.Neoforce;

namespace CasaEngine.Framework.Game.Components;

public class UserInterfaceComponent : DrawableGameComponent, IGameComponentResizable
{
    private readonly CasaEngineGame _game;
    private SpriteRendererComponent? _spriteRendererComponent;

    public Manager UINeoForceManager { get; private set; }

    public UserInterfaceComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        _game = Game as CasaEngineGame;
        game.Components.Add(this);

        UpdateOrder = (int)ComponentUpdateOrder.GUI;
        DrawOrder = (int)ComponentDrawOrder.GUI;
    }

    public override void Initialize()
    {
        UINeoForceManager = new Manager(_game, Game.Services.GetService<IGraphicsDeviceService>(), "Default",
            new AssetContentManagerAdapter(_game.AssetContentManager));
        UINeoForceManager.UpdateOrder = (int)ComponentUpdateOrder.GUI;
        UINeoForceManager.DrawOrder = (int)ComponentDrawOrder.GUIBegin;
        _game.Components.Add(UINeoForceManager);
        //UINeoForceManager.Visible = false;
        UINeoForceManager.SkinDirectory = Path.Combine(EngineEnvironment.ProjectPath, "Skins");
        UINeoForceManager.LayoutDirectory = Path.Combine(EngineEnvironment.ProjectPath, "Layout");

        UINeoForceManager.AutoCreateRenderTarget = true;
        UINeoForceManager.TargetFrames = 60;
        UINeoForceManager.ShowSoftwareCursor = true;
        //UINeoForceManager.GlobalDepth = 1.0f;

        OnScreenResized(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        base.LoadContent();

        _spriteRendererComponent = Game.GetDrawableGameComponent<SpriteRendererComponent>();
    }

    public override void Draw(GameTime gameTime)
    {
        if (!UINeoForceManager.DeviceReset)
        {
            UINeoForceManager.EndDraw();
            //_spriteRendererComponent.DrawDirectly(_Game.UserInterfaceComponent.UINeoForceManager.RenderTarget);
        }
    }

    public void OnScreenResized(int width, int height)
    {
        UINeoForceManager.OnScreenResized(width, height);
    }
}