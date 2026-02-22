using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Log;
using CasaEngine.Demos.Demos;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
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
        currentWorld.ClearScreens();
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

        // Navigate demos with Left/Right arrow keys
        if (kb.IsKeyDown(Keys.Right) && !_prevKeyboard.IsKeyDown(Keys.Right))
        {
            ChangeDemo((_currentDemoIndex + 1) % _demos.Count);
        }
        else if (kb.IsKeyDown(Keys.Left) && !_prevKeyboard.IsKeyDown(Keys.Left))
        {
            ChangeDemo((_currentDemoIndex - 1 + _demos.Count) % _demos.Count);
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
    }
}