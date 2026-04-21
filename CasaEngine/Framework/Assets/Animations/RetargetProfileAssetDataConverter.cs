using CasaEngine.Framework.Animations;

namespace CasaEngine.Framework.Assets.Animations;

public static class RetargetProfileAssetDataConverter
{
    public static RetargetProfile CreateRetargetProfile(
        RetargetProfileAsset retargetProfileAsset,
        SkeletonDefinition sourceSkeleton,
        SkeletonDefinition targetSkeleton)
    {
        ArgumentNullException.ThrowIfNull(retargetProfileAsset);
        ArgumentNullException.ThrowIfNull(sourceSkeleton);
        ArgumentNullException.ThrowIfNull(targetSkeleton);

        var jointMappings = new RetargetJointMapping[retargetProfileAsset.JointMappings.Count];
        for (var mappingIndex = 0; mappingIndex < retargetProfileAsset.JointMappings.Count; mappingIndex++)
        {
            var mappingAsset = retargetProfileAsset.JointMappings[mappingIndex];
            if (!sourceSkeleton.TryGetJointIndex(mappingAsset.SourceJointName, out var sourceJointIndex))
            {
                throw new InvalidOperationException($"Retarget profile '{retargetProfileAsset.Name}' references unknown source joint '{mappingAsset.SourceJointName}'.");
            }

            if (!targetSkeleton.TryGetJointIndex(mappingAsset.TargetJointName, out var targetJointIndex))
            {
                throw new InvalidOperationException($"Retarget profile '{retargetProfileAsset.Name}' references unknown target joint '{mappingAsset.TargetJointName}'.");
            }

            jointMappings[mappingIndex] = new RetargetJointMapping(
                sourceSkeleton,
                targetSkeleton,
                mappingAsset.SourceJointName,
                sourceJointIndex,
                mappingAsset.TargetJointName,
                targetJointIndex,
                mappingAsset.TranslationScale);
        }

        return new RetargetProfile(
            sourceSkeleton,
            targetSkeleton,
            jointMappings,
            retargetProfileAsset.ReferencePoseMode,
            retargetProfileAsset.SourceForwardAxis,
            retargetProfileAsset.SourceUpAxis,
            retargetProfileAsset.TargetForwardAxis,
            retargetProfileAsset.TargetUpAxis,
            retargetProfileAsset.RootTranslationScale);
    }
}