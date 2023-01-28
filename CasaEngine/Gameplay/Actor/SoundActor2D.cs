using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Gameplay.Actor.Object;
using Microsoft.Xna.Framework.Audio;

namespace CasaEngine.Gameplay.Actor
{
    /// <summary>
    /// 
    /// </summary>
    public class SoundActor2D
        : Actor2D
    {

        //SoundEffect 
        //SoundEffectInstance





        /// <summary>
        /// 
        /// </summary>
        public SoundActor2D()
            : base()
        {

        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override BaseObject Clone()
        {
            throw new NotImplementedException();
        }

#if EDITOR
        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        /// <returns></returns>
        public override bool CompareTo(BaseObject other_)
        {
            throw new NotImplementedException();
        }
#endif

    }
}
