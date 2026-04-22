using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Animations;

public static class AnimationPoseBlender
{
    public static void Blend(SkeletonPoseLocal sourcePose, SkeletonPoseLocal targetPose, float weight, SkeletonPoseLocal destination)
    {
        ArgumentNullException.ThrowIfNull(sourcePose);
        ArgumentNullException.ThrowIfNull(targetPose);
        ArgumentNullException.ThrowIfNull(destination);

        for (var jointIndex = 0; jointIndex < destination.Count; jointIndex++)
        {
            var sourceTransform = sourcePose.GetTransform(jointIndex);
            var targetTransform = targetPose.GetTransform(jointIndex);
            var blendedTransform = new BoneTransform(
                Vector3.Lerp(sourceTransform.Translation, targetTransform.Translation, weight),
                Quaternion.Slerp(sourceTransform.Rotation, targetTransform.Rotation, weight),
                Vector3.Lerp(sourceTransform.Scale, targetTransform.Scale, weight));

            destination.SetTransformDirect(jointIndex, blendedTransform);
        }

        destination.MarkDirtyFrom(0);
    }

    public static void BlendWeighted(IReadOnlyList<SkeletonPoseLocal> poses, IReadOnlyList<float> weights, SkeletonPoseLocal destination)
    {
        ArgumentNullException.ThrowIfNull(poses);
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(destination);

        if (poses.Count == 0)
        {
            throw new ArgumentException("At least one pose is required for weighted blending.", nameof(poses));
        }

        if (poses.Count != weights.Count)
        {
            throw new ArgumentException("The pose and weight counts must match.", nameof(weights));
        }

        for (var poseIndex = 0; poseIndex < poses.Count; poseIndex++)
        {
            if (!ReferenceEquals(poses[poseIndex].Skeleton, destination.Skeleton))
            {
                throw new ArgumentException("All poses must target the same skeleton as the destination pose.", nameof(poses));
            }
        }

        var totalWeight = 0f;
        for (var poseIndex = 0; poseIndex < weights.Count; poseIndex++)
        {
            totalWeight += Math.Max(weights[poseIndex], 0f);
        }

        if (totalWeight <= float.Epsilon)
        {
            destination.CopyFrom(poses[0]);
            return;
        }

        for (var jointIndex = 0; jointIndex < destination.Count; jointIndex++)
        {
            Vector3 blendedTranslation = Vector3.Zero;
            Vector3 blendedScale = Vector3.Zero;
            Vector4 blendedRotation = Vector4.Zero;
            var rotationReference = Quaternion.Identity;
            var hasRotationReference = false;

            for (var poseIndex = 0; poseIndex < poses.Count; poseIndex++)
            {
                var normalizedWeight = Math.Max(weights[poseIndex], 0f) / totalWeight;
                if (normalizedWeight <= 0f)
                {
                    continue;
                }

                var transform = poses[poseIndex].GetTransform(jointIndex);
                blendedTranslation += transform.Translation * normalizedWeight;
                blendedScale += transform.Scale * normalizedWeight;

                var rotation = transform.Rotation.LengthSquared() <= float.Epsilon
                    ? Quaternion.Identity
                    : Quaternion.Normalize(transform.Rotation);

                if (!hasRotationReference)
                {
                    rotationReference = rotation;
                    hasRotationReference = true;
                }
                else if (Quaternion.Dot(rotationReference, rotation) < 0f)
                {
                    rotation = new Quaternion(-rotation.X, -rotation.Y, -rotation.Z, -rotation.W);
                }

                blendedRotation += new Vector4(rotation.X, rotation.Y, rotation.Z, rotation.W) * normalizedWeight;
            }

            var blendedQuaternion = new Quaternion(
                blendedRotation.X,
                blendedRotation.Y,
                blendedRotation.Z,
                blendedRotation.W);
            if (blendedQuaternion.LengthSquared() <= float.Epsilon)
            {
                blendedQuaternion = rotationReference;
            }
            else
            {
                blendedQuaternion = Quaternion.Normalize(blendedQuaternion);
            }

            destination.SetTransformDirect(jointIndex, new BoneTransform(blendedTranslation, blendedQuaternion, blendedScale));
        }

        destination.MarkDirtyFrom(0);
    }
}