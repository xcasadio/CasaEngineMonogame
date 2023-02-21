using System.Numerics;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace SandBoxGame
{
    public class SandBoxGame : CasaEngineGame
    {
        public SandBoxGame()
        {
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        protected override void LoadContent()
        {
            var world = new World();
            GameInfo.Instance.CurrentWorld = world;

            var entity = new Entity();
            var camera = new ArcBallCameraComponent(entity);
            entity.ComponentManager.Components.Add(camera);
            GameInfo.Instance.ActiveCamera = camera;
            camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
            world.AddObjectImmediately(entity);

            entity = new Entity();
            entity.Coordinates.LocalPosition += Vector3.Up * 0.5f;
            var meshComponent = new MeshComponent(entity);
            entity.ComponentManager.Components.Add(meshComponent);
            meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
            meshComponent.Mesh.Texture = Content.Load<Texture2D>("checkboard");
            world.AddObjectImmediately(entity);

            base.LoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // TODO: Add your update logic here

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }
    }
}