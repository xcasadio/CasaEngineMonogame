using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class RetargetProcessorTests
{
    [Fact]
    public void RetargetProcessor_RetargetsRootTranslationBetweenAxisConventions()
    {
        var sourceSkeleton = CreateSourceSkeleton();
        var targetSkeleton = CreateTargetSkeleton();
        var profile = CreateRetargetProfile(sourceSkeleton, targetSkeleton);
        var sourceClip = new AnimationClip(
            "MoveForward",
            sourceSkeleton,
            new[]
            {
                new JointAnimationTrack(
                    0,
                    new Vector3AnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Vector3>(0f, new Vector3(0f, 1f, 0f)),
                            new AnimationKeyframe<Vector3>(1f, new Vector3(0f, 1f, 10f)),
                        }),
                    null,
                    null),
            },
            1f);

        var retargetedClip = RetargetProcessor.RetargetClip(sourceClip, profile, "MoveForward_Retargeted");

        Assert.True(retargetedClip.TryGetJointTrack(0, out var rootTrack));
        Assert.NotNull(rootTrack);
        Assert.NotNull(rootTrack!.TranslationTrack);
        Assert.Equal(new Vector3(0f, 1f, 0f), rootTrack.TranslationTrack!.GetKeyframe(0).Value);
        Assert.Equal(new Vector3(-1f, 1f, 0f), rootTrack.TranslationTrack.GetKeyframe(1).Value);
    }

    [Fact]
    public void RetargetProcessor_PreservesChildRotationDeltaRelativeToBindPose()
    {
        var sourceSkeleton = CreateSourceSkeleton();
        var targetSkeleton = CreateTargetSkeleton();
        var profile = CreateRetargetProfile(sourceSkeleton, targetSkeleton);
        var sourceBindRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathHelper.PiOver4);
        var targetBindRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.PiOver4);

        sourceSkeleton = CreateSourceSkeleton(sourceBindRotation);
        targetSkeleton = CreateTargetSkeleton(targetBindRotation);
        profile = CreateRetargetProfile(sourceSkeleton, targetSkeleton);

        var sourceDeltaRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver2);
        var sourceClip = new AnimationClip(
            "UpperBody",
            sourceSkeleton,
            new[]
            {
                new JointAnimationTrack(
                    1,
                    null,
                    new QuaternionAnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Quaternion>(0f, sourceBindRotation),
                            new AnimationKeyframe<Quaternion>(1f, Quaternion.Normalize(sourceBindRotation * sourceDeltaRotation)),
                        }),
                    null),
            },
            1f);

        var retargetedClip = RetargetProcessor.RetargetClip(sourceClip, profile, "UpperBody_Retargeted");

        Assert.True(retargetedClip.TryGetJointTrack(1, out var childTrack));
        Assert.NotNull(childTrack);
        Assert.NotNull(childTrack!.RotationTrack);

        var retargetedRotation = childTrack.RotationTrack!.GetKeyframe(1).Value;
        var expectedRotation = Quaternion.Normalize(targetBindRotation * sourceDeltaRotation);
        var rotatedUp = Vector3.Transform(Vector3.UnitY, retargetedRotation);
        var expectedUp = Vector3.Transform(Vector3.UnitY, expectedRotation);

        AssertVector3Equal(expectedUp, rotatedUp);
    }

    private static SkeletonDefinition CreateSourceSkeleton(Quaternion childBindRotation = default)
    {
        if (childBindRotation == default)
        {
            childBindRotation = Quaternion.Identity;
        }

        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition("Root", -1, new BoneTransform(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One), Matrix.Identity, 0),
                new SkeletonJointDefinition("Hand_R", 0, new BoneTransform(new Vector3(0f, 0f, 1f), childBindRotation, Vector3.One), Matrix.Identity, 1),
            });
    }

    private static SkeletonDefinition CreateTargetSkeleton(Quaternion childBindRotation = default)
    {
        if (childBindRotation == default)
        {
            childBindRotation = Quaternion.Identity;
        }

        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition("Pelvis", -1, new BoneTransform(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One), Matrix.Identity, 0),
                new SkeletonJointDefinition("RightHand", 0, new BoneTransform(new Vector3(1f, 0f, 0f), childBindRotation, Vector3.One), Matrix.Identity, 1),
            });
    }

    private static RetargetProfile CreateRetargetProfile(SkeletonDefinition sourceSkeleton, SkeletonDefinition targetSkeleton)
    {
        return new RetargetProfile(
            sourceSkeleton,
            targetSkeleton,
            new[]
            {
                new RetargetJointMapping(sourceSkeleton, targetSkeleton, "Root", 0, "Pelvis", 0, 1f),
                new RetargetJointMapping(sourceSkeleton, targetSkeleton, "Hand_R", 1, "RightHand", 1, 1f),
            },
            sourceForwardAxis: RetargetAxis.PositiveZ,
            sourceUpAxis: RetargetAxis.PositiveY,
            targetForwardAxis: RetargetAxis.PositiveX,
            targetUpAxis: RetargetAxis.PositiveY,
            rootTranslationScale: 0.1f);
    }

    private static void AssertVector3Equal(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) <= 0.0001f, $"Expected {expected} but got {actual}.");
    }
}