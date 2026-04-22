namespace CasaEngine.Framework.Animations;

public sealed class AnimationClip
{
    private readonly JointAnimationTrack?[] _tracksByJointIndex;

    public AnimationClip(
        string name,
        SkeletonDefinition skeleton,
        IReadOnlyList<JointAnimationTrack> jointTracks,
        float durationSeconds = 0f,
        AnimationEventTrack? eventTrack = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Animation clips need a name.", nameof(name));
        }

        Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
        ArgumentNullException.ThrowIfNull(jointTracks);

        _tracksByJointIndex = new JointAnimationTrack?[skeleton.Count];
        Name = name;

        var computedDuration = 0f;
        for (var index = 0; index < jointTracks.Count; index++)
        {
            var track = jointTracks[index] ?? throw new ArgumentException("Joint tracks cannot contain null entries.", nameof(jointTracks));

            if (track.JointIndex >= skeleton.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(jointTracks), $"Joint track index {track.JointIndex} is outside the skeleton definition.");
            }

            if (_tracksByJointIndex[track.JointIndex] != null)
            {
                throw new ArgumentException($"Joint track for joint index {track.JointIndex} is duplicated.", nameof(jointTracks));
            }

            _tracksByJointIndex[track.JointIndex] = track;
            computedDuration = Math.Max(computedDuration, track.EndTimeSeconds);
        }

        if (durationSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        if (durationSeconds > 0f && durationSeconds < computedDuration)
        {
            throw new ArgumentException("Animation clip duration cannot be shorter than the last keyframe time.", nameof(durationSeconds));
        }

        DurationSeconds = durationSeconds > 0f ? durationSeconds : computedDuration;
        EventTrack = eventTrack;
    }

    public string Name { get; }

    public SkeletonDefinition Skeleton { get; }

    public float DurationSeconds { get; }

    public AnimationEventTrack? EventTrack { get; }

    public bool TryGetJointTrack(int jointIndex, out JointAnimationTrack? track)
    {
        if ((uint)jointIndex >= (uint)_tracksByJointIndex.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(jointIndex));
        }

        track = _tracksByJointIndex[jointIndex];
        return track != null;
    }
}