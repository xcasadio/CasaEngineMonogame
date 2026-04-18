using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class AnimationGraphNodeTests
{
    [Fact]
    public void AnimationClipNode_Evaluate_SamplesClipIntoOutputPose()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(10f, 0f, 0f));
        var node = new AnimationClipNode(clip)
        {
            TimeSeconds = 0.5f,
        };
        var outputPose = skeleton.CreateLocalBindPose();

        node.Evaluate(outputPose);

        Assert.Equal(new Vector3(5f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void LinearBlendAnimationNode_Evaluate_BlendsChildren()
    {
        var skeleton = CreateSkeleton();
        var idleClip = CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero);
        var runClip = CreateClip(skeleton, "Run", Vector3.Zero, new Vector3(8f, 0f, 0f));
        var idleNode = new AnimationClipNode(idleClip, loop: false)
        {
            TimeSeconds = 1f,
        };
        var runNode = new AnimationClipNode(runClip, loop: false)
        {
            TimeSeconds = 1f,
        };
        var blendNode = new LinearBlendAnimationNode(idleNode, runNode, 0.25f);
        var outputPose = skeleton.CreateLocalBindPose();

        blendNode.Evaluate(outputPose);

        Assert.Equal(new Vector3(2f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void LinearBlendAnimationNode_Evaluate_MultiInputInterpolatesAdjacentEntries()
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
        var blendNode = new LinearBlendAnimationNode(new IAnimationGraphNode[] { idleNode, walkNode, runNode }, 1.5f);
        var outputPose = skeleton.CreateLocalBindPose();

        blendNode.Evaluate(outputPose);

        Assert.Equal(new Vector3(7f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void LinearBlendAnimationNode_Evaluate_MultiInputClampsOutsideBounds()
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
        var blendNode = new LinearBlendAnimationNode(new IAnimationGraphNode[] { idleNode, walkNode, runNode }, 4f);
        var outputPose = skeleton.CreateLocalBindPose();

        blendNode.Evaluate(outputPose);

        Assert.Equal(new Vector3(10f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void BlendSpace1DNode_Evaluate_InterpolatesNonUniformSamples()
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
        var blendSpace = new BlendSpace1DNode(
            new[]
            {
                new BlendSpace1DSample(0f, idleNode),
                new BlendSpace1DSample(2f, walkNode),
                new BlendSpace1DSample(6f, runNode),
            },
            parameter: 4f);
        var outputPose = skeleton.CreateLocalBindPose();

        blendSpace.Evaluate(outputPose);

        Assert.Equal(new Vector3(7f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void BlendSpace1DNode_Evaluate_ClampsOutsideBounds()
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
        var blendSpace = new BlendSpace1DNode(
            new[]
            {
                new BlendSpace1DSample(0f, idleNode),
                new BlendSpace1DSample(1f, runNode),
            },
            parameter: 2f);
        var outputPose = skeleton.CreateLocalBindPose();

        blendSpace.Evaluate(outputPose);

        Assert.Equal(new Vector3(8f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void BlendSpace2DNode_Evaluate_BlendsInsideTriangle()
    {
        var skeleton = CreateSkeleton();
        var idleNode = new AnimationClipNode(CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero), loop: false)
        {
            TimeSeconds = 1f,
        };
        var strafeNode = new AnimationClipNode(CreateClip(skeleton, "Strafe", Vector3.Zero, new Vector3(10f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var forwardNode = new AnimationClipNode(CreateClip(skeleton, "Forward", Vector3.Zero, new Vector3(0f, 10f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var blendSpace = new BlendSpace2DNode(
            new[]
            {
                new BlendSpace2DSample(new Vector2(0f, 0f), idleNode),
                new BlendSpace2DSample(new Vector2(1f, 0f), strafeNode),
                new BlendSpace2DSample(new Vector2(0f, 1f), forwardNode),
            },
            new Vector2(0.25f, 0.25f));
        var outputPose = skeleton.CreateLocalBindPose();

        blendSpace.Evaluate(outputPose);

        Assert.Equal(new Vector3(2.5f, 2.5f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void BlendSpace2DNode_Evaluate_ClampsToClosestSegmentOutsideHull()
    {
        var skeleton = CreateSkeleton();
        var leftNode = new AnimationClipNode(CreateClip(skeleton, "Left", Vector3.Zero, Vector3.Zero), loop: false)
        {
            TimeSeconds = 1f,
        };
        var rightNode = new AnimationClipNode(CreateClip(skeleton, "Right", Vector3.Zero, new Vector3(10f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var upNode = new AnimationClipNode(CreateClip(skeleton, "Up", Vector3.Zero, new Vector3(0f, 10f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var blendSpace = new BlendSpace2DNode(
            new[]
            {
                new BlendSpace2DSample(new Vector2(0f, 0f), leftNode),
                new BlendSpace2DSample(new Vector2(1f, 0f), rightNode),
                new BlendSpace2DSample(new Vector2(0f, 1f), upNode),
            },
            new Vector2(0.5f, -0.5f));
        var outputPose = skeleton.CreateLocalBindPose();

        blendSpace.Evaluate(outputPose);

        Assert.Equal(new Vector3(5f, 0f, 0f), outputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void BlendSpace2DNode_Evaluate_UsesDirectionalCenterSampleInsideQuad()
    {
        var skeleton = CreateSkeleton();
        var idleNode = new AnimationClipNode(CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero), loop: false)
        {
            TimeSeconds = 1f,
        };
        var leftNode = new AnimationClipNode(CreateClip(skeleton, "Left", Vector3.Zero, new Vector3(-10f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var rightNode = new AnimationClipNode(CreateClip(skeleton, "Right", Vector3.Zero, new Vector3(10f, 0f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var forwardNode = new AnimationClipNode(CreateClip(skeleton, "Forward", Vector3.Zero, new Vector3(0f, 10f, 0f)), loop: false)
        {
            TimeSeconds = 1f,
        };
        var blendSpace = new BlendSpace2DNode(
            new[]
            {
                new BlendSpace2DSample(new Vector2(0f, 0f), idleNode),
                new BlendSpace2DSample(new Vector2(-1f, 0f), leftNode),
                new BlendSpace2DSample(new Vector2(0f, 1f), forwardNode),
                new BlendSpace2DSample(new Vector2(1f, 0f), rightNode),
            },
            new Vector2(0.25f, 0.25f));
        var outputPose = skeleton.CreateLocalBindPose();

        blendSpace.Evaluate(outputPose);

        Assert.Equal(new Vector3(2.5f, 2.5f, 0f), outputPose.GetTransform(0).Translation);
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