using System.Collections.Generic;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Physics;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DemosGame
{
    public class DemosGame : CasaEngineGame
    {
        private readonly List<Demo> _demos = new();
        private Demo? _currentDemo;

        protected override void Initialize()
        {
            GameSettings.ProjectSettings.IsMouseVisible = true;
            GameSettings.ProjectSettings.WindowTitle = "CasaEngine demos";
            GameSettings.ProjectSettings.AllowUserResizing = true;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            var world = new World();
            GameManager.CurrentWorld = world;
            PhysicsDebugViewRendererComponent.DisplayPhysics = true;
            base.LoadContent();

            _demos.Add(new BasicDemo());
            _demos.Add(new Basic2dDemo());

            ChangeDemo(0);
        }

        private void ChangeDemo(int index)
        {
            _currentDemo?.Clean(GameManager.CurrentWorld);

            _currentDemo = _demos[index];
            _currentDemo.Initialize(this);

            //============ Camera ===============
            var entity = new Entity();
            var camera = new ArcBallCameraComponent(entity);
            entity.ComponentManager.Components.Add(camera);
            GameManager.ActiveCamera = camera;
            camera.SetCamera(Vector3.Backward * 15 + Vector3.Up * 12, Vector3.Zero, Vector3.Up);
            GameManager.CurrentWorld.AddEntityImmediately(entity);

            GameManager.CurrentWorld.Initialize(this);
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