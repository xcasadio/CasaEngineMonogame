using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public static class IkSolverLookAt
{
    private const float DirectionEpsilon = 1e-6f;

    public static bool Solve(
        SkeletonPoseLocal localPose,
        SkeletonPoseModel modelPose,
        LookAtConstraint constraint)
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

        modelPose.UpdateFromLocalPose(localPose);

        var jointTransform = BoneTransform.FromMatrix(modelPose.GetTransform(constraint.JointIndex));
        var currentRotation = NormalizeRotation(jointTransform.Rotation);
        var currentForward = Vector3.Transform(SafeNormalize(constraint.LocalForwardAxis, Vector3.UnitZ), currentRotation);
        var currentUp = Vector3.Transform(SafeNormalize(constraint.LocalUpAxis, Vector3.UnitY), currentRotation);
        var targetDirection = constraint.TargetPosition - jointTransform.Translation;
        if (targetDirection.LengthSquared() <= DirectionEpsilon)
        {
            return false;
        }

        var desiredForward = SafeNormalize(targetDirection, currentForward);
        var forwardDelta = CreateRotationBetweenVectors(currentForward, desiredForward);
        var alignedRotation = Quaternion.Normalize(forwardDelta * currentRotation);
        currentUp = Vector3.Transform(SafeNormalize(constraint.LocalUpAxis, Vector3.UnitY), alignedRotation);

        var desiredUp = ProjectOntoPlane(constraint.WorldUp, desiredForward);
        if (desiredUp.LengthSquared() <= DirectionEpsilon)
        {
            desiredUp = ProjectOntoPlane(currentUp, desiredForward);
        }

        var currentProjectedUp = ProjectOntoPlane(currentUp, desiredForward);
        if (currentProjectedUp.LengthSquared() <= DirectionEpsilon)
        {
            currentProjectedUp = FindOrthogonal(desiredForward);
        }

        desiredUp = SafeNormalize(desiredUp, currentProjectedUp);
        currentProjectedUp = SafeNormalize(currentProjectedUp, desiredUp);

        var twistDelta = CreateRotationAroundAxis(currentProjectedUp, desiredUp, desiredForward);
        var fullDelta = Quaternion.Normalize(twistDelta * forwardDelta);
        var blendedDelta = Quaternion.Slerp(Quaternion.Identity, fullDelta, Math.Clamp(constraint.Weight, 0f, 1f));
        if (IsIdentity(blendedDelta))
        {
            return false;
        }

        ApplyModelSpaceRotation(localPose, modelPose, constraint.JointIndex, blendedDelta);
        modelPose.UpdateFromLocalPose(localPose);
        return true;
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
        var from = SafeNormalize(fromDirection, Vector3.UnitZ);
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
        return Quaternion.CreateFromAxisAngle(axis, MathF.Acos(dot));
    }

    private static Quaternion CreateRotationAroundAxis(Vector3 fromDirection, Vector3 toDirection, Vector3 axis)
    {
        var from = SafeNormalize(fromDirection, FindOrthogonal(axis));
        var to = SafeNormalize(toDirection, from);
        var normalizedAxis = SafeNormalize(axis, Vector3.UnitY);
        var dot = MathHelper.Clamp(Vector3.Dot(from, to), -1f, 1f);
        if (dot >= 1f - DirectionEpsilon)
        {
            return Quaternion.Identity;
        }

        var angle = MathF.Acos(dot);
        var cross = Vector3.Cross(from, to);
        if (Vector3.Dot(cross, normalizedAxis) < 0f)
        {
            angle = -angle;
        }

        return Quaternion.CreateFromAxisAngle(normalizedAxis, angle);
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
                ? Vector3.UnitZ
                : Vector3.Normalize(fallback);
        }

        return Vector3.Normalize(vector);
    }

    private static Vector3 FindOrthogonal(Vector3 vector)
    {
        var normalized = SafeNormalize(vector, Vector3.UnitY);
        var axis = MathF.Abs(normalized.Y) < 0.8f ? Vector3.UnitY : Vector3.UnitX;
        var orthogonal = Vector3.Cross(normalized, axis);
        if (orthogonal.LengthSquared() <= DirectionEpsilon)
        {
            orthogonal = Vector3.Cross(normalized, Vector3.UnitZ);
        }

        return SafeNormalize(orthogonal, Vector3.UnitX);
    }

    private static Quaternion NormalizeRotation(Quaternion rotation)
    {
        return rotation.LengthSquared() <= float.Epsilon
            ? Quaternion.Identity
            : Quaternion.Normalize(rotation);
    }

    private static bool IsIdentity(Quaternion rotation)
    {
        return MathF.Abs(rotation.X) <= DirectionEpsilon
               && MathF.Abs(rotation.Y) <= DirectionEpsilon
               && MathF.Abs(rotation.Z) <= DirectionEpsilon
               && MathF.Abs(rotation.W - 1f) <= DirectionEpsilon;
    }
}