using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public sealed class AnimationClipSampler
{
    public void Sample(AnimationClip clip, float timeSeconds, SkeletonPoseLocal destination, bool loop)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(destination);

        if (!ReferenceEquals(clip.Skeleton, destination.Skeleton))
        {
            throw new ArgumentException("The destination pose targets a different skeleton than the clip.", nameof(destination));
        }

        var skeleton = destination.Skeleton;
        var evaluationTime = NormalizeClipTime(timeSeconds, clip.DurationSeconds, loop);

        for (var jointIndex = 0; jointIndex < skeleton.Count; jointIndex++)
        {
            var bindTransform = skeleton.GetBindLocalTransform(jointIndex);

            if (!clip.TryGetJointTrack(jointIndex, out var jointTrack) || jointTrack == null)
            {
                destination.SetTransformDirect(jointIndex, bindTransform);
                continue;
            }

            var sampledTransform = new BoneTransform(
                SampleVector3Track(jointTrack.TranslationTrack, evaluationTime, clip.DurationSeconds, loop, bindTransform.Translation),
                SampleQuaternionTrack(jointTrack.RotationTrack, evaluationTime, clip.DurationSeconds, loop, bindTransform.Rotation),
                SampleVector3Track(jointTrack.ScaleTrack, evaluationTime, clip.DurationSeconds, loop, bindTransform.Scale));

            destination.SetTransformDirect(jointIndex, sampledTransform);
        }

        destination.MarkDirtyFrom(0);
    }

    private static float NormalizeClipTime(float timeSeconds, float durationSeconds, bool loop)
    {
        if (durationSeconds <= 0f)
        {
            return 0f;
        }

        if (!loop)
        {
            return Math.Clamp(timeSeconds, 0f, durationSeconds);
        }

        var wrappedTime = timeSeconds % durationSeconds;
        if (wrappedTime < 0f)
        {
            wrappedTime += durationSeconds;
        }

        return wrappedTime;
    }

    private static Vector3 SampleVector3Track(Vector3AnimationTrack track, float timeSeconds, float clipDurationSeconds, bool loop, Vector3 fallbackValue)
    {
        if (track == null || track.KeyframeCount == 0)
        {
            return fallbackValue;
        }

        if (track.KeyframeCount == 1 || clipDurationSeconds <= 0f)
        {
            return track.GetKeyframe(0).Value;
        }

        var firstKey = track.GetKeyframe(0);
        var lastKey = track.GetKeyframe(track.KeyframeCount - 1);

        if (!loop)
        {
            if (timeSeconds <= firstKey.TimeSeconds)
            {
                return firstKey.Value;
            }

            if (timeSeconds >= lastKey.TimeSeconds)
            {
                return lastKey.Value;
            }
        }
        else if (timeSeconds < firstKey.TimeSeconds || timeSeconds > lastKey.TimeSeconds)
        {
            var wrappedTime = timeSeconds < firstKey.TimeSeconds ? timeSeconds + clipDurationSeconds : timeSeconds;
            return InterpolateVector3(lastKey, new AnimationKeyframe<Vector3>(firstKey.TimeSeconds + clipDurationSeconds, firstKey.Value), wrappedTime);
        }

        for (var keyframeIndex = 1; keyframeIndex < track.KeyframeCount; keyframeIndex++)
        {
            var upperKey = track.GetKeyframe(keyframeIndex);
            if (timeSeconds <= upperKey.TimeSeconds)
            {
                var lowerKey = track.GetKeyframe(keyframeIndex - 1);
                return InterpolateVector3(lowerKey, upperKey, timeSeconds);
            }
        }

        return lastKey.Value;
    }

    private static Quaternion SampleQuaternionTrack(QuaternionAnimationTrack track, float timeSeconds, float clipDurationSeconds, bool loop, Quaternion fallbackValue)
    {
        if (track == null || track.KeyframeCount == 0)
        {
            return fallbackValue;
        }

        if (track.KeyframeCount == 1 || clipDurationSeconds <= 0f)
        {
            return track.GetKeyframe(0).Value;
        }

        var firstKey = track.GetKeyframe(0);
        var lastKey = track.GetKeyframe(track.KeyframeCount - 1);

        if (!loop)
        {
            if (timeSeconds <= firstKey.TimeSeconds)
            {
                return firstKey.Value;
            }

            if (timeSeconds >= lastKey.TimeSeconds)
            {
                return lastKey.Value;
            }
        }
        else if (timeSeconds < firstKey.TimeSeconds || timeSeconds > lastKey.TimeSeconds)
        {
            var wrappedTime = timeSeconds < firstKey.TimeSeconds ? timeSeconds + clipDurationSeconds : timeSeconds;
            return InterpolateQuaternion(lastKey, new AnimationKeyframe<Quaternion>(firstKey.TimeSeconds + clipDurationSeconds, firstKey.Value), wrappedTime);
        }

        for (var keyframeIndex = 1; keyframeIndex < track.KeyframeCount; keyframeIndex++)
        {
            var upperKey = track.GetKeyframe(keyframeIndex);
            if (timeSeconds <= upperKey.TimeSeconds)
            {
                var lowerKey = track.GetKeyframe(keyframeIndex - 1);
                return InterpolateQuaternion(lowerKey, upperKey, timeSeconds);
            }
        }

        return lastKey.Value;
    }

    private static Vector3 InterpolateVector3(AnimationKeyframe<Vector3> start, AnimationKeyframe<Vector3> end, float timeSeconds)
    {
        var blendFactor = GetBlendFactor(start.TimeSeconds, end.TimeSeconds, timeSeconds);
        return Vector3.Lerp(start.Value, end.Value, blendFactor);
    }

    private static Quaternion InterpolateQuaternion(AnimationKeyframe<Quaternion> start, AnimationKeyframe<Quaternion> end, float timeSeconds)
    {
        var blendFactor = GetBlendFactor(start.TimeSeconds, end.TimeSeconds, timeSeconds);
        return Quaternion.Slerp(start.Value, end.Value, blendFactor);
    }

    private static float GetBlendFactor(float startTimeSeconds, float endTimeSeconds, float timeSeconds)
    {
        var duration = endTimeSeconds - startTimeSeconds;
        if (duration <= 0f)
        {
            return 1f;
        }

        return Math.Clamp((timeSeconds - startTimeSeconds) / duration, 0f, 1f);
    }
}