namespace CasaEngine.Framework.Audio;

/// <summary>
/// Playback parameters of a single voice, in the ranges accepted by the audio backend.
/// Values are clamped on construction and NaN/Infinity is replaced by the neutral value:
/// a bad gameplay value must never throw from a Play call nor blow up the mixer.
/// </summary>
public readonly struct AudioVoiceParameters : IEquatable<AudioVoiceParameters>
{
    public const float MinVolume = 0f;
    public const float MaxVolume = 1f;

    /// <summary>-1 is fully left, 0 is centered, 1 is fully right.</summary>
    public const float MinPan = -1f;
    public const float MaxPan = 1f;

    /// <summary>-1 is one octave down, 0 is the original pitch, 1 is one octave up.</summary>
    public const float MinPitch = -1f;
    public const float MaxPitch = 1f;

    /// <summary>Full volume, centered, unaltered pitch, no looping.</summary>
    public static readonly AudioVoiceParameters Default = new(MaxVolume, 0f, 0f, false);

    public AudioVoiceParameters(float volume, float pan, float pitch, bool isLooped)
    {
        Volume = Sanitize(volume, MinVolume, MaxVolume, MaxVolume);
        Pan = Sanitize(pan, MinPan, MaxPan, 0f);
        Pitch = Sanitize(pitch, MinPitch, MaxPitch, 0f);
        IsLooped = isLooped;
    }

    public float Volume { get; }

    public float Pan { get; }

    public float Pitch { get; }

    public bool IsLooped { get; }

    public AudioVoiceParameters WithVolume(float volume) => new(volume, Pan, Pitch, IsLooped);

    public AudioVoiceParameters WithPan(float pan) => new(Volume, pan, Pitch, IsLooped);

    public AudioVoiceParameters WithPitch(float pitch) => new(Volume, Pan, pitch, IsLooped);

    public AudioVoiceParameters WithLooping(bool isLooped) => new(Volume, Pan, Pitch, isLooped);

    public bool Equals(AudioVoiceParameters other)
    {
        return Volume.Equals(other.Volume)
               && Pan.Equals(other.Pan)
               && Pitch.Equals(other.Pitch)
               && IsLooped == other.IsLooped;
    }

    public override bool Equals(object obj)
    {
        return obj is AudioVoiceParameters other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Volume, Pan, Pitch, IsLooped);
    }

    public override string ToString()
    {
        return $"Volume:{Volume} Pan:{Pan} Pitch:{Pitch} Looped:{IsLooped}";
    }

    private static float Sanitize(float value, float min, float max, float fallback)
    {
        if (float.IsNaN(value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }
}
