namespace CasaEngine.Framework.Animations;

public sealed class LinearBlendAnimationNode : IAnimationGraphRuntimeNode
{
    private readonly SkeletonPoseLocal _sourcePose;
    private readonly SkeletonPoseLocal _targetPose;

    public LinearBlendAnimationNode(IAnimationGraphNode source, IAnimationGraphNode target, float weight = 0.5f)
        : this(new[] { source, target }, weight)
    {
    }

    public LinearBlendAnimationNode(IReadOnlyList<IAnimationGraphNode> inputs, float parameter = 0f)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count < 2)
        {
            throw new ArgumentException("Linear blend nodes require at least two inputs.", nameof(inputs));
        }

        for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
        {
            ArgumentNullException.ThrowIfNull(inputs[inputIndex]);
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
        _sourcePose = Skeleton.CreateLocalBindPose();
        _targetPose = Skeleton.CreateLocalBindPose();
        Parameter = parameter;
    }

    public IReadOnlyList<IAnimationGraphNode> Inputs { get; }

    public IAnimationGraphNode Source => Inputs[0];

    public IAnimationGraphNode Target => Inputs[1];

    public SkeletonDefinition Skeleton { get; }

    public float Parameter { get; set; }

    public float Weight
    {
        get => Parameter;
        set => Parameter = value;
    }

    public void Advance(float elapsedSeconds)
    {
        for (var inputIndex = 0; inputIndex < Inputs.Count; inputIndex++)
        {
            AdvanceNode(Inputs[inputIndex], elapsedSeconds);
        }
    }

    public void Evaluate(SkeletonPoseLocal outputPose)
    {
        ArgumentNullException.ThrowIfNull(outputPose);

        if (!ReferenceEquals(outputPose.Skeleton, Skeleton))
        {
            throw new ArgumentException("The output pose targets a different skeleton.", nameof(outputPose));
        }

        if (Inputs.Count == 2)
        {
            Source.Evaluate(_sourcePose);
            Target.Evaluate(_targetPose);
            AnimationPoseBlender.Blend(_sourcePose, _targetPose, Math.Clamp(Parameter, 0f, 1f), outputPose);
            return;
        }

        var clampedParameter = Math.Clamp(Parameter, 0f, Inputs.Count - 1f);
        var lowerIndex = (int)MathF.Floor(clampedParameter);
        var upperIndex = Math.Min(lowerIndex + 1, Inputs.Count - 1);
        if (lowerIndex == upperIndex)
        {
            Inputs[lowerIndex].Evaluate(outputPose);
            return;
        }

        Inputs[lowerIndex].Evaluate(_sourcePose);
        Inputs[upperIndex].Evaluate(_targetPose);
        AnimationPoseBlender.Blend(_sourcePose, _targetPose, clampedParameter - lowerIndex, outputPose);
    }

    private static void AdvanceNode(IAnimationGraphNode node, float elapsedSeconds)
    {
        if (node is IAnimationGraphRuntimeNode runtimeNode)
        {
            runtimeNode.Advance(elapsedSeconds);
        }
    }
}