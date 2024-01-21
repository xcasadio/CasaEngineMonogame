using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Logs;
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

    protected override void Initialize()
    {
        LogManager.Instance.AddLogger(new DebugLogger());
        LogManager.Instance.AddLogger(new FileLogger("log.txt"));
        LogManager.Instance.Verbosity = LogVerbosity.Trace;

        EngineEnvironment.ProjectPath = Path.Combine(Environment.CurrentDirectory, "Content");
        GameSettings.ProjectSettings.IsMouseVisible = true;
        GameSettings.ProjectSettings.WindowTitle = "CasaEngine demos";
        GameSettings.ProjectSettings.AllowUserResizing = true;

        new AxisComponent(this);

        base.Initialize();
    }

    protected override void LoadContentPrivate()
    {
        GameSettings.AssetInfoManager.Load("Content\\AssetInfos.json", SaveOption.Editor);

        var world = new World();
        GameManager.CurrentWorld = world;
        this.GetGameComponent<PhysicsDebugViewRendererComponent>().DisplayPhysics = true;

        _demos.Add(new Collision3dBasicDemo());
        _demos.Add(new Collision2dBasicDemo());
        _demos.Add(new TileMapDemo());
        _demos.Add(new SkinnedMeshDemo());
        _demos.Add(new SceneManagementDemo());

        ChangeDemo(0);
    }

    private void ChangeDemo(int index)
    {
        GameManager.CurrentWorld.ClearEntities();
        _currentDemo?.Clean();

        _currentDemo = _demos[index];
        _currentDemo.Initialize(this);
        var camera = _currentDemo.CreateCamera(this);
        GameManager.CurrentWorld.Initialize(this);
        _currentDemo.InitializeCamera(camera);
        GameManager.ActiveCamera = camera;

        Window.Title = _currentDemo.Title;
        //GameManager.CurrentWorld.Initialize(this);
    }

    protected override void Update(GameTime gameTime)
    {
        _currentDemo.Update(gameTime);

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (Keyboard.GetState().IsKeyDown(Keys.Add))
        {
        }

        if (Keyboard.GetState().IsKeyDown(Keys.Subtract))
        {
        }

        base.Update(gameTime);
    }

    /*protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);

        _currentDemo.Update(gameTime);
    }*/
}