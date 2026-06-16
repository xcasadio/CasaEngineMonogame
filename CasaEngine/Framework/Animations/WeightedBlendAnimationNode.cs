namespace CasaEngine.Framework.Animations;

/// <summary>
/// Blends an arbitrary number of input poses according to per-input weights.
/// The weights are normalized by their sum, so <c>result = Σ wᵢ·poseᵢ / Σ wᵢ</c>.
/// When every weight is zero the node falls back to the bind pose.
/// </summary>
public sealed class WeightedBlendAnimationNode : IAnimationGraphRuntimeNode
{
    private readonly SkeletonPoseLocal[] _inputPoses;
    private readonly float[] _weights;

    public WeightedBlendAnimationNode(IReadOnlyList<IAnimationGraphNode> inputs)
        : this(inputs, null)
    {
    }

    public WeightedBlendAnimationNode(IReadOnlyList<IAnimationGraphNode> inputs, IReadOnlyList<float> weights)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count < 1)
        {
            throw new ArgumentException("Weighted blend nodes require at least one input.", nameof(inputs));
        }

        for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
        {
            ArgumentNullException.ThrowIfNull(inputs[inputIndex]);
        }

        if (weights != null && weights.Count != inputs.Count)
        {
            throw new ArgumentException("The weight count must match the input count.", nameof(weights));
        }

        var skeleton = inputs[0].Skeleton;
        for (var inputIndex = 1; inputIndex < inputs.Count; inputIndex++)
        {
            if (!ReferenceEquals(skeleton, inputs[inputIndex].Skeleton))
            {
                throw new ArgumentException("Blend graph nodes must target the same skeleton.", nameof(inputs));
            }
        }

        Inputs = inputs;
        Skeleton = skeleton;
        _inputPoses = new SkeletonPoseLocal[inputs.Count];
        _weights = new float[inputs.Count];

        for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
        {
            _inputPoses[inputIndex] = Skeleton.CreateLocalBindPose();
            _weights[inputIndex] = weights != null ? Math.Max(weights[inputIndex], 0f) : 0f;
        }
    }

    public IReadOnlyList<IAnimationGraphNode> Inputs { get; }

    public SkeletonDefinition Skeleton { get; }

    public int Count => Inputs.Count;

    public float GetWeight(int index)
    {
        ValidateIndex(index);
        return _weights[index];
    }

    public void SetWeight(int index, float weight)
    {
        ValidateIndex(index);
        _weights[index] = Math.Max(weight, 0f);
    }

    public void Advance(float elapsedSeconds)
    {
        for (var inputIndex = 0; inputIndex < Inputs.Count; inputIndex++)
        {
            if (Inputs[inputIndex] is IAnimationGraphRuntimeNode runtimeNode)
            {
                runtimeNode.Advance(elapsedSeconds);
            }
        }
    }

    public void Evaluate(SkeletonPoseLocal outputPose)
    {
        ArgumentNullException.ThrowIfNull(outputPose);

        if (!ReferenceEquals(outputPose.Skeleton, Skeleton))
        {
            throw new ArgumentException("The output pose targets a different skeleton.", nameof(outputPose));
        }

        for (var inputIndex = 0; inputIndex < Inputs.Count; inputIndex++)
        {
            Inputs[inputIndex].Evaluate(_inputPoses[inputIndex]);
        }

        AnimationPoseBlender.BlendWeighted(_inputPoses, _weights, outputPose);
    }

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= Inputs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
