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
    private bool _automationScreenshotCaptured;

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

        int startupDemoIndex = ResolveStartupDemoIndex();
        _demoCycle = DemoCycleAutomation.TryCreate(_demos.Count, startupDemoIndex);

        // The first demo creates the first world, so GameManager.EndLoadContent never falls back to FirstWorldLoaded.
        ChangeDemo(startupDemoIndex);
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

        _prevKeyboard = kb;

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || kb.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

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