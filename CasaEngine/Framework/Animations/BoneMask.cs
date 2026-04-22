namespace CasaEngine.Framework.Animations;

public sealed class BoneMask
{
    private readonly float[] _weights;

    public BoneMask(SkeletonDefinition skeleton)
    {
        Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
        _weights = new float[skeleton.Count];
    }

    public SkeletonDefinition Skeleton { get; }

    public int Count => _weights.Length;

    public static BoneMask CreateFullBody(SkeletonDefinition skeleton)
    {
        var mask = new BoneMask(skeleton);
        for (var jointIndex = 0; jointIndex < skeleton.Count; jointIndex++)
        {
            mask._weights[jointIndex] = 1f;
        }

        return mask;
    }

    public float GetWeight(int jointIndex)
    {
        ValidateJointIndex(jointIndex);
        return _weights[jointIndex];
    }

    public void SetWeight(int jointIndex, float weight)
    {
        ValidateJointIndex(jointIndex);
        _weights[jointIndex] = Math.Clamp(weight, 0f, 1f);
    }

    public void SetWeightRecursive(int jointIndex, float weight)
    {
        ValidateJointIndex(jointIndex);
        SetWeight(jointIndex, weight);

        for (var childIndex = 0; childIndex < Skeleton.Count; childIndex++)
        {
            if (Skeleton.GetJoint(childIndex).ParentIndex == jointIndex)
            {
                SetWeightRecursive(childIndex, weight);
            }
        }
    }

    private void ValidateJointIndex(int jointIndex)
    {
        if ((uint)jointIndex >= (uint)_weights.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(jointIndex));
        }
    }
}