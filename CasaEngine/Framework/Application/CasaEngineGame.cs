using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.UI;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Input;

using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Particles.Authoring;
using MGUI.Shared.Rendering;
using System.Diagnostics;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;
using RenderingBackBufferSurface = CasaEngine.Framework.Rendering.BackBufferSurface;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Application;

public class CasaEngineGame : Game, IObservableUpdate
{
    private readonly string _projectFileName;
    private readonly GraphicsDeviceManager _graphicsDeviceManager;
    public GameManager GameManager { get; }

    // ---- IObservableUpdate (required by MGUI's GameRenderHost/ViewRenderHost) ----

    /// <summary>Fired at the very start of Update. MGUI desktops subscribe to refresh input state.</summary>
    public event EventHandler<TimeSpan> PreviewUpdate;

    /// <summary>Fired at the very end of Update. MGUI desktops subscribe to finalize frame state.</summary>
    public new event EventHandler<EventArgs> EndUpdate;
    public AssetContentManager AssetContentManager { get; } = new();
    public FontSystem FontSystem { get; private set; }
    internal byte[] DefaultFontSystemTtfData { get; private set; } = Array.Empty<byte>();
    public SpriteBatch SpriteBatch { get; set; }
    public InputComponent InputComponent { get; private set; }
    public Renderer2DComponent Renderer2DComponent { get; private set; }
    public SpriteRendererComponent SpriteRendererComponent { get; private set; }
    public ParticleRendererComponent ParticleRendererComponent { get; private set; }
    public Line3dRendererComponent Line3dRendererComponent { get; private set; }
    public StaticMeshRendererComponent MeshRendererComponent { get; private set; }
    public SkinnedMeshRendererComponent SkinnedMeshRendererComponent { get; private set; }
    public PhysicsSystemComponent PhysicsSystemComponent { get; private set; }
    public PhysicsDebugViewRendererComponent PhysicsDebugViewRendererComponent { get; private set; }
    public IUIViewRuntimeFactory UIViewRuntimeFactory { get; }
    public IUICompositionService DefaultUICompositionService { get; }
    public IRuntimeViewBootstrapper RuntimeViewBootstrapper { get; }
    public EngineRuntimeContext RuntimeContext { get; }
    public MaterialCache MaterialCache { get; }
    public RenderTargetPool RenderTargetPool { get; private set; }
    public GameplayExecutionPolicy ExecutionPolicy { get; set; } = GameplayExecutionPolicies.Runtime;
    private readonly MaterialDependencyIndex _materialDependencyIndex = new();

    // ---- Multi-view render pipeline ----
    private RenderPipeline _renderPipeline;

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
            if (!ExecutionPolicy.UseExternalViewManagement)
            {
            return Window.ClientBounds.Width;
            }

