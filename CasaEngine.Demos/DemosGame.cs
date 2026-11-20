using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Demos.Demos;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
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
    private const string ThemeGettingStarted = "Getting started";
    private const string ThemeRendering = "Rendering";
    private const string ThemeAnimation = "Animation";
    private const string ThemePhysics = "Physics";
    private const string ThemeGameplay = "Gameplay";
    private const string ThemeSceneAndViews = "Scene and views";
    private const string ThemeCutscenes = "Cutscenes";
    private const string ThemeTileMaps = "2D and tile maps";
    private const string ThemeUI = "UI";
    private const string ThemeAudio = "Audio";
    private const string ThemeEngineValidation = "Engine validation";
    private static readonly string[] ThemeOrder =
    {
        ThemeGettingStarted, ThemeRendering, ThemeAnimation, ThemePhysics, ThemeGameplay, ThemeSceneAndViews, ThemeCutscenes, ThemeTileMaps, ThemeUI,
        ThemeAudio, ThemeEngineValidation,
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

    // Created once: the sheets ask it what each action is bound to.
    private Func<string, DemoBinding> _describeAction;
    private bool _automationScreenshotCaptured;
    // CASAENGINE_CAPTURE_ON_IBL_GENERATION=1 (plan T6.3): one more capture, suffix "-gen", at the end of the first frame in which the
    // generation counter of the IBL cache has increased. The usual delayed capture still happens.
    private readonly bool _captureOnIblGeneration = Environment.GetEnvironmentVariable("CASAENGINE_CAPTURE_ON_IBL_GENERATION") == "1";
    private bool _generationCaptured;

    // CASAENGINE_THEN_DEMO="<exact title>" (plan T10.1, criterion 9): after CASAENGINE_THEN_DEMO_FRAME updates (default 120) of the first
    // demo, asks for that demo, as the player would from the menu. It proves what a demo leaves behind when it is left.
    private readonly string _thenDemoTitle = Environment.GetEnvironmentVariable("CASAENGINE_THEN_DEMO");
    private readonly int _thenDemoFrame = int.TryParse(Environment.GetEnvironmentVariable("CASAENGINE_THEN_DEMO_FRAME"), out int thenFrame) && thenFrame > 0 ? thenFrame : 120;
    private int _thenDemoFrames;
    private bool _thenDemoRequested;

    // CASAENGINE_DEMO_FRAME_STATS=1 (plan T10.2): cost of images 101 to 700, one log line at the end, then the game exits. Without
    // vertical synchronisation and with a variable step, so the image time is not set by the refresh rate.
    private readonly DemoFrameStats _frameStats = DemoFrameStats.IsRequested ? new DemoFrameStats() : null;

    // ---- Main screen, in a world of its own (plan demos-main-menu, decisions D1, D3, D4) ----
    private const string MainScreenTitle = "CasaEngine demos";
    private MainMenuScreen.Entry[] _mainMenuEntries;
    private MainMenuScreen _mainMenu;
    private World _menuWorld;
    private CameraComponent _menuCamera;
    private int _menuSelection;
    // The demo shown as loading on the main screen for one frame before its world loads (decision D6).
    private int _launchingDemoIndex = -1;
    private bool _returnToMenuRequested;
    private bool _quitRequested;

    // ---- Reminder shown in the scene: how to go back to the main screen ----
    private DemoHintOverlay _demoHintOverlay;

    // ---- Help panel shown in the scene (ADR-0090): visible when a demo starts, F1 or Start hides and shows it ----
    private DemoHelpOverlay _demoHelpOverlay;
    private bool _demoHelpVisible = true;

    // The default demo camera (Demo.CreateCamera: ScriptArcBallCamera), as docs/editor/viewport-camera-controls.md
    // describes it. Formatted once. Its letter keys are key positions, named here by the key the active layout puts there
    // (ADR-0096): W A S D on QWERTY, Z Q S D on AZERTY.
    private static readonly DemoControl[] DefaultCameraControls =
    {
        new("Right mouse drag", "look around"),
        new($"Right mouse + {OnLayout(Keys.W)} {OnLayout(Keys.A)} {OnLayout(Keys.S)} {OnLayout(Keys.D)}, {OnLayout(Keys.Q)} {OnLayout(Keys.E)}", "fly"),
        new("Alt + left mouse drag", "orbit around the pivot"),
        new("Middle mouse drag", "pan"),
        new("Mouse wheel", "zoom on the pivot"),
        new(OnLayout(Keys.F), "put the pivot back on the scene"),
    };

    private static string OnLayout(Keys qwertyKey) => DemoBindingText.KeyLabel(KeyboardLayout.FromQwertyPosition(qwertyKey));

    private static readonly string DefaultCameraControlsText = DemoSheetText.FormatControls(DefaultCameraControls);

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

        if (_frameStats != null)
        {
            RuntimeContext.ProjectSettings.VSyncEnabled = false;
        }

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

        // The input configuration of the demos: loaded before any sheet is built, because the sheets show its bindings.
        DemoInput.Load(this);
        _describeAction = action => DemoInput.Describe(this, action);

        // The registration order is the order of the main screen: a learning path (ADR-0090). It is also the index
        // CASAENGINE_START_DEMO accepts, so documentation and scripts name a demo by its title, which is stable.
        AddDemo(new HelloWorldDemo(), ThemeGettingStarted);
        AddDemo(new MovingAnEntityDemo(), ThemeGettingStarted);
        AddDemo(new LoadingAModelDemo(), ThemeGettingStarted);
        AddDemo(new EditorWorldDemo(), ThemeGettingStarted);

        AddDemo(new StaticModelDemo(), ThemeRendering);
        AddDemo(new MaterialDemo(), ThemeRendering);
        AddDemo(new ParticleSystemDemo(), ThemeRendering);
        AddDemo(new EnvironmentShowcaseDemo(), ThemeRendering);

        AddDemo(new SkinnedMeshDemo(), ThemeAnimation);
        AddDemo(new AnimationBlendDemo(), ThemeAnimation);
        AddDemo(new AnimationIkDemo(), ThemeAnimation);
        AddDemo(new SkeletalAnimationBlendingDemo(), ThemeAnimation);

        AddDemo(new Collision3dBasicDemo(), ThemePhysics);
        AddDemo(new Collision2dBasicDemo(), ThemePhysics);
        AddDemo(new TopDownElevationDemo(), ThemePhysics);

        AddDemo(new PlayerInputDemo(), ThemeGameplay);
        AddDemo(new CoroutineDemo(), ThemeGameplay);
        AddDemo(new SaveGameDemo(), ThemeGameplay);

        AddDemo(new SceneManagementDemo(), ThemeSceneAndViews);
        AddDemo(new SplitScreenDemo(), ThemeSceneAndViews);
        AddDemo(new RenderToTextureDemo(), ThemeSceneAndViews);

        AddDemo(new CutsceneMoveToDemo(), ThemeCutscenes);
        AddDemo(new CutsceneNavigateToDemo(), ThemeCutscenes);

        // TileMapDemo's Camera2dComponent is the only camera the above-UI screen effect quad supports
        // (ScreenEffectComponent), so it is the demo that hosts that effect.
        AddDemo(new TileMapDemo(), ThemeTileMaps);
        AddDemo(new TileMap3dDemo(), ThemeTileMaps);
        AddDemo(new TileMapSurfaceScreenDemo(), ThemeTileMaps);
        AddDemo(new Layers2dDemo(), ThemeTileMaps);

        AddDemo(new UIOverlayDemo(), ThemeUI);
        AddDemo(new WorldSpaceUIDemo(), ThemeUI);

        AddDemo(new AudioDemo(), ThemeAudio);

        // Scenes whose purpose is to check the engine rather than to teach a feature.
        AddDemo(new StaticShadowValidationDemo(), ThemeEngineValidation);
        AddDemo(new ViewManagerSandbox(), ThemeEngineValidation);
        AddDemo(new FrozenFrameDemo(), ThemeEngineValidation);
        // Registered last on purpose: it moves no index that CASAENGINE_START_DEMO or a script may use.
        AddDemo(new ColorPipelineDemo(), ThemeEngineValidation);
        AddDemo(new PbrMaterialDemo(), ThemeEngineValidation);

        _mainMenuEntries = new MainMenuScreen.Entry[_demos.Count];
        for (int i = 0; i < _demos.Count; i++)
        {
            _mainMenuEntries[i] = new MainMenuScreen.Entry(
                _demos[i].Title,
                _demos[i].Description,
                _demoThemes[i],
                DemoSheetText.FormatControls(_demos[i].Controls, _describeAction),
                DemoSheetText.FormatHighlights(_demos[i].Highlights),
                _demos[i].DocumentationPath);
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
        // The demo supplies that world: a new empty one, or one loaded from a file made in the editor.
        _currentDemo = _demos[_currentDemoIndex];
        var world = _currentDemo.CreateWorld(this);
        GameManager.SetWorldToLoad(world);

        // The demo builds its scene before the world loads: World.LoadContent adds its entities (AddEntity only queues
        // them), creates the physics context with the space policy the demo set, and the view bootstrapper picks the
        // demo's camera among them.
        _currentDemo.Initialize(this);
        _currentDemo.ConfigureSceneLighting(world);
        _pendingDemoCamera = _currentDemo.CreateCamera(this);
        ApplyColorPipelineHarness(world);

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
        var world = new World();
        GameManager.SetWorldToLoad(world);
        _menuWorld = world;

        // No scene behind the main screen (decision D8), but a camera of its own, so the view bootstrapper does not
        // create a default one; the screen covers the whole view.
        var cameraEntity = new Entity { Name = "main screen camera" };
        var camera = new PerspectiveCameraComponent();
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
            _demoCycle?.OnWorldLoaded(GameManager, MainScreenTitle, _menuCamera, isMenu: true, _mainMenu, demoIndex: -1);
            return;
        }

        if (_currentDemo == null)
        {
            return;
        }

        // A demo whose world comes from a file creates no camera: the view then shows the camera saved in that world,
        // and the demo may still frame it.
        var demoCamera = _pendingDemoCamera ?? GameManager.ViewManager.ActiveView?.Camera;
        if (demoCamera != null)
        {
            _currentDemo.InitializeCamera(demoCamera);
            _pendingDemoCamera = null;
        }

        ApplyAutomationViewSettings();
        RefreshDemoUI(demoCamera);
        _demoCycle?.OnWorldLoaded(GameManager, _currentDemo.Title, demoCamera, isMenu: false, mainScreen: null, _currentDemoIndex);
    }

    // ---- Demo navigation UI helpers ----

    private IUIViewRuntime? GetUIView()
        => GameManager.ViewManager.GetActiveUIView();

    /// <summary>
    /// (Re)creates the reminder line and the help panel of the current demo on the current UI view. Called after every
    /// demo change because each world change rebuilds the views and their UI runtimes. The panel is visible again at
    /// every launch (ADR-0090).
    /// </summary>
    private void RefreshDemoUI(CameraComponent demoCamera)
    {
        var uiView = GetUIView();
        if (uiView == null) return;

        _demoHintOverlay = new DemoHintOverlay(_describeAction(DemoInput.Help), _describeAction(DemoInput.Back));
        uiView.PushScreen(_demoHintOverlay);

        // The camera lines show only when the demo kept the default camera script.
        bool defaultCamera = demoCamera?.Owner?.GameplayProxy is ScriptArcBallCamera;
        _demoHelpOverlay = new DemoHelpOverlay(
            _currentDemo.Title,
            DemoSheetText.FormatControls(_currentDemo.Controls, _describeAction),
            defaultCamera ? DefaultCameraControlsText : string.Empty,
            DemoSheetText.FormatHighlights(_currentDemo.Highlights));
        uiView.PushScreen(_demoHelpOverlay);
        _demoHelpVisible = true;
        UpdateDemoHintVisibility();
    }

    /// <summary>
    /// The reminder and the help panel show in every demo, except during an automation run that captures or probes the back buffer
    /// and in a demo that reads its own image (<see cref="Demo.ReadsItsOwnImage"/>), so that the image is the one the demo draws.
    /// </summary>
    private void UpdateDemoHintVisibility()
    {
        bool automation = !string.IsNullOrWhiteSpace(_automationScreenshotPath)
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_PIXELS_PATH"))
            || _currentDemo?.ReadsItsOwnImage == true;

        // CASAENGINE_DEMO_SHOW_HELP keeps both in a capture, to check or document the panel itself.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_SHOW_HELP")))
        {
            automation = false;
        }

        _demoHintOverlay?.SetVisible(!automation);
        _demoHelpOverlay?.SetVisible(!automation && _demoHelpVisible);
    }

    /// <summary>Asks for a demo change; it happens at the start of the next update, outside any UI callback.</summary>
    private void RequestDemo(int index)
    {
        _pendingDemoIndex = index;
    }

    /// <summary>
    /// All three are actions of the input configuration (<see cref="DemoInput"/>). Esc and the gamepad Back (Select) button ask to return to the main screen during a demo (decision D5); on the main
    /// screen they do nothing (decision D4). F1 and Start hide or show the help panel of the demo. Enter and the A button launch the demo selected on the main screen, which
    /// MGTreeView does not do. Pressed edges only, read while the window is active.
    /// </summary>
    private void ReadMenuInput()
    {
        // Which key and which button: the input configuration. The devices are read here, not through the engine's
        // mappings: see DemoInput.IsBoundDown.
        var keyboard = IsActive ? Keyboard.GetState() : new KeyboardState();
        var gamePad = IsActive ? GamePad.GetState(PlayerIndex.One) : new GamePadState();

        bool backPressed = WasAppActionPressed(DemoInput.Back, keyboard, gamePad);
        if (backPressed && _currentDemo != null)
        {
            _returnToMenuRequested = true;
        }

        bool helpPressed = WasAppActionPressed(DemoInput.Help, keyboard, gamePad);
        if (helpPressed && _currentDemo != null)
        {
            _demoHelpVisible = !_demoHelpVisible;
            UpdateDemoHintVisibility();
        }

        bool launchPressed = WasAppActionPressed(DemoInput.Launch, keyboard, gamePad);
        if (launchPressed && _mainMenu != null && _launchingDemoIndex < 0 && _mainMenu.TryGetSelectedDemo(out int selectedDemo))
        {
            RequestDemo(selectedDemo);
        }

        _prevKeyboard = keyboard;
        _prevGamePad = gamePad;
    }

    private bool WasAppActionPressed(string action, in KeyboardState keyboard, in GamePadState gamePad)
    {
        return DemoInput.IsBoundDown(this, action, keyboard, gamePad)
            && !DemoInput.IsBoundDown(this, action, _prevKeyboard, _prevGamePad);
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
            // The demo shown as loading is the one that loads: a launch asked during the loading frame (a second click, a
            // double click) is dropped, or it would change the world again at the next update.
            int launching = _launchingDemoIndex;
            _launchingDemoIndex = -1;
            _pendingDemoIndex = -1;
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
        long drawStart = 0;
        if (_frameStats != null)
        {
            drawStart = Stopwatch.GetTimestamp();
            _frameStats.BeginDraw(drawStart);
        }

        base.Draw(gameTime);

        TryCaptureOnIblGeneration();

        // Captured once the whole frame is drawn: a demo that switches render targets discards what the back buffer held
        // before AfterRenderPipeline.
        TryCaptureAutomationScreenshot(gameTime);

        if (_frameStats != null)
        {
            _frameStats.EndDraw(drawStart, GameManager.ViewManager.Views, _currentDemo?.Title ?? "none",
                GetDisplaySettings().IsVSyncEnabled, IsFixedTimeStep);
            if (_frameStats.IsFinished)
            {
                LogDemoConfiguration();
                Exit();
            }
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (_frameStats == null)
        {
            UpdateDemos(gameTime);
            return;
        }

        long updateStart = Stopwatch.GetTimestamp();
        IsFixedTimeStep = false;
        _frameStats.BeginUpdate();
        UpdateDemos(gameTime);
        _frameStats.EndUpdate(updateStart);
    }

    private void UpdateDemos(GameTime gameTime)
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

        TryRequestThenDemo();
        ReadMenuInput();
        ApplyWorldRequests();

        _currentDemo?.Update(gameTime);

        base.Update(gameTime);
    }

    /// <summary>Harness of <c>CASAENGINE_THEN_DEMO</c>: one switch to the named demo once the first one has run its frames.</summary>
    private void TryRequestThenDemo()
    {
        if (string.IsNullOrWhiteSpace(_thenDemoTitle) || _thenDemoRequested || _currentDemo == null || ++_thenDemoFrames < _thenDemoFrame)
        {
            return;
        }

        _thenDemoRequested = true;
        for (int i = 0; i < _demos.Count; i++)
        {
            if (string.Equals(_demos[i].Title, _thenDemoTitle, StringComparison.OrdinalIgnoreCase))
            {
                RequestDemo(i);
                return;
            }
        }

        Logs.WriteWarning($"[DemosGame] CASAENGINE_THEN_DEMO '{_thenDemoTitle}' was not found.");
    }

    /// <summary>
    /// With <c>CASAENGINE_CAPTURE_ON_IBL_GENERATION=1</c> and a capture path, saves the back buffer of the first frame in which the
    /// IBL cache has generated something (its counter is above 0 at the end of the frame), next to the usual capture and with
    /// the suffix <c>-gen</c> before the extension. That frame is the first one drawn with the generated textures.
    /// </summary>
    private void TryCaptureOnIblGeneration()
    {
        if (!_captureOnIblGeneration
            || _generationCaptured
            || string.IsNullOrWhiteSpace(_automationScreenshotPath)
            || (IblCache?.GenerationCount ?? 0) == 0)
        {
            return;
        }

        _generationCaptured = true;
        string extension = Path.GetExtension(_automationScreenshotPath);
        string generationPath = Path.ChangeExtension(_automationScreenshotPath, null) + "-gen" + extension;
        CaptureScreenshot(generationPath);
        Logs.WriteInfo($"[DemosGame] Capture on IBL generation (counter {IblCache!.GenerationCount}): {generationPath}");
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
        LogFxaaPassCounts();
        LogSceneDepthCounts();
        LogSsaoPassCounts();
        bool succeeded = CaptureScreenshot(_automationScreenshotPath);
        Environment.ExitCode = succeeded ? 0 : 1;
        Exit();
    }

    /// <summary>
    /// Writes once, at the moment of the automation capture, how many FXAA passes each view drew in the frame just rendered
    /// (ADR-0114): a capture measurement of the anti-aliasing is only valid when the count of the views it concerns is at least 1
    /// (and 0 without FXAA).
    /// </summary>
    private void LogFxaaPassCounts()
    {
        var views = GameManager.ViewManager.Views;
        int total = 0;
        var perView = new System.Text.StringBuilder();
        for (int i = 0; i < views.Count; i++)
        {
            int passes = views[i].RenderStats.FxaaPasses;
            total += passes;
            perView.Append(i == 0 ? "" : ", ").Append("view ").Append(i).Append(": ").Append(passes);
        }

        Logs.WriteInfo($"[DemosGame] FXAA passes at capture: total {total} ({perView}).");
    }

    /// <summary>
    /// Writes once, at the moment of the automation capture, how many static and skinned meshes the scene depth pre-pass drew for each
    /// view in the frame just rendered (ADR-0116): a capture measurement of the SSAO is only valid when the counts of the views it
    /// concerns show the pre-pass ran (at least 1 for the geometry the demo draws), and are 0 without SSAO.
    /// </summary>
    private void LogSceneDepthCounts()
    {
        var views = GameManager.ViewManager.Views;
        int staticTotal = 0;
        int skinnedTotal = 0;
        var perView = new System.Text.StringBuilder();
        for (int i = 0; i < views.Count; i++)
        {
            int staticDraws = views[i].RenderStats.SceneDepthStaticDraws;
            int skinnedDraws = views[i].RenderStats.SceneDepthSkinnedDraws;
            staticTotal += staticDraws;
            skinnedTotal += skinnedDraws;
            perView.Append(i == 0 ? "" : ", ").Append("view ").Append(i).Append(": static ").Append(staticDraws).Append(" skinned ").Append(skinnedDraws);
        }

        Logs.WriteInfo($"[DemosGame] Scene depth draws at capture: static {staticTotal}, skinned {skinnedTotal} ({perView}).");
    }

    /// <summary>
    /// Writes once, at the moment of the automation capture, how many SSAO compositions each view drew in the frame just rendered
    /// (ADR-0116): a capture measurement of the ambient occlusion is only valid when the count of the views it concerns is at least 1
    /// (and 0 without SSAO), because a measurement that holds as well when the SSAO never runs proves nothing.
    /// </summary>
    private void LogSsaoPassCounts()
    {
        var views = GameManager.ViewManager.Views;
        int total = 0;
        var perView = new System.Text.StringBuilder();
        for (int i = 0; i < views.Count; i++)
        {
            int passes = views[i].RenderStats.SsaoPasses;
            total += passes;
            perView.Append(i == 0 ? "" : ", ").Append("view ").Append(i).Append(": ").Append(passes);
        }

        Logs.WriteInfo($"[DemosGame] SSAO passes at capture: total {total} ({perView}).");
    }

    /// <summary>
    /// Writes once, when the cost measurement ends (the line follows the <c>[FrameStats]</c> line of the same run), the settings that
    /// were in effect at the last measured image: colour pipeline mode, anti-aliasing, SSAO and the state of the clear coat row of the
    /// PBR demo. A cost line whose counters do not match these values is not valid (plan T4.1).
    /// </summary>
    private void LogDemoConfiguration()
    {
        var colorPipeline = GameManager.CurrentWorld.EnvironmentSettings.ColorPipeline;
        string clearCoatRow = _currentDemo is PbrMaterialDemo pbrDemo ? pbrDemo.ClearCoatRowState : "n/a";
        Logs.WriteInfo($"[DemoConfig] demo='{_currentDemo?.Title ?? "none"}' mode={colorPipeline.Mode} aa={colorPipeline.AntiAliasing} ssao={(colorPipeline.SsaoEnabled ? "On" : "Off")} clearCoatRow={clearCoatRow}");
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
            // The extension of the path chooses the format: .jpg or .jpeg writes a JPEG, anything else a PNG.
            string extension = Path.GetExtension(fullOutputPath);
            if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                screenshot.SaveAsJpeg(stream, width, height);
            }
            else
            {
                screenshot.SaveAsPng(stream, width, height);
            }

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
    /// Harness variables that put any demo in another colour pipeline (ADR-0104): <c>CASAENGINE_DEMO_COLOR_PIPELINE</c>
    /// (<c>Legacy</c> or <c>LinearHdr</c>), <c>CASAENGINE_DEMO_TONEMAPPER</c> (<c>AcesApprox</c> or <c>Reinhard</c>) and
    /// <c>CASAENGINE_DEMO_EXPOSURE</c> (a number, invariant culture). Applied to the world of the demo once the demo has
    /// built its scene, lighting and camera, then followed by <c>MarkDirty</c>. Without any variable nothing changes; an
    /// unreadable value is logged and ignored.
    /// </summary>
    private static void ApplyColorPipelineHarness(World world)
    {
        var modeText = Environment.GetEnvironmentVariable("CASAENGINE_DEMO_COLOR_PIPELINE");
        var tonemapperText = Environment.GetEnvironmentVariable("CASAENGINE_DEMO_TONEMAPPER");
        var exposureText = Environment.GetEnvironmentVariable("CASAENGINE_DEMO_EXPOSURE");
        if (string.IsNullOrWhiteSpace(modeText)
            && string.IsNullOrWhiteSpace(tonemapperText)
            && string.IsNullOrWhiteSpace(exposureText)
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_AA"))
            && !HasSsaoHarnessVariable()
            && !HasBloomHarnessVariable())
        {
            return;
        }

        var settings = world.EnvironmentSettings;
        if (!string.IsNullOrWhiteSpace(modeText))
        {
            if (Enum.TryParse<ColorPipelineMode>(modeText, true, out var mode))
            {
                settings.ColorPipeline.Mode = mode;
            }
            else
            {
                Logs.WriteWarning($"[DemosGame] CASAENGINE_DEMO_COLOR_PIPELINE '{modeText}' is not Legacy or LinearHdr; ignored.");
            }
        }

        if (!string.IsNullOrWhiteSpace(tonemapperText))
        {
            if (Enum.TryParse<TonemapOperator>(tonemapperText, true, out var tonemapper))
            {
                settings.ColorPipeline.Tonemapper = tonemapper;
            }
            else
            {
                Logs.WriteWarning($"[DemosGame] CASAENGINE_DEMO_TONEMAPPER '{tonemapperText}' is not AcesApprox or Reinhard; ignored.");
            }
        }

        if (!string.IsNullOrWhiteSpace(exposureText))
        {
            if (float.TryParse(exposureText, NumberStyles.Float, CultureInfo.InvariantCulture, out var exposure))
            {
                settings.ColorPipeline.Exposure = exposure;
            }
            else
            {
                Logs.WriteWarning($"[DemosGame] CASAENGINE_DEMO_EXPOSURE '{exposureText}' is not a number; ignored.");
            }
        }

        ApplyBloomHarness(settings.ColorPipeline);
        ApplyAntiAliasingHarness(settings.ColorPipeline);
        ApplySsaoHarness(settings.ColorPipeline);
        settings.MarkDirty();
        Logs.WriteInfo($"[DemosGame] Colour pipeline harness: {settings.ColorPipeline.Mode}, {settings.ColorPipeline.Tonemapper}, exposure {settings.ColorPipeline.Exposure}.");
    }

    /// <summary>
    /// Harness variables of the bloom (ADR-0108): <c>CASAENGINE_DEMO_BLOOM</c> (<c>1</c>/<c>true</c> or <c>0</c>/<c>false</c>) and the
    /// optional <c>CASAENGINE_DEMO_BLOOM_THRESHOLD</c>, <c>CASAENGINE_DEMO_BLOOM_INTENSITY</c> and <c>CASAENGINE_DEMO_BLOOM_RADIUS</c>
    /// (numbers, invariant culture). Without any variable nothing changes; an unreadable value is logged and ignored.
    /// </summary>
    private static bool HasBloomHarnessVariable()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_BLOOM"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_BLOOM_THRESHOLD"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_BLOOM_INTENSITY"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_BLOOM_RADIUS"));
    }

    private static void ApplyBloomHarness(ColorPipelineSettings colorPipeline)
    {
        if (!HasBloomHarnessVariable())
        {
            return;
        }

        var enabledText = Environment.GetEnvironmentVariable("CASAENGINE_DEMO_BLOOM");
        if (!string.IsNullOrWhiteSpace(enabledText))
        {
            if (enabledText == "1" || enabledText.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                colorPipeline.BloomEnabled = true;
            }
            else if (enabledText == "0" || enabledText.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                colorPipeline.BloomEnabled = false;
            }
            else
            {
                Logs.WriteWarning($"[DemosGame] CASAENGINE_DEMO_BLOOM '{enabledText}' is not 1, 0, true or false; ignored.");
            }
        }

        if (TryReadFloatVariable("CASAENGINE_DEMO_BLOOM_THRESHOLD", out var threshold))
        {
            colorPipeline.BloomThreshold = threshold;
        }

        if (TryReadFloatVariable("CASAENGINE_DEMO_BLOOM_INTENSITY", out var intensity))
        {
            colorPipeline.BloomIntensity = intensity;
        }

        if (TryReadFloatVariable("CASAENGINE_DEMO_BLOOM_RADIUS", out var radius))
        {
            colorPipeline.BloomRadius = radius;
        }

        Logs.WriteInfo($"[DemosGame] Bloom harness: enabled {colorPipeline.BloomEnabled}, threshold {colorPipeline.BloomThreshold}, intensity {colorPipeline.BloomIntensity}, radius {colorPipeline.BloomRadius}.");
    }

    /// <summary>
    /// Harness variable of the anti-aliasing (ADR-0113): <c>CASAENGINE_DEMO_AA</c> = <c>Fxaa</c> or <c>None</c>. Without it nothing
    /// changes; an unreadable value is logged and ignored.
    /// </summary>
    private static void ApplyAntiAliasingHarness(ColorPipelineSettings colorPipeline)
    {
        var text = Environment.GetEnvironmentVariable("CASAENGINE_DEMO_AA");
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (Enum.TryParse<AntiAliasingMode>(text, true, out var antiAliasing) && Enum.IsDefined(antiAliasing))
        {
            colorPipeline.AntiAliasing = antiAliasing;
            Logs.WriteInfo($"[DemosGame] Anti-aliasing harness: {colorPipeline.AntiAliasing}.");
        }
        else
        {
            Logs.WriteWarning($"[DemosGame] CASAENGINE_DEMO_AA '{text}' is not Fxaa or None; ignored.");
        }
    }

    /// <summary>
    /// Harness variables of the SSAO (ADR-0116): <c>CASAENGINE_DEMO_SSAO</c> (<c>1</c>/<c>true</c> or <c>0</c>/<c>false</c>) and the
    /// optional <c>CASAENGINE_DEMO_SSAO_RADIUS</c>, <c>CASAENGINE_DEMO_SSAO_INTENSITY</c> and <c>CASAENGINE_DEMO_SSAO_BIAS</c> (numbers,
    /// invariant culture). Without any variable nothing changes; an unreadable value is logged and ignored.
    /// </summary>
    private static bool HasSsaoHarnessVariable()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_SSAO"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_SSAO_RADIUS"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_SSAO_INTENSITY"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CASAENGINE_DEMO_SSAO_BIAS"));
    }

    private static void ApplySsaoHarness(ColorPipelineSettings colorPipeline)
    {
        if (!HasSsaoHarnessVariable())
        {
            return;
        }

        var enabledText = Environment.GetEnvironmentVariable("CASAENGINE_DEMO_SSAO");
        if (!string.IsNullOrWhiteSpace(enabledText))
        {
            if (enabledText == "1" || enabledText.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                colorPipeline.SsaoEnabled = true;
            }
            else if (enabledText == "0" || enabledText.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                colorPipeline.SsaoEnabled = false;
            }
            else
            {
                Logs.WriteWarning($"[DemosGame] CASAENGINE_DEMO_SSAO '{enabledText}' is not 1, 0, true or false; ignored.");
            }
        }

        if (TryReadFloatVariable("CASAENGINE_DEMO_SSAO_RADIUS", out var radius))
        {
            colorPipeline.SsaoRadius = radius;
        }

        if (TryReadFloatVariable("CASAENGINE_DEMO_SSAO_INTENSITY", out var intensity))
        {
            colorPipeline.SsaoIntensity = intensity;
        }

        if (TryReadFloatVariable("CASAENGINE_DEMO_SSAO_BIAS", out var bias))
        {
            colorPipeline.SsaoBias = bias;
        }

        Logs.WriteInfo($"[DemosGame] SSAO harness: enabled {colorPipeline.SsaoEnabled}, radius {colorPipeline.SsaoRadius}, intensity {colorPipeline.SsaoIntensity}, bias {colorPipeline.SsaoBias}.");
    }

    private static bool TryReadFloatVariable(string name, out float value)
    {
        value = 0.0f;
        var text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        Logs.WriteWarning($"[DemosGame] {name} '{text}' is not a number; ignored.");
        return false;
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