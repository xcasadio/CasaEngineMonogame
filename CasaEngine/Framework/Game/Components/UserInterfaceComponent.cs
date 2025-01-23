using CasaEngine.Core.Log;
using CasaEngine.Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
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

        UINeoForceManager.OnScreenResize(GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferWidth);

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

    private static void NoesisGUIErrorMessageReceivedHandler(string errorMessage)
    {
        if (errorMessage.IndexOf("Binding", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // binding error
            //Global.Logger.Write("NoesisGUI error: " + errorMessage, LogSeverity.Info);
            return;
        }

        if (errorMessage.IndexOf("fallback texture", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // async texture loading
            return;
        }

        Logs.WriteError("NoesisGUI error: " + errorMessage);
    }

    private static void NoesisGUIUnhandledExceptionHandler(Exception exception)
    {
        Logs.WriteException(exception);
    }
}