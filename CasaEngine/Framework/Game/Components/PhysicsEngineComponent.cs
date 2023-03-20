using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Game.Components
{
    public class PhysicsEngineComponent : GameComponent
    {
        private readonly PhysicsEngine _physicsEngine;

        public PhysicsEngineComponent(CasaEngineGame game) : base(game)
        {
            game.Components.Add(this);
            UpdateOrder = (int)ComponentUpdateOrder.Physics;

            _physicsEngine = new PhysicsEngine();
        }

        public override void Initialize()
        {
            _physicsEngine.Initialize();
            base.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            _physicsEngine.Update(gameTime);
            base.Update(gameTime);
        }
    }
}