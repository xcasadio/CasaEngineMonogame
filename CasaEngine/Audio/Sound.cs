using CasaEngine.Entities;
using Microsoft.Xna.Framework.Audio;

namespace CasaEngine.Audio
{
    public class Sound
        : Entity
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
    }
}
