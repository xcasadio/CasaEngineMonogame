﻿using System;
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

    protected override void Initialize()
    {
        Logs.AddLogger(new DebugLogger());
        Logs.AddLogger(new FileLogger("log.txt"));
        Logs.Verbosity = LogVerbosity.Trace;

        EngineEnvironment.ProjectPath = Path.Combine(Environment.CurrentDirectory, "Content");
        GameSettings.ProjectSettings.IsMouseVisible = true;
        GameSettings.ProjectSettings.WindowTitle = "CasaEngine demos";
        GameSettings.ProjectSettings.AllowUserResizing = true;

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
        _demos.Add(new TileMapDemo());
        _demos.Add(new SkinnedMeshDemo());
        _demos.Add(new SceneManagementDemo());

        ChangeDemo(1);
    }

    private void ChangeDemo(int index)
    {
        GameManager.CurrentWorld.ClearEntities();
        GameManager.CurrentWorld.ClearScreens();
        _currentDemo?.Clean();

        _currentDemo = _demos[index];
        _currentDemo.Initialize(this);
        var camera = _currentDemo.CreateCamera(this);
        GameManager.CurrentWorld.LoadContent(this);
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
}