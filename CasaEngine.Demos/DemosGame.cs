using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Core.Log;
using CasaEngine.Demos.Demos;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos;

public class DemosGame : CasaEngineGame
{
    private readonly List<Demo> _demos = new();
    private Demo _currentDemo;
    private int _currentDemoIndex;
    private KeyboardState _prevKeyboard;

    // ---- Demo navigation UI ----
    private DemoInfoScreen?  _demoInfoScreen;
    private DemoHintOverlay? _demoHintOverlay;
    private bool             _demoInfoVisible = true;

    protected override void Initialize()
    {
        Logs.AddLogger(new DebugLogger());
        Logs.AddLogger(new FileLogger("log.txt"));
        Logs.Verbosity = LogVerbosity.Trace;

        EngineEnvironment.ProjectPath = Path.Combine(Environment.CurrentDirectory, "Content");
        var projectSettings = GameSettings.ProjectSettings;
        projectSettings.IsMouseVisible = true;
        projectSettings.WindowTitle = "CasaEngine demos";
        projectSettings.AllowUserResizing = true;

        new AxisComponent(this);

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
        //_demos.Add(new TileMapDemo()); // 2
        _demos.Add(new SkinnedMeshDemo());
        _demos.Add(new SceneManagementDemo());
        _demos.Add(new SplitScreenDemo()); // 5
        _demos.Add(new RenderToTextureDemo());
        _demos.Add(new ViewManagerSandbox());
        _demos.Add(new UIOverlayDemo());

        ChangeDemo(0);
    }

    private void ChangeDemo(int index)
    {
        _currentDemoIndex = Math.Clamp(index, 0, _demos.Count - 1);
        var currentWorld = GameManager.CurrentWorld;
        currentWorld.ClearEntities();
        _currentDemo?.Clean();

        _currentDemo = _demos[_currentDemoIndex];
        _currentDemo.Initialize(this);
        var camera = _currentDemo.CreateCamera(this);
        // Clear any views registered by the previous demo so that World.LoadContent
        // can register a fresh default view (it only does so when Views.Count == 0).
        GameManager.ViewManager.Clear();
        currentWorld.LoadContent(this);
        _currentDemo.InitializeCamera(camera);

        Window.Title = _currentDemo.Title;
        RefreshDemoUI();
    }

    // ---- Demo navigation UI helpers ----

    private UIRoot? GetUIRoot()
        => GameManager.ViewManager.Views.FirstOrDefault(v => v.UIRoot != null)?.UIRoot;

    /// <summary>
    /// (Re)creates the DemoInfoScreen and DemoHintOverlay on the current UIRoot.
    /// Called after every demo change because ViewManager.Clear() tears down the old UIRoot.
    /// </summary>
    private void RefreshDemoUI()
    {
        var uiRoot = GetUIRoot();
        if (uiRoot == null) return;

        var entries = _demos
            .Select(d => (d.Title, d.Description))
            .ToList();

        _demoInfoScreen  = new DemoInfoScreen(entries, _currentDemoIndex, ChangeDemo);
        _demoHintOverlay = new DemoHintOverlay();

        uiRoot.PushScreen(_demoInfoScreen);
        uiRoot.PushScreen(_demoHintOverlay);

        _demoInfoScreen.SetVisible(_demoInfoVisible);
        _demoHintOverlay.SetVisible(!_demoInfoVisible);
    }

    protected override void OnViewsResized(int width, int height)
    {
        _currentDemo?.OnScreenResized(this, width, height);
    }

    protected override void AfterRenderPipeline(GameTime gameTime)
    {
        _currentDemo?.PostDraw(this, gameTime);
    }

    protected override void Update(GameTime gameTime)
    {
        _currentDemo.Update(gameTime);

        var kb = Keyboard.GetState();

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

        if (kb.IsKeyDown(Keys.Add))
        {
        }

        if (kb.IsKeyDown(Keys.Subtract))
        {
        }

        base.Update(gameTime);

        // base.Update calls GameManager.UpdateWorld, which on the very first frame
        // sees _isNewWorld=true (set by SetWorldToLoad) and rebuilds the views,
        // discarding every UIRoot that ChangeDemo set up during LoadContentPrivate.
        // Detect this and re-push the demo UI screens onto the fresh UIRoot.
        var uiRootAfter = GetUIRoot();
        if (uiRootAfter != null
            && _demoInfoScreen != null
            && !uiRootAfter.ScreenStack.Screens.Contains(_demoInfoScreen))
        {
            RefreshDemoUI();
        }
    }
}