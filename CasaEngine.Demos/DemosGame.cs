using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Demos.Demos;
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
    // Parallel to _demos (same index): the theme of each demo in the browser's tree, and whether it needs the whole
    // window (decision D4). Declared at registration, so the demo classes stay unchanged (plan point P5).
    private readonly List<string> _demoThemes = new();
    private readonly List<bool> _demoCollapsesBrowser = new();

    // Themes of the demo browser, in the order of its tree (decision D8).
    private const string ThemeRendering = "Rendering";
    private const string ThemeAnimation = "Animation";
    private const string ThemePhysics = "Physics";
    private const string ThemeSceneAndViews = "Scene and views";
    private const string ThemeCutscenes = "Cutscenes";
    private const string ThemeTileMaps = "2D and tile maps";
    private const string ThemeUI = "UI";
    private const string ThemeAudio = "Audio";
    private const string ThemePsx = "PSX rendering";
    private static readonly string[] ThemeOrder =
    {
        ThemeRendering, ThemeAnimation, ThemePhysics, ThemeSceneAndViews, ThemeCutscenes, ThemeTileMaps, ThemeUI, ThemeAudio, ThemePsx,
    };
    private readonly string? _automationScreenshotPath = ResolveAutomationScreenshotPath();
    private readonly TimeSpan? _automationScreenshotDelay = ResolveAutomationScreenshotDelay();
    private readonly bool _automationShowDebugOverlay = ResolveAutomationShowDebugOverlay();
    private Demo _currentDemo;
    private int _currentDemoIndex;
    // Camera of the demo being loaded: the demo creates it on its new world before the world loads, and initializes it
    // once the world and its views exist (OnWorldLoaded).
    private CameraComponent _pendingDemoCamera;
    private DemoCycleAutomation _demoCycle;
    // Demo requested by the UI, loaded at the start of the next update rather than inside the UI callback.
    private int _pendingDemoIndex = -1;
    private KeyboardState _prevKeyboard;
    private bool _automationScreenshotCaptured;

    // ---- F1 reminder shown in the scene while the demo browser is collapsed ----
    private DemoHintOverlay? _demoHintOverlay;

    // ---- Demo browser beside the scene (ADR-0070) ----
    private const int DefaultBrowserWidth = 280;
    private const int MinimumBrowserWidth = 200;
    // The last column of demo-browser.xaml, which the game drags (the scene is not part of the browser's desktop).
    private const int SceneHandleWidth = 6;
    // The scene keeps at least this fraction of its height in width: a narrower 3D view widens its vertical field of
    // view past 90 degrees (Camera3dComponent.OnScreenResized), plan point P1.
    private const float MinimumSceneAspect = 0.89f;
    private UIRoot _browserRoot;
    private BackBufferSurface _browserSurface;
    private DemoBrowserScreen _browserScreen;
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
        this.GetGameComponent<PhysicsDebugViewRendererComponent>().DisplayPhysics = true;

        AddDemo(new CutsceneMoveToDemo(), ThemeCutscenes);
        AddDemo(new CutsceneNavigateToDemo(), ThemeCutscenes);
        AddDemo(new Collision3dBasicDemo(), ThemePhysics);
        AddDemo(new Collision2dBasicDemo(), ThemePhysics);
        AddDemo(new TopDownElevationDemo(), ThemePhysics);
        AddDemo(new StaticModelDemo(), ThemeRendering);
        AddDemo(new MaterialDemo(), ThemeRendering);
        AddDemo(new ParticleSystemDemo(), ThemeRendering);
        AddDemo(new EnvironmentShowcaseDemo(), ThemeRendering);
        // Re-enabled for T2.2 (ai-agent/tasks/screen-effect-above-ui-tasks.md): it was disabled
        // since 2026-05-24 (commit 9d73f9460, an unrelated runtime/editor separation pass), and its
        // Camera2dComponent is the only supported camera for the above-UI screen effect quad's
        // placement formula (ScreenEffectComponent.cs:53-67), so it is also the only demo that can
        // host that smoke.
        AddDemo(new TileMapDemo(), ThemeTileMaps);
        AddDemo(new TileMap3dDemo(), ThemeTileMaps);
        AddDemo(new TileMapSurfaceScreenDemo(), ThemeTileMaps);
        AddDemo(new SkinnedMeshDemo(), ThemeAnimation);
        AddDemo(new StaticShadowValidationDemo(), ThemeRendering);
        AddDemo(new AnimationBlendDemo(), ThemeAnimation);
        AddDemo(new AnimationIkDemo(), ThemeAnimation);
        AddDemo(new SkeletalAnimationBlendingDemo(), ThemeAnimation);
        AddDemo(new SceneManagementDemo(), ThemeSceneAndViews);
        AddDemo(new SplitScreenDemo(), ThemeSceneAndViews, collapsesBrowser: true);
        AddDemo(new RenderToTextureDemo(), ThemeSceneAndViews);
        AddDemo(new WorldSpaceUIDemo(), ThemeUI);
        AddDemo(new ViewManagerSandbox(), ThemeSceneAndViews, collapsesBrowser: true);
        AddDemo(new UIOverlayDemo(), ThemeUI);
        AddDemo(new AudioDemo(), ThemeAudio);

        // Before the first demo: its default view is created inside the layout area the browser leaves.
        CreateDemoBrowser();
        ApplyBrowserLayout(ScreenSizeWidth, ScreenSizeHeight, relayoutViews: false);

        int startupDemoIndex = ResolveStartupDemoIndex();
        _demoCycle = DemoCycleAutomation.TryCreate(_demos.Count, startupDemoIndex);

        // The first demo creates the first world, so GameManager.EndLoadContent never falls back to FirstWorldLoaded.
        ChangeDemo(startupDemoIndex);
    }

    /// <summary>
    /// Registers a demo at the next index (the index <c>CASAENGINE_START_DEMO</c> and the browser load it by), with its
    /// theme in the browser and whether it needs the whole window (decision D4: the browser collapses when it loads).
    /// </summary>
    private void AddDemo(Demo demo, string theme, bool collapsesBrowser = false)
    {
        _demos.Add(demo);
        _demoThemes.Add(theme);
        _demoCollapsesBrowser.Add(collapsesBrowser);
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
        var entries = new DemoBrowserScreen.Entry[_demos.Count];
        for (int i = 0; i < _demos.Count; i++)
        {
            entries[i] = new DemoBrowserScreen.Entry(_demos[i].Title, _demos[i].Description, _demoThemes[i]);
        }

        _browserScreen = new DemoBrowserScreen(entries, ThemeOrder, RequestDemo, ToggleDemoBrowser);
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

        UpdateDemoHintVisibility();

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

        // Decision D4: a demo that needs the whole window collapses the browser before its views are created.
        if (_demoCollapsesBrowser[_currentDemoIndex] && _browserOpen)
        {
            _browserOpen = false;
            ApplyBrowserLayout(ScreenSizeWidth, ScreenSizeHeight, relayoutViews: false);
        }

        // Every demo runs in a fresh world (plan demos-main-menu, decision D2). GameManager.SetWorldToLoad(World) replaces the
        // current world without clearing it, so the world of the demo being left is cleared here first: its entities, its
        // physics context, the voices it owns and its UI. Then the demo cleans what it holds, as before (entities first).
        var outgoingWorld = GameManager.CurrentWorld;
        _demoCycle?.OnDemoLeaving(outgoingWorld);
        if (outgoingWorld != null && outgoingWorld.Game != null)
        {
            outgoingWorld.Clear();
        }

        _currentDemo?.Clean();

        // A multi-view demo sets its own automatic layout; the next demo starts without one.
        GameManager.ViewManager.AutoLayoutMode = null;

        // The new world is the current world from this call on. It loads in the next GameManager.UpdateWorld (in
        // base.Update, at the end of this update, or in the first update at startup), which clears the views, loads the
        // world, builds its views from its entities and raises WorldLoaded.
        var world = new World();
        GameManager.SetWorldToLoad(world);

        // The demo builds its scene before the world loads: World.LoadContent adds its entities (AddEntity only queues
        // them), creates the physics context with the space policy the demo set, and the view bootstrapper picks the
        // demo's camera among them.
        _currentDemo = _demos[_currentDemoIndex];
        _browserScreen?.SetCurrentDemo(_currentDemoIndex);
        _currentDemo.Initialize(this);
        _currentDemo.ConfigureSceneLighting(world);
        _pendingDemoCamera = _currentDemo.CreateCamera(this);

        Window.Title = _currentDemo.Title;
    }

    private void OnWorldLoaded()
    {
        if (_currentDemo == null)
        {
            return;
        }

        var demoCamera = _pendingDemoCamera;
        if (demoCamera != null)
        {
            _currentDemo.InitializeCamera(demoCamera);
            _pendingDemoCamera = null;
        }

        ApplyAutomationViewSettings();
        RefreshDemoUI();
        _demoCycle?.OnDemoWorldLoaded(GameManager, _currentDemo.Title, demoCamera);
    }

    // ---- Demo navigation UI helpers ----

    private IUIViewRuntime? GetUIView()
        => GameManager.ViewManager.GetActiveUIView();

    /// <summary>
    /// (Re)creates the F1 reminder on the current UI view (the demo browser itself lives on its own runtime and is never
    /// recreated). Called after every demo change because ViewManager.Clear() tears down the old runtime.
    /// </summary>
    private void RefreshDemoUI()
    {
        var uiView = GetUIView();
        if (uiView == null) return;

        _demoHintOverlay = new DemoHintOverlay();
        uiView.PushScreen(_demoHintOverlay);
        UpdateDemoHintVisibility();
    }

    /// <summary>
    /// The F1 reminder shows while the browser is collapsed (by the player, by a demo that needs the whole window, or
    /// because the window is too narrow), never during an automation run that captures or probes the back buffer.
    /// </summary>
    private void UpdateDemoHintVisibility()
    {
        bool automation = !string.IsNullOrWhiteSpace(_automationScreenshotPath)
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_PIXELS_PATH"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_PSXQUAD_DUMP_PATH"));
        _demoHintOverlay?.SetVisible(!automation && WindowUI != _browserRoot);
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
    }

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);

        // Captured once the whole frame is drawn, the window-level demo browser included (ADR-0070): it is drawn after
        // AfterRenderPipeline, and a demo that switches render targets discards what the back buffer held before.
        TryCaptureAutomationScreenshot(gameTime);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_browserLayoutDirty)
        {
            ApplyBrowserLayout(ScreenSizeWidth, ScreenSizeHeight, relayoutViews: true);
        }

        UpdateDemoBrowserInput();

        if (_demoCycle != null)
        {
            int nextDemo = _demoCycle.Update();
            if (nextDemo >= 0)
            {
                RequestDemo(nextDemo);
            }
            else if (_demoCycle.IsFinished)
            {
                Environment.ExitCode = _demoCycle.Passed ? 0 : 1;
                Exit();
                return;
            }
        }

        if (_pendingDemoIndex >= 0)
        {
            int requestedIndex = _pendingDemoIndex;
            _pendingDemoIndex = -1;
            ChangeDemo(requestedIndex);
        }

        _currentDemo?.Update(gameTime);

        var kb = IsActive ? Keyboard.GetState() : new KeyboardState();

        // F1 — collapse or reopen the demo browser
        if (kb.IsKeyDown(Keys.F1) && !_prevKeyboard.IsKeyDown(Keys.F1))
        {
            ToggleDemoBrowser();
        }

        // Enter loads the demo selected in the browser while the browser owns the keyboard (decision D2, point P9).
        if (_browserOwnsKeyboard
            && kb.IsKeyDown(Keys.Enter) && !_prevKeyboard.IsKeyDown(Keys.Enter)
            && _browserScreen != null && _browserScreen.TryGetSelectedDemo(out int selectedDemo))
        {
            RequestDemo(selectedDemo);
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