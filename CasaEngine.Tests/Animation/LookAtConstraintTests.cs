using CasaEngine.Framework.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class LookAtConstraintTests
{
    [Fact]
    public void IkSolverLookAt_RotatesJointTowardTargetDirection()
    {
        var skeleton = CreateSkeleton();
        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        modelPose.UpdateFromLocalPose(localPose);

        var solved = IkSolverLookAt.Solve(
            localPose,
            modelPose,
            LookAtConstraint.CreateDefault(1, new Vector3(10f, 1f, 0f)));

        Assert.True(solved);
        modelPose.UpdateFromLocalPose(localPose);

        var headRotation = BoneTransform.FromMatrix(modelPose.GetTransform(1)).Rotation;
        var headForward = Vector3.Transform(Vector3.UnitZ, headRotation);
        var expectedDirection = Vector3.Normalize(new Vector3(10f, 0f, 0f));

        Assert.True(Vector3.Dot(headForward, expectedDirection) > 0.99f);
    }

    [Fact]
    public void SimpleBoneConstraintSolver_BlendsTowardTargetLocalRotation()
    {
        var skeleton = CreateSkeleton();
        var localPose = skeleton.CreateLocalBindPose();
        var modelPose = new SkeletonPoseModel(skeleton);
        modelPose.UpdateFromLocalPose(localPose);

        var targetRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver2);
        var applied = SimpleBoneConstraintSolver.Apply(
            localPose,
            modelPose,
            new BoneRotationConstraint(1, targetRotation, 0.5f));

        Assert.True(applied);

        var localRotation = localPose.GetTransform(1).Rotation;
        var forward = Vector3.Transform(Vector3.UnitZ, localRotation);
        var expected = Vector3.Transform(Vector3.UnitZ, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver4));

        Assert.True(Vector3.Dot(Vector3.Normalize(forward), Vector3.Normalize(expected)) > 0.99f);
    }

    private static SkeletonDefinition CreateSkeleton()
    {
        return new SkeletonDefinition(
            new[]
            {
                new SkeletonJointDefinition("Root", -1, new BoneTransform(new Vector3(0f, 1f, 0f), Quaternion.Identity, Vector3.One), Matrix.Identity, 0),
                new SkeletonJointDefinition("Head", 0, new BoneTransform(Vector3.Zero, Quaternion.Identity, Vector3.One), Matrix.Identity, 1),
            });
    }
}