using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logs;
using CasaEngine.Engine;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Debugger;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Graphics2D;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.World;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TomShane.Neoforce.Controls;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;
using IKeyboardStateProvider = CasaEngine.Engine.Input.IKeyboardStateProvider;
using IMouseStateProvider = CasaEngine.Engine.Input.IMouseStateProvider;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Game;

public class GameManager
{
    private readonly CasaEngineGame _game;
    private World.World? _currentWorld;
    private CameraComponent? _activeCamera;

#if !FINAL
    public string ContentPath = string.Empty;
#endif

    public string[] Arguments { get; set; }
    private string ProjectFile { get; set; } = string.Empty;
    public AssetContentManager AssetContentManager { get; } = new();
    public Manager UiManager { get; private set; }
    public GuiEndRendererComponent UiManagerEnd { get; private set; }

    public FontSystem FontSystem { get; private set; }
    public SpriteBatch? SpriteBatch { get; set; }
    public InputComponent InputComponent { get; private set; }
    public Renderer2dComponent Renderer2dComponent { get; private set; }
    public SpriteRendererComponent SpriteRendererComponent { get; private set; }
    public Line3dRendererComponent Line3dRendererComponent { get; private set; }
    public StaticMeshRendererComponent MeshRendererComponent { get; private set; }
    public SkinnedMeshRendererComponent SkinnedMeshRendererComponent { get; private set; }
    public PhysicsEngineComponent PhysicsEngineComponent { get; private set; }
    public PhysicsDebugViewRendererComponent PhysicsDebugViewRendererComponent { get; private set; }

    public World.World? CurrentWorld
    {
        get => _currentWorld;
        set
        {
            _currentWorld = value;
            OnWorldChange();
        }
    }

    public CameraComponent? ActiveCamera
    {
        get => _activeCamera;
        set
        {
            _activeCamera = value;
            if (_activeCamera != null)
            {
                if (_activeCamera.IsInitialized == false)
                {
                    throw new InvalidOperationException("Initialize the camera before activate it");
                }

                //TODO: why change min an max depth create bugs ?
                _game.GraphicsDevice.Viewport = new Viewport(_activeCamera.Viewport.Bounds);
            }
        }
    }

