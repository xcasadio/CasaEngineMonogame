using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Demos.Demos;
using CasaEngine.Demos.Demos.PsxSemiTransparency;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Rendering;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos;

public class DemosGame : CasaEngineGame
{
    private readonly List<Demo> _demos = new();
    private readonly string? _automationScreenshotPath = ResolveAutomationScreenshotPath();
    private readonly TimeSpan? _automationScreenshotDelay = ResolveAutomationScreenshotDelay();
    private readonly bool _automationShowDebugOverlay = ResolveAutomationShowDebugOverlay();
    private Demo _currentDemo;
    private int _currentDemoIndex;
    private CameraComponent _pendingStartupCamera;
    // Demo requested by the UI, loaded at the start of the next update rather than inside the UI callback.
    private int _pendingDemoIndex = -1;
    private KeyboardState _prevKeyboard;
    private bool _automationScreenshotCaptured;

    // ---- Demo navigation UI ----
    private DemoInfoScreen?  _demoInfoScreen;
    private DemoHintOverlay? _demoHintOverlay;

    // ---- Demo browser beside the scene (ADR-0070) ----
    private const int DefaultBrowserWidth = 280;
    private const int MinimumBrowserWidth = 200;
    // The last column of demo-browser.xaml, which the game drags (the scene is not part of the browser's desktop).
    private const int SceneHandleWidth = 6;
    // The scene keeps at least this fraction of its height in width: a narrower 3D view widens its vertical field of
    // view past 90 degrees (Camera3dComponent.OnScreenResized), plan point P1.
    private const float MinimumSceneAspect = 0.89f;
    private UIRoot? _browserRoot;
    private BackBufferSurface? _browserSurface;
    private DemoBrowserScreen? _browserScreen;
    private bool _browserOpen = true;
    private bool _browserLayoutDirty;
    private int _browserWidth = DefaultBrowserWidth;
    private bool _browserOwnsKeyboard;
    private bool _draggingSceneHandle;
    private int _sceneHandleGrabOffset;
    private MouseState _prevMouse;

    // The demos content folder is a regular editor project (DemosGame.json + AssetInfos.json).
    // Passing the project file to the base constructor lets CasaEngineGame.Initialize load it
    // through ProjectSettingsHelper, which keeps RuntimeContext.ProjectPath and
    // EngineEnvironment.ProjectPath synchronized and loads the asset catalog.
    public DemosGame()
        : base(Path.Combine(Environment.CurrentDirectory, "Content", "DemosGame.json"))
    {
    }

    protected override void Initialize()
    {
        Logs.AddLogger(new DebugLogger());
        Logs.AddLogger(new FileLogger("log.txt"));
        Logs.Verbosity = LogVerbosity.Trace;

        _browserOpen = ResolveInitialBrowserOpen();

        // Push demo UI screens whenever the engine finishes building views for a world.
        // On startup the first demo is prepared before GameManager loads the world, so
        // its camera/UI setup must be finalized only after the world and views exist.
        GameManager.WorldLoaded += (_, _) => OnWorldLoaded();

        // Without a virtual resolution the engine does not lay the views out again when the player resizes the window
        // (CasaEngineGame.OnWindowClientSizeChanged): the demos do it, so the scene follows the window.
        Window.ClientSizeChanged += OnDemosWindowClientSizeChanged;

        // Window title, mouse visibility, resizing, project path and the asset catalog
        // all come from Content\DemosGame.json, loaded by the base class (see constructor).
        base.Initialize();
    }

