namespace CasaEngine.Framework.Animations;

public sealed class AnimationClip
{
    private readonly JointAnimationTrack[] _tracksByJointIndex;

    public AnimationClip(
        string name,
        SkeletonDefinition skeleton,
        IReadOnlyList<JointAnimationTrack> jointTracks,
        float durationSeconds = 0f,
        AnimationEventTrack eventTrack = null,
        float loopPeriodSeconds = 0f)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Animation clips need a name.", nameof(name));
        }

        Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
        ArgumentNullException.ThrowIfNull(jointTracks);

        _tracksByJointIndex = new JointAnimationTrack[skeleton.Count];
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

        if (loopPeriodSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(loopPeriodSeconds));
        }

        if (loopPeriodSeconds > 0f && loopPeriodSeconds < DurationSeconds)
        {
            throw new ArgumentException("The loop period cannot be shorter than the clip duration.", nameof(loopPeriodSeconds));
        }

        LoopPeriodSeconds = loopPeriodSeconds > 0f ? loopPeriodSeconds : DurationSeconds;
    }

    public string Name { get; }

    public SkeletonDefinition Skeleton { get; }

    /// <summary>Time of the last keyframe (or the explicit duration): the clip's playable range is [0, DurationSeconds].</summary>
    public float DurationSeconds { get; }

    /// <summary>
    /// Length of one cycle when the clip is played looped. Equals <see cref="DurationSeconds"/> by
    /// default: the last keyframe then coincides with the start of the next cycle, which is right
    /// for a clip whose first pose is duplicated at the end. A uniformly sampled clip whose last
    /// keyframe is a distinct frame (e.g. 18 frames at 30 Hz keyed 0..17/30) needs
    /// <c>DurationSeconds + 1/30</c>: the sampler then interpolates from the last keyframe back to
    /// the first over that extra interval instead of jumping a frame at the seam. See
    /// <see cref="WithLoopPeriod"/>.
    /// </summary>
    public float LoopPeriodSeconds { get; }

    public AnimationEventTrack EventTrack { get; }

    /// <summary>Returns a copy of this clip (same name, skeleton, tracks, duration and events) with another <see cref="LoopPeriodSeconds"/>.</summary>
    public AnimationClip WithLoopPeriod(float loopPeriodSeconds)
    {
        var tracks = new List<JointAnimationTrack>(_tracksByJointIndex.Length);
        for (var jointIndex = 0; jointIndex < _tracksByJointIndex.Length; jointIndex++)
        {
            if (_tracksByJointIndex[jointIndex] != null)
            {
                tracks.Add(_tracksByJointIndex[jointIndex]);
            }
        }

        return new AnimationClip(Name, Skeleton, tracks, DurationSeconds, EventTrack, loopPeriodSeconds);
    }

    public bool TryGetJointTrack(int jointIndex, out JointAnimationTrack track)
    {
        if ((uint)jointIndex >= (uint)_tracksByJointIndex.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(jointIndex));
        }

        track = _tracksByJointIndex[jointIndex];
        return track != null;
    }
}