using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class AnimationControllerTests
{
    [Fact]
    public void Play_SamplesCurrentClipIntoOutputPose()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(skeleton, "A", Vector3.Zero, new Vector3(4f, 0f, 0f));
        var controller = new AnimationController(skeleton);

        controller.Play(clip);
        controller.Update(0.5f);

        Assert.Equal(new Vector3(2f, 0f, 0f), controller.OutputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void CrossFade_BlendsSourceAndTargetClips()
    {
        var skeleton = CreateSkeleton();
        var clipA = CreateClip(skeleton, "A", Vector3.Zero, Vector3.Zero);
        var clipB = CreateClip(skeleton, "B", new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 0f));
        var controller = new AnimationController(skeleton);

        controller.Play(clipA);
        controller.CrossFade(clipB, 1f);
        controller.Update(0.5f);

        Assert.True(controller.IsCrossFading);
        Assert.Equal(new Vector3(5f, 0f, 0f), controller.OutputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void CrossFade_CompletesAndPromotesTargetState()
    {
        var skeleton = CreateSkeleton();
        var clipA = CreateClip(skeleton, "A", Vector3.Zero, Vector3.Zero);
        var clipB = CreateClip(skeleton, "B", new Vector3(8f, 0f, 0f), new Vector3(8f, 0f, 0f));
        var controller = new AnimationController(skeleton);

        controller.Play(clipA);
        controller.CrossFade(clipB, 0.25f);
        controller.Update(0.3f);

        Assert.False(controller.IsCrossFading);
        Assert.NotNull(controller.CurrentState);
        Assert.Equal("B", controller.CurrentState!.Clip.Name);
        Assert.Equal(new Vector3(8f, 0f, 0f), controller.OutputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void Update_ExtractsRootMotionDeltaFromOutputPose()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(10f, 0f, 0f));
        var controller = new AnimationController(skeleton);

        controller.Play(clip);
        controller.Update(0.25f);
        var firstDelta = controller.ConsumeRootMotionDelta();

        controller.Update(0.25f);
        var secondDelta = controller.ConsumeRootMotionDelta();

        Assert.Equal(new Vector3(2.5f, 0f, 0f), firstDelta.Translation);
        Assert.Equal(new Vector3(2.5f, 0f, 0f), secondDelta.Translation);
        Assert.Equal(Quaternion.Identity, firstDelta.Rotation);
    }

    [Fact]
    public void Update_InApplyRootMotionMode_RemovesRootTransformFromOutputPose()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(10f, 0f, 0f));
        var controller = new AnimationController(skeleton)
        {
            RootMotionMode = RootMotionMode.Apply,
        };

        controller.Play(clip);
        controller.Update(0.25f);
        var rootMotionDelta = controller.ConsumeRootMotionDelta();

        Assert.Equal(new Vector3(2.5f, 0f, 0f), rootMotionDelta.Translation);
        Assert.Equal(Vector3.Zero, controller.OutputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void OverrideLayer_WithBoneMask_AffectsOnlyMaskedJoint()
    {
        var skeleton = CreateTwoJointSkeleton();
        var baseClip = CreateTwoJointClip(skeleton, "Base", Vector3.Zero, new Vector3(0f, 1f, 0f));
        var upperBodyClip = CreateTwoJointClip(skeleton, "Upper", Vector3.Zero, new Vector3(0f, 5f, 0f));
        var controller = new AnimationController(skeleton);
        var mask = new BoneMask(skeleton);
        mask.SetWeight(1, 1f);

        controller.Play(baseClip);
        controller.SetLayerAnimation(0, upperBodyClip, mask, 1f, AnimationLayerBlendMode.Override);
        controller.Update(1f);

        Assert.Equal(Vector3.Zero, controller.OutputPose.GetTransform(0).Translation);
        Assert.Equal(new Vector3(0f, 5f, 0f), controller.OutputPose.GetTransform(1).Translation);
    }

    [Fact]
    public void AdditiveLayer_UsesBindPoseAsReference()
    {
        var skeleton = CreateTwoJointSkeleton();
        var baseClip = CreateTwoJointClip(skeleton, "Base", Vector3.Zero, new Vector3(0f, 2f, 0f));
        var additiveClip = CreateTwoJointClip(skeleton, "Add", Vector3.Zero, new Vector3(0f, 4f, 0f));
        var controller = new AnimationController(skeleton);
        var mask = new BoneMask(skeleton);
        mask.SetWeight(1, 1f);

        controller.Play(baseClip);
        controller.SetLayerAnimation(0, additiveClip, mask, 0.5f, AnimationLayerBlendMode.Additive);
        controller.Update(1f);

        Assert.Equal(new Vector3(0f, 3.5f, 0f), controller.OutputPose.GetTransform(1).Translation);
    }

    [Fact]
    public void Update_DispatchesAnimationEvents_WhenCrossingKeyframes()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(
            skeleton,
            "Events",
            Vector3.Zero,
            Vector3.Zero,
            new AnimationEventTrack(
                new[]
                {
                    new AnimationEventKeyframe(0.25f, "Footstep"),
                }));
        var controller = new AnimationController(skeleton);
        var triggeredEvents = new List<string>();
        controller.AnimationEventTriggered += keyframe => triggeredEvents.Add(keyframe.EventName);

        controller.Play(clip);
        controller.Update(0.3f);

        Assert.Single(triggeredEvents);
        Assert.Equal("Footstep", triggeredEvents[0]);
    }

    [Fact]
    public void Seek_DoesNotDispatchAnimationEvents()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(
            skeleton,
            "Events",
            Vector3.Zero,
            Vector3.Zero,
            new AnimationEventTrack(
                new[]
                {
                    new AnimationEventKeyframe(0.25f, "Footstep"),
                }));
        var controller = new AnimationController(skeleton);
        var triggeredEvents = new List<string>();
        controller.AnimationEventTriggered += keyframe => triggeredEvents.Add(keyframe.EventName);

        controller.Play(clip);
        controller.Seek(0.5f);

        Assert.Empty(triggeredEvents);
    }

    [Fact]
    public void Update_DispatchesLoopedAnimationEvents_AfterWrap()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(
            skeleton,
            "Events",
            Vector3.Zero,
            Vector3.Zero,
            new AnimationEventTrack(
                new[]
                {
                    new AnimationEventKeyframe(0.1f, "LoopEvent"),
                    new AnimationEventKeyframe(0.9f, "EndEvent"),
                }));
        var controller = new AnimationController(skeleton);
        var triggeredEvents = new List<string>();
        controller.AnimationEventTriggered += keyframe => triggeredEvents.Add(keyframe.EventName);

        controller.Play(clip);
        controller.Update(0.95f);
        controller.Update(0.2f);

        Assert.Equal(new[] { "LoopEvent", "EndEvent", "LoopEvent" }, triggeredEvents);
    }

    [Fact]
    public void PlayGraph_AdvancesRuntimeNodesAndEvaluatesOutputPose()
    {
        var skeleton = CreateSkeleton();
        var idleNode = new AnimationClipNode(CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero), loop: false);
        var runNode = new AnimationClipNode(CreateClip(skeleton, "Run", Vector3.Zero, new Vector3(8f, 0f, 0f)), loop: false);
        var graphRoot = new BlendSpace1DNode(
            new[]
            {
                new BlendSpace1DSample(0f, idleNode),
                new BlendSpace1DSample(1f, runNode),
            },
            parameter: 0.5f);
        var controller = new AnimationController(skeleton);

        controller.PlayGraph(graphRoot);
        controller.Update(1f);

        Assert.True(controller.HasAnimationGraph);
        Assert.True(controller.IsPlaying);
        Assert.Equal(1f, controller.CurrentTimeSeconds);
        Assert.Equal(new Vector3(4f, 0f, 0f), controller.OutputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void PauseAndResume_GraphPlaybackStopsAndRestartsNodeAdvancement()
    {
        var skeleton = CreateSkeleton();
        var clipNode = new AnimationClipNode(CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(4f, 0f, 0f)), loop: false);
        var controller = new AnimationController(skeleton);

        controller.PlayGraph(clipNode);
        controller.Update(0.25f);
        controller.Pause();
        controller.Update(0.25f);

        Assert.Equal(new Vector3(1f, 0f, 0f), controller.OutputPose.GetTransform(0).Translation);
        Assert.False(controller.IsPlaying);

        controller.Resume();
        controller.Update(0.25f);

        Assert.Equal(new Vector3(2f, 0f, 0f), controller.OutputPose.GetTransform(0).Translation);
        Assert.True(controller.IsPlaying);
    }

    private static SkeletonDefinition CreateSkeleton()
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition("Root", -1, BoneTransform.Identity, Matrix.Identity),
            });
    }

    private static AnimationClip CreateClip(
        SkeletonDefinition skeleton,
        string name,
        Vector3 start,
        Vector3 end,
        AnimationEventTrack? eventTrack = null)
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
                    1f,
                    eventTrack);
    }

    private static SkeletonDefinition CreateTwoJointSkeleton()
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition("Root", -1, BoneTransform.Identity, Matrix.Identity),
                new SkeletonJointDefinition(
                    "Upper",
                    0,
                    new BoneTransform(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One),
                    Matrix.Identity),
            });
    }

    private static AnimationClip CreateTwoJointClip(SkeletonDefinition skeleton, string name, Vector3 rootTranslation, Vector3 upperTranslation)
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
                            new AnimationKeyframe<Vector3>(0f, rootTranslation),
                            new AnimationKeyframe<Vector3>(1f, rootTranslation),
                        }),
                    null,
                    null),
                new JointAnimationTrack(
                    1,
                    new Vector3AnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Vector3>(0f, upperTranslation),
                            new AnimationKeyframe<Vector3>(1f, upperTranslation),
                        }),
                    null,
                    null),
            },
            1f);
    }
}