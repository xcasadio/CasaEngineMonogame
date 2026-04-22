using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class SkeletonPoseTests
{
    [Fact]
    public void SkeletonDefinition_RequiresParentBeforeChild()
    {
        var joints = new[]
        {
            new SkeletonJointDefinition("Child", 1, BoneTransform.Identity, Matrix.Identity),
            new SkeletonJointDefinition("Root", -1, BoneTransform.Identity, Matrix.Identity),
        };

        Assert.Throws<ArgumentException>(() => new SkeletonDefinition(joints));
    }

    [Fact]
    public void LocalPose_ResetToBindPose_RestoresBindTransforms()
    {
        var skeleton = CreateSkeleton();
        var pose = skeleton.CreateLocalBindPose();

        pose.ClearDirty();
        pose.SetTransform(1, new BoneTransform(new Vector3(10f, 0f, 0f), Quaternion.Identity, Vector3.One));

        Assert.True(pose.IsDirty);
        Assert.Equal(1, pose.DirtyStartIndex);

        pose.ResetToBindPose();

        Assert.True(pose.IsDirty);
        Assert.Equal(0, pose.DirtyStartIndex);
        Assert.Equal(skeleton.GetBindLocalTransform(1), pose.GetTransform(1));
    }

    [Fact]
    public void ModelPose_ComposesHierarchyFromLocalPose()
    {
        var skeleton = CreateSkeleton();
        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);

        modelPose.UpdateFromLocalPose(localPose);

        Assert.Equal(new Vector3(1f, 0f, 0f), modelPose.GetTransform(0).Translation);
        Assert.Equal(new Vector3(1f, 2f, 0f), modelPose.GetTransform(1).Translation);
        Assert.Equal(new Vector3(1f, 2f, 3f), modelPose.GetTransform(2).Translation);
    }

    [Fact]
    public void ModelPose_UpdatesDescendantsWhenParentChanges()
    {
        var skeleton = CreateSkeleton();
        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);

        modelPose.UpdateFromLocalPose(localPose);
        localPose.ClearDirty();
        localPose.SetTransform(1, new BoneTransform(new Vector3(5f, 2f, 0f), Quaternion.Identity, Vector3.One));
        modelPose.UpdateFromLocalPose(localPose);

        Assert.Equal(new Vector3(1f, 0f, 0f), modelPose.GetTransform(0).Translation);
        Assert.Equal(new Vector3(6f, 2f, 0f), modelPose.GetTransform(1).Translation);
        Assert.Equal(new Vector3(6f, 2f, 3f), modelPose.GetTransform(2).Translation);
    }

    private static SkeletonDefinition CreateSkeleton()
    {
        var joints = new[]
        {
            new SkeletonJointDefinition(
                "Root",
                -1,
                new BoneTransform(new Vector3(1f, 0f, 0f), Quaternion.Identity, Vector3.One),
                Matrix.Identity),
            new SkeletonJointDefinition(
                "Spine",
                0,
                new BoneTransform(new Vector3(0f, 2f, 0f), Quaternion.Identity, Vector3.One),
                Matrix.Identity),
            new SkeletonJointDefinition(
                "Hand",
                1,
                new BoneTransform(new Vector3(0f, 0f, 3f), Quaternion.Identity, Vector3.One),
                Matrix.Identity),
        };

        return new SkeletonDefinition(joints);
    }
}