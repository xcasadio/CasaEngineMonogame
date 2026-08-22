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
        Assert.False(controller.GetFootState(0).IsReleasing);
        Assert.Equal(1f, controller.GetFootState(0).Weight, 3);

        // Blend-out: falling edge, weight must fall monotonically to 0, then IsLocked becomes false.
        // IsReleasing flags the whole blend-out (still locked, weight dropping) and nothing else.
        contacts[0] = false;
        previousWeight = 2f;
        var framesForBlendOut = (int)MathF.Ceiling(settings.BlendOutSeconds / dt) + 5;
        for (var frame = 0; frame < framesForBlendOut; frame++)
        {
            controller.Update(dt, modelPose, entityWorld, contacts);
            var state = controller.GetFootState(0);
            Assert.True(state.Weight <= previousWeight + 1e-6f);
            Assert.Equal(state.IsLocked, state.IsReleasing);
            previousWeight = state.Weight;
        }

        var finalState = controller.GetFootState(0);
        Assert.False(finalState.IsLocked);
        Assert.False(finalState.IsReleasing);
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
    public void Update_WhenContactStartsWithAnkleAboveMaxLockHeight_KeepsContactPendingUntilAnkleComesDown()
    {
        // Bind ankle sits at Y = -LegSegmentLength: treat that as the resting (ground) height.
        var settings = new FootLockSettings { GroundHeight = -LegSegmentLength, MaxLockHeight = 10f, BlendInSeconds = 0f };
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, settings, foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        const float dt = 1f / 60f;
        var entityWorld = Matrix.Identity;
        Span<bool> contacts = stackalloc bool[1] { true };

        // Ankle lifted 25 units above its resting height (> MaxLockHeight) while a contact is
        // reported (the Run -> Stunned case: the target clip says "planted" but the blended pose
        // still has the foot mid-swing): the lock must not engage in the air.
        BobAnkleAlongY(localPose, 25f);
        modelPose.UpdateFromLocalPose(localPose);
        for (var frame = 0; frame < 5; frame++)
        {
            controller.Update(dt, modelPose, entityWorld, contacts);
            var pending = controller.GetFootState(0);
            Assert.False(pending.IsLocked);
            Assert.Equal(0f, pending.Weight);
        }

        // The contact then drops out while still in the air: no falling edge, nothing to release.
        contacts[0] = false;
        controller.Update(dt, modelPose, entityWorld, contacts);
        Assert.False(controller.GetFootState(0).IsLocked);

        // Contact again, ankle back within MaxLockHeight of the ground: this is the rising edge,
        // and the pin lands at the current animated position (not the mid-air one).
        contacts[0] = true;
        BobAnkleAlongY(localPose, -20f);
        modelPose.UpdateFromLocalPose(localPose);
        controller.Update(dt, modelPose, entityWorld, contacts);

        var locked = controller.GetFootState(0);
        Assert.True(locked.IsLocked);
        Assert.Equal(1f, locked.Weight, 3);
        AssertVectorNear(modelPose.GetTransform(AnkleIndex).Translation, locked.LockedWorldPosition, 1e-3f);
    }

    [Fact]
    public void Update_WhenLockVerticalIsOff_VerticalBobIsNeitherSlideNorARelease()
    {
        var settings = new FootLockSettings { MaxLockDistance = 5f, BlendInSeconds = 0f };
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, settings, foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        modelPose.UpdateFromLocalPose(localPose);
        Span<bool> contacts = stackalloc bool[1] { true };
        controller.Update(1f / 60f, modelPose, Matrix.Identity, contacts);

        // The ankle rises 12 units (more than MaxLockDistance) without moving on the ground plane.
        BobAnkleAlongY(localPose, 12f);
        modelPose.UpdateFromLocalPose(localPose);
        controller.Update(1f / 60f, modelPose, Matrix.Identity, contacts);

        var state = controller.GetFootState(0);
        Assert.True(state.IsLocked);
        Assert.False(state.IsReleasing);
        Assert.Equal(0f, state.SlideDistance, 3);

        // With LockVertical on, the same bob is a real drift.
        var vertical = new FootLockController(skeleton, settings with { LockVertical = true }, foot);
        localPose = skeleton.CreateLocalBindPose();
        modelPose.UpdateFromLocalPose(localPose);
        vertical.Update(1f / 60f, modelPose, Matrix.Identity, contacts);
        BobAnkleAlongY(localPose, 12f);
        modelPose.UpdateFromLocalPose(localPose);
        vertical.Update(1f / 60f, modelPose, Matrix.Identity, contacts);
        Assert.Equal(12f, vertical.GetFootState(0).SlideDistance, 2);
        Assert.True(vertical.GetFootState(0).IsReleasing);
    }

    [Fact]
    public void Update_WhenAnkleLiftsAfterLockingWithMaxLockHeight_KeepsTheLock()
    {
        var settings = new FootLockSettings { GroundHeight = -LegSegmentLength, MaxLockHeight = 10f, BlendInSeconds = 0f };
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, settings, foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        modelPose.UpdateFromLocalPose(localPose);
        const float dt = 1f / 60f;
        Span<bool> contacts = stackalloc bool[1] { true };

        controller.Update(dt, modelPose, Matrix.Identity, contacts);
        Assert.True(controller.GetFootState(0).IsLocked);

        // The height check only gates the rising edge: a planted foot bobbing above MaxLockHeight
        // stays locked (release is still driven by the falling edge / MaxLockDistance only).
        BobAnkleAlongY(localPose, 25f);
        modelPose.UpdateFromLocalPose(localPose);
        controller.Update(dt, modelPose, Matrix.Identity, contacts);

        var state = controller.GetFootState(0);
        Assert.True(state.IsLocked);
        Assert.Equal(1f, state.Weight, 3);
    }

    [Fact]
    public void Release_RePinsAtCurrentPositionWhenContactContinuesAndBlendsOutOtherwise()
    {
        var settings = new FootLockSettings { BlendInSeconds = 0f, BlendOutSeconds = 0.1f };
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
        var originalPin = controller.GetFootState(0).LockedWorldPosition;

        // The animation drifts a little (a clip change lands the foot somewhere else), then the
        // caller releases because the contact source changed.
        SlideAnkleAlongZ(localPose, -8f);
        modelPose.UpdateFromLocalPose(localPose);
        controller.Update(dt, modelPose, entityWorld, contacts);
        controller.Release();

        // Contact still reported by the new source: the foot re-pins where it is now, with the
        // weight carried over (no pop), instead of being dragged back to the old pin.
        controller.Update(dt, modelPose, entityWorld, contacts);
        var rePinned = controller.GetFootState(0);
        Assert.True(rePinned.IsLocked);
        Assert.Equal(1f, rePinned.Weight, 3);
        AssertVectorNear(modelPose.GetTransform(AnkleIndex).Translation, rePinned.LockedWorldPosition, 1e-3f);
        Assert.True(Vector3.Distance(originalPin, rePinned.LockedWorldPosition) > 7f);

        // Release again, this time with no contact from the new source: plain blend-out to unlocked.
        controller.Release();
        contacts[0] = false;
        var previousWeight = 2f;
        for (var frame = 0; frame < 12; frame++)
        {
            controller.Update(dt, modelPose, entityWorld, contacts);
            var weight = controller.GetFootState(0).Weight;
            Assert.True(weight <= previousWeight + 1e-6f);
            previousWeight = weight;
        }

        Assert.False(controller.GetFootState(0).IsLocked);
        Assert.Equal(0f, controller.GetFootState(0).Weight);
    }

    [Fact]
    public void Reset_ClearsLockAndContactHistoryImmediately()
    {
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, new FootLockSettings { BlendInSeconds = 0f }, foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        modelPose.UpdateFromLocalPose(localPose);
        Span<bool> contacts = stackalloc bool[1] { true };

        controller.Update(1f / 60f, modelPose, Matrix.Identity, contacts);
        Assert.True(controller.GetFootState(0).IsLocked);

        controller.Reset();

        var cleared = controller.GetFootState(0);
        Assert.False(cleared.IsLocked);
        Assert.Equal(0f, cleared.Weight);
        Assert.Equal(0f, cleared.SlideDistance);

        // Contact history is gone too: the very next contact frame is a rising edge again.
        SlideAnkleAlongZ(localPose, -5f);
        modelPose.UpdateFromLocalPose(localPose);
        controller.Update(1f / 60f, modelPose, Matrix.Identity, contacts);
        var relocked = controller.GetFootState(0);
        Assert.True(relocked.IsLocked);
        AssertVectorNear(modelPose.GetTransform(AnkleIndex).Translation, relocked.LockedWorldPosition, 1e-3f);
    }

    [Fact]
    public void Update_AfterDriftRelease_RePinsOnceTheFootComesToRestWhileContactStaysTrue()
    {
        var settings = new FootLockSettings { MaxLockDistance = 10f, BlendInSeconds = 0.05f, BlendOutSeconds = 0.05f, RelockMaxSpeed = 30f };
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
        var firstPin = controller.GetFootState(0).LockedWorldPosition;

        // A transition drags the planted foot away at 300 units/s (5 per frame) while the clip keeps
        // reporting contact: the lock releases and, the foot still moving, must not re-pin.
        var sawUnlocked = false;
        for (var frame = 1; frame <= 12; frame++)
        {
            SlideAnkleAlongZ(localPose, -5f * frame);
            modelPose.UpdateFromLocalPose(localPose);
            controller.Update(dt, modelPose, entityWorld, contacts);
            sawUnlocked |= !controller.GetFootState(0).IsLocked;
        }

        Assert.True(sawUnlocked);
        Assert.False(controller.GetFootState(0).IsLocked);

        // The foot then comes to rest (60 units away): the still-true contact re-pins it there.
        for (var frame = 0; frame < 10; frame++)
        {
            controller.Update(dt, modelPose, entityWorld, contacts);
        }

        var state = controller.GetFootState(0);
        Assert.True(state.IsLocked);
        Assert.Equal(1f, state.Weight, 3);
        AssertVectorNear(modelPose.GetTransform(AnkleIndex).Translation, state.LockedWorldPosition, 1e-3f);
        Assert.True(Vector3.Distance(firstPin, state.LockedWorldPosition) > 50f);
    }

    [Fact]
    public void Update_AfterDriftRelease_WithoutRelockSpeed_StaysFreeUntilTheNextRisingEdge()
    {
        var settings = new FootLockSettings { MaxLockDistance = 10f, BlendInSeconds = 0f, BlendOutSeconds = 0f };
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, settings, foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        modelPose.UpdateFromLocalPose(localPose);
        const float dt = 1f / 60f;
        Span<bool> contacts = stackalloc bool[1] { true };

        controller.Update(dt, modelPose, Matrix.Identity, contacts);
        SlideAnkleAlongZ(localPose, -25f);
        modelPose.UpdateFromLocalPose(localPose);
        controller.Update(dt, modelPose, Matrix.Identity, contacts);
        Assert.False(controller.GetFootState(0).IsLocked);

        // Foot at rest, contact still true: no re-pin without RelockMaxSpeed...
        for (var frame = 0; frame < 10; frame++)
        {
            controller.Update(dt, modelPose, Matrix.Identity, contacts);
        }

        Assert.False(controller.GetFootState(0).IsLocked);

        // ...until a real rising edge.
        contacts[0] = false;
        controller.Update(dt, modelPose, Matrix.Identity, contacts);
        contacts[0] = true;
        controller.Update(dt, modelPose, Matrix.Identity, contacts);
        Assert.True(controller.GetFootState(0).IsLocked);
    }

    [Fact]
    public void TranslateLockedPositions_MovesThePinWithATeleportedEntity()
    {
        var settings = new FootLockSettings { BlendInSeconds = 0f, MaxLockDistance = 10f };
        var skeleton = CreateLegSkeleton();
        var foot = FootLockFoot.FromAnkle(skeleton, AnkleIndex);
        var controller = new FootLockController(skeleton, settings, foot);

        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        modelPose.UpdateFromLocalPose(localPose);
        const float dt = 1f / 60f;
        Span<bool> contacts = stackalloc bool[1] { true };

        controller.Update(dt, modelPose, Matrix.Identity, contacts);
        var pinBefore = controller.GetFootState(0).LockedWorldPosition;

        // Teleport the entity 6 units along -Z (a treadmill wrap) and shift the pins along.
        var teleport = new Vector3(0f, 0f, -600f);
        controller.TranslateLockedPositions(teleport);
        controller.Update(dt, modelPose, Matrix.CreateTranslation(teleport), contacts);

        var state = controller.GetFootState(0);
        Assert.True(state.IsLocked);
        Assert.Equal(1f, state.Weight, 3);
        AssertVectorNear(pinBefore + teleport, state.LockedWorldPosition, 1e-3f);
        Assert.InRange(state.SlideDistance, 0f, 1e-3f);

        // The constraint expressed in the entity's model space is unchanged by the teleport.
        var constraint = controller.GetConstraint(0, Matrix.CreateTranslation(teleport));
        AssertVectorNear(modelPose.GetTransform(AnkleIndex).Translation, constraint.TargetPosition, 1e-3f);
    }

    [Fact]
    public void Validate_RejectsNonPositiveOrNaNMaxLockHeightAndNaNGroundHeight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FootLockSettings { RelockMaxSpeed = -1f }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new FootLockSettings { MaxLockHeight = 0f }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new FootLockSettings { MaxLockHeight = -1f }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new FootLockSettings { MaxLockHeight = float.NaN }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new FootLockSettings { GroundHeight = float.NaN }.Validate());
        new FootLockSettings { MaxLockHeight = float.PositiveInfinity, GroundHeight = -12f }.Validate();
        new FootLockSettings { MaxLockHeight = 15f }.Validate();
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
