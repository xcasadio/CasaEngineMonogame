using Microsoft.Xna.Framework.Audio;
using CasaEngine.Gameplay.Actor.Object;

namespace CasaEngine.Audio
{
    public class Sound
        : BaseObject
    {
        readonly SoundEffect _soundEffect;
        SoundEffectInstance _soundEffectInstance;



        public SoundEffectInstance SoundEffectInstance
        {
            get => _soundEffectInstance;
            set => _soundEffectInstance = value;
        }



        public Sound(SoundEffect soundEffect)
        {
            if (soundEffect == null)
            {
                throw new ArgumentNullException("Sound() : SoundEffect is null");
            }

            _soundEffect = soundEffect;
        }



        public void Initialize()
        {
            _soundEffectInstance = _soundEffect.CreateInstance();
        }

#if EDITOR

        public override bool CompareTo(BaseObject other)
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
