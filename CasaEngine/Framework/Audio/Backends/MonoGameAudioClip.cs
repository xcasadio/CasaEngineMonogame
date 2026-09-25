using Microsoft.Xna.Framework.Audio;

namespace CasaEngine.Framework.Audio.Backends;

/// <summary>
/// <see cref="IAudioClip"/> backed by a MonoGame <see cref="SoundEffect"/>, i.e. a sound fully
/// resident in memory. Streamed sounds do not go through this type: see the streaming voices.
/// </summary>
/// <remarks>
/// Also implements <see cref="IAudioClipSamples"/>: when <see cref="SoundEffectLoader"/> decoded
/// the source wav as 16 bit PCM mono, the samples are kept here too, so
/// <see cref="AudioService.PlayClipStereo"/> can feed them on a software stereo voice (ADR-0039).
/// The constructor without samples leaves <see cref="MonoSamples"/> empty, exactly as before.
/// </remarks>
public sealed class MonoGameAudioClip : IAudioClip, IAudioClipSamples
{
    private readonly SoundEffect _soundEffect;
    private readonly ReadOnlyMemory<short> _monoSamples;
    private readonly int _sampleRate;
    private readonly int _channelCount;

    public MonoGameAudioClip(SoundEffect soundEffect)
    {
        _soundEffect = soundEffect ?? throw new ArgumentNullException(nameof(soundEffect));
        _monoSamples = ReadOnlyMemory<short>.Empty;
        _sampleRate = 0;
        _channelCount = 0;
    }

    /// <summary>
    /// Same as <see cref="MonoGameAudioClip(SoundEffect)"/>, but also keeps the decoded mono
    /// samples of the source wav next to it, so this clip can be played on a software stereo
    /// voice. <paramref name="monoSamples"/> is used as-is, not copied.
    /// </summary>
    public MonoGameAudioClip(SoundEffect soundEffect, short[] monoSamples, int sampleRate)
    {
        _soundEffect = soundEffect ?? throw new ArgumentNullException(nameof(soundEffect));
        ArgumentNullException.ThrowIfNull(monoSamples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        _monoSamples = monoSamples;
        _sampleRate = sampleRate;
        _channelCount = 1;
    }

    /// <summary>
    /// The underlying resource. Only the MonoGame backend is expected to use it; the engine
    /// audio layer works against <see cref="IAudioClip"/>.
    /// </summary>
    public SoundEffect SoundEffect => _soundEffect;

    // MonoGame does not expose the sample rate or the channel count of a SoundEffect: without
    // decoded samples, the two report 0 rather than a made-up value, since nothing in the
    // non-streamed path needs them.
    public int SampleRate => _sampleRate;

    public int ChannelCount => _channelCount;

    /// <inheritdoc/>
    public ReadOnlyMemory<short> MonoSamples => _monoSamples;

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
