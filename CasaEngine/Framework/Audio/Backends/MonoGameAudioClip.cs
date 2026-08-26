using Microsoft.Xna.Framework.Audio;

namespace CasaEngine.Framework.Audio.Backends;

/// <summary>
/// <see cref="IAudioClip"/> backed by a MonoGame <see cref="SoundEffect"/>, i.e. a sound fully
/// resident in memory. Streamed sounds do not go through this type: see the streaming voices.
/// </summary>
public sealed class MonoGameAudioClip : IAudioClip
{
    private readonly SoundEffect _soundEffect;

    public MonoGameAudioClip(SoundEffect soundEffect)
    {
        _soundEffect = soundEffect ?? throw new ArgumentNullException(nameof(soundEffect));
    }

    /// <summary>
    /// The underlying resource. Only the MonoGame backend is expected to use it; the engine
    /// audio layer works against <see cref="IAudioClip"/>.
    /// </summary>
    public SoundEffect SoundEffect => _soundEffect;

    // MonoGame does not expose the sample rate or the channel count of a SoundEffect.
    // Duration is the only descriptive value available, so the two others report 0 rather
    // than a made-up value; nothing in the non-streamed path needs them.
    public int SampleRate => 0;

    public int ChannelCount => 0;

    public TimeSpan Duration => _soundEffect.IsDisposed ? TimeSpan.Zero : _soundEffect.Duration;

    public bool IsDisposed => _soundEffect.IsDisposed;

    public void Dispose()
    {
        if (!_soundEffect.IsDisposed)
        {
            _soundEffect.Dispose();
        }
    }
}
