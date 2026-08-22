using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// Synthetic 4-joint leg: Root(0) -&gt; Hip(1) -&gt; Knee(2) -&gt; Ankle(3), facing +Z.
/// The knee is bent (hip-&gt;knee runs +Z, knee-&gt;ankle runs -Y) so the chain is never collinear,
/// which keeps the two-bone IK solver's bend-plane math well defined for the reach test.
/// Both segments are 40 units.
/// </summary>
public class FootLockControllerTests
{
    private const int RootIndex = 0;
    private const int HipIndex = 1;
    private const int KneeIndex = 2;
    private const int AnkleIndex = 3;
    private const float LegSegmentLength = 40f;

    [Fact]
    public void Update_WhenAnimatedAnkleIsStationaryInWorldSpace_LocksWithNearZeroSlide()
    {
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, new FootLockSettings(), foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        Span<bool> contacts = stackalloc bool[1];

        const float speed = 100f;
        const float dt = 1f / 60f;
        var entityPosition = Vector3.Zero;

        for (var frame = 0; frame < 10; frame++)
        {
            // Ankle slides -Z in the animation while the entity moves +Z at the same speed,
            // so the world-space animated ankle position stays put.
            SlideAnkleAlongZ(localPose, -speed * dt * (frame + 1));
            modelPose.UpdateFromLocalPose(localPose);
            entityPosition += new Vector3(0f, 0f, speed * dt);
            var entityWorld = Matrix.CreateTranslation(entityPosition);

            contacts[0] = true;
            controller.Update(dt, modelPose, entityWorld, contacts);
        }

        var state = controller.GetFootState(0);
        Assert.True(state.IsLocked);
        Assert.InRange(state.SlideDistance, 0f, 0.5f);

        var animatedAnkleWorld = Vector3.Transform(modelPose.GetTransform(AnkleIndex).Translation, Matrix.CreateTranslation(entityPosition));
        AssertVectorNear(animatedAnkleWorld, state.LockedWorldPosition, 0.5f);
    }

    [Fact]
    public void Update_WhenEntityDoesNotMove_LockedTargetStaysConstantAndSlideGrowsAndIkReachesTarget()
    {
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, new FootLockSettings(), foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        Span<bool> contacts = stackalloc bool[1] { true };
        var entityWorld = Matrix.Identity;
        const float dt = 1f / 60f;
        const float angularRatePerSecond = 2.0f;

        // First frame: rising edge, locks at the current (bind) animated position.
        modelPose.UpdateFromLocalPose(localPose);
        controller.Update(dt, modelPose, entityWorld, contacts);
        var lockedPosition = controller.GetFootState(0).LockedWorldPosition;

        var previousSlide = -1f;
        for (var frame = 1; frame <= 5; frame++)
        {
            // Swing the ankle around the (fixed) knee: the entity never moves, so the animated
            // ankle keeps sliding away from the world-space lock point, without changing bone
            // lengths (the target therefore always stays reachable).
            var angle = -angularRatePerSecond * frame * dt;
            SetKneeRotationAboutX(localPose, angle);
            modelPose.UpdateFromLocalPose(localPose);
            controller.Update(dt, modelPose, entityWorld, contacts);

            var state = controller.GetFootState(0);
            AssertVectorNear(lockedPosition, state.LockedWorldPosition, 1e-3f);
            Assert.True(state.SlideDistance > previousSlide);
            previousSlide = state.SlideDistance;
        }

        var finalState = controller.GetFootState(0);
        Assert.Equal(1f, finalState.Weight, 3);
        Assert.True(finalState.SlideDistance > 1f);

        // With full weight, solving the constraint should place the ankle within 1e-2 of the target.
        var constraint = controller.GetConstraint(0, entityWorld);
        var solved = IkSolverTwoBone.Solve(localPose, modelPose, constraint);
        modelPose.UpdateFromLocalPose(localPose);

        Assert.True(solved);
        AssertVectorNear(constraint.TargetPosition, modelPose.GetTransform(AnkleIndex).Translation, 1e-2f);
    }

    [Fact]
    public void Update_BlendsInAfterRisingEdgeAndOutAfterFallingEdgeThenUnlocks()
    {
        var settings = new FootLockSettings { BlendInSeconds = 0.2f, BlendOutSeconds = 0.4f };
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, settings, foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        modelPose.UpdateFromLocalPose(localPose);

        const float dt = 1f / 60f;
        var entityWorld = Matrix.Identity;
        Span<bool> contacts = stackalloc bool[1];

        // Blend-in: weight must rise monotonically to 1 while contact stays true.
        contacts[0] = true;
        var previousWeight = -1f;
        var framesForBlendIn = (int)MathF.Ceiling(settings.BlendInSeconds / dt) + 5;
        for (var frame = 0; frame < framesForBlendIn; frame++)
        {
            controller.Update(dt, modelPose, entityWorld, contacts);
            var weight = controller.GetFootState(0).Weight;
            Assert.True(weight >= previousWeight - 1e-6f);
            previousWeight = weight;
        }

        Assert.True(controller.GetFootState(0).IsLocked);
        Assert.Equal(1f, controller.GetFootState(0).Weight, 3);

        // Blend-out: falling edge, weight must fall monotonically to 0, then IsLocked becomes false.
        contacts[0] = false;
        previousWeight = 2f;
        var framesForBlendOut = (int)MathF.Ceiling(settings.BlendOutSeconds / dt) + 5;
        for (var frame = 0; frame < framesForBlendOut; frame++)
        {
            controller.Update(dt, modelPose, entityWorld, contacts);
            var weight = controller.GetFootState(0).Weight;
            Assert.True(weight <= previousWeight + 1e-6f);
            previousWeight = weight;
        }

        var finalState = controller.GetFootState(0);
        Assert.False(finalState.IsLocked);
        Assert.Equal(0f, finalState.Weight);
    }

