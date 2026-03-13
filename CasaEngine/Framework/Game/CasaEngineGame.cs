using CasaEngine.Core.Log;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics2D;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.GUI;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Engine.Input.InputDeviceStateProviders;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.GameFramework;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Project;
using MGUI.Shared.Rendering;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;
using RenderingBackBufferSurface = CasaEngine.Framework.Rendering.BackBufferSurface;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Game;

public class CasaEngineGame : Microsoft.Xna.Framework.Game, IObservableUpdate
{
    private readonly string? _projectFileName;
    public GameManager GameManager { get; }

    // ---- IObservableUpdate (required by MGUI's GameRenderHost/ViewRenderHost) ----

    /// <summary>Fired at the very start of Update. MGUI desktops subscribe to refresh input state.</summary>
    public event EventHandler<TimeSpan>? PreviewUpdate;

    /// <summary>Fired at the very end of Update. MGUI desktops subscribe to finalize frame state.</summary>
    public new event EventHandler<EventArgs>? EndUpdate;
    public AssetContentManager AssetContentManager { get; } = new();
    public FontSystem FontSystem { get; private set; }
    public SpriteBatch? SpriteBatch { get; set; }
    public InputComponent InputComponent { get; private set; }
    public Renderer2DComponent Renderer2DComponent { get; private set; }
    public SpriteRendererComponent SpriteRendererComponent { get; private set; }
    public Line3dRendererComponent Line3dRendererComponent { get; private set; }
    public StaticMeshRendererComponent MeshRendererComponent { get; private set; }
    public SkinnedMeshRendererComponent SkinnedMeshRendererComponent { get; private set; }
    public PhysicsEngineComponent PhysicsEngineComponent { get; private set; }
    public PhysicsDebugViewRendererComponent PhysicsDebugViewRendererComponent { get; private set; }
    public IUIViewRuntimeFactory UIViewRuntimeFactory { get; }
    public IUICompositionService DefaultUICompositionService { get; }
    public IRuntimeViewBootstrapper? RuntimeViewBootstrapper { get; }
    public EngineRuntimeContext RuntimeContext { get; }
    public RenderTargetPool RenderTargetPool { get; private set; }

    // ---- Multi-view render pipeline ----
    private RenderPipeline? _renderPipeline;

#if !FINAL
    public string ContentPath = string.Empty;
#endif

    public string[] Arguments { get; set; }
    private string ProjectFile { get; set; } = string.Empty;

    // Per-instance screen dimensions, updated by OnScreenResized (editor) or Window.ClientBounds (runtime).
    // In editor mode we cannot rely on GraphicsDevice.PresentationParameters when the device is shared
    // across multiple tabs — each tab has its own size.
    private int _screenSizeWidth;
    private int _screenSizeHeight;

    public int ScreenSizeWidth
    {
        get
        {
#if EDITOR
            // Return the stored value (set by OnScreenResized or first-init from PP).
            return _screenSizeWidth > 0
                ? _screenSizeWidth
                : GraphicsDevice.PresentationParameters.BackBufferWidth;
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
            return _screenSizeHeight > 0
                ? _screenSizeHeight
                : GraphicsDevice.PresentationParameters.BackBufferHeight;
#else
            return Window.ClientBounds.Height;
#endif
        }
    }

    public CasaEngineGame(
        string? projectFileName = null,
        IGraphicsDeviceService? graphicsDeviceService = null,
        EngineRuntimeContext? runtimeContext = null)
    {
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledExceptions;

        _projectFileName = projectFileName;
        RuntimeContext = runtimeContext ?? GameSettings.CreateRuntimeContext();
        GameManager = new GameManager(this);
        UIViewRuntimeFactory = RuntimeContext.UIViewRuntimeFactory;
        DefaultUICompositionService = RuntimeContext.UICompositionService;
        RuntimeViewBootstrapper = DefaultRuntimeViewBootstrapper.Instance;
        AssetContentManager.RuntimeContext = RuntimeContext;

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
        // Keep per-instance dimensions so ScreenSizeWidth/Height return the correct value
        // even when the GraphicsDevice is shared across multiple editor tabs.
        _screenSizeWidth  = width;
        _screenSizeHeight = height;

        foreach (var component in Components)
        {
            if (component is IGameComponentResizable resizable)
            {
                resizable?.OnScreenResized(width, height);
            }
        }

        GameManager.CurrentWorld?.OnScreenResized(width, height);

        var views    = GameManager.ViewManager.Views;
        var bbViews  = new System.Collections.Generic.List<RenderView>();

        foreach (var v in views)
        {
            if (v.Surface is RenderingBackBufferSurface) bbViews.Add(v);
        }

        // Single full-screen backbuffer view: auto-resize both the surface and its camera.
        if (bbViews.Count == 1 && bbViews[0].Surface is RenderingBackBufferSurface single)
        {
            single.ViewportRect = new Rectangle(0, 0, width, height);
            bbViews[0].Camera?.OnScreenResized(width, height);
        }
        else if (GameManager.ViewManager.AutoLayoutMode != null)
        {
            // Multi-view with a declared AutoLayoutMode: ViewManager handles rect recomputation.
            // OnViewsResized() is still called below so demos can do extra work (e.g. update
            // RenderTarget surfaces), but they no longer need to recompute BackBuffer viewports.
            GameManager.ViewManager.ApplyBackBufferLayout(width, height);
        }
        else
        {
            // No auto-layout (editor or custom split): just resize the active camera.
            GameManager.ViewManager.ActiveView?.Camera?.OnScreenResized(width, height);
        }

        // Allow derived games (e.g. DemosGame) to propagate the resize to the current
        // demo so that multi-view layouts can be recomputed.
        OnViewsResized(width, height);

        // After all surfaces/cameras have been updated, automatically invalidate every
        // OnDemand view so it re-renders once with the new dimensions.
        // (Harmless on RealTime/Throttled views — IsDirty is only checked for OnDemand.)
        foreach (var v in GameManager.ViewManager.Views)
        {
            v.Invalidate();
        }
    }

