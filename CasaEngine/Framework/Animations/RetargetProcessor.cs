using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public static class RetargetProcessor
{
    public static AnimationClip RetargetClip(AnimationClip sourceClip, RetargetProfile retargetProfile, string? retargetedName = null)
    {
        ArgumentNullException.ThrowIfNull(sourceClip);
        ArgumentNullException.ThrowIfNull(retargetProfile);

        if (!ReferenceEquals(sourceClip.Skeleton, retargetProfile.SourceSkeleton))
        {
            throw new ArgumentException("The source clip skeleton must match the retarget profile source skeleton.", nameof(sourceClip));
        }

        var jointTracks = new List<JointAnimationTrack>(retargetProfile.JointMappings.Count);
        for (var mappingIndex = 0; mappingIndex < retargetProfile.JointMappings.Count; mappingIndex++)
        {
            var jointMapping = retargetProfile.JointMappings[mappingIndex];
            if (!sourceClip.TryGetJointTrack(jointMapping.SourceJointIndex, out var sourceJointTrack) || sourceJointTrack == null)
            {
                continue;
            }

            var translationTrack = RetargetTranslationTrack(sourceJointTrack.TranslationTrack, jointMapping, retargetProfile);
            var rotationTrack = RetargetRotationTrack(sourceJointTrack.RotationTrack, jointMapping, retargetProfile);
            var scaleTrack = RetargetScaleTrack(sourceJointTrack.ScaleTrack, jointMapping, retargetProfile);

            if (translationTrack == null && rotationTrack == null && scaleTrack == null)
            {
                continue;
            }

            jointTracks.Add(new JointAnimationTrack(jointMapping.TargetJointIndex, translationTrack, rotationTrack, scaleTrack));
        }

        var clipName = string.IsNullOrWhiteSpace(retargetedName)
            ? sourceClip.Name + "_Retargeted"
            : retargetedName;
        return new AnimationClip(
            clipName,
            retargetProfile.TargetSkeleton,
            jointTracks,
            sourceClip.DurationSeconds,
            CopyEventTrack(sourceClip.EventTrack));
    }

    private static Vector3AnimationTrack? RetargetTranslationTrack(
        Vector3AnimationTrack? sourceTrack,
        RetargetJointMapping jointMapping,
        RetargetProfile retargetProfile)
    {
        if (sourceTrack == null || sourceTrack.KeyframeCount == 0)
        {
            return null;
        }

        var sourceBindTransform = retargetProfile.SourceSkeleton.GetBindLocalTransform(jointMapping.SourceJointIndex);
        var targetBindTransform = retargetProfile.TargetSkeleton.GetBindLocalTransform(jointMapping.TargetJointIndex);
        var translationScale = jointMapping.TranslationScale;
        if (jointMapping.SourceJointIndex == retargetProfile.SourceSkeleton.RootIndex)
        {
            translationScale *= retargetProfile.RootTranslationScale;
        }

        var keyframes = new AnimationKeyframe<Vector3>[sourceTrack.KeyframeCount];
        for (var keyframeIndex = 0; keyframeIndex < sourceTrack.KeyframeCount; keyframeIndex++)
        {
            var keyframe = sourceTrack.GetKeyframe(keyframeIndex);
            var sourceDelta = keyframe.Value - sourceBindTransform.Translation;
            var convertedDelta = ConvertVectorBetweenBases(sourceDelta * translationScale, retargetProfile);
            keyframes[keyframeIndex] = new AnimationKeyframe<Vector3>(
                keyframe.TimeSeconds,
                targetBindTransform.Translation + convertedDelta);
        }

        return new Vector3AnimationTrack(keyframes);
    }

    private static QuaternionAnimationTrack? RetargetRotationTrack(
        QuaternionAnimationTrack? sourceTrack,
        RetargetJointMapping jointMapping,
        RetargetProfile retargetProfile)
    {
        if (sourceTrack == null || sourceTrack.KeyframeCount == 0)
        {
            return null;
        }

        var sourceBindRotation = NormalizeRotation(retargetProfile.SourceSkeleton.GetBindLocalTransform(jointMapping.SourceJointIndex).Rotation);
        var targetBindRotation = NormalizeRotation(retargetProfile.TargetSkeleton.GetBindLocalTransform(jointMapping.TargetJointIndex).Rotation);
        var axisConversion = CreateBasisConversion(retargetProfile);
        var inverseAxisConversion = Quaternion.Inverse(axisConversion);
        var convertRootRotation = jointMapping.SourceJointIndex == retargetProfile.SourceSkeleton.RootIndex;

        var keyframes = new AnimationKeyframe<Quaternion>[sourceTrack.KeyframeCount];
        for (var keyframeIndex = 0; keyframeIndex < sourceTrack.KeyframeCount; keyframeIndex++)
        {
            var keyframe = sourceTrack.GetKeyframe(keyframeIndex);
            var sourceRotation = NormalizeRotation(keyframe.Value);
            var sourceDelta = Quaternion.Normalize(Quaternion.Inverse(sourceBindRotation) * sourceRotation);
            if (convertRootRotation)
            {
                sourceDelta = Quaternion.Normalize(axisConversion * sourceDelta * inverseAxisConversion);
            }

            keyframes[keyframeIndex] = new AnimationKeyframe<Quaternion>(
                keyframe.TimeSeconds,
                Quaternion.Normalize(targetBindRotation * sourceDelta));
        }

        return new QuaternionAnimationTrack(keyframes);
    }

    private static Vector3AnimationTrack? RetargetScaleTrack(
        Vector3AnimationTrack? sourceTrack,
        RetargetJointMapping jointMapping,
        RetargetProfile retargetProfile)
    {
        if (sourceTrack == null || sourceTrack.KeyframeCount == 0)
        {
            return null;
        }

        var sourceBindScale = retargetProfile.SourceSkeleton.GetBindLocalTransform(jointMapping.SourceJointIndex).Scale;
        var targetBindScale = retargetProfile.TargetSkeleton.GetBindLocalTransform(jointMapping.TargetJointIndex).Scale;
        var keyframes = new AnimationKeyframe<Vector3>[sourceTrack.KeyframeCount];

        for (var keyframeIndex = 0; keyframeIndex < sourceTrack.KeyframeCount; keyframeIndex++)
        {
            var keyframe = sourceTrack.GetKeyframe(keyframeIndex);
            keyframes[keyframeIndex] = new AnimationKeyframe<Vector3>(
                keyframe.TimeSeconds,
                targetBindScale + (keyframe.Value - sourceBindScale));
        }

        return new Vector3AnimationTrack(keyframes);
    }

    private static AnimationEventTrack? CopyEventTrack(AnimationEventTrack? eventTrack)
    {
        if (eventTrack == null || eventTrack.Count == 0)
        {
            return null;
        }

        var keyframes = new AnimationEventKeyframe[eventTrack.Count];
        for (var eventIndex = 0; eventIndex < eventTrack.Count; eventIndex++)
        {
            keyframes[eventIndex] = eventTrack.GetKeyframe(eventIndex);
        }

        return new AnimationEventTrack(keyframes);
    }

    private static Vector3 ConvertVectorBetweenBases(Vector3 value, RetargetProfile retargetProfile)
    {
        var sourceForward = retargetProfile.SourceForwardVector;
        var sourceUp = retargetProfile.SourceUpVector;
        var sourceRight = Vector3.Normalize(Vector3.Cross(sourceUp, sourceForward));
        var targetForward = retargetProfile.TargetForwardVector;
        var targetUp = retargetProfile.TargetUpVector;
        var targetRight = Vector3.Normalize(Vector3.Cross(targetUp, targetForward));

        var worldVector = sourceRight * value.X + sourceUp * value.Y + sourceForward * value.Z;
        return new Vector3(
            Vector3.Dot(worldVector, targetRight),
            Vector3.Dot(worldVector, targetUp),
            Vector3.Dot(worldVector, targetForward));
    }

    private static Quaternion CreateBasisConversion(RetargetProfile retargetProfile)
    {
        var sourceBasis = CreateBasisOrientation(retargetProfile.SourceForwardVector, retargetProfile.SourceUpVector);
        var targetBasis = CreateBasisOrientation(retargetProfile.TargetForwardVector, retargetProfile.TargetUpVector);
        return Quaternion.Normalize(Quaternion.Inverse(targetBasis) * sourceBasis);
    }

    private static Quaternion CreateBasisOrientation(Vector3 forward, Vector3 up)
    {
        var normalizedForward = Vector3.Normalize(forward);
        var normalizedUp = Vector3.Normalize(up);
        var right = Vector3.Normalize(Vector3.Cross(normalizedUp, normalizedForward));
        var correctedUp = Vector3.Normalize(Vector3.Cross(normalizedForward, right));
        var rotationMatrix = new Matrix(
            right.X, right.Y, right.Z, 0f,
            correctedUp.X, correctedUp.Y, correctedUp.Z, 0f,
            normalizedForward.X, normalizedForward.Y, normalizedForward.Z, 0f,
            0f, 0f, 0f, 1f);
        return Quaternion.CreateFromRotationMatrix(rotationMatrix);
    }

    private static Quaternion NormalizeRotation(Quaternion rotation)
    {
        return rotation.LengthSquared() <= float.Epsilon
            ? Quaternion.Identity
            : Quaternion.Normalize(rotation);
    }
}