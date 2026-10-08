using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Demos.Demos;
using CasaEngine.Framework.Scene.Entities;
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
    // Parallel to _demos (same index): the theme of each demo on the main screen. Declared at registration, so the demo
    // classes stay unchanged.
    private readonly List<string> _demoThemes = new();

    // Themes of the main screen, in the order of its tree.
    private const string ThemeRendering = "Rendering";
    private const string ThemeAnimation = "Animation";
    private const string ThemePhysics = "Physics";
    private const string ThemeSceneAndViews = "Scene and views";
    private const string ThemeCutscenes = "Cutscenes";
    private const string ThemeTileMaps = "2D and tile maps";
    private const string ThemeUI = "UI";
    private const string ThemeAudio = "Audio";
    private static readonly string[] ThemeOrder =
    {
        ThemeRendering, ThemeAnimation, ThemePhysics, ThemeSceneAndViews, ThemeCutscenes, ThemeTileMaps, ThemeUI, ThemeAudio,
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
    private GamePadState _prevGamePad;
    private bool _automationScreenshotCaptured;

    // ---- Main screen, in a world of its own (plan demos-main-menu, decisions D1, D3, D4) ----
    private const string MainScreenTitle = "CasaEngine demos";
    private MainMenuScreen.Entry[] _mainMenuEntries;
    private MainMenuScreen _mainMenu;
    private World _menuWorld;
    private CameraComponent _menuCamera;
    private int _menuSelection;
    private int _mainMenuPushes;
    // The demo shown as loading on the main screen for one frame before its world loads (decision D6).
    private int _launchingDemoIndex = -1;
    private bool _returnToMenuRequested;
    private bool _quitRequested;

    // ---- Reminder shown in the scene: how to go back to the main screen ----
    private DemoHintOverlay _demoHintOverlay;

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
        AddDemo(new SplitScreenDemo(), ThemeSceneAndViews);
        AddDemo(new RenderToTextureDemo(), ThemeSceneAndViews);
        AddDemo(new WorldSpaceUIDemo(), ThemeUI);
        AddDemo(new ViewManagerSandbox(), ThemeSceneAndViews);
        AddDemo(new UIOverlayDemo(), ThemeUI);
        AddDemo(new AudioDemo(), ThemeAudio);

        _mainMenuEntries = new MainMenuScreen.Entry[_demos.Count];
        for (int i = 0; i < _demos.Count; i++)
        {
            _mainMenuEntries[i] = new MainMenuScreen.Entry(_demos[i].Title, _demos[i].Description, _demoThemes[i]);
        }

        // Decision D6: CASAENGINE_START_DEMO starts that demo directly; otherwise the game starts on the main screen.
        bool startOnMainScreen = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_START_DEMO"));
        int startupDemoIndex = startOnMainScreen ? 0 : ResolveStartupDemoIndex();
        _demoCycle = DemoCycleAutomation.TryCreate(_demos.Count, startupDemoIndex);

        // The first world is created here, so GameManager.EndLoadContent never falls back to FirstWorldLoaded.
        if (startOnMainScreen)
        {
            EnterMainScreen(startupDemoIndex);
        }
        else
        {
            ChangeDemo(startupDemoIndex);
        }
    }

    /// <summary>
    /// Registers a demo at the next index (the index <c>CASAENGINE_START_DEMO</c> loads it by), with its theme on the main
    /// screen.
    /// </summary>
    private void AddDemo(Demo demo, string theme)
    {
        _demos.Add(demo);
        _demoThemes.Add(theme);
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

        // Every demo runs in a fresh world (plan demos-main-menu, decision D2).
        LeaveCurrentWorld();

        // The new world is the current world from this call on. It loads in the next GameManager.UpdateWorld (in
        // base.Update, at the end of this update, or in the first update at startup), which clears the views, loads the
        // world, builds its views from its entities and raises WorldLoaded.
        var world = new World();
        GameManager.SetWorldToLoad(world);

        // The demo builds its scene before the world loads: World.LoadContent adds its entities (AddEntity only queues
        // them), creates the physics context with the space policy the demo set, and the view bootstrapper picks the
        // demo's camera among them.
        _currentDemo = _demos[_currentDemoIndex];
        _currentDemo.Initialize(this);
        _currentDemo.ConfigureSceneLighting(world);
        _pendingDemoCamera = _currentDemo.CreateCamera(this);

        Window.Title = _currentDemo.Title;
    }

    /// <summary>
    /// Leaves the current demo, if any, and loads a fresh menu world. Its main screen opens when the world is loaded, with
    /// <paramref name="selectedDemoIndex"/> selected (decision D1: the menu world is rebuilt on every return).
    /// </summary>
    private void EnterMainScreen(int selectedDemoIndex)
    {
        LeaveCurrentWorld();

        _menuSelection = selectedDemoIndex;
        _mainMenuPushes = 0;
        var world = new World();
        GameManager.SetWorldToLoad(world);
        _menuWorld = world;

        // No scene behind the main screen (decision D8), but a camera of its own, so the view bootstrapper does not
        // create a default one; the screen covers the whole view.
        var cameraEntity = new Entity { Name = "main screen camera" };
        var camera = new CameraLookAtComponent();
        cameraEntity.RootComponent = camera;
        camera.SetPositionAndTarget(Vector3.Forward * -10, Vector3.Zero);
        cameraEntity.Initialize();
        world.AddEntity(cameraEntity);
        _menuCamera = camera;

        Window.Title = MainScreenTitle;
    }

    /// <summary>
    /// Leaves the world in use: removes the main screen, clears the world (GameManager.SetWorldToLoad(World) replaces the
    /// current world without clearing it, so its entities, physics context, voices and UI are released here), then lets
    /// the demo clean what it holds, as before (entities first).
    /// </summary>
    private void LeaveCurrentWorld()
    {
        CloseMainScreen();

        var outgoingWorld = GameManager.CurrentWorld;
        _demoCycle?.OnWorldLeaving(outgoingWorld);
        if (outgoingWorld != null && outgoingWorld.Game != null)
        {
            outgoingWorld.Clear();
        }

        _currentDemo?.Clean();
        _currentDemo = null;
        _menuWorld = null;
        _menuCamera = null;
        _launchingDemoIndex = -1;

        // A multi-view demo sets its own automatic layout; the next world starts without one.
        GameManager.ViewManager.AutoLayoutMode = null;
    }

    /// <summary>Pushes the main screen on the view of the menu world, as the RPG title screen does on its own world.</summary>
    private void OpenMainScreen()
    {
        _mainMenu = new MainMenuScreen(_mainMenuEntries, ThemeOrder, _menuSelection, RequestDemo, () => _quitRequested = true);
        GameManager.ScreenManager.PushScreenToActiveView(_mainMenu);
        _mainMenuPushes++;
    }

    private void CloseMainScreen()
    {
        if (_mainMenu == null)
        {
            return;
        }

        GameManager.ScreenManager.RemoveScreenFromActiveView(_mainMenu);
        _mainMenu.Dispose();
        _mainMenu = null;
    }

    private void OnWorldLoaded()
    {
        if (_menuWorld != null && ReferenceEquals(GameManager.CurrentWorld, _menuWorld))
        {
            OpenMainScreen();
            _demoCycle?.OnWorldLoaded(GameManager, MainScreenTitle, _menuCamera, isMenu: true, _mainMenuPushes, demoIndex: -1);
            return;
        }

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
        _demoCycle?.OnWorldLoaded(GameManager, _currentDemo.Title, demoCamera, isMenu: false, mainScreenPushes: 0, _currentDemoIndex);
    }

    // ---- Demo navigation UI helpers ----

    private IUIViewRuntime? GetUIView()
        => GameManager.ViewManager.GetActiveUIView();

    /// <summary>
    /// (Re)creates the reminder of how to go back to the main screen on the current UI view. Called after every demo
    /// change because each world change rebuilds the views and their UI runtimes.
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
    /// The reminder shows in every demo, never during an automation run that captures or probes the back buffer, so the
    /// image is the one the demo draws.
    /// </summary>
    private void UpdateDemoHintVisibility()
    {
        bool automation = !string.IsNullOrWhiteSpace(_automationScreenshotPath)
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_PIXELS_PATH"));
        _demoHintOverlay?.SetVisible(!automation);
    }

    /// <summary>Asks for a demo change; it happens at the start of the next update, outside any UI callback.</summary>
    private void RequestDemo(int index)
    {
        _pendingDemoIndex = index;
    }

    /// <summary>
    /// Esc and the gamepad Back (Select) button ask to return to the main screen during a demo (decision D5); on the main
    /// screen they do nothing (decision D4). Enter and the A button launch the demo selected on the main screen, which
    /// MGTreeView does not do. Pressed edges only, read while the window is active.
    /// </summary>
    private void ReadMenuInput()
    {
        var keyboard = IsActive ? Keyboard.GetState() : new KeyboardState();
        var gamePad = IsActive ? GamePad.GetState(PlayerIndex.One) : new GamePadState();

        bool backPressed = (keyboard.IsKeyDown(Keys.Escape) && !_prevKeyboard.IsKeyDown(Keys.Escape))
            || (gamePad.Buttons.Back == ButtonState.Pressed && _prevGamePad.Buttons.Back != ButtonState.Pressed);
        if (backPressed && _currentDemo != null)
        {
            _returnToMenuRequested = true;
        }

        bool launchPressed = (keyboard.IsKeyDown(Keys.Enter) && !_prevKeyboard.IsKeyDown(Keys.Enter))
            || (gamePad.Buttons.A == ButtonState.Pressed && _prevGamePad.Buttons.A != ButtonState.Pressed);
        if (launchPressed && _mainMenu != null && _launchingDemoIndex < 0 && _mainMenu.TryGetSelectedDemo(out int selectedDemo))
        {
            RequestDemo(selectedDemo);
        }

        _prevKeyboard = keyboard;
        _prevGamePad = gamePad;
    }

    /// <summary>
    /// Applies the world change asked this frame, outside any UI callback. A demo launched from the main screen first
    /// shows "Loading..." for one frame, then its world loads at the next update (decision D6).
    /// </summary>
    private void ApplyWorldRequests()
    {
        if (_returnToMenuRequested)
        {
            _returnToMenuRequested = false;
            _pendingDemoIndex = -1;
            if (_currentDemo != null)
            {
                EnterMainScreen(_currentDemoIndex);
            }

            return;
        }

        if (_launchingDemoIndex >= 0)
        {
            int launching = _launchingDemoIndex;
            _launchingDemoIndex = -1;
            ChangeDemo(launching);
            return;
        }

        if (_pendingDemoIndex < 0)
        {
            return;
        }

        int requested = _pendingDemoIndex;
        _pendingDemoIndex = -1;
        if (_mainMenu != null)
        {
            _mainMenu.ShowLoading(requested);
            _launchingDemoIndex = requested;
        }
        else
        {
            ChangeDemo(requested);
        }
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

        OnScreenResized(bounds.Width, bounds.Height);
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
        // Demo.PostDraw draws in the layout area of the views (the whole back buffer: the demos set no layout insets).
        var pp = GraphicsDevice.PresentationParameters;
        var previousViewport = GraphicsDevice.Viewport;
        GraphicsDevice.Viewport = new Viewport(GameManager.ViewManager.GetLayoutArea(pp.BackBufferWidth, pp.BackBufferHeight));
        _currentDemo?.PostDraw(this, gameTime);
        GraphicsDevice.Viewport = previousViewport;
    }

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);

        // Captured once the whole frame is drawn: a demo that switches render targets discards what the back buffer held
        // before AfterRenderPipeline.
        TryCaptureAutomationScreenshot(gameTime);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_quitRequested)
        {
            Exit();
            return;
        }

        if (_demoCycle != null)
        {
            int next = _demoCycle.Update();
            if (next == DemoCycleAutomation.MenuTarget)
            {
                _returnToMenuRequested = true;
            }
            else if (next >= 0)
            {
                RequestDemo(next);
            }
            else if (_demoCycle.IsFinished)
            {
                Environment.ExitCode = _demoCycle.Passed ? 0 : 1;
                Exit();
                return;
            }
        }

        ReadMenuInput();
        ApplyWorldRequests();

        _currentDemo?.Update(gameTime);

        base.Update(gameTime);
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