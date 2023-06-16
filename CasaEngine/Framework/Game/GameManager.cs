using CasaEngine.Core.Helpers;
using CasaEngine.Engine.Input;
using CasaEngine.Engine.Renderer;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Debugger;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.FrontEnd.Screen;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Graphics2D;
using CasaEngine.Framework.UserInterface;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;

namespace CasaEngine.Framework.Game;

public class GameManager
{
    private readonly CasaEngineGame _game;

    public string[] Arguments { get; set; }
    private string ProjectFile { get; set; } = string.Empty;
    public AssetContentManager AssetContentManager { get; internal set; } = new();
    public ScreenManager ScreenManager { get; } = new();
    public UserInterfaceManager UiManager { get; } = new();
    public SpriteBatch? SpriteBatch { get; set; }

#if !FINAL
    public SpriteFont? DefaultSpriteFont { get; set; }
    public string ContentPath = string.Empty;
#endif

#if EDITOR
    public BasicEffect? BasicEffect { get; set; }
#endif

    public InputComponent InputComponent { get; private set; }
    public Renderer2dComponent Renderer2dComponent { get; private set; }
    public SpriteRendererComponent SpriteRendererComponent { get; private set; }
    public Line3dRendererComponent Line3dRendererComponent { get; private set; }
    public StaticMeshRendererComponent MeshRendererComponent { get; private set; }
    public ScreenManagerComponent ManagerComponent { get; private set; }
    public PhysicsEngineComponent PhysicsEngineComponent { get; private set; }
    public PhysicsDebugViewRendererComponent PhysicsDebugViewRendererComponent { get; private set; }

    // Game running infos
    private World.World? _currentWorld;

    public World.World? CurrentWorld
    {
        get => _currentWorld;
        set
        {
            _currentWorld = value;
            OnWorldChange();
        }
    }

    public CameraComponent? ActiveCamera { get; set; }

#if EDITOR
    public event EventHandler? WorldChanged;
#endif

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
            game.Services.AddService(typeof(IGraphicsDeviceManager), graphicsDeviceService as IGraphicsDeviceManager);
        }
    }

    private void PreparingDeviceSettings(object? sender, PreparingDeviceSettingsEventArgs e)
    {
        e.GraphicsDeviceInformation.PresentationParameters.BackBufferWidth = GameSettings.ProjectSettings.DebugWidth;
        e.GraphicsDeviceInformation.PresentationParameters.BackBufferHeight = GameSettings.ProjectSettings.DebugHeight;

        e.GraphicsDeviceInformation.GraphicsProfile = GraphicsAdapter.Adapters
            .Any(x => x.IsProfileSupported(GraphicsProfile.HiDef)) ? GraphicsProfile.HiDef : GraphicsProfile.Reach;
    }

    public void OnScreenResized(int width, int height)
    {
        if (CurrentWorld != null)
        {
            foreach (var entity in CurrentWorld.Entities)
            {
                entity.ScreenResized(width, height);
            }
        }
    }

    private void OnDeviceReset(object? sender, EventArgs e)
    {
        GraphicsDevice graphicsDevice;

        if (sender is GraphicsDeviceManager graphicsDeviceManager)
        {
            graphicsDevice = graphicsDeviceManager.GraphicsDevice;
        }
        else
        {
            graphicsDevice = (GraphicsDevice)sender!;
        }

        OnScreenResized(graphicsDevice.PresentationParameters.BackBufferWidth, graphicsDevice.PresentationParameters.BackBufferHeight);
    }

    public void Initialize()
    {
        AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
        AssetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());
        AssetContentManager.Initialize(_game.GraphicsDevice);
        AssetContentManager.RootDirectory = ContentPath;

        DebugSystem.Initialize(_game);

        Line3dRendererComponent = new Line3dRendererComponent(_game);
        SpriteBatch = new SpriteBatch(_game.GraphicsDevice);
        Renderer2dComponent = new Renderer2dComponent(_game) { SpriteBatch = SpriteBatch };
        SpriteRendererComponent = new SpriteRendererComponent(_game);
        InputComponent = new InputComponent(_game);
        ManagerComponent = new ScreenManagerComponent(_game);
        MeshRendererComponent = new StaticMeshRendererComponent(_game);
        PhysicsEngineComponent = new PhysicsEngineComponent(_game);
        PhysicsDebugViewRendererComponent = new PhysicsDebugViewRendererComponent(_game);
        PhysicsDebugViewRendererComponent.DisplayPhysics = true;

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
        _game.Window.Title = GameSettings.ProjectSettings.WindowTitle;
        _game.Window.AllowUserResizing = GameSettings.ProjectSettings.AllowUserResizing;
        _game.IsFixedTimeStep = GameSettings.ProjectSettings.IsFixedTimeStep;
        _game.IsMouseVisible = GameSettings.ProjectSettings.IsMouseVisible;

        //UiManager.Initialize(_game, null/*Window.Handle*/, _game.Window.ClientBounds);
    }

    public void BeginLoadContent()
    {
#if EDITOR
        CreateDefaultTexture();
#endif
    }

    private void CreateDefaultTexture()
    {
        var texture2D = new Texture2D(_game.GraphicsDevice, 128, 128, true, SurfaceFormat.Color);
        texture2D.SetData(Enumerable.Repeat(Color.Orange, texture2D.Width * texture2D.Height).ToArray());
        var texture = new Assets.Textures.Texture(texture2D);
        texture.Name = Assets.Textures.Texture.DefaultTextureName;
        AssetContentManager.AddAsset(Assets.Textures.Texture.DefaultTextureName, texture);
    }

    public void EndLoadContent()
    {
#if !EDITOR
        if (CurrentWorld == null)
        {
            if (string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.FirstWorldLoaded))
            {
                throw new InvalidOperationException("FirstWorldLoaded is undefined");
            }

            CurrentWorld = new World.World();
            CurrentWorld.Load(GameSettings.ProjectSettings.FirstWorldLoaded);
        }

        CurrentWorld.Initialize(_game);
        //_physics2dDebugViewRendererComponent.SetCurrentPhysicsWorld(CurrentWorld.Physic2dWorld);
