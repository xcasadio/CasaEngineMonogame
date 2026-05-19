using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Demos.Demos;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.DebugTools;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.Scene.World;
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
    private KeyboardState _prevKeyboard;
    private bool _automationScreenshotCaptured;

    // ---- Demo navigation UI ----
    private DemoInfoScreen?  _demoInfoScreen;
    private DemoHintOverlay? _demoHintOverlay;
    private bool             _demoInfoVisible = true;

    protected override void Initialize()
    {
        Logs.AddLogger(new DebugLogger());
        Logs.AddLogger(new FileLogger("log.txt"));
        Logs.Verbosity = LogVerbosity.Trace;

        if (!string.IsNullOrWhiteSpace(_automationScreenshotPath))
        {
            _demoInfoVisible = false;
        }

        // Push demo UI screens whenever the engine finishes building views for a world.
        // On startup the first demo is prepared before GameManager loads the world, so
        // its camera/UI setup must be finalized only after the world and views exist.
        GameManager.WorldLoaded += (_, _) => OnWorldLoaded();

        EngineEnvironment.ProjectPath = Path.Combine(Environment.CurrentDirectory, "Content");
        var projectSettings = GameSettings.ProjectSettings;
        projectSettings.IsMouseVisible = true;
        projectSettings.WindowTitle = "CasaEngine demos";
        projectSettings.AllowUserResizing = true;

        new DebugAxisComponent(this);

        base.Initialize();
    }

    protected override void LoadContentPrivate()
    {
        AssetCatalog.Load("Content\\AssetInfos.json");

        var world = new World();
        GameManager.SetWorldToLoad(world);
        this.GetGameComponent<PhysicsDebugViewRendererComponent>().DisplayPhysics = true;

        _demos.Add(new Collision3dBasicDemo());
        _demos.Add(new Collision2dBasicDemo());
        _demos.Add(new StaticModelDemo());
        _demos.Add(new MaterialDemo());
        _demos.Add(new EnvironmentShowcaseDemo());
        //_demos.Add(new TileMapDemo()); // 2
        _demos.Add(new SkinnedMeshDemo());
        _demos.Add(new StaticShadowValidationDemo());
        _demos.Add(new AnimationBlendDemo());
        _demos.Add(new AnimationIkDemo());
        _demos.Add(new SceneManagementDemo());
        _demos.Add(new SplitScreenDemo()); // 5
        _demos.Add(new RenderToTextureDemo());
        _demos.Add(new WorldSpaceUIDemo());
        _demos.Add(new ViewManagerSandbox());
        _demos.Add(new UIOverlayDemo());

        ChangeDemo(ResolveStartupDemoIndex());
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

        _demoInfoScreen  = new DemoInfoScreen(entries, _currentDemoIndex, ChangeDemo);
        _demoHintOverlay = new DemoHintOverlay();

        uiView.PushScreen(_demoInfoScreen);
        uiView.PushScreen(_demoHintOverlay);

        bool automationScreenshotEnabled = !string.IsNullOrWhiteSpace(_automationScreenshotPath);
        _demoInfoScreen.SetVisible(!automationScreenshotEnabled && _demoInfoVisible);
        _demoHintOverlay.SetVisible(!automationScreenshotEnabled && !_demoInfoVisible);
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
        _currentDemo?.PostDraw(this, gameTime);
        TryCaptureAutomationScreenshot(gameTime);
    }

    protected override void Update(GameTime gameTime)
    {
        _currentDemo.Update(gameTime);

        var kb = IsActive ? Keyboard.GetState() : new KeyboardState();

        // F1 — toggle demo info panel visibility
        if (kb.IsKeyDown(Keys.F1) && !_prevKeyboard.IsKeyDown(Keys.F1))
        {
            _demoInfoVisible = !_demoInfoVisible;
            _demoInfoScreen?.SetVisible(_demoInfoVisible);
            _demoHintOverlay?.SetVisible(!_demoInfoVisible);
        }

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