using CasaEngine.Core.Log;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics2D;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.Debugger;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.GameFramework;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Objects;
using CasaEngine.Framework.Project;
using Cursor = CasaEngine.Framework.GUI.Neoforce.Cursor;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Game;

public class CasaEngineGame : Microsoft.Xna.Framework.Game
{
    private readonly string? _projectFileName;
    public GameManager GameManager { get; }
    public UserInterfaceComponent UserInterfaceComponent { get; private set; }
    public AssetContentManager AssetContentManager { get; } = new();
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

#if !FINAL
    public string ContentPath = string.Empty;
#endif

    public string[] Arguments { get; set; }
    private string ProjectFile { get; set; } = string.Empty;

    public int ScreenSizeWidth
    {
        get
        {
#if EDITOR
            return GraphicsDevice.PresentationParameters.BackBufferWidth;
#else
            return Window.ClientBounds.Width;
#endif
        }
    }

    public int ScreenSizeHeight
    {
        get
        {
#if EDITOR
            return GraphicsDevice.PresentationParameters.BackBufferHeight;
#else
            return Window.ClientBounds.Height;
#endif
        }
    }

    public CasaEngineGame(string? projectFileName = null, IGraphicsDeviceService? graphicsDeviceService = null)
    {
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledExceptions;

        _projectFileName = projectFileName;
        GameManager = new GameManager(this);

        if (graphicsDeviceService == null)
        {
            var graphicsDeviceManager = new GraphicsDeviceManager(this);
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
            if (Services.GetService<IGraphicsDeviceService>() != null)
            {
                Services.RemoveService(typeof(IGraphicsDeviceService));
            }
            Services.AddService(typeof(IGraphicsDeviceService), graphicsDeviceService);
            Services.AddService(typeof(IGraphicsDeviceManager), graphicsDeviceService as IGraphicsDeviceManager);
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
        foreach (var component in Components)
        {
            if (component is IGameComponentResizable resizable)
            {
                resizable?.OnScreenResized(width, height);
            }
        }

        GameManager.CurrentWorld?.OnScreenResized(width, height);

        if (GameManager.ActiveCamera != null)
        {
            SetViewport(GameManager.ActiveCamera.Viewport.Bounds);
        }
    }

    public void SetViewport(Rectangle viewportBounds)
    {
        GraphicsDevice.Viewport = new Viewport(viewportBounds);
    }

    private void HandleUnhandledExceptions(object sender, UnhandledExceptionEventArgs e)
    {
        Logs.WriteException((e.ExceptionObject as Exception)!);
    }

    protected override void Initialize()
    {
        if (!string.IsNullOrWhiteSpace(_projectFileName))
        {
            ProjectSettingsHelper.Load(_projectFileName);
        }

        Line3dRendererComponent = new Line3dRendererComponent(this);
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        Renderer2dComponent = new Renderer2dComponent(this) { SpriteBatch = SpriteBatch };
        SpriteRendererComponent = new SpriteRendererComponent(this);
        InputComponent = new InputComponent(this);
        MeshRendererComponent = new StaticMeshRendererComponent(this);
        SkinnedMeshRendererComponent = new SkinnedMeshRendererComponent(this);
        PhysicsEngineComponent = new PhysicsEngineComponent(this);
        PhysicsDebugViewRendererComponent = new PhysicsDebugViewRendererComponent(this);
        FontSystem = new FontSystem();
        UserInterfaceComponent = new UserInterfaceComponent(this);

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
        AssetContentManager.Initialize(GraphicsDevice);

        Content.RootDirectory = ContentPath;
        Window.Title = GameSettings.ProjectSettings.WindowTitle;
        Window.AllowUserResizing = GameSettings.ProjectSettings.AllowUserResizing;
        IsFixedTimeStep = GameSettings.ProjectSettings.IsFixedTimeStep;
        IsMouseVisible = GameSettings.ProjectSettings.IsMouseVisible;

        RegisterLoaders();

        //default font
        FontSystem.AddFont(File.ReadAllBytes(@"C:\\Windows\\Fonts\\Tahoma.ttf"));

        DebugSystem.Initialize(this);

        base.Initialize();
    }

    private void RegisterLoaders()
    {
        AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
        AssetContentManager.RegisterAssetLoader(typeof(Effect), new EffectLoader());
        AssetContentManager.RegisterAssetLoader(typeof(RiggedModel), new ModelLoader());
        //AssetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());
        AssetContentManager.RegisterAssetLoader(typeof(Cursor), new NeoForceCursorLoader());

        AssetContentManager.RegisterAssetLoader(typeof(ObjectBase), new AssetLoader<ObjectBase>());
        AssetContentManager.RegisterAssetLoader(typeof(Entity), new AssetLoader<Entity>());
        AssetContentManager.RegisterAssetLoader(typeof(Pawn), new AssetLoader<Pawn>());
        AssetContentManager.RegisterAssetLoader(typeof(SkinnedMesh), new AssetLoader<SkinnedMesh>());
        AssetContentManager.RegisterAssetLoader(typeof(Animation2dData), new AssetLoader<Animation2dData>());
        AssetContentManager.RegisterAssetLoader(typeof(SpriteData), new AssetLoader<SpriteData>());
        AssetContentManager.RegisterAssetLoader(typeof(Texture), new AssetLoader<Texture>());
        AssetContentManager.RegisterAssetLoader(typeof(TileMapData), new AssetLoader<TileMapData>());
        AssetContentManager.RegisterAssetLoader(typeof(TileSetData), new AssetLoader<TileSetData>());
        AssetContentManager.RegisterAssetLoader(typeof(ScreenGui), new AssetLoader<ScreenGui>());
        AssetContentManager.RegisterAssetLoader(typeof(World.World), new AssetLoader<World.World>());
        AssetContentManager.RegisterAssetLoader(typeof(GameMode), new AssetLoader<GameMode>());
    }

    protected override void LoadContent()
    {
        CreateDefaultTexture();
        base.LoadContent();
        LoadContentPrivate();
        GameManager.EndLoadContent();
    }

    private void CreateDefaultTexture()
    {
        var texture2D = new Texture2D(GraphicsDevice, 128, 128, true, SurfaceFormat.Color);
        texture2D.SetData(Enumerable.Repeat(Color.Orange, texture2D.Width * texture2D.Height).ToArray());
        var texture = new Texture(texture2D);
        AssetContentManager.AddAsset(texture.Id, Texture.DefaultTextureName, texture);
    }

    protected virtual void LoadContentPrivate()
    {
    }

    protected override void Update(GameTime gameTime)
    {
#if !FINAL
        DebugSystem.Instance.TimeRuler.StartFrame();
        DebugSystem.Instance.TimeRuler.BeginMark("Update", Color.Blue);
#endif

        GameManager.UpdateWorld(gameTime);

#if EDITOR
        var sortedGameComponents = new List<GameComponent>(Components.Count);
        sortedGameComponents.AddRange(Components
            .Where(x => x is IUpdateable { Enabled: true })
            .Cast<GameComponent>()
            .OrderBy(x => x.UpdateOrder));

        foreach (var component in sortedGameComponents)
        {
            component.Update(gameTime);
        }
#else
        base.Update(gameTime);
#endif

#if !FINAL
        DebugSystem.Instance.TimeRuler.EndMark("Update");
#endif

#if EDITOR
        FrameComputed?.Invoke(this, EventArgs.Empty);
#endif
    }

    /*
    protected override bool DrawWorld()
    {
        return base.DrawWorld();
    }
    */

    protected override void Draw(GameTime gameTime)
    {
#if !FINAL
        DebugSystem.Instance.TimeRuler.StartFrame();
        DebugSystem.Instance.TimeRuler.BeginMark("Draw", Color.Blue);
#endif

        GraphicsDevice.Clear(Color.Black);

        GameManager.DrawWorld(gameTime);

#if EDITOR
        var sortedGameComponents = new List<IDrawable>(Components.Count);
        sortedGameComponents.AddRange(Components
            .Where(x => x is IDrawable { Visible: true })
            .Cast<IDrawable>()
            .OrderBy(x => x.DrawOrder));

        foreach (var component in sortedGameComponents)
        {
            component.Draw(gameTime);
        }
#else
        base.Draw(gameTime);
#endif

#if !FINAL
        DebugSystem.Instance.TimeRuler.EndMark("Draw");
#endif
    }


#if EDITOR

    public event EventHandler? FrameComputed;

    public bool IsRunningInGameEditorMode { get; set; }

    public void SetInputProvider(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider)
    {
        InputComponent.SetProviders(keyboardStateProvider, mouseStateProvider, new GamePadStateProvider());
        UserInterfaceComponent.UINeoForceManager.SetProviders(keyboardStateProvider, mouseStateProvider);
    }

    public void InitializeWithEditor()
    {
        Initialize();
    }

    public void LoadContentWithEditor()
    {
        LoadContent();
    }

    public void UpdateWithEditor(GameTime gameTime)
    {
        Update(gameTime);
    }

    public void DrawWithEditor(GameTime gameTime)
    {
        Draw(gameTime);
    }

#endif
}