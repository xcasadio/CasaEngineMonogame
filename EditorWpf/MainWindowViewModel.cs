using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.World;
using EditorWpf.Controls.MonoGame;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EditorWpf
{
    public class MainWindowViewModel : MonoGameViewModel
    {
        private GameManager _gameManager;
        private CasaEngineGame _casaEngineGame;

        public override void Initialize()
        {
            base.Initialize();

            _casaEngineGame = new CasaEngineGame();
            _gameManager = new GameManager(_casaEngineGame, GraphicsDeviceService);
            _gameManager.Initialize();
            foreach (var component in _casaEngineGame.Components)
            {
                component.Initialize();
            }
        }

        public override void LoadContent()
        {
            //test
            var world = new World();
            GameInfo.Instance.CurrentWorld = world;

            var entity = new Entity();
            var camera = new ArcBallCameraComponent(entity);
            entity.ComponentManager.Components.Add(camera);
            GameInfo.Instance.ActiveCamera = camera;
            camera.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
            world.AddObjectImmediately(entity);

            entity = new Entity();
            //entity.Coordinates.LocalPosition += Vector3.Up * 0.5f;
            var meshComponent = new MeshComponent(entity);
            entity.ComponentManager.Components.Add(meshComponent);
            meshComponent.Mesh = new BoxPrimitive(GraphicsDevice).CreateMesh();
            meshComponent.Mesh.Texture = Content.Load<Texture2D>("checkboard");
            world.AddObjectImmediately(entity);
            // end test

            _gameManager.BeginLoadContent();
            base.LoadContent();
            _gameManager.EndLoadContent();
        }

        public override void Update(GameTime gameTime)
        {
            _gameManager.BeginUpdate(gameTime);
            foreach (GameComponent component in _casaEngineGame.Components)
            {
                if (component.Enabled)
                {
                    component.Update(gameTime);
                }
            }
            base.Update(gameTime);
            _gameManager.EndUpdate(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _gameManager.BeginDraw(gameTime);
            foreach (var component in _casaEngineGame.Components)
            {
                if (component is DrawableGameComponent { Visible: true } gameComponent)
                {
                    gameComponent.Draw(gameTime);
                }
            }
            base.Draw(gameTime);
            _gameManager.EndDraw(gameTime);
        }
    }
}