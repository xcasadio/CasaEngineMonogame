using CasaEngine.Core.Helper;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Debugger;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.FrontEnd.Screen;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game;

public class GameManager
{
    private readonly IGraphicsDeviceService _graphicsDeviceService;
    private GraphicsDeviceManager _graphicsDeviceManager;
    private Renderer2DComponent _renderer2DComponent;
    private ScreenManagerComponent _screenManagerComponent;
    private InputComponent _inputComponent;
    private ShapeRendererComponent _shapeRendererComponent;
    private MeshRendererComponent _meshRendererComponent;

    private string ProjectFile { get; set; } = string.Empty;

#if !FINAL
    protected string ContentPath = string.Empty;
#endif

    public GameManager(CasaEngineGame game)
    {
        Engine.Instance.Game = game;
        _graphicsDeviceManager = new GraphicsDeviceManager(game);
        _graphicsDeviceManager.PreparingDeviceSettings += PreparingDeviceSettings;
        _graphicsDeviceManager.DeviceReset += OnDeviceReset;

    }

    public GameManager(CasaEngineGame game, IGraphicsDeviceService graphicsDeviceService)
    {
        Engine.Instance.Game = game;
        _graphicsDeviceService = graphicsDeviceService;
        graphicsDeviceService.GraphicsDevice.DeviceReset += OnDeviceReset;
        if (game.Services.GetService<IGraphicsDeviceService>() != null)
        {
            game.Services.RemoveService(typeof(IGraphicsDeviceService));
        }
        game.Services.AddService(typeof(IGraphicsDeviceService), (object)_graphicsDeviceService);
    }

    private void PreparingDeviceSettings(object? sender, PreparingDeviceSettingsEventArgs e)
    {
        e.GraphicsDeviceInformation.PresentationParameters.BackBufferWidth = Engine.Instance.ProjectSettings.DebugWidth;
        e.GraphicsDeviceInformation.PresentationParameters.BackBufferHeight = Engine.Instance.ProjectSettings.DebugHeight;
        //e.GraphicsDeviceInformation.PresentationParameters.DeviceWindowHandle = IntPtr.Zero;

        e.GraphicsDeviceInformation.GraphicsProfile = GraphicsAdapter.Adapters
            .Any(x => x.IsProfileSupported(GraphicsProfile.HiDef)) ? GraphicsProfile.HiDef : GraphicsProfile.Reach;
    }

    public void OnScreenResized(int width, int height)
    {
        if (GameInfo.Instance.CurrentWorld != null)
        {
            foreach (var entity in GameInfo.Instance.CurrentWorld.Entities)
            {
                entity.ScreenResized(width, height);
            }
        }
    }

    private void OnDeviceReset(object? sender, EventArgs e)
    {
        var graphicsDevice = (GraphicsDevice)sender;
        OnScreenResized(graphicsDevice.PresentationParameters.BackBufferWidth, graphicsDevice.PresentationParameters.BackBufferHeight);
    }

    public void Initialize()
    {
        Engine.Instance.AssetContentManager = new AssetContentManager();
        Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
        Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());

        var game = Engine.Instance.Game;
        DebugSystem.Initialize(game);

        _renderer2DComponent = new Renderer2DComponent(game);
        _inputComponent = new InputComponent(game);
        _screenManagerComponent = new ScreenManagerComponent(game);
        _shapeRendererComponent = new ShapeRendererComponent(game);
        _meshRendererComponent = new MeshRendererComponent(game);
        var gizmoComponent = new GizmoComponent(game);

#if EDITOR
        new GridComponent(game);
#endif

#if !FINAL
        var args = Environment.CommandLine.Split(' ');

        if (args.Length > 1)
        {
            ProjectFile = args[1];
        }

        ContentPath = args.Length > 2 ? args[2] : Path.Combine(Directory.GetCurrentDirectory(), "Content");
#else
        ContentPath = "Content";
#endif

        Engine.Instance.Game.Content.RootDirectory = ContentPath;
        //CasaEngine.Game.Engine.Instance.ProjectManager.Load(ProjectFile);
        //TODO : create hierarchy of the project
        if (!string.IsNullOrWhiteSpace(ProjectFile))
        {
            Engine.Instance.ProjectSettings.Load(ProjectFile);
        }

