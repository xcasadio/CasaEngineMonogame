namespace CasaEngine.Framework.Animations;

public sealed class MorphChannelSampler
{
    public void Sample(MorphChannel channel, float timeSeconds, float clipDurationSeconds, bool loop, float[] destinationWeights)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(destinationWeights);

        var keyframes = channel.Keyframes;
        if (keyframes.Count == 0)
        {
            return;
        }

        var samplingDurationSeconds = clipDurationSeconds > 0f
            ? clipDurationSeconds
            : channel.EndTimeSeconds;
        var evaluationTime = NormalizeClipTime(timeSeconds, samplingDurationSeconds, loop);

        if (keyframes.Count == 1 || samplingDurationSeconds <= 0f)
        {
            WriteKeyframe(keyframes[0], destinationWeights);
            return;
        }

        var firstKeyframe = keyframes[0];
        var lastKeyframe = keyframes[keyframes.Count - 1];

        if (!loop)
        {
            if (evaluationTime <= firstKeyframe.TimeSeconds)
            {
                WriteKeyframe(firstKeyframe, destinationWeights);
                return;
            }

            if (evaluationTime >= lastKeyframe.TimeSeconds)
            {
                WriteKeyframe(lastKeyframe, destinationWeights);
                return;
            }
        }
        else if (evaluationTime < firstKeyframe.TimeSeconds || evaluationTime > lastKeyframe.TimeSeconds)
        {
            var wrappedTime = evaluationTime < firstKeyframe.TimeSeconds
                ? evaluationTime + samplingDurationSeconds
                : evaluationTime;
            WriteBlendedKeyframes(
                lastKeyframe,
                new MorphKeyframe(firstKeyframe.TimeSeconds + samplingDurationSeconds, firstKeyframe.AttachmentIndices, firstKeyframe.Weights),
                wrappedTime,
                destinationWeights);
            return;
        }

        for (var keyframeIndex = 1; keyframeIndex < keyframes.Count; keyframeIndex++)
        {
            var upperKeyframe = keyframes[keyframeIndex];
            if (evaluationTime <= upperKeyframe.TimeSeconds)
            {
                WriteBlendedKeyframes(keyframes[keyframeIndex - 1], upperKeyframe, evaluationTime, destinationWeights);
                return;
            }
        }

        WriteKeyframe(lastKeyframe, destinationWeights);
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

    private static void WriteKeyframe(MorphKeyframe keyframe, float[] destinationWeights)
    {
        ResetKeyedAttachments(keyframe, destinationWeights);
        AccumulateKeyframe(keyframe, 1f, destinationWeights);
    }

    private static void WriteBlendedKeyframes(MorphKeyframe lowerKeyframe, MorphKeyframe upperKeyframe, float timeSeconds, float[] destinationWeights)
    {
        var blendFactor = GetBlendFactor(lowerKeyframe.TimeSeconds, upperKeyframe.TimeSeconds, timeSeconds);
        ResetKeyedAttachments(lowerKeyframe, destinationWeights);
        ResetKeyedAttachments(upperKeyframe, destinationWeights);
        AccumulateKeyframe(lowerKeyframe, 1f - blendFactor, destinationWeights);
        AccumulateKeyframe(upperKeyframe, blendFactor, destinationWeights);
    }

    private static void ResetKeyedAttachments(MorphKeyframe keyframe, float[] destinationWeights)
    {
        for (var attachmentIndex = 0; attachmentIndex < keyframe.AttachmentIndices.Count; attachmentIndex++)
        {
            var destinationIndex = keyframe.AttachmentIndices[attachmentIndex];
            ValidateDestinationIndex(destinationIndex, destinationWeights.Length);
            destinationWeights[destinationIndex] = 0f;
        }
    }

    private static void AccumulateKeyframe(MorphKeyframe keyframe, float factor, float[] destinationWeights)
    {
        if (Math.Abs(factor) <= float.Epsilon)
        {
            return;
        }

        for (var attachmentIndex = 0; attachmentIndex < keyframe.AttachmentIndices.Count; attachmentIndex++)
        {
            var destinationIndex = keyframe.AttachmentIndices[attachmentIndex];
            ValidateDestinationIndex(destinationIndex, destinationWeights.Length);
            destinationWeights[destinationIndex] += keyframe.Weights[attachmentIndex] * factor;
        }
    }

    private static float GetBlendFactor(float startTimeSeconds, float endTimeSeconds, float timeSeconds)
    {
        var durationSeconds = endTimeSeconds - startTimeSeconds;
        if (durationSeconds <= 0f)
        {
            return 1f;
        }

        return Math.Clamp((timeSeconds - startTimeSeconds) / durationSeconds, 0f, 1f);
    }

    private static void ValidateDestinationIndex(int destinationIndex, int destinationLength)
    {
        if ((uint)destinationIndex >= (uint)destinationLength)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationIndex), $"Morph attachment index {destinationIndex} exceeds the destination weight buffer length {destinationLength}.");
        }
    }
}