#else
        //in editor the active camera is debug camera and it isn't belong to the world
        var entity = new Entity { Name = "Camera" };
        var camera = new ArcBallCameraComponent(entity);
        entity.ComponentManager.Components.Add(camera);
        ActiveCamera = camera;
        camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        camera.Initialize(_game);
#endif
    }

    public void BeginUpdate(GameTime gameTime)
    {
        if (CurrentWorld == null)
        {
            //TODO : create something to know the new world to load and not the 'FirstWorldLoaded'
            CurrentWorld = new World.World();
            CurrentWorld.Load(GameSettings.ProjectSettings.FirstWorldLoaded);
            CurrentWorld.Initialize(_game);
        }

#if !FINAL
        DebugSystem.Instance.TimeRuler.StartFrame();
        DebugSystem.Instance.TimeRuler.BeginMark("Update", Color.Blue);
#endif

#if EDITOR
        //In editor the camera is not an entity of the world
        ActiveCamera?.Update(gameTime.ElapsedGameTime.Milliseconds / 1000.0f);
#endif

        //if (Keyboard.GetState().IsKeyDown(Keys.OemQuotes))
        //    DebugSystem.Instance.DebugCommandUI.Show(); 

        var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);
        CurrentWorld?.Update(elapsedTime);
        //UiManager.Update(elapsedTime);
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

        _game.GraphicsDevice.Clear(Color.Black);

        var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);
        CurrentWorld?.Draw(elapsedTime);
        //UiManager.PreRenderControls();
    }

    public void EndDraw(GameTime gameTime)
    {
#if !FINAL
        DebugSystem.Instance.TimeRuler.EndMark("Draw");
#endif
    }

    private void OnWorldChange()
    {
#if EDITOR
        WorldChanged?.Invoke(this, EventArgs.Empty);
#endif
    }


#if EDITOR
    public void SetInputProvider(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider)
    {
        InputComponent.SetProviders(keyboardStateProvider, mouseStateProvider);
    }
#endif
}