    public GameManager(CasaEngineGame game, IGraphicsDeviceService? graphicsDeviceService)
    {
        _game = game;

        if (graphicsDeviceService == null)
        {
            var graphicsDeviceManager = new GraphicsDeviceManager(game);
            graphicsDeviceManager.DeviceReset += OnDeviceReset;

            graphicsDeviceManager.PreferredBackBufferWidth = GameSettings.ProjectSettings.DebugWidth;
            graphicsDeviceManager.PreferredBackBufferHeight = GameSettings.ProjectSettings.DebugHeight;
            graphicsDeviceManager.PreferMultiSampling = false;
            graphicsDeviceManager.PreferredBackBufferFormat = SurfaceFormat.Color;
            graphicsDeviceManager.PreferredDepthStencilFormat = DepthFormat.Depth24;
            graphicsDeviceManager.GraphicsProfile = GraphicsProfile.HiDef;
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

    public void OnScreenResized(int width, int height)
    {
        _game.ScreenResize(width, height);

        if (CurrentWorld != null)
        {
            foreach (var entity in CurrentWorld.Entities)
            {
                entity.ScreenResized(width, height);
            }
        }

#if EDITOR
        if (UseGui)
        {
            UiManager.OnScreenResized(width, height);
        }
#else
        UiManager.OnScreenResized(width, height);
#endif

    }

    public void Initialize()
    {
        RegisterLoaders();

        Line3dRendererComponent = new Line3dRendererComponent(_game);
        SpriteBatch = new SpriteBatch(_game.GraphicsDevice);
        Renderer2dComponent = new Renderer2dComponent(_game) { SpriteBatch = SpriteBatch };
        SpriteRendererComponent = new SpriteRendererComponent(_game);
        InputComponent = new InputComponent(_game);
        MeshRendererComponent = new StaticMeshRendererComponent(_game);
        SkinnedMeshRendererComponent = new SkinnedMeshRendererComponent(_game);
        PhysicsEngineComponent = new PhysicsEngineComponent(_game);
        PhysicsDebugViewRendererComponent = new PhysicsDebugViewRendererComponent(_game);
        FontSystem = new FontSystem();

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

        AssetContentManager.RootDirectory = ContentPath;

        _game.Content.RootDirectory = ContentPath;
        _game.Window.Title = GameSettings.ProjectSettings.WindowTitle;
        _game.Window.AllowUserResizing = GameSettings.ProjectSettings.AllowUserResizing;
        _game.IsFixedTimeStep = GameSettings.ProjectSettings.IsFixedTimeStep;
        _game.IsMouseVisible = GameSettings.ProjectSettings.IsMouseVisible;

        //default font
        FontSystem.AddFont(File.ReadAllBytes(@"C:\\Windows\\Fonts\\Tahoma.ttf"));

        DebugSystem.Initialize(_game);
        AssetContentManager.Initialize(_game.GraphicsDevice);

#if EDITOR
        if (UseGui)
        {
            InitializeGui();
        }
#else
        InitializeGui();
#endif
    }

    private void InitializeGui()
    {
        UiManager = new Manager(_game, _game.Services.GetService<IGraphicsDeviceService>(), "Default",
            new AssetContentManagerAdapter(AssetContentManager), LogManager.Instance);
        UiManager.UpdateOrder = (int)ComponentUpdateOrder.GUI;
        UiManager.DrawOrder = (int)ComponentDrawOrder.GUIBegin;
        _game.Components.Add(UiManager);
        //UiManager.Visible = false;
        UiManager.SkinDirectory = Path.Combine(EngineEnvironment.ProjectPath, "Skins");
        UiManager.LayoutDirectory = Path.Combine(EngineEnvironment.ProjectPath, "Layout");

        UiManager.AutoCreateRenderTarget = true;
        UiManager.TargetFrames = 60;
        UiManager.ShowSoftwareCursor = true;
        //UiManager.GlobalDepth = 1.0f;

        UiManager.OnScreenResize(_game.GraphicsDevice.PresentationParameters.BackBufferWidth,
            _game.GraphicsDevice.PresentationParameters.BackBufferWidth);

        UiManagerEnd = new GuiEndRendererComponent(_game);
    }

    public Entity SpawnEntity(string assetName)
    {
        var assetInfo = GameSettings.AssetInfoManager.Get(assetName);
        var entity = AssetContentManager.Load<Entity>(assetInfo).Clone();
        entity.Initialize(_game);
        CurrentWorld.AddEntity(entity);
        return entity;
    }

    private void RegisterLoaders()
    {
        AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
        //AssetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());
        AssetContentManager.RegisterAssetLoader(typeof(TomShane.Neoforce.Controls.Cursor), new NeoForceCursorLoader());

        AssetContentManager.RegisterAssetLoader(typeof(EntityFlowGraph), new AssetLoader<EntityFlowGraph>());
        AssetContentManager.RegisterAssetLoader(typeof(Entity), new AssetLoader<Entity>());
        AssetContentManager.RegisterAssetLoader(typeof(Animation2dData), new AssetLoader<Animation2dData>());
        AssetContentManager.RegisterAssetLoader(typeof(SpriteData), new AssetLoader<SpriteData>());
        AssetContentManager.RegisterAssetLoader(typeof(Texture), new AssetLoader<Texture>());
        AssetContentManager.RegisterAssetLoader(typeof(TileMapData), new AssetLoader<TileMapData>());
        AssetContentManager.RegisterAssetLoader(typeof(TileSetData), new AssetLoader<TileSetData>());
        AssetContentManager.RegisterAssetLoader(typeof(ScreenGui), new AssetLoader<ScreenGui>());
    }

    public void BeginLoadContent()
    {
        CreateDefaultTexture();
    }

    private void CreateDefaultTexture()
    {
        var texture2D = new Texture2D(_game.GraphicsDevice, 128, 128, true, SurfaceFormat.Color);
        texture2D.SetData(Enumerable.Repeat(Color.Orange, texture2D.Width * texture2D.Height).ToArray());
        var texture = new Texture(texture2D);
        texture.AssetInfo.Name = Texture.DefaultTextureName;
        AssetContentManager.AddAsset(IdManager.GetId(), Texture.DefaultTextureName, texture);
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

            CurrentWorld = new World.World(_game);
            CurrentWorld.Load(GameSettings.ProjectSettings.FirstWorldLoaded, SaveOption.Editor);
        }

        CurrentWorld.Initialize();
        CurrentWorld.BeginPlay();
        //_physics2dDebugViewRendererComponent.SetCurrentPhysicsWorld(CurrentWorld.Physic2dWorld);
#else
        //in editor the active camera is debug camera and it isn't belong to the world
        CreateCameraEditor();
        ActiveCamera = _cameraEditor;
#endif
    }

    public void BeginUpdate(GameTime gameTime)
    {
        if (CurrentWorld == null)
        {
            //TODO : create something to know the new world to load and not the 'FirstWorldLoaded'
            CurrentWorld = new World.World(_game);
            var fileName = Path.Combine(EngineEnvironment.ProjectPath, GameSettings.ProjectSettings.FirstWorldLoaded);
            CurrentWorld.Load(fileName, SaveOption.Editor);
            CurrentWorld.Initialize();
            CurrentWorld.BeginPlay();
        }

#if !FINAL
        DebugSystem.Instance.TimeRuler.StartFrame();
        DebugSystem.Instance.TimeRuler.BeginMark("Update", Color.Blue);
#endif

        var elapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);
        var totalElapsedTime = GameTimeHelper.ConvertTotalTimeToSeconds(gameTime);

#if EDITOR
        if (ActiveCamera == _cameraEditor)
        {
            _cameraEditorEntity.Update(elapsedTime);
        }
#endif

        //if (Keyboard.GetState().IsKeyDown(Keys.OemQuotes))
        //    DebugSystem.Instance.DebugCommandUI.Show(); 

        CurrentWorld?.Update(elapsedTime);
        //UiManager.Update(gameTime);
    }