    [Fact]
    public void Update_ReleasesLockWhenDriftExceedsMaxLockDistance()
    {
        var settings = new FootLockSettings { MaxLockDistance = 10f, BlendOutSeconds = 0.1f };
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, settings, foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        modelPose.UpdateFromLocalPose(localPose);

        const float dt = 1f / 60f;
        var entityWorld = Matrix.Identity;
        Span<bool> contacts = stackalloc bool[1] { true };

        controller.Update(dt, modelPose, entityWorld, contacts);
        Assert.True(controller.GetFootState(0).IsLocked);

        // Slide the animated ankle far beyond MaxLockDistance while contact stays true.
        for (var frame = 0; frame < 60; frame++)
        {
            SlideAnkleAlongZ(localPose, -50f * (frame + 1));
            modelPose.UpdateFromLocalPose(localPose);
            controller.Update(dt, modelPose, entityWorld, contacts);
        }

        Assert.False(controller.GetFootState(0).IsLocked);
        Assert.Equal(0f, controller.GetFootState(0).Weight);
    }

    [Fact]
    public void GetConstraint_WhenLockVerticalIsFalse_KeepsAnimatedVerticalPosition()
    {
        var settings = new FootLockSettings { LockVertical = false };
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, settings, foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        modelPose.UpdateFromLocalPose(localPose);

        const float dt = 1f / 60f;
        var entityWorld = Matrix.Identity;
        Span<bool> contacts = stackalloc bool[1] { true };
        controller.Update(dt, modelPose, entityWorld, contacts);

        // Bob the ankle up in the animation without changing contact.
        BobAnkleAlongY(localPose, 5f);
        modelPose.UpdateFromLocalPose(localPose);
        controller.Update(dt, modelPose, entityWorld, contacts);

        var constraint = controller.GetConstraint(0, entityWorld);
        var animatedAnkleY = modelPose.GetTransform(AnkleIndex).Translation.Y;

        Assert.Equal(animatedAnkleY, constraint.TargetPosition.Y, 3);
    }

    [Fact]
    public void FromAnkle_ResolvesChainAndThrowsWithoutTwoAncestors()
    {
        var skeleton = CreateLegSkeleton();

        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        Assert.Equal(HipIndex, foot.RootJointIndex);
        Assert.Equal(KneeIndex, foot.MidJointIndex);
        Assert.Equal(AnkleIndex, foot.EndJointIndex);

        // The skeleton root (index 0) has no parent, and the hip (index 1) has only one ancestor.
        Assert.Throws<ArgumentException>(() => FootLockFoot.FromAnkle(skeleton, RootIndex));
        Assert.Throws<ArgumentException>(() => FootLockFoot.FromAnkle(skeleton, HipIndex));
    }

    private static void SlideAnkleAlongZ(SkeletonPoseLocal localPose, float deltaZ)
    {
        var bindTransform = localPose.Skeleton.GetBindLocalTransform(AnkleIndex);
        localPose.SetTransform(AnkleIndex, bindTransform with { Translation = bindTransform.Translation + new Vector3(0f, 0f, deltaZ) });
    }

    private static void BobAnkleAlongY(SkeletonPoseLocal localPose, float deltaY)
    {
        var currentTransform = localPose.GetTransform(AnkleIndex);
        localPose.SetTransform(AnkleIndex, currentTransform with { Translation = currentTransform.Translation + new Vector3(0f, deltaY, 0f) });
    }

    private static void SetKneeRotationAboutX(SkeletonPoseLocal localPose, float angleRadians)
    {
        var kneeBind = localPose.Skeleton.GetBindLocalTransform(KneeIndex);
        localPose.SetTransform(KneeIndex, kneeBind with { Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, angleRadians) });
    }

    /// <summary>Root -&gt; Hip -&gt; Knee -&gt; Ankle, bent at the knee, 40 units per segment.</summary>
    private static SkeletonDefinition CreateLegSkeleton()
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition(
                    "Root",
                    -1,
                    BoneTransform.Identity,
                    Matrix.Identity),
                new SkeletonJointDefinition(
                    "Hip",
                    RootIndex,
                    BoneTransform.Identity,
                    Matrix.Identity),
                new SkeletonJointDefinition(
                    "Knee",
                    HipIndex,
                    new BoneTransform(new Vector3(0f, 0f, LegSegmentLength), Quaternion.Identity, Vector3.One),
                    Matrix.Identity),
                new SkeletonJointDefinition(
                    "Ankle",
                    KneeIndex,
                    new BoneTransform(new Vector3(0f, -LegSegmentLength, 0f), Quaternion.Identity, Vector3.One),
                    Matrix.Identity),
            });
    }

    private static void AssertVectorNear(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0f, tolerance);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0f, tolerance);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0f, tolerance);
    }
}