    protected override void LoadContentPrivate()
    {
        var world = new World();
        GameManager.SetWorldToLoad(world);
        this.GetGameComponent<PhysicsDebugViewRendererComponent>().DisplayPhysics = true;

        _demos.Add(new CutsceneMoveToDemo());
    _demos.Add(new CutsceneNavigateToDemo());
        _demos.Add(new Collision3dBasicDemo());
        _demos.Add(new Collision2dBasicDemo());
        _demos.Add(new TopDownElevationDemo());
        _demos.Add(new StaticModelDemo());
        _demos.Add(new MaterialDemo());
        _demos.Add(new ParticleSystemDemo());
        _demos.Add(new EnvironmentShowcaseDemo());
        // Re-enabled for T2.2 (ai-agent/tasks/screen-effect-above-ui-tasks.md): it was disabled
        // since 2026-05-24 (commit 9d73f9460, an unrelated runtime/editor separation pass), and its
        // Camera2dComponent is the only supported camera for the above-UI screen effect quad's
        // placement formula (ScreenEffectComponent.cs:53-67), so it is also the only demo that can
        // host that smoke.
        _demos.Add(new TileMapDemo());
        _demos.Add(new TileMap3dDemo());
        _demos.Add(new TileMapSurfaceScreenDemo());
        _demos.Add(new SkinnedMeshDemo());
        _demos.Add(new StaticShadowValidationDemo());
        _demos.Add(new AnimationBlendDemo());
        _demos.Add(new AnimationIkDemo());
        _demos.Add(new SkeletalAnimationBlendingDemo());
        _demos.Add(new SceneManagementDemo());
        _demos.Add(new SplitScreenDemo());
        _demos.Add(new RenderToTextureDemo());
        _demos.Add(new WorldSpaceUIDemo());
        _demos.Add(new ViewManagerSandbox());
        _demos.Add(new UIOverlayDemo());
        _demos.Add(new AudioDemo());
        // E19.g G2a (ADR-0051): PSX semi-transparency of sprites and queue capacity, each checks its own back-buffer.
        _demos.Add(new PsxSemiTransparencyDemo());
        _demos.Add(new SpriteQueueCapacityDemo());
        // E19.g G2c (ADR-0051 extended to the background layers): per-texel PSX semi-transparency of the layer sheets.
        _demos.Add(new BackdropLayersPsxSemiTransparencyDemo());
        // E19.g G2d (ADR-0066): the tint overlay of the scrolling layers draws with the PSX mode of the map, one scene per mode.
        _demos.Add(new BackgroundTintPsxMode1Demo());
        _demos.Add(new BackgroundTintPsxMode0Demo());
        // E19.g G2b-1 (ADR-0068): free PS1 quads (scaled, mirrored, sheared, trapezoid), compared with the prediction of the annex.
        _demos.Add(new PsxFreeQuadDemo());

        // Before the first demo: its default view is created inside the layout area the browser leaves.
        CreateDemoBrowser();
        ApplyBrowserLayout(ScreenSizeWidth, ScreenSizeHeight, relayoutViews: false);

        ChangeDemo(ResolveStartupDemoIndex());
    }

    /// <summary>
    /// True while the demo browser owns the keyboard (plan point P9): it is shown, the game is active and the pointer is
    /// over it (or drags its handle). Computed at the start of every update from the raw mouse state.
    /// </summary>
    internal bool BrowserOwnsKeyboard => _browserOwnsKeyboard;

