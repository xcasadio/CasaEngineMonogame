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
    public void CrossFade_WithEaseOutCubic_UsesNonLinearBlendWeight()
    {
        var skeleton = CreateSkeleton();
        var clipA = CreateClip(skeleton, "A", Vector3.Zero, Vector3.Zero);
        var clipB = CreateClip(skeleton, "B", new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 0f));
        var controller = new AnimationController(skeleton);

        controller.Play(clipA);
        controller.CrossFade(
            clipB,
            1f,
            new AnimationCrossFadeSettings
            {
                EasingMode = AnimationTransitionEasingMode.EaseOutCubic,
            });
        controller.Update(0.5f);

        Assert.Equal(new Vector3(8.75f, 0f, 0f), controller.OutputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void CrossFade_WithRootVelocityPreservation_PushesTransitionForward()
    {
        var skeleton = CreateSkeleton();
        var movingClip = CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(10f, 0f, 0f));
        var idleClip = CreateClip(skeleton, "Idle", Vector3.Zero, Vector3.Zero);
        var defaultController = new AnimationController(skeleton);
        var preservedController = new AnimationController(skeleton);

        defaultController.Play(movingClip);
        preservedController.Play(movingClip);
        defaultController.Update(0.4f);
        preservedController.Update(0.4f);

        defaultController.CrossFade(idleClip, 1f);
        preservedController.CrossFade(
            idleClip,
            1f,
            new AnimationCrossFadeSettings
            {
                PreserveRootTranslationVelocity = true,
            });

        defaultController.Update(0.2f);
        preservedController.Update(0.2f);

        Assert.True(
            preservedController.OutputPose.GetTransform(0).Translation.X > defaultController.OutputPose.GetTransform(0).Translation.X,
            "The preserved transition should keep more forward motion than the default linear cross-fade.");
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

    [Fact]
    public void Advance_WhilePausedGraph_StepsPlaybackForward()
    {
        var skeleton = CreateSkeleton();
        var clipNode = new AnimationClipNode(CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(4f, 0f, 0f)), loop: false);
        var controller = new AnimationController(skeleton);

        controller.PlayGraph(clipNode);
        controller.Pause();
        controller.Update(0.5f);

        Assert.Equal(Vector3.Zero, controller.OutputPose.GetTransform(0).Translation);

        controller.Advance(0.25f);

        Assert.Equal(new Vector3(1f, 0f, 0f), controller.OutputPose.GetTransform(0).Translation);
        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void Advance_WhilePausedSingleState_StepsPlaybackForward()
    {
        var skeleton = CreateSkeleton();
        var clip = CreateClip(skeleton, "Move", Vector3.Zero, new Vector3(8f, 0f, 0f));
        var controller = new AnimationController(skeleton);

        controller.Play(clip, loop: false);
        controller.Pause();
        controller.Update(0.5f);

        Assert.Equal(Vector3.Zero, controller.OutputPose.GetTransform(0).Translation);

        controller.Advance(0.25f);

        Assert.Equal(new Vector3(2f, 0f, 0f), controller.OutputPose.GetTransform(0).Translation);
    }

    [Fact]
    public void Inertialize_FirstUpdateAfterTransitionStart_HasNoPop()
    {
        var skeleton = CreateThreeJointSkeleton();
        var movingClip = CreateThreeJointLinearClip(skeleton, "Move", velocityPerSecond: 4f, durationSeconds: 4f);
        var idleClip = CreateThreeJointStationaryClip(skeleton, "Idle", translation: new Vector3(100f, 50f, -30f));
        var controller = new AnimationController(skeleton);

        controller.Play(movingClip, loop: false);
        controller.Update(0.5f);
        controller.Update(0.5f);

        var previousTranslations = new Vector3[skeleton.Count];
        for (var jointIndex = 0; jointIndex < skeleton.Count; jointIndex++)
        {
            previousTranslations[jointIndex] = controller.OutputPose.GetTransform(jointIndex).Translation;
        }

        controller.CrossFade(
            idleClip,
            1f,
            new AnimationCrossFadeSettings { TransitionMode = AnimationTransitionMode.Inertialize },
            loop: false);
        controller.Update(1e-5f);

        for (var jointIndex = 0; jointIndex < skeleton.Count; jointIndex++)
        {
            var newTranslation = controller.OutputPose.GetTransform(jointIndex).Translation;
            Assert.True(
                Vector3.Distance(previousTranslations[jointIndex], newTranslation) < 1e-3f,
                $"Joint {jointIndex} popped: {previousTranslations[jointIndex]} -> {newTranslation}");
        }
    }

    [Fact]
    public void Inertialize_FirstFrameVelocity_MatchesSourceVelocity()
    {
        var skeleton = CreateThreeJointSkeleton();
        const float velocityPerSecond = 4f;
        var movingClip = CreateThreeJointLinearClip(skeleton, "Move", velocityPerSecond, durationSeconds: 4f);
        var idleClip = CreateThreeJointStationaryClip(skeleton, "Idle", translation: new Vector3(100f, 50f, -30f));
        var controller = new AnimationController(skeleton);

        controller.Play(movingClip, loop: false);
        controller.Update(0.5f);
        controller.Update(0.5f);

        var previousTranslation = controller.OutputPose.GetTransform(0).Translation;

        controller.CrossFade(
            idleClip,
            1f,
            new AnimationCrossFadeSettings { TransitionMode = AnimationTransitionMode.Inertialize },
            loop: false);

        const float dt = 1e-3f;
        controller.Update(dt);
        var newTranslation = controller.OutputPose.GetTransform(0).Translation;
        var measuredVelocity = (newTranslation - previousTranslation) / dt;

        Assert.Equal(velocityPerSecond, measuredVelocity.X, tolerance: 0.05f);
    }

    [Fact]
    public void Inertialize_AtTransitionEnd_ConvergesExactlyOnTarget_AndStaysThere()
    {
        var skeleton = CreateThreeJointSkeleton();
        var movingClip = CreateThreeJointLinearClip(skeleton, "Move", velocityPerSecond: 4f, durationSeconds: 4f);
        var targetTranslation = new Vector3(100f, 50f, -30f);
        var idleClip = CreateThreeJointStationaryClip(skeleton, "Idle", translation: targetTranslation);
        var controller = new AnimationController(skeleton);

        controller.Play(movingClip, loop: false);
        controller.Update(0.5f);
        controller.Update(0.5f);

        controller.CrossFade(
            idleClip,
            1f,
            new AnimationCrossFadeSettings { TransitionMode = AnimationTransitionMode.Inertialize },
            loop: false);
        controller.Update(1f);

        Assert.False(controller.IsCrossFading);
        for (var jointIndex = 0; jointIndex < skeleton.Count; jointIndex++)
        {
            Assert.True(Vector3.Distance(targetTranslation, controller.OutputPose.GetTransform(jointIndex).Translation) < 1e-4f);
        }

        controller.Update(0.25f);
        Assert.True(Vector3.Distance(targetTranslation, controller.OutputPose.GetTransform(0).Translation) < 1e-4f);
    }

    [Fact]
    public void Inertialize_RotationOffset_DecaysThroughShortestPath()
    {
        var skeleton = CreateSkeleton();
        var longWayRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.ToRadians(350f));
        var sourceClip = CreateRotationClip(skeleton, "Source", longWayRotation);
        var targetClip = CreateRotationClip(skeleton, "Target", Quaternion.Identity);
        var controller = new AnimationController(skeleton);

        controller.Play(sourceClip, loop: false);
        controller.Update(0.1f);
        controller.Update(0.1f);

        controller.CrossFade(
            targetClip,
            1f,
            new AnimationCrossFadeSettings { TransitionMode = AnimationTransitionMode.Inertialize },
            loop: false);

        controller.Update(0.25f);
        AssertShortestPathAngle(controller.OutputPose.GetTransform(0).Rotation);

        controller.Update(0.25f);
        AssertShortestPathAngle(controller.OutputPose.GetTransform(0).Rotation);

        static void AssertShortestPathAngle(Quaternion rotation)
        {
            var normalized = Quaternion.Normalize(rotation);
            var clampedW = Math.Clamp(MathF.Abs(normalized.W), -1f, 1f);
            var angleDegrees = MathHelper.ToDegrees(2f * MathF.Acos(clampedW));

            // The offset started at -10 degrees (the short way around from 350). If the decay
            // took the long way (350 degrees), the intermediate angle would spike towards 350;
            // taking the short way it must never exceed the initial 10-degree offset (plus a
            // small margin for the quintic's non-monotonic tail).
            Assert.True(angleDegrees < 15f, $"Rotation offset took the long way around: {angleDegrees} degrees.");
        }
    }

    private static SkeletonDefinition CreateThreeJointSkeleton()
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition("Root", -1, BoneTransform.Identity, Matrix.Identity),
                new SkeletonJointDefinition(
                    "Middle",
                    0,
                    new BoneTransform(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One),
                    Matrix.Identity),
                new SkeletonJointDefinition(
                    "Tip",
                    1,
                    new BoneTransform(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One),
                    Matrix.Identity),
            });
    }

    private static AnimationClip CreateThreeJointLinearClip(SkeletonDefinition skeleton, string name, float velocityPerSecond, float durationSeconds)
    {
        var tracks = new JointAnimationTrack[skeleton.Count];
        for (var jointIndex = 0; jointIndex < skeleton.Count; jointIndex++)
        {
            tracks[jointIndex] = new JointAnimationTrack(
                jointIndex,
                new Vector3AnimationTrack(
                    new[]
                    {
                        new AnimationKeyframe<Vector3>(0f, Vector3.Zero),
                        new AnimationKeyframe<Vector3>(durationSeconds, new Vector3(velocityPerSecond * durationSeconds, 0f, 0f)),
                    }),
                null,
                null);
        }

        return new AnimationClip(name, skeleton, tracks, durationSeconds);
    }

    private static AnimationClip CreateThreeJointStationaryClip(SkeletonDefinition skeleton, string name, Vector3 translation)
    {
        var tracks = new JointAnimationTrack[skeleton.Count];
        for (var jointIndex = 0; jointIndex < skeleton.Count; jointIndex++)
        {
            tracks[jointIndex] = new JointAnimationTrack(
                jointIndex,
                new Vector3AnimationTrack(
                    new[]
                    {
                        new AnimationKeyframe<Vector3>(0f, translation),
                        new AnimationKeyframe<Vector3>(1f, translation),
                    }),
                null,
                null);
        }

        return new AnimationClip(name, skeleton, tracks, 1f);
    }

    private static AnimationClip CreateRotationClip(SkeletonDefinition skeleton, string name, Quaternion rotation)
    {
        return new AnimationClip(
            name,
            skeleton,
            new[]
            {
                new JointAnimationTrack(
                    0,
                    null,
                    new QuaternionAnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Quaternion>(0f, rotation),
                            new AnimationKeyframe<Quaternion>(1f, rotation),
                        }),
                    null),
            },
            1f);
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