using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class IkSolverTwoBoneTests
{
    [Fact]
    public void Solve_ReachesReachableTarget()
    {
        var skeleton = CreateChainSkeleton();
        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        var target = new Vector3(1.25f, 0.75f, 0f);
        var constraint = new TwoBoneIkConstraint(0, 1, 2, target, new Vector3(0f, 0f, 1f));

        var solved = IkSolverTwoBone.Solve(localPose, modelPose, constraint);
        modelPose.UpdateFromLocalPose(localPose);

        Assert.True(solved);
        AssertVectorNear(target, modelPose.GetTransform(2).Translation, 0.001f);
    }

    [Fact]
    public void Solve_ClampsTargetBeyondMaximumReach()
    {
        var skeleton = CreateChainSkeleton();
        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        var constraint = new TwoBoneIkConstraint(0, 1, 2, new Vector3(3f, 0f, 0f), new Vector3(0f, 1f, 0f));

        var solved = IkSolverTwoBone.Solve(localPose, modelPose, constraint);
        modelPose.UpdateFromLocalPose(localPose);

        Assert.True(solved);
        AssertVectorNear(new Vector3(2f, 0f, 0f), modelPose.GetTransform(2).Translation, 0.001f);
    }

    [Fact]
    public void Solve_UsesPoleVectorToChooseBendSide()
    {
        var skeleton = CreateChainSkeleton();
        var positiveLocalPose = skeleton.CreateLocalBindPose();
        var positiveModelPose = new SkeletonPoseModel(skeleton);
        var negativeLocalPose = skeleton.CreateLocalBindPose();
        var negativeModelPose = new SkeletonPoseModel(skeleton);

        IkSolverTwoBone.Solve(
            positiveLocalPose,
            positiveModelPose,
            new TwoBoneIkConstraint(0, 1, 2, new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f)));
        IkSolverTwoBone.Solve(
            negativeLocalPose,
            negativeModelPose,
            new TwoBoneIkConstraint(0, 1, 2, new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f)));

        positiveModelPose.UpdateFromLocalPose(positiveLocalPose);
        negativeModelPose.UpdateFromLocalPose(negativeLocalPose);

        Assert.True(positiveModelPose.GetTransform(1).Translation.Y > 0f);
        Assert.True(negativeModelPose.GetTransform(1).Translation.Y < 0f);
    }

    [Fact]
    public void Solve_ThrowsWhenChainIsNotParentChildParent()
    {
        var skeleton = CreateChainSkeleton();
        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        var constraint = new TwoBoneIkConstraint(0, 2, 1, new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f));

        Assert.Throws<ArgumentException>(() => IkSolverTwoBone.Solve(localPose, modelPose, constraint));
    }

    private static SkeletonDefinition CreateChainSkeleton()
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition(
                    "Root",
                    -1,
                    new BoneTransform(Vector3.Zero, Quaternion.Identity, Vector3.One),
                    Matrix.Identity),
                new SkeletonJointDefinition(
                    "Mid",
                    0,
                    new BoneTransform(new Vector3(1f, 0f, 0f), Quaternion.Identity, Vector3.One),
                    Matrix.Identity),
                new SkeletonJointDefinition(
                    "End",
                    1,
                    new BoneTransform(new Vector3(1f, 0f, 0f), Quaternion.Identity, Vector3.One),
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