    /// <summary>
    /// Called at the end of <see cref="OnScreenResized"/> after single-view surfaces
    /// and cameras have been updated. Override to recompute multi-view / split-screen
    /// layouts (e.g. forward the call to the active demo).
    /// </summary>
    protected virtual void OnViewsResized(int width, int height) { }

    private void HandleUnhandledExceptions(object sender, UnhandledExceptionEventArgs e)
    {
        Logs.WriteException((e.ExceptionObject as Exception)!);
    }

    protected override void Initialize()
    {
        if (!string.IsNullOrWhiteSpace(_projectFileName))
        {
            ProjectSettingsHelper.Load(_projectFileName, RuntimeContext);
        }

        Line3dRendererComponent = new Line3dRendererComponent(this);
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        Renderer2DComponent = new Renderer2DComponent(this) { SpriteBatch = SpriteBatch };
        SpriteRendererComponent = new SpriteRendererComponent(this);
        InputComponent = new InputComponent(this);
        MeshRendererComponent = new StaticMeshRendererComponent(this);
        SkinnedMeshRendererComponent = new SkinnedMeshRendererComponent(this);
        PhysicsEngineComponent = new PhysicsEngineComponent(this);
        PhysicsDebugViewRendererComponent = new PhysicsDebugViewRendererComponent(this);
        FontSystem = new FontSystem();

        // Initialize the multi-view render pipeline.
        // SpriteBatch is passed so that viewport-scoped color clears work correctly on the
        // backbuffer (GraphicsDevice.Clear ignores the current viewport).
        _renderPipeline = new RenderPipeline(GraphicsDevice, new IViewFlushableRenderer[]
        {
            MeshRendererComponent,
            SkinnedMeshRendererComponent,
            SpriteRendererComponent,
            Line3dRendererComponent,
            Renderer2DComponent,
        }, SpriteBatch!);

        // Initialize the shared RT pool so RenderTargetSurface can return obsolete
        // targets to the pool instead of disposing them immediately.
        RenderTargetPool = new RenderTargetPool(GraphicsDevice);
        RuntimeContext.RenderTargetPool = RenderTargetPool;
        RenderTargetPool.Shared = RenderTargetPool;

        // Wire UI runtime auto-creation/disposal for each new render view.
        // The concrete runtime is provided by UIViewRuntimeFactory.
        // and disposed when the view is removed.
        GameManager.ViewManager.ViewAdded   += OnViewAddedCreateUIRuntime;
        GameManager.ViewManager.ViewRemoved += OnViewRemovedDisposeUIRuntime;

        // Create the per-view input router and make it available on InputComponent.
        InputComponent.InputRouter = new Framework.Input.InputRouter(GameManager.ViewManager);

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

        AssetLoaderRegistry.RegisterLoaders(AssetContentManager);

        //default font
        FontSystem.AddFont(File.ReadAllBytes(@"Content\Fonts\tahoma.ttf"));

        // Wire optional debug overlay into the render pipeline.
        // Toggle per-view with RenderView.ShowDebugOverlay = true.
        if (_renderPipeline != null)
        {
            _renderPipeline.DebugOverlay = new DebugOverlay(SpriteBatch!, FontSystem);
        }

        //DebugSystem.Initialize(this);

        base.Initialize();
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
        //DebugSystem.Instance.TimeRuler.StartFrame();
        //DebugSystem.Instance.TimeRuler.BeginMark("Update", Color.Blue);
#endif

        // Fire MGUI PreviewUpdate so all ViewRenderHost instances refresh their input state.
        PreviewUpdate?.Invoke(this, gameTime.TotalGameTime);

        // Update all per-view UI runtimes BEFORE gameplay so the UI has first-chance input.
        // Snapshot Views so that a demo change (ViewManager.Clear inside a button callback)
        // does not throw "Collection was modified" during enumeration.
        foreach (var view in GameManager.ViewManager.Views.ToArray())
        {
            SyncUIViewMetrics(view);
            view.UIView?.Update(gameTime);
        }

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
        //DebugSystem.Instance.TimeRuler.EndMark("Update");
#endif

        // Fire MGUI EndUpdate to finalise frame state in all desktops.
        EndUpdate?.Invoke(this, EventArgs.Empty);

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
        try
        {
#if !FINAL
            //DebugSystem.Instance.TimeRuler.StartFrame();
            //DebugSystem.Instance.TimeRuler.BeginMark("Draw", Color.Blue);
#endif

            if (_renderPipeline != null)
            {
                // ---- Multi-view pipeline path ----
                //
                // Draw order issue: some game components (e.g. UINeoForceManager at DrawOrder=GUIBegin=1)
                // run before the mesh renderer (DrawOrder=MeshComponent=2) and may clear the backbuffer.
                // In the legacy path the mesh renderer draws AFTER them (via base.Draw).
                // In the pipeline path we must do the same: let GUIBegin components run first, THEN
                // render the 3D pipeline, THEN let the remaining components run.
                //
                // We therefore iterate components manually and split at ComponentDrawOrder.MeshComponent.

                var sortedComponents = Components
                    .Where(x => x is IDrawable { Visible: true })
                    .Cast<IDrawable>()
                    .OrderBy(x => x.DrawOrder)
                    .ToList();

                // Phase 1 — components whose DrawOrder < MeshComponent (e.g. GUIBegin / UI setup).
                int meshDrawOrder = (int)ComponentDrawOrder.MeshComponent;
                foreach (var component in sortedComponents)
                {
                    if (component.DrawOrder < meshDrawOrder)
                    {
                        component.Draw(gameTime);
                    }
                }

                // Phase 2 — 3D pipeline rendering (fills backbuffer via viewports).
                // A runtime bootstrapper usually creates a presentation view after world load,
                // but zero views is still valid for loading screens or headless/editor flows.
                var views = GameManager.ViewManager.Views;
                if (views.Count == 0)
                {
                    // Safety net: no world loaded yet — just clear the screen.
                    GraphicsDevice.Clear(Color.Black);
                }
                else
                {
                    _renderPipeline.Render(views, (float)gameTime.ElapsedGameTime.TotalSeconds);
                }

                // Hook for derived classes to draw overlays after the pipeline
                // (e.g. SpriteBatch.Draw for render-to-texture thumbnails)
                AfterRenderPipeline(gameTime);

                // Phase 3 — remaining components (renderer fallbacks see empty queues,
                // Line3d, PhysicsDebug, Axis, UI EndDraw, etc.).
                foreach (var component in sortedComponents)
                {
                    if (component.DrawOrder >= meshDrawOrder)
                    {
                        component.Draw(gameTime);
                    }
                }
            }

#if !FINAL
            //DebugSystem.Instance.TimeRuler.EndMark("Draw");
#endif
        }
        catch (Exception e)
        {
            Logs.WriteException(e);
        }
    }

