using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class RetargetProfileTests
{
    [Fact]
    public void CreateRetargetProfile_ResolvesMappingsAndConventions()
    {
        var sourceSkeleton = CreateSkeleton("Root", "Spine");
        var targetSkeleton = CreateSkeleton("Pelvis", "Chest");
        var retargetProfileAsset = new RetargetProfileAsset
        {
            Name = "HeroToNpc",
            ReferencePoseMode = RetargetReferencePoseMode.BindPose,
            SourceForwardAxis = RetargetAxis.PositiveZ,
            SourceUpAxis = RetargetAxis.PositiveY,
            TargetForwardAxis = RetargetAxis.PositiveX,
            TargetUpAxis = RetargetAxis.PositiveY,
            RootTranslationScale = 0.01f,
        };
        retargetProfileAsset.JointMappings.Add(new RetargetJointMappingAsset
        {
            SourceJointName = "Root",
            TargetJointName = "Pelvis",
            TranslationScale = 0.01f,
        });
        retargetProfileAsset.JointMappings.Add(new RetargetJointMappingAsset
        {
            SourceJointName = "Spine",
            TargetJointName = "Chest",
            TranslationScale = 1f,
        });

        var retargetProfile = RetargetProfileAssetDataConverter.CreateRetargetProfile(retargetProfileAsset, sourceSkeleton, targetSkeleton);

        Assert.Equal(RetargetReferencePoseMode.BindPose, retargetProfile.ReferencePoseMode);
        Assert.Equal(Vector3.UnitZ, retargetProfile.SourceForwardVector);
        Assert.Equal(Vector3.UnitX, retargetProfile.TargetForwardVector);
        Assert.Equal(0.01f, retargetProfile.RootTranslationScale);
        Assert.True(retargetProfile.TryGetTargetJointIndex(0, out var pelvisIndex));
        Assert.Equal(0, pelvisIndex);
        Assert.True(retargetProfile.TryGetJointMapping(1, out var spineMapping));
        Assert.Equal("Chest", spineMapping.TargetJointName);
        Assert.Equal(1f, spineMapping.TranslationScale);
    }

    [Fact]
    public void RetargetProfile_RejectsColinearAxes()
    {
        var sourceSkeleton = CreateSkeleton("Root", "Spine");
        var targetSkeleton = CreateSkeleton("Pelvis", "Chest");
        var jointMappings = new[]
        {
            new RetargetJointMapping(sourceSkeleton, targetSkeleton, "Root", 0, "Pelvis", 0),
        };

        Assert.Throws<ArgumentException>(() => new RetargetProfile(
            sourceSkeleton,
            targetSkeleton,
            jointMappings,
            sourceForwardAxis: RetargetAxis.PositiveZ,
            sourceUpAxis: RetargetAxis.NegativeZ));
    }

    private static SkeletonDefinition CreateSkeleton(string rootName, string childName)
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition(rootName, -1, BoneTransform.Identity, Matrix.Identity, 0),
                new SkeletonJointDefinition(childName, 0, BoneTransform.Identity, Matrix.Identity, 1),
            });
    }
}