using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

/// <summary>
/// <see cref="SkinnedMeshComponent.AttachFootLock"/> must feed the controller the pure animated
/// (pre-IK) pose and solve its constraints in the same frame. The fixture is the same bent
/// 4-joint leg as <see cref="FootLockControllerTests"/> (Root -&gt; Hip -&gt; Knee -&gt; Ankle, 40 units per
/// segment) with one clip sliding the ankle along -Z over one second.
/// </summary>
public class SkinnedMeshComponentFootLockTests
{
    private const int HipIndex = 1;
    private const int KneeIndex = 2;
    private const int AnkleIndex = 3;
    private const float LegSegmentLength = 40f;
    private const float AnkleSlideDistance = 30f;
    private const float ClipDurationSeconds = 1f;

    [Fact]
    public void Update_FeedsTheAnimatedPoseToTheControllerAndSolvesTheLockInTheSameFrame()
    {
        var component = CreateComponentWithSlidingAnkleClip(out var controller);
        component.AttachFootLock(controller, contacts => contacts[0] = true);
        component.PlayAnimation(0);

        const float dt = 1f / 60f;
        var elapsed = 0f;
        for (var frame = 0; frame < 30; frame++)
        {
            component.Update(dt);
            elapsed += dt;
        }

        var state = controller.GetFootState(0);
        var expectedAnimatedAnkle = AnimatedAnkleModelPosition(elapsed);
        var solvedAnkle = component.CurrentModelPose.GetTransform(AnkleIndex).Translation;

        // The controller saw the animation's own ankle (not the IK output it produced last frame)...
        Assert.True(state.IsLocked);
        Assert.Equal(1f, state.Weight, 3);
        AssertVectorNear(expectedAnimatedAnkle, state.AnimatedWorldPosition, 0.5f);
        // ...so the slide is the real drift away from the pin (placed when PlayAnimation evaluated
        // the first pose, at t = 0): 15 units after half a clip...
        AssertVectorNear(AnimatedAnkleModelPosition(0f), state.LockedWorldPosition, 0.1f);
        Assert.InRange(state.SlideDistance, 14f, 16f);
        // ...and the pose exposed after Update is already solved against that pin (X/Z pinned,
        // Y left to the animation since LockVertical is off).
        Assert.InRange(MathF.Abs(solvedAnkle.X - state.LockedWorldPosition.X), 0f, 0.05f);
        Assert.InRange(MathF.Abs(solvedAnkle.Z - state.LockedWorldPosition.Z), 0f, 0.05f);
        Assert.InRange(MathF.Abs(solvedAnkle.Y - expectedAnimatedAnkle.Y), 0f, 0.05f);
        Assert.True(Vector3.Distance(solvedAnkle, expectedAnimatedAnkle) > 1f);
    }

    [Fact]
    public void Update_WhenContactsAreFalse_LeavesThePoseUntouched()
    {
        var component = CreateComponentWithSlidingAnkleClip(out var controller);
        component.AttachFootLock(controller, contacts => contacts[0] = false);
        component.PlayAnimation(0);

        const float dt = 1f / 60f;
        var elapsed = 0f;
        for (var frame = 0; frame < 30; frame++)
        {
            component.Update(dt);
            elapsed += dt;
        }

        Assert.False(controller.GetFootState(0).IsLocked);
        AssertVectorNear(AnimatedAnkleModelPosition(elapsed), component.CurrentModelPose.GetTransform(AnkleIndex).Translation, 0.05f);
    }

    [Fact]
    public void FootLockApplyConstraints_False_KeepsTrackingButDoesNotSolve()
    {
        var component = CreateComponentWithSlidingAnkleClip(out var controller);
        component.AttachFootLock(controller, contacts => contacts[0] = true);
        component.FootLockApplyConstraints = false;
        component.PlayAnimation(0);

        const float dt = 1f / 60f;
        var elapsed = 0f;
        for (var frame = 0; frame < 30; frame++)
        {
            component.Update(dt);
            elapsed += dt;
        }

        var state = controller.GetFootState(0);
        Assert.True(state.IsLocked);
        Assert.True(state.SlideDistance > 1f);
        AssertVectorNear(AnimatedAnkleModelPosition(elapsed), component.CurrentModelPose.GetTransform(AnkleIndex).Translation, 0.05f);
        Assert.All(component.TwoBoneIkConstraints, constraint => Assert.False(constraint.Enabled));

        // Re-enabling applies the (still live) lock on the very next frame.
        component.FootLockApplyConstraints = true;
        component.Update(dt);
        var solvedAnkle = component.CurrentModelPose.GetTransform(AnkleIndex).Translation;
        Assert.InRange(MathF.Abs(solvedAnkle.Z - state.LockedWorldPosition.Z), 0f, 0.05f);
    }