    /// <summary>
    /// Called after the render pipeline has run (and before game components are drawn).
    /// Override in derived classes to add overlay rendering (SpriteBatch, etc.).
    /// </summary>
    protected virtual void AfterRenderPipeline(GameTime gameTime)
    {
    }

    // ---- Per-view UI runtime lifecycle ----

    /// <summary>
    /// Automatically creates a UI runtime for every newly registered view.
    /// Subscribed to <see cref="ViewManager.ViewAdded"/> in <see cref="Initialize"/>.
    /// </summary>
    private void OnViewAddedCreateUIRuntime(RenderView view)
    {
        view.UIView = UIViewRuntimeFactory.Create(this, view.Surface);
        view.UICompositionService ??= DefaultUICompositionService;
        SyncUIViewMetrics(view);
    }

    /// <summary>
    /// Disposes the hosted UI runtime when its view is removed from the manager.
    /// </summary>
    private void OnViewRemovedDisposeUIRuntime(RenderView view)
    {
        view.UIView?.Dispose();
        view.UIView = null;
    }

    private static void SyncUIViewMetrics(RenderView view)
    {
        if (view.UIView == null)
        {
            return;
        }

        Point viewportSize;
        if (view.Host != null)
        {
            viewportSize = new Point(Math.Max(1, view.Host.Width), Math.Max(1, view.Host.Height));
        }
        else
        {
            var viewport = view.Surface.ViewportRect;
            viewportSize = new Point(Math.Max(1, viewport.Width), Math.Max(1, viewport.Height));
        }

        var metrics = view.UIScaler.ComputeMetrics(viewportSize, view.UISafeAreaInset);
        view.UIView.UpdateMetrics(metrics);
    }

#if EDITOR

    public event EventHandler? FrameComputed;

    public bool IsRunningInGameEditorMode { get; set; }

    public void SetInputProvider(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider)
    {
        InputComponent.SetProviders(keyboardStateProvider, mouseStateProvider, new GamePadStateProvider());
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