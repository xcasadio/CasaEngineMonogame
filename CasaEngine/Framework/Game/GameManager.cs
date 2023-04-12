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
    private Renderer2DComponent _renderer2DComponent;
    private ScreenManagerComponent _screenManagerComponent;
    private InputComponent _inputComponent;
    private Physics2dDebugViewRendererComponent _physics2dDebugViewRendererComponent;
    private PhysicsDebugViewRendererComponent _physicsDebugViewRendererComponent;
    private StaticMeshRendererComponent _staticMeshRendererComponent;
    private PhysicsEngineComponent _physicsEngine;

    private string ProjectFile { get; set; } = string.Empty;

#if !FINAL
    protected string ContentPath = string.Empty;
#endif

    public GameManager(CasaEngineGame game)
    {
        EngineComponents.Game = game;
        var graphicsDeviceManager = new GraphicsDeviceManager(game);
        graphicsDeviceManager.PreparingDeviceSettings += PreparingDeviceSettings;
        graphicsDeviceManager.DeviceReset += OnDeviceReset;

    }

    public GameManager(CasaEngineGame game, IGraphicsDeviceService graphicsDeviceService)
    {
        EngineComponents.Game = game;
        graphicsDeviceService.GraphicsDevice.DeviceReset += OnDeviceReset;
        if (game.Services.GetService<IGraphicsDeviceService>() != null)
        {
            game.Services.RemoveService(typeof(IGraphicsDeviceService));
        }
        game.Services.AddService(typeof(IGraphicsDeviceService), graphicsDeviceService);
    }

    private void PreparingDeviceSettings(object? sender, PreparingDeviceSettingsEventArgs e)
    {
        e.GraphicsDeviceInformation.PresentationParameters.BackBufferWidth = EngineComponents.ProjectSettings.DebugWidth;
        e.GraphicsDeviceInformation.PresentationParameters.BackBufferHeight = EngineComponents.ProjectSettings.DebugHeight;

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
        EngineComponents.AssetContentManager = new AssetContentManager();
        EngineComponents.AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
        EngineComponents.AssetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());

        var game = EngineComponents.Game;
        DebugSystem.Initialize(game);

        _renderer2DComponent = new Renderer2DComponent(game);
        _inputComponent = new InputComponent(game);
        _screenManagerComponent = new ScreenManagerComponent(game);
        _physics2dDebugViewRendererComponent = new Physics2dDebugViewRendererComponent(game);
        _staticMeshRendererComponent = new StaticMeshRendererComponent(game);
        _physicsEngine = new PhysicsEngineComponent(game);

