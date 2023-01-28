using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Audio;
using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Audio
{
    /// <summary>
    /// 
    /// </summary>
    public class Sound
        : BaseObject
    {

        SoundEffect m_SoundEffect;
        SoundEffectInstance m_SoundEffectInstance;



        /// <summary>
        /// 
        /// </summary>
        public SoundEffectInstance SoundEffectInstance
        {
            get { return m_SoundEffectInstance; }
            set { m_SoundEffectInstance = value; }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="soundEffect_"></param>
        public Sound(SoundEffect soundEffect_)
        {
            if (soundEffect_ == null)
            {
                throw new ArgumentNullException("Sound() : SoundEffect is null");
            }

            m_SoundEffect = soundEffect_;
        }



        /// <summary>
        /// 
        /// </summary>
        public void Initialize()
        {
            m_SoundEffectInstance = m_SoundEffect.CreateInstance();
        }

#if EDITOR

        public override bool CompareTo(BaseObject other_)
        {
            throw new Exception("The method or operation is not implemented.");
        }

#endif

        public override BaseObject Clone()
        {
            throw new Exception("The method or operation is not implemented.");
        }

    }
}
