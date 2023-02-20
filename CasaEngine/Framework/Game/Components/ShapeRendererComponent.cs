using Genbox.VelcroPhysics.MonoGame.DebugView;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Game.Components
{
    public class ShapeRendererComponent : DrawableGameComponent
    {
        public static bool DisplayPhysics = false;
        private DebugView _shapeRenderer;

        public ShapeRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
        {
            game.Components.Add(this);
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
                _shapeRenderer.RenderDebugData(GameInfo.Instance.ActiveCamera.ProjectionMatrix, GameInfo.Instance.ActiveCamera.ViewMatrix);
            }
        }
    }
}
