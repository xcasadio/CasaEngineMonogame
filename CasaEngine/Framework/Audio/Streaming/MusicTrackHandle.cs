namespace CasaEngine.Framework.Audio.Streaming;

/// <summary>
/// Identifies a track owned by the <see cref="MusicPlayer"/>. Like a voice handle, it carries a
/// generation so a handle kept across a Stop cannot act on the track that replaced it.
/// </summary>
public readonly struct MusicTrackHandle : IEquatable<MusicTrackHandle>
{
    public static readonly MusicTrackHandle None = default;

    private readonly int _indexPlusOne;

    public MusicTrackHandle(int index, int generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);

        _indexPlusOne = index + 1;
        Generation = generation;
    }

    public int Index => _indexPlusOne - 1;

    public int Generation { get; }

    public bool IsValid => _indexPlusOne > 0;

    public bool Equals(MusicTrackHandle other)
        => _indexPlusOne == other._indexPlusOne && Generation == other.Generation;

    public override bool Equals(object obj) => obj is MusicTrackHandle other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_indexPlusOne, Generation);

    public static bool operator ==(MusicTrackHandle left, MusicTrackHandle right) => left.Equals(right);

    public static bool operator !=(MusicTrackHandle left, MusicTrackHandle right) => !left.Equals(right);

    public override string ToString() => IsValid ? $"Track({Index}:{Generation})" : "Track(none)";
}
