using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class WeightedBlendAnimationNodeTests
{
    [Fact]
    public void Evaluate_EqualWeights_BlendsProportionally()
    {
        var skeleton = CreateSkeleton();
        var idleNode = new AnimationClipNode(CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero), loop: false)
        {
            TimeSeconds = 1f,
        };
        var runNode = new AnimationClipNode(CreateClip(skeleton, "Run", Vector3.Zero, new Vector3(8f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var blendNode = new WeightedBlendAnimationNode(
            new IAnimationGraphNode[] { idleNode, runNode },
            new[] { 1f, 1f });
        var outputPose = skeleton.CreateLocalBindPose();

        blendNode.Evaluate(outputPose);

        Assert.Equal(new Vector3(4f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void Evaluate_SingleActiveWeight_ReturnsThatPose()
    {
        var skeleton = CreateSkeleton();
        var idleNode = new AnimationClipNode(CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero), loop: false)
        {
            TimeSeconds = 1f,
        };
        var walkNode = new AnimationClipNode(CreateClip(skeleton, "Walk", Vector3.Zero, new Vector3(4f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var runNode = new AnimationClipNode(CreateClip(skeleton, "Run", Vector3.Zero, new Vector3(10f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var blendNode = new WeightedBlendAnimationNode(
            new IAnimationGraphNode[] { idleNode, walkNode, runNode },
            new[] { 0f, 1f, 0f });
        var outputPose = skeleton.CreateLocalBindPose();

        blendNode.Evaluate(outputPose);

        Assert.Equal(new Vector3(4f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void Evaluate_AllZeroWeights_FallsBackToFirstInput()
    {
        var skeleton = CreateSkeleton();
        var idleNode = new AnimationClipNode(CreateClip(skeleton, "Idle", Vector3.Zero, new Vector3(3f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var runNode = new AnimationClipNode(CreateClip(skeleton, "Run", Vector3.Zero, new Vector3(10f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var blendNode = new WeightedBlendAnimationNode(
            new IAnimationGraphNode[] { idleNode, runNode },
            new[] { 0f, 0f });
        var outputPose = skeleton.CreateLocalBindPose();

        blendNode.Evaluate(outputPose);

        Assert.Equal(new Vector3(3f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void SetWeight_UpdatesBlendAndClampsNegativeToZero()
    {
        var skeleton = CreateSkeleton();
        var idleNode = new AnimationClipNode(CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero), loop: false)
        {
            TimeSeconds = 1f,
        };
        var runNode = new AnimationClipNode(CreateClip(skeleton, "Run", Vector3.Zero, new Vector3(10f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var blendNode = new WeightedBlendAnimationNode(new IAnimationGraphNode[] { idleNode, runNode });

        blendNode.SetWeight(0, -5f);
        blendNode.SetWeight(1, 1f);

        Assert.Equal(0f, blendNode.GetWeight(0));
        Assert.Equal(1f, blendNode.GetWeight(1));

        var outputPose = skeleton.CreateLocalBindPose();
        blendNode.Evaluate(outputPose);

        Assert.Equal(new Vector3(10f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void Advance_AdvancesRuntimeInputNodes()
    {
        var skeleton = CreateSkeleton();
        var moveNode = new AnimationClipNode(CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(10f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 0f,
        };
        var idleNode = new AnimationClipNode(CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero), loop: false)
        {
            TimeSeconds = 0f,
        };
        var blendNode = new WeightedBlendAnimationNode(
            new IAnimationGraphNode[] { moveNode, idleNode },
            new[] { 1f, 0f });

        blendNode.Advance(0.5f);
        var outputPose = skeleton.CreateLocalBindPose();
        blendNode.Evaluate(outputPose);

        Assert.Equal(new Vector3(5f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void Constructor_MismatchedSkeleton_Throws()
    {
        var skeletonA = CreateSkeleton();
        var skeletonB = CreateSkeleton();
        var nodeA = new AnimationClipNode(CreateClip(skeletonA, "A", Vector3.Zero, Vector3.Zero));
        var nodeB = new AnimationClipNode(CreateClip(skeletonB, "B", Vector3.Zero, Vector3.Zero));

        Assert.Throws<ArgumentException>(() =>
            new WeightedBlendAnimationNode(new IAnimationGraphNode[] { nodeA, nodeB }));
    }

    [Fact]
    public void Constructor_MismatchedWeightCount_Throws()
    {
        var skeleton = CreateSkeleton();
        var nodeA = new AnimationClipNode(CreateClip(skeleton, "A", Vector3.Zero, Vector3.Zero));
        var nodeB = new AnimationClipNode(CreateClip(skeleton, "B", Vector3.Zero, Vector3.Zero));

        Assert.Throws<ArgumentException>(() =>
            new WeightedBlendAnimationNode(new IAnimationGraphNode[] { nodeA, nodeB }, new[] { 1f }));
    }

    private static SkeletonDefinition CreateSkeleton()
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition("Root", -1, BoneTransform.Identity, Matrix.Identity),
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
