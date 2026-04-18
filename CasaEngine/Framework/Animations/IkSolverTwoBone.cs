using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public static class IkSolverTwoBone
{
    private const float LengthEpsilon = 1e-5f;
    private const float DirectionEpsilon = 1e-6f;

    public static bool Solve(
        SkeletonPoseLocal localPose,
        SkeletonPoseModel modelPose,
        TwoBoneIkConstraint constraint)
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

        ValidateConstraint(localPose.Skeleton, constraint);

        modelPose.UpdateFromLocalPose(localPose);

        var rootPosition = modelPose.GetTransform(constraint.RootJointIndex).Translation;
        var midPosition = modelPose.GetTransform(constraint.MidJointIndex).Translation;
        var endPosition = modelPose.GetTransform(constraint.EndJointIndex).Translation;

        var upperVector = midPosition - rootPosition;
        var lowerVector = endPosition - midPosition;
        var upperLength = upperVector.Length();
        var lowerLength = lowerVector.Length();
        if (upperLength <= LengthEpsilon || lowerLength <= LengthEpsilon)
        {
            return false;
        }

        var targetVector = constraint.TargetPosition - rootPosition;
        if (targetVector.LengthSquared() <= DirectionEpsilon)
        {
            targetVector = endPosition - rootPosition;
            if (targetVector.LengthSquared() <= DirectionEpsilon)
            {
                return false;
            }
        }

        var targetDistance = targetVector.Length();
        var targetDirection = SafeNormalize(targetVector, Vector3.UnitX);
        var maxReach = MathF.Max(upperLength + lowerLength - LengthEpsilon, LengthEpsilon);
        var minReach = MathF.Max(MathF.Abs(upperLength - lowerLength) + LengthEpsilon, LengthEpsilon);
        var clampedDistance = Math.Clamp(targetDistance, minReach, maxReach);

        var bendDirection = ComputeBendDirection(
            rootPosition,
            midPosition,
            endPosition,
            constraint.PolePosition,
            targetDirection);

        var upperLengthSquared = upperLength * upperLength;
        var lowerLengthSquared = lowerLength * lowerLength;
        var alongTarget = (upperLengthSquared - lowerLengthSquared + clampedDistance * clampedDistance)
            / (2f * clampedDistance);
        var perpendicularDistance = MathF.Sqrt(MathF.Max(upperLengthSquared - alongTarget * alongTarget, 0f));

        var desiredMidPosition = rootPosition
                                 + targetDirection * alongTarget
                                 + bendDirection * perpendicularDistance;
        var desiredEndPosition = rootPosition + targetDirection * clampedDistance;
        var weight = Math.Clamp(constraint.Weight, 0f, 1f);

        var currentUpperDirection = SafeNormalize(midPosition - rootPosition, targetDirection);
        var desiredUpperDirection = SafeNormalize(desiredMidPosition - rootPosition, currentUpperDirection);
        var rootDelta = Quaternion.Slerp(
            Quaternion.Identity,
            CreateRotationBetweenVectors(currentUpperDirection, desiredUpperDirection),
            weight);
        if (!IsIdentity(rootDelta))
        {
            ApplyModelSpaceRotation(localPose, modelPose, constraint.RootJointIndex, rootDelta);
            modelPose.UpdateFromLocalPose(localPose);
        }

        midPosition = modelPose.GetTransform(constraint.MidJointIndex).Translation;
        endPosition = modelPose.GetTransform(constraint.EndJointIndex).Translation;

        var currentLowerDirection = SafeNormalize(endPosition - midPosition, targetDirection);
        var desiredLowerDirection = SafeNormalize(desiredEndPosition - midPosition, currentLowerDirection);
        var midDelta = Quaternion.Slerp(
            Quaternion.Identity,
            CreateRotationBetweenVectors(currentLowerDirection, desiredLowerDirection),
            weight);
        if (!IsIdentity(midDelta))
        {
            ApplyModelSpaceRotation(localPose, modelPose, constraint.MidJointIndex, midDelta);
            modelPose.UpdateFromLocalPose(localPose);
        }

        return true;
    }

    private static void ValidateConstraint(SkeletonDefinition skeleton, TwoBoneIkConstraint constraint)
    {
        var midParentIndex = skeleton.GetJoint(constraint.MidJointIndex).ParentIndex;
        var endParentIndex = skeleton.GetJoint(constraint.EndJointIndex).ParentIndex;
        if (midParentIndex != constraint.RootJointIndex || endParentIndex != constraint.MidJointIndex)
        {
            throw new ArgumentException("The two-bone IK chain must be an immediate parent-child-parent chain.", nameof(constraint));
        }
    }

    private static Vector3 ComputeBendDirection(
        Vector3 rootPosition,
        Vector3 midPosition,
        Vector3 endPosition,
        Vector3 polePosition,
        Vector3 targetDirection)
    {
        var bendDirection = ProjectOntoPlane(polePosition - rootPosition, targetDirection);
        if (bendDirection.LengthSquared() <= DirectionEpsilon)
        {
            bendDirection = ProjectOntoPlane(midPosition - rootPosition, targetDirection);
        }

        if (bendDirection.LengthSquared() <= DirectionEpsilon)
        {
            bendDirection = ProjectOntoPlane(endPosition - midPosition, targetDirection);
        }

        if (bendDirection.LengthSquared() <= DirectionEpsilon)
        {
            bendDirection = FindOrthogonal(targetDirection);
        }

        return SafeNormalize(bendDirection, FindOrthogonal(targetDirection));
    }

    private static void ApplyModelSpaceRotation(
        SkeletonPoseLocal localPose,
        SkeletonPoseModel modelPose,
        int jointIndex,
        Quaternion modelSpaceDelta)
    {
        var skeleton = localPose.Skeleton;
        var parentIndex = skeleton.GetJoint(jointIndex).ParentIndex;
        var parentModelRotation = parentIndex >= 0
            ? BoneTransform.FromMatrix(modelPose.GetTransform(parentIndex)).Rotation
            : Quaternion.Identity;
        var jointModelRotation = BoneTransform.FromMatrix(modelPose.GetTransform(jointIndex)).Rotation;
        var targetModelRotation = Quaternion.Normalize(modelSpaceDelta * jointModelRotation);
        var targetLocalRotation = Quaternion.Normalize(Quaternion.Inverse(parentModelRotation) * targetModelRotation);

        var currentLocalTransform = localPose.GetTransform(jointIndex);
        localPose.SetTransform(jointIndex, currentLocalTransform with { Rotation = targetLocalRotation });
    }

    private static Quaternion CreateRotationBetweenVectors(Vector3 fromDirection, Vector3 toDirection)
    {
        var from = SafeNormalize(fromDirection, Vector3.UnitX);
        var to = SafeNormalize(toDirection, from);
        var dot = MathHelper.Clamp(Vector3.Dot(from, to), -1f, 1f);

        if (dot >= 1f - DirectionEpsilon)
        {
            return Quaternion.Identity;
        }

        if (dot <= -1f + DirectionEpsilon)
        {
            return Quaternion.CreateFromAxisAngle(FindOrthogonal(from), MathF.PI);
        }

        var axis = Vector3.Cross(from, to);
        if (axis.LengthSquared() <= DirectionEpsilon)
        {
            return Quaternion.Identity;
        }

        axis.Normalize();
        var angle = MathF.Acos(dot);
        return Quaternion.CreateFromAxisAngle(axis, angle);
    }

    private static Vector3 ProjectOntoPlane(Vector3 vector, Vector3 planeNormal)
    {
        return vector - planeNormal * Vector3.Dot(vector, planeNormal);
    }

    private static Vector3 SafeNormalize(Vector3 vector, Vector3 fallback)
    {
        if (vector.LengthSquared() <= DirectionEpsilon)
        {
            return fallback.LengthSquared() <= DirectionEpsilon
                ? Vector3.UnitX
                : Vector3.Normalize(fallback);
        }

        return Vector3.Normalize(vector);
    }

    private static Vector3 FindOrthogonal(Vector3 vector)
    {
        var normalized = SafeNormalize(vector, Vector3.UnitX);
        var axis = MathF.Abs(normalized.X) < 0.8f ? Vector3.UnitX : Vector3.UnitY;
        var orthogonal = Vector3.Cross(normalized, axis);
        if (orthogonal.LengthSquared() <= DirectionEpsilon)
        {
            orthogonal = Vector3.Cross(normalized, Vector3.UnitZ);
        }

        return SafeNormalize(orthogonal, Vector3.UnitY);
    }

    private static bool IsIdentity(Quaternion rotation)
    {
        return MathF.Abs(rotation.X) <= DirectionEpsilon
               && MathF.Abs(rotation.Y) <= DirectionEpsilon
               && MathF.Abs(rotation.Z) <= DirectionEpsilon
               && MathF.Abs(rotation.W - 1f) <= DirectionEpsilon;
    }
}