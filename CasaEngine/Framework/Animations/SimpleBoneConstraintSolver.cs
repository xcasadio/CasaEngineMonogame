using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public static class SimpleBoneConstraintSolver
{
    public static bool Apply(
        SkeletonPoseLocal localPose,
        SkeletonPoseModel modelPose,
        BoneRotationConstraint constraint)
    {
        ArgumentNullException.ThrowIfNull(localPose);
        ArgumentNullException.ThrowIfNull(modelPose);

        if (!ReferenceEquals(localPose.Skeleton, modelPose.Skeleton))
        {
            throw new ArgumentException("The local pose and model pose must target the same skeleton.", nameof(modelPose));
        }

        if (!constraint.Enabled || constraint.Weight <= 0f)
        {
            return false;
        }

        if ((uint)constraint.JointIndex >= (uint)localPose.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(constraint));
        }

        var currentTransform = localPose.GetTransform(constraint.JointIndex);
        var currentRotation = NormalizeRotation(currentTransform.Rotation);
        var targetRotation = NormalizeRotation(constraint.TargetLocalRotation);
        var blendedRotation = Quaternion.Slerp(currentRotation, targetRotation, Math.Clamp(constraint.Weight, 0f, 1f));

        localPose.SetTransform(constraint.JointIndex, currentTransform with { Rotation = blendedRotation });
        modelPose.UpdateFromLocalPose(localPose);
        return true;
    }

    private static Quaternion NormalizeRotation(Quaternion rotation)
    {
        return rotation.LengthSquared() <= float.Epsilon
            ? Quaternion.Identity
            : Quaternion.Normalize(rotation);
    }
}