        Engine.Instance.Game.Window.Title = Engine.Instance.ProjectSettings.WindowTitle;
        Engine.Instance.Game.Window.AllowUserResizing = Engine.Instance.ProjectSettings.AllowUserResizing;
        Engine.Instance.Game.IsFixedTimeStep = Engine.Instance.ProjectSettings.IsFixedTimeStep;
        Engine.Instance.Game.IsMouseVisible = Engine.Instance.ProjectSettings.IsMouseVisible;

        if (!string.IsNullOrWhiteSpace(Engine.Instance.ProjectSettings.GameplayDllName))
        {
            Engine.Instance.PluginManager.Load(Engine.Instance.ProjectSettings.GameplayDllName);
        }
    }

    public void BeginLoadContent()
    {
        Engine.Instance.AssetContentManager.Initialize(Engine.Instance.Game.GraphicsDevice);
        Engine.Instance.AssetContentManager.RootDirectory = ContentPath;

        //CasaEngine.Game.Engine.Instance.UiManager.Initialize(GraphicsDevice, Window.Handle, Window.ClientBounds);

        Engine.Instance.SpriteBatch = new SpriteBatch(Engine.Instance.Game.GraphicsDevice);
        //TODO : defaultSpriteFont
        //GameInfo.Instance.DefaultSpriteFont = Content.Load<SpriteFont>("Content/defaultSpriteFont");

        //_renderer2DComponent.SpriteBatch = Engine.Instance.SpriteBatch;
        //var renderTarget = new RenderTarget2D(Engine.Instance.Game.GraphicsDevice, 1024, 768, false,
        //    SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        //Engine.Instance.Game.GraphicsDevice.SetRenderTarget(renderTarget);
    }

    public void EndLoadContent()
    {
#if !EDITOR
        if (GameInfo.Instance.CurrentWorld == null)
        {
            if (string.IsNullOrWhiteSpace(Engine.Instance.ProjectSettings.FirstWorldLoaded))
            {
                throw new InvalidOperationException("FirstWorldLoaded is undefined");
            }

            GameInfo.Instance.CurrentWorld = new World.World();
            GameInfo.Instance.CurrentWorld.Load(Engine.Instance.ProjectSettings.FirstWorldLoaded);
        }
#else
        GameInfo.Instance.CurrentWorld = new World.World();

        var entity = new Entity();
        entity.Name = "Camera";
        var camera = new ArcBallCameraComponent(entity);
        entity.ComponentManager.Components.Add(camera);
        GameInfo.Instance.ActiveCamera = camera;
        camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        GameInfo.Instance.CurrentWorld.AddObjectImmediately(entity);
#endif

        GameInfo.Instance.CurrentWorld.Initialize();

        _shapeRendererComponent.SetCurrentPhysicsWorld(GameInfo.Instance.CurrentWorld.PhysicWorld);
    }

    public void BeginUpdate(GameTime gameTime)
    {
#if !FINAL
        DebugSystem.Instance.TimeRuler.StartFrame();
        DebugSystem.Instance.TimeRuler.BeginMark("Update", Color.Blue);
#endif

        //if (Keyboard.GetState().IsKeyDown(Keys.OemQuotes))
        //    DebugSystem.Instance.DebugCommandUI.Show(); 

        var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);
        GameInfo.Instance.CurrentWorld?.Update(elapsedTime);
        //CasaEngine.Game.Engine.Instance.UiManager.Update(time);
    }

    public void EndUpdate(GameTime gameTime)
    {
#if !FINAL
        DebugSystem.Instance.TimeRuler.EndMark("Update");
#endif
    }

    public void BeginDraw(GameTime gameTime)
    {
#if !FINAL
        DebugSystem.Instance.TimeRuler.StartFrame();
        DebugSystem.Instance.TimeRuler.BeginMark("Draw", Color.Blue);
#endif

        //Engine.Instance.Game.GraphicsDevice.Clear(Color.CornflowerBlue);
        Engine.Instance.Game.GraphicsDevice.Clear(Color.Black);

        var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);
        GameInfo.Instance.CurrentWorld?.Draw(elapsedTime);
        //CasaEngine.Game.Engine.Instance.UiManager.PreRenderControls();
    }

    public void EndDraw(GameTime gameTime)
    {
#if !FINAL
        DebugSystem.Instance.TimeRuler.EndMark("Draw");
#endif
    }

#if EDITOR
    public void SetInputProvider(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider)
    {
        _inputComponent.SetProviders(keyboardStateProvider, mouseStateProvider);
    }
#endif
}