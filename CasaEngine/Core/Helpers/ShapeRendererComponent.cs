using CasaEngine.Core.Maths.Shape2D;
using CasaEngine.Framework.Game;
using Genbox.VelcroPhysics.MonoGame.DebugView;
using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Helpers
{
    public class ShapeRendererComponent : DrawableGameComponent
    {
        public static bool DisplayPhysics = false;

        private DebugView _shapeRenderer;
        Matrix _projectionMatrix, _view;

        public ShapeRendererComponent(Game game) : base(game)
        {
            if (game == null)
            {
                throw new ArgumentException("ScreenManagerComponent : Game is null");
            }

            game.Components.Add(this);

            //Enabled = false;
            //Visible = false;

            UpdateOrder = (int)ComponentUpdateOrder.DebugPhysics;
            DrawOrder = (int)ComponentDrawOrder.DebugPhysics;
        }

        public void SetCurrentPhysicsWorld(Genbox.VelcroPhysics.Dynamics.World world)
        {
            _shapeRenderer = new DebugView(world);
        }

        public override void Draw(GameTime gameTime)
        {
            if (DisplayPhysics)
            {
                //TODO : Get matrix from camera
                _shapeRenderer.RenderDebugData(ref _projectionMatrix, ref _view);
            }
        }
    }
}
