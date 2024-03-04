using CasaEngine.Core.Log;
using CasaEngine.Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using CasaEngine.Framework.GUI.Neoforce;
using CasaEngine.Framework.GUI.NoesisGUIWrapper;
using CasaEngine.Framework.GUI.NoesisGUIWrapper.Config;
using CasaEngine.Framework.GUI.NoesisGUIWrapper.Providers;

namespace CasaEngine.Framework.Game.Components;

public class UserInterfaceComponent : DrawableGameComponent, IGameComponentResizable
{
    private readonly CasaEngineGame _game;
    private SpriteRendererComponent? _spriteRendererComponent;

    public Manager UINeoForceManager { get; private set; }
    public NoesisWrapper UINoesisWrapper { get; private set; }

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


        CreateNoesisGUI();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        base.LoadContent();

        _spriteRendererComponent = Game.GetDrawableGameComponent<SpriteRendererComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        UINoesisWrapper.UpdateInput(gameTime, isWindowActive: Game.IsActive);
        UINoesisWrapper.Update(gameTime);
        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        if (!UINeoForceManager.DeviceReset)
        {
            UINeoForceManager.EndDraw();
            //_spriteRendererComponent.DrawDirectly(_Game.UserInterfaceComponent.UINeoForceManager.RenderTarget);
        }

        UINoesisWrapper.PreRender();
        UINoesisWrapper.Render();
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

    private void CreateNoesisGUI()
    {
        // ensure the Noesis.App assembly is loaded otherwise NoesisGUI will be unable to located "Window" type
        System.Reflection.Assembly.Load("Noesis.App");

        // TODO: input your license details here
        //go to https://www.noesisengine.com/trial/
        var licenseName = "xav";
        var licenseKey = "ciab2xRrm9t4HQHSUn+aa23Pv2SNkzISdVb6D7BWxwNhM38V";
        NoesisWrapper.Init(licenseName, licenseKey);

        var providerManager = new NoesisProviderManager(
            new FolderXamlProvider(EngineEnvironment.ProjectPath),
            new FolderFontProvider(EngineEnvironment.ProjectPath),
            new FolderTextureProvider(EngineEnvironment.ProjectPath, GraphicsDevice));

        var config = new NoesisConfig(
            Game.Window,
            Game.GraphicsDevice,
            providerManager,
            rootXamlFilePath: "MainWindow.xaml",
            themeXamlFilePath: "Resources.xaml",
            currentTotalGameTime: new TimeSpan(),
            callbackGetViewport: GetMainComposerViewportForUI,
            onErrorMessageReceived: NoesisGUIErrorMessageReceivedHandler,
            onDevLogMessageReceived: NoesisGUIDevLogMessageReceivedHandler,
            onUnhandledException: NoesisGUIUnhandledExceptionHandler);

        //config.SetupInputFromWindows();

        UINoesisWrapper = new NoesisWrapper(config);
        UINoesisWrapper.View.IsPPAAEnabled = true;
    }

    private void DestroyNoesisGUI()
    {
        if (UINoesisWrapper == null)
        {
            return;
        }

        UINoesisWrapper.Dispose();
        UINoesisWrapper = null;
    }

    private Viewport GetMainComposerViewportForUI()
    {
        return new Viewport(0, 0, _game.ScreenSizeWidth, _game.ScreenSizeHeight);
    }

    private void NoesisGUIDevLogMessageReceivedHandler(string message)
    {
        if (message.IndexOf("Does not contain a property", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("returned null", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // binding error
            return;
        }

        Debug.WriteLine("NoesisGUI DEV log: " + message);
    }
}