namespace CasaEngine.Framework.Assets.Animations;

public sealed class Animation2dCompositionData
{
    public AnimationType AnimationType { get; }

    public float DurationSeconds { get; }

    public IReadOnlyList<Animation2dPartData> Parts { get; }

    public IReadOnlyList<Animation2dTrackData> Tracks { get; }

    public IReadOnlyList<AnimationEventAsset> Events { get; }

    internal Animation2dCompositionData(
        AnimationType animationType,
        float durationSeconds,
        IReadOnlyList<Animation2dPartData> parts,
        IReadOnlyList<Animation2dTrackData> tracks,
        IReadOnlyList<AnimationEventAsset> events)
    {
        AnimationType = animationType;
        DurationSeconds = durationSeconds;
        Parts = parts;
        Tracks = tracks;
        Events = events;
    }
}