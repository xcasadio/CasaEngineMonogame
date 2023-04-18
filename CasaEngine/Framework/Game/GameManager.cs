using CasaEngine.Core.Helper;
using CasaEngine.Editor.Tools;
using CasaEngine.Engine.Input;
using CasaEngine.Engine.Physics2D;
using CasaEngine.Engine.Plugin;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Debugger;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.FrontEnd.Screen;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics2D;
using CasaEngine.Framework.Project;
using CasaEngine.Framework.UserInterface;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using EventArgs = System.EventArgs;

namespace CasaEngine.Framework.Game;

public class GameManager
{
    private readonly CasaEngineGame _game;
    public string[] Arguments { get; set; }
    private string ProjectFile { get; set; } = string.Empty;
    public GraphicsDeviceManager GraphicsDeviceManager { get; private set; }
    public AssetContentManager AssetContentManager { get; internal set; } = new();
    public ProjectManager ProjectManager { get; } = new();
    public ScreenManager ScreenManager { get; } = new();
    public UserInterfaceManager UiManager { get; } = new();
    public SpriteBatch? SpriteBatch { get; set; }
    public ProjectSettings ProjectSettings { get; } = new();
    public GraphicsSettings GraphicsSettings { get; } = new();
    public Physics2dSettings Physics2dSettings { get; } = new();
    public Physics3dSettings Physics3dSettings { get; } = new();
    public PluginManager PluginManager { get; }

#if !FINAL
    public SpriteFont? DefaultSpriteFont { get; set; }
    public string ContentPath = string.Empty;
#endif

#if EDITOR
    public BasicEffect? BasicEffect { get; set; }

    public ExternalToolManager ExternalToolManager { get; }
#endif

    public InputComponent InputComponent { get; private set; }
    public Renderer2dComponent Renderer2dComponent { get; private set; }
    public StaticMeshRendererComponent MeshRendererComponent { get; private set; }
    public ScreenManagerComponent ManagerComponent { get; private set; }
    public PhysicsEngineComponent PhysicsEngine { get; private set; }
    public Physics2dDebugViewRendererComponent Physics2dDebugViewRendererComponent { get; private set; }
    public PhysicsDebugViewRendererComponent PhysicsDebugViewRendererComponent { get; private set; }

    public GameManager(CasaEngineGame game, IGraphicsDeviceService? graphicsDeviceService)
    {
        _game = game;

        if (graphicsDeviceService == null)
        {
            var graphicsDeviceManager = new GraphicsDeviceManager(game);
            graphicsDeviceManager.PreparingDeviceSettings += PreparingDeviceSettings;
            graphicsDeviceManager.DeviceReset += OnDeviceReset;
        }
        else
        {
            graphicsDeviceService.GraphicsDevice.DeviceReset += OnDeviceReset;
            if (game.Services.GetService<IGraphicsDeviceService>() != null)
            {
                game.Services.RemoveService(typeof(IGraphicsDeviceService));
            }
            game.Services.AddService(typeof(IGraphicsDeviceService), graphicsDeviceService);
        }

        GraphicsDeviceManager = (GraphicsDeviceManager)game.GetService<IGraphicsDeviceManager>();

        PluginManager = new PluginManager(game);

#if EDITOR
        ExternalToolManager = new ExternalToolManager(game);
#endif
    }

    private void PreparingDeviceSettings(object? sender, PreparingDeviceSettingsEventArgs e)
    {
        e.GraphicsDeviceInformation.PresentationParameters.BackBufferWidth = ProjectSettings.DebugWidth;
        e.GraphicsDeviceInformation.PresentationParameters.BackBufferHeight = ProjectSettings.DebugHeight;

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
        AssetContentManager = new AssetContentManager();
        AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
        AssetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());
        AssetContentManager.Initialize(_game.GraphicsDevice);
        AssetContentManager.RootDirectory = ContentPath;

        DebugSystem.Initialize(_game);

        Renderer2dComponent = new Renderer2dComponent(_game);
        SpriteBatch = new SpriteBatch(_game.GraphicsDevice);
        Renderer2dComponent.SpriteBatch = SpriteBatch;
        InputComponent = new InputComponent(_game);
        ManagerComponent = new ScreenManagerComponent(_game);
        Physics2dDebugViewRendererComponent = new Physics2dDebugViewRendererComponent(_game);
        MeshRendererComponent = new StaticMeshRendererComponent(_game);
        PhysicsEngine = new PhysicsEngineComponent(_game);

#if EDITOR
        //In editor mode the game is in idle mode so we don't update physics
        PhysicsEngine.Enabled = false;
        PhysicsDebugViewRendererComponent = new PhysicsDebugViewRendererComponent(_game);
        var gizmoComponent = new GizmoComponent(_game);
        var gridComponent = new GridComponent(_game);
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

        _game.Content.RootDirectory = ContentPath;
        //CasaEngine.Game.ProjectManager.Load(ProjectFile);
        //TODO : create hierarchy of the project
        if (!string.IsNullOrWhiteSpace(ProjectFile))
        {
            ProjectSettings.Load(ProjectFile);
        }

        _game.Window.Title = ProjectSettings.WindowTitle;
        _game.Window.AllowUserResizing = ProjectSettings.AllowUserResizing;
        _game.IsFixedTimeStep = ProjectSettings.IsFixedTimeStep;
        _game.IsMouseVisible = ProjectSettings.IsMouseVisible;

