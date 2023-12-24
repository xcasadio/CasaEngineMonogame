using Microsoft.Xna.Framework.Audio;

namespace CasaEngine.Framework.Audio;

public class Sound
{
    private readonly SoundEffect _soundEffect;

    public SoundEffectInstance SoundEffectInstance { get; private set; }

    public Sound(SoundEffect soundEffect)
    {
        _soundEffect = soundEffect ?? throw new ArgumentNullException(nameof(soundEffect));
    }

    public void Initialize()
    {
        SoundEffectInstance = _soundEffect.CreateInstance();
    }
}