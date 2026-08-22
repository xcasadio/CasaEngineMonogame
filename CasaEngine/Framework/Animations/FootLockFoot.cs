namespace CasaEngine.Framework.Animations;

/// <summary>
/// A resolved hip/knee/ankle two-bone chain for <see cref="FootLockController"/>, expressed as
/// joint indices into a <see cref="SkeletonDefinition"/> (root = hip, mid = knee, end = ankle).
/// </summary>
public readonly record struct FootLockFoot(int RootJointIndex, int MidJointIndex, int EndJointIndex)
{
    /// <summary>
    /// Builds a chain by walking the parent chain up from the ankle joint twice
    /// (ankle -&gt; knee -&gt; hip).
    /// </summary>
    public static FootLockFoot FromAnkle(SkeletonDefinition skeleton, int ankleJointIndex)
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        var ankleJoint = skeleton.GetJoint(ankleJointIndex);
        var midJointIndex = ankleJoint.ParentIndex;
        if (midJointIndex < 0)
        {
            throw new ArgumentException(
                $"Joint '{ankleJoint.Name}' has no parent joint; it cannot be the ankle of a two-bone leg chain.",
                nameof(ankleJointIndex));
        }

        var midJoint = skeleton.GetJoint(midJointIndex);
        var rootJointIndex = midJoint.ParentIndex;
        if (rootJointIndex < 0)
        {
            throw new ArgumentException(
                $"Joint '{midJoint.Name}' has no parent joint; it cannot be the knee of a two-bone leg chain.",
                nameof(ankleJointIndex));
        }

        return new FootLockFoot(rootJointIndex, midJointIndex, ankleJointIndex);
    }

    /// <summary>Builds a chain from the ankle joint's name. See <see cref="FromAnkle"/>.</summary>
    public static FootLockFoot FromAnkleName(SkeletonDefinition skeleton, string ankleJointName)
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        if (!skeleton.TryGetJointIndex(ankleJointName, out var ankleJointIndex))
        {
            throw new ArgumentException($"Joint '{ankleJointName}' was not found in the skeleton.", nameof(ankleJointName));
        }

        return FromAnkle(skeleton, ankleJointIndex);
    }

    /// <summary>Validates that the chain is an immediate parent-child-parent chain in <paramref name="skeleton"/>.</summary>
    public void Validate(SkeletonDefinition skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        var midParentIndex = skeleton.GetJoint(MidJointIndex).ParentIndex;
        var endParentIndex = skeleton.GetJoint(EndJointIndex).ParentIndex;
        if (midParentIndex != RootJointIndex || endParentIndex != MidJointIndex)
        {
            throw new ArgumentException("The foot lock chain must be an immediate parent-child-parent (hip-knee-ankle) chain.");
        }
    }
}
