using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class SkinnedMeshAnimationRuntimeTests
{
    [Fact]
    public void OverrideRuntimeAnimationAssets_DoesNotCreateLegacyRuntimeByDefault()
    {
        var riggedModel = CreateRiggedModel();
        var skeleton = CreateSkeleton();

        riggedModel.OverrideRuntimeAnimationAssets(
            skeleton,
            new[]
            {
                CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero),
            });

        Assert.Same(skeleton, riggedModel.SkeletonDefinition);
        Assert.Null(riggedModel.AnimationController);
        Assert.Null(riggedModel.LocalPose);
        Assert.Null(riggedModel.ModelPose);
        Assert.Single(riggedModel.AnimationClips);
    }

    [Fact]
    public void RuntimeInstances_KeepPlaybackStateIndependentForSharedRiggedModel()
    {
        var riggedModel = CreateRiggedModel();
        var skeleton = CreateSkeleton();

        riggedModel.OverrideRuntimeAnimationAssets(
            skeleton,
            new[]
            {
                CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero),
                CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(8f, 0f, 0f)),
            });

        var firstRuntime = new SkinnedMeshAnimationRuntime(riggedModel);
        var secondRuntime = new SkinnedMeshAnimationRuntime(riggedModel);

        firstRuntime.PlayAnimation(0);
        firstRuntime.Update(0.5f);
        secondRuntime.PlayAnimation(1);
        secondRuntime.Update(0.5f);

        Assert.NotSame(firstRuntime.SkinningPalette, secondRuntime.SkinningPalette);
        Assert.Equal(Vector3.Zero, firstRuntime.LocalPose.GetTransform(0).Translation);
        Assert.Equal(new Vector3(4f, 0f, 0f), secondRuntime.LocalPose.GetTransform(0).Translation);
        Assert.Equal(Vector3.Zero, firstRuntime.SkinningPalette[0].Translation);
        Assert.Equal(new Vector3(4f, 0f, 0f), secondRuntime.SkinningPalette[0].Translation);
        Assert.Null(riggedModel.AnimationController);
    }

    [Fact]
    public void AnimationTimeSeconds_WrapsOnTheLoopPeriod_ForALoopingState()
    {
        var riggedModel = CreateRiggedModel();
        var skeleton = CreateSkeleton();
        riggedModel.OverrideRuntimeAnimationAssets(
            skeleton,
            new[] { CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(8f, 0f, 0f)).WithLoopPeriod(1.25f) });

        var runtime = new SkinnedMeshAnimationRuntime(riggedModel);
        runtime.PlayAnimation(0);
        runtime.Update(1.125f);

        Assert.Equal(1.125f, runtime.AnimationTimeSeconds, 4);
        // 1 s -> x = 8, 1.25 s -> back to x = 0: halfway through the extra interval.
        Assert.Equal(4f, runtime.LocalPose.GetTransform(0).Translation.X, 3);

        runtime.Update(0.25f);
        Assert.Equal(0.125f, runtime.AnimationTimeSeconds, 4);
    }

    [Fact]
    public void BeginAnimation_CreatesLegacyRuntimeOnDemand()
    {
        var riggedModel = CreateRiggedModel();
        var skeleton = CreateSkeleton();

        riggedModel.OverrideRuntimeAnimationAssets(
            skeleton,
            new[]
            {
                CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero),
                CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(8f, 0f, 0f)),
            });

        riggedModel.BeginAnimation(1);
        riggedModel.Update(0.5f);

        Assert.NotNull(riggedModel.AnimationController);
        Assert.NotNull(riggedModel.LocalPose);
        Assert.NotNull(riggedModel.ModelPose);
        Assert.Equal(new Vector3(4f, 0f, 0f), riggedModel.LocalPose!.GetTransform(0).Translation);
    }

    private static RiggedModel CreateRiggedModel()
    {
        var riggedModel = new RiggedModel();
        var rootNode = new RiggedModel.RiggedModelNode
        {
            Name = "Root",
            IsTheRootNode = true,
            IsThisARealBone = true,
            BoneShaderFinalTransformIndex = 0,
            BindLocalTransformMg = Matrix.Identity,
            LocalTransformMg = Matrix.Identity,
            CombinedTransformMg = Matrix.Identity,
            OffsetMatrixMg = Matrix.Identity,
        };

        riggedModel.RootNodeOfTree = rootNode;
        riggedModel.FirstRealBoneInTree = rootNode;
        riggedModel.Meshes = Array.Empty<RiggedModel.RiggedModelMesh>();
        riggedModel.FlatListToAllNodes.Add(rootNode);
        riggedModel.FlatListToBoneNodes.Add(rootNode);
        riggedModel.NumberOfBonesInUse = 1;
        riggedModel.NumberOfNodesInUse = 1;
        return riggedModel;
    }

    private static SkeletonDefinition CreateSkeleton()
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition("Root", -1, BoneTransform.Identity, Matrix.Identity, 0),
            });
    }

    private static AnimationClip CreateClip(SkeletonDefinition skeleton, string name, Vector3 start, Vector3 end)
    {
        return new AnimationClip(
            name,
            skeleton,
            new[]
            {
                new JointAnimationTrack(
                    0,
                    new Vector3AnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Vector3>(0f, start),
                            new AnimationKeyframe<Vector3>(1f, end),
                        }),
                    null,
                    null),
            },
            1f);
    }
}