            return _screenSizeWidth > 0
                ? _screenSizeWidth
                : GraphicsDevice.PresentationParameters.BackBufferWidth;
        }
    }

    public int ScreenSizeHeight
    {
        get
        {
            if (!ExecutionPolicy.UseExternalViewManagement)
            {
            return Window.ClientBounds.Height;
            }

            return _screenSizeHeight > 0
                ? _screenSizeHeight
                : GraphicsDevice.PresentationParameters.BackBufferHeight;
        }
    }

    public CasaEngineGame(
        string projectFileName = null,
        IGraphicsDeviceService graphicsDeviceService = null,
        EngineRuntimeContext runtimeContext = null)
    {
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledExceptions;

        _projectFileName = projectFileName;
        RuntimeContext = runtimeContext ?? GameSettings.CreateRuntimeContext();
        MaterialCache = RuntimeContext.MaterialCache ?? new MaterialCache();
        RuntimeContext.MaterialCache = MaterialCache;
        RuntimeContext.MaterialAuthoringCache ??= new MaterialAuthoringAssetCache();
        GameManager = new GameManager(this);
        UIViewRuntimeFactory = RuntimeContext.UIViewRuntimeFactory;
        DefaultUICompositionService = RuntimeContext.UICompositionService;
        RuntimeViewBootstrapper = DefaultRuntimeViewBootstrapper.Instance;
        AssetContentManager.RuntimeContext = RuntimeContext;
        var projectSettings = RuntimeContext.ProjectSettings;

        if (graphicsDeviceService == null)
        {
            _graphicsDeviceManager = new GraphicsDeviceManager(this);
            var graphicsDeviceManager = _graphicsDeviceManager;
            graphicsDeviceManager.DeviceReset += OnDeviceReset;

            graphicsDeviceManager.PreferredBackBufferWidth = projectSettings.DebugWidth;
            graphicsDeviceManager.PreferredBackBufferHeight = projectSettings.DebugHeight;
            graphicsDeviceManager.IsFullScreen = projectSettings.DebugIsFullScreen;
            graphicsDeviceManager.SynchronizeWithVerticalRetrace = projectSettings.VSyncEnabled;
            graphicsDeviceManager.PreferMultiSampling = false;
            graphicsDeviceManager.PreferredBackBufferFormat = SurfaceFormat.Color;
            graphicsDeviceManager.PreferredDepthStencilFormat = DepthFormat.Depth24;
            graphicsDeviceManager.GraphicsProfile = GraphicsProfile.HiDef;
        }
        else
        {
            _graphicsDeviceManager = graphicsDeviceService as GraphicsDeviceManager;
            graphicsDeviceService.GraphicsDevice.DeviceReset += OnDeviceReset;
            if (Services.GetService<IGraphicsDeviceService>() != null)
            {
                Services.RemoveService(typeof(IGraphicsDeviceService));
            }
            Services.AddService(typeof(IGraphicsDeviceService), graphicsDeviceService);
            Services.AddService(typeof(IGraphicsDeviceManager), graphicsDeviceService as IGraphicsDeviceManager);
        }
    }

    public DisplaySettings GetDisplaySettings()
    {
        bool isFullScreen = _graphicsDeviceManager?.IsFullScreen
            ?? RuntimeContext.ProjectSettings.DebugIsFullScreen;
        bool isVSyncEnabled = _graphicsDeviceManager?.SynchronizeWithVerticalRetrace
            ?? RuntimeContext.ProjectSettings.VSyncEnabled;
        return new DisplaySettings(ScreenSizeWidth, ScreenSizeHeight, isFullScreen, isVSyncEnabled);
    }

    public bool ApplyDisplaySettings(DisplaySettings displaySettings, bool persistToProjectSettings = true)
    {
        if (persistToProjectSettings)
        {
            RuntimeContext.ProjectSettings.DebugWidth = displaySettings.Width;
            RuntimeContext.ProjectSettings.DebugHeight = displaySettings.Height;
            RuntimeContext.ProjectSettings.DebugIsFullScreen = displaySettings.IsFullScreen;
            RuntimeContext.ProjectSettings.VSyncEnabled = displaySettings.IsVSyncEnabled;

            if (!ReferenceEquals(RuntimeContext.ProjectSettings, GameSettings.ProjectSettings))
            {
                GameSettings.ProjectSettings.DebugWidth = displaySettings.Width;
                GameSettings.ProjectSettings.DebugHeight = displaySettings.Height;
                GameSettings.ProjectSettings.DebugIsFullScreen = displaySettings.IsFullScreen;
                GameSettings.ProjectSettings.VSyncEnabled = displaySettings.IsVSyncEnabled;
            }
        }

        if (_graphicsDeviceManager == null || ExecutionPolicy.UseExternalViewManagement)
        {
            return false;
        }

        bool settingsChanged = _graphicsDeviceManager.PreferredBackBufferWidth != displaySettings.Width
            || _graphicsDeviceManager.PreferredBackBufferHeight != displaySettings.Height
            || _graphicsDeviceManager.IsFullScreen != displaySettings.IsFullScreen
            || _graphicsDeviceManager.SynchronizeWithVerticalRetrace != displaySettings.IsVSyncEnabled;

        if (!settingsChanged)
        {
            return false;
        }

        _graphicsDeviceManager.PreferredBackBufferWidth = displaySettings.Width;
        _graphicsDeviceManager.PreferredBackBufferHeight = displaySettings.Height;
        _graphicsDeviceManager.IsFullScreen = displaySettings.IsFullScreen;
        _graphicsDeviceManager.SynchronizeWithVerticalRetrace = displaySettings.IsVSyncEnabled;
        _graphicsDeviceManager.ApplyChanges();

        OnScreenResized(displaySettings.Width, displaySettings.Height);
        foreach (var view in GameManager.ViewManager.Views)
        {
            SyncUIViewMetrics(view);
            view.Invalidate();
        }

        return true;
    }

    public DisplaySettings LoadDisplaySettings(string fileName, bool applyToGraphicsDevice = true)
    {
        DisplaySettings currentSettings = GetDisplaySettings();
        DisplaySettings persistedSettings = DisplaySettingsPersistence.Load(fileName, currentSettings);

        if (applyToGraphicsDevice)
        {
            ApplyDisplaySettings(persistedSettings);
        }
        else
        {
            RuntimeContext.ProjectSettings.DebugWidth = persistedSettings.Width;
            RuntimeContext.ProjectSettings.DebugHeight = persistedSettings.Height;
            RuntimeContext.ProjectSettings.DebugIsFullScreen = persistedSettings.IsFullScreen;
            RuntimeContext.ProjectSettings.VSyncEnabled = persistedSettings.IsVSyncEnabled;
        }

        return persistedSettings;
    }

    public void SaveDisplaySettings(string fileName)
    {
        DisplaySettingsPersistence.Save(fileName, GetDisplaySettings());
    }

    private void OnDeviceReset(object sender, EventArgs e)
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
        var bbViews  = new List<RenderView>();

        foreach (var v in views)
        {
            if (v.Surface is RenderingBackBufferSurface)
            {
                bbViews.Add(v);
            }
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

        if (_graphicsDeviceManager != null && !ExecutionPolicy.UseExternalViewManagement)
        {
            _graphicsDeviceManager.PreferredBackBufferWidth = RuntimeContext.ProjectSettings.DebugWidth;
            _graphicsDeviceManager.PreferredBackBufferHeight = RuntimeContext.ProjectSettings.DebugHeight;
            _graphicsDeviceManager.IsFullScreen = RuntimeContext.ProjectSettings.DebugIsFullScreen;
            _graphicsDeviceManager.SynchronizeWithVerticalRetrace = RuntimeContext.ProjectSettings.VSyncEnabled;
            _graphicsDeviceManager.ApplyChanges();
        }

        Line3dRendererComponent = new Line3dRendererComponent(this);
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        Renderer2DComponent = new Renderer2DComponent(this) { SpriteBatch = SpriteBatch };
        SpriteRendererComponent = new SpriteRendererComponent(this);
        ParticleRendererComponent = new ParticleRendererComponent(this);
        InputComponent = new InputComponent(this);
        MeshRendererComponent = new StaticMeshRendererComponent(this);
        SkinnedMeshRendererComponent = new SkinnedMeshRendererComponent(this);
        PhysicsSystemComponent = new PhysicsSystemComponent(this);
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
            ParticleRendererComponent,
            Line3dRendererComponent,
            Renderer2DComponent,
        }, SpriteBatch!);
        _renderPipeline.BeforeViewRender = view => PhysicsDebugViewRendererComponent.RenderForView(view);

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
        InputComponent.InputRouter = new InputRouter(GameManager.ViewManager);

        RuntimeContext.WindowInputSource ??= new MonoGameWindowInputSource(
            () => IsActive,
            () => Window);
        if (RuntimeContext.WindowInputSource is not FrameCachedWindowInputSource)
        {
            RuntimeContext.WindowInputSource = new FrameCachedWindowInputSource(RuntimeContext.WindowInputSource);
        }

        if (RuntimeContext.WindowInputSource != null)
        {
            InputComponent.SetFallbackInputSource(RuntimeContext.WindowInputSource);
        }