    /// <summary>
    /// Builds the demo browser once, on its own UI runtime with the MGUI Dark theme (decision D7): it is installed as the
    /// window-level UI and outlives every demo change.
    /// </summary>
    private void CreateDemoBrowser()
    {
        _browserSurface = new BackBufferSurface(new Rectangle(0, 0, _browserWidth, Math.Max(1, ScreenSizeHeight)));
        _browserRoot = new UIRoot(this, _browserSurface, RuntimeContext);
        _browserRoot.Desktop.Resources.DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, _browserRoot.Desktop.DefaultFontFamily);
        _browserScreen = new DemoBrowserScreen(ToggleDemoBrowser);
        _browserRoot.PushScreen(_browserScreen);
    }

    /// <summary>Asks to collapse or reopen the browser; applied at the start of the next update.</summary>
    private void ToggleDemoBrowser()
    {
        _browserOpen = !_browserOpen;
        _browserLayoutDirty = true;
    }

    /// <summary>
    /// The browser widths the screen allows (plan point P1): at least <see cref="MinimumBrowserWidth"/>, at most what
    /// leaves the scene <see cref="MinimumSceneAspect"/> of its height. False when the screen is too narrow for both.
    /// </summary>
    private static bool TryGetBrowserWidthRange(int screenWidth, int screenHeight, out int minimum, out int maximum)
    {
        minimum = MinimumBrowserWidth;
        maximum = screenWidth - (int)MathF.Ceiling(MinimumSceneAspect * screenHeight);
        return maximum >= minimum;
    }

    /// <summary>
    /// Shows the browser at its width and keeps the scene views out of it (ADR-0070), or gives the whole window back to
    /// the scene when it is collapsed or the screen is too narrow. With <paramref name="relayoutViews"/>, the views and
    /// their cameras are laid out again for the new area.
    /// </summary>
    private void ApplyBrowserLayout(int screenWidth, int screenHeight, bool relayoutViews)
    {
        _browserLayoutDirty = false;

        bool fits = TryGetBrowserWidthRange(screenWidth, screenHeight, out int minimum, out int maximum);
        if (_browserOpen && _browserRoot != null && fits)
        {
            _browserWidth = Math.Clamp(_browserWidth, minimum, maximum);
            _browserSurface!.ViewportRect = new Rectangle(0, 0, _browserWidth, screenHeight);
            GameManager.ViewManager.LayoutInsets = new ViewLayoutInsets(_browserWidth, 0, 0, 0);
            if (WindowUI != _browserRoot)
            {
                SetWindowUI(_browserRoot!, _browserSurface);
            }
        }
        else
        {
            GameManager.ViewManager.LayoutInsets = ViewLayoutInsets.Zero;
            if (WindowUI != null)
            {
                ClearWindowUI();
            }

            _draggingSceneHandle = false;
            SetBrowserOwnsKeyboard(false);
        }

        if (relayoutViews)
        {
            OnScreenResized(screenWidth, screenHeight);
        }
    }

    /// <summary>
    /// Keyboard ownership and the scene handle of the browser, from the raw mouse state (plan points P9 and P1):
    /// dragging the handle sets the width, and the browser owns the keyboard while the pointer is over it.
    /// </summary>
    private void UpdateDemoBrowserInput()
    {
        bool shown = _browserRoot != null && WindowUI == _browserRoot;
        var mouse = IsActive ? Mouse.GetState() : default;
        var position = mouse.Position;
        var browserBounds = shown ? _browserSurface!.ViewportRect : Rectangle.Empty;

        if (shown)
        {
            bool pressed = mouse.LeftButton == ButtonState.Pressed;
            bool wasPressed = _prevMouse.LeftButton == ButtonState.Pressed;
            var handle = new Rectangle(browserBounds.Right - SceneHandleWidth, browserBounds.Top, SceneHandleWidth, browserBounds.Height);

            if (!_draggingSceneHandle && pressed && !wasPressed && handle.Contains(position))
            {
                _draggingSceneHandle = true;
                _sceneHandleGrabOffset = browserBounds.Right - position.X;
            }
            else if (_draggingSceneHandle && !pressed)
            {
                _draggingSceneHandle = false;
            }

            if (_draggingSceneHandle
                && TryGetBrowserWidthRange(ScreenSizeWidth, ScreenSizeHeight, out int minimum, out int maximum))
            {
                int width = Math.Clamp(position.X + _sceneHandleGrabOffset, minimum, maximum);
                if (width != _browserWidth)
                {
                    _browserWidth = width;
                    ApplyBrowserLayout(ScreenSizeWidth, ScreenSizeHeight, relayoutViews: true);
                    browserBounds = _browserSurface!.ViewportRect;
                }
            }
        }
        else
        {
            _draggingSceneHandle = false;
        }

        _prevMouse = mouse;
        SetBrowserOwnsKeyboard(shown && IsActive && (_draggingSceneHandle || browserBounds.Contains(position)));
    }

    private void SetBrowserOwnsKeyboard(bool owns)
    {
        if (owns == _browserOwnsKeyboard)
        {
            return;
        }

        _browserOwnsKeyboard = owns;
        _browserScreen?.SetKeyboardArmed(owns);
    }

    private int ResolveStartupDemoIndex()
    {
        const int defaultIndex = 0;
        var requestedDemo = Environment.GetEnvironmentVariable("CASAENGINE_START_DEMO");
        if (string.IsNullOrWhiteSpace(requestedDemo))
        {
            return defaultIndex;
        }

        if (int.TryParse(requestedDemo, out var parsedIndex))
        {
            return Math.Clamp(parsedIndex, 0, _demos.Count - 1);
        }

        for (int i = 0; i < _demos.Count; i++)
        {
            if (string.Equals(_demos[i].Title, requestedDemo, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        for (int i = 0; i < _demos.Count; i++)
        {
            if (_demos[i].Title.Contains(requestedDemo, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        Logs.WriteWarning($"[DemosGame] Startup demo '{requestedDemo}' was not found. Falling back to demo index {defaultIndex}.");
        return defaultIndex;
    }

    private void ChangeDemo(int index)
    {
        _currentDemoIndex = Math.Clamp(index, 0, _demos.Count - 1);
        var currentWorld = GameManager.CurrentWorld;
        ArgumentNullException.ThrowIfNull(currentWorld);
        bool worldAlreadyLoaded = currentWorld.Game != null;

        currentWorld.ClearEntities();
        _currentDemo?.Clean();

        // A multi-view demo sets its own automatic layout; the next demo starts without one.
        GameManager.ViewManager.AutoLayoutMode = null;

        _currentDemo = _demos[_currentDemoIndex];
        _currentDemo.Initialize(this);
        _currentDemo.ConfigureSceneLighting(currentWorld);
        var camera = _currentDemo.CreateCamera(this);

        Window.Title = _currentDemo.Title;

        if (!worldAlreadyLoaded)
        {
            _pendingStartupCamera = camera;
            return;
        }

        _pendingStartupCamera = null;
        // Clear any views registered by the previous demo so that World.LoadContent
        // can register a fresh default view (it only does so when Views.Count == 0).
        GameManager.ViewManager.Clear();
        currentWorld.LoadContent(this);
        RuntimeViewBootstrapper?.BootstrapViews(this, currentWorld, GameManager.ViewManager);
        _currentDemo.InitializeCamera(camera);
        ApplyAutomationViewSettings();
        RefreshDemoUI();
    }

    private void OnWorldLoaded()
    {
        if (_pendingStartupCamera != null)
        {
            _currentDemo.InitializeCamera(_pendingStartupCamera);
            _pendingStartupCamera = null;
        }

        ApplyAutomationViewSettings();
        RefreshDemoUI();
    }

    // ---- Demo navigation UI helpers ----

    private IUIViewRuntime? GetUIView()
        => GameManager.ViewManager.GetActiveUIView();

    /// <summary>
    /// (Re)creates the DemoInfoScreen and DemoHintOverlay on the current UI view.
    /// Called after every demo change because ViewManager.Clear() tears down the old runtime.
    /// </summary>
    private void RefreshDemoUI()
    {
        var uiView = GetUIView();
        if (uiView == null) return;

        var entries = _demos
            .Select(d => (d.Title, d.Description))
            .ToList();

        _demoInfoScreen  = new DemoInfoScreen(entries, _currentDemoIndex, RequestDemo);
        _demoHintOverlay = new DemoHintOverlay();

        uiView.PushScreen(_demoInfoScreen);
        uiView.PushScreen(_demoHintOverlay);

        bool automationScreenshotEnabled = !string.IsNullOrWhiteSpace(_automationScreenshotPath);
        _demoInfoScreen.SetVisible(!automationScreenshotEnabled);
        _demoHintOverlay.SetVisible(false);
    }

    /// <summary>Asks for a demo change; it happens at the start of the next update, outside any UI callback.</summary>
    private void RequestDemo(int index)
    {
        _pendingDemoIndex = index;
    }

    private void OnDemosWindowClientSizeChanged(object sender, EventArgs e)
    {
        if (ActiveVirtualResolution != null)
        {
            return;
        }

        var bounds = Window.ClientBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            // Minimized: keep the last layout.
            return;
        }

        ApplyBrowserLayout(bounds.Width, bounds.Height, relayoutViews: true);
    }

    private void ApplyAutomationViewSettings()
    {
        if (!_automationShowDebugOverlay)
        {
            return;
        }

        foreach (var view in GameManager.ViewManager.Views)
        {
            view.ShowDebugOverlay = true;
        }
    }

    protected override void OnViewsResized(int width, int height)
    {
        _currentDemo?.OnScreenResized(this, width, height);
    }

    protected override void AfterRenderPipeline(GameTime gameTime)
    {
        // Demo.PostDraw draws in the scene area (the layout area of the views, ADR-0070), never over the demo browser.
        var pp = GraphicsDevice.PresentationParameters;
        var previousViewport = GraphicsDevice.Viewport;
        GraphicsDevice.Viewport = new Viewport(GameManager.ViewManager.GetLayoutArea(pp.BackBufferWidth, pp.BackBufferHeight));
        _currentDemo?.PostDraw(this, gameTime);
        GraphicsDevice.Viewport = previousViewport;

        TryCaptureAutomationScreenshot(gameTime);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_browserLayoutDirty)
        {
            ApplyBrowserLayout(ScreenSizeWidth, ScreenSizeHeight, relayoutViews: true);
        }

        UpdateDemoBrowserInput();

        if (_pendingDemoIndex >= 0)
        {
            int requestedIndex = _pendingDemoIndex;
            _pendingDemoIndex = -1;
            ChangeDemo(requestedIndex);
        }

        _currentDemo.Update(gameTime);

        var kb = IsActive ? Keyboard.GetState() : new KeyboardState();

        // F1 — collapse or reopen the demo browser
        if (kb.IsKeyDown(Keys.F1) && !_prevKeyboard.IsKeyDown(Keys.F1))
        {
            ToggleDemoBrowser();
        }

        _prevKeyboard = kb;

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || kb.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        base.Update(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // The window-level UI belongs to the game that installed it (ADR-0070).
            _browserRoot?.Dispose();
            _browserRoot = null;
        }

        base.Dispose(disposing);
    }

    private void TryCaptureAutomationScreenshot(GameTime gameTime)
    {
        if (_automationScreenshotCaptured
            || string.IsNullOrWhiteSpace(_automationScreenshotPath)
            || _automationScreenshotDelay is null
            || gameTime.TotalGameTime < _automationScreenshotDelay.Value)
        {
            return;
        }

        _automationScreenshotCaptured = true;
        bool succeeded = CaptureScreenshot(_automationScreenshotPath);
        Environment.ExitCode = succeeded ? 0 : 1;
        Exit();
    }

    private bool CaptureScreenshot(string outputPath)
    {
        try
        {
            string fullOutputPath = Path.GetFullPath(outputPath);
            string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            byte[] backBuffer = new byte[width * height * 4];
            GraphicsDevice.GetBackBufferData(backBuffer);

            using var screenshot = new Texture2D(
                GraphicsDevice,
                width,
                height,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat);
            screenshot.SetData(backBuffer);

            using FileStream stream = File.Create(fullOutputPath);
            screenshot.SaveAsPng(stream, width, height);
            Logs.WriteInfo($"[DemosGame] Screenshot saved: {fullOutputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Logs.WriteException(ex);
            return false;
        }
    }

    /// <summary>
    /// Whether the demo browser starts open (plan point P4): collapsed whenever an automation run captures or probes the
    /// back buffer, so the image is the one the demos always produced; <c>CASAENGINE_DEMO_BROWSER=open</c> or
    /// <c>collapsed</c> overrides it, e.g. to capture the browser itself.
    /// </summary>
    private static bool ResolveInitialBrowserOpen()
    {
        var requested = Environment.GetEnvironmentVariable("CASAENGINE_DEMO_BROWSER");
        if (string.Equals(requested, "open", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(requested, "collapsed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(requested))
        {
            Logs.WriteWarning($"[DemosGame] CASAENGINE_DEMO_BROWSER='{requested}' is neither 'open' nor 'collapsed'; it is ignored.");
        }

        return string.IsNullOrWhiteSpace(ResolveAutomationScreenshotPath())
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_PIXELS_PATH"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_PSXQUAD_DUMP_PATH"));
    }

    private static string? ResolveAutomationScreenshotPath()
    {
        var path = Environment.GetEnvironmentVariable("CASAENGINE_CAPTURE_SCREENSHOT_PATH");
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static TimeSpan? ResolveAutomationScreenshotDelay()
    {
        if (string.IsNullOrWhiteSpace(ResolveAutomationScreenshotPath()))
        {
            return null;
        }

        var delayText = Environment.GetEnvironmentVariable("CASAENGINE_CAPTURE_SCREENSHOT_DELAY_MS");
        return int.TryParse(delayText, out int delayMs) && delayMs >= 0
            ? TimeSpan.FromMilliseconds(delayMs)
            : TimeSpan.FromMilliseconds(1500);
    }

    private static bool ResolveAutomationShowDebugOverlay()
    {
        var value = Environment.GetEnvironmentVariable("CASAENGINE_SHOW_DEBUG_OVERLAY");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value == "1"
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || bool.TryParse(value, out var enabled) && enabled;
    }
}