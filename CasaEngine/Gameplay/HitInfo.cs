using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Physics2D;
using Microsoft.Xna.Framework;
using CasaEngine.Gameplay.Actor;

namespace CasaEngine.Gameplay
{
    /// <summary>
    /// 
    /// </summary>
    public struct HitInfo
    {
        public Actor2D ActorAttacking, ActorHit;
        public Vector2 Direction;
        public Vector2 ContactPoint;
    }
}