#if !FINAL
    var args = Environment.GetCommandLineArgs();

    if (string.IsNullOrWhiteSpace(ProjectFile) && args.Length > 1)
    {
        ProjectFile = args[1];
    }

    if (string.IsNullOrWhiteSpace(ContentPath))
    {
        ContentPath = args.Length > 2
        ? args[2]
        : Path.Combine(AppContext.BaseDirectory, "Content");
    }
#else
        ContentPath = "Content";
#endif

        AssetContentManager.RootDirectory = ContentPath;
        AssetContentManager.Initialize(GraphicsDevice);

        Content.RootDirectory = ContentPath;
        if (!ExecutionPolicy.UseExternalViewManagement)
        {
            Window.Title = RuntimeContext.ProjectSettings.WindowTitle;
            Window.AllowUserResizing = RuntimeContext.ProjectSettings.AllowUserResizing;
            IsFixedTimeStep = RuntimeContext.ProjectSettings.IsFixedTimeStep;
            IsMouseVisible = RuntimeContext.ProjectSettings.IsMouseVisible;
        }

        AssetLoaderRegistry.RegisterLoaders(AssetContentManager);

        // Keep the raw TTF bytes so MGUI can share the same FontSystem instance and sizing calibration.
        DefaultFontSystemTtfData = File.ReadAllBytes(Path.Combine(Content.RootDirectory, "Fonts", "tahoma.ttf"));
        FontSystem.AddFont(DefaultFontSystemTtfData);

        // Wire optional debug overlay into the render pipeline.
        // Toggle per-view with RenderView.ShowDebugOverlay = true.
        if (_renderPipeline != null)
        {
            _renderPipeline.DebugOverlay = new DebugOverlay(SpriteBatch!, FontSystem);
        }

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
        long updateStartTimestamp = Stopwatch.GetTimestamp();

        try
        {
            if (RuntimeContext.WindowInputSource is FrameCachedWindowInputSource { CaptureAutomatically: true } frameCachedWindowInputSource)
            {
                frameCachedWindowInputSource.CaptureFrameInput();
            }

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

            var worldsWithUI = GameManager.ViewManager.Views
                .Select(static view => view.World)
                .Distinct()
                .ToList();

            if (GameManager.CurrentWorld != null && !worldsWithUI.Contains(GameManager.CurrentWorld))
            {
                worldsWithUI.Add(GameManager.CurrentWorld);
            }

            foreach (var world in worldsWithUI)
            {
                world.UpdateWorldUI(gameTime);
            }

            GameManager.UpdateWorld(gameTime);

            if (ExecutionPolicy.UseExternalViewManagement)
            {
                var sortedGameComponents = new List<GameComponent>(Components.Count);
                sortedGameComponents.AddRange(Components
                    .Where(x => x is IUpdateable { Enabled: true })
                    .Cast<GameComponent>()
                    .OrderBy(x => x.UpdateOrder));

                foreach (var component in sortedGameComponents)
                {
                    component.Update(gameTime);
                }
            }
            else
            {
                base.Update(gameTime);
            }

            // Fire MGUI EndUpdate to finalise frame state in all desktops.
            EndUpdate?.Invoke(this, EventArgs.Empty);

            FrameComputed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            var debugOverlay = _renderPipeline?.DebugOverlay;
            if (debugOverlay != null)
            {
                debugOverlay.RecordUpdate(GetElapsedMilliseconds(updateStartTimestamp));
            }
        }
    }

    /*
    protected override bool DrawWorld()
    {
        return base.DrawWorld();
    }
    */

    protected override void Draw(GameTime gameTime)
    {
        long drawStartTimestamp = Stopwatch.GetTimestamp();

        try
        {
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
        }
        catch (Exception e)
        {
            Logs.WriteException(e);
        }
        finally
        {
            var debugOverlay = _renderPipeline?.DebugOverlay;
            if (debugOverlay != null)
            {
                debugOverlay.RecordDraw(
                    GetElapsedMilliseconds(drawStartTimestamp),
                    (float)gameTime.ElapsedGameTime.TotalSeconds);
            }
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
        view.UIView = UIViewRuntimeFactory.Create(this, view.Surface, RuntimeContext);
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

    public MaterialHotReloadMetrics ReloadMaterialAsset(Guid materialAssetId)
        => ReloadMaterialAsset(materialAssetId, null);

    public ParticleHotReloadMetrics ReloadParticleAsset(Guid particleAssetId)
        => ReloadParticleAsset(particleAssetId, null);

    public SpriteHotReloadMetrics ReloadSpriteAsset(Guid spriteAssetId)
        => ReloadSpriteAsset(spriteAssetId, null);

    public ParticleHotReloadMetrics ReloadParticleAsset(Guid particleAssetId, ParticleEffectAsset authoringParticleAsset)
    {
        if (particleAssetId == Guid.Empty)
        {
            return default;
        }

        var stopwatch = Stopwatch.StartNew();
        ParticleEffectAsset particleAsset = ResolveParticleAssetForHotReload(particleAssetId, authoringParticleAsset);
        CacheParticleAssetForHotReload(particleAssetId, particleAsset);
        int refreshedComponentCount = RefreshLoadedParticleSystems(particleAssetId, particleAsset);
        int invalidatedViewCount = InvalidateAllViews();
        stopwatch.Stop();

        var hotReloadMetrics = new ParticleHotReloadMetrics(
            refreshedComponentCount,
            refreshedComponentCount,
            invalidatedViewCount,
            stopwatch.Elapsed.TotalMilliseconds);

        Logs.WriteInfo(
            $"[ParticleHotReload] particle='{particleAssetId}' refreshedParticleSystemComponents={hotReloadMetrics.RefreshedParticleSystemComponentCount} rebuiltRuntimeInstances={hotReloadMetrics.RebuiltRuntimeInstanceCount} invalidatedViews={hotReloadMetrics.InvalidatedViewCount} elapsedMs={hotReloadMetrics.ElapsedMilliseconds:F2}");

        return hotReloadMetrics;
    }

    public SpriteHotReloadMetrics ReloadSpriteAsset(Guid spriteAssetId, SpriteData authoringSpriteData)
    {
        if (spriteAssetId == Guid.Empty)
        {
            return default;
        }

        var stopwatch = Stopwatch.StartNew();
        SpriteData spriteData = ResolveSpriteAssetForHotReload(spriteAssetId, authoringSpriteData);
        CacheSpriteAssetForHotReload(spriteAssetId, spriteData);
        (int refreshedStaticSpriteComponentCount, int refreshedAnimatedSpriteComponentCount) = RefreshLoadedSprites(spriteAssetId, spriteData);
        int invalidatedViewCount = InvalidateAllViews();
        stopwatch.Stop();

        var hotReloadMetrics = new SpriteHotReloadMetrics(
            refreshedStaticSpriteComponentCount,
            refreshedAnimatedSpriteComponentCount,
            invalidatedViewCount,
            stopwatch.Elapsed.TotalMilliseconds);

        Logs.WriteInfo(
            $"[SpriteHotReload] sprite='{spriteAssetId}' refreshedStaticSprites={hotReloadMetrics.RefreshedStaticSpriteComponentCount} refreshedAnimatedSprites={hotReloadMetrics.RefreshedAnimatedSpriteComponentCount} invalidatedViews={hotReloadMetrics.InvalidatedViewCount} elapsedMs={hotReloadMetrics.ElapsedMilliseconds:F2}");

        return hotReloadMetrics;
    }

    public ShaderHotReloadMetrics ReloadBuiltInShader(string contentName, byte[] effectByteCode)
    {
        if (string.IsNullOrWhiteSpace(contentName)
            || effectByteCode == null
            || effectByteCode.Length == 0)
        {
            return default;
        }

        string normalizedContentName = BuiltInShaderCatalog.NormalizeContentName(contentName);
        if (!BuiltInShaderCatalog.TryGetByContentName(normalizedContentName, out _))
        {
            return default;
        }

        var stopwatch = Stopwatch.StartNew();
        int reloadedConsumerCount = 0;

        using var baseEffect = new Effect(GraphicsDevice, effectByteCode);

        switch (normalizedContentName)
        {
            case BuiltInShaderCatalog.LitForwardContentName:
            case BuiltInShaderCatalog.UnlitTextureContentName:
            case BuiltInShaderCatalog.ShadowDepthContentName:
            case BuiltInShaderCatalog.SkyCubemapContentName:
                if (MeshRendererComponent is not null
                    && TryReloadBuiltInShaderClone(baseEffect, effect => MeshRendererComponent.TryReloadBuiltInShader(normalizedContentName, effect)))
                {
                    reloadedConsumerCount++;
                }
                break;

            case BuiltInShaderCatalog.SkinEffectContentName:
                if (SkinnedMeshRendererComponent is not null
                    && TryReloadBuiltInShaderClone(baseEffect, effect => SkinnedMeshRendererComponent.TryReloadBuiltInShader(normalizedContentName, effect)))
                {
                    reloadedConsumerCount++;
                }
                break;

            case BuiltInShaderCatalog.SpriteBatchContentName:
                if (SpriteRendererComponent is not null
                    && TryReloadBuiltInShaderClone(baseEffect, effect => SpriteRendererComponent.TryReloadBuiltInShader(normalizedContentName, effect)))
                {
                    reloadedConsumerCount++;
                }
                break;

            case BuiltInShaderCatalog.DebugPrimitiveColorContentName:
                if (Line3dRendererComponent is not null
                    && TryReloadBuiltInShaderClone(baseEffect, effect => Line3dRendererComponent.TryReloadBuiltInShader(normalizedContentName, effect)))
                {
                    reloadedConsumerCount++;
                }
                break;
        }

        int invalidatedViewCount = reloadedConsumerCount > 0 ? InvalidateAllViews() : 0;
        stopwatch.Stop();

        var hotReloadMetrics = new ShaderHotReloadMetrics(
            normalizedContentName,
            reloadedConsumerCount,
            invalidatedViewCount,
            stopwatch.Elapsed.TotalMilliseconds);

        if (reloadedConsumerCount > 0)
        {
            Logs.WriteInfo(
                $"[ShaderHotReload] shader='{hotReloadMetrics.ShaderContentName}' reloadedConsumers={hotReloadMetrics.ReloadedConsumerCount} invalidatedViews={hotReloadMetrics.InvalidatedViewCount} elapsedMs={hotReloadMetrics.ElapsedMilliseconds:F2}");
        }

        return hotReloadMetrics;
    }

    public MaterialHotReloadMetrics ReloadMaterialAsset(Guid materialAssetId, MaterialAsset authoringMaterialAsset)
    {
        if (materialAssetId == Guid.Empty)
        {
            return default;
        }

        var stopwatch = Stopwatch.StartNew();
        _materialDependencyIndex.RefreshMaterialDependency(materialAssetId);
        var affectedMaterialAssetIds = _materialDependencyIndex.GetAffectedMaterialAssetIds(materialAssetId);
        int invalidatedRuntimeMaterialCount = 0;
        foreach (Guid affectedMaterialId in affectedMaterialAssetIds)
        {
            if (MaterialCache.Invalidate(affectedMaterialId))
            {
                invalidatedRuntimeMaterialCount++;
            }
        }

        int invalidatedAuthoringMaterialCount = 0;
        if (RuntimeContext.MaterialAuthoringCache != null)
        {
            if (authoringMaterialAsset != null && authoringMaterialAsset.Id == materialAssetId)
            {
                RuntimeContext.MaterialAuthoringCache.Set(authoringMaterialAsset);
            }
            else if (RuntimeContext.MaterialAuthoringCache.Invalidate(materialAssetId))
            {
                invalidatedAuthoringMaterialCount = 1;
            }
        }

        var staticModelHotReloadMetrics = RefreshLoadedStaticModelMaterials(affectedMaterialAssetIds);
        int invalidatedViewCount = InvalidateAllViews();
        stopwatch.Stop();

        var hotReloadMetrics = new MaterialHotReloadMetrics(
            affectedMaterialAssetIds.Count,
            invalidatedRuntimeMaterialCount,
            invalidatedAuthoringMaterialCount,
            staticModelHotReloadMetrics.RefreshedStaticModelComponentCount,
            staticModelHotReloadMetrics.RecalculatedOverrideSlotCount,
            staticModelHotReloadMetrics.AuthoringMaterialCacheHitCount,
            staticModelHotReloadMetrics.AuthoringMaterialCacheMissCount,
            invalidatedViewCount,
            stopwatch.Elapsed.TotalMilliseconds);

        Logs.WriteInfo(
            $"[MaterialHotReload] material='{materialAssetId}' affectedMaterials={hotReloadMetrics.AffectedMaterialCount} invalidatedRuntimeMaterials={hotReloadMetrics.InvalidatedRuntimeMaterialCount} invalidatedAuthoringMaterials={hotReloadMetrics.InvalidatedAuthoringMaterialCount} refreshedStaticModelComponents={hotReloadMetrics.RefreshedStaticModelComponentCount} recalculatedOverrideSlots={hotReloadMetrics.RecalculatedOverrideSlotCount} authoringCacheHits={hotReloadMetrics.AuthoringMaterialCacheHitCount} authoringCacheMisses={hotReloadMetrics.AuthoringMaterialCacheMissCount} invalidatedViews={hotReloadMetrics.InvalidatedViewCount} elapsedMs={hotReloadMetrics.ElapsedMilliseconds:F2}");

        return hotReloadMetrics;
    }

    private StaticModelHotReloadMetrics RefreshLoadedStaticModelMaterials(ISet<Guid> affectedMaterialAssetIds)
    {
        var world = GameManager.CurrentWorld;
        if (world == null)
        {
            return default;
        }

        int refreshedStaticModelComponentCount = 0;
        int recalculatedOverrideSlotCount = 0;
        int authoringMaterialCacheHitCount = 0;
        int authoringMaterialCacheMissCount = 0;

        foreach (var entity in EnumerateEntities(world.Entities))
        {
            var staticModelComponent = entity.GetComponent<StaticModelComponent>();
            if (staticModelComponent == null)
            {
                continue;
            }

            var refreshMetrics = staticModelComponent.RefreshResolvedMaterialsDetailed(AssetContentManager, affectedMaterialAssetIds);
            if (!refreshMetrics.RefreshedAny)
            {
                continue;
            }

            refreshedStaticModelComponentCount++;
            recalculatedOverrideSlotCount += refreshMetrics.RecalculatedOverrideSlotCount;
            authoringMaterialCacheHitCount += refreshMetrics.AuthoringMaterialCacheHitCount;
            authoringMaterialCacheMissCount += refreshMetrics.AuthoringMaterialCacheMissCount;
        }

        return new StaticModelHotReloadMetrics(
            refreshedStaticModelComponentCount,
            recalculatedOverrideSlotCount,
            authoringMaterialCacheHitCount,
            authoringMaterialCacheMissCount);
    }

    private ParticleEffectAsset ResolveParticleAssetForHotReload(Guid particleAssetId, ParticleEffectAsset authoringParticleAsset)
    {
        if (authoringParticleAsset != null
            && (authoringParticleAsset.AssetId == particleAssetId || authoringParticleAsset.Id == particleAssetId))
        {
            return authoringParticleAsset;
        }

        return AssetContentManager.Load<ParticleEffectAsset>(particleAssetId, cache: false);
    }

    private SpriteData ResolveSpriteAssetForHotReload(Guid spriteAssetId, SpriteData authoringSpriteData)
    {
        if (authoringSpriteData != null
            && (authoringSpriteData.AssetId == spriteAssetId || authoringSpriteData.Id == spriteAssetId))
        {
            return authoringSpriteData;
        }

        return AssetContentManager.Load<SpriteData>(spriteAssetId, cache: false);
    }

    private void CacheParticleAssetForHotReload(Guid particleAssetId, ParticleEffectAsset particleAsset)
    {
        AssetInfo assetInfo = RuntimeContext?.ResolveAssetInfo(particleAssetId) ?? AssetCatalog.Get(particleAssetId);
        if (assetInfo != null)
        {
            particleAsset.AssetId = assetInfo.Id;
            particleAsset.Name = assetInfo.Name;
            particleAsset.FileName = assetInfo.FileName;
            AssetContentManager.AddAsset(assetInfo, particleAsset);
            return;
        }

        particleAsset.AssetId = particleAssetId;
        AssetContentManager.AddAsset(particleAssetId, particleAsset.Name, particleAsset);
    }

    private void CacheSpriteAssetForHotReload(Guid spriteAssetId, SpriteData spriteData)
    {
        AssetInfo assetInfo = RuntimeContext?.ResolveAssetInfo(spriteAssetId) ?? AssetCatalog.Get(spriteAssetId);
        if (assetInfo != null)
        {
            spriteData.AssetId = assetInfo.Id;
            spriteData.Name = assetInfo.Name;
            spriteData.FileName = assetInfo.FileName;
            AssetContentManager.AddAsset(assetInfo, spriteData);
            return;
        }

        spriteData.AssetId = spriteAssetId;
        AssetContentManager.AddAsset(spriteAssetId, spriteData.Name, spriteData);
    }

    private int RefreshLoadedParticleSystems(Guid particleAssetId, ParticleEffectAsset particleAsset)
    {
        int refreshedComponentCount = 0;
        var visitedWorlds = new HashSet<CasaEngine.Framework.Scene.World.World>();

        var currentWorld = GameManager.CurrentWorld;
        if (currentWorld != null && visitedWorlds.Add(currentWorld))
        {
            refreshedComponentCount += RefreshLoadedParticleSystems(currentWorld, particleAssetId, particleAsset);
        }

        foreach (var view in GameManager.ViewManager.Views)
        {
            var world = view.World;
            if (world == null || !visitedWorlds.Add(world))
            {
                continue;
            }

            refreshedComponentCount += RefreshLoadedParticleSystems(world, particleAssetId, particleAsset);
        }

        return refreshedComponentCount;
    }

    private (int RefreshedStaticSpriteComponentCount, int RefreshedAnimatedSpriteComponentCount) RefreshLoadedSprites(Guid spriteAssetId, SpriteData spriteData)
    {
        int refreshedStaticSpriteComponentCount = 0;
        int refreshedAnimatedSpriteComponentCount = 0;
        var visitedWorlds = new HashSet<CasaEngine.Framework.Scene.World.World>();

        var currentWorld = GameManager.CurrentWorld;
        if (currentWorld != null && visitedWorlds.Add(currentWorld))
        {
            (int refreshedStaticCount, int refreshedAnimatedCount) = RefreshLoadedSprites(currentWorld, spriteAssetId, spriteData);
            refreshedStaticSpriteComponentCount += refreshedStaticCount;
            refreshedAnimatedSpriteComponentCount += refreshedAnimatedCount;
        }

        foreach (var view in GameManager.ViewManager.Views)
        {
            var world = view.World;
            if (world == null || !visitedWorlds.Add(world))
            {
                continue;
            }

            (int refreshedStaticCount, int refreshedAnimatedCount) = RefreshLoadedSprites(world, spriteAssetId, spriteData);
            refreshedStaticSpriteComponentCount += refreshedStaticCount;
            refreshedAnimatedSpriteComponentCount += refreshedAnimatedCount;
        }

        return (refreshedStaticSpriteComponentCount, refreshedAnimatedSpriteComponentCount);
    }

    private static (int RefreshedStaticSpriteComponentCount, int RefreshedAnimatedSpriteComponentCount) RefreshLoadedSprites(
        CasaEngine.Framework.Scene.World.World world,
        Guid spriteAssetId,
        SpriteData spriteData)
    {
        int refreshedStaticSpriteComponentCount = 0;
        int refreshedAnimatedSpriteComponentCount = 0;

        foreach (var entity in EnumerateEntities(world.Entities))
        {
            if (entity.RootComponent != null)
            {
                (int refreshedStaticCount, int refreshedAnimatedCount) = RefreshSpriteComponentTree(entity.RootComponent, spriteAssetId, spriteData);
                refreshedStaticSpriteComponentCount += refreshedStaticCount;
                refreshedAnimatedSpriteComponentCount += refreshedAnimatedCount;
            }

            for (int componentIndex = 0; componentIndex < entity.ComponentList.Count; componentIndex++)
            {
                switch (entity.ComponentList[componentIndex])
                {
                    case StaticSpriteComponent staticSpriteComponent when staticSpriteComponent.ReloadSpriteAsset(spriteAssetId, spriteData):
                        refreshedStaticSpriteComponentCount++;
                        break;

                    case AnimatedSpriteComponent animatedSpriteComponent when animatedSpriteComponent.ReloadSpriteAsset(spriteAssetId, spriteData):
                        refreshedAnimatedSpriteComponentCount++;
                        break;
                }
            }
        }

        return (refreshedStaticSpriteComponentCount, refreshedAnimatedSpriteComponentCount);
    }

    private static (int RefreshedStaticSpriteComponentCount, int RefreshedAnimatedSpriteComponentCount) RefreshSpriteComponentTree(
        SceneComponent sceneComponent,
        Guid spriteAssetId,
        SpriteData spriteData)
    {
        int refreshedStaticSpriteComponentCount = 0;
        int refreshedAnimatedSpriteComponentCount = 0;

        switch (sceneComponent)
        {
            case StaticSpriteComponent staticSpriteComponent when staticSpriteComponent.ReloadSpriteAsset(spriteAssetId, spriteData):
                refreshedStaticSpriteComponentCount++;
                break;

            case AnimatedSpriteComponent animatedSpriteComponent when animatedSpriteComponent.ReloadSpriteAsset(spriteAssetId, spriteData):
                refreshedAnimatedSpriteComponentCount++;
                break;
        }

        for (int childIndex = 0; childIndex < sceneComponent.Children.Count; childIndex++)
        {
            (int refreshedStaticCount, int refreshedAnimatedCount) = RefreshSpriteComponentTree(sceneComponent.Children[childIndex], spriteAssetId, spriteData);
            refreshedStaticSpriteComponentCount += refreshedStaticCount;
            refreshedAnimatedSpriteComponentCount += refreshedAnimatedCount;
        }

        return (refreshedStaticSpriteComponentCount, refreshedAnimatedSpriteComponentCount);
    }

    private static int RefreshLoadedParticleSystems(
        CasaEngine.Framework.Scene.World.World world,
        Guid particleAssetId,
        ParticleEffectAsset particleAsset)
    {
        int refreshedComponentCount = 0;
        foreach (var entity in EnumerateEntities(world.Entities))
        {
            if (entity.RootComponent != null)
            {
                refreshedComponentCount += RefreshParticleSystemComponentTree(entity.RootComponent, particleAssetId, particleAsset);
            }

            for (int componentIndex = 0; componentIndex < entity.ComponentList.Count; componentIndex++)
            {
                if (entity.ComponentList[componentIndex] is not ParticleSystemComponent particleSystemComponent)
                {
                    continue;
                }

                if (RefreshParticleSystemComponent(particleSystemComponent, particleAssetId, particleAsset))
                {
                    refreshedComponentCount++;
                }
            }
        }

        return refreshedComponentCount;
    }

    private static int RefreshParticleSystemComponentTree(
        SceneComponent sceneComponent,
        Guid particleAssetId,
        ParticleEffectAsset particleAsset)
    {
        int refreshedComponentCount = 0;
        if (sceneComponent is ParticleSystemComponent particleSystemComponent
            && RefreshParticleSystemComponent(particleSystemComponent, particleAssetId, particleAsset))
        {
            refreshedComponentCount++;
        }

        for (int childIndex = 0; childIndex < sceneComponent.Children.Count; childIndex++)
        {
            refreshedComponentCount += RefreshParticleSystemComponentTree(sceneComponent.Children[childIndex], particleAssetId, particleAsset);
        }

        return refreshedComponentCount;
    }

    private static bool RefreshParticleSystemComponent(
        ParticleSystemComponent particleSystemComponent,
        Guid particleAssetId,
        ParticleEffectAsset particleAsset)
    {
        if (particleSystemComponent.ParticleEffectAssetId != particleAssetId)
        {
            return false;
        }

        particleSystemComponent.SetParticleEffectAsset(particleAsset);
        return true;
    }

    private int InvalidateAllViews()
    {
        int invalidatedViewCount = 0;
        foreach (var view in GameManager.ViewManager.Views)
        {
            view.Invalidate();
            invalidatedViewCount++;
        }

        return invalidatedViewCount;
    }

    private static bool TryReloadBuiltInShaderClone(Effect sourceEffect, Func<Effect, bool> reloadAction)
    {
        Effect effectClone = sourceEffect.Clone();
        bool reloaded = false;

        try
        {
            reloaded = reloadAction(effectClone);
            return reloaded;
        }
        finally
        {
            if (!reloaded)
            {
                effectClone.Dispose();
            }
        }
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }

    private static IEnumerable<Entity> EnumerateEntities(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            yield return entity;

            foreach (var child in EnumerateEntities(entity.Children))
            {
                yield return child;
            }
        }
    }

    public event EventHandler FrameComputed;
}