    [Fact]
    public void DetachFootLock_ClearsTheConstraintsItOwned()
    {
        var component = CreateComponentWithSlidingAnkleClip(out var controller);
        component.AttachFootLock(controller, contacts => contacts[0] = true, firstConstraintIndex: 2);
        component.PlayAnimation(0);
        component.Update(1f / 60f);
        component.Update(1f / 60f);

        Assert.True(component.TwoBoneIkConstraints[2].Enabled);
        Assert.Same(controller, component.FootLockController);

        component.DetachFootLock();

        Assert.Null(component.FootLockController);
        Assert.False(component.TwoBoneIkConstraints[2].Enabled);
        component.Update(1f / 60f);
        Assert.False(component.TwoBoneIkConstraints[2].Enabled);
    }

    [Fact]
    public void AttachFootLock_RejectsNullArguments()
    {
        var component = CreateComponentWithSlidingAnkleClip(out var controller);

        Assert.Throws<ArgumentNullException>(() => component.AttachFootLock(null!, contacts => { }));
        Assert.Throws<ArgumentNullException>(() => component.AttachFootLock(controller, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => component.AttachFootLock(controller, contacts => { }, firstConstraintIndex: -1));
    }

    private static Vector3 AnimatedAnkleModelPosition(float timeSeconds)
    {
        var t = MathHelper.Clamp(timeSeconds / ClipDurationSeconds, 0f, 1f);
        // Hip at origin, knee at +Z, ankle hanging -Y below the knee then sliding along -Z.
        return new Vector3(0f, -LegSegmentLength, LegSegmentLength - AnkleSlideDistance * t);
    }

    private static SkinnedMeshComponent CreateComponentWithSlidingAnkleClip(out FootLockController controller)
    {
        var skeleton = CreateLegSkeleton();
        var riggedModel = CreateRiggedModel();
        riggedModel.OverrideRuntimeAnimationAssets(skeleton, new[] { CreateSlidingAnkleClip(skeleton) });

        var skinnedMesh = new SkinnedMesh();
        skinnedMesh.SetRiggedModel(riggedModel);

        var component = new SkinnedMeshComponent { SkinnedMesh = skinnedMesh };
        controller = new FootLockController(
            skeleton,
            new FootLockSettings { BlendInSeconds = 0f, MaxLockDistance = 1000f },
            FootLockFoot.FromAnkle(skeleton, AnkleIndex));
        return component;
    }

    private static AnimationClip CreateSlidingAnkleClip(SkeletonDefinition skeleton)
    {
        var ankleBind = skeleton.GetBindLocalTransform(AnkleIndex).Translation;
        return new AnimationClip(
            "SlideAnkle",
            skeleton,
            new[]
            {
                new JointAnimationTrack(
                    AnkleIndex,
                    new Vector3AnimationTrack(
                        new[]
                        {
                            new AnimationKeyframe<Vector3>(0f, ankleBind),
                            new AnimationKeyframe<Vector3>(ClipDurationSeconds, ankleBind + new Vector3(0f, 0f, -AnkleSlideDistance)),
                        }),
                    null,
                    null),
            },
            ClipDurationSeconds);
    }

    private static SkeletonDefinition CreateLegSkeleton()
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition("Root", -1, BoneTransform.Identity, Matrix.Identity),
                new SkeletonJointDefinition("Hip", 0, BoneTransform.Identity, Matrix.Identity),
                new SkeletonJointDefinition("Knee", HipIndex, new BoneTransform(new Vector3(0f, 0f, LegSegmentLength), Quaternion.Identity, Vector3.One), Matrix.Identity),
                new SkeletonJointDefinition("Ankle", KneeIndex, new BoneTransform(new Vector3(0f, -LegSegmentLength, 0f), Quaternion.Identity, Vector3.One), Matrix.Identity),
            });
    }

    /// <summary>One node per skeleton joint (same names/order), as <see cref="RiggedModel.OverrideRuntimeAnimationAssets"/> requires.</summary>
    private static RiggedModel CreateRiggedModel()
    {
        var riggedModel = new RiggedModel();
        var names = new[] { "Root", "Hip", "Knee", "Ankle" };
        RiggedModel.RiggedModelNode parent = null;
        for (var nodeIndex = 0; nodeIndex < names.Length; nodeIndex++)
        {
            var node = new RiggedModel.RiggedModelNode
            {
                Name = names[nodeIndex],
                IsTheRootNode = nodeIndex == 0,
                IsThisARealBone = true,
                BoneShaderFinalTransformIndex = nodeIndex,
                BindLocalTransformMg = Matrix.Identity,
                LocalTransformMg = Matrix.Identity,
                CombinedTransformMg = Matrix.Identity,
                OffsetMatrixMg = Matrix.Identity,
                Parent = parent,
            };
            parent?.Children.Add(node);

            riggedModel.FlatListToAllNodes.Add(node);
            riggedModel.FlatListToBoneNodes.Add(node);
            if (nodeIndex == 0)
            {
                riggedModel.RootNodeOfTree = node;
                riggedModel.FirstRealBoneInTree = node;
            }

            parent = node;
        }

        riggedModel.Meshes = Array.Empty<RiggedModel.RiggedModelMesh>();
        riggedModel.NumberOfBonesInUse = names.Length;
        riggedModel.NumberOfNodesInUse = names.Length;
        return riggedModel;
    }

    private static void AssertVectorNear(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0f, tolerance);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0f, tolerance);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0f, tolerance);
    }
}
