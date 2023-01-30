using Microsoft.Xna.Framework.Audio;
using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Audio
{
    public class Sound
        : BaseObject
    {
        readonly SoundEffect m_SoundEffect;
        SoundEffectInstance m_SoundEffectInstance;



        public SoundEffectInstance SoundEffectInstance
        {
            get => m_SoundEffectInstance;
            set => m_SoundEffectInstance = value;
        }



        public Sound(SoundEffect soundEffect_)
        {
            if (soundEffect_ == null)
            {
                throw new ArgumentNullException("Sound() : SoundEffect is null");
            }

            m_SoundEffect = soundEffect_;
        }



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