#if EDITOR
        //In editor mode the game is in idle mode so we don't update physics
        _physicsEngine.Enabled = false;
        _physicsDebugViewRendererComponent = new PhysicsDebugViewRendererComponent(game);
        var gizmoComponent = new GizmoComponent(game);
        var gridComponent = new GridComponent(game);
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

        game.Content.RootDirectory = ContentPath;
        //CasaEngine.Game.EngineComponents.ProjectManager.Load(ProjectFile);
        //TODO : create hierarchy of the project
        if (!string.IsNullOrWhiteSpace(ProjectFile))
        {
            EngineComponents.ProjectSettings.Load(ProjectFile);
        }

        game.Window.Title = EngineComponents.ProjectSettings.WindowTitle;
        game.Window.AllowUserResizing = EngineComponents.ProjectSettings.AllowUserResizing;
        game.IsFixedTimeStep = EngineComponents.ProjectSettings.IsFixedTimeStep;
        game.IsMouseVisible = EngineComponents.ProjectSettings.IsMouseVisible;

        if (!string.IsNullOrWhiteSpace(EngineComponents.ProjectSettings.GameplayDllName))
        {
            EngineComponents.PluginManager.Load(EngineComponents.ProjectSettings.GameplayDllName);
        }
    }

    public void BeginLoadContent()
    {
        EngineComponents.AssetContentManager.Initialize(EngineComponents.Game.GraphicsDevice);
        EngineComponents.AssetContentManager.RootDirectory = ContentPath;

        //CasaEngine.Game.EngineComponents.UiManager.Initialize(GraphicsDevice, Window.Handle, Window.ClientBounds);

        EngineComponents.SpriteBatch = new SpriteBatch(EngineComponents.Game.GraphicsDevice);
        //TODO : defaultSpriteFont
        //GameInfo.Instance.DefaultSpriteFont = Content.Load<SpriteFont>("Content/defaultSpriteFont");

        //_renderer2DComponent.SpriteBatch = EngineComponents.SpriteBatch;
        //var renderTarget = new RenderTarget2D(EngineComponents.Game.GraphicsDevice, 1024, 768, false,
        //    SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        //EngineComponents.Game.GraphicsDevice.SetRenderTarget(renderTarget);
    }

    public void EndLoadContent()
    {
#if !EDITOR
        if (GameInfo.Instance.CurrentWorld == null)
        {
            if (string.IsNullOrWhiteSpace(EngineComponents.ProjectSettings.FirstWorldLoaded))
            {
                throw new InvalidOperationException("FirstWorldLoaded is undefined");
            }

            GameInfo.Instance.CurrentWorld = new World.World();
            GameInfo.Instance.CurrentWorld.Load(EngineComponents.ProjectSettings.FirstWorldLoaded);
        }
#else
        //in editor the active camera is debug camera and it isn't belong to the world
        var entity = new Entity { Name = "Camera" };
        var camera = new ArcBallCameraComponent(entity);
        entity.ComponentManager.Components.Add(camera);
        GameInfo.Instance.ActiveCamera = camera;
        camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        camera.Initialize();
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
        //world.AddObjectImmediately(entity);
        //
        //entity = new Entity();
        //entity.Name = "Entity box";
        ////entity.Coordinates.LocalPosition += Vector3.Up * 0.5f;
        //var meshComponent = new StaticMeshComponent(entity);
        //entity.ComponentManager.Components.Add(meshComponent);
        //meshComponent.Mesh = new BoxPrimitive(EngineComponents.Game.GraphicsDevice).CreateMesh();
        //meshComponent.Mesh.Texture = EngineComponents.Game.Content.Load<Texture2D>("checkboard");
        ////
        //entity.ComponentManager.Components.Add(new PhysicsComponent(entity)
        //{
        //    Shape = new Box { Height = 1f, Width = 1f, Length = 1f }
        //});
        ////
        //world.AddObjectImmediately(entity);

        //
        //entity = new Entity();
        //entity.Name = "Entity box 2";
        //entity.Coordinates.LocalPosition += Vector3.UnitX * 2.0f;
        //meshComponent = new MeshComponent(entity);
        //entity.ComponentManager.Components.Add(meshComponent);
        //meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
        //meshComponent.Mesh.Texture = Content.Load<Texture2D>("checkboard");
        //world.AddObjectImmediately(entity);

    }

    public void BeginUpdate(GameTime gameTime)
    {
        if (GameInfo.Instance.CurrentWorld == null)
        {
            //TODO : create something to know the new world to load and not the 'FirstWorldLoaded'
            GameInfo.Instance.CurrentWorld = new World.World();
            GameInfo.Instance.CurrentWorld.Load(EngineComponents.ProjectSettings.FirstWorldLoaded);
            GameInfo.Instance.CurrentWorld.Initialize();
            _physics2dDebugViewRendererComponent.SetCurrentPhysicsWorld(GameInfo.Instance.CurrentWorld.Physic2dWorld);
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
        //CasaEngine.Game.EngineComponents.UiManager.Update(time);
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

        //EngineComponents.Game.GraphicsDevice.Clear(Color.CornflowerBlue);
        EngineComponents.Game.GraphicsDevice.Clear(Color.Black);

        var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);
        GameInfo.Instance.CurrentWorld?.Draw(elapsedTime);
        //CasaEngine.Game.EngineComponents.UiManager.PreRenderControls();
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