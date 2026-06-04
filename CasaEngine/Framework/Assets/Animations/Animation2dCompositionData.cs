namespace CasaEngine.Framework.Assets.Animations;

public sealed class Animation2dCompositionData
{
    public float DurationSeconds { get; }

    public float RestartTimeSeconds { get; }

    public bool HasRestartEvent => RestartTimeSeconds > 0f;

    public IReadOnlyList<Animation2dPartData> Parts { get; }

    public IReadOnlyList<Animation2dTrackData> Tracks { get; }

    public IReadOnlyList<AnimationEventAsset> Events { get; }

    internal Animation2dCompositionData(
        float durationSeconds,
        float restartTimeSeconds,
        IReadOnlyList<Animation2dPartData> parts,
        IReadOnlyList<Animation2dTrackData> tracks,
        IReadOnlyList<AnimationEventAsset> events)
    {
        DurationSeconds = durationSeconds;
        RestartTimeSeconds = restartTimeSeconds;
        Parts = parts;
        Tracks = tracks;
        Events = events;
    }
}