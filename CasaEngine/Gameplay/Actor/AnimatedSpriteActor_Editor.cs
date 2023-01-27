using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Gameplay.Actor
{
    public partial class AnimatedSpriteActor
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        /// <returns></returns>
        public override bool CompareTo(BaseObject other_)
        {
            if (other_ is AnimatedSpriteActor)
            {
                AnimatedSpriteActor asa = (AnimatedSpriteActor)other_;
                //asa.m_Animations
            }

            return false;
        }
    }
}
