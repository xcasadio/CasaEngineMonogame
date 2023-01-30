using Microsoft.Xna.Framework;
using CasaEngine.Gameplay.Actor;

namespace CasaEngine.Gameplay
{
    public struct HitInfo
    {
        public Actor2D ActorAttacking, ActorHit;
        public Vector2 Direction;
        public Vector2 ContactPoint;
    }
}
