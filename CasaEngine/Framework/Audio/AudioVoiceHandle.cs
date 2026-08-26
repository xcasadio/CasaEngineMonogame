namespace CasaEngine.Framework.Audio;

/// <summary>
/// Identifies a voice owned by an <see cref="IAudioBackend"/>.
/// The generation makes a stale handle detectable once the voice slot has been recycled,
/// so a caller that kept a handle across a Stop cannot act on somebody else's voice.
/// </summary>
public readonly struct AudioVoiceHandle : IEquatable<AudioVoiceHandle>
{
    /// <summary>The invalid handle, returned when a voice could not be started.</summary>
    public static readonly AudioVoiceHandle None = default;

    // Stored offset by one so that default(AudioVoiceHandle) is invalid.
    private readonly int _indexPlusOne;

    public AudioVoiceHandle(int index, int generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);

        _indexPlusOne = index + 1;
        Generation = generation;
    }

    /// <summary>Slot index in the backend voice table. Undefined when <see cref="IsValid"/> is false.</summary>
    public int Index => _indexPlusOne - 1;

    /// <summary>Increments every time the slot is reused.</summary>
    public int Generation { get; }

    public bool IsValid => _indexPlusOne > 0;

    public bool Equals(AudioVoiceHandle other)
    {
        return _indexPlusOne == other._indexPlusOne && Generation == other.Generation;
    }

    public override bool Equals(object obj)
    {
        return obj is AudioVoiceHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_indexPlusOne, Generation);
    }

    public static bool operator ==(AudioVoiceHandle left, AudioVoiceHandle right) => left.Equals(right);

    public static bool operator !=(AudioVoiceHandle left, AudioVoiceHandle right) => !left.Equals(right);

    public override string ToString()
    {
        return IsValid ? $"Voice({Index}:{Generation})" : "Voice(none)";
    }
}
