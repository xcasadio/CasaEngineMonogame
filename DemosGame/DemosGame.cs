using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Core.Design;
using CasaEngine.Core.Logger;
using CasaEngine.Engine;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.World;
using DemosGame.Demos;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DemosGame
{
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

        protected override void LoadContent()
        {
            GameSettings.AssetInfoManager.Load("Content\\AssetInfos.json", SaveOption.Editor);

            var world = new World();
            GameManager.CurrentWorld = world;
            //PhysicsDebugViewRendererComponent.DisplayPhysics = true;
            base.LoadContent();

            _demos.Add(new Collision3dBasicDemo());
            _demos.Add(new Collision2dBasicDemo());
            _demos.Add(new TileMapDemo());
            _demos.Add(new SkinnedMeshDemo());

            ChangeDemo(3);
        }

        private void ChangeDemo(int index)
        {
            _currentDemo?.Clean(GameManager.CurrentWorld);

            _currentDemo = _demos[index];
            _currentDemo.Initialize(this);
            var camera = _currentDemo.CreateCamera(this);
            GameManager.CurrentWorld.Initialize(this);
            GameManager.ActiveCamera = camera;

            Window.Title = _currentDemo.Title;
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

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}