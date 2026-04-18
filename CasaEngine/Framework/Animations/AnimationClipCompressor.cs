using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public static class AnimationClipCompressor
{
    public static AnimationClip Compress(AnimationClip clip, AnimationClipCompressionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(clip);

        settings ??= AnimationClipCompressionSettings.Default;
        settings.Validate();

        var compressedTracks = new List<JointAnimationTrack>(clip.Skeleton.Count);

        for (var jointIndex = 0; jointIndex < clip.Skeleton.Count; jointIndex++)
        {
            if (!clip.TryGetJointTrack(jointIndex, out var jointTrack) || jointTrack == null)
            {
                continue;
            }

            var bindTransform = clip.Skeleton.GetBindLocalTransform(jointIndex);
            var translationTrack = CompressVector3Track(jointTrack.TranslationTrack, bindTransform.Translation, settings.TranslationTolerance);
            var rotationTrack = CompressQuaternionTrack(jointTrack.RotationTrack, bindTransform.Rotation, settings.RotationToleranceRadians);
            var scaleTrack = CompressVector3Track(jointTrack.ScaleTrack, bindTransform.Scale, settings.ScaleTolerance);

            if (translationTrack == null && rotationTrack == null && scaleTrack == null)
            {
                continue;
            }

            compressedTracks.Add(new JointAnimationTrack(jointIndex, translationTrack, rotationTrack, scaleTrack));
        }

        return new AnimationClip(clip.Name, clip.Skeleton, compressedTracks, clip.DurationSeconds, clip.EventTrack);
    }

    private static Vector3AnimationTrack? CompressVector3Track(Vector3AnimationTrack? track, Vector3 bindValue, float tolerance)
    {
        if (track == null || track.KeyframeCount == 0)
        {
            return null;
        }

        var reducedKeyframes = ReduceVector3Keyframes(track.Keyframes, tolerance);
        if (AllVector3KeyframesMatch(reducedKeyframes, reducedKeyframes[0].Value, tolerance))
        {
            reducedKeyframes = new[] { reducedKeyframes[0] };
        }

        var toleranceSquared = tolerance * tolerance;
        if (reducedKeyframes.Length == 1 && Vector3.DistanceSquared(reducedKeyframes[0].Value, bindValue) <= toleranceSquared)
        {
            return null;
        }

        return new Vector3AnimationTrack(reducedKeyframes);
    }

    private static QuaternionAnimationTrack? CompressQuaternionTrack(QuaternionAnimationTrack? track, Quaternion bindValue, float toleranceRadians)
    {
        if (track == null || track.KeyframeCount == 0)
        {
            return null;
        }

        var reducedKeyframes = ReduceQuaternionKeyframes(track.Keyframes, toleranceRadians);
        if (AllQuaternionKeyframesMatch(reducedKeyframes, reducedKeyframes[0].Value, toleranceRadians))
        {
            reducedKeyframes = new[] { reducedKeyframes[0] };
        }

        if (reducedKeyframes.Length == 1
            && GetQuaternionErrorRadians(reducedKeyframes[0].Value, bindValue) <= toleranceRadians)
        {
            return null;
        }

        return new QuaternionAnimationTrack(reducedKeyframes);
    }

    private static AnimationKeyframe<Vector3>[] ReduceVector3Keyframes(IReadOnlyList<AnimationKeyframe<Vector3>> source, float tolerance)
    {
        if (source.Count <= 1)
        {
            return source.Count == 0 ? Array.Empty<AnimationKeyframe<Vector3>>() : new[] { source[0] };
        }

        if (AllVector3KeyframesMatch(source, source[0].Value, tolerance))
        {
            return new[] { source[0] };
        }

        var toleranceSquared = tolerance * tolerance;
        var reducedKeyframes = new List<AnimationKeyframe<Vector3>>(source.Count)
        {
            source[0],
        };

        var segmentStartIndex = 0;
        var candidateIndex = 2;

        while (candidateIndex < source.Count)
        {
            if (CanReduceVector3Segment(source, segmentStartIndex, candidateIndex, toleranceSquared))
            {
                candidateIndex++;
                continue;
            }

            var preservedIndex = candidateIndex - 1;
            reducedKeyframes.Add(source[preservedIndex]);
            segmentStartIndex = preservedIndex;
            candidateIndex = preservedIndex + 2;
        }

        reducedKeyframes.Add(source[source.Count - 1]);
        return reducedKeyframes.ToArray();
    }

    private static AnimationKeyframe<Quaternion>[] ReduceQuaternionKeyframes(IReadOnlyList<AnimationKeyframe<Quaternion>> source, float toleranceRadians)
    {
        if (source.Count <= 1)
        {
            return source.Count == 0 ? Array.Empty<AnimationKeyframe<Quaternion>>() : new[] { source[0] };
        }

        if (AllQuaternionKeyframesMatch(source, source[0].Value, toleranceRadians))
        {
            return new[] { source[0] };
        }

        var reducedKeyframes = new List<AnimationKeyframe<Quaternion>>(source.Count)
        {
            source[0],
        };

        var segmentStartIndex = 0;
        var candidateIndex = 2;

        while (candidateIndex < source.Count)
        {
            if (CanReduceQuaternionSegment(source, segmentStartIndex, candidateIndex, toleranceRadians))
            {
                candidateIndex++;
                continue;
            }

            var preservedIndex = candidateIndex - 1;
            reducedKeyframes.Add(source[preservedIndex]);
            segmentStartIndex = preservedIndex;
            candidateIndex = preservedIndex + 2;
        }

        reducedKeyframes.Add(source[source.Count - 1]);
        return reducedKeyframes.ToArray();
    }

    private static bool AllVector3KeyframesMatch(IReadOnlyList<AnimationKeyframe<Vector3>> keyframes, Vector3 referenceValue, float tolerance)
    {
        var toleranceSquared = tolerance * tolerance;
        for (var index = 0; index < keyframes.Count; index++)
        {
            if (Vector3.DistanceSquared(keyframes[index].Value, referenceValue) > toleranceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllQuaternionKeyframesMatch(IReadOnlyList<AnimationKeyframe<Quaternion>> keyframes, Quaternion referenceValue, float toleranceRadians)
    {
        for (var index = 0; index < keyframes.Count; index++)
        {
            if (GetQuaternionErrorRadians(keyframes[index].Value, referenceValue) > toleranceRadians)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanReduceVector3Segment(IReadOnlyList<AnimationKeyframe<Vector3>> keyframes, int startIndex, int endIndex, float toleranceSquared)
    {
        var startKey = keyframes[startIndex];
        var endKey = keyframes[endIndex];

        for (var index = startIndex + 1; index < endIndex; index++)
        {
            var originalKey = keyframes[index];
            var interpolatedValue = InterpolateVector3(startKey, endKey, originalKey.TimeSeconds);
            if (Vector3.DistanceSquared(interpolatedValue, originalKey.Value) > toleranceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanReduceQuaternionSegment(IReadOnlyList<AnimationKeyframe<Quaternion>> keyframes, int startIndex, int endIndex, float toleranceRadians)
    {
        var startKey = keyframes[startIndex];
        var endKey = keyframes[endIndex];

        for (var index = startIndex + 1; index < endIndex; index++)
        {
            var originalKey = keyframes[index];
            var interpolatedValue = InterpolateQuaternion(startKey, endKey, originalKey.TimeSeconds);
            if (GetQuaternionErrorRadians(interpolatedValue, originalKey.Value) > toleranceRadians)
            {
                return false;
            }
        }

        return true;
    }

    private static Vector3 InterpolateVector3(AnimationKeyframe<Vector3> start, AnimationKeyframe<Vector3> end, float timeSeconds)
    {
        var blendFactor = GetBlendFactor(start.TimeSeconds, end.TimeSeconds, timeSeconds);
        return Vector3.Lerp(start.Value, end.Value, blendFactor);
    }

    private static Quaternion InterpolateQuaternion(AnimationKeyframe<Quaternion> start, AnimationKeyframe<Quaternion> end, float timeSeconds)
    {
        var startRotation = NormalizeQuaternion(start.Value);
        var endRotation = NormalizeQuaternion(end.Value);
        var blendFactor = GetBlendFactor(start.TimeSeconds, end.TimeSeconds, timeSeconds);
        return Quaternion.Slerp(startRotation, endRotation, blendFactor);
    }

    private static Quaternion NormalizeQuaternion(Quaternion rotation)
    {
        return rotation.LengthSquared() <= float.Epsilon
            ? Quaternion.Identity
            : Quaternion.Normalize(rotation);
    }

    private static float GetQuaternionErrorRadians(Quaternion first, Quaternion second)
    {
        var normalizedFirst = NormalizeQuaternion(first);
        var normalizedSecond = NormalizeQuaternion(second);
        var dot = Math.Abs(Quaternion.Dot(normalizedFirst, normalizedSecond));
        dot = Math.Clamp(dot, -1f, 1f);
        return 2f * MathF.Acos(dot);
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