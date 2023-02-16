using System.Linq;
using CasaEngine.Game;
using CasaEngine.Helpers;
using Microsoft.Xna.Framework;

namespace Pong
{
    public class PongGame : CasaEngineGame
    {
        private ShapeRendererComponent _shapeRendererComponent;

        public PongGame()
        {
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            //GameInfo.Instance.CurrentWorld = new World();
            //var baseObject = new BaseObject();
            //baseObject.ComponentManager.Components.Add(new PlayerComponent(baseObject));
            //GameInfo.Instance.CurrentWorld.AddObjectImmediately(baseObject);
            //
            //baseObject.Coordinates.LocalPosition = new Vector3(100, 100, 0.0f);

            //baseObject.Save("test.json", SaveOption.Editor);

            base.Initialize();
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            _shapeRendererComponent = (ShapeRendererComponent)Components.First(x => x is ShapeRendererComponent);

            _shapeRendererComponent.SetCurrentPhysicsWorld(GameInfo.Instance.CurrentWorld.PhysicWorld);
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            base.Draw(gameTime);
        }
    }
}