        if (!string.IsNullOrWhiteSpace(ProjectSettings.GameplayDllName))
        {
            PluginManager.Load(ProjectSettings.GameplayDllName);
        }

        //CasaEngine.Game.UiManager.Initialize(GraphicsDevice, Window.Handle, Window.ClientBounds);

    }

    public void BeginLoadContent()
    {
        //TODO : defaultSpriteFont
        //GameInfo.Instance.DefaultSpriteFont = Content.Load<SpriteFont>("Content/defaultSpriteFont");

        //_renderer2dComponent.SpriteBatch = SpriteBatch;
        //var renderTarget = new RenderTarget2D(Game.GraphicsDevice, 1024, 768, false,
        //    SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        //Game.GraphicsDevice.SetRenderTarget(renderTarget);
    }

    public void EndLoadContent()
    {
#if !EDITOR
        if (GameInfo.Instance.CurrentWorld == null)
        {
            if (string.IsNullOrWhiteSpace(ProjectSettings.FirstWorldLoaded))
            {
                throw new InvalidOperationException("FirstWorldLoaded is undefined");
            }

            GameInfo.Instance.CurrentWorld = new World.World();
            GameInfo.Instance.CurrentWorld.Load(ProjectSettings.FirstWorldLoaded);
        }

        GameInfo.Instance.CurrentWorld.Initialize(_game);
        //_physics2dDebugViewRendererComponent.SetCurrentPhysicsWorld(GameInfo.Instance.CurrentWorld.Physic2dWorld);
#else
        //in editor the active camera is debug camera and it isn't belong to the world
        var entity = new Entity { Name = "Camera" };
        var camera = new ArcBallCameraComponent(entity);
        entity.ComponentManager.Components.Add(camera);
        GameInfo.Instance.ActiveCamera = camera;
        camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        camera.Initialize(_game);
#endif

        //TEST
        //var world = GameInfo.Instance.CurrentWorld;
        //
        //var entity = new Entity();
        //entity.Name = "Entity camera";
        //var camera = new ArcBallCameraComponent(entity);
        //entity.ComponentManager.Components.Add(camera);
        //GameInfo.Instance.ActiveCamera = camera;
        //camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        //world.AddEntityImmediately(entity);
        //
        //entity = new Entity();
        //entity.Name = "Entity box";
        ////entity.Coordinates.LocalPosition += Vector3.Up * 0.5f;
        //var meshComponent = new StaticMeshComponent(entity);
        //entity.ComponentManager.Components.Add(meshComponent);
        //meshComponent.Mesh = new BoxPrimitive(Game.GraphicsDevice).CreateMesh();
        //meshComponent.Mesh.Texture = Game.Content.Load<Texture2D>("checkboard");
        ////
        //entity.ComponentManager.Components.Add(new PhysicsComponent(entity)
        //{
        //    Shape = new Box { Height = 1f, Width = 1f, Length = 1f }
        //});
        ////
        //world.AddEntityImmediately(entity);

        //
        //entity = new Entity();
        //entity.Name = "Entity box 2";
        //entity.Coordinates.LocalPosition += Vector3.UnitX * 2.0f;
        //meshComponent = new MeshComponent(entity);
        //entity.ComponentManager.Components.Add(meshComponent);
        //meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
        //meshComponent.Mesh.Texture = Content.Load<Texture2D>("checkboard");
        //world.AddEntityImmediately(entity);

    }

    public void BeginUpdate(GameTime gameTime)
    {
        if (GameInfo.Instance.CurrentWorld == null)
        {
            //TODO : create something to know the new world to load and not the 'FirstWorldLoaded'
            GameInfo.Instance.CurrentWorld = new World.World();
            GameInfo.Instance.CurrentWorld.Load(ProjectSettings.FirstWorldLoaded);
            GameInfo.Instance.CurrentWorld.Initialize(_game);

            if (GameInfo.Instance.CurrentWorld.Physic2dWorld != null)
            {
                Physics2dDebugViewRendererComponent.SetCurrentPhysicsWorld(GameInfo.Instance.CurrentWorld.Physic2dWorld);
            }
        }

#if !FINAL
        DebugSystem.Instance.TimeRuler.StartFrame();
        DebugSystem.Instance.TimeRuler.BeginMark("Update", Color.Blue);
#endif

#if EDITOR
        //In editor the camera is not an entity of the world
        GameInfo.Instance.ActiveCamera?.Update(gameTime.ElapsedGameTime.Milliseconds / 1000.0f);
#endif

        //if (Keyboard.GetState().IsKeyDown(Keys.OemQuotes))
        //    DebugSystem.Instance.DebugCommandUI.Show(); 

        var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);
        GameInfo.Instance.CurrentWorld?.Update(elapsedTime);
        //CasaEngine.Game.UiManager.Update(time);
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

        //Game.GraphicsDevice.Clear(Color.CornflowerBlue);
        _game.GraphicsDevice.Clear(Color.Black);

        var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);
        GameInfo.Instance.CurrentWorld?.Draw(elapsedTime);
        //CasaEngine.Game.UiManager.PreRenderControls();
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
        InputComponent.SetProviders(keyboardStateProvider, mouseStateProvider);
    }
#endif
}