    public void EndUpdate(GameTime gameTime)
    {
#if !FINAL
        DebugSystem.Instance.TimeRuler.EndMark("Update");
#endif

#if EDITOR
        FrameComputed?.Invoke(this, EventArgs.Empty);
#endif
    }

    public void BeginDraw(GameTime gameTime)
    {
#if !FINAL
        DebugSystem.Instance.TimeRuler.StartFrame();
        DebugSystem.Instance.TimeRuler.BeginMark("Draw", Color.Blue);
#endif

        _game.GraphicsDevice.Clear(Color.Black);

        CurrentWorld?.Draw(ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
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

    public event EventHandler? FrameComputed;
    public event EventHandler? WorldChanged;

    private ArcBallCameraComponent _cameraEditor;
    private Entity _cameraEditorEntity;

    public bool IsRunningInGameEditorMode { get; set; }
    public bool UseGui { get; set; }

    public void SetInputProvider(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider)
    {
        InputComponent.SetProviders(keyboardStateProvider, mouseStateProvider);

        if (UseGui)
        {
            UiManager.SetProviders(keyboardStateProvider, mouseStateProvider);
        }
    }

    private void CreateCameraEditor()
    {
        _cameraEditorEntity = new Entity { Name = "Camera" };
        _cameraEditorEntity.IsTemporary = true;
        _cameraEditorEntity.IsVisible = false;
        _cameraEditor = new ArcBallCameraComponent();
        _cameraEditorEntity.ComponentManager.Components.Add(_cameraEditor);
        var gamePlayComponent = new GamePlayComponent();
        _cameraEditorEntity.ComponentManager.Components.Add(gamePlayComponent);
        gamePlayComponent.ExternalComponent = new ScriptArcBallCamera();

        _cameraEditorEntity.Initialize(_game);

        _cameraEditor.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
    }
#endif
}