namespace CasaEngine.Framework.Assets.Animations;

public sealed class Animation2dCompositionData
{
    public float DurationSeconds { get; }

    public AnimationType AnimationType { get; }

    public bool IsLooping => AnimationType == AnimationType.Loop;

    public IReadOnlyList<Animation2dPartData> Parts { get; }

    public IReadOnlyList<Animation2dTrackData> Tracks { get; }

    public IReadOnlyList<AnimationEventAsset> Events { get; }

    /// <summary>Collision fixture sets of the timeline, sorted by time and sampled with step semantics.</summary>
    public IReadOnlyList<Animation2dCollisionKeyframeData> CollisionKeyframes { get; }

    internal Animation2dCompositionData(
        float durationSeconds,
        AnimationType animationType,
        IReadOnlyList<Animation2dPartData> parts,
        IReadOnlyList<Animation2dTrackData> tracks,
        IReadOnlyList<AnimationEventAsset> events,
        IReadOnlyList<Animation2dCollisionKeyframeData> collisionKeyframes)
    {
        DurationSeconds = durationSeconds;
        AnimationType = animationType;
        Parts = parts;
        Tracks = tracks;
        Events = events;
        CollisionKeyframes = collisionKeyframes;
    }
}