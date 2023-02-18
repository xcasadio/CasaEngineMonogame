using Microsoft.Xna.Framework;
using CasaEngine.Framework.Gameplay.Actor;

namespace CasaEngine.Framework.Gameplay
{
    public struct HitInfo
    {
        public Actor2D ActorAttacking, ActorHit;
        public Vector2 Direction;
        public Vector2 ContactPoint;
    }
}
