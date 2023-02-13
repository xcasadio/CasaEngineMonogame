using System.Linq;
using CasaEngine.Game;
using CasaEngine.Helpers;
using Microsoft.Xna.Framework;

namespace Pong
{
    public class PongGame : CasaEngineGame
    {
        private Player[] players = { new Player() };
        private ShapeRendererComponent _shapeRendererComponent;

        public PongGame()
        {
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();

            _shapeRendererComponent = (ShapeRendererComponent)Components.First(x => x is ShapeRendererComponent);
            _shapeRendererComponent.SetCurrentPhysicsWorld(null);
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            players[0].Position = new Vector2(100, 100);
        }

        protected override void Update(float elapsedTime)
        {
            foreach (var player in players)
            {
                player.Update(elapsedTime);
            }
        }

        protected override void Draw(float elapsedTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            foreach (var player in players)
            {
                player.Draw(elapsedTime, _shapeRendererComponent);
            }
        }
    }
}