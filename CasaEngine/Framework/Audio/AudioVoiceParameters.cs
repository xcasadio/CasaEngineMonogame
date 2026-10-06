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

    /// <summary>Largest accepted <see cref="RateMultiplier"/>.</summary>
    public const float MaxRateMultiplier = 16f;

    private readonly float _rateMultiplier;

    public AudioVoiceParameters(float volume, float pan, float pitch, bool isLooped)
        : this(volume, pan, pitch, isLooped, false, 0, 0, 1f)
    {
    }

    private AudioVoiceParameters(float volume, float pan, float pitch, bool isLooped, bool hasLoopRegion, int loopStartFrame, int loopEndFrame, float rateMultiplier)
    {
        Volume = Sanitize(volume, MinVolume, MaxVolume, MaxVolume);
        Pan = Sanitize(pan, MinPan, MaxPan, 0f);
        Pitch = Sanitize(pitch, MinPitch, MaxPitch, 0f);
        IsLooped = isLooped;
        HasLoopRegion = hasLoopRegion;
        LoopStartFrame = loopStartFrame;
        LoopEndFrame = loopEndFrame;
        _rateMultiplier = SanitizeRateMultiplier(rateMultiplier);
    }

    public float Volume { get; }

    public float Pan { get; }

    public float Pitch { get; }

    public bool IsLooped { get; }

    /// <summary>True when a loop region was set; otherwise a looped voice loops the whole clip.</summary>
    public bool HasLoopRegion { get; }

    /// <summary>First frame of the loop region (inclusive); 0 without a region.</summary>
    public int LoopStartFrame { get; }

    /// <summary>End of the loop region (exclusive); 0 without a region.</summary>
    public int LoopEndFrame { get; }

    /// <summary>
    /// Playback speed factor applied on top of <see cref="Pitch"/>, in ]0, 16]; 1 by default. Values
    /// that are NaN, not positive or above 16 are replaced by 1.
    /// </summary>
    public float RateMultiplier => _rateMultiplier == 0f ? 1f : _rateMultiplier;

    public AudioVoiceParameters WithVolume(float volume) => new(volume, Pan, Pitch, IsLooped, HasLoopRegion, LoopStartFrame, LoopEndFrame, RateMultiplier);

    public AudioVoiceParameters WithPan(float pan) => new(Volume, pan, Pitch, IsLooped, HasLoopRegion, LoopStartFrame, LoopEndFrame, RateMultiplier);

    public AudioVoiceParameters WithPitch(float pitch) => new(Volume, Pan, pitch, IsLooped, HasLoopRegion, LoopStartFrame, LoopEndFrame, RateMultiplier);

    public AudioVoiceParameters WithLooping(bool isLooped) => new(Volume, Pan, Pitch, isLooped, HasLoopRegion, LoopStartFrame, LoopEndFrame, RateMultiplier);

    /// <summary>
    /// Loops frames [startFrame, endFrame[ instead of the whole clip. Only a looped voice uses it. The
    /// region is validated against the clip length when the voice starts: an invalid one (start below 0,
    /// end past the clip, start not below end) falls back to the whole clip with a warning.
    /// </summary>
    /// <remarks>The MonoGame backend ignores the region and loops the whole clip.</remarks>
    public AudioVoiceParameters WithLoopRegion(int startFrame, int endFrame) => new(Volume, Pan, Pitch, IsLooped, true, startFrame, endFrame, RateMultiplier);

    /// <summary>Removes the loop region: a looped voice loops the whole clip.</summary>
    public AudioVoiceParameters WithoutLoopRegion() => new(Volume, Pan, Pitch, IsLooped, false, 0, 0, RateMultiplier);

    public AudioVoiceParameters WithRateMultiplier(float rateMultiplier) => new(Volume, Pan, Pitch, IsLooped, HasLoopRegion, LoopStartFrame, LoopEndFrame, rateMultiplier);

    public bool Equals(AudioVoiceParameters other)
    {
        return Volume.Equals(other.Volume)
               && Pan.Equals(other.Pan)
               && Pitch.Equals(other.Pitch)
               && IsLooped == other.IsLooped
               && HasLoopRegion == other.HasLoopRegion
               && LoopStartFrame == other.LoopStartFrame
               && LoopEndFrame == other.LoopEndFrame
               && RateMultiplier.Equals(other.RateMultiplier);
    }

    public override bool Equals(object obj)
    {
        return obj is AudioVoiceParameters other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Volume);
        hash.Add(Pan);
        hash.Add(Pitch);
        hash.Add(IsLooped);
        hash.Add(HasLoopRegion);
        hash.Add(LoopStartFrame);
        hash.Add(LoopEndFrame);
        hash.Add(RateMultiplier);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        // The text of the four original fields is unchanged; the region and the multiplier are only
        // appended when they differ from their defaults, so existing output stays the same.
        var region = HasLoopRegion ? $" Region:[{LoopStartFrame},{LoopEndFrame})" : string.Empty;
        var rate = RateMultiplier != 1f ? $" Rate:x{RateMultiplier}" : string.Empty;
        return $"Volume:{Volume} Pan:{Pan} Pitch:{Pitch} Looped:{IsLooped}{region}{rate}";
    }

    private static float SanitizeRateMultiplier(float value)
    {
        return float.IsNaN(value) || value <= 0f || value > MaxRateMultiplier ? 